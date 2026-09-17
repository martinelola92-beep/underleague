namespace Underleague.Sim.Analysis;

/// <summary>
/// Valor de una métrica de <see cref="MatchMetrics"/> para UN partido (no el agregado del lote), usado
/// solo para el power-check de §5.5/§18 (varianza por partido) — el número reportado como métrica oficial
/// sigue siendo el de <see cref="MatchMetrics.Compute"/>. <see cref="MatchMetrics.BallThirdMaxShare"/> se
/// calcula aquí con la MISMA fórmula (máximo tercio / suma de los tres) aplicada a un solo partido en vez
/// de al lote entero — no es una métrica nueva, es la fórmula existente evaluada a escala de un partido.
/// </summary>
public static class PrimaryMetricPerMatch
{
    /// <summary>
    /// <c>null</c> si <paramref name="metricName"/> no tiene una traducción por-partido conocida (p. ej.
    /// las métricas "bespoke" de selección de objetivo, §18: no se inventa un valor cuando no lo hay).
    /// </summary>
    public static double? Value(string metricName, MatchSummary match)
    {
        if (metricName == MatchMetrics.TacklesPerMatch)
        {
            return match.Tackles;
        }

        if (metricName == MatchMetrics.InjuriesPerMatch)
        {
            return match.Injuries;
        }

        if (metricName == MatchMetrics.ShotsPerMatch)
        {
            return match.Shots;
        }

        if (metricName == MatchMetrics.BallThirdMaxShare)
        {
            return BallThirdShare(match);
        }

        if (metricName == MatchMetrics.PossessionChanges)
        {
            return match.PossessionChanges;
        }

        if (metricName == MatchMetrics.PassChainAvgLength)
        {
            return match.PassChains > 0 ? (double)match.PassChainTotalLength / match.PassChains : 0.0;
        }

        if (metricName == MatchMetrics.ScorelineShare)
        {
            return MatchMetrics.IsCreditableScoreline(match.HomeGoals, match.AwayGoals) ? 1.0 : 0.0;
        }

        return null;
    }

    private static double BallThirdShare(MatchSummary match)
    {
        long sum = match.BallThird0 + match.BallThird1 + match.BallThird2;
        if (sum <= 0)
        {
            return 0.0;
        }

        long max = Math.Max(match.BallThird0, Math.Max(match.BallThird1, match.BallThird2));
        return 100.0 * max / sum;
    }
}
