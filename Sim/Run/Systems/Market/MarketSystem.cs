using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Equipment;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.Systems.Market;

/// <summary>
/// Categorías del surtido del mercado, tal y como las nombra <see cref="BuyOffer.Category"/>. Cuatro
/// categorías visibles pide RF-114 (jugadores, perks, equipamiento, consumibles); "player" y "youth" se
/// muestran juntas como "jugadores", cada una con su propia lista de índices.
/// </summary>
public static class MarketCategories
{
    public const string Player = "player";
    public const string Youth = "youth";
    public const string Perk = "perk";
    public const string Item = "item";
    public const string Consumable = "consumable";
}

/// <summary>
/// Compra, venta de jugadores y fichaje de mercenarios en el mercado (RF-114..114f, RF-110..113). El
/// surtido se deriva con <see cref="MarketOfferGenerator"/> cada vez, así que no hace falta guardarlo
/// (W-12): dos llamadas con el mismo estado ven siempre el mismo surtido.
/// </summary>
public static class MarketSystem
{
    public static RunState Buy(
        RunState state,
        BuyOffer decision,
        Catalog catalog,
        EconomyConfig economy,
        ItemCatalog items,
        ConsumableCatalog consumables)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        var node = NodeGuards.RequireOpen(state, NodeKind.Market, "comprar en el mercado");
        var offers = MarketOfferGenerator.Generate(state, node, catalog, economy, items, consumables);

        return decision.Category switch
        {
            MarketCategories.Player => BuyPlayer(state, offers.Recruits, decision, requirePayment: true),
            MarketCategories.Youth => BuyPlayer(state, offers.Youths, decision, requirePayment: false),
            MarketCategories.Perk => BuyPerk(state, offers.Perks, decision, catalog, economy),
            MarketCategories.Item => BuyItem(state, offers.Items, decision, economy, items),
            MarketCategories.Consumable => BuyConsumable(state, offers.Consumables, decision),
            _ => throw new ArgumentException($"categoría de mercado desconocida: '{decision.Category}'", nameof(decision)),
        };
    }

    public static RunState Hire(RunState state, HireMercenary decision, Catalog catalog, EconomyConfig economy, ItemCatalog items, ConsumableCatalog consumables)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        var node = NodeGuards.RequireOpen(state, NodeKind.Market, "fichar a un mercenario");
        var offers = MarketOfferGenerator.Generate(state, node, catalog, economy, items, consumables);
        var offer = AtIndex(offers.Mercenaries, decision.OfferIndex, "mercenario");
        return state.WithNewPlayer(offer.Player);
    }

    public static RunState Sell(RunState state, SellPlayer decision, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);

        NodeGuards.RequireOpen(state, NodeKind.Market, "vender un jugador");
        var player = state.GetPlayer(decision.PlayerId);
        int price = economy.Market.PlayerSaleBase.Of(player.Rarity)
            + (economy.Market.PlayerSalePerLevel * (player.Level - 1))
            + (economy.Market.PlayerSalePerPerk * player.Perks.Count)
            + (economy.Market.PlayerSalePerBond * player.Bonds.Count);

        return state.WithoutPlayer(player.Id).AddGold(price);
    }

    private static RunState BuyPlayer(RunState state, IReadOnlyList<PlayerOffer> offers, BuyOffer decision, bool requirePayment)
    {
        var offer = AtIndex(offers, decision.OfferIndex, "jugador");
        if (requirePayment)
        {
            RequireGold(state, offer.Price);
            state = state.AddGold(-offer.Price);
        }

        return state.WithNewPlayer(offer.Player);
    }

    private static RunState BuyPerk(RunState state, IReadOnlyList<PerkOffer> offers, BuyOffer decision, Catalog catalog, EconomyConfig economy)
    {
        var offer = AtIndex(offers, decision.OfferIndex, "perk");
        if (decision.TargetPlayerId < 0)
        {
            throw new ArgumentException("comprar un perk exige elegir un jugador que lo lleve (RF-114e)", nameof(decision));
        }

        var perk = catalog.Perks.Find(offer.PerkId)
            ?? throw new InvalidOperationException($"el mercado ofrece el perk '{offer.PerkId}', que no está en el catálogo");
        var carriers = PerkPool.EligibleCarriers(state, perk, catalog);
        if (!carriers.Contains(decision.TargetPlayerId))
        {
            throw new ArgumentException(
                $"el jugador {decision.TargetPlayerId} no puede llevar el perk '{offer.PerkId}' (sin slot libre, ya lo lleva, o no cumple sus etiquetas)",
                nameof(decision));
        }

        RequireGold(state, offer.Price);
        var target = state.GetPlayer(decision.TargetPlayerId);
        return state.AddGold(-offer.Price).WithPlayer(PerkPool.WithPerk(target, offer.PerkId));
    }

    private static RunState BuyItem(RunState state, IReadOnlyList<ItemOffer> offers, BuyOffer decision, EconomyConfig economy, ItemCatalog items)
    {
        var offer = AtIndex(offers, decision.OfferIndex, "objeto");
        if (decision.TargetPlayerId < 0)
        {
            throw new ArgumentException("comprar un objeto exige elegir el jugador que lo equipa (RF-114e)", nameof(decision));
        }

        RequireGold(state, offer.Price);
        state = state.AddGold(-offer.Price);
        return EquipmentSystem.AssignPurchasedItem(state, decision.TargetPlayerId, offer.ItemId, economy, items);
    }

    private static RunState BuyConsumable(RunState state, IReadOnlyList<ConsumableOffer> offers, BuyOffer decision)
    {
        var offer = AtIndex(offers, decision.OfferIndex, "consumible");
        RequireGold(state, offer.Price);
        string counter = "consumable_owned:" + offer.ConsumableId;
        return state.AddGold(-offer.Price).WithCounter(counter, state.Counter(counter) + 1);
    }

    private static T AtIndex<T>(IReadOnlyList<T> offers, int index, string what)
    {
        if (index < 0 || index >= offers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"el mercado no tiene ninguna oferta de {what} en el índice {index}");
        }

        return offers[index];
    }

    private static void RequireGold(RunState state, int price)
    {
        if (state.Gold < price)
        {
            throw new ArgumentException($"la compra cuesta {price} de oro y la run solo tiene {state.Gold}");
        }
    }
}
