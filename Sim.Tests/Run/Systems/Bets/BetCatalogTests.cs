using Underleague.Sim.Data;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Sim.Tests.Run.Systems.Bets;

/// <summary>ADR 0157: carga y validación de <c>data/bets/bets.json</c>.</summary>
public sealed class BetCatalogTests
{
    private static BetCatalog Catalog => BetLoader.FromJson(TestData.LoadAllFiles());

    [Fact]
    public void EveryConditionHasExactlyOneCard()
    {
        var catalog = Catalog;
        Assert.Equal(Enum.GetValues<BetKind>().Length, catalog.All.Count);
        foreach (var kind in Enum.GetValues<BetKind>())
        {
            Assert.NotNull(catalog.Find(kind));
        }
    }

    [Fact]
    public void CardsAreOrderedByIdAndFindableById()
    {
        var catalog = Catalog;
        var ids = catalog.All.Select(b => b.Id).ToList();
        Assert.Equal(ids.OrderBy(i => i, StringComparer.Ordinal).ToList(), ids);
        Assert.Equal(BetKind.HuntTheStar, catalog.Find("hunt_the_star")!.Kind);
        Assert.Null(catalog.Find("no_existe"));
    }

    [Fact]
    public void StakesAndPayoutsHaveTheDeclaredShape()
    {
        foreach (var bet in Catalog.All)
        {
            Assert.Equal(3, bet.StakeByAct.Count);
            Assert.Equal(5, bet.PayoutPercentByDifficulty.Count);
            Assert.All(bet.PayoutPercentByDifficulty, p => Assert.InRange(p, 100, 2000));
            Assert.Equal(bet.StakeByAct[0], bet.StakeFor(1));
            Assert.Equal(bet.StakeByAct[2], bet.StakeFor(3));
            Assert.Equal(bet.PayoutPercentByDifficulty[0], bet.PayoutPercentFor(1));
            Assert.Equal(bet.PayoutPercentByDifficulty[4], bet.PayoutPercentFor(5));
            Assert.False(string.IsNullOrWhiteSpace(bet.Name.Es));
            Assert.False(string.IsNullOrWhiteSpace(bet.Name.En));
            Assert.False(string.IsNullOrWhiteSpace(bet.Condition.Es));
            Assert.False(string.IsNullOrWhiteSpace(bet.Condition.En));
        }
    }

    /// <summary>Solo <c>hunt_the_star</c> nombra a un jugador: es el único cuyo texto lleva {player}.</summary>
    [Fact]
    public void OnlyHuntTheStarTemplatesAPlayerName()
    {
        foreach (var bet in Catalog.All)
        {
            bool templated = bet.Condition.Es.Contains("{player}") && bet.Condition.En.Contains("{player}");
            Assert.Equal(bet.Kind == BetKind.HuntTheStar, templated);
        }
    }

    [Fact]
    public void AMissingFileIsAnExplicitError()
    {
        var ex = Assert.Throws<DataException>(() => BetLoader.FromJson(new Dictionary<string, string>()));
        Assert.Equal("bets/bets.json", ex.File);
    }

    [Fact]
    public void AnUnknownKindIsAnExplicitError()
    {
        string content = TestData.LoadAllFiles()["bets/bets.json"].Replace("\"BloodBeforeGoals\"", "\"Inventada\"");
        var files = new Dictionary<string, string> { ["bets/bets.json"] = content };
        Assert.Throws<DataException>(() => BetLoader.FromJson(files));
    }

    [Fact]
    public void ARepeatedConditionIsAnExplicitError()
    {
        string content = TestData.LoadAllFiles()["bets/bets.json"].Replace("\"kind\": \"Thrashing\"", "\"kind\": \"Comeback\"");
        var files = new Dictionary<string, string> { ["bets/bets.json"] = content };
        Assert.Throws<DataException>(() => BetLoader.FromJson(files));
    }

    [Fact]
    public void APayoutBelowTheStakeIsAnExplicitError()
    {
        string content = TestData.LoadAllFiles()["bets/bets.json"];
        int at = content.IndexOf("\"payoutPercentByDifficulty\": [", StringComparison.Ordinal);
        int end = content.IndexOf(']', at);
        string broken = content[..at] + "\"payoutPercentByDifficulty\": [50, 200, 200, 200, 200" + content[end..];
        var files = new Dictionary<string, string> { ["bets/bets.json"] = broken };
        Assert.Throws<DataException>(() => BetLoader.FromJson(files));
    }
}
