namespace Underleague.Sim.Analysis;

/// <summary>
/// Estrategia de búsqueda del valor por tipo de parámetro (§7 de
/// docs/analisis/protocolo-balanceo-automatizado.md). Cada tipo de parámetro tiene un método propio; no
/// se fuerza bisección continua donde una tripleta anclada en cuartiles ya basta (bonus de utilidad,
/// probabilidades) ni donde no hay parámetro que buscar (selección de objetivo, §6.5).
/// </summary>
public static class BalanceValueSearch
{
    /// <summary>
    /// Tripleta bajo/central/alto anclada en los cuartiles de una distribución medida barata (mismo
    /// principio que C1 §3.1b/§4.0: los candidatos vienen de una medición del hueco mecánico, no de un
    /// redondeo a ojo).
    /// </summary>
    public static (double Low, double Central, double High) AnchoredTriplet(IReadOnlyList<double> measuredGaps)
    {
        ArgumentNullException.ThrowIfNull(measuredGaps);
        if (measuredGaps.Count == 0)
        {
            throw new ArgumentException("no hay medición del hueco mecánico que anclar (§6.5: este perk no tiene una distancia numérica que buscar)", nameof(measuredGaps));
        }

        var sorted = measuredGaps.OrderBy(v => v).ToList();
        return (Percentile(sorted, 0.25), Percentile(sorted, 0.50), Percentile(sorted, 0.75));
    }

    /// <summary>Percentil por interpolación lineal sobre una lista ya ordenada (mismo método usado en C1 §3.1b).</summary>
    public static double Percentile(IReadOnlyList<double> sortedValues, double p)
    {
        ArgumentNullException.ThrowIfNull(sortedValues);
        int n = sortedValues.Count;
        if (n == 0)
        {
            throw new ArgumentException("lista vacía", nameof(sortedValues));
        }

        if (n == 1)
        {
            return sortedValues[0];
        }

        double k = (n - 1) * p;
        int f = (int)k;
        int c = Math.Min(f + 1, n - 1);
        if (f == c)
        {
            return sortedValues[f];
        }

        double d = k - f;
        return sortedValues[f] + ((sortedValues[c] - sortedValues[f]) * d);
    }

    /// <summary>
    /// Siguiente candidato entero para parámetros de geometría (RT-023, aritmética entera — casillas):
    /// bisección simple entre el último válido y el límite hacia el que hay que moverse.
    /// </summary>
    public static int NextIntegerBisection(int lastValid, int target) =>
        lastValid + Math.Sign(target - lastValid) * Math.Max(1, Math.Abs(target - lastValid) / 2);

    /// <summary>
    /// Un paso más allá del candidato "alto", mismo tamaño de paso relativo (§6.2, extrapolación cuando
    /// los tres candidatos quedan por debajo de la banda objetivo).
    /// </summary>
    public static double ExtrapolateOneStepBeyondHigh(double central, double high)
    {
        double step = high - central;
        return high + step;
    }
}
