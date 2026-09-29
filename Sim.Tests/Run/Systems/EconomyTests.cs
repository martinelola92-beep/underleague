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

        // ADR 0096: la liga cambió su elección por oro, así que en MONEDA paga más que el élite; el
        // escalón de la ADR 0043 se lee en valor total (el élite añade una elección, el jefe dos y la cura).
        Assert.True(leagueGold > eliteGold);
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

    /// <summary>
    /// RF-114i (ADR 0157): el oro del partido no escala con el rendimiento. Con el partido excelente ya no
    /// hay bonus por objetivo: ganar 1-0, por 3 o por 9 paga exactamente lo mismo, que es el desglose base.
    /// </summary>
    [Fact]
    public void WinGoldIsTheSameWhateverTheMargin()
    {
        var state = RunTestState();
        var economy = SystemsTestSupport.Systems.Economy;
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

        var golds = new[] { (1, 0), (3, 0), (9, 0), (4, 1) }
            .Select(g => GoldCalculator.GoldForWin(state, node, Summary(node.Id, true, g.Item1, g.Item2, 500, 0), economy))
            .ToList();
        var breakdown = GoldCalculator.Breakdown(state, node, Summary(node.Id, true, 1, 0, 500, 0), economy);

        Assert.All(golds, g => Assert.Equal(breakdown.AfterDifficulty + breakdown.NodeBonus, g));
    }

    /// <summary>ADR 0157: el partido excelente (RF-114h) ya no existe ni en el código ni en los datos.</summary>
    [Fact]
    public void TheExcellentMatchNoLongerExists()
    {
        Assert.Null(typeof(GoldForWinBreakdown).GetProperty("Objective"));
        Assert.Null(typeof(GoldForWinBreakdown).GetProperty("ObjectiveBonus"));
        Assert.Null(typeof(EconomyConfig).GetProperty("ExcellentMatchBonusGold"));
        Assert.DoesNotContain("excellentMatchBonusGold", TestData.LoadAllFiles()["economy/economy.json"]);
        Assert.Null(typeof(GoldCalculator).Assembly.GetType("Underleague.Sim.Run.Systems.Economy.ExcellentMatchObjectives"));
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

    private static RunState RunTestState() =>
        RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
}
