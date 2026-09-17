using Underleague.Sim.Analysis;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>Prueba determinista (sin simular ningún partido) de la comprobación de potencia de §5.5.</summary>
public sealed class BalancePowerCheckTests
{
    [Fact]
    public void SmallDeltaAgainstHighVarianceIsNotDistinguishableFromNoise()
    {
        // Mismo patrón que el primer lote pequeño de C1 §5: delta de 0,03 sobre una desviación grande.
        bool sufficient = BalancePowerCheck.HasSufficientPower(
            deltaObserved: 0.03, varianceArmed: 4.0, countArmed: 40, varianceControl: 4.0, countControl: 40);

        Assert.False(sufficient);
    }

    [Fact]
    public void LargeDeltaWithLowVarianceIsDistinguishable()
    {
        bool sufficient = BalancePowerCheck.HasSufficientPower(
            deltaObserved: 1.5, varianceArmed: 0.5, countArmed: 40, varianceControl: 0.5, countControl: 40);

        Assert.True(sufficient);
    }

    [Fact]
    public void DoublingSampleSizeCanTurnAnIndistinguishableDeltaIntoADistinguishableOne()
    {
        // Refleja el hallazgo real de C1 §5: 40/brazo no bastó, 200/brazo sí, para el mismo tamaño de
        // efecto (aquí con números redondos que cruzan el umbral entre las dos N, no los de Cazagoles).
        const double delta = 0.3;
        const double variance = 1.0;

        bool atSmallSample = BalancePowerCheck.HasSufficientPower(delta, variance, 40, variance, 40);
        bool atLargeSample = BalancePowerCheck.HasSufficientPower(delta, variance, 200, variance, 200);

        Assert.False(atSmallSample);
        Assert.True(atLargeSample);
    }

    [Fact]
    public void ZeroVarianceWithNonZeroDeltaIsAlwaysSufficient()
    {
        bool sufficient = BalancePowerCheck.HasSufficientPower(
            deltaObserved: 0.5, varianceArmed: 0.0, countArmed: 10, varianceControl: 0.0, countControl: 10);

        Assert.True(sufficient);
    }

    [Fact]
    public void ZeroDeltaIsNeverSufficientRegardlessOfVariance()
    {
        bool sufficient = BalancePowerCheck.HasSufficientPower(
            deltaObserved: 0.0, varianceArmed: 0.0, countArmed: 10, varianceControl: 0.0, countControl: 10);

        Assert.False(sufficient);
    }

    [Fact]
    public void SampleVarianceMatchesKnownValue()
    {
        // {2,4,4,4,5,5,7,9}: media 5, varianza muestral (n-1) = 32/7 ≈ 4.571428...
        double variance = BalancePowerCheck.SampleVariance(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });

        Assert.Equal(32.0 / 7.0, variance, precision: 9);
    }

    [Fact]
    public void SingleObservationHasZeroVarianceNotAnException()
    {
        double variance = BalancePowerCheck.SampleVariance(new double[] { 42 });
        Assert.Equal(0.0, variance);
    }
}
