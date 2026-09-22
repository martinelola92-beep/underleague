using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems.Rivals;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BE-B: la memoria de "quién knaveó a quién" que la ADR 0124 (enmienda) prometía y que su
/// implementación no llegó a cubrir. Distinta de <see cref="MatchResolutionCareerTests"/> -esa cubre el
/// registro tipado <see cref="RunCareer"/> de la plantilla propia; esta cubre el PAR (causante, víctima)
/// con identidad de los dos lados, en <see cref="RunState.RivalCreditPrefix"/>-.
/// </summary>
public sealed class MatchResolutionRivalCreditTests
{
    private const string OpponentId = "act1_elf_swiftwing";

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static RunState BaseState(ulong seed = 20260922UL) =>
        RunEngine.Start(TestRuns.Setup(), seed, Catalog);

    private static MapNode Node(string opponentId = OpponentId, int id = 101) =>
        new(id, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), opponentId, 3);

    private static MatchLineup LineupFor(IReadOnlyList<RunPlayer> starters)
    {
        var startDefs = starters.Select(p => p.ToDefinition(Catalog)).ToList();
        return new MatchLineup(startDefs, Array.Empty<PlayerDefinition>(), Lineup.Default(startDefs), EmergencyGoalkeeperId: -1);
    }

    private static PlayerMatchStats Stats(int playerId, int team, int ticksOnPitch = 900) =>
        new(playerId, team, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, ticksOnPitch, 0, 0);

    private static MatchResult ResultWith(IReadOnlyList<PlayerMatchStats> stats, IReadOnlyList<MatchEvent> events)
    {
        var builder = new MatchReportBuilder();
        builder.Winner = 0;
        builder.Ticks = 900;
        builder.Players.AddRange(stats);
        return new MatchResult(events, builder.Build(), Array.Empty<PlayerCounterDelta>());
    }

    private static MatchEvent InjuryEvent(int team, int actor, int opponent, string detail = "minor", int tick = 50) =>
        new(EventType.Injury, tick, team, actor, -1, opponent, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, detail);

    private static MatchEvent DeathEvent(int team, int actor, int opponent, string detail = "severeInjury", int tick = 80) =>
        new(EventType.Death, tick, team, actor, -1, opponent, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, detail);

    private static string Key(int rivalIndex, int ownPlayerId, string kind, string opponentId = OpponentId) =>
        RunState.RivalCreditPrefix + opponentId + ":" + rivalIndex + ":" + ownPlayerId + ":" + kind;

    /// <summary>Un jugador propio lesiona a un rival concreto: sube la clave "causedInjury" exacta del par.</summary>
    [Fact]
    public void OwnPlayerInjuringASpecificRivalCreditsCausedInjuryForThatPair()
    {
        var state = BaseState();
        var causer = state.Roster[0];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 3;
        var lineup = LineupFor(new[] { causer });
        var stats = new[] { Stats(causer.Id, 0) };
        var events = new[] { InjuryEvent(team: 1, actor: rivalId, opponent: causer.Id) };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events), Catalog);

        Assert.Equal(1, applied.State.Counter(Key(3, causer.Id, "causedInjury")));
        Assert.Equal(0, applied.State.Counter(Key(3, causer.Id, "sufferedInjury")));
    }

    /// <summary>Un rival lesiona a un jugador propio: sube la clave de la OTRA dirección, "sufferedInjury".</summary>
    [Fact]
    public void RivalInjuringAnOwnPlayerCreditsSufferedInjuryForThatPair()
    {
        var state = BaseState();
        var victim = state.Roster[0];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 5;
        var lineup = LineupFor(new[] { victim });
        var stats = new[] { Stats(victim.Id, 0) };
        var events = new[] { InjuryEvent(team: 0, actor: victim.Id, opponent: rivalId) };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events), Catalog);

        Assert.Equal(1, applied.State.Counter(Key(5, victim.Id, "sufferedInjury")));
        Assert.Equal(0, applied.State.Counter(Key(5, victim.Id, "causedInjury")));
    }

    /// <summary>
    /// Una muerte se registra en su propia clave y se distingue de una lesión: dos hechos del mismo
    /// causante contra el mismo rival, en la misma jugada de reincidencia (RF-093 vía 1, R2 de la
    /// enmienda), suben DOS contadores distintos, no uno solo.
    /// </summary>
    [Fact]
    public void ADeathIsCreditedSeparatelyFromAnInjuryForTheSamePair()
    {
        var state = BaseState();
        var causer = state.Roster[0];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 7;
        var lineup = LineupFor(new[] { causer });
        var stats = new[] { Stats(causer.Id, 0) };
        var events = new[]
        {
            InjuryEvent(team: 1, actor: rivalId, opponent: causer.Id, detail: "severe", tick: 50),
            DeathEvent(team: 1, actor: rivalId, opponent: causer.Id, tick: 51),
        };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events), Catalog);

        Assert.Equal(1, applied.State.Counter(Key(7, causer.Id, "causedInjury")));
        Assert.Equal(1, applied.State.Counter(Key(7, causer.Id, "causedDeath")));
    }

    /// <summary>Un suceso anulado por un perk (sufijo ":cancelled") no registra nada: mismo criterio que la atribución de RunCareer.</summary>
    [Fact]
    public void ACancelledEventCreditsNothing()
    {
        var state = BaseState();
        var causer = state.Roster[0];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 2;
        var lineup = LineupFor(new[] { causer });
        var stats = new[] { Stats(causer.Id, 0) };
        var events = new[] { InjuryEvent(team: 1, actor: rivalId, opponent: causer.Id, detail: "minor:cancelled") };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events), Catalog);

        Assert.Equal(0, applied.State.Counter(Key(2, causer.Id, "causedInjury")));
        Assert.DoesNotContain(
            applied.State.Counters.Keys, k => k.StartsWith(RunState.RivalCreditPrefix, StringComparison.Ordinal));
    }

    /// <summary>Dos encuentros contra el MISMO clan acumulan sobre la misma clave, no la sustituyen.</summary>
    [Fact]
    public void TwoEncountersAgainstTheSameClanAccumulateOnTheSameKey()
    {
        var state = BaseState();
        var causer = state.Roster[0];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 1;
        var lineup = LineupFor(new[] { causer });
        var stats = new[] { Stats(causer.Id, 0) };

        var first = MatchResolution.Apply(
            state, Node(id: 101), lineup,
            ResultWith(stats, new[] { InjuryEvent(team: 1, actor: rivalId, opponent: causer.Id) }), Catalog);
        var second = MatchResolution.Apply(
            first.State, Node(id: 102), lineup,
            ResultWith(stats, new[] { InjuryEvent(team: 1, actor: rivalId, opponent: causer.Id) }), Catalog);

        Assert.Equal(2, second.State.Counter(Key(1, causer.Id, "causedInjury")));
    }

    /// <summary>
    /// Round-trip de guardado con claves de rivalCredit: el contador sobrevive a Save/Load y el esquema
    /// SIGUE en 5 -esta memoria vive en Counters, de clave libre, precisamente para no subir de versión.
    /// </summary>
    [Fact]
    public void RoundTrip_KeepsRivalCreditCountersWithoutBumpingTheSchemaVersion()
    {
        var state = BaseState();
        var causer = state.Roster[0];
        int rivalId = RivalTeamBuilder.OpponentFirstPlayerId + 4;
        var lineup = LineupFor(new[] { causer });
        var stats = new[] { Stats(causer.Id, 0) };
        var events = new[] { InjuryEvent(team: 1, actor: rivalId, opponent: causer.Id) };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events), Catalog);
        string key = Key(4, causer.Id, "causedInjury");
        Assert.Equal(1, applied.State.Counter(key));

        string json = RunSave.Save(applied.State);
        var loaded = RunSave.Load(json);

        Assert.Equal(1, loaded.Counter(key));
        Assert.Equal(5, RunState.CurrentSchemaVersion);
        Assert.Equal(5, loaded.SchemaVersion);
    }
}
