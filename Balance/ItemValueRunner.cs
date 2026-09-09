using System.Globalization;
using System.Text;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Balance;

/// <summary>Valor medido de un objeto: lo que gana un equipo por llevarlo frente a su espejo sin él.</summary>
/// <param name="Carriers">Titulares que podían llevarlo en la plantilla generada (7 salvo restricción de etiqueta).</param>
/// <param name="ValueMilli">Milésimas de punto de tasa de victoria (RT-023: aritmética entera).</param>
public readonly record struct ItemValueRow(
    string ItemId,
    int Carriers,
    int Matches,
    int Wins,
    int ControlWins,
    int ValueMilli)
{
    public double WinRate => Matches > 0 ? 100.0 * Wins / Matches : 0.0;

    /// <summary>Tasa de victoria del control: las mismas plantillas y semillas sin el objeto (ADR 0087).</summary>
    public double ControlWinRate => Matches > 0 ? 100.0 * ControlWins / Matches : 0.0;
}

/// <summary>
/// Modo <c>--item-values</c>: mide <b>cuánto vale cada objeto</b> con el <b>mismo espejo</b> con el que
/// <see cref="PerkValueRunner"/> mide un perk (AT-A, paso 1).
///
/// <para><b>Por qué existe.</b> Hoy el valor de un objeto se <b>calcula</b>
/// (<c>ItemScale.ValueOf</c>: suma de modificador × valor marginal del atributo) y esa tabla de valor
/// marginal está medida en fase 1b sobre un experimento <b>distinto</b> —un +20 repartido entre los diez
/// jugadores del equipo, no un solo portador—. Las dos cifras están en milésimas de punto de tasa de
/// victoria pero <b>no en la misma escala</b>: por rendimientos decrecientes y por el ajuste de posición,
/// un +20 concentrado en un jugador no responde como el mismo +20 repartido entre diez. Por eso el
/// listón del mercado no puede ser hoy "el del slot MÁS el del oro" (AT-A): las dos columnas no se
/// pueden restar. Este instrumento mide el objeto <b>exactamente como se mide un perk</b>, y con eso las
/// dos tablas quedan en la misma unidad.</para>
///
/// <para><b>Cómo se mide.</b> Espejo puro, idéntico al de los perks: dos plantillas de la misma raza,
/// calidad (<see cref="Quality"/>) y nivel (<see cref="Level"/>), generadas con dados distintos. El
/// equipo A pone el objeto sobre <b>uno de sus titulares elegibles</b> y el B no lleva nada; sobre el 50%
/// de partida, lo que suba A <b>es</b> lo que vale el objeto. Un objeto restringido se mide sobre una
/// plantilla de <b>su</b> raza, igual que un perk exclusivo (<c>ItemCatalog.OfferableTo</c>: sólo entra
/// en el pool de una run de esa raza).</para>
///
/// <para><b>Y NO se mide en campaña</b>, al contrario que un perk (ADR 0070). Un objeto es un paquete de
/// atributos estático: <see cref="ItemDefinition"/> no tiene <c>accumulatesAcrossMatches</c> ni ningún
/// contador de carrera, así que vale lo mismo en el partido 1 que en el 8 y arrastrar el estado entre
/// partidos mediría un eje que no existe. Cada pareja de plantillas juega
/// <see cref="DefaultMatchesPerRoster"/> partidos <b>independientes</b> —ida y vuelta, para que el
/// factor campo se cancele exactamente en cada pareja— sin arrastrar nada. Por la misma razón no hay
/// curva por horizonte (AV-B): la de los perks existe porque su contador crece, y aquí no hay contador.
/// </para>
///
/// <para><b>El portador rota</b> entre los titulares que pueden llevarlo, con el mismo criterio que
/// <see cref="PerkValueRunner"/>: medir siempre sobre el slot 0 pondría todos los objetos en el portero.
/// En un objeto la rotación pesa más que en un perk, porque un objeto universal lo puede llevar
/// cualquiera de los siete: lo que se mide es lo que vale el objeto <b>cuando cae</b>, promediado sobre
/// las posiciones, no lo que vale colocado por un entrenador que sabe dónde ponerlo. Es la misma
/// convención de la tabla de perks, que es justo lo que AT-A necesita.</para>
///
/// <para><b>Qué queda deliberadamente fuera.</b> La rotura del frágil (RF-077) se resuelve <b>entre</b>
/// partidos sobre el <c>RunState</c>, así que dentro del espejo un frágil rinde como uno normal: lo que
/// esta tabla mide es lo que el objeto hace mientras está puesto, y su durabilidad esperada sigue
/// descontándose donde ya estaba, en el precio (<c>ItemScale.FragilePricePercent</c>). Los malditos se
/// miden igual que el resto y salen con el signo que les toque: la contrapartida se aplica siempre.</para>
///
/// <para><b>Precisión.</b> Son <c>rosters × matchesPerRoster</c> partidos por objeto. La desviación por
/// fila de una tasa de victoria con N partidos es del orden de <c>1000/√N</c> milésimas: 63 con 256
/// partidos, 31 con 1.024. Como en los perks, la tabla <b>ordena</b>, no dictamina.</para>
/// </summary>
public static class ItemValueRunner
{
    /// <summary>Raza con la que se miden los objetos universales: la misma que usan los perks universales.</summary>
    public const Race NeutralRace = PerkValueRunner.NeutralRace;

    /// <summary>Nivel de la plantilla de prueba: el mismo que el de la tabla de perks, para que las dos columnas sean comparables.</summary>
    public const int Level = PerkValueRunner.Level;

    /// <summary>Calidad de la plantilla de prueba: la misma que la de la tabla de perks.</summary>
    public const int Quality = PerkValueRunner.Quality;

    /// <summary>
    /// Partidos por pareja de plantillas: ida y vuelta. No es una campaña —no se arrastra nada entre
    /// ellos— sino la forma de que cada portador juegue el mismo número de partidos en casa y fuera.
    /// Por eso conviene que sea par.
    /// </summary>
    public const int DefaultMatchesPerRoster = 2;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    /// <summary>Mide todo el catálogo de objetos, en orden de id ordinal ascendente (RT-041).</summary>
    public static IReadOnlyList<ItemValueRow> Run(
        Catalog catalog, ItemCatalog items, ulong seed, int rosters, int matchesPerRoster = DefaultMatchesPerRoster)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(rosters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(matchesPerRoster, 1);

        var rows = new List<ItemValueRow>();
        int itemIndex = 0;

        // ItemCatalog.All ya viene ordenado por id ordinal ascendente.
        foreach (var item in items.All)
        {
            var row = Measure(catalog, item, seed, rosters, matchesPerRoster, itemIndex++);
            if (row is { } measured)
            {
                rows.Add(measured);
            }
        }

        return rows;
    }

    /// <summary>Lo que sacó una pareja de plantillas: portadores elegibles y victorias. <c>null</c> si nadie podía llevarlo.</summary>
    private sealed record RosterOutcome(int Eligible, int Wins, int ControlWins);

    private static ItemValueRow? Measure(
        Catalog catalog, ItemDefinition item, ulong seed, int rosters, int matchesPerRoster, int itemIndex)
    {
        var race = item.Race ?? NeutralRace;

        // La misma conversión que usa el bucle de run (RunEquipment.ToMatchItem), así que /Balance mide
        // el objeto que juega y no una copia hecha aquí.
        var matchItem = RunEquipment.ToMatchItem(item);

        // Las parejas de plantillas son independientes entre sí —aquí no hay campaña que arrastrar— y sus
        // dados (RngStreams.Generation(seed, itemIndex*1000 + roster)) y semillas de partido
        // (RngStreams.MatchSeed con el índice global itemIndex*100_000 + ...) son función pura del índice,
        // nunca de un contador que avance con la ejecución. Por eso se juegan en paralelo escribiendo por
        // índice, con la reducción después en orden de índice —incluida la de portadores, que es un
        // mínimo— para que el resultado sea bit a bit el mismo que el del bucle secuencial (RT-020..024).
        var played = new RosterOutcome?[rosters];
        Parallel.For(0, rosters, roster =>
        {
            // Catálogo por hilo (BalanceCatalogs): las condiciones compiladas de los perks no son
            // reentrantes. El dato es el mismo, así que la medida no cambia.
            played[roster] = PlayRoster(
                BalanceCatalogs.Current(catalog), item, matchItem, race, seed, roster, matchesPerRoster, itemIndex);
        });

        int matches = 0, wins = 0, controlWins = 0, carriers = 0;
        for (int roster = 0; roster < rosters; roster++)
        {
            if (played[roster] is not { } outcome)
            {
                // Ningún titular generado puede llevarlo: no se mide y se queda sin entrada, igual que
                // un perk sin portador posible. Es más honesto que medirlo sobre un portador imposible,
                // donde el objeto no aportaría nada (MatchItem.AppliesTo) y saldría valiendo cero. El
                // bucle secuencial abandonaba en la primera plantilla sin portador; el resultado es el
                // mismo, porque la fila entera se descarta venga de la plantilla que venga.
                return null;
            }

            carriers = carriers == 0 ? outcome.Eligible : Math.Min(carriers, outcome.Eligible);
            matches += matchesPerRoster;
            wins += outcome.Wins;
            controlWins += outcome.ControlWins;
        }

        // Diferencia emparejada contra el control (ADR 0087): quita el sesgo de la pareja de plantillas.
        int valueMilli = matches > 0 ? (int)Math.Round(1000.0 * (wins - controlWins) / matches * 2.0) : 0;
        return new ItemValueRow(item.Id, carriers, matches, wins, controlWins, valueMilli);
    }

    /// <summary>Los <paramref name="matchesPerRoster"/> partidos independientes de una pareja de plantillas (ida y vuelta).</summary>
    private static RosterOutcome? PlayRoster(
        Catalog catalog,
        ItemDefinition item,
        MatchItem matchItem,
        Race race,
        ulong seed,
        int roster,
        int matchesPerRoster,
        int itemIndex)
    {
        var config = new SimConfig(CollectLog: false);

        // Misma generación de espejo que PerkValueRunner.PlayRoster, deliberadamente duplicada: es
        // código privado de aquel modo y extraerlo obligaría a tocar el instrumento de perks, que
        // sostiene una tabla ya medida. Cuatro líneas duplicadas cuestan menos que ese riesgo.
        var subjectRng = RngStreams.Generation(seed, (itemIndex * 1000) + roster);
        var mirrorRng = RngStreams.Generation(seed, (itemIndex * 1000) + 500 + roster);
        var subject = TeamGenerator.Generate(ref subjectRng, catalog, "subject", race, Quality, 1, Level);
        var mirror = TeamGenerator.Generate(ref mirrorRng, catalog, "mirror", race, Quality, 100001, Level);

        var eligible = EligibleStarters(subject, item, matchItem);
        if (eligible.Count == 0)
        {
            return null;
        }

        int carrier = eligible[roster % eligible.Count];
        var players = subject.Players.ToList();
        players[carrier] = players[carrier] with { Item = matchItem };
        var armed = subject with { Players = players };

        // Dos brazos con las MISMAS plantillas y semillas: con el objeto y sin él (control, ADR 0087).
        int wins = 0, controlWins = 0;
        for (int k = 0; k < matchesPerRoster; k++)
        {
            bool subjectAway = (k % 2) == 1;
            int subjectSide = subjectAway ? 1 : 0;
            ulong matchSeed = RngStreams.MatchSeed(seed, (itemIndex * 100_000) + (roster * matchesPerRoster) + k);

            var armedResult = Simulator.Run(
                subjectAway ? new MatchSetup(mirror, armed, Referee) : new MatchSetup(armed, mirror, Referee),
                matchSeed, catalog, config);
            if (armedResult.Report.Winner == subjectSide)
            {
                wins++;
            }

            var controlResult = Simulator.Run(
                subjectAway ? new MatchSetup(mirror, subject, Referee) : new MatchSetup(subject, mirror, Referee),
                matchSeed, catalog, config);
            if (controlResult.Report.Winner == subjectSide)
            {
                controlWins++;
            }

            // Aquí NO se arrastra nada al partido siguiente (a diferencia de la campaña de la
            // ADR 0070): un objeto no tiene contador de carrera que arrastrar.
        }

        return new RosterOutcome(eligible.Count, wins, controlWins);
    }

    /// <summary>
    /// Titulares (slot 0..6) sobre los que el objeto <b>surte efecto</b>, con los dos filtros que usa el
    /// juego: el de la run (<c>ItemCatalog.OfferableTo</c>: un restringido sólo entra en una run de su
    /// raza) y el del partido (<see cref="MatchItem.AppliesTo"/>: si el portador no lleva la etiqueta
    /// exigida, el objeto no aporta nada).
    /// </summary>
    private static List<int> EligibleStarters(TeamSetup team, ItemDefinition item, MatchItem matchItem)
    {
        var slots = new List<int>(7);
        for (int i = 0; i < 7 && i < team.Players.Count; i++)
        {
            var player = team.Players[i];
            if ((item.Race is null || item.Race == player.Race) && matchItem.AppliesTo(player))
            {
                slots.Add(i);
            }
        }

        return slots;
    }

    /// <summary>
    /// El bloque <c>values</c> de <c>data/economy/item-values.json</c>, listo para pegar. No se escribe
    /// el fichero desde aquí, con el mismo criterio que <see cref="PerkValueRunner.ToJsonValues"/>:
    /// <c>/Balance</c> no toca <c>/data</c> (los valores de balance los cambia una persona, con su ADR si
    /// hace falta, RT-057).
    /// </summary>
    public static string ToJsonValues(IReadOnlyList<ItemValueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var builder = new StringBuilder();
        builder.AppendLine("  \"values\": {");
        var ordered = rows.OrderBy(r => r.ItemId, StringComparer.Ordinal).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            builder.Append("    \"").Append(ordered[i].ItemId).Append("\": ")
                .Append(ordered[i].ValueMilli.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(i == ordered.Count - 1 ? string.Empty : ",");
        }

        builder.AppendLine("  }");
        return builder.ToString();
    }

    public static void PrintTable(IReadOnlyList<ItemValueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Console.WriteLine("valor medido por objeto (AT-A): milésimas de punto de tasa de victoria, mismo espejo que --perk-values");
        Console.WriteLine($"{"objeto",-26} {"portadores",10} {"partidos",9} {"victoria%",10} {"valor",8}");
        foreach (var row in rows.OrderByDescending(r => r.ValueMilli).ThenBy(r => r.ItemId, StringComparer.Ordinal))
        {
            Console.WriteLine($"{row.ItemId,-26} {row.Carriers,10} {row.Matches,9} {row.WinRate,10:F2} {row.ValueMilli,8}");
        }
    }
}
