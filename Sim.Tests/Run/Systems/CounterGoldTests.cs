using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// Paquete Z, primitiva D: un contador de partido declarado en <c>data/economy/counter-gold.json</c>
/// (<see cref="CounterGoldTable"/>) paga oro al resolver el partido, junto al resto del oro y visible en
/// el mismo desglose (RF-119) que ya usan el acto, la dificultad y el objetivo cumplido.
/// </summary>
public sealed class CounterGoldTests
{
    private static readonly ulong Seed = 424242UL;

    [Fact]
    public void ACounterWithARateAddsGoldToTheBreakdownAndIsVisibleAsARow()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var baseline = Summary(node.Id, ownPlayerId);
        var withGoals = baseline with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "local_idol_goals", 3) },
        };

        var economy = EconomyWithRates(("local_idol_goals", 5));

        var withoutCounter = GoldCalculator.Breakdown(state, node, baseline, economy);
        var withCounter = GoldCalculator.Breakdown(state, node, withGoals, economy);

        // Paga y se ve: el total sube exactamente lo que declara la tarifa (3 unidades x 5 de oro), y la
        // fila del desglose dice de qué jugador y de qué contador salió (RF-119).
        Assert.Equal(0, withoutCounter.CounterGoldTotal);
        Assert.Empty(withoutCounter.CounterGoldRows);
        Assert.Equal(15, withCounter.CounterGoldTotal);
        Assert.Equal(withoutCounter.Total + 15, withCounter.Total);

        var row = Assert.Single(withCounter.CounterGoldRows);
        Assert.Equal(ownPlayerId, row.PlayerId);
        Assert.Equal("local_idol_goals", row.Counter);
        Assert.Equal(3, row.Delta);
        Assert.Equal(5, row.RatePerUnit);
        Assert.Equal(15, row.Gold);
    }

    [Fact]
    public void ACounterWithoutARateInTheTableDoesNotPay()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var summary = Summary(node.Id, ownPlayerId) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "not_in_the_tariff", 10) },
        };

        var economy = EconomyWithRates(("local_idol_goals", 5));

        var breakdown = GoldCalculator.Breakdown(state, node, summary, economy);

        Assert.Equal(0, breakdown.CounterGoldTotal);
        Assert.Empty(breakdown.CounterGoldRows);
    }

    /// <summary>Un contador del RIVAL (no está en la plantilla propia) nunca paga a este club.</summary>
    [Fact]
    public void ARivalsCounterNeverPaysThisClub()
    {
        var state = RunTestState();
        int rivalPlayerId = -1;
        while (state.FindPlayer(rivalPlayerId) is not null)
        {
            rivalPlayerId--;
        }

        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var summary = Summary(node.Id, state.Roster[0].Id) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(rivalPlayerId, "local_idol_goals", 3) },
        };

        var economy = EconomyWithRates(("local_idol_goals", 5));

        var breakdown = GoldCalculator.Breakdown(state, node, summary, economy);

        Assert.Equal(0, breakdown.CounterGoldTotal);
        Assert.Empty(breakdown.CounterGoldRows);
    }

    /// <summary>Sin la instantánea de <c>counter-gold.json</c>, ningún contador paga (comportamiento de hoy).</summary>
    [Fact]
    public void WithoutTheFileNoCounterPaysAnything()
    {
        var state = RunTestState();
        int ownPlayerId = state.Roster[0].Id;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var summary = Summary(node.Id, ownPlayerId) with
        {
            CounterDeltas = new[] { new PlayerCounterDelta(ownPlayerId, "local_idol_goals", 3) },
        };

        Assert.Equal(0, CounterGoldTable.Empty.Count);
        var economy = SystemsTestSupport.Systems.Economy;
        Assert.Equal(0, economy.CounterGold.Count);

        var breakdown = GoldCalculator.Breakdown(state, node, summary, economy);

        Assert.Equal(0, breakdown.CounterGoldTotal);
    }

    private static EconomyConfig EconomyWithRates(params (string Counter, int Rate)[] rates)
    {
        var pairs = string.Join(
            ", ", rates.Select(r => $"\"{r.Counter}\": {r.Rate}"));
        var table = CounterGoldTable.FromJson(new Dictionary<string, string>
        {
            [CounterGoldTable.Path] = $$"""{ "rates": { {{pairs}} } }""",
        });

        return SystemsTestSupport.Systems.Economy with { CounterGold = table };
    }

    private static RunMatchSummary Summary(int nodeId, int ownPlayerId)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 1;
        builder.Goals[1] = 0;
        builder.Winner = 0;
        builder.Ticks = 500;
        var report = builder.Build();

        return new RunMatchSummary(
            NodeId: nodeId,
            Kind: NodeKind.LeagueMatch,
            Won: true,
            GoalsFor: 1,
            GoalsAgainst: 0,
            Ticks: 500,
            WentToGoldenGoal: false,
            PlayedPlayerIds: new[] { ownPlayerId },
            BenchedPlayerIds: Array.Empty<int>(),
            OwnInjuries: 0,
            OwnDeaths: 0,
            Report: report);
    }

    private static RunState RunTestState() =>
        RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
}
