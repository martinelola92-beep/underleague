using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.View;
using Underleague.Sim.Tests.Run;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0172 (BA-H, enmienda de RF-080..082 y RF-085): dos huecos de consumible. El hueco <b>es</b> la
/// posesión —no hay inventario suelto—, comprar exige un hueco libre y lo deja ya equipado y manual, lo que no
/// se usa se queda para el partido siguiente y un guardado anterior se pliega a los dos huecos.
/// </summary>
public sealed class ConsumableSlotsTests
{
    private static Underleague.Sim.Data.Catalog Catalog => SystemsTestSupport.Catalog;

    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static RunState AtAMarket(ulong seed = 172UL, int gold = 400)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, Systems).WithGold(gold);
        return SystemsTestSupport.WithFakePendingNode(state, NodeKind.Market);
    }

    private static MarketScreenView ViewOf(RunState state) =>
        MarketView.Build(state, Catalog, Systems.Economy, Systems.Items, Systems.Consumables)!;

    // ------------------------------------------------------------------ el estado

    [Fact]
    public void ThereAreTwoSlotsAndATakenConsumableEntersManual()
    {
        Assert.Equal(2, RunRules.ConsumableSlots);
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 1, Catalog, Systems);
        Assert.True(state.HasFreeConsumableSlot);

        state = state.WithTakenConsumable("field_bandage");
        var only = Assert.Single(state.Consumables);
        Assert.Equal(new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty), only);
        Assert.True(state.CarriesConsumable("field_bandage"));
        Assert.False(state.CanTakeConsumable("field_bandage"));
        Assert.True(state.CanTakeConsumable("lucky_charm"));

        state = state.WithTakenConsumable("lucky_charm");
        Assert.False(state.HasFreeConsumableSlot);
        Assert.Throws<InvalidOperationException>(() => state.WithTakenConsumable("smoke_flare"));
    }

    [Fact]
    public void TheSameConsumableCannotFillBothSlots()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 1, Catalog, Systems).WithTakenConsumable("field_bandage");
        Assert.Throws<InvalidOperationException>(() => state.WithTakenConsumable("field_bandage"));
    }

    // ------------------------------------------------------------------ SetConsumables: reconfigura, no crea

    [Fact]
    public void SetConsumablesReconfiguresAndDiscardsButNeverCreates()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 2, Catalog, Systems)
            .WithTakenConsumable("field_bandage")
            .WithTakenConsumable("lucky_charm");

        // Pasar los dos a condicional es legítimo: ya no hace falta uno manual (RF-082 enmendada).
        var both = RunEngine.Apply(
            state,
            new SetConsumables(new[]
            {
                new EquippedConsumable("field_bandage", ConsumableMode.Conditional, "ownInjury"),
                new EquippedConsumable("lucky_charm", ConsumableMode.Conditional, "lastSeconds"),
            }),
            Catalog,
            Systems);
        Assert.All(both.Consumables, c => Assert.Equal(ConsumableMode.Conditional, c.Mode));

        // Descartar libera el hueco.
        var discarded = RunEngine.Apply(both, new SetConsumables(new[] { both.Consumables[1] }), Catalog, Systems);
        Assert.Single(discarded.Consumables);
        Assert.True(discarded.HasFreeConsumableSlot);

        // No se equipa lo que no se lleva (antes «cualquier id»: el límite X-9), ni se repite, ni sobra uno.
        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            state, new SetConsumables(new[] { new EquippedConsumable("smoke_flare", ConsumableMode.Manual, string.Empty) }), Catalog, Systems));
        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            state,
            new SetConsumables(new[]
            {
                new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty),
                new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty),
            }),
            Catalog,
            Systems));
        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            state,
            new SetConsumables(new[]
            {
                new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty),
                new EquippedConsumable("lucky_charm", ConsumableMode.Manual, string.Empty),
                new EquippedConsumable("smoke_flare", ConsumableMode.Manual, string.Empty),
            }),
            Catalog,
            Systems));
    }

    // ------------------------------------------------------------------ el mercado

    [Fact]
    public void BuyingAConsumableLeavesItEquippedAndManual()
    {
        var state = AtAMarket();
        var view = ViewOf(state);
        var row = view.Consumables.First(r => r.Affordable && r.Block == RewardBlock.None);

        var after = RunEngine.Apply(state, new BuyOffer(MarketCategories.Consumable, row.Index), Catalog, Systems);

        var taken = Assert.Single(after.Consumables);
        Assert.Equal(new EquippedConsumable(row.Id, ConsumableMode.Manual, string.Empty), taken);
        Assert.Equal(state.Gold - row.Price, after.Gold);
        Assert.DoesNotContain(after.Counters, c => c.Key.StartsWith(RunState.LegacyConsumableOwnedPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void WithBothSlotsFullTheMarketBlocksConsumablesAndRefusesTheSale()
    {
        var state = AtAMarket();
        var offered = ViewOf(state).Consumables.Select(r => r.Id).ToHashSet();
        var others = Systems.Consumables.All.Select(c => c.Id).Where(id => !offered.Contains(id)).Take(2).ToArray();
        var full = state.WithTakenConsumable(others[0]).WithTakenConsumable(others[1]);

        var view = ViewOf(full);
        Assert.NotEmpty(view.Consumables);
        Assert.All(view.Consumables, r => Assert.Equal(RewardBlock.NoConsumableSlot, r.Block));

        var error = Assert.Throws<ArgumentException>(
            () => RunEngine.Apply(full, new BuyOffer(MarketCategories.Consumable, view.Consumables[0].Index), Catalog, Systems));
        Assert.Contains("huecos", error.Message);
    }

    [Fact]
    public void ACarriedConsumableIsBlockedAsAlreadyCarriedAndTheRestStayBuyable()
    {
        var state = AtAMarket();
        var first = ViewOf(state).Consumables[0];
        var carrying = state.WithTakenConsumable(first.Id);

        var view = ViewOf(carrying);
        Assert.Equal(RewardBlock.AlreadyCarried, view.Consumables.Single(r => r.Id == first.Id).Block);
        Assert.All(view.Consumables.Where(r => r.Id != first.Id), r => Assert.Equal(RewardBlock.None, r.Block));
        Assert.Throws<ArgumentException>(
            () => RunEngine.Apply(carrying, new BuyOffer(MarketCategories.Consumable, first.Index), Catalog, Systems));
    }

    // ------------------------------------------------------------------ el partido

    [Fact]
    public void OnlyTheConsumableThatFiredLeavesItsSlot()
    {
        // RF-085 (enmendada): el manual que el jugador pulsa se gasta; el que no pulsa se queda en su hueco
        // con su modo, para el partido siguiente.
        const int T = 300;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 17200UL, Catalog, Systems)
            .WithTakenConsumable("field_bandage")
            .WithTakenConsumable("hold_the_line");
        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, Systems);
        var decisions = MatchDecisions.None with { ManualActivations = new[] { new ManualActivation("field_bandage", T) } };

        var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, Systems, decisions);

        var left = Assert.Single(entry.State.Consumables);
        Assert.Equal(new EquippedConsumable("hold_the_line", ConsumableMode.Manual, string.Empty), left);
        Assert.Contains(entry.Summary.Report.ConsumableActivations, a => a.ConsumableId == "field_bandage" && a.Team == 0);
    }

    // ------------------------------------------------------------------ guardados

    [Fact]
    public void ASaveWithTwoSlotsRoundTripsUntouched()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 3, Catalog, Systems)
            .WithConsumables(new[]
            {
                new EquippedConsumable("field_bandage", ConsumableMode.Conditional, "ownInjury"),
                new EquippedConsumable("lucky_charm", ConsumableMode.Manual, string.Empty),
            });

        var loaded = RunSave.Load(RunSave.Save(state));

        Assert.Equal(state.Consumables, loaded.Consumables);
        Assert.Same(loaded, loaded.WithLegacyConsumablesFolded());
    }

    [Fact]
    public void AnOldSaveFoldsItsLooseStockIntoTheTwoSlots()
    {
        // Antes de la ADR 0172 la run llevaba un inventario suelto (contadores «consumable_owned:») y hasta
        // tres equipados. Cargarla pliega lo equipado primero (hasta dos, con su modo) y las copias sueltas
        // después (por id, manuales, sin repetir); lo que no cabe se pierde y los contadores viejos se borran.
        const string Prefix = RunState.LegacyConsumableOwnedPrefix;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 4, Catalog, Systems)
            .WithCounter(Prefix + "field_bandage", 2)
            .WithCounter(Prefix + "lucky_charm", 1)
            .WithCounter(Prefix + "smoke_flare", 3)
            .WithConsumables(new[]
            {
                new EquippedConsumable("smoke_flare", ConsumableMode.Manual, string.Empty),
                new EquippedConsumable("lucky_charm", ConsumableMode.Conditional, "lastSeconds"),
                new EquippedConsumable("field_bandage", ConsumableMode.Conditional, "ownInjury"),
            });

        var loaded = RunSave.Load(RunSave.Save(state));

        Assert.Equal(
            new[]
            {
                new EquippedConsumable("smoke_flare", ConsumableMode.Manual, string.Empty),
                new EquippedConsumable("lucky_charm", ConsumableMode.Conditional, "lastSeconds"),
            },
            loaded.Consumables);
        Assert.DoesNotContain(loaded.Counters, c => c.Key.StartsWith(Prefix, StringComparison.Ordinal));

        // Sin nada equipado, las copias sueltas rellenan los huecos por id ascendente (RT-041).
        var stockOnly = RunEngine.Start(SystemsTestSupport.Setup(), 5, Catalog, Systems)
            .WithCounter(Prefix + "smoke_flare", 1)
            .WithCounter(Prefix + "field_bandage", 1)
            .WithCounter(Prefix + "lucky_charm", 1);
        var folded = RunSave.Load(RunSave.Save(stockOnly)).Consumables;
        Assert.Equal(new[] { "field_bandage", "lucky_charm" }, folded.Select(c => c.Id));
        Assert.All(folded, c => Assert.Equal(ConsumableMode.Manual, c.Mode));
    }
}
