using Underleague.Sim.Analysis;

namespace Underleague.Balance;

/// <summary>Una fila de summary.csv: metric,value,rangeMin,rangeMax,status (docs/fase0-diseno.md §4, docs/balance.md RT-056).</summary>
public sealed record MetricRow(string Name, double Value, double? RangeMin, double? RangeMax, string Status);

/// <summary>
/// Adaptador de <see cref="MatchMetrics"/> (en /Sim, sin E/S) al formato de summary.csv. El cálculo de
/// las métricas RT-056 vive en /Sim para que la puerta estadística de Sim.Tests use exactamente el
/// mismo código que el lote de /Balance (docs/fase0-diseno.md §6).
/// </summary>
public static class Metrics
{
    public static List<MetricRow> Compute(IReadOnlyList<MatchRow> matches, ReferenceConfig reference)
    {
        var summaries = new List<MatchSummary>(matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            summaries.Add(new MatchSummary(
                match.HomeId,
                match.AwayId,
                match.HomeGoals,
                match.AwayGoals,
                match.Winner,
                match.GoldenGoal,
                match.PossessionChanges,
                match.PassChains,
                match.PassChainTotalLength,
                match.Shots,
                match.Tackles,
                match.Injuries,
                match.BallThird0,
                match.BallThird1,
                match.BallThird2));
        }

        var qualityById = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var team in reference.Teams)
        {
            qualityById[team.Id] = team.Quality;
        }

        var pairings = new List<MetricPairing>(reference.Pairings.Count);
        foreach (var pairing in reference.Pairings)
        {
            pairings.Add(new MetricPairing(
                pairing.HomeId,
                pairing.AwayId,
                qualityById[pairing.HomeId],
                qualityById[pairing.AwayId]));
        }

        var rows = new List<MetricRow>();
        foreach (var result in MatchMetrics.Compute(summaries, pairings))
        {
            rows.Add(new MetricRow(result.Name, result.Value, result.RangeMin, result.RangeMax, result.Status));
        }

        return rows;
    }
}
