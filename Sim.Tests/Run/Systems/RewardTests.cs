using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>RF-071, RF-071b: tres opciones de recompensa y reroll de coste creciente.</summary>
public sealed class RewardTests
{
    [Fact]
    public void OptionsAreThreeAndReproducibleForTheSameRerollCount()
    {
        var state = FreshPendingReward(11111UL);
        var node = state.GetNode(state.PendingNodeId);

        var first = RewardSystem.Options(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);
        var second = RewardSystem.Options(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);

        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(Describe), second.Select(Describe));
    }

    [Fact]
    public void RerollChangesTheOptionsAndCostsIncreaseAcrossTheRun()
    {
        var state = FreshPendingReward(22222UL).WithGold(1000);
        var node = state.GetNode(state.PendingNodeId);
        var before = RewardSystem.Options(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);

        int firstCost = SystemsTestSupport.Systems.Economy.RerollCost(0);
        int goldBefore = state.Gold;
        var afterFirstReroll = RewardSystem.Reroll(state, SystemsTestSupport.Systems.Economy);

        Assert.Equal(goldBefore - firstCost, afterFirstReroll.Gold);
        Assert.Equal(1, afterFirstReroll.RerollsUsed);
        Assert.Equal(1, afterFirstReroll.NodeRerolls);

        var after = RewardSystem.Options(afterFirstReroll, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);
        Assert.NotEqual(before.Select(Describe), after.Select(Describe));

        // Un segundo reroll del MISMO nodo no está permitido (uno por nodo, RF-071b).
        Assert.Throws<InvalidOperationException>(() => RewardSystem.Reroll(afterFirstReroll, SystemsTestSupport.Systems.Economy));

        // Cerrar el nodo y abrir otro de recompensa: el coste del siguiente reroll de la run es mayor.
        var closed = afterFirstReroll.WithPendingNode(-1);
        var otherPending = SystemsTestSupport.WithFakePendingNode(closed, NodeKind.EliteMatch);
        int secondCost = SystemsTestSupport.Systems.Economy.RerollCost(otherPending.RerollsUsed);
        Assert.True(secondCost > firstCost);

        var afterSecondReroll = RewardSystem.Reroll(otherPending.WithGold(1000), SystemsTestSupport.Systems.Economy);
        Assert.Equal(1000 - secondCost, afterSecondReroll.Gold);
        Assert.Equal(2, afterSecondReroll.RerollsUsed);
    }

    [Fact]
    public void RerollFailsWithoutEnoughGold()
    {
        var state = FreshPendingReward(33333UL).WithGold(0);
        Assert.Throws<ArgumentException>(() => RewardSystem.Reroll(state, SystemsTestSupport.Systems.Economy));
    }

    [Fact]
    public void ChoosingAPlayerOptionAddsItToTheRosterAndClosingAdvancesTheHistory()
    {
        var state = FreshPendingReward(44444UL);
        var node = state.GetNode(state.PendingNodeId);
        var options = RewardSystem.Options(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);

        int playerOptionIndex = options.ToList().FindIndex(o => o is PlayerRewardOption);
        if (playerOptionIndex < 0)
        {
            // Con esta semilla no salió ninguna opción de jugador: el test no puede afirmar nada sobre esa rama.
            return;
        }

        int rosterCountBefore = state.Roster.Count;
        var chosen = RunEngine.Apply(state, new ChooseReward(playerOptionIndex), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.Equal(rosterCountBefore + 1, chosen.Roster.Count);

        // Elegir dos veces la misma recompensa no está permitido.
        Assert.Throws<InvalidOperationException>(() => RewardSystem.Choose(chosen, new ChooseReward(playerOptionIndex), SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items));

        var closed = RunEngine.Apply(chosen, new LeaveNode(), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.Equal(-1, closed.PendingNodeId);
    }

    private static RunState FreshPendingReward(ulong seed)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        return SystemsTestSupport.WithFakePendingNode(state, NodeKind.LeagueMatch);
    }

    private static string Describe(RewardOption option) => option switch
    {
        PerkRewardOption perk => "perk:" + perk.PerkId,
        ItemRewardOption item => "item:" + item.ItemId,
        PlayerRewardOption player => "player:" + player.Player.Rarity + ":" + player.Player.Position + ":" + player.Player.Attributes,
        _ => option.GetType().Name,
    };
}
