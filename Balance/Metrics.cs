namespace Underleague.Balance;

/// <summary>Una fila de summary.csv: metric,value,rangeMin,rangeMax,status (docs/fase0-diseno.md §4, docs/balance.md RT-056).</summary>
public sealed record MetricRow(string Name, double Value, double? RangeMin, double? RangeMax, string Status);

/// <summary>
/// Calcula las métricas de sensación de fútbol (RT-056) y betterTeamWinRate a partir de los partidos
/// del lote. Todas las métricas de rango obligatorio dan IN/OUT; scorelineShare_1-0_to_3-2,
/// ballThirdMaxShare y betterTeamWinRate (con diferencia de calidad 20) también dan IN/OUT;
/// share_over5goals, drawShareAtRegulation y betterTeamWinRate con otra diferencia de calidad son
/// siempre INFO (no bloquean el código de salida), tal como pide §4.
/// </summary>
public static class Metrics
{
    public static List<MetricRow> Compute(IReadOnlyList<MatchRow> matches, ReferenceConfig reference)
    {
        var rows = new List<MetricRow>();
        int n = matches.Count;
        if (n == 0)
        {
            return rows;
        }

        rows.Add(InRange("possessionChanges", matches.Average(m => m.PossessionChanges), 12, 25));

        int totalPassChains = matches.Sum(m => m.PassChains);
        int totalPassChainLength = matches.Sum(m => m.PassChainTotalLength);
        double passChainAvgLength = totalPassChains > 0 ? (double)totalPassChainLength / totalPassChains : 0.0;
        rows.Add(InRange("passChainAvgLength", passChainAvgLength, 2, 4));

        rows.Add(InRange("shotsPerMatch", matches.Average(m => m.Shots), 8, 16));

        // scorelineShare_1-0_to_3-2: porcentaje de partidos cuyo marcador final tiene entre 1 y 5 goles
        // totales con diferencia de 1 o 2 goles (1-0, 2-0, 2-1, 3-1, 3-2 y sus simétricos visitante-local).
        int scorelineCount = matches.Count(m => IsCreditableScoreline(m.HomeGoals, m.AwayGoals));
        double scorelineShare = 100.0 * scorelineCount / n;
        rows.Add(new MetricRow("scorelineShare_1-0_to_3-2", scorelineShare, 50, 100, scorelineShare >= 50 ? "IN" : "OUT"));

        int over5Count = matches.Count(m => m.HomeGoals + m.AwayGoals > 5);
        double over5Share = 100.0 * over5Count / n;
        rows.Add(new MetricRow("share_over5goals", over5Share, null, 5, "INFO"));

        // drawShareAtRegulation: porcentaje de partidos que llegaron empatados al final del reglamentario
        // (WentToGoldenGoal), es decir, antes de resolver con gol de oro/turba.
        int drawCount = matches.Count(m => m.GoldenGoal);
        double drawShare = 100.0 * drawCount / n;
        rows.Add(new MetricRow("drawShareAtRegulation", drawShare, null, 15, "INFO"));

        // ballThirdMaxShare: se agregan los ticks de balón por tercio de TODOS los partidos y se toma el
        // máximo de los tres porcentajes resultantes (no la media de los máximos por partido).
        long[] thirdTotals = new long[3];
        foreach (var match in matches)
        {
            thirdTotals[0] += match.BallThird0;
            thirdTotals[1] += match.BallThird1;
            thirdTotals[2] += match.BallThird2;
        }

        long thirdsSum = thirdTotals[0] + thirdTotals[1] + thirdTotals[2];
        double ballThirdMaxShare = thirdsSum > 0 ? 100.0 * thirdTotals.Max() / thirdsSum : 0.0;
        rows.Add(new MetricRow("ballThirdMaxShare", ballThirdMaxShare, 0, 50, ballThirdMaxShare <= 50 ? "IN" : "OUT"));

        rows.Add(InRange("tacklesPerMatch", matches.Average(m => m.Tackles), 6, 14));
        rows.Add(InRange("injuriesPerMatch", matches.Average(m => m.Injuries), 0.3, 0.8));

        rows.AddRange(BetterTeamWinRates(matches, reference));

        return rows;
    }

    /// <summary>1-0, 2-0, 2-1, 3-1, 3-2 y simétricos: total de goles en [1,5] y diferencia en {1,2}.</summary>
    private static bool IsCreditableScoreline(int homeGoals, int awayGoals)
    {
        int total = homeGoals + awayGoals;
        int diff = Math.Abs(homeGoals - awayGoals);
        return total is >= 1 and <= 5 && diff is 1 or 2;
    }

    private static IEnumerable<MetricRow> BetterTeamWinRates(IReadOnlyList<MatchRow> matches, ReferenceConfig reference)
    {
        var qualityById = reference.Teams.ToDictionary(t => t.Id, t => t.Quality, StringComparer.Ordinal);

        foreach (var pairing in reference.Pairings)
        {
            int homeQuality = qualityById[pairing.HomeId];
            int awayQuality = qualityById[pairing.AwayId];
            if (homeQuality == awayQuality)
            {
                continue;
            }

            var pairingMatches = matches.Where(m => m.HomeId == pairing.HomeId && m.AwayId == pairing.AwayId).ToList();
            if (pairingMatches.Count == 0)
            {
                continue;
            }

            string betterId = homeQuality > awayQuality ? pairing.HomeId : pairing.AwayId;
            int betterWins = pairingMatches.Count(m => (m.Winner == 0 ? m.HomeId : m.AwayId) == betterId);
            double rate = 100.0 * betterWins / pairingMatches.Count;

            int qualityDiff = Math.Abs(homeQuality - awayQuality);
            string status = qualityDiff == 20 ? (rate is >= 65 and <= 80 ? "IN" : "OUT") : "INFO";

            yield return new MetricRow($"betterTeamWinRate_{pairing.HomeId}_vs_{pairing.AwayId}", rate, 65, 80, status);
        }
    }

    private static MetricRow InRange(string name, double value, double min, double max) =>
        new(name, value, min, max, value >= min && value <= max ? "IN" : "OUT");
}
