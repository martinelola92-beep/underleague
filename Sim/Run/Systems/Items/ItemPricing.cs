using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Precio de un objeto <b>derivado de su valor</b>, no de su rareza (ADR 0038).
///
/// <code>
/// valor(objeto)  = Σ (bonus_atributo × valorMarginal_atributo)      (ItemScale.ValueOf)
/// precio(objeto) = precioBase(rareza) × valor(objeto) / valorMedio(rareza)
/// </code>
///
/// <para>Consecuencia deliberada: dos objetos comunes que suben lo mismo en atributos distintos
/// <b>no cuestan lo mismo</b>. Un +10 de fuerza cuesta casi el cuádruple que un +10 de resistencia,
/// porque eso es lo que valen. Sin esa corrección el de resistencia sería relleno que ocupa sitio en el
/// surtido sin ser nunca una opción.</para>
///
/// <para>El <b>frágil</b> no se equilibra con números sino con precio (ADR 0036): cuesta
/// <c>fragilePricePercent</c> de lo que costaría el mismo objeto sin romperse, y aparece más a menudo
/// en las recompensas (<c>fragileOfferWeightPercent</c>). Así la decisión es real —"llevo el bueno o dos
/// frágiles por el mismo oro"— en vez de una trampa.</para>
///
/// <para>El <b>maldito</b> no necesita regla aparte: su contrapartida entra en el valor con signo
/// negativo, así que un maldito que baja algo caro vale y cuesta menos. Es la misma aritmética.</para>
/// </summary>
public static class ItemPricing
{
    /// <summary>Precio de compra de un objeto en el mercado, antes de la dispersión de la ADR 0037.</summary>
    public static int Price(ItemDefinition item, ItemScale scale, MarketConfig market)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(scale);
        ArgumentNullException.ThrowIfNull(market);

        int reference = scale.ReferenceValue(item.Rarity);
        if (reference <= 0)
        {
            return market.ItemPrice.Of(item.Rarity);
        }

        int rarityBase = market.ItemPrice.Of(item.Rarity);
        long price = (long)rarityBase * scale.ValueOf(item) / reference;
        if (item.Archetype == ItemArchetype.Fragile)
        {
            price = price * scale.FragilePricePercent / 100;
        }

        // ADR 0044: el valor modula el precio dentro de la banda de su rareza, no fuera de ella. Sin la
        // banda, un objeto común de fuerza costaba casi el cuádruple que otro común de resistencia y el
        // rango dentro de una categoría llegaba a 18:1.
        return market.ClampToBand(price > int.MaxValue ? int.MaxValue : (int)price, rarityBase);
    }

    /// <summary>Oro que devuelve vender un objeto (RF-076b): la fracción de mercado de su precio calculado.</summary>
    public static int SalePrice(ItemDefinition item, ItemScale scale, MarketConfig market)
    {
        ArgumentNullException.ThrowIfNull(market);
        return Price(item, scale, market) * market.ItemSellFractionPercent / 100;
    }

    /// <summary>
    /// Peso de un objeto en el sorteo del surtido y de las recompensas. Todos pesan igual salvo el
    /// frágil, que sale más a menudo porque su ventaja es la frecuencia y el precio (ADR 0036).
    /// </summary>
    public static int OfferWeight(ItemDefinition item, ItemScale scale)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(scale);
        return item.Archetype == ItemArchetype.Fragile ? scale.FragileOfferWeightPercent : 100;
    }
}
