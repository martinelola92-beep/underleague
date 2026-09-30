using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BR-B: la muerte es terminal. Ningún evento posterior cambia el estado de un muerto, y los créditos y
/// los <c>DeathDetails</c> coinciden con el estado final (RF-093, ADR 0048, ADR 0124, ADR 0163).
/// Encontrado con la Gaceta: ≈15 % de los créditos de muerte de 60 runs eran de jugadores que el estado
/// dejaba vivos (semilla 7, run 0, partido 15: DEATH de <c>perk:skullsplitter</c> y luego INJURY minor del
/// mismo jugador en el mismo tick).
/// </summary>
public sealed class MatchResolutionDeathIsTerminalTests
{
    private const string OpponentId = "act1_elf_swiftwing";

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MapNode Node() =>
        new(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), OpponentId, 3);

    private static MatchLineup LineupFor(IReadOnlyList<RunPlayer> starters)
    {
        var defs = starters.Select(p => p.ToDefinition(Catalog)).ToList();
        return new MatchLineup(defs, Array.Empty<PlayerDefinition>(), Lineup.Default(defs), EmergencyGoalkeeperId: -1);
    }

    private static MatchResult ResultWith(IReadOnlyList<int> ownIds, IReadOnlyList<MatchEvent> events)
    {
        var builder = new MatchReportBuilder { Winner = 0, Ticks = 900 };
        foreach (int id in ownIds)
        {
            builder.Players.Add(new PlayerMatchStats(id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 900, 0, 0));
        }

        return new MatchResult(events, builder.Build(), Array.Empty<PlayerCounterDelta>());
    }

    private static MatchEvent Event(EventType type, int tick, int actor, int opponent, string detail) =>
        new(type, tick, 0, actor, -1, opponent, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, detail);

    private static string Key(int rivalIndex, int ownPlayerId, string kind) =>
        RunState.RivalCreditPrefix + OpponentId + ":" + rivalIndex + ":" + ownPlayerId + ":" + kind;

    /// <summary>El par que el motor emitía: DEATH y, en el mismo tick, INJURY del mismo jugador.</summary>
    [Fact]
    public void AnInjuryAfterTheDeathOfTheSamePlayerChangesNothing()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 20260929UL, Catalog);
        var victim = state.Roster[1];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 1;
        var events = new[]
        {
            Event(EventType.Death, 1322, victim.Id, rivalId, "perk:skullsplitter"),
            Event(EventType.Injury, 1322, victim.Id, rivalId, "minor"),
        };

        var applied = MatchResolution.Apply(state, Node(), LineupFor(new[] { victim }), ResultWith(new[] { victim.Id }, events), Catalog);

        Assert.Equal(PhysicalState.Dead, applied.State.Roster.First(p => p.Id == victim.Id).PhysicalState);
        Assert.Equal(0, applied.Summary.OwnInjuries);
        Assert.Equal(1, applied.Summary.OwnDeaths);
        Assert.Single(applied.Summary.DeathDetails);
        Assert.Equal(1, applied.State.Counter(Key(1, victim.Id, "sufferedDeath")));
        Assert.Equal(0, applied.State.Counter(Key(1, victim.Id, "sufferedInjury")));
    }

    /// <summary>La lesión ANTERIOR a la muerte sigue contando: es la vía 1 de RF-093 (lesión grave y remate).</summary>
    [Fact]
    public void AnInjuryBeforeTheDeathStillCounts()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 20260929UL, Catalog);
        var victim = state.Roster[1];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 1;
        var events = new[]
        {
            Event(EventType.Injury, 100, victim.Id, rivalId, "severe"),
            Event(EventType.Death, 100, victim.Id, rivalId, "severeInjury"),
        };

        var applied = MatchResolution.Apply(state, Node(), LineupFor(new[] { victim }), ResultWith(new[] { victim.Id }, events), Catalog);

        Assert.Equal(1, applied.Summary.OwnInjuries);
        Assert.Equal(1, applied.Summary.OwnDeaths);
        Assert.Equal(1, applied.State.Counter(Key(1, victim.Id, "sufferedInjury")));
        Assert.Equal(1, applied.State.Counter(Key(1, victim.Id, "sufferedDeath")));
    }

    /// <summary>
    /// BE-C, hermano de BR-B: los créditos paran donde para el bucle de bajas. Cuando la plantilla baja del
    /// mínimo la run termina, y un hecho posterior del mismo partido no se apunta.
    /// </summary>
    [Fact]
    public void CreditsStopAtTheEventThatEndsTheRun()
    {
        var state = RunEngine.Start(TestRuns.Setup(), 20260929UL, Catalog);
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 2;
        var events = new List<MatchEvent>();
        int deadNeeded = state.Roster.Count - RunRules.MinimumAvailablePlayers + 1;
        for (int i = 0; i < deadNeeded; i++)
        {
            events.Add(Event(EventType.Death, 10 + i, state.Roster[i].Id, rivalId, "perk:skullsplitter"));
        }

        var late = state.Roster[deadNeeded];
        events.Add(Event(EventType.Injury, 500, late.Id, rivalId, "minor"));
        var starters = state.Roster.Take(deadNeeded + 1).ToList();

        var applied = MatchResolution.Apply(state, Node(), LineupFor(starters), ResultWith(starters.Select(p => p.Id).ToList(), events), Catalog);

        Assert.Equal(RunOutcomeKind.Defeat, applied.Outcome.Kind);
        Assert.Equal(0, applied.State.Counter(Key(2, late.Id, "sufferedInjury")));
        Assert.Equal(1, applied.State.Counter(Key(2, state.Roster[0].Id, "sufferedDeath")));
    }

    /// <summary>
    /// La reproducción real: las runs 0, 2, 4 y 14 de la semilla 7 tenían un INJURY tras un DEATH del mismo
    /// jugador. Con el arreglo del motor ya no se emite, y todo muerto del resumen está muerto al final.
    /// </summary>
    [Fact]
    public void RealRunsNeverEmitACasualtyAfterADeathAndEveryDeathIsReflectedInTheFinalRoster()
    {
        var files = TestData.LoadAllFiles();
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var races = Catalog.Races.Where(r => r.Launch).Select(r => r.Id).OrderBy(r => r).ToList();
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        int deaths = 0;
        // Las cuatro de la reproducción y otras doce: cualquier cambio del motor reparte las muertes de otra manera
        // entre las runs, y con sólo cuatro el instrumento podía quedarse sin ninguna (ADR 0175, Regla J).
        foreach (int i in new[] { 0, 2, 4, 14, 1, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 15 })
        {
            var setup = standard.NewRunSetup("balance_club", races[i % races.Count], files) with { GeneratedQuality = 50 };
            var dead = new HashSet<int>();
            var result = RunPolicy.Play(
                setup,
                700_000UL + (ulong)i,
                ThreadCatalogs.Current,
                standard,
                bosses,
                options,
                (before, node, matchSetup, match, summary) =>
                {
                    var diedThisMatch = new HashSet<int>();
                    foreach (var e in match.Events)
                    {
                        if (e.Detail.EndsWith(":cancelled", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (e.Type == EventType.Death)
                        {
                            // second_wound (trigger INJURY) mata REACCIONANDO a la lesión: el motor publica
                            // antes de registrar, así que su DEATH aparece delante de la INJURY que lo causa.
                            // Es orden de registro, no una lesión sobre un muerto; MatchResolution lo cubre.
                            if (e.Detail != "perk:second_wound")
                            {
                                diedThisMatch.Add(e.Actor);
                            }

                        }
                        else if (e.Type == EventType.Injury)
                        {
                            Assert.False(diedThisMatch.Contains(e.Actor), $"run {i}, tick {e.Tick}: INJURY del jugador {e.Actor} tras su DEATH");
                        }
                    }

                    foreach (var detail in summary.DeathDetails)
                    {
                        dead.Add(detail.PlayerId);
                    }
                });

            deaths += dead.Count;
            foreach (int id in dead)
            {
                var player = result.FinalState!.Roster.FirstOrDefault(p => p.Id == id);
                if (player is not null)
                {
                    Assert.Equal(PhysicalState.Dead, player.PhysicalState);
                }
            }
        }

        Assert.True(deaths > 0, "el instrumento no vio ninguna muerte propia: el test no mide nada (Regla J)");
    }
}
