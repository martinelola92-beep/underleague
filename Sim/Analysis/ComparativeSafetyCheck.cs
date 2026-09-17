namespace Underleague.Sim.Analysis;

/// <summary>
/// Veredicto de si un "OUT" de una métrica obligatoria de RT-056 es atribuible al perk armado, o si es
/// un artefacto de la muestra/población base (§19, punto E del encargo del 19 sep 2026: "SAFETY_LIMIT
/// relativo al control emparejado" — sin tocar RT-056, la banda, ni ningún umbral).
/// </summary>
public enum SafetyAttribution
{
    /// <summary>El valor armado está dentro de la banda: no hay nada que atribuir.</summary>
    InBand,

    /// <summary>
    /// El valor armado está fuera de banda, pero no se puede atribuir al perk: o el propio control
    /// (mismo tamaño de muestra, sin el perk) YA está fuera de banda —la banda no distingue "con perk" de
    /// "sin perk" a esta escala—, o la diferencia armado/control no es distinguible del ruido de muestreo
    /// (mismo power-check de §5.5, reutilizado, no reinventado).
    /// </summary>
    NotAttributable,

    /// <summary>
    /// El valor armado está fuera de banda, el control está dentro, y la diferencia armado/control SÍ es
    /// distinguible del ruido: hay evidencia real de que el perk empuja la métrica fuera de rango.
    /// </summary>
    AttributableViolation,
}

/// <summary>
/// Compara el "OUT" de una métrica obligatoria contra el brazo de control emparejado (§19.3/§19.4,
/// hallazgo de <c>blood_scent</c>/<c>bloodhound</c>: con el portero, armado y control daban EXACTAMENTE
/// el mismo valor, y aun así un <c>SAFETY_LIMIT</c> absoluto sobre el armado solo lo marcaba como
/// violación). No cambia la banda RT-056 ni ningún umbral — solo decide si el dato ya disponible
/// (control) permite atribuir el "OUT" al perk o no. Pura, sin E/S, reutiliza
/// <see cref="BalancePowerCheck"/> tal cual, sin duplicar su lógica.
/// </summary>
public static class ComparativeSafetyCheck
{
    public static SafetyAttribution Evaluate(
        double armedMeanValue, double? bandMin, double? bandMax,
        IReadOnlyList<double> armedPerMatchValues, IReadOnlyList<double> controlPerMatchValues)
    {
        ArgumentNullException.ThrowIfNull(armedPerMatchValues);
        ArgumentNullException.ThrowIfNull(controlPerMatchValues);

        bool armedOut = IsOutOfBand(armedMeanValue, bandMin, bandMax);
        if (!armedOut)
        {
            return SafetyAttribution.InBand;
        }

        if (armedPerMatchValues.Count < 2 || controlPerMatchValues.Count < 2)
        {
            // Sin suficientes partidos para comparar: no se afirma una violación que no se puede
            // distinguir del ruido (mismo principio de §5.5, "nunca ACCEPT/REJECT con esa muestra").
            return SafetyAttribution.NotAttributable;
        }

        double controlMean = Average(controlPerMatchValues);
        if (IsOutOfBand(controlMean, bandMin, bandMax))
        {
            // El propio control (sin el perk) ya rompe la misma banda: la banda no está distinguiendo
            // "con perk" de "sin perk" a esta escala — no es un hallazgo sobre el perk.
            return SafetyAttribution.NotAttributable;
        }

        double armedMean = Average(armedPerMatchValues);
        double delta = armedMean - controlMean;
        double varianceArmed = BalancePowerCheck.SampleVariance(armedPerMatchValues);
        double varianceControl = BalancePowerCheck.SampleVariance(controlPerMatchValues);
        bool distinguishable = BalancePowerCheck.HasSufficientPower(
            delta, varianceArmed, armedPerMatchValues.Count, varianceControl, controlPerMatchValues.Count);

        return distinguishable ? SafetyAttribution.AttributableViolation : SafetyAttribution.NotAttributable;
    }

    private static bool IsOutOfBand(double value, double? min, double? max) =>
        min is not null && max is not null && (value < min.Value || value > max.Value);

    private static double Average(IReadOnlyList<double> values)
    {
        double sum = 0.0;
        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum / values.Count;
    }
}
