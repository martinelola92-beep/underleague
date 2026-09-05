using Underleague.Sim.Data;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.Systems.Market;

/// <summary>Fichaje de pago o canterano gratuito ofrecido en el mercado (RF-114, RF-114b).</summary>
public sealed record PlayerOffer(RunPlayer Player, int Price);

/// <summary>Mercenario ofrecido en el mercado (RF-110..113): sin coste de fichaje, salario por partido.</summary>
public sealed record MercenaryOffer(RunPlayer Player);

/// <summary>Perk ofrecido en el mercado (RF-114e).</summary>
public sealed record PerkOffer(string PerkId, int Price);

/// <summary>Objeto de equipamiento ofrecido en el mercado (RF-078).</summary>
public sealed record ItemOffer(string ItemId, int Price, Model.Rarity Rarity = Model.Rarity.Common);

/// <summary>Consumible ofrecido en el mercado.</summary>
public sealed record ConsumableOffer(string ConsumableId, int Price);

/// <summary>
/// Surtido completo de un nodo de mercado (RF-114): jugadores (fichajes + canteranos + mercenarios),
/// perks, equipamiento y consumibles. Se deriva, no se guarda (W-12): el mismo (semilla, nodo) produce
/// siempre el mismo surtido, y el mercado nunca se renueva (RF-114: "no se renueva"), así que siempre usa
/// <c>rerollCount = 0</c>.
/// </summary>
public sealed record MarketOffers(
    IReadOnlyList<PlayerOffer> Recruits,
    IReadOnlyList<PlayerOffer> Youths,
    IReadOnlyList<MercenaryOffer> Mercenaries,
    IReadOnlyList<PerkOffer> Perks,
    IReadOnlyList<ItemOffer> Items,
    IReadOnlyList<ConsumableOffer> Consumables);

/// <summary>Deriva el surtido de un nodo de mercado abierto.</summary>
public static class MarketOfferGenerator
{
    public static MarketOffers Generate(
        RunState state,
        MapNode node,
        Catalog catalog,
        EconomyConfig economy,
        ItemCatalog items,
        Consumables.ConsumableCatalog consumables)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(consumables);

        var market = economy.Market;
        var rng = OfferStream.For(state.Seed, node.Id, rerollCount: 0);

        // El precio de un artículo concreto se dispersa alrededor del de su rareza (ADR 0037,
        // market.priceSpreadPercent). Sin dispersión, todos los comunes cuestan exactamente lo mismo y
        // "cuánto del surtido puedo pagar" salta de 0 a 1 con el oro, que es justo lo contrario de la
        // decisión que la ADR quiere: con dispersión hay artículos que se pueden pagar y otros por los
        // que hay que ahorrar dentro de la misma rareza.
        // ADR 0044: y se acota a la banda de su rareza, porque dispersión y valor juntos daban rangos de
        // 18 a 1 dentro de la misma categoría. rarityBase es el precio de referencia de la rareza; para un
        // objeto, price ya viene modulado por su valor medido (ADR 0038) y rarityBase no.
        int Priced(ref Pcg32 stream, int price, int rarityBase)
        {
            int spread = market.PriceSpreadPercent;
            if (price <= 0)
            {
                return 0;
            }

            if (spread > 0)
            {
                int percent = 100 - spread + stream.Range(0, (2 * spread) + 1);
                price = price * percent / 100;
            }

            return market.ClampToBand(price, rarityBase);
        }

        var recruits = new List<PlayerOffer>(market.PlayerOffers);
        for (int i = 0; i < market.PlayerOffers; i++)
        {
            var player = GeneratedPlayers.Recruit(ref rng, catalog, state.ClubRace, market.RecruitQuality, economy.RecruitLevel(node.Act));
            int recruitBase = market.PlayerPrice.Of(player.Rarity);
            recruits.Add(new PlayerOffer(player, Priced(ref rng, recruitBase, recruitBase)));
        }

        int youthCount = market.YouthMin == market.YouthMax
            ? market.YouthMin
            : market.YouthMin + rng.Range(0, market.YouthMax - market.YouthMin + 1);
        var youths = new List<PlayerOffer>(youthCount);
        for (int i = 0; i < youthCount; i++)
        {
            var youth = GeneratedPlayers.Youth(ref rng, catalog, state.ClubRace, market.YouthQuality);
            youths.Add(new PlayerOffer(youth, 0));
        }

        var mercenaries = new List<MercenaryOffer>(market.MercenaryOffers);
        var foreignRaces = ForeignRaces(catalog, state.ClubRace);
        for (int i = 0; i < market.MercenaryOffers && foreignRaces.Count > 0; i++)
        {
            var race = foreignRaces[rng.Range(0, foreignRaces.Count)];
            var mercenary = GeneratedPlayers.Mercenary(ref rng, catalog, race, market.MercenaryQuality, wage: 0, economy.RecruitLevel(node.Act));
            // El salario depende de la rareza sorteada, así que se calcula después de generarla.
            mercenary = mercenary with { Wage = economy.MercenaryWage(mercenary.Rarity) };
            mercenaries.Add(new MercenaryOffer(mercenary));
        }

        // ADR 0038, tabla de palancas: en el **mercado** la palanca es el PRECIO y en la recompensa es la
        // FRECUENCIA. Aquí se aplicaban las dos —el peso inversamente proporcional al valor medido— y el
        // resultado era un mostrador lleno de los perks que nadie quiere: 20 de los 45 perks medidos
        // valen negativo, el peso inverso los ofrece hasta cuatro veces más, y encima hay que pagarlos.
        // El surtido usa por eso el peso BASE de la tabla: el acto y la frecuencia propia del perk siguen
        // mandando (ADR 0051), el valor medido no. Es lo que devuelve al mercado el papel que la ADR 0055
        // le pide —el sitio donde está lo bueno y donde hay que pagarlo— sin tocar un solo número de
        // potencia.
        var perkPool = PerkPool.Offerable(state, catalog, node.Act, PerkSource.Market);
        var perkWeights = new List<int>(perkPool.Count);
        for (int i = 0; i < perkPool.Count; i++)
        {
            perkWeights.Add(PerkPool.OfferWeight(
                state, perkPool[i], catalog, economy.PerkValues.BaseWeight, node.Act));
        }

        var perks = new List<PerkOffer>(market.PerkOffers);
        for (int i = 0; i < market.PerkOffers && perkPool.Count > 0; i++)
        {
            var perk = perkPool[WeightedPick.Index(ref rng, perkWeights)];
            int perkBase = market.PerkPrice.Of(perk.Rarity);
            perks.Add(new PerkOffer(perk.Id, Priced(ref rng, perkBase, perkBase)));
        }

        // Solo los universales y los restringidos de la raza del club (ADR 0036); el precio sale del
        // valor del objeto, no de su rareza (ADR 0038).
        var itemPool = items.OfferableTo(state.ClubRace, node.Act);
        var itemWeights = new List<int>(itemPool.Count);
        for (int i = 0; i < itemPool.Count; i++)
        {
            itemWeights.Add(items.DepthWeight(
                itemPool[i], ItemPricing.OfferWeight(itemPool[i], items.Scale), node.Act));
        }

        var itemOffers = new List<ItemOffer>(market.ItemOffers);
        for (int i = 0; i < market.ItemOffers && itemPool.Count > 0; i++)
        {
            var item = itemPool[WeightedPick.Index(ref rng, itemWeights)];
            itemOffers.Add(new ItemOffer(
                item.Id,
                Priced(ref rng, ItemPricing.Price(item, items.Scale, market), market.ItemPrice.Of(item.Rarity)),
                item.Rarity));
        }

        var consumableOffers = new List<ConsumableOffer>(market.ConsumableOffers);
        for (int i = 0; i < market.ConsumableOffers && consumables.All.Count > 0; i++)
        {
            var consumable = consumables.All[rng.Range(0, consumables.All.Count)];
            consumableOffers.Add(new ConsumableOffer(consumable.Id, Priced(ref rng, market.ConsumablePrice, market.ConsumablePrice)));
        }

        return new MarketOffers(recruits, youths, mercenaries, perks, itemOffers, consumableOffers);
    }

    /// <summary>Razas jugables de lanzamiento distintas de la del club (RF-004c, RF-110): única vía para fichar otra raza.</summary>
    private static List<Model.Race> ForeignRaces(Catalog catalog, Model.Race clubRace)
    {
        var races = new List<Model.Race>();
        for (int i = 0; i < catalog.Races.Count; i++)
        {
            if (catalog.Races[i].Launch && catalog.Races[i].Id != clubRace)
            {
                races.Add(catalog.Races[i].Id);
            }
        }

        races.Sort();
        return races;
    }
}
