namespace Underleague.Sim.Analysis;

/// <summary>
/// Comprobación de potencia estadística (§5.5 de docs/analisis/protocolo-balanceo-automatizado.md):
/// separa "se jugaron N partidos" de "el efecto ya se distingue del ruido con esa N". Sin esto, el
/// muestreo adaptativo del protocolo solo se adaptaba a la exposición, nunca a la potencia real de la
/// métrica primaria — el hueco que la revisión del 17 sep 2026 identificó.
/// </summary>
public static class BalancePowerCheck
{
    /// <summary>
    /// Multiplicador de la comprobación de potencia (§5.5): convención estadística habitual
    /// (≈ intervalo de confianza del 95% para una normal), <b>no calibrada específicamente contra el
    /// ruido de este motor</b> — a diferencia de los tamaños de muestra de §5.1-5.3, que sí vienen de
    /// datos propios (C1/Tanda 0). Marcado [CONVENCIÓN ESTÁNDAR, NO CALIBRADA AL PROYECTO] en el
    /// documento; vive aquí como constante para que cambiarlo, si algún día se calibra, sea un solo sitio.
    /// </summary>
    public const double StandardErrorMultiplier = 2.0;

    /// <summary>
    /// True si <paramref name="deltaObserved"/> (armado − control) ya se distingue del ruido de muestreo
    /// con la potencia mínima exigida (§5.5): <c>|delta| &gt;= multiplicador × error_estándar</c>, donde
    /// el error estándar combina la varianza de cada brazo (aproximación de Welch, sin asumir varianzas
    /// iguales — cada brazo puede tener un tamaño de muestra distinto).
    /// </summary>
    public static bool HasSufficientPower(
        double deltaObserved, double varianceArmed, int countArmed, double varianceControl, int countControl,
        double multiplier = StandardErrorMultiplier)
    {
        if (countArmed <= 1 || countControl <= 1)
        {
            return false;
        }

        double standardError = StandardError(varianceArmed, countArmed, varianceControl, countControl);
        if (standardError <= 0.0)
        {
            // Varianza nula en los dos brazos: o el efecto es perfectamente constante (raro, pero
            // entonces cualquier delta distinto de cero es una señal real) o no hay datos de verdad.
            return Math.Abs(deltaObserved) > 0.0;
        }

        return Math.Abs(deltaObserved) >= multiplier * standardError;
    }

    /// <summary>Error estándar combinado de la diferencia de dos medias muestrales independientes (Welch).</summary>
    public static double StandardError(double varianceArmed, int countArmed, double varianceControl, int countControl)
    {
        if (countArmed <= 0 || countControl <= 0)
        {
            return double.PositiveInfinity;
        }

        return Math.Sqrt((varianceArmed / countArmed) + (varianceControl / countControl));
    }

    /// <summary>Varianza muestral (con corrección de Bessel, n-1) de una serie de valores por partido.</summary>
    public static double SampleVariance(IReadOnlyList<double> perMatchValues)
    {
        ArgumentNullException.ThrowIfNull(perMatchValues);
        int n = perMatchValues.Count;
        if (n <= 1)
        {
            return 0.0;
        }

        double mean = 0.0;
        for (int i = 0; i < n; i++)
        {
            mean += perMatchValues[i];
        }

        mean /= n;

        double sumSquares = 0.0;
        for (int i = 0; i < n; i++)
        {
            double diff = perMatchValues[i] - mean;
            sumSquares += diff * diff;
        }

        return sumSquares / (n - 1);
    }
}
