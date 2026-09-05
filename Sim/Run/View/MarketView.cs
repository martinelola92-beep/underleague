using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Market;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Un artículo del surtido del mercado (RF-114), con todo lo que hay que saber para decidir si comprarlo
/// <b>antes</b> de comprarlo (RF-012d).
/// </summary>
/// <param name="Category">
/// Categoría con la que se compra: una de <see cref="MarketCategories"/>, o
/// <see cref="MarketView.MercenaryCategory"/> para los mercenarios, que se fichan con
/// <see cref="HireMercenary"/> y no con <see cref="BuyOffer"/>.
/// </param>
/// <param name="Index">Índice dentro de la lista de su categoría: es el que viaja en la decisión.</param>
/// <param name="Description">Descripción <b>generada</b> del efecto o del dato (RT-035).</param>
/// <param name="Free">Canterano gratuito (RF-114b): no cuesta oro, pero sí un hueco de plantilla.</param>
/// <param name="Wage">Salario por partido de un mercenario (RF-111); 0 en lo demás.</param>
/// <param name="Archetype">Arquetipo del objeto (RF-077, ADR 0036); null si el artículo no es un objeto.</param>
/// <param name="BreakChancePercent">Probabilidad de rotura de un objeto frágil al terminar el partido; 0 en los demás.</param>
/// <param name="RaceRestriction">Raza a la que un objeto restringido es exclusivo; vacío en los universales.</param>
/// <param name="Block">Por qué no se puede comprar ahora mismo, más allá del precio.</param>
public sealed record MarketRow(
    string Category,
    int Index,
    string Id,
    string Name,
    string Headline,
    string Description,
    Rarity Rarity,
    int Price,
    bool Affordable,
    bool Free,
    bool Youth,
    bool Mercenary,
    int Wage,
    ItemArchetype? Archetype,
    int BreakChancePercent,
    string RaceRestriction,
    bool NeedsCarrier,
    IReadOnlyList<RewardCarrier> Carriers,
    RewardBlock Block);

/// <summary>Un jugador de la plantilla que se puede vender, con lo que pagan por él (RF-114f).</summary>
public sealed record MarketSaleRow(
    int PlayerId,
    string Name,
    Position Position,
    Rarity Rarity,
    int Level,
    PhysicalState PhysicalState,
    int Perks,
    int Bonds,
    int Price,
    bool LastAvailable);

/// <summary>
/// El nodo de mercado entero (RF-114..114f): las cuatro categorías, el oro disponible y la venta.
/// </summary>
/// <param name="LeavesBelowMinimum">
/// True si vender a alguien más dejaría la plantilla por debajo del mínimo (RF-002b): el mercado es uno
/// de los sitios donde una venta puede terminar la run, y eso hay que decirlo antes.
/// </param>
public sealed record MarketScreenView(
    int NodeId,
    int Act,
    int Gold,
    int RosterSize,
    int RosterCapacity,
    int AvailablePlayers,
    bool LeavesBelowMinimum,
    IReadOnlyList<MarketRow> Players,
    IReadOnlyList<MarketRow> Perks,
    IReadOnlyList<MarketRow> Items,
    IReadOnlyList<MarketRow> Consumables,
    IReadOnlyList<MarketSaleRow> Sellable);

/// <summary>
/// Compone la pantalla de mercado desde el estado. Puro: el surtido lo deriva
/// <see cref="MarketOfferGenerator"/> (el mismo que ejecuta la compra, así que lo que se ve es lo que se
/// compra) y aquí solo se le añaden nombre, descripción generada y precio comparado con el oro.
/// </summary>
public static class MarketView
{
    /// <summary>Categoría sintética de los mercenarios: no se compran, se fichan (RF-110..113).</summary>
    public const string MercenaryCategory = "mercenary";

    /// <summary>Vista del nodo de mercado abierto; null si el nodo pendiente no es un mercado.</summary>
    public static MarketScreenView? Build(
        RunState state,
        Catalog catalog,
        EconomyConfig economy,
        ItemCatalog items,
        ConsumableCatalog consumables,
        string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(consumables);

        if (state.PendingNodeId < 0)
        {
            return null;
        }

        var node = state.GetNode(state.PendingNodeId);
        if (node.Kind != NodeKind.Market)
        {
            return null;
        }

        var templates = catalog.Localization.Get(language);
        var offers = MarketOfferGenerator.Generate(state, node, catalog, economy, items, consumables);

        var players = new List<MarketRow>();

        // Los canteranos primero y marcados (RF-114b): son gratis, así que quien no tiene oro tiene que
        // verlos antes que nada. Su valor no está en lo que son hoy sino en el 33% de experiencia extra
        // (RF-114c), y eso solo se cobra fichándolos pronto.
        for (int i = 0; i < offers.Youths.Count; i++)
        {
            players.Add(PlayerRow(MarketCategories.Youth, i, offers.Youths[i].Player, 0, state, catalog, templates, youth: true, mercenary: false));
        }

        for (int i = 0; i < offers.Recruits.Count; i++)
        {
            players.Add(PlayerRow(MarketCategories.Player, i, offers.Recruits[i].Player, offers.Recruits[i].Price, state, catalog, templates, youth: false, mercenary: false));
        }

        for (int i = 0; i < offers.Mercenaries.Count; i++)
        {
            players.Add(PlayerRow(MercenaryCategory, i, offers.Mercenaries[i].Player, 0, state, catalog, templates, youth: false, mercenary: true));
        }

        var perkRows = new List<MarketRow>(offers.Perks.Count);
        for (int i = 0; i < offers.Perks.Count; i++)
        {
            var offer = offers.Perks[i];
            var perk = catalog.Perks.Get(offer.PerkId);
            var carriers = RewardView.Carriers(state, items, PerkPool.EligibleCarriers(state, perk, catalog));
            perkRows.Add(new MarketRow(
                MarketCategories.Perk,
                i,
                perk.Id,
                perk.Name.Es,
                string.Empty,
                DescriptionGenerator.Describe(perk, templates, catalog.Perks),
                perk.Rarity,
                offer.Price,
                state.Gold >= offer.Price,
                Free: false,
                Youth: false,
                Mercenary: false,
                Wage: 0,
                Archetype: null,
                BreakChancePercent: 0,
                RaceRestriction: string.Empty,
                NeedsCarrier: true,
                carriers,
                carriers.Count == 0 ? RewardBlock.NoCarrier : RewardBlock.None));
        }

        var itemRows = new List<MarketRow>(offers.Items.Count);
        for (int i = 0; i < offers.Items.Count; i++)
        {
            var offer = offers.Items[i];
            var item = items.Get(offer.ItemId);
            var carriers = RewardView.Carriers(state, items, LivingIds(state));
            itemRows.Add(new MarketRow(
                MarketCategories.Item,
                i,
                item.Id,
                item.Name.Es,
                string.Empty,
                ItemDescriptions.Describe(item, language),
                item.Rarity,
                offer.Price,
                state.Gold >= offer.Price,
                Free: false,
                Youth: false,
                Mercenary: false,
                Wage: 0,
                item.Archetype,
                item.BreakChancePercent,
                item.Race is { } race ? race.ToString() : string.Empty,
                NeedsCarrier: true,
                carriers,
                carriers.Count == 0 ? RewardBlock.NoCarrier : RewardBlock.None));
        }

        var consumableRows = new List<MarketRow>(offers.Consumables.Count);
        for (int i = 0; i < offers.Consumables.Count; i++)
        {
            var offer = offers.Consumables[i];
            var consumable = consumables.Find(offer.ConsumableId);
            if (consumable is null)
            {
                continue;
            }

            consumableRows.Add(new MarketRow(
                MarketCategories.Consumable,
                i,
                consumable.Id,
                consumable.Name.Es,
                string.Empty,
                DescriptionGenerator.DescribeEffects(consumable.Effects, templates),
                consumable.Rarity,
                offer.Price,
                state.Gold >= offer.Price,
                Free: false,
                Youth: false,
                Mercenary: false,
                Wage: 0,
                Archetype: null,
                BreakChancePercent: 0,
                RaceRestriction: string.Empty,
                NeedsCarrier: false,
                Array.Empty<RewardCarrier>(),
                RewardBlock.None));
        }

        return new MarketScreenView(
            node.Id,
            node.Act,
            state.Gold,
            state.RosterSize,
            state.RosterCapacity,
            state.AvailablePlayerCount,
            state.AvailablePlayerCount <= RunRules.MinimumAvailablePlayers,
            players,
            perkRows,
            itemRows,
            consumableRows,
            Sellable(state, economy));
    }

    private static MarketRow PlayerRow(
        string category,
        int index,
        RunPlayer player,
        int price,
        RunState state,
        Catalog catalog,
        DescriptionTemplates templates,
        bool youth,
        bool mercenary)
    {
        bool free = price <= 0;
        return new MarketRow(
            category,
            index,
            player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            player.Name,
            PlayerDescriptions.Headline(player, catalog, templates),
            PlayerDescriptions.AttributeLine(player, templates),
            player.Rarity,
            price,
            free || state.Gold >= price,
            free,
            youth,
            mercenary,
            player.Wage,
            Archetype: null,
            BreakChancePercent: 0,
            RaceRestriction: string.Empty,
            NeedsCarrier: false,
            Array.Empty<RewardCarrier>(),
            state.HasRosterSpace ? RewardBlock.None : RewardBlock.RosterFull);
    }

    private static IReadOnlyList<MarketSaleRow> Sellable(RunState state, EconomyConfig economy)
    {
        var rows = new List<MarketSaleRow>(state.Roster.Count);
        bool last = state.AvailablePlayerCount <= RunRules.MinimumAvailablePlayers;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            rows.Add(new MarketSaleRow(
                player.Id,
                player.Name,
                player.Position,
                player.Rarity,
                player.Level,
                player.PhysicalState,
                player.Perks.Count,
                player.Bonds.Count,
                MarketSystem.SalePrice(player, economy),
                last && player.IsAvailable));
        }

        return rows;
    }

    private static IReadOnlyList<int> LivingIds(RunState state)
    {
        var ids = new List<int>(state.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState != PhysicalState.Dead)
            {
                ids.Add(state.Roster[i].Id);
            }
        }

        return ids;
    }
}
