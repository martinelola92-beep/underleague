namespace Underleague.Sim.Analysis;

/// <summary>
/// Circuito de seguridad a nivel de LOTE (§9.1 punto 3 de docs/analisis/protocolo-balanceo-automatizado.md):
/// si una fracción alta de los perks ya procesados en una tanda termina en <c>DESIGN_REVIEW</c>/
/// <c>SYSTEMIC_REGRESSION</c>/<c>INSUFFICIENT_EVIDENCE</c>, el lote se detiene en vez de seguir procesando
/// mecánicamente. Fracción y mínimo de perks procesados antes de comprobar son <c>[ASUNCIÓN — sin
/// calibrar]</c> (el propio documento: "provisionalmente 1 de cada 5"); no se recalibran aquí.
/// </summary>
public static class BatchEscalation
{
    /// <summary>1 de cada 5 (§9.1 punto 3), literal del documento — no un número elegido para este lote.</summary>
    public const double EscalationFraction = 1.0 / 5.0;

    /// <summary>
    /// El propio 1/5 solo tiene sentido con un denominador de al menos 5: comprobar la fracción tras
    /// procesar 1 perk (1/1 = 100%) dispararía el circuito con cualquier primer resultado adverso, que no
    /// es lo que "una fracción alta del lote" pretende decir. Interpretación explícita, no una
    /// recalibración del umbral.
    /// </summary>
    public const int MinimumProcessedBeforeCheck = 5;

    /// <summary>
    /// True si, con <paramref name="processed"/> perks ya terminados y <paramref name="escalatedCount"/>
    /// de ellos en un estado de escalada (DesignReview/SystemicRegression/InsufficientEvidence), el lote
    /// debe pararse antes de procesar el siguiente perk.
    /// </summary>
    public static bool ShouldStopBatch(int processed, int escalatedCount)
    {
        if (processed < MinimumProcessedBeforeCheck)
        {
            return false;
        }

        return (double)escalatedCount / processed >= EscalationFraction;
    }

    /// <summary>Los tres estados que cuentan como "escalada" para este circuito (§9.1 punto 3, literal).</summary>
    public static bool IsEscalatedState(BalanceState state) =>
        state is BalanceState.DesignReview or BalanceState.SystemicRegression or BalanceState.InsufficientEvidence;
}
