using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Market;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// El precio de reventa de un perk con tarifa en <c>data/economy/perk-resale.json</c> (paquete BB,
/// consumidor Préstamo: "se revende sin perder la mitad",
/// <c>docs/analisis/perks-catalogo-unificado.md</c> §3.2). No es un sistema nuevo: es el modificador por
/// perk concreto que le faltaba a <see cref="MarketSystem.SalePrice"/>, que ya sumaba
/// <c>PlayerSalePerPerk</c> por cada perk llevado (RF-114f).
/// </summary>
public sealed class PerkResaleTests
{
    private static readonly ulong Seed = 240924UL;

    private static RunPlayer Player(PhysicalState physical, IReadOnlyList<string> perks) =>
        RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .Roster[0] with
        {
            PhysicalState = physical,
            Experience = 100,
            Level = 4,
            Perks = perks,
        };

    private static EconomyConfig EconomyWithResale(string perkId, int percent)
    {
        var table = PerkResaleTable.FromJson(new Dictionary<string, string>
        {
            [PerkResaleTable.Path] = $$"""{ "overrides": { "{{perkId}}": {{percent}} } }""",
        });

        return SystemsTestSupport.Systems.Economy with { PerkResale = table };
    }

    /// <summary>
    /// El precio de reventa del alquilado es mayor que el del mismo jugador sin el perk, con el MISMO
    /// número de perks (para aislar el override del simple bonus "por perk llevado" de RF-114f): un
    /// jugador con lesión leve (50%) vende como si estuviera sano (100%) con "loan", y no con un perk
    /// cualquiera.
    /// </summary>
    [Fact]
    public void ALoanedInjuredPlayerSellsForMoreThanTheSamePlayerWithAnOrdinaryPerk()
    {
        var economy = EconomyWithResale("loan", 100);

        var withLoan = Player(PhysicalState.MinorInjury, new[] { "loan" });
        var withOtherPerk = Player(PhysicalState.MinorInjury, new[] { "some_other_perk" });

        int loanedPrice = MarketSystem.SalePrice(withLoan, economy);
        int ordinaryPrice = MarketSystem.SalePrice(withOtherPerk, economy);

        Assert.True(loanedPrice > ordinaryPrice, "el alquilado tiene que vender por más que el mismo jugador con otro perk cualquiera");

        // Y exactamente lo que dice el override: como si estuviera sano (100%), no al 50% de la lesión leve.
        var healthyNoPerks = Player(PhysicalState.Healthy, Array.Empty<string>());
        int healthyBase = MarketSystem.SalePrice(healthyNoPerks, economy)
            - (economy.Market.PlayerSalePerPerk * healthyNoPerks.Perks.Count);
        int expectedLoanBase = healthyBase + economy.Market.PlayerSalePerPerk;
        Assert.Equal(expectedLoanBase, loanedPrice);
    }

    /// <summary>El override nunca puede BAJAR el precio: un jugador sano no pierde nada por llevarlo.</summary>
    [Fact]
    public void TheOverrideNeverLowersThePriceOfAHealthyPlayer()
    {
        var economy = EconomyWithResale("loan", 30);

        var healthyWithLoan = Player(PhysicalState.Healthy, new[] { "loan" });
        var healthyWithOtherPerk = Player(PhysicalState.Healthy, new[] { "some_other_perk" });

        Assert.Equal(
            MarketSystem.SalePrice(healthyWithOtherPerk, economy),
            MarketSystem.SalePrice(healthyWithLoan, economy));
    }

    /// <summary>Sin tarifa para ese perk, no cambia nada respecto al mismo jugador sin él.</summary>
    [Fact]
    public void WithoutAnOverrideThePerkChangesNothing()
    {
        var economy = SystemsTestSupport.Systems.Economy with { PerkResale = PerkResaleTable.Empty };

        var withLoan = Player(PhysicalState.SevereInjury, new[] { "loan" });
        var withOtherPerk = Player(PhysicalState.SevereInjury, new[] { "some_other_perk" });

        Assert.Equal(
            MarketSystem.SalePrice(withOtherPerk, economy),
            MarketSystem.SalePrice(withLoan, economy));
    }
}
