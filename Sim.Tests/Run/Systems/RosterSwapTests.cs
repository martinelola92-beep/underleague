using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.Systems.Rewards;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// BX-5: con la plantilla llena, fichar (mercado, mercenario, recompensa) puede hacerse soltando a uno propio a cambio.
/// No es una regla de economía nueva: es la de siempre —vender si el veto de la ADR 0108 lo permite, descartar si no— en
/// una sola decisión atómica.
/// </summary>
public sealed class RosterSwapTests
{
    private static readonly Underleague.Sim.Run.Systems.Economy.EconomyConfig Economy = SystemsTestSupport.Systems.Economy;

    private static RunState Full(ulong seed, NodeKind node)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        state = state.WithNewPlayer(state.Roster[0] with { Id = -1 });
        Assert.False(state.HasRosterSpace);
        return SystemsTestSupport.WithFakePendingNode(state, node);
    }

    private static (RunState State, int Index) MarketWithARecruit()
    {
        for (ulong seed = 1; seed < 80; seed++)
        {
            var state = Full(seed, NodeKind.Market).WithGold(10_000);
            var node = state.GetNode(state.PendingNodeId);
            var offers = MarketOfferGenerator.Generate(
                state, node, SystemsTestSupport.Catalog, Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);
            if (offers.Recruits.Count > 0)
            {
                return (state, 0);
            }
        }

        throw new InvalidOperationException("instrumento: ninguna semilla ofrece un fichaje en el mercado");
    }

    [Fact]
    public void ABuyWithAFullRosterAndNoReplacementIsStillRefused()
    {
        var (state, index) = MarketWithARecruit();
        Assert.Throws<ArgumentException>(
            () => RunEngine.Apply(state, new BuyOffer(MarketCategories.Player, index), SystemsTestSupport.Catalog, SystemsTestSupport.Systems));
    }

    [Fact]
    public void AReplacedPlayerWhoHasPlayedIsSoldAtTheUsualPrice()
    {
        var (state, index) = MarketWithARecruit();
        var out1 = state.Roster[2] with { Experience = 50 };
        state = state.WithPlayer(out1);
        int expected = MarketSystem.SalePrice(out1, Economy);
        var node = state.GetNode(state.PendingNodeId);
        int price = MarketOfferGenerator.Generate(
            state, node, SystemsTestSupport.Catalog, Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables)
            .Recruits[index].Price;

        var view = RosterSwapView.Candidates(state, Economy).Single(c => c.PlayerId == out1.Id);
        Assert.Equal(SwapOutcome.Sold, view.Outcome);
        Assert.Equal(expected, view.Gold);

        var next = RunEngine.Apply(
            state, new BuyOffer(MarketCategories.Player, index, -1, out1.Id), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

        Assert.Equal(state.Roster.Count, next.Roster.Count);
        Assert.Null(next.FindPlayer(out1.Id));
        Assert.Equal(state.Gold + expected - price, next.Gold);
    }

    [Fact]
    public void AReplacedPlayerWhoHasNotPlayedIsReleasedWithoutPay()
    {
        var (state, index) = MarketWithARecruit();
        var fresh = state.Roster[2] with { Experience = 0 };
        state = state.WithPlayer(fresh);
        var node = state.GetNode(state.PendingNodeId);
        int price = MarketOfferGenerator.Generate(
            state, node, SystemsTestSupport.Catalog, Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables)
            .Recruits[index].Price;

        var view = RosterSwapView.Candidates(state, Economy).Single(c => c.PlayerId == fresh.Id);
        Assert.Equal(SwapOutcome.Released, view.Outcome);
        Assert.Equal(0, view.Gold);

        var next = RunEngine.Apply(
            state, new BuyOffer(MarketCategories.Player, index, -1, fresh.Id), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

        Assert.Null(next.FindPlayer(fresh.Id));
        Assert.Equal(state.Gold - price, next.Gold);
    }

    [Fact]
    public void ASwapThatCannotBePaidChangesNothing()
    {
        var (state, index) = MarketWithARecruit();
        state = state.WithGold(0);
        var node = state.GetNode(state.PendingNodeId);
        int price = MarketOfferGenerator.Generate(
            state, node, SystemsTestSupport.Catalog, Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables)
            .Recruits[index].Price;
        Assert.True(price > 0, "instrumento: el fichaje tiene que costar oro");

        var fresh = state.Roster[2] with { Experience = 0 };
        state = state.WithPlayer(fresh);
        Assert.Throws<ArgumentException>(() => RunEngine.Apply(
            state, new BuyOffer(MarketCategories.Player, index, -1, fresh.Id), SystemsTestSupport.Catalog, SystemsTestSupport.Systems));
        Assert.NotNull(state.FindPlayer(fresh.Id));
    }

    [Fact]
    public void ThereIsNothingToReplaceWhileTheRosterHasRoom_AndTheDeadAreNeverOffered()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 55UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.True(state.HasRosterSpace);
        Assert.Empty(RosterSwapView.Candidates(state, Economy));

        var full = state.WithNewPlayer(state.Roster[0] with { Id = -1 });
        var dead = full.Roster[1] with { PhysicalState = PhysicalState.Dead };
        full = full.WithPlayer(dead);
        Assert.DoesNotContain(RosterSwapView.Candidates(full, Economy), c => c.PlayerId == dead.Id);
        Assert.Throws<ArgumentException>(() => Underleague.Sim.Run.Systems.Nodes.EnrollmentSystem.MakeRoomFor(full, dead.Id, Economy));
    }

    [Fact]
    public void ARewardPlayerCanBeTakenReleasingAnOwnOne()
    {
        for (ulong seed = 1; seed < 120; seed++)
        {
            var state = Full(seed, NodeKind.EliteMatch);
            var node = state.GetNode(state.PendingNodeId);
            var options = RewardSystem.Options(
                state, node, SystemsTestSupport.Catalog, Economy, SystemsTestSupport.Systems.Items).ToList();
            int index = options.FindIndex(o => o is PlayerRewardOption);
            if (index < 0)
            {
                continue;
            }

            // Sin cambio, la plantilla llena sigue bloqueando (ADR 0046).
            Assert.Throws<InvalidOperationException>(() => RunEngine.Apply(
                state, new ChooseReward(index), SystemsTestSupport.Catalog, SystemsTestSupport.Systems));

            var out1 = state.Roster[3];
            Assert.All(RosterSwapView.Candidates(state, null), c => Assert.Equal(SwapOutcome.Released, c.Outcome));
            var next = RunEngine.Apply(
                state, new ChooseReward(index, -1, out1.Id), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

            Assert.Equal(state.Roster.Count, next.Roster.Count);
            Assert.Null(next.FindPlayer(out1.Id));
            Assert.Equal(state.Gold, next.Gold);
            return;
        }

        throw new InvalidOperationException("instrumento: ninguna semilla ofrece un jugador de recompensa");
    }

    /// <summary>
    /// Soltar a un tocado, a un grave o a un lisiado a cambio: el precio es el de su estado (ADR 0108), no el del sano, y el
    /// veto de «aún no ha jugado» manda sobre todos (se descarta). Es lo que enseña la vista antes de confirmar.
    /// </summary>
    [Fact]
    public void ReplacingAnInjuredOrCrippledPlayerPaysItsStatePriceAndAFreshOneIsReleased()
    {
        var (state, index) = MarketWithARecruit();
        var healthy = state.Roster[2] with { Experience = 50, PhysicalState = PhysicalState.Healthy };
        var minor = state.Roster[3] with { Experience = 50, PhysicalState = PhysicalState.MinorInjury };
        var severe = state.Roster[4] with { Experience = 50, PhysicalState = PhysicalState.SevereInjury };
        var crippled = state.Roster[5] with
        {
            Experience = 50,
            PhysicalState = PhysicalState.SevereInjury,
            Prostheses = Enumerable.Range(0, RunRules.MaxProstheses).Select(i => new RunProsthesis("slot" + i, "effect" + i)).ToList(),
        };
        var fresh = state.Roster[6] with { Experience = 0, PhysicalState = PhysicalState.SevereInjury };
        state = state.WithPlayer(healthy).WithPlayer(minor).WithPlayer(severe).WithPlayer(crippled).WithPlayer(fresh);
        Assert.True(crippled.IsCrippled);

        var rows = RosterSwapView.Candidates(state, Economy);
        foreach (var player in new[] { healthy, minor, severe, crippled })
        {
            var row = rows.Single(c => c.PlayerId == player.Id);
            Assert.Equal(SwapOutcome.Sold, row.Outcome);
            Assert.Equal(MarketSystem.SalePrice(player, Economy), row.Gold);

            var before = state.Gold;
            var next = RunEngine.Apply(
                state, new BuyOffer(MarketCategories.Player, index, -1, player.Id), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
            Assert.Equal(row.Gold, next.Gold - (before - PriceOf(state, index)));
        }

        Assert.True(MarketSystem.SalePrice(severe, Economy) < MarketSystem.SalePrice(healthy, Economy));
        Assert.Equal(SwapOutcome.Released, rows.Single(c => c.PlayerId == fresh.Id).Outcome);
        Assert.Equal(0, rows.Single(c => c.PlayerId == fresh.Id).Gold);
    }

    private static int PriceOf(RunState state, int index)
    {
        var node = state.GetNode(state.PendingNodeId);
        return MarketOfferGenerator.Generate(
            state, node, SystemsTestSupport.Catalog, Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables)
            .Recruits[index].Price;
    }
}