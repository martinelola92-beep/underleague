using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Market;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// Lo que pagan por un jugador (RF-114f, ADR 0108): un fichaje que no ha jugado no se vende, y lo que
/// valga se cobra según el estado en que lo vendes.
/// </summary>
public sealed class PlayerSaleTests
{
    private static readonly Underleague.Sim.Run.Systems.Economy.EconomyConfig Economy =
        SystemsTestSupport.Systems.Economy;

    private static RunState Start(ulong seed) =>
        RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

    /// <summary>Un titular real de la plantilla inicial, puesto en el estado físico que pide el test.</summary>
    private static RunPlayer Player(PhysicalState physical) =>
        Start(7701UL).Roster[0] with { PhysicalState = physical, Experience = 100, Level = 4 };

    /// <summary>
    /// El grifo que esto cierra: el canterano es GRATIS (RF-114b) y se podía vender en el mismo nodo de
    /// mercado en que se fichaba. Con 1-2 por mercado, tres mercados por acto y tres actos, eran hasta 72
    /// de oro de la nada contra un ingreso de acto de 9/11/13.
    /// </summary>
    [Fact]
    public void APlayerThatHasNotPlayedYet_CannotBeSold()
    {
        var state = SystemsTestSupport.WithFakePendingNode(Start(7702UL), NodeKind.Market);
        var roster = new List<RunPlayer>(state.Roster);
        var fresh = roster[0] with { Experience = 0 };
        roster[0] = fresh;
        state = state.WithRoster(roster);

        var error = Assert.Throws<ArgumentException>(
            () => MarketSystem.Sell(state, new SellPlayer(fresh.Id), Economy));
        Assert.Contains("no ha jugado", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Y el estado se cobra: muerto 0 —ya no sirve para nada—, grave un cuarto, tocado la mitad. Vender la
    /// plantilla rota deja de ser una salida de emergencia gratuita, que es lo que hace del desgaste un
    /// recurso de verdad (RF-035, RF-104).
    /// </summary>
    [Fact]
    public void TheSalePriceFallsWithThePhysicalState()
    {
        int healthy = MarketSystem.SalePrice(Player(PhysicalState.Healthy), Economy);
        int minor = MarketSystem.SalePrice(Player(PhysicalState.MinorInjury), Economy);
        int severe = MarketSystem.SalePrice(Player(PhysicalState.SevereInjury), Economy);
        int dead = MarketSystem.SalePrice(Player(PhysicalState.Dead), Economy);

        Assert.True(healthy > 0, "un jugador sano tiene que valer algo");
        Assert.Equal(healthy * 50 / 100, minor);
        Assert.Equal(healthy * 25 / 100, severe);
        Assert.Equal(0, dead);
    }
}
