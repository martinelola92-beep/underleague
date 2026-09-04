using System.Globalization;
using System.Text;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Balance;

/// <summary>Valor medido de un perk: lo que gana un equipo por llevarlo frente a su espejo sin él.</summary>
/// <param name="ValueMilli">Milésimas de punto de tasa de victoria (RT-023: aritmética entera).</param>
public readonly record struct PerkValueRow(string PerkId, int Slot, int Matches, int Wins, int ValueMilli)
{
    public double WinRate => Matches > 0 ? 100.0 * Wins / Matches : 0.0;
}

/// <summary>
/// Modo <c>--perk-values</c>: mide **cuánto vale cada perk** (ADR 0038).
///
/// <para>Los objetos se calculan —son un paquete de atributos y hay tabla de valor marginal—; los perks
/// hay que medirlos, y esa medición es parte del lote de balance. El resultado alimenta
/// <c>data/economy/perk-values.json</c>, de donde sale el <b>peso de cada perk en el pool</b>: la palanca
/// de la vía gratuita (RF-071), donde el precio no interviene.</para>
///
/// <para><b>Cómo se mide.</b> Espejo puro: dos plantillas de la misma raza, calidad y nivel, generadas
/// con dados distintos, jugando ida y vuelta. El equipo A lleva el perk sobre el <b>primer titular
/// elegible</b> (mismo filtro de <c>PerkAssignment.Eligible</c> que usa el juego, así que se respeta
/// posición, etiquetas y raza) y el B no lleva nada. Sobre el 50% de partida, lo que suba A <b>es</b> lo
/// que vale el perk. Un perk que ningún titular generado puede llevar —los que exigen una etiqueta que
/// el dado no da— no se mide y se queda sin entrada: pesa lo que pese el resto.</para>
///
/// <para><b>Precisión.</b> Con las opciones por defecto son 256 partidos por perk, es decir una
/// desviación de unos 3 puntos por fila. Es suficiente para una palanca de <b>frecuencia</b> con pesos
/// acotados, y no lo es para afirmar que un perk vale exactamente X: la tabla ordena, no dictamina.</para>
/// </summary>
public static class PerkValueRunner
{
    /// <summary>Raza con la que se miden los perks universales: la que no tiene sesgo de atributos.</summary>
    public const Race NeutralRace = Race.Human;

    /// <summary>Nivel de la plantilla de prueba: el de mitad de run, donde la mayoría de los perks se usan.</summary>
    public const int Level = 4;

    /// <summary>Calidad de la plantilla de prueba (el pivote del generador).</summary>
    public const int Quality = 50;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    public static IReadOnlyList<PerkValueRow> Run(Catalog catalog, ulong seed, int rosters, int matchesPerRoster)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfLessThan(rosters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(matchesPerRoster, 1);

        var rows = new List<PerkValueRow>();
        int perkIndex = 0;
        foreach (var perk in catalog.Perks.All.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var race = perk.Race ?? NeutralRace;

            // La habilidad racial no ocupa slot y no se puede repartir (ADR 0026): no entra en el pool.
            if (string.Equals(perk.Id, catalog.Race(race).Ability, StringComparison.Ordinal))
            {
                perkIndex++;
                continue;
            }

            var row = Measure(catalog, perk, race, seed, rosters, matchesPerRoster, perkIndex++);
            if (row is { } measured)
            {
                rows.Add(measured);
            }
        }

        return rows;
    }

    private static PerkValueRow? Measure(
        Catalog catalog, PerkDefinition perk, Race race, ulong seed, int rosters, int matchesPerRoster, int perkIndex)
    {
        var config = new SimConfig(CollectLog: false);
        int matches = 0, wins = 0, slot = -1;

        for (int roster = 0; roster < rosters; roster++)
        {
            var subjectRng = RngStreams.Generation(seed, (perkIndex * 1000) + roster);
            var mirrorRng = RngStreams.Generation(seed, (perkIndex * 1000) + 500 + roster);
            var subject = TeamGenerator.Generate(ref subjectRng, catalog, "subject", race, Quality, 1, Level);
            var mirror = TeamGenerator.Generate(ref mirrorRng, catalog, "mirror", race, Quality, 100001, Level);

            var eligible = EligibleStarters(subject, perk, catalog);
            if (eligible.Count == 0)
            {
                // Ningún titular generado puede llevarlo (etiqueta que el dado no dio): no se mide.
                return null;
            }

            // El portador rota entre los titulares que pueden llevarlo: medir siempre sobre el slot 0
            // pondría todos los perks en el portero, que es exactamente donde ninguno significa nada.
            // Lo que interesa es lo que vale el perk CUANDO CAE, y cae en cualquiera de los suyos.
            int carrier = eligible[roster % eligible.Count];
            slot = -1;
            var players = subject.Players.ToList();
            players[carrier] = players[carrier] with { Perks = new[] { perk.Id } };
            subject = subject with { Players = players };

            for (int k = 0; k < matchesPerRoster; k++)
            {
                bool subjectAway = (k % 2) == 1;
                int subjectSide = subjectAway ? 1 : 0;
                var setup = subjectAway
                    ? new MatchSetup(mirror, subject, Referee)
                    : new MatchSetup(subject, mirror, Referee);

                var report = Simulator.Run(
                    setup,
                    RngStreams.MatchSeed(seed, (perkIndex * 100_000) + (roster * matchesPerRoster) + k),
                    catalog,
                    config).Report;

                matches++;
                if (report.Winner == subjectSide)
                {
                    wins++;
                }
            }
        }

        int valueMilli = matches > 0 ? (int)Math.Round(((1000.0 * wins / matches) - 500.0) * 2.0) : 0;
        return new PerkValueRow(perk.Id, slot, matches, wins, valueMilli);
    }

    /// <summary>
    /// Titulares (slot 0..6) que pueden llevar el perk con el mismo filtro que el juego. Vacía si
    /// ninguno puede: entonces el perk no es medible con una plantilla generada y se queda fuera de la
    /// tabla, que es más honesto que medirlo sobre un portador imposible.
    /// </summary>
    private static List<int> EligibleStarters(TeamSetup team, PerkDefinition perk, Catalog catalog)
    {
        var slots = new List<int>(7);
        for (int i = 0; i < 7 && i < team.Players.Count; i++)
        {
            foreach (var candidate in PerkAssignment.Eligible(team.Players[i], catalog))
            {
                if (string.Equals(candidate.Id, perk.Id, StringComparison.Ordinal))
                {
                    slots.Add(i);
                    break;
                }
            }
        }

        return slots;
    }

    /// <summary>
    /// El bloque <c>values</c> de <c>data/economy/perk-values.json</c>, listo para pegar. No se escribe el
    /// fichero desde aquí: <c>/Balance</c> no toca <c>/data</c> (los valores de balance los cambia una
    /// persona, con su ADR si hace falta, RT-057).
    /// </summary>
    public static string ToJsonValues(IReadOnlyList<PerkValueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder();
        builder.AppendLine("  \"values\": {");
        var ordered = rows.OrderBy(r => r.PerkId, StringComparer.Ordinal).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            builder.Append("    \"").Append(ordered[i].PerkId).Append("\": ")
                .Append(ordered[i].ValueMilli.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(i == ordered.Count - 1 ? string.Empty : ",");
        }

        builder.AppendLine("  }");
        return builder.ToString();
    }

    public static void PrintTable(IReadOnlyList<PerkValueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Console.WriteLine("valor medido por perk (ADR 0038): milésimas de punto de tasa de victoria");
        Console.WriteLine($"{"perk",-26} {"slot",4} {"partidos",9} {"victoria%",10} {"valor",8}");
        foreach (var row in rows.OrderByDescending(r => r.ValueMilli).ThenBy(r => r.PerkId, StringComparer.Ordinal))
        {
            Console.WriteLine($"{row.PerkId,-26} {row.Slot,4} {row.Matches,9} {row.WinRate,10:F2} {row.ValueMilli,8}");
        }
    }
}
