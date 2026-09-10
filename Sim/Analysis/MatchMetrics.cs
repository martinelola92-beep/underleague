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
    int BallThird2,
    int ShotsOnTarget,
    int Saves,
    int ShotsBlocked,
    int Fouls,
    int YellowCards,
    int RedCards,
    int PassesAttempted,
    int PassesCompleted,
    int PassesIntercepted,
    int PassesLoose,
    int PassesBeaten,
    int ThroughPasses,
    int ThroughPassesCompleted)
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
        report.BallTicksByThird[2],
        report.ShotsOnTarget[0] + report.ShotsOnTarget[1],
        report.Saves[0] + report.Saves[1],
        report.ShotsBlocked[0] + report.ShotsBlocked[1],
        report.Fouls,
        report.YellowCards,
        report.RedCards,
        report.Players.Sum(p => p.PassesAttempted),
        report.Players.Sum(p => p.PassesCompleted),
        report.PassesIntercepted[0] + report.PassesIntercepted[1],
        report.PassesLoose[0] + report.PassesLoose[1],
        report.PassesBeaten[0] + report.PassesBeaten[1],
        report.ThroughPasses[0] + report.ThroughPasses[1],
        report.ThroughPassesCompleted[0] + report.ThroughPassesCompleted[1]);
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

    /// <summary>
    /// Nombre de la métrica informativa de goles por partido (ambos equipos), paso 0 de
    /// `docs/plan-intercepcion-disparo.md`: referencia sobre la que miden los pasos 1 y 2 (AW-A).
    /// </summary>
    public const string GoalsPerMatch = "goalsPerMatch";

    /// <summary>Faltas ocurridas por partido, señaladas o no (AZ-E, ADR 0090). INFO.</summary>
    public const string FoulsPerMatch = "foulsPerMatch";

    public const string YellowCardsPerMatch = "yellowCardsPerMatch";

    public const string RedCardsPerMatch = "redCardsPerMatch";

    /// <summary>Desenlaces del pase (AZ-B paso 0), en % de los intentados. INFO.</summary>
    public const string PassCompletionRate = "passCompletionRate";

    public const string PassInterceptRate = "passInterceptRate";

    public const string PassLooseRate = "passLooseRate";

    public const string PassBeatenRate = "passBeatenRate";
    public const string ThroughPassesPerMatch = "throughPassesPerMatch";
    public const string ThroughPassCompletionRate = "throughPassCompletionRate";

    /// <summary>Nombre de la métrica informativa de porcentaje de tiros que van a puerta (paso 0).</summary>
    public const string ShotsOnTargetShare = "shotsOnTargetShare";

    /// <summary>Nombre de la métrica informativa de porcentaje de tiros a puerta que el portero para (paso 0).</summary>
    public const string SaveRate = "saveRate";

    /// <summary>
    /// Nombre de la métrica informativa de porcentaje de tiros que un jugador de campo bloquea en vuelo
    /// (AW-A, paso 3). El denominador son <b>todos</b> los tiros, no solo los que iban a puerta: el
    /// bloqueo ocurre antes de que se sepa si el disparo habría entrado, y un defensa se cruza igual ante
    /// un tiro que se iba fuera. Es la diferencia con <c>saveRate</c>, que sí depende de
    /// <c>shotsOnTarget</c> porque el portero solo dispute lo que va entre los tres palos.
    /// </summary>
    public const string BlockRate = "blockRate";

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
        long goals = 0;
        long shotsOnTarget = 0;
        long saves = 0, fouls = 0, yellows = 0, reds = 0, passesAttempted = 0, passesCompleted = 0, passesIntercepted = 0, passesLoose = 0, passesBeaten = 0, throughPasses = 0, throughPassesCompleted = 0;
        long shotsBlocked = 0;
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
            goals += match.HomeGoals + match.AwayGoals;
            shotsOnTarget += match.ShotsOnTarget;
            saves += match.Saves;
            fouls += match.Fouls;
            passesAttempted += match.PassesAttempted;
            passesCompleted += match.PassesCompleted;
            passesIntercepted += match.PassesIntercepted;
            passesLoose += match.PassesLoose;
            passesBeaten += match.PassesBeaten;
            throughPasses += match.ThroughPasses;
            throughPassesCompleted += match.ThroughPassesCompleted;
            yellows += match.YellowCards;
            reds += match.RedCards;
            shotsBlocked += match.ShotsBlocked;
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

        // ADR 0081: 12-25 medía un motor de fase 0 sin bloqueo de tiro, sin que el portero tuviera que
        // llegar al balón y sin decisión inmediata al cambiar de posesión; con esos tres cambios (y los
        // anteriores de AW-D/AW-E) el valor natural del motor actual va de 22,07 a 26,60 en la versión
        // final de cada uno. El techo sube a 28, un margen sobre lo medido, no una previsión.
        rows.Add(InRange(PossessionChanges, (double)possessionChanges / n, 12, 28));

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

        // ADR 0082: 0,3-0,8 medía un motor donde el equipo se quedaba congelado en cada balón muerto; con
        // AW-R recomponiendo la marca antes de reanudar, el peor valor medido (tres semillas de 500
        // partidos) sube a 0,86-0,87. El techo sube a 0,90, justo por encima de lo medido, no una
        // previsión.
        rows.Add(InRange(InjuriesPerMatch, (double)injuries / n, 0.3, 0.9));

        // Paso 0 de docs/plan-intercepcion-disparo.md: instrumentación pura, sin banda de gating. Fijan la
        // referencia de goalsPerMatch y saveRate que usarán los pasos 1 (AW-A) y 2 al medir el efecto de
        // exigirle al portero llegar al balón.
        rows.Add(new MetricResult(GoalsPerMatch, (double)goals / n, null, null, "INFO"));
        rows.Add(new MetricResult(FoulsPerMatch, (double)fouls / n, null, null, "INFO"));
        rows.Add(new MetricResult(YellowCardsPerMatch, (double)yellows / n, null, null, "INFO"));
        rows.Add(new MetricResult(RedCardsPerMatch, (double)reds / n, null, null, "INFO"));
        double attempted = Math.Max(1, passesAttempted);
        rows.Add(new MetricResult(PassCompletionRate, 100.0 * passesCompleted / attempted, null, null, "INFO"));
        rows.Add(new MetricResult(PassInterceptRate, 100.0 * passesIntercepted / attempted, null, null, "INFO"));
        rows.Add(new MetricResult(PassLooseRate, 100.0 * passesLoose / attempted, null, null, "INFO"));
        rows.Add(new MetricResult(PassBeatenRate, 100.0 * passesBeaten / attempted, null, null, "INFO"));
        rows.Add(new MetricResult(ThroughPassesPerMatch, (double)throughPasses / Math.Max(1, matches.Count), null, null, "INFO"));
        rows.Add(new MetricResult(ThroughPassCompletionRate, 100.0 * throughPassesCompleted / Math.Max(1, throughPasses), null, null, "INFO"));

        double shotsOnTargetShare = shots > 0 ? 100.0 * shotsOnTarget / shots : 0.0;
        rows.Add(new MetricResult(ShotsOnTargetShare, shotsOnTargetShare, null, null, "INFO"));

        double saveRate = shotsOnTarget > 0 ? 100.0 * saves / shotsOnTarget : 0.0;
        rows.Add(new MetricResult(SaveRate, saveRate, null, null, "INFO"));

        double blockRate = shots > 0 ? 100.0 * shotsBlocked / shots : 0.0;
        rows.Add(new MetricResult(BlockRate, blockRate, null, null, "INFO"));

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
