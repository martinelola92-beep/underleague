using Underleague.Sim.Analysis;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Prueba determinista (sin simular ningún partido) del motor de decisión de §6. Los escenarios
/// reproducen situaciones reales ya vividas en esta sesión (Tanda 0, C1) para comprobar que el código
/// las decide igual que se decidieron a mano.
/// </summary>
public sealed class BalanceDecisionRulesTests
{
    [Fact]
    public void LowExposureFirstAttemptAsksToRetryWithLargerSample()
    {
        // bulwark_stance en Tanda 0: 2/40 activación, muy por debajo de cualquier suelo razonable.
        var exposure = new ExposureCheck(Fraction: 0.05, Floor: 0.30, FloorConfidence.Assumption);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: false, anyMandatoryMetricOut: false, hasNumericParameter: true);

        Assert.Equal(BalanceState.Screening, state);
    }

    [Fact]
    public void LowExposureAfterRetryWithAssumedFloorIsInsufficientEvidenceNotInsufficientExposure()
    {
        // Corrección central de la revisión del 17 sep 2026: un suelo [ASUNCIÓN] nunca produce
        // INSUFFICIENT_EXPOSURE con la confianza de un umbral medido.
        var exposure = new ExposureCheck(Fraction: 0.05, Floor: 0.30, FloorConfidence.Assumption);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: true, anyMandatoryMetricOut: false, hasNumericParameter: true);

        Assert.Equal(BalanceState.InsufficientEvidence, state);
    }

    [Fact]
    public void LowExposureAfterRetryWithMeasuredFloorIsInsufficientExposure()
    {
        var exposure = new ExposureCheck(Fraction: 0.05, Floor: 0.30, FloorConfidence.Measured);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: true, anyMandatoryMetricOut: false, hasNumericParameter: true);

        Assert.Equal(BalanceState.InsufficientExposure, state);
    }

    [Fact]
    public void HighExposureWithZeroEffectAndAssumedFloorIsInsufficientEvidence()
    {
        var exposure = new ExposureCheck(Fraction: 0.95, Floor: 0.30, FloorConfidence.Measured);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: false, anyMandatoryMetricOut: false,
            hasNumericParameter: true, primaryEffectMagnitude: 0.0002, effectZeroFloor: 0.0013,
            zeroFloorConfidence: FloorConfidence.Assumption);

        Assert.Equal(BalanceState.InsufficientEvidence, state);
    }

    [Fact]
    public void HighExposureWithZeroEffectAndMeasuredFloorEscalatesToDesignReview()
    {
        // own_third_anchor en Tanda 0: activación completa (40/40), L1=0 informativo.
        var exposure = new ExposureCheck(Fraction: 1.0, Floor: 0.30, FloorConfidence.Measured);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: false, anyMandatoryMetricOut: false,
            hasNumericParameter: true, primaryEffectMagnitude: 0.0, effectZeroFloor: 0.0013,
            zeroFloorConfidence: FloorConfidence.Measured);

        Assert.Equal(BalanceState.DesignReview, state);
    }

    [Fact]
    public void MandatoryMetricOutIsSafetyLimitRegardlessOfEffect()
    {
        var exposure = new ExposureCheck(Fraction: 0.9, Floor: 0.30, FloorConfidence.Measured);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: false, anyMandatoryMetricOut: true, hasNumericParameter: true);

        Assert.Equal(BalanceState.SafetyLimit, state);
    }

    [Fact]
    public void RealEffectWithNumericParameterContinuesToTuning()
    {
        var exposure = new ExposureCheck(Fraction: 0.9, Floor: 0.30, FloorConfidence.Measured);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: false, anyMandatoryMetricOut: false,
            hasNumericParameter: true, primaryEffectMagnitude: 0.05, effectZeroFloor: 0.0013,
            zeroFloorConfidence: FloorConfidence.Assumption);

        Assert.Equal(BalanceState.Tuning, state);
    }

    [Fact]
    public void PerkWithoutNumericParameterSkipsTuning()
    {
        // modifyMarkBias/modifyTackleBias, immunity, cancelEvent: sin parámetro que buscar (§6.5).
        var exposure = new ExposureCheck(Fraction: 0.9, Floor: 0.30, FloorConfidence.Measured);

        var state = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: false, anyMandatoryMetricOut: false, hasNumericParameter: false);

        Assert.Equal(BalanceState.Validating, state);
    }

    [Theory]
    [InlineData(new[] { 0.0336, 0.0385, 0.0698 }, true, true)]   // L1 de los tres candidatos de Cazagoles: monótono
    [InlineData(new[] { -0.029, -0.081, -0.090 }, false, true)]  // deltaPassChain: cae, monótono decreciente
    [InlineData(new[] { 0.05, 0.02, 0.08 }, true, false)]        // no monótono (el primer lote pequeño de C1)
    public void MonotonicityMatchesWhatWasActuallyMeasuredInC1(double[] values, bool increasing, bool expected)
    {
        Assert.Equal(expected, BalanceDecisionRules.IsMonotonic(values, increasing));
    }

    [Fact]
    public void CandidateWithMandatoryMetricOutOfBandFailsSafety()
    {
        var metrics = new[]
        {
            new PairedMetric("shotsPerMatch", ArmedValue: 8.1, ControlValue: 7.8, BandMin: 7, BandMax: 15),
            new PairedMetric("ballThirdMaxShare", ArmedValue: 60.0, ControlValue: 46.1, BandMin: 0, BandMax: 52),
        };

        Assert.True(BalanceDecisionRules.CandidateFailsSafety(metrics));
    }

    [Fact]
    public void CandidateWithAllMandatoryMetricsInBandPassesSafety()
    {
        var metrics = new[]
        {
            new PairedMetric("shotsPerMatch", ArmedValue: 8.1, ControlValue: 7.8, BandMin: 7, BandMax: 15),
            new PairedMetric("ballThirdMaxShare", ArmedValue: 47.2, ControlValue: 46.1, BandMin: 0, BandMax: 52),
        };

        Assert.False(BalanceDecisionRules.CandidateFailsSafety(metrics));
    }

    [Fact]
    public void AllSevenConditionsSatisfiedIsBalanced()
    {
        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: true, SafetyMetricsInBand: true, EvidenceSufficient: true,
            ReplicatesAcrossSeeds: true, DeterministicRerun: true, NoDesignDegeneracy: true,
            NoUnexplainedSystemicMove: true);

        Assert.Equal(BalanceState.Balanced, BalanceDecisionRules.EvaluateValidation(checklist));
    }

    [Fact]
    public void PassingSafetyAloneIsNotEnoughForBalanced()
    {
        // El caso que la revisión del 17 sep 2026 exigió cerrar: "no romper nada" no es BALANCED.
        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: false, SafetyMetricsInBand: true, EvidenceSufficient: true,
            ReplicatesAcrossSeeds: true, DeterministicRerun: true, NoDesignDegeneracy: true,
            NoUnexplainedSystemicMove: true);

        Assert.NotEqual(BalanceState.Balanced, BalanceDecisionRules.EvaluateValidation(checklist));
    }

    [Fact]
    public void OnlyInsufficientEvidenceFailingGivesNeedsReplicationNotReject()
    {
        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: true, SafetyMetricsInBand: true, EvidenceSufficient: false,
            ReplicatesAcrossSeeds: true, DeterministicRerun: true, NoDesignDegeneracy: true,
            NoUnexplainedSystemicMove: true);

        Assert.Equal(BalanceState.NeedsReplication, BalanceDecisionRules.EvaluateValidation(checklist));
    }

    [Fact]
    public void UnexplainedSystemicMoveOverridesEverythingElse()
    {
        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: true, SafetyMetricsInBand: true, EvidenceSufficient: true,
            ReplicatesAcrossSeeds: true, DeterministicRerun: true, NoDesignDegeneracy: true,
            NoUnexplainedSystemicMove: false);

        Assert.Equal(BalanceState.SystemicRegression, BalanceDecisionRules.EvaluateValidation(checklist));
    }

    [Fact]
    public void DesignDegeneracyIsRejectNotBalanced()
    {
        // 48% de Cazagoles: ninguna métrica numérica sale de rango, pero ShortPass queda exprimido.
        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: true, SafetyMetricsInBand: true, EvidenceSufficient: true,
            ReplicatesAcrossSeeds: true, DeterministicRerun: true, NoDesignDegeneracy: false,
            NoUnexplainedSystemicMove: true);

        Assert.Equal(BalanceState.Reject, BalanceDecisionRules.EvaluateValidation(checklist));
    }

    [Fact]
    public void AnchoredTripletMatchesCazagolesQuartiles()
    {
        // Mismos 32 valores combinados de §3.1b de C1 (dos semillas), redondeados.
        double[] gaps =
        {
            1.0, 4.4, 8.1, 9.3, 10.0, 12.7, 13.8, 16.6, 16.9, 17.9, 18.4, 20.1, 20.8, 22.9, 23.4, 23.9,
            24.7, 26.5, 26.8, 33.2, 38.2, 40.5, 47.3, 47.5, 48.6, 50.1, 59.2, 59.7, 62.1, 76.9, 121.0, 135.6,
        };

        var (low, central, high) = BalanceValueSearch.AnchoredTriplet(gaps);

        Assert.Equal(16.8, low, precision: 1);
        Assert.Equal(24.3, central, precision: 1);
        Assert.Equal(47.8, high, precision: 1);
    }
}
