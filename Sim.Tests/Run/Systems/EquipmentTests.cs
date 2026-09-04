using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Equipment;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>RF-075..078: transferencia y venta de equipamiento, y ruptura de un objeto frágil.</summary>
public sealed class EquipmentTests
{
    [Fact]
    public void TransferMovesTheItemBetweenPlayers()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8001UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var from = state.Roster[0] with { Item = "worn_boots" };
        var to = state.Roster[1] with { Item = null };
        state = state.WithPlayer(from).WithPlayer(to);

        var next = EquipmentSystem.Apply(state, new TransferItem(from.Id, to.Id), SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);

        Assert.Null(next.GetPlayer(from.Id).Item);
        Assert.Equal("worn_boots", next.GetPlayer(to.Id).Item);
    }

    [Fact]
    public void TransferToAPlayerWhoAlreadyHasAnItemSellsTheDisplacedOne()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var items = SystemsTestSupport.Systems.Items;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8002UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var from = state.Roster[0] with { Item = "worn_boots" };
        var to = state.Roster[1] with { Item = "iron_gauntlets" };
        state = state.WithPlayer(from).WithPlayer(to).WithGold(0);

        var next = EquipmentSystem.Apply(state, new TransferItem(from.Id, to.Id), economy, items);

        Assert.Equal("worn_boots", next.GetPlayer(to.Id).Item);
        var displaced = items.Get("iron_gauntlets");
        int expectedPrice = ItemPricing.SalePrice(displaced, items.Scale, economy.Market);
        Assert.Equal(expectedPrice, next.Gold);
    }

    [Fact]
    public void SellingAnItemRequiresAnOpenMarketAndPaysAFractionOfItsValue()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var items = SystemsTestSupport.Systems.Items;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8003UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var owner = state.Roster[0] with { Item = "veteran_armband" };
        state = state.WithPlayer(owner).WithGold(0);

        // Fuera de un mercado abierto, vender (ToPlayerId < 0) se rechaza.
        Assert.Throws<InvalidOperationException>(() => EquipmentSystem.Apply(state, new TransferItem(owner.Id, -1), economy, items));

        var atMarket = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Market);
        var sold = EquipmentSystem.Apply(atMarket, new TransferItem(owner.Id, -1), economy, items);

        Assert.Null(sold.GetPlayer(owner.Id).Item);
        var item = items.Get("veteran_armband");
        int expectedPrice = ItemPricing.SalePrice(item, items.Scale, economy.Market);
        Assert.Equal(expectedPrice, sold.Gold);
        Assert.True(
            expectedPrice < ItemPricing.Price(item, items.Scale, economy.Market),
            "vender debe pagar solo una fracción del valor del objeto (RF-076b)");
    }

    [Fact]
    public void FragileItemBreaksWithItsAnnouncedChanceAtTheEndOfTheMatch()
    {
        // ADR 0036: la tirada se resuelve AL TERMINAR el partido, con la probabilidad que el objeto
        // anuncia. Se comprueba sobre muchos post-partidos, no sobre uno: lo que define al frágil es la
        // frecuencia, y esa frecuencia es la que se muestra antes de equiparlo (RF-012d).
        var items = SystemsTestSupport.Systems.Items;
        var fragile = items.All.First(i => i.Archetype == ItemArchetype.Fragile);
        var start = RunEngine.Start(SystemsTestSupport.Setup(), 8004UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var owner = start.Roster[0];

        int broken = 0;
        const int Trials = 400;
        for (int trial = 0; trial < Trials; trial++)
        {
            var state = start.WithPlayer(owner with { Item = fragile.Id, PhysicalState = PhysicalState.Healthy });
            var summary = HealthySummary(new List<int> { owner.Id }) with { NodeId = 200 + trial };
            state = EquipmentSystem.ProcessFragileItems(state, summary, items);
            if (state.GetPlayer(owner.Id).Item is null)
            {
                broken++;
                Assert.Equal(1, state.Counter(EquipmentSystem.ItemsBrokenCounter));
            }
        }

        double rate = 100.0 * broken / Trials;
        Assert.InRange(rate, fragile.BreakChancePercent - 8, fragile.BreakChancePercent + 8);
    }

    [Fact]
    public void OnlyFragileItemsEverBreak()
    {
        // Un objeto que no es frágil no se rompe nunca, pase lo que pase en el partido: la rotura es lo
        // único que distingue al arquetipo (ADR 0036), y su portador puede acabar lesionado sin perderlo.
        var items = SystemsTestSupport.Systems.Items;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8005UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var owner = state.Roster[0] with { Item = "worn_boots", PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 1 };
        state = state.WithPlayer(owner);

        for (int match = 0; match < 50; match++)
        {
            var summary = HealthySummary(new List<int> { owner.Id }) with { NodeId = 300 + match };
            state = EquipmentSystem.ProcessFragileItems(state, summary, items);
        }

        Assert.Equal("worn_boots", state.GetPlayer(owner.Id).Item);
        Assert.Equal(0, state.Counter(EquipmentSystem.ItemsBrokenCounter));
    }

    private static RunMatchSummary HealthySummary(IReadOnlyList<int> playedIds)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 1;
        builder.Winner = 0;
        return new RunMatchSummary(101, NodeKind.LeagueMatch, true, 1, 0, 500, false, playedIds, Array.Empty<int>(), 0, 0, builder.Build());
    }
}
