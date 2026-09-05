using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// RF-071, RF-071b, ADR 0052 §2: <b>tres</b> opciones en los tres tipos de nodo, con reroll de coste
/// creciente, y el escalonado de la recompensa por <b>rareza</b> —la liga degradada a común, el élite y
/// el jefe con la rareza alta— en vez de por número de opciones, que es como lo había hecho la ADR 0049
/// y salió al revés de lo buscado.
/// </summary>
public sealed class RewardTests
{
    [Fact]
    public void OptionsAreThreeInALeagueMatchAndReproducibleForTheSameRerollCount()
    {
        var state = FreshPendingReward(11111UL);
        var node = state.GetNode(state.PendingNodeId);

        var first = RewardSystem.Options(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);
        var second = RewardSystem.Options(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items);

        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(Describe), second.Select(Describe));
    }

    /// <summary>
    /// El escalonado por tipo de nodo sigue siendo de <b>calidad de decisión</b> y no solo de oro (ADR
    /// 0043), pero desde la ADR 0052 §2 lo es por <b>rareza</b> y no por número de opciones: los tres
    /// tipos de nodo ofrecen las <b>tres</b> opciones de RF-071, la liga las degrada a común y el élite y
    /// el jefe conservan la rareza alta, que es lo que los hace deseables. La ADR 0049 lo había hecho al
    /// revés —dos opciones en liga— y salió al contrario de lo buscado: la ventaja de la doctrina con
    /// criterio cayó de +5,6 a +0,2 puntos porque la ventaja no estaba en comprar sino en tener con qué
    /// elegir. Si esto se aplana, el mercado vuelve a ser prescindible.
    /// </summary>
    [Fact]
    public void TheEliteAndTheBossOfferBetterRarityThanALeagueMatch()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var league = economy.RewardFor(NodeKind.LeagueMatch);
        var elite = economy.RewardFor(NodeKind.EliteMatch);
        var boss = economy.RewardFor(NodeKind.Boss);

        Assert.Equal(3, league.Options);
        Assert.Equal(3, elite.Options);
        Assert.Equal(3, boss.Options);

        Assert.Equal(0, league.RarityFloorPercent);
        Assert.True(league.CommonCeilingPercent > 0, "la liga degrada la rareza de sus opciones (ADR 0052)");
        Assert.Equal(0, elite.CommonCeilingPercent);
        Assert.Equal(0, boss.CommonCeilingPercent);
        Assert.True(elite.RarityFloorPercent > boss.RarityFloorPercent);

        // Suelo y techo salen de la MISMA tirada, desde extremos opuestos: si se solapasen, un mismo
        // número caería en los dos tramos y el que gana dejaría de ser evidente.
        Assert.True(league.RarityFloorPercent + league.CommonCeilingPercent <= 100);
        Assert.True(elite.RarityFloorPercent + elite.CommonCeilingPercent <= 100);
        Assert.True(boss.RarityFloorPercent + boss.CommonCeilingPercent <= 100);

        Assert.Equal(1, league.Picks);
        Assert.Equal(2, boss.Picks);
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

        // La plantilla de una run recién empezada está llena (10 de 10, RF-020) y una recompensa de
        // jugador necesita hueco, así que se aparta al último para poder probar la rama. Hasta la ADR
        // 0053 esta semilla no ofrecía opción de jugador -el id del nodo, del que sale el flujo de
        // recompensas, se movió con el mapa- y la rama no llegaba a probarse nunca.
        if (state.Roster.Count >= state.RosterCapacity)
        {
            state = RunStateBuilder.From(state).WithRoster(state.Roster.Take(state.Roster.Count - 1)).Build();
        }

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
