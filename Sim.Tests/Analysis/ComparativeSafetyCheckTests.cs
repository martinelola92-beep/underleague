using Underleague.Sim.Analysis;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Tests focalizados del chequeo de seguridad comparativo (§19, punto E del encargo del 19 sep 2026),
/// pedidos explícitamente antes de tocar el harness real: sintéticos, sin simular ningún partido — solo
/// listas de valores por partido, para aislar la lógica de atribución de la simulación.
/// </summary>
public sealed class ComparativeSafetyCheckTests
{
    [Fact]
    public void InBandArmedValueNeedsNoComparison()
    {
        var result = ComparativeSafetyCheck.Evaluate(
            armedMeanValue: 0.5, bandMin: 0.3, bandMax: 0.9,
            armedPerMatchValues: new[] { 0.5, 0.5 }, controlPerMatchValues: new[] { 0.5, 0.5 });

        Assert.Equal(SafetyAttribution.InBand, result);
    }

    [Fact]
    public void SameBaselineInBothArmsIsNotAttributableEvenIfBothAreOutOfBand()
    {
        // Reproduce blood_scent/bloodhound (§19.3, con el portero): armado y control IDÉNTICOS, ambos
        // por debajo del suelo — el 0.25 no es "el efecto del perk", es el nivel base de la muestra.
        var armed = new[] { 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0 };
        var control = new[] { 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0 };

        var result = ComparativeSafetyCheck.Evaluate(
            armedMeanValue: 0.2, bandMin: 0.3, bandMax: 0.9,
            armedPerMatchValues: armed, controlPerMatchValues: control);

        Assert.Equal(SafetyAttribution.NotAttributable, result);
    }

    [Fact]
    public void SmallSampleNoiseWithoutPowerIsNotAttributable()
    {
        // Armado nominalmente fuera de banda, control dentro, pero la diferencia es pequeña frente a la
        // varianza de la muestra (no hay potencia, §5.5) — no se confunde ruido con efecto del perk.
        var armed = new[] { 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0 }; // media 0.2
        var control = new[] { 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0 }; // media 0.3, en banda

        var result = ComparativeSafetyCheck.Evaluate(
            armedMeanValue: 0.2, bandMin: 0.3, bandMax: 0.9,
            armedPerMatchValues: armed, controlPerMatchValues: control);

        Assert.Equal(SafetyAttribution.NotAttributable, result);
    }

    [Fact]
    public void RealIncreaseAboveTheBandWithControlInBandAndCleanSeparationIsAttributable()
    {
        // El perk SÍ dispara injuriesPerMatch muy por encima del techo, con control claramente sano y
        // separación grande y consistente entre las dos series — debe detectarse como violación real.
        var control = new[] { 0.4, 0.5, 0.6, 0.4, 0.5, 0.5, 0.6, 0.4, 0.5, 0.6, 0.4, 0.5, 0.6, 0.5, 0.4, 0.5, 0.6, 0.4, 0.5, 0.5 }; // media ~0.49, en banda
        var armed = new[] { 3.0, 3.2, 3.1, 3.0, 3.3, 3.1, 3.0, 3.2, 3.1, 3.0, 3.2, 3.1, 3.0, 3.3, 3.1, 3.0, 3.2, 3.1, 3.0, 3.2 }; // media ~3.11, muy fuera

        double armedMean = armed.Average();
        var result = ComparativeSafetyCheck.Evaluate(
            armedMeanValue: armedMean, bandMin: 0.3, bandMax: 0.9,
            armedPerMatchValues: armed, controlPerMatchValues: control);

        Assert.Equal(SafetyAttribution.AttributableViolation, result);
    }

    [Fact]
    public void ControlAlreadyOutOfBandMeansTheBandItselfIsNotDiscriminatingHere()
    {
        // El control (sin el perk) YA rompe la banda: sea lo que sea que hace el perk, esta banda no
        // sirve para juzgarlo a esta escala — no se marca como violación del perk.
        var armed = new[] { 1.5, 1.6, 1.4, 1.5, 1.6 };
        var control = new[] { 1.4, 1.5, 1.6, 1.5, 1.4 };

        var result = ComparativeSafetyCheck.Evaluate(
            armedMeanValue: armed.Average(), bandMin: 0.3, bandMax: 0.9,
            armedPerMatchValues: armed, controlPerMatchValues: control);

        Assert.Equal(SafetyAttribution.NotAttributable, result);
    }

    [Fact]
    public void TooFewMatchesToCompareIsNotAttributable()
    {
        var result = ComparativeSafetyCheck.Evaluate(
            armedMeanValue: 5.0, bandMin: 0.3, bandMax: 0.9,
            armedPerMatchValues: new[] { 5.0 }, controlPerMatchValues: new[] { 0.5 });

        Assert.Equal(SafetyAttribution.NotAttributable, result);
    }
}
