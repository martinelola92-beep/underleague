using System.Globalization;
using System.Text;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Balance;

/// <summary>
/// Contadores agregados del lado del <b>sujeto</b> y de su <b>espejo</b> en los mismos partidos (paquete
/// AY, paso 3). No entran en la tabla ni en ninguna puerta: son el diagnóstico de <b>por qué</b> un perk
/// mide lo que mide, y sobre todo <see cref="Activations"/>, que dice si la condición llega a cumplirse.
/// Todo son sumas sobre los partidos medidos; se leen divididas por <c>Matches</c>.
/// </summary>
public readonly record struct PerkDiagnostics(
    long Activations,
    long GoalsFor,
    long GoalsAgainst,
    long ShotsFor,
    long ShotsAgainst,
    long TacklesSubject,
    long TacklesMirror,
    long FoulsSubject,
    long FoulsMirror,
    long CardsSubject,
    long CardsMirror,
    long InjuredSubject,
    long InjuredMirror,
    long PossessionChanges,
    long PossessionTicksSubject)
{
    public static PerkDiagnostics operator +(PerkDiagnostics a, PerkDiagnostics b) => new(
        a.Activations + b.Activations,
        a.GoalsFor + b.GoalsFor,
        a.GoalsAgainst + b.GoalsAgainst,
        a.ShotsFor + b.ShotsFor,
        a.ShotsAgainst + b.ShotsAgainst,
        a.TacklesSubject + b.TacklesSubject,
        a.TacklesMirror + b.TacklesMirror,
        a.FoulsSubject + b.FoulsSubject,
        a.FoulsMirror + b.FoulsMirror,
        a.CardsSubject + b.CardsSubject,
        a.CardsMirror + b.CardsMirror,
        a.InjuredSubject + b.InjuredSubject,
        a.InjuredMirror + b.InjuredMirror,
        a.PossessionChanges + b.PossessionChanges,
        a.PossessionTicksSubject + b.PossessionTicksSubject);
}

/// <summary>Valor medido de un perk: lo que gana un equipo por llevarlo frente a su espejo sin él.</summary>
/// <param name="ValueMilli">Milésimas de punto de tasa de victoria (RT-023: aritmética entera).</param>
/// <param name="WinsByMatch">
/// Victorias por <b>índice de partido dentro de la campaña</b> (AV-B): la posición 0 es el primer partido
/// —contador a cero— y la última, el partido <c>L−1</c>. Es lo que convierte una sola medición en la
/// <b>curva</b> del valor contra los partidos que le quedan al perk: el valor a horizonte <c>R</c> es el
/// acumulado de los <c>R</c> primeros índices. Diagnóstico del instrumento; no entra en ninguna puerta.
/// </param>
/// <param name="MatchesByMatch">Partidos jugados en cada índice, denominador de <paramref name="WinsByMatch"/>.</param>
public readonly record struct PerkValueRow(
    string PerkId,
    int Slot,
    int Matches,
    int Wins,
    int ValueMilli,
    IReadOnlyList<int> WinsByMatch,
    IReadOnlyList<int> MatchesByMatch,
    int ControlWins,
    IReadOnlyList<int> ControlWinsByMatch,
    PerkDiagnostics Diagnostics = default)
{
    public double WinRate => Matches > 0 ? 100.0 * Wins / Matches : 0.0;

    /// <summary>Tasa de victoria del <b>control</b>: las mismas plantillas y semillas sin el perk (paquete AY, paso 3).</summary>
    public double ControlWinRate => Matches > 0 ? 100.0 * ControlWins / Matches : 0.0;

    /// <summary>
    /// El valor es la <b>diferencia emparejada</b> entre el brazo con el perk y su control, en milésimas
    /// de punto de tasa de victoria y en la misma escala que antes (×2). Restar el control quita el sesgo
    /// de la pareja de plantillas de cada perk —medido: seis perks sin efecto daban entre −33 y +5 con la
    /// fórmula absoluta, y exactamente lo mismo su control— y la mayor parte del ruido común a los dos
    /// brazos, que comparten plantillas y semillas.
    /// </summary>
    public static int PairedValueMilli(int wins, int controlWins, int matches) =>
        matches > 0 ? (int)Math.Round(1000.0 * (wins - controlWins) / matches * 2.0) : 0;

    /// <summary>Valor medido si el perk sólo jugara los <paramref name="horizon"/> primeros partidos.</summary>
    public int ValueAtHorizon(int horizon)
    {
        int wins = 0, controlWins = 0, matches = 0;
        for (int k = 0; k < horizon && k < WinsByMatch.Count; k++)
        {
            wins += WinsByMatch[k];
            controlWins += ControlWinsByMatch[k];
            matches += MatchesByMatch[k];
        }

        return PairedValueMilli(wins, controlWins, matches);
    }
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
/// <para><b>Y se mide en CAMPAÑA, no en un partido suelto</b> (ADR 0070, corrige AN-B). Cada plantilla
/// juega <see cref="CampaignMatches"/> partidos <b>consecutivos</b> arrastrando los contadores de
/// carrera de un partido al siguiente con el mismo <c>ApplyCounterDeltas</c> que usa la run (RF-070), y
/// el valor del perk es lo que gana sobre <b>toda</b> la campaña. Sin eso el instrumento evaluaba
/// siempre el efecto con el contador a <b>cero</b> —<c>k⁰ = 1</c> sea cual sea <c>k</c>— y los quince
/// perks con <c>accumulatesAcrossMatches</c> valían, para la tabla, exactamente lo que valen sin su eje:
/// la tabla entera salía bit a bit idéntica antes y después de subir seis magnitudes al techo
/// (ADR 0069 §34.5). La campaña es además lo único que mide cada contador a <b>su</b> ritmo: uno de
/// partido llega a 7 en ocho partidos y uno de regate ganado se queda en 2, y esa diferencia es real.</para>
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

    /// <summary>
    /// Partidos de la campaña con la que se mide cada plantilla, es decir <b>cuántos partidos vive un
    /// perk</b> (ADR 0070). No es un número elegido: es el que mide el banco de 1.200 runs de la doctrina
    /// contextual. Los seis contadores que suman uno por partido —<c>ironLungsMatches</c>,
    /// <c>battleReaderMatches</c>, <c>captainsVoiceMatches</c>, <c>deathlessMarchMatches</c>,
    /// <c>longLeashMatches</c> y <c>scarVeteranMatches</c>— llegan al final de la run a un pico medio
    /// ponderado por frecuencia de <b>7,4</b> y mediano de <b>7</b>, así que un perk se juega del orden de
    /// ocho partidos y su contador recorre 0..7. La campaña reproduce ese recorrido entero en vez de
    /// quedarse en el 0. Y se mide en campaña, y no cebando el contador a un valor fijo, porque cada
    /// contador crece a <b>su</b> ritmo: los de partido llegan a 7 y <c>silkyVeteranDribbles</c> se queda
    /// en 2,7 y <c>scarTissueInjuries</c> en 1,4. Un cebado plano acertaría en seis de los quince.
    /// </summary>
    public const int CampaignMatches = 8;

    /// <summary>Calidad de la plantilla de prueba (el pivote del generador).</summary>
    public const int Quality = 50;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    public static IReadOnlyList<PerkValueRow> Run(Catalog catalog, ulong seed, int rosters, int matchesPerRoster) =>
        Run(catalog, seed, rosters, matchesPerRoster, filter: null);

    /// <param name="filter">
    /// Si no es null, sólo se miden los perks de este conjunto. El <b>índice</b> de cada perk sigue
    /// siendo su posición en el catálogo ordenado, así que una fila filtrada sale <b>bit a bit</b> igual
    /// que en la tabla completa con la misma semilla: es un filtro de coste, no una medición distinta.
    /// </param>
    /// <param name="control">
    /// Medida de control (paquete AY, paso 3): las mismas plantillas y las mismas semillas de partido,
    /// pero <b>sin poner el perk</b>. Es el <b>cero del instrumento</b> para esa fila; sin él no se puede
    /// distinguir "este perk resta" de "este par de plantillas estaba desnivelado".
    /// </param>
    public static IReadOnlyList<PerkValueRow> Run(
        Catalog catalog, ulong seed, int rosters, int matchesPerRoster, IReadOnlySet<string>? filter)
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

            if (filter is not null && !filter.Contains(perk.Id))
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

    /// <summary>
    /// Lo que sacó una plantilla de su campaña: victorias por índice de partido, o <c>null</c> si ningún
    /// titular generado podía llevar el perk (entonces el perk entero se queda sin medir).
    /// </summary>
    private sealed record RosterOutcome(int[] WinsByMatch, int[] ControlWinsByMatch, PerkDiagnostics Diagnostics);

    private static PerkValueRow? Measure(
        Catalog catalog, PerkDefinition perk, Race race, ulong seed, int rosters, int matchesPerRoster,
        int perkIndex)
    {
        // Las plantillas son independientes entre sí —la campaña se arrastra DENTRO de una plantilla, no
        // entre ellas— y tanto sus dados (RngStreams.Generation(seed, perkIndex*1000 + roster)) como las
        // semillas de sus partidos (RngStreams.MatchSeed con el índice global perkIndex*100_000 + ...)
        // son función pura del índice, nunca de un contador que avance con la ejecución. Por eso se
        // juegan en paralelo escribiendo por índice, y la reducción se hace después en orden de índice:
        // el resultado es bit a bit el mismo que el del bucle secuencial (RT-020..024). Mismo patrón que
        // el plan de celdas de Sim.Tests/Analysis/BossGateTests.
        var played = new RosterOutcome?[rosters];
        Parallel.For(0, rosters, roster =>
        {
            // Catálogo por hilo (BalanceCatalogs): las condiciones compiladas de los perks no son
            // reentrantes. El dato es el mismo, así que la medida no cambia.
            played[roster] = PlayRoster(
                BalanceCatalogs.Current(catalog), perk, race, seed, roster, matchesPerRoster, perkIndex);
        });

        int matches = 0, wins = 0, controlWins = 0;
        const int Slot = -1;
        var winsByMatch = new int[matchesPerRoster];
        var controlWinsByMatch = new int[matchesPerRoster];
        var matchesByMatch = new int[matchesPerRoster];
        var diagnostics = default(PerkDiagnostics);

        for (int roster = 0; roster < rosters; roster++)
        {
            if (played[roster] is not { } outcome)
            {
                // Alguna plantilla no tenía portador posible (etiqueta que el dado no dio): el perk no se
                // mide, igual que en el bucle secuencial, que abandonaba en cuanto se topaba con una.
                return null;
            }

            diagnostics += outcome.Diagnostics;
            for (int k = 0; k < matchesPerRoster; k++)
            {
                matches++;
                matchesByMatch[k]++;
                wins += outcome.WinsByMatch[k];
                winsByMatch[k] += outcome.WinsByMatch[k];
                controlWins += outcome.ControlWinsByMatch[k];
                controlWinsByMatch[k] += outcome.ControlWinsByMatch[k];
            }
        }

        int valueMilli = PerkValueRow.PairedValueMilli(wins, controlWins, matches);
        return new PerkValueRow(
            perk.Id, Slot, matches, wins, valueMilli, winsByMatch, matchesByMatch, controlWins, controlWinsByMatch, diagnostics);
    }

    /// <summary>La campaña de una plantilla: <paramref name="matchesPerRoster"/> partidos consecutivos que arrastran el contador de carrera (ADR 0070).</summary>
    private static RosterOutcome? PlayRoster(
        Catalog catalog, PerkDefinition perk, Race race, ulong seed, int roster, int matchesPerRoster,
        int perkIndex)
    {
        var config = new SimConfig(CollectLog: false);
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
        var armedPlayers = subject.Players.ToList();
        armedPlayers[carrier] = armedPlayers[carrier] with { Perks = new[] { perk.Id } };
        var armed = subject with { Players = armedPlayers };

        // Dos brazos sobre las MISMAS plantillas y las MISMAS semillas de partido: el sujeto con el perk
        // y el sujeto sin él (control). El valor es la diferencia (PerkValueRow.PairedValueMilli). Cada
        // brazo arrastra su propia campaña.
        var winsByMatch = new int[matchesPerRoster];
        var controlWinsByMatch = new int[matchesPerRoster];
        var diagnostics = default(PerkDiagnostics);
        for (int k = 0; k < matchesPerRoster; k++)
        {
            bool subjectAway = (k % 2) == 1;
            int subjectSide = subjectAway ? 1 : 0;
            ulong matchSeed = RngStreams.MatchSeed(seed, (perkIndex * 100_000) + (roster * matchesPerRoster) + k);

            var armedResult = Simulator.Run(
                subjectAway ? new MatchSetup(mirror, armed, Referee) : new MatchSetup(armed, mirror, Referee),
                matchSeed, catalog, config);
            if (armedResult.Report.Winner == subjectSide)
            {
                winsByMatch[k]++;
            }

            diagnostics += Diagnose(armedResult.Report, perk.Id, subjectSide);

            var controlResult = Simulator.Run(
                subjectAway ? new MatchSetup(mirror, subject, Referee) : new MatchSetup(subject, mirror, Referee),
                matchSeed, catalog, config);
            if (controlResult.Report.Winner == subjectSide)
            {
                controlWinsByMatch[k]++;
            }

            // Campaña (ADR 0070): el contador de carrera pasa al partido siguiente igual que en la
            // run (RF-070, ProgressionRules.ApplyCounterDeltas). Es lo único que se arrastra: la
            // experiencia y las lesiones no, porque las pagarían por igual los dos lados del espejo
            // y sólo añadirían varianza a una diferencia que ya es pequeña.
            armed = Carry(armed, armedResult);
            subject = Carry(subject, controlResult);
        }

        return new RosterOutcome(winsByMatch, controlWinsByMatch, diagnostics);
    }

    private static TeamSetup Carry(TeamSetup team, MatchResult result)
    {
        if (result.CounterDeltas.Count == 0)
        {
            return team;
        }

        var carried = team.Players.ToList();
        for (int i = 0; i < carried.Count; i++)
        {
            carried[i] = ProgressionRules.ApplyCounterDeltas(carried[i], result.CounterDeltas);
        }

        return team with { Players = carried };
    }

    /// <summary>Contadores de un partido vistos desde el lado del sujeto (diagnóstico, paquete AY paso 3).</summary>
    private static PerkDiagnostics Diagnose(MatchReport report, string perkId, int subjectSide)
    {
        long activations = 0;
        foreach (var summary in report.PerksSummary)
        {
            if (string.Equals(summary.PerkId, perkId, StringComparison.Ordinal))
            {
                activations += summary.Activations;
            }
        }

        long tacklesSubject = 0, tacklesMirror = 0, foulsSubject = 0, foulsMirror = 0;
        long cardsSubject = 0, cardsMirror = 0, injuredSubject = 0, injuredMirror = 0;
        foreach (var player in report.Players)
        {
            bool mine = player.Team == subjectSide;
            tacklesSubject += mine ? player.Tackles : 0;
            tacklesMirror += mine ? 0 : player.Tackles;
            foulsSubject += mine ? player.Fouls : 0;
            foulsMirror += mine ? 0 : player.Fouls;
            cardsSubject += mine ? player.Cards : 0;
            cardsMirror += mine ? 0 : player.Cards;
            injuredSubject += mine && player.Injured ? 1 : 0;
            injuredMirror += !mine && player.Injured ? 1 : 0;
        }

        return new PerkDiagnostics(
            activations,
            report.Goals[subjectSide],
            report.Goals[1 - subjectSide],
            report.Shots[subjectSide],
            report.Shots[1 - subjectSide],
            tacklesSubject,
            tacklesMirror,
            foulsSubject,
            foulsMirror,
            cardsSubject,
            cardsMirror,
            injuredSubject,
            injuredMirror,
            report.PossessionChanges,
            report.PossessionTicks[subjectSide]);
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

    /// <summary>
    /// Diagnóstico por perk (paquete AY, paso 3): activaciones por partido y la diferencia sujeto−espejo
    /// de los contadores que explican un valor negativo. No entra en ninguna tabla de datos.
    /// </summary>
    public static void PrintDiagnostics(IReadOnlyList<PerkValueRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Console.WriteLine();
        Console.WriteLine("diagnóstico por perk: por partido, y Δ = sujeto − espejo");
        Console.WriteLine(
            $"{"perk",-24} {"valor",6} {"activ",7} {"gol",6} {"Δgol",6} {"Δtiro",6} " +
            $"{"Δentr",6} {"Δfalta",7} {"Δtarj",6} {"Δles",6} {"cambPos",8} {"posee",7}");
        foreach (var row in rows.OrderBy(r => r.ValueMilli))
        {
            var d = row.Diagnostics;
            double n = Math.Max(1, row.Matches);
            Console.WriteLine(
                $"{row.PerkId,-24} {row.ValueMilli,6} {d.Activations / n,7:F2} {d.GoalsFor / n,6:F2} " +
                $"{(d.GoalsFor - d.GoalsAgainst) / n,6:F3} {(d.ShotsFor - d.ShotsAgainst) / n,6:F3} " +
                $"{(d.TacklesSubject - d.TacklesMirror) / n,6:F3} {(d.FoulsSubject - d.FoulsMirror) / n,7:F3} " +
                $"{(d.CardsSubject - d.CardsMirror) / n,6:F3} {(d.InjuredSubject - d.InjuredMirror) / n,6:F3} " +
                $"{d.PossessionChanges / n,8:F2} {d.PossessionTicksSubject / n,7:F1}");
        }
    }
}
