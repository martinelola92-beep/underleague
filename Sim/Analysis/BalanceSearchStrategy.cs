namespace Underleague.Sim.Analysis;

/// <summary>Estrategia de búsqueda de valor por tipo de parámetro (§7). "None" es una respuesta válida, no un hueco.</summary>
public enum SearchStrategyKind
{
    /// <summary>Tripleta anclada en cuartiles de una medición barata (§6.2/§7): bonus de utilidad, probabilidades.</summary>
    TripletAnchored,

    /// <summary>Bisección en aritmética entera (RT-023): geometría, casillas.</summary>
    IntegerBisection,

    /// <summary>Bisección continua simple: escalares de rasgo, atributos, sesgo arbitral.</summary>
    LinearBisection,

    /// <summary>Sin parámetro numérico que buscar (§6.5): selección de objetivo, sucesos binarios.</summary>
    None,
}

/// <summary>
/// Selecciona la estrategia de búsqueda por categoría (§7), no por perk — punto 10 del contrato de
/// `READY_FOR_SCREENING` (§16): un perk auto-tuneable (<c>HasNumericParameter</c>) siempre debe resolver
/// a una estrategia distinta de <see cref="SearchStrategyKind.None"/>, y uno sin parámetro numérico
/// siempre debe resolver exactamente a <see cref="SearchStrategyKind.None"/> — la consistencia entre las
/// dos es lo que el contrato comprueba, no un mapeo per-perk.
/// </summary>
public static class BalanceSearchStrategy
{
    public static SearchStrategyKind SelectStrategy(PerkBalanceCategory category, bool hasNumericParameter)
    {
        if (!hasNumericParameter)
        {
            return SearchStrategyKind.None;
        }

        return category switch
        {
            PerkBalanceCategory.UtilityBonus or PerkBalanceCategory.ProbabilityBonus => SearchStrategyKind.TripletAnchored,
            PerkBalanceCategory.Geometry => SearchStrategyKind.IntegerBisection,
            PerkBalanceCategory.TraitScalar or PerkBalanceCategory.Attribute or PerkBalanceCategory.RefereeBias => SearchStrategyKind.LinearBisection,
            PerkBalanceCategory.BinaryEvent => SearchStrategyKind.None, // con o sin valor propio (§13.4), nunca se busca un número
            _ => SearchStrategyKind.None,
        };
    }
}
