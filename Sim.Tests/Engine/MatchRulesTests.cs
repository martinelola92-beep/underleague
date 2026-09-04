using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Reglas invariantes del partido: nunca hay empate (RF-055c), la incomparecencia con menos de 5
/// jugadores en campo (RF-059), el portero nunca sale del área (RF-057b) y la secuencia de eventos
/// está ordenada y delimitada por MatchStart/MatchEnd (RF-066, RT-013).
/// </summary>
public sealed class MatchRulesTests
{
    private const int Matches = 50;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void NeverEndsInADraw()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Run(seed);
            var report = result.Report;

            Assert.True(report.Winner is 0 or 1, $"semilla {seed}: ganador {report.Winner}");
            if (report.Goals[0] != report.Goals[1])
            {
                Assert.Equal(report.Goals[0] > report.Goals[1] ? 0 : 1, report.Winner);
            }

            var end = result.Events[^1];
            Assert.Equal(EventType.MatchEnd, end.Type);
            Assert.Contains(end.Detail, new[] { "regulation", "goldenGoal", "tiebreak", "forfeit" });
        }
    }

    [Fact]
    public void GoldenGoalIsOnlyPlayedWhenRegulationIsLevel()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var report = Run(seed).Report;
            if (report.WentToGoldenGoal)
            {
                Assert.True(report.Ticks > Catalog.Tuning.RegulationTicks, $"semilla {seed}: gol de oro sin agotar el reglamentario");
            }
            else if (!report.Forfeit)
            {
                Assert.NotEqual(report.Goals[0], report.Goals[1]);
            }
        }
    }

    [Fact]
    public void ForfeitWhenATeamDropsBelowFivePlayers()
    {
        int forfeits = 0;
        var setup = TestMatches.Brutal(Catalog);

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            if (!result.Report.Forfeit)
            {
                continue;
            }

            forfeits++;
            var end = result.Events[^1];
            Assert.Equal(EventType.MatchEnd, end.Type);
            Assert.Equal("forfeit", end.Detail);

            int loser = 1 - result.Report.Winner;
            Assert.True(OnPitch(setup, result, loser) < 5, $"semilla {seed}: el perdedor conserva 5 o más jugadores");
            Assert.True(OnPitch(setup, result, result.Report.Winner) >= 5, $"semilla {seed}: el ganador también estaba por debajo de 5");
        }

        Assert.True(forfeits > 0, "el emparejamiento de prueba debe producir al menos una incomparecencia en 20 semillas");
    }

    [Fact]
    public void GoalkeeperNeverLeavesTheArea()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            Assert.False(Run(seed).Report.GoalkeeperLeftArea, $"semilla {seed}: un portero salió de su área");
        }
    }

    [Fact]
    public void EventTicksAreNonDecreasing()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var events = Run(seed).Events;
            for (int i = 1; i < events.Count; i++)
            {
                Assert.True(
                    events[i].Tick >= events[i - 1].Tick,
                    $"semilla {seed}: el evento {i} ({events[i].Type}, t={events[i].Tick}) va detrás de t={events[i - 1].Tick}");
            }
        }
    }

    [Fact]
    public void MatchStartIsFirstAndMatchEndIsLast()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Run(seed);
            var events = result.Events;

            Assert.Equal(EventType.MatchStart, events[0].Type);
            Assert.Equal(0, events[0].Tick);
            Assert.Equal(EventType.MatchEnd, events[^1].Type);
            Assert.Equal(result.Report.Ticks, events[^1].Tick);

            for (int i = 1; i < events.Count - 1; i++)
            {
                Assert.NotEqual(EventType.MatchStart, events[i].Type);
                Assert.NotEqual(EventType.MatchEnd, events[i].Type);
            }
        }
    }

    [Fact]
    public void ReportCountersAgreeWithTheEventStream()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var result = Run(seed);
            var report = result.Report;
            int goals = 0;
            int shots = 0;
            int tackles = 0;
            int fouls = 0;
            int injuries = 0;
            int deaths = 0;

            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case EventType.Goal:
                        goals++;
                        break;
                    case EventType.Shot:
                        shots++;
                        break;
                    case EventType.Tackle:
                        tackles++;
                        break;
                    case EventType.Foul:
                        fouls++;
                        break;
                    case EventType.Injury:
                        injuries++;
                        break;
                    case EventType.Death:
                        deaths++;
                        break;
                    default:
                        break;
                }
            }

            Assert.Equal(report.Goals[0] + report.Goals[1], goals);
            Assert.Equal(report.Shots[0] + report.Shots[1], shots);
            Assert.Equal(report.Tackles, tackles);
            Assert.Equal(report.Fouls, fouls);
            Assert.Equal(report.Injuries, injuries);
            Assert.Equal(0, deaths);
            Assert.Equal(0, report.Deaths);
        }
    }

    [Fact]
    public void InvalidSetupIsRejectedWithAClearMessage()
    {
        var valid = TestMatches.Reference(Catalog, 1);

        var fourStarters = new Lineup(valid.Home.Lineup.Slots.Take(4).ToList());
        var tooFew = valid with { Home = valid.Home with { Lineup = fourStarters } };
        var missingStarters = Assert.Throws<ArgumentException>(() => Simulator.Run(tooFew, 1, Catalog, SimConfig.Default));
        Assert.Contains("titulares", missingStarters.Message, StringComparison.Ordinal);

        var withoutGoalkeeper = new Lineup(valid.Home.Lineup.Slots.Skip(1).ToList());
        var noKeeper = valid with { Home = valid.Home with { Lineup = withoutGoalkeeper } };
        var keeper = Assert.Throws<ArgumentException>(() => Simulator.Run(noKeeper, 1, Catalog, SimConfig.Default));
        Assert.Contains("porteros", keeper.Message, StringComparison.Ordinal);

        var shared = valid with { Away = valid.Away with { Players = valid.Home.Players, Lineup = valid.Home.Lineup } };
        var duplicated = Assert.Throws<ArgumentException>(() => Simulator.Run(shared, 1, Catalog, SimConfig.Default));
        Assert.Contains("únicos", duplicated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UtilityDumpIsCapturedForTheRequestedPlayerAndTick()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var result = Simulator.Run(setup, 1, Catalog, new SimConfig(CollectLog: false, DumpUtility: (4, 100)));

        var dump = result.Report.UtilityDump;
        Assert.NotNull(dump);
        Assert.Equal(4, dump!.PlayerId);

        // El jugador solo decide cada tuning.decisionIntervalTicks ticks, desplazado por su id, y solo si
        // su estado lo permite: lo que el motor garantiza (y lo que documenta MatchEngine.Decide) es la
        // PRIMERA decisión de ese jugador en un tick >= el pedido, no exactamente ese tick. Exigir la
        // igualdad convertía este test en un test del reparto de estados de una semilla concreta.
        Assert.True(dump.Tick >= 100, $"el volcado debía capturarse en un tick >= 100, fue {dump.Tick}");
        Assert.True(StateMachine.IsDecisionState(dump.State));
        Assert.NotEmpty(dump.Rows);
        Assert.Contains(dump.Rows, row => row.Action == dump.Chosen && !row.Rejected);
    }

    [Fact]
    public void LogHasOneLinePerEventAndOnlyWhenRequested()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var withLog = Simulator.Run(setup, 1, Catalog, new SimConfig(CollectLog: true));
        var withoutLog = Simulator.Run(setup, 1, Catalog, new SimConfig(CollectLog: false));

        Assert.Equal(withLog.Events.Count, withLog.Report.Log.Count);
        Assert.Empty(withoutLog.Report.Log);
        Assert.All(withLog.Report.Log, line => Assert.StartsWith("[t=", line, StringComparison.Ordinal));
    }

    private static MatchResult Run(ulong seed) =>
        Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false));

    /// <summary>Jugadores del equipo que siguen en el campo al acabar, según lesiones y rojas del log de eventos.</summary>
    private static int OnPitch(MatchSetup setup, MatchResult result, int team)
    {
        var starters = team == 0 ? setup.Home.Lineup.Slots : setup.Away.Lineup.Slots;
        int count = starters.Count;
        foreach (var e in result.Events)
        {
            bool isTeamPlayer = false;
            for (int i = 0; i < starters.Count; i++)
            {
                if (starters[i].PlayerId == e.Actor)
                {
                    isTeamPlayer = true;
                    break;
                }
            }

            if (!isTeamPlayer)
            {
                continue;
            }

            if (e.Type == EventType.Injury || (e.Type == EventType.Card && e.Detail == "red"))
            {
                count--;
            }
        }

        return count;
    }
}
