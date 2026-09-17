namespace Underleague.Sim.Analysis;

/// <summary>Si un suelo/umbral usado en una regla viene de una medición real o es un valor provisional (§5.4).</summary>
public enum FloorConfidence
{
    Measured,
    Assumption,
}

/// <summary>Resultado de comprobar la exposición de un perk contra su suelo (§5.4/§6.1).</summary>
public readonly record struct ExposureCheck(double Fraction, double Floor, FloorConfidence Confidence)
{
    public bool Sufficient => Fraction >= Floor;
}

/// <summary>
/// Una métrica emparejada armado/control, con su banda si tiene una (§6.4). <see cref="Delta"/> es
/// armado menos control; <see cref="ArmedInBand"/> es null si la métrica no tiene banda (INFO).
/// </summary>
public readonly record struct PairedMetric(
    string Name, double ArmedValue, double ControlValue, double? BandMin, double? BandMax)
{
    public double Delta => ArmedValue - ControlValue;

    public bool? ArmedInBand => BandMin is null || BandMax is null
        ? null
        : ArmedValue >= BandMin.Value && ArmedValue <= BandMax.Value;
}

/// <summary>
/// Las siete condiciones de <c>BALANCED</c> (§6.4 de docs/analisis/protocolo-balanceo-automatizado.md),
/// cada una como un booleano ya evaluado por quien orquesta — esta clase solo combina, no mide.
/// </summary>
public readonly record struct ValidationChecklist(
    bool BehaviorAndEffectInBand,
    bool SafetyMetricsInBand,
    bool EvidenceSufficient,
    bool ReplicatesAcrossSeeds,
    bool DeterministicRerun,
    bool NoDesignDegeneracy,
    bool NoUnexplainedSystemicMove)
{
    public bool AllSatisfied =>
        BehaviorAndEffectInBand && SafetyMetricsInBand && EvidenceSufficient && ReplicatesAcrossSeeds
        && DeterministicRerun && NoDesignDegeneracy && NoUnexplainedSystemicMove;
}

/// <summary>
/// Motor de decisión determinista del protocolo de balanceo (§6). Cada método es una traducción literal
/// de un bloque de pseudocódigo del documento — cero heurística nueva, cero llamada a un modelo.
/// </summary>
public static class BalanceDecisionRules
{
    /// <summary>
    /// §6.1: decide qué sigue tras Screening. <paramref name="alreadyRetriedWithLargerSample"/> es true
    /// si ya se repitió una vez con muestra ×3 (§5.1) y la exposición sigue por debajo del suelo.
    /// </summary>
    public static BalanceState EvaluateScreening(
        ExposureCheck exposure,
        bool alreadyRetriedWithLargerSample,
        bool anyMandatoryMetricOut,
        bool hasNumericParameter,
        double? primaryEffectMagnitude = null,
        double effectZeroFloor = 0.0,
        FloorConfidence zeroFloorConfidence = FloorConfidence.Assumption)
    {
        if (!exposure.Sufficient)
        {
            if (!alreadyRetriedWithLargerSample)
            {
                return BalanceState.Screening; // señal para el llamador: repetir con ×3 plantillas
            }

            return exposure.Confidence == FloorConfidence.Measured
                ? BalanceState.InsufficientExposure
                : BalanceState.InsufficientEvidence;
        }

        if (anyMandatoryMetricOut)
        {
            return BalanceState.SafetyLimit;
        }

        if (primaryEffectMagnitude is { } magnitude && Math.Abs(magnitude) < effectZeroFloor)
        {
            return zeroFloorConfidence == FloorConfidence.Measured
                ? BalanceState.DesignReview
                : BalanceState.InsufficientEvidence;
        }

        return hasNumericParameter ? BalanceState.Tuning : BalanceState.Validating;
    }

    /// <summary>
    /// Monotonicidad de una serie medida en el mismo orden que el parámetro (§6.2/§7): true si nunca
    /// baja (o, con <paramref name="increasing"/> a false, si nunca sube).
    /// </summary>
    public static bool IsMonotonic(IReadOnlyList<double> valuesInParameterOrder, bool increasing = true)
    {
        ArgumentNullException.ThrowIfNull(valuesInParameterOrder);
        for (int i = 1; i < valuesInParameterOrder.Count; i++)
        {
            if (increasing && valuesInParameterOrder[i] < valuesInParameterOrder[i - 1])
            {
                return false;
            }

            if (!increasing && valuesInParameterOrder[i] > valuesInParameterOrder[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// §6.3: condiciones de descarte de seguridad sobre un candidato ya medido. No decide ACCEPT — solo
    /// si este candidato concreto debe descartarse.
    /// </summary>
    public static bool CandidateFailsSafety(IReadOnlyList<PairedMetric> mandatoryMetrics) =>
        mandatoryMetrics.Any(m => m.ArmedInBand == false);

    /// <summary>§6.4: combina las siete condiciones en el estado final de Validation.</summary>
    public static BalanceState EvaluateValidation(ValidationChecklist checklist)
    {
        if (checklist.AllSatisfied)
        {
            return BalanceState.Balanced;
        }

        if (!checklist.NoUnexplainedSystemicMove)
        {
            return BalanceState.SystemicRegression;
        }

        bool onlyEvidenceFails = !checklist.EvidenceSufficient
            && checklist.BehaviorAndEffectInBand
            && checklist.SafetyMetricsInBand
            && checklist.NoDesignDegeneracy;

        return onlyEvidenceFails ? BalanceState.NeedsReplication : BalanceState.Reject;
    }
}
