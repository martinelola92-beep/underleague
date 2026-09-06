using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Tests.Run;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>RF-114..114f: surtido del mercado, pool que respeta la raza (ADR 0023) y canteranos.</summary>
public sealed class MarketTests
{
    [Fact]
    public void OffersAreReproducibleForTheSameSeedAndNode()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 424242UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        var first = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);
        var second = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);

        Assert.Equal(first.Recruits.Count, second.Recruits.Count);
        for (int i = 0; i < first.Recruits.Count; i++)
        {
            Assert.Equal(first.Recruits[i].Price, second.Recruits[i].Price);
            TestRuns.AssertSamePlayer(first.Recruits[i].Player, second.Recruits[i].Player);
        }

        Assert.Equal(first.Perks.Select(p => p.PerkId), second.Perks.Select(p => p.PerkId));
        Assert.Equal(first.Items.Select(i => i.ItemId), second.Items.Select(i => i.ItemId));
        Assert.Equal(first.Consumables.Select(c => c.ConsumableId), second.Consumables.Select(c => c.ConsumableId));
        Assert.Equal(first.Youths.Count, second.Youths.Count);
        Assert.Equal(first.Mercenaries.Select(m => m.Player.Rarity), second.Mercenaries.Select(m => m.Player.Rarity));
    }

    [Fact]
    public void OffersDifferForDifferentNodesOrSeeds()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 424242UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var nodeA = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
        var nodeB = new MapNode(502, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        var offersA = MarketOfferGenerator.Generate(state, nodeA, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);
        var offersB = MarketOfferGenerator.Generate(state, nodeB, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);

        bool anyDifference =
            !offersA.Perks.Select(p => p.PerkId).SequenceEqual(offersB.Perks.Select(p => p.PerkId))
            || !offersA.Items.Select(i => i.ItemId).SequenceEqual(offersB.Items.Select(i => i.ItemId))
            || offersA.Recruits[0].Player.Attributes != offersB.Recruits[0].Player.Attributes;

        Assert.True(anyDifference);
    }

    [Fact]
    public void PerkPoolRespectsRace()
    {
        var setup = SystemsTestSupport.Setup(Race.Dwarf);
        var state = RunEngine.Start(setup, 13579UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        var offers = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);

        foreach (var perkOffer in offers.Perks)
        {
            var perk = SystemsTestSupport.Catalog.Perks.Get(perkOffer.PerkId);
            Assert.True(perk.Race is null || perk.Race == Race.Dwarf, $"'{perk.Id}' es exclusivo de {perk.Race} y no debería salir en una run de Dwarf");
        }

        // El pool completo (no solo lo que salió sorteado esta vez) tampoco debe contener exclusivos de otra raza.
        var pool = Underleague.Sim.Run.Systems.PerkPool.Offerable(state, SystemsTestSupport.Catalog, node.Act);
        Assert.All(pool, perk => Assert.True(perk.Race is null || perk.Race == Race.Dwarf));
        Assert.Contains(pool, perk => perk.Race == Race.Dwarf);
    }

    [Fact]
    public void YouthOffersAreFreeCommonClubRaceWithExperienceBonus()
    {
        var setup = SystemsTestSupport.Setup(Race.Elf);
        var state = RunEngine.Start(setup, 24680UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        var offers = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);
        var youthConfig = SystemsTestSupport.Systems.Economy.Market;

        Assert.InRange(offers.Youths.Count, youthConfig.YouthMin, youthConfig.YouthMax);
        foreach (var youth in offers.Youths)
        {
            Assert.Equal(0, youth.Price);
            Assert.True(youth.Player.IsYouth);
            Assert.Equal(Rarity.Common, youth.Player.Rarity);
            Assert.Equal(Race.Elf, youth.Player.Race);
        }
    }

    [Fact]
    public void MercenariesAreForeignRaceWithWageAndStrangerTag()
    {
        var setup = SystemsTestSupport.Setup(Race.Human);
        var state = RunEngine.Start(setup, 11223344UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        var offers = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);

        Assert.NotEmpty(offers.Mercenaries);
        foreach (var offer in offers.Mercenaries)
        {
            Assert.NotEqual(Race.Human, offer.Player.Race);
            Assert.True(offer.Player.IsMercenary);
            Assert.True(offer.Player.Wage > 0);
            Assert.Contains("Stranger", offer.Player.Tags);
        }
    }

    /// <summary>
    /// RF-114, ADR 0080: todo nodo de mercado ofrece EXACTAMENTE un portero, mínimo y máximo uno, y ese
    /// portero sale siempre del primer fichaje de pago (nunca de un canterano ni de un mercenario).
    /// </summary>
    [Fact]
    public void EveryMarketHasExactlyOneGoalkeeperAsTheFirstRecruit()
    {
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        for (ulong seed = 1UL; seed <= 50UL; seed++)
        {
            var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
            var offers = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, SystemsTestSupport.Systems.Economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);

            int goalkeepers =
                offers.Recruits.Count(r => r.Player.Position == Position.Goalkeeper)
                + offers.Youths.Count(y => y.Player.Position == Position.Goalkeeper)
                + offers.Mercenaries.Count(m => m.Player.Position == Position.Goalkeeper);

            Assert.True(goalkeepers == 1, $"semilla {seed}: se esperaba exactamente 1 portero en el mercado, hubo {goalkeepers}");
            Assert.NotEmpty(offers.Recruits);
            Assert.Equal(Position.Goalkeeper, offers.Recruits[0].Player.Position);
        }
    }

    /// <summary>
    /// RF-114, ADR 0080: sin fichajes de portero reservados (<c>goalkeeperOffers = 0</c>) no hay garantía
    /// ninguna: el mercado puede quedarse sin ningún portero, porque canteranos y mercenarios son siempre
    /// de campo y ya no queda ningún fichaje reservado a portero.
    /// </summary>
    [Fact]
    public void WithZeroGoalkeeperOffersThereIsNoGuarantee()
    {
        var economy = SystemsTestSupport.Systems.Economy with
        {
            Market = SystemsTestSupport.Systems.Economy.Market with { GoalkeeperOffers = 0 },
        };
        var node = new MapNode(201, 2, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);

        for (ulong seed = 1UL; seed <= 50UL; seed++)
        {
            var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
            var offers = MarketOfferGenerator.Generate(state, node, SystemsTestSupport.Catalog, economy, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables);

            int goalkeepers =
                offers.Recruits.Count(r => r.Player.Position == Position.Goalkeeper)
                + offers.Youths.Count(y => y.Player.Position == Position.Goalkeeper)
                + offers.Mercenaries.Count(m => m.Player.Position == Position.Goalkeeper);

            Assert.Equal(0, goalkeepers);
        }
    }
}
