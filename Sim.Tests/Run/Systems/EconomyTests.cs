using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>RF-114g..k: oro fijo por acto y dificultad, nunca escalado por el rendimiento dentro del partido.</summary>
public sealed class EconomyTests
{
    private static readonly ulong Seed = 909090UL;

    [Fact]
    public void GoldDoesNotScaleWithMatchPerformance()
    {
        var state = RunTestState();
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

        // Mismo marcador (así el objetivo anunciado, si depende de goles, se cumple o no igual en los
        // dos casos), pero lesiones y duración del partido radicalmente distintas: RF-114i exige que el
        // oro no se mueva ni un punto por eso.
        var mild = Summary(node.Id, won: true, goalsFor: 2, goalsAgainst: 1, ticks: 400, injuries: 0);
        var brutal = Summary(node.Id, won: true, goalsFor: 2, goalsAgainst: 1, ticks: 1300, injuries: 6);

        int goldMild = GoldCalculator.GoldForWin(state, node, mild, SystemsTestSupport.Systems.Economy);
        int goldBrutal = GoldCalculator.GoldForWin(state, node, brutal, SystemsTestSupport.Systems.Economy);

        Assert.Equal(goldMild, goldBrutal);
    }

    [Fact]
    public void LosingNeverPays()
    {
        var state = RunTestState();
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var loss = Summary(node.Id, won: false, goalsFor: 0, goalsAgainst: 3, ticks: 900, injuries: 2);

        var next = SystemsTestSupport.Systems.AfterMatch(state, node, loss, SystemsTestSupport.Catalog);

        Assert.Equal(state.Gold, next.Gold);
        Assert.Equal(-1, next.PendingNodeId);
    }

    [Fact]
    public void EliteAndBossPayMoreThanLeagueAtSameActAndDifficulty()
    {
        var state = RunTestState();
        var economy = SystemsTestSupport.Systems.Economy;

        var league = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
        var elite = new MapNode(102, 1, 0, 0, NodeKind.EliteMatch, Array.Empty<int>(), string.Empty, 3);
        var boss = new MapNode(103, 1, 0, 0, NodeKind.Boss, Array.Empty<int>(), string.Empty, 3);

        int leagueGold = GoldCalculator.GoldForWin(state, league, Summary(league.Id, true, 1, 0, 500, 0), economy);
        int eliteGold = GoldCalculator.GoldForWin(state, elite, Summary(elite.Id, true, 1, 0, 500, 0), economy);
        int bossGold = GoldCalculator.GoldForWin(state, boss, Summary(boss.Id, true, 1, 0, 500, 0), economy);

        Assert.True(eliteGold > leagueGold);
        Assert.True(bossGold > eliteGold);
    }

    [Fact]
    public void HigherDifficultyPaysMoreAtSameAct()
    {
        var state = RunTestState();
        var economy = SystemsTestSupport.Systems.Economy;

        var easy = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 1);
        var hard = new MapNode(102, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 5);

        int goldEasy = GoldCalculator.GoldForWin(state, easy, Summary(easy.Id, true, 1, 0, 500, 0), economy);
        int goldHard = GoldCalculator.GoldForWin(state, hard, Summary(hard.Id, true, 1, 0, 500, 0), economy);

        Assert.True(goldHard > goldEasy);
    }

    [Fact]
    public void ExcellentMatchBonusIsFixedNotProportionalToMargin()
    {
        var state = RunTestState();
        var economy = SystemsTestSupport.Systems.Economy;
        var node = FindNodeWithObjective(state.Seed, ExcellentMatchObjective.WinByThreeOrMore);

        var narrow = Summary(node.Id, won: true, goalsFor: 3, goalsAgainst: 0, ticks: 500, injuries: 0);
        var crushing = Summary(node.Id, won: true, goalsFor: 9, goalsAgainst: 0, ticks: 500, injuries: 0);
        var miss = Summary(node.Id, won: true, goalsFor: 1, goalsAgainst: 0, ticks: 500, injuries: 0);

        int goldNarrow = GoldCalculator.GoldForWin(state, node, narrow, economy);
        int goldCrushing = GoldCalculator.GoldForWin(state, node, crushing, economy);
        int goldMiss = GoldCalculator.GoldForWin(state, node, miss, economy);

        Assert.Equal(goldNarrow, goldCrushing);
        Assert.Equal(economy.ExcellentMatchBonusGold, goldNarrow - goldMiss);
    }

    [Fact]
    public void ExcellentMatchBonusForCleanSheetIgnoresGoalsScored()
    {
        var state = RunTestState();
        var economy = SystemsTestSupport.Systems.Economy;
        var node = FindNodeWithObjective(state.Seed, ExcellentMatchObjective.CleanSheet);

        var oneNil = Summary(node.Id, won: true, goalsFor: 1, goalsAgainst: 0, ticks: 500, injuries: 0);
        var sevenNil = Summary(node.Id, won: true, goalsFor: 7, goalsAgainst: 0, ticks: 500, injuries: 0);
        var conceded = Summary(node.Id, won: true, goalsFor: 4, goalsAgainst: 1, ticks: 500, injuries: 0);

        int goldOneNil = GoldCalculator.GoldForWin(state, node, oneNil, economy);
        int goldSevenNil = GoldCalculator.GoldForWin(state, node, sevenNil, economy);
        int goldConceded = GoldCalculator.GoldForWin(state, node, conceded, economy);

        Assert.Equal(goldOneNil, goldSevenNil);
        Assert.Equal(economy.ExcellentMatchBonusGold, goldOneNil - goldConceded);
    }

    private static RunMatchSummary Summary(
        int nodeId,
        bool won,
        int goalsFor,
        int goalsAgainst,
        int ticks,
        int injuries,
        int playedCount = 7)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = goalsFor;
        builder.Goals[1] = goalsAgainst;
        builder.Winner = won ? 0 : 1;
        builder.Ticks = ticks;
        var report = builder.Build();

        var played = new List<int>();
        for (int i = 0; i < playedCount; i++)
        {
            played.Add(i);
        }

        return new RunMatchSummary(
            NodeId: nodeId,
            Kind: NodeKind.LeagueMatch,
            Won: won,
            GoalsFor: goalsFor,
            GoalsAgainst: goalsAgainst,
            Ticks: ticks,
            WentToGoldenGoal: false,
            PlayedPlayerIds: played,
            BenchedPlayerIds: Array.Empty<int>(),
            OwnInjuries: injuries,
            OwnDeaths: 0,
            Report: report);
    }

    /// <summary>Busca, para esa semilla, un id de nodo cuyo objetivo anunciado sea el pedido (búsqueda determinista, sin azar de test).</summary>
    private static MapNode FindNodeWithObjective(ulong seed, ExcellentMatchObjective objective)
    {
        for (int id = 101; id < 101 + 500; id++)
        {
            var candidate = new MapNode(id, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);
            if (ExcellentMatchObjectives.For(seed, candidate) == objective)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"no se ha encontrado ningún nodo con el objetivo {objective} en 500 intentos");
    }

    private static RunState RunTestState() =>
        RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
}
