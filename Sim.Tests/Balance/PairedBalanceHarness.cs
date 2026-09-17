using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Harness genérico de control/armado (§12, pieza 2: generalización de
/// <c>_C1CazagolesExperiment</c>/<c>_C1CazagolesBalanceLote</c>, ya borrados por ser instrumentos
/// temporales de un solo perk). Sirve para cualquier perk de destino único (<c>Owner</c>/<c>Actor</c>,
/// <see cref="PerkClassification.NeedsMultiTargetHarness"/> false — la variante multi-objetivo de
/// <c>pack_mentality</c> y similares es un hueco pendiente, §13 punto 10, no resuelto aquí).
///
/// <para>Mismo esquema que Tanda 0/C1: mismas plantillas y semillas en los dos brazos, sin arrastre de
/// campaña, portador elegible por posición. No conoce categorías ni reglas de decisión (§6) — solo
/// produce partidos y resúmenes; el motor de decisión y el clasificador son piezas separadas y ya
/// probadas por su cuenta.</para>
/// </summary>
public static class PairedBalanceHarness
{
    private const Race NeutralRace = Race.Human;
    private const int Quality = 50;
    private const int Level = 4;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    /// <summary>Resumen de un lote emparejado: partidos completos de cada brazo, listos para <see cref="MatchMetrics"/>.</summary>
    public sealed record PairedResult(
        IReadOnlyList<MatchSummary> ArmedMatches,
        IReadOnlyList<MatchSummary> ControlMatches,
        long ArmedOnPitchTicks,
        long ArmedExposedTicks,
        IReadOnlyDictionary<PlayerAction, long> ArmedExposedActionCounts,
        IReadOnlyDictionary<PlayerAction, long> ControlExposedActionCounts,
        long ArmedActivations,
        IReadOnlyList<int> ArmedActivationsPerMatch,
        long TotalSimulatedTicks);

    /// <summary>
    /// Ejecuta <paramref name="rosters"/> plantillas × 2 direcciones para <paramref name="perkId"/> (ya
    /// presente en <paramref name="catalog"/>) sobre un delantero/defensa/etc. elegible por
    /// <paramref name="carrierPosition"/>. <paramref name="exposureZone"/> es opcional: si no es null, se
    /// mide exposición/histograma restringido a esa zona (§3.3/§5.4, tipo "continua"), igual que C1.
    /// </summary>
    public static PairedResult Run(
        Catalog catalog, string perkId, Position carrierPosition, int rosters, ulong seed,
        Zone? exposureZone = null, bool collectExposure = false) =>
        Run(catalog, perkId, home => FindByPosition(home, carrierPosition), rosters, seed, exposureZone, collectExposure);

    /// <summary>
    /// Igual que la sobrecarga por posición, pero elige el portador con el mismo criterio de elegibilidad
    /// real que usa el juego (<see cref="PerkAssignment.Eligible"/>), no una coincidencia de puesto —
    /// necesario para el contrato de §16 (punto 5), que se aplica a cualquier `ReadyForScreening` sin
    /// saber de antemano qué puesto le corresponde.
    /// </summary>
    public static PairedResult RunWithEligibleCarrier(Catalog catalog, PerkDefinition perk, int rosters, ulong seed) =>
        Run(catalog, perk.Id, home => FindEligible(home, perk, catalog), rosters, seed, race: perk.Race);

    /// <summary>
    /// Expone el criterio real de selección de portador (§19: preferir campo, caer a portería) para que
    /// el forense/diagnóstico pueda comprobar A QUÉ POSICIÓN cae un perk sin duplicar el criterio.
    /// </summary>
    public static int FindEligibleCarrierSlot(TeamSetup team, PerkDefinition perk, Catalog catalog) =>
        FindEligible(team, perk, catalog);

    private static int FindByPosition(TeamSetup team, Position position)
    {
        for (int i = 0; i < team.Players.Count; i++)
        {
            if (team.Players[i].Position == position)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Mismo filtro que el juego (<see cref="PerkAssignment.Eligible"/>), sobre los siete titulares —
    /// pero prefiriendo un titular de campo (slots 1-6) antes que la portería (slot 0), y cayendo a la
    /// portería solo si ningún jugador de campo es elegible. <c>TeamGenerator.StarterPositions[0]</c> es
    /// SIEMPRE <c>Goalkeeper</c> (Sim/Generation/TeamGenerator.cs): antes de este cambio, cualquier perk
    /// sin <c>positionOnly</c>/<c>tagsRequired</c> restrictivo se probaba SIEMPRE sobre el portero —
    /// confirmado como la causa raíz común de varias escaladas del primer screening real (§18.3/§19,
    /// forense del 19 sep 2026: los cinco perks del primer lote cayeron los cinco en el portero). Un
    /// portero rara vez entra/dispara, así que medir ahí subestima sistemáticamente perks pensados para
    /// jugadores de campo — no es un cambio de <see cref="PerkAssignment.Eligible"/> (esa función sigue
    /// reflejando la elegibilidad REAL del juego tal cual), solo el desempate arbitrario de qué portador
    /// concreto usa la MEDICIÓN cuando el perk no dice nada sobre la posición.
    /// </summary>
    private static int FindEligible(TeamSetup team, PerkDefinition perk, Catalog catalog)
    {
        for (int i = 1; i < 7 && i < team.Players.Count; i++)
        {
            foreach (var candidate in PerkAssignment.Eligible(team.Players[i], catalog))
            {
                if (string.Equals(candidate.Id, perk.Id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        if (team.Players.Count > 0)
        {
            foreach (var candidate in PerkAssignment.Eligible(team.Players[0], catalog))
            {
                if (string.Equals(candidate.Id, perk.Id, StringComparison.Ordinal))
                {
                    return 0;
                }
            }
        }

        return -1;
    }

    private static PairedResult Run(
        Catalog catalog, string perkId, Func<TeamSetup, int> selectCarrier, int rosters, ulong seed,
        Zone? exposureZone = null, bool collectExposure = false, Race? race = null)
    {
        // Perks restringidos a una raza (tagsRequired/race, p. ej. iron_gate=Dwarf) nunca encontrarían un
        // titular elegible en una plantilla neutral (Human) — mismo criterio que PerkValueRunner.Measure
        // ("var race = perk.Race ?? NeutralRace"), no una regla nueva (§16.6, encontrado ejecutando el
        // contrato de READY_FOR_SCREENING contra datos reales).
        var effectiveRace = race ?? NeutralRace;
        var config = new SimConfig(CollectLog: false, Trace: collectExposure);
        var armedMatches = new List<MatchSummary>();
        var controlMatches = new List<MatchSummary>();
        long armedOnPitch = 0, armedExposed = 0, armedActivations = 0, totalTicks = 0;
        var armedCounts = new Dictionary<PlayerAction, long>();
        var controlCounts = new Dictionary<PlayerAction, long>();
        var armedActivationsPerMatch = new List<int>();

        for (int roster = 0; roster < rosters; roster++)
        {
            var homeRng = RngStreams.Generation(seed, roster);
            var awayRng = RngStreams.Generation(seed, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, catalog, "home", effectiveRace, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, catalog, "away", effectiveRace, Quality, 100001, Level);

            int carrierSlot = selectCarrier(home);
            if (carrierSlot < 0)
            {
                continue;
            }

            var armedPlayers = home.Players.ToList();
            armedPlayers[carrierSlot] = armedPlayers[carrierSlot] with { Perks = new[] { perkId } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[carrierSlot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                bool subjectAway = direction == 1;
                ulong matchSeed = RngStreams.MatchSeed(seed, (roster * 2) + direction);

                var armedSetup = subjectAway ? new MatchSetup(away, armedHome, Referee) : new MatchSetup(armedHome, away, Referee);
                var armedResult = Simulator.Run(armedSetup, matchSeed, catalog, config);
                armedMatches.Add(MatchSummary.FromReport(armedResult.Report, "home", "away"));
                long armedActivationsThisMatch = CountActivations(armedResult, perkId, carrierId);
                armedActivations += armedActivationsThisMatch;
                armedActivationsPerMatch.Add((int)armedActivationsThisMatch);
                totalTicks += armedResult.Report.Ticks;
                if (collectExposure && exposureZone is { } zone)
                {
                    Accumulate(armedResult.Trace, carrierId, zone, armedCounts, ref armedOnPitch, ref armedExposed);
                }

                var controlSetup = subjectAway ? new MatchSetup(away, home, Referee) : new MatchSetup(home, away, Referee);
                var controlResult = Simulator.Run(controlSetup, matchSeed, catalog, config);
                controlMatches.Add(MatchSummary.FromReport(controlResult.Report, "home", "away"));
                totalTicks += controlResult.Report.Ticks;
                if (collectExposure && exposureZone is { } zoneControl)
                {
                    long dummyOnPitch = 0, dummyExposed = 0;
                    Accumulate(controlResult.Trace, carrierId, zoneControl, controlCounts, ref dummyOnPitch, ref dummyExposed);
                }
            }
        }

        return new PairedResult(
            armedMatches, controlMatches, armedOnPitch, armedExposed, armedCounts, controlCounts,
            armedActivations, armedActivationsPerMatch, totalTicks);
    }

    private static long CountActivations(MatchResult result, string perkId, int carrierId)
    {
        long total = 0;
        foreach (var summary in result.Report.PerksSummary)
        {
            if (string.Equals(summary.PerkId, perkId, StringComparison.Ordinal) && summary.OwnerId == carrierId)
            {
                total += summary.Activations;
            }
        }

        return total;
    }

    private static void Accumulate(
        MatchTrace? trace, int carrierId, Zone zone, Dictionary<PlayerAction, long> counts,
        ref long onPitchTicks, ref long exposedTicks)
    {
        if (trace is null)
        {
            return;
        }

        int slot = -1;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == carrierId)
            {
                slot = i;
                break;
            }
        }

        if (slot < 0)
        {
            return;
        }

        int team = trace.Players[slot].Team;
        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            if (!trace.OnPitchAt(frame, slot))
            {
                continue;
            }

            onPitchTicks++;
            var pos = trace.PositionAt(frame, slot);
            if (Pitch.ZoneOf(pos, team) != zone)
            {
                continue;
            }

            exposedTicks++;
            if (trace.ActionAt(frame, slot) is { } action)
            {
                counts.TryGetValue(action, out long c);
                counts[action] = c + 1;
            }
        }
    }
}
