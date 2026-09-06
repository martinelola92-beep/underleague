using Underleague.Sim.Engine;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Resumen de un partido reducido a lo que necesitan las métricas de RT-056. Se construye desde un
/// <see cref="MatchReport"/> con <see cref="FromReport"/>; /Balance añade además sus propias columnas
/// de CSV, que no intervienen en las métricas.
/// </summary>
public readonly record struct MatchSummary(
    string HomeId,
    string AwayId,
    int HomeGoals,
    int AwayGoals,
    int Winner,
    bool WentToGoldenGoal,
    int PossessionChanges,
    int PassChains,
    int PassChainTotalLength,
    int Shots,
    int Tackles,
    int Injuries,
    int BallThird0,
    int BallThird1,
    int BallThird2)
{
    /// <summary>Resumen de un informe de partido entre homeId (equipo 0) y awayId (equipo 1).</summary>
    public static MatchSummary FromReport(MatchReport report, string homeId, string awayId) => new(
        homeId,
        awayId,
        report.Goals[0],
        report.Goals[1],
        report.Winner,
        report.WentToGoldenGoal,
        report.PossessionChanges,
        report.PassChains,
        report.PassChainTotalLength,
        report.Shots[0] + report.Shots[1],
        report.Tackles,
        report.Injuries,
        report.BallTicksByThird[0],
        report.BallTicksByThird[1],
        report.BallTicksByThird[2]);
}

/// <summary>Un emparejamiento del lote con la calidad de cada equipo, para betterTeamWinRate.</summary>
public readonly record struct MetricPairing(string HomeId, string AwayId, int HomeQuality, int AwayQuality);

/// <summary>
/// Una métrica calculada: valor, rango objetivo (null si no hay límite por ese lado) y estado
/// <c>IN</c>/<c>OUT</c>/<c>INFO</c>. <c>INFO</c> nunca hace fallar la puerta.
/// </summary>
public sealed record MetricResult(string Name, double Value, double? RangeMin, double? RangeMax, string Status);

/// <summary>
/// Métricas de sensación de fútbol (RT-056) y betterTeamWinRate a partir de los partidos de un lote.
/// Pura: sin E/S, sin reloj y sin aleatoriedad, para que la comparta /Balance y la puerta estadística
/// de Sim.Tests (docs/balance.md, docs/fase0-diseno.md §4 y §6).
/// </summary>
public static class MatchMetrics
{
    /// <summary>Nombre de la métrica de alternancias de posesión.</summary>
    public const string PossessionChanges = "possessionChanges";

    /// <summary>Nombre de la métrica de longitud media de cadena de pases.</summary>
    public const string PassChainAvgLength = "passChainAvgLength";

    /// <summary>Nombre de la métrica de tiros por partido.</summary>
    public const string ShotsPerMatch = "shotsPerMatch";

    /// <summary>Nombre de la métrica de reparto de resultados creíbles.</summary>
    public const string ScorelineShare = "scorelineShare_1-0_to_3-2";

    /// <summary>Nombre de la métrica informativa de partidos con más de cinco goles.</summary>
    public const string ShareOverFiveGoals = "share_over5goals";

    /// <summary>Nombre de la métrica informativa de empates al final del reglamentario.</summary>
    public const string DrawShareAtRegulation = "drawShareAtRegulation";

    /// <summary>Nombre de la métrica del tercio más ocupado por el balón.</summary>
    public const string BallThirdMaxShare = "ballThirdMaxShare";

    /// <summary>Nombre de la métrica de entradas por partido.</summary>
    public const string TacklesPerMatch = "tacklesPerMatch";

    /// <summary>Nombre de la métrica de lesiones por partido.</summary>
    public const string InjuriesPerMatch = "injuriesPerMatch";

    /// <summary>Prefijo del nombre de las métricas de tasa de victoria del mejor equipo.</summary>
    public const string BetterTeamWinRatePrefix = "betterTeamWinRate_";

    /// <summary>
    /// Banda de <see cref="BetterTeamWinRatePrefix"/> con una diferencia de calidad de 20 (ADR 0054).
    /// Sube de 65-80 a 70-88 porque la de fase 0 se fijó cuando todas las resoluciones eran lineales y de
    /// varianza máxima, y todo lo hecho desde entonces sube el peso de la habilidad. El techo existe para
    /// que el peor equipo pueda ganar: con 88, un equipo veinte puntos peor todavía gana una de cada
    /// ocho veces; por encima de 90 el resultado se vuelve determinista y el partido deja de interesar.
    /// </summary>
    public const double BetterTeamWinRateMin = 70;

    /// <inheritdoc cref="BetterTeamWinRateMin"/>
    public const double BetterTeamWinRateMax = 88;

    /// <summary>Diferencia de calidad para la que betterTeamWinRate es obligatoria (fase 0, §4).</summary>
    public const int GatedQualityDifference = 20;

    /// <summary>
    /// Calcula todas las métricas del lote en el orden de summary.csv. Las de rango obligatorio dan
    /// IN/OUT; share_over5goals, drawShareAtRegulation y betterTeamWinRate con una diferencia de calidad
    /// distinta de <see cref="GatedQualityDifference"/> son siempre INFO.
    /// </summary>
    public static List<MetricResult> Compute(IReadOnlyList<MatchSummary> matches, IReadOnlyList<MetricPairing> pairings)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(pairings);

        var rows = new List<MetricResult>();
        int n = matches.Count;
        if (n == 0)
        {
            return rows;
        }

        long possessionChanges = 0;
        long passChains = 0;
        long passChainLength = 0;
        long shots = 0;
        long tackles = 0;
        long injuries = 0;
        int scorelineCount = 0;
        int overFiveCount = 0;
        int drawCount = 0;
        var thirds = new long[3];

        for (int i = 0; i < n; i++)
        {
            var match = matches[i];
            possessionChanges += match.PossessionChanges;
            passChains += match.PassChains;
            passChainLength += match.PassChainTotalLength;
            shots += match.Shots;
            tackles += match.Tackles;
            injuries += match.Injuries;
            thirds[0] += match.BallThird0;
            thirds[1] += match.BallThird1;
            thirds[2] += match.BallThird2;

            if (IsCreditableScoreline(match.HomeGoals, match.AwayGoals))
            {
                scorelineCount++;
            }

            if (match.HomeGoals + match.AwayGoals > 5)
            {
                overFiveCount++;
            }

            if (match.WentToGoldenGoal)
            {
                drawCount++;
            }
        }

        rows.Add(InRange(PossessionChanges, (double)possessionChanges / n, 12, 25));

        double passChainAvgLength = passChains > 0 ? (double)passChainLength / passChains : 0.0;
        rows.Add(InRange(PassChainAvgLength, passChainAvgLength, 2, 4));

        rows.Add(InRange(ShotsPerMatch, (double)shots / n, 8, 16));

        // scorelineShare_1-0_to_3-2: porcentaje de partidos cuyo marcador final tiene entre 1 y 5 goles
        // totales con diferencia de 1 o 2 goles (1-0, 2-0, 2-1, 3-1, 3-2 y sus simétricos).
        double scorelineShare = 100.0 * scorelineCount / n;
        rows.Add(new MetricResult(ScorelineShare, scorelineShare, 50, 100, scorelineShare >= 50 ? "IN" : "OUT"));

        rows.Add(new MetricResult(ShareOverFiveGoals, 100.0 * overFiveCount / n, null, 5, "INFO"));

        // drawShareAtRegulation: partidos que llegaron empatados al final del reglamentario, es decir,
        // los que entraron en gol de oro (WentToGoldenGoal).
        rows.Add(new MetricResult(DrawShareAtRegulation, 100.0 * drawCount / n, null, 15, "INFO"));

        // ballThirdMaxShare: se agregan los ticks de balón por tercio de TODOS los partidos y se toma el
        // máximo de los tres porcentajes resultantes (no la media de los máximos por partido).
        long thirdsSum = thirds[0] + thirds[1] + thirds[2];
        long thirdsMax = Math.Max(thirds[0], Math.Max(thirds[1], thirds[2]));
        double ballThirdMaxShare = thirdsSum > 0 ? 100.0 * thirdsMax / thirdsSum : 0.0;
        rows.Add(new MetricResult(BallThirdMaxShare, ballThirdMaxShare, 0, 50, ballThirdMaxShare <= 50 ? "IN" : "OUT"));

        rows.Add(InRange(TacklesPerMatch, (double)tackles / n, 6, 14));
        rows.Add(InRange(InjuriesPerMatch, (double)injuries / n, 0.3, 0.8));

        rows.AddRange(BetterTeamWinRates(matches, pairings));
        return rows;
    }

    /// <summary>1-0, 2-0, 2-1, 3-1, 3-2 y simétricos: total de goles en [1,5] y diferencia en {1,2}.</summary>
    public static bool IsCreditableScoreline(int homeGoals, int awayGoals)
    {
        int total = homeGoals + awayGoals;
        int difference = Math.Abs(homeGoals - awayGoals);
        return total is >= 1 and <= 5 && difference is 1 or 2;
    }

    private static List<MetricResult> BetterTeamWinRates(IReadOnlyList<MatchSummary> matches, IReadOnlyList<MetricPairing> pairings)
    {
        var rows = new List<MetricResult>();
        for (int p = 0; p < pairings.Count; p++)
        {
            var pairing = pairings[p];
            if (pairing.HomeQuality == pairing.AwayQuality)
            {
                continue;
            }

            bool betterIsHome = pairing.HomeQuality > pairing.AwayQuality;
            int played = 0;
            int betterWins = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!string.Equals(match.HomeId, pairing.HomeId, StringComparison.Ordinal)
                    || !string.Equals(match.AwayId, pairing.AwayId, StringComparison.Ordinal))
                {
                    continue;
                }

                played++;
                if ((match.Winner == 0) == betterIsHome)
                {
                    betterWins++;
                }
            }

            if (played == 0)
            {
                continue;
            }

            double rate = 100.0 * betterWins / played;
            int difference = Math.Abs(pairing.HomeQuality - pairing.AwayQuality);
            string status = difference == GatedQualityDifference
                ? (rate >= BetterTeamWinRateMin && rate <= BetterTeamWinRateMax ? "IN" : "OUT")
                : "INFO";

            rows.Add(new MetricResult($"{BetterTeamWinRatePrefix}{pairing.HomeId}_vs_{pairing.AwayId}", rate, BetterTeamWinRateMin, BetterTeamWinRateMax, status));
        }

        return rows;
    }

    private static MetricResult InRange(string name, double value, double min, double max) =>
        new(name, value, min, max, value >= min && value <= max ? "IN" : "OUT");
}
