namespace Underleague.Sim.Analysis;

/// <summary>
/// Agregado objetivo de <see cref="Underleague.Sim.Engine.MatchReport.FinalBias"/> (§16.2 de
/// docs/analisis/protocolo-balanceo-automatizado.md, "MissingAggregateMetric" de <c>modifyBias</c>).
/// <c>FinalBias</c> ya existe por partido, relativo al equipo 0 (<c>MatchEngine.BiasFor</c>); esta clase
/// solo lo normaliza al equipo del portador del perk y lo resume — no inventa ningún dato nuevo.
///
/// <para><b>Que esta métrica exista no la hace suficiente para balanceo automático</b> (distinción pedida
/// explícitamente): no hay ninguna banda RT-056 ni ADR que diga qué rango de sesgo medio es aceptable, así
/// que un perk de <c>modifyBias</c> con esta métrica calculada sigue siendo <c>DESIGN_REVIEW</c>
/// (`MissingBand`), nunca `ReadyForScreening`, hasta que exista ese criterio — ver
/// <see cref="PerkBalanceClassifier"/>.</para>
/// </summary>
public static class RefereeBiasMetrics
{
    /// <summary>
    /// Sesgo normalizado a favor de <paramref name="team"/> — misma fórmula que
    /// <c>MatchEngine.BiasFor</c> (no se duplica su definición, solo se aplica desde fuera del motor,
    /// que es todo lo que un fichero de <c>/Sim</c> con cero E/S puede hacer con un <c>FinalBias</c> ya
    /// calculado).
    /// </summary>
    public static int BiasFavoring(int finalBias, int team) => team == 0 ? finalBias : -finalBias;

    /// <summary>Media del sesgo a favor del equipo del portador, sobre una serie de partidos ya jugados.</summary>
    public static double MeanBiasFavoringCarrier(IReadOnlyList<(int FinalBias, int CarrierTeam)> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        if (matches.Count == 0)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = 0; i < matches.Count; i++)
        {
            sum += BiasFavoring(matches[i].FinalBias, matches[i].CarrierTeam);
        }

        return sum / matches.Count;
    }

    /// <summary>Varianza muestral del sesgo a favor del portador — para el power-check de §5.5.</summary>
    public static double VarianceBiasFavoringCarrier(IReadOnlyList<(int FinalBias, int CarrierTeam)> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        var values = new double[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            values[i] = BiasFavoring(matches[i].FinalBias, matches[i].CarrierTeam);
        }

        return BalancePowerCheck.SampleVariance(values);
    }
}
