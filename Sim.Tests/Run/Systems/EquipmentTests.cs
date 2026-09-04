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
        int expectedPrice = economy.Market.ItemPrice.Of(displaced.Rarity) * economy.Market.ItemSellFractionPercent / 100;
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
        int expectedPrice = economy.Market.ItemPrice.Of(item.Rarity) * economy.Market.ItemSellFractionPercent / 100;
        Assert.Equal(expectedPrice, sold.Gold);
        Assert.True(expectedPrice < economy.Market.ItemPrice.Of(item.Rarity), "vender debe pagar solo una fracción del valor del objeto (RF-076b)");
    }

    [Fact]
    public void FragileItemBreaksAfterItsUsesLimit()
    {
        var items = SystemsTestSupport.Systems.Items;
        var fragile = items.All.First(i => i.Archetype == ItemArchetype.Fragile);
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8004UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var owner = state.Roster[0] with { Item = fragile.Id, PhysicalState = PhysicalState.Healthy };
        state = state.WithPlayer(owner);
        var playedIds = new List<int> { owner.Id };

        for (int use = 1; use < fragile.UsesLimit; use++)
        {
            var summary = HealthySummary(playedIds);
            state = EquipmentSystem.ProcessFragileItems(state, summary, items);
            Assert.Equal(fragile.Id, state.GetPlayer(owner.Id).Item);
        }

        state = EquipmentSystem.ProcessFragileItems(state, HealthySummary(playedIds), items);
        Assert.Null(state.GetPlayer(owner.Id).Item);
    }

    [Fact]
    public void FragileItemBreaksImmediatelyIfTheWearerIsInjured()
    {
        var items = SystemsTestSupport.Systems.Items;
        var fragile = items.All.First(i => i.Archetype == ItemArchetype.Fragile);
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 8005UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var owner = state.Roster[0] with { Item = fragile.Id, PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 1 };
        state = state.WithPlayer(owner);

        var summary = HealthySummary(new List<int> { owner.Id });
        var next = EquipmentSystem.ProcessFragileItems(state, summary, items);

        Assert.Null(next.GetPlayer(owner.Id).Item);
    }

    private static RunMatchSummary HealthySummary(IReadOnlyList<int> playedIds)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 1;
        builder.Winner = 0;
        return new RunMatchSummary(101, NodeKind.LeagueMatch, true, 1, 0, 500, false, playedIds, Array.Empty<int>(), 0, 0, builder.Build());
    }
}
