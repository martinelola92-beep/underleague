namespace Underleague.Sim.Analysis;

/// <summary>Resultado de la comprobación de dirección de §18.2.</summary>
public enum DirectionVerdict
{
    /// <summary>El <c>Value</c> del efecto y la métrica primaria no comparten eje: la comprobación no dice nada y no se aplica.</summary>
    NotApplicable,

    /// <summary>El signo del delta observado coincide con el que predice el signo del <c>Value</c>.</summary>
    Consistent,

    /// <summary>Contradicción real de signo: procede escalar (§18.2).</summary>
    Contradiction,
}

/// <summary>
/// Precondición de la comprobación de dirección de §18.2 (corregida el 19 sep 2026, §29).
///
/// <para>La regla original comparaba <c>sign(effect.Value)</c> con <c>sign(delta)</c> asumiendo
/// <b>implícitamente</b> que los dos viven en el mismo eje semántico. Eso es cierto cuando el
/// <c>Value</c> es una magnitud sobre la misma cantidad que cuenta la métrica, y falso cuando es un
/// desplazamiento espacial: <c>high_line</c> es <c>shiftHome(+2)</c> —dos casillas hacia adelante— y su
/// métrica es <c>ballThirdMaxShare</c>, una medida de CONCENTRACIÓN. Medido (§28.3), el balón sí avanza
/// (tercio atacante +2,89) y el máximo baja 2,32 justo por eso: no hay contradicción que escalar.</para>
///
/// <para>La precondición es una <b>lista blanca sobre <see cref="PerkBalanceCategory"/></b>, la
/// clasificación que ya existía en el repositorio — no una tabla de perks concretos. Falla de forma
/// segura: cualquier categoría no listada (incluida una futura) no recibe la comprobación, que es el
/// sentido conservador correcto — no escalar por una comparación que no significa nada.</para>
/// </summary>
public static class DirectionCheck
{
    /// <summary>
    /// True solo si el <c>Value</c> del efecto de esa categoría es monótono sobre la métrica primaria que
    /// el clasificador le asigna:
    /// <list type="bullet">
    /// <item><see cref="PerkBalanceCategory.ProbabilityBonus"/>: <c>Value</c> es un delta sobre la
    ///   probabilidad del mismo suceso que la métrica cuenta (<c>tackle</c> → <c>tacklesPerMatch</c>).</item>
    /// <item><see cref="PerkBalanceCategory.TraitScalar"/>: <c>Value</c> suma a un escalar cuya métrica
    ///   asociada (<c>PerkBalanceClassifier.ScalarMetricName</c>) crece con él en todos los escalares con
    ///   perk real hoy (<c>injuryChanceBonus</c>, <c>hardTackleBonus</c>, <c>shootRangeBonusCells</c>).</item>
    /// </list>
    /// <see cref="PerkBalanceCategory.Geometry"/> queda fuera: su <c>Value</c> son casillas de
    /// desplazamiento y su métrica es una cuota de concentración. El resto de categorías no llega hoy a
    /// esta comprobación (o no tiene parámetro numérico, o su métrica no tiene traducción por partido),
    /// así que quedan fuera por el mismo criterio conservador.
    /// </summary>
    public static bool AppliesTo(PerkBalanceCategory category) =>
        category is PerkBalanceCategory.ProbabilityBonus or PerkBalanceCategory.TraitScalar;

    /// <summary>
    /// Veredicto completo. <paramref name="effectValue"/> es el <c>Value</c> del efecto principal y
    /// <paramref name="observedDelta"/> el delta medido de la métrica primaria (armado − control).
    /// </summary>
    public static DirectionVerdict Evaluate(PerkBalanceCategory category, int effectValue, double observedDelta)
    {
        if (!AppliesTo(category))
        {
            return DirectionVerdict.NotApplicable;
        }

        bool expectedIncrease = effectValue >= 0;
        bool observedIncrease = observedDelta >= 0;
        return expectedIncrease == observedIncrease ? DirectionVerdict.Consistent : DirectionVerdict.Contradiction;
    }
}
