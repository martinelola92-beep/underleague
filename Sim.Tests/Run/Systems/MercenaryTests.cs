using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Mercenaries;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>RF-111: el mercenario cobra por partido y abandona por dos vías, banquillo o derrotas seguidas.</summary>
public sealed class MercenaryTests
{
    [Fact]
    public void MercenaryAbandonsAfterThreeConsecutiveMatchesBenched()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7001UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var mercenary = MakeMercenary(state, matchesBenched: economy.MercenaryBenchAbandonMatches);
        state = state.WithNewPlayer(mercenary);
        int mercenaryId = state.Roster.Last().Id;

        var won = Win(new List<int> { state.Roster[0].Id });
        var next = MercenarySystem.Process(state, won, economy);

        Assert.Null(next.FindPlayer(mercenaryId));
    }

    [Fact]
    public void MercenaryStaysIfBenchedLessThanTheLimit()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7002UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var mercenary = MakeMercenary(state, matchesBenched: economy.MercenaryBenchAbandonMatches - 1);
        state = state.WithNewPlayer(mercenary);
        int mercenaryId = state.Roster.Last().Id;

        var won = Win(new List<int> { state.Roster[0].Id });
        var next = MercenarySystem.Process(state, won, economy);

        Assert.NotNull(next.FindPlayer(mercenaryId));
    }

    [Fact]
    public void AllMercenariesAbandonAfterThreeConsecutiveTeamLosses()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7003UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        state = state.WithNewPlayer(MakeMercenary(state, matchesBenched: 0));
        int mercenaryId = state.Roster.Last().Id;

        var loss = Loss(new List<int> { state.Roster[0].Id });
        for (int i = 1; i < economy.MercenaryLossStreakAbandon; i++)
        {
            state = MercenarySystem.Process(state, loss, economy);
            Assert.NotNull(state.FindPlayer(mercenaryId));
        }

        state = MercenarySystem.Process(state, loss, economy);
        Assert.Null(state.FindPlayer(mercenaryId));
        Assert.Equal(0, state.Counter(MercenarySystem.ConsecutiveLossesCounter));
    }

    [Fact]
    public void AWinResetsTheLossStreak()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7004UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var loss = Loss(new List<int> { state.Roster[0].Id });
        var win = Win(new List<int> { state.Roster[0].Id });

        state = MercenarySystem.Process(state, loss, economy);
        Assert.Equal(1, state.Counter(MercenarySystem.ConsecutiveLossesCounter));
        state = MercenarySystem.Process(state, win, economy);
        Assert.Equal(0, state.Counter(MercenarySystem.ConsecutiveLossesCounter));
    }

    [Fact]
    public void WagesAreDeductedEveryMatchWonOrLost()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7005UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(1000);
        state = state.WithNewPlayer(MakeMercenary(state, matchesBenched: 0));
        var mercenary = state.Roster.Last();

        var loss = Loss(new List<int> { state.Roster[0].Id });
        var next = MercenarySystem.Process(state, loss, economy);

        Assert.Equal(state.Gold - mercenary.Wage, next.Gold);
    }

    private static RunPlayer MakeMercenary(RunState state, int matchesBenched)
    {
        var template = state.Roster[0];
        return template with
        {
            Id = -1,
            IsMercenary = true,
            Wage = 15,
            MatchesBenched = matchesBenched,
            Race = template.Race == Race.Orc ? Race.Elf : Race.Orc,
        };
    }

    private static RunMatchSummary Win(IReadOnlyList<int> playedIds) => Summary(won: true, playedIds);

    private static RunMatchSummary Loss(IReadOnlyList<int> playedIds) => Summary(won: false, playedIds);

    private static RunMatchSummary Summary(bool won, IReadOnlyList<int> playedIds)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = won ? 1 : 0;
        builder.Goals[1] = won ? 0 : 1;
        builder.Winner = won ? 0 : 1;
        var report = builder.Build();
        return new RunMatchSummary(101, NodeKind.LeagueMatch, won, builder.Goals[0], builder.Goals[1], 500, false, playedIds, Array.Empty<int>(), 0, 0, report);
    }
}
