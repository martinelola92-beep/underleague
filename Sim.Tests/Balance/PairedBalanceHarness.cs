using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
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
        long ArmedActivations);

    /// <summary>
    /// Ejecuta <paramref name="rosters"/> plantillas × 2 direcciones para <paramref name="perkId"/> (ya
    /// presente en <paramref name="catalog"/>) sobre un delantero/defensa/etc. elegible por
    /// <paramref name="carrierPosition"/>. <paramref name="exposureZone"/> es opcional: si no es null, se
    /// mide exposición/histograma restringido a esa zona (§3.3/§5.4, tipo "continua"), igual que C1.
    /// </summary>
    public static PairedResult Run(
        Catalog catalog, string perkId, Position carrierPosition, int rosters, ulong seed,
        Zone? exposureZone = null, bool collectExposure = false)
    {
        var config = new SimConfig(CollectLog: false, Trace: collectExposure);
        var armedMatches = new List<MatchSummary>();
        var controlMatches = new List<MatchSummary>();
        long armedOnPitch = 0, armedExposed = 0, armedActivations = 0;
        var armedCounts = new Dictionary<PlayerAction, long>();
        var controlCounts = new Dictionary<PlayerAction, long>();

        for (int roster = 0; roster < rosters; roster++)
        {
            var homeRng = RngStreams.Generation(seed, roster);
            var awayRng = RngStreams.Generation(seed, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, catalog, "home", NeutralRace, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, catalog, "away", NeutralRace, Quality, 100001, Level);

            int carrierSlot = -1;
            for (int i = 0; i < home.Players.Count; i++)
            {
                if (home.Players[i].Position == carrierPosition)
                {
                    carrierSlot = i;
                    break;
                }
            }

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
                armedActivations += CountActivations(armedResult, perkId, carrierId);
                if (collectExposure && exposureZone is { } zone)
                {
                    Accumulate(armedResult.Trace, carrierId, zone, armedCounts, ref armedOnPitch, ref armedExposed);
                }

                var controlSetup = subjectAway ? new MatchSetup(away, home, Referee) : new MatchSetup(home, away, Referee);
                var controlResult = Simulator.Run(controlSetup, matchSeed, catalog, config);
                controlMatches.Add(MatchSummary.FromReport(controlResult.Report, "home", "away"));
                if (collectExposure && exposureZone is { } zoneControl)
                {
                    long dummyOnPitch = 0, dummyExposed = 0;
                    Accumulate(controlResult.Trace, carrierId, zoneControl, controlCounts, ref dummyOnPitch, ref dummyExposed);
                }
            }
        }

        return new PairedResult(armedMatches, controlMatches, armedOnPitch, armedExposed, armedCounts, controlCounts, armedActivations);
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
