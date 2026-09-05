using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Métricas de run completa de la fase 2 (fase2-diseno.md §10) y de escasez económica (ADR 0037).
/// Puras, sin E/S y sin reloj, como <see cref="MatchMetrics"/> y <see cref="BossGateMetrics"/>: las
/// comparten la puerta de <c>Sim.Tests</c> y el modo <c>--full-runs</c> de <c>/Balance</c>, que solo se
/// diferencian en quién lee los ficheros.
///
/// <para>La métrica principal de la fase sigue siendo la curva de puertas de la ADR 0033
/// (<see cref="BossGateMetrics"/>). Estas responden a la pregunta que la curva deja abierta —si la
/// <b>economía</b> permite llegar a cada puerta con la build que esa puerta exige— y a la que la ADR
/// 0037 añade: si la escasez convierte comprar en una <b>decisión</b>.</para>
/// </summary>
public static class FullRunMetrics
{
    /// <summary>Tasa de victoria de la run con la política contextual (25-40%).</summary>
    public const string RunWinRate = "runWinRate";

    /// <summary>Porcentaje de las <b>derrotas</b> que son por bajar de 5 jugadores (&lt; 35%).</summary>
    public const string RosterDefeatShare = "defeatShare_notEnoughPlayers";

    /// <summary>Partidos que juega una run que llega hasta el jefe final (18-22).</summary>
    public const string MatchesPerFullRun = "matchesPerFullRun";

    /// <summary>Muertes por run (0,5-2).</summary>
    public const string DeathsPerRun = "deathsPerRun";

    /// <summary>Sumideros que el oro de un acto permite pagar; 2-3 y nunca todos (RF-114k).</summary>
    public const string SinksAffordablePerAct = "sinksAffordablePerAct";

    /// <summary>Fracción del surtido que el jugador puede pagar al llegar a un mercado (20-35%, ADR 0037).</summary>
    public const string AffordableShare = "affordableShareAtMarket";

    /// <summary>Compras por visita a un mercado (1-2, ADR 0037).</summary>
    public const string PurchasesPerMarket = "purchasesPerMarket";

    /// <summary>Oro sobrante al terminar la run, en porcentaje del ganado (&lt; 15%, ADR 0037).</summary>
    public const string LeftoverGoldShare = "leftoverGoldShare";

    /// <summary>Runs que llegan a un mercado sin poder comprar nada de pago (10-25%, ADR 0037).</summary>
    public const string BrokeMarketRunShare = "brokeMarketRunShare";

    /// <summary>Ventaja de la política contextual sobre la mejor de las dos puras, en puntos (&gt;= 8, ADR 0037).</summary>
    public const string ContextualAdvantage = "contextualAdvantage";

    /// <summary>Prefijo del reparto de las derrotas por acto (ADR 0043: la mayoría deben caer en el acto 2).</summary>
    public const string DefeatShareByActPrefix = "defeatShareAct";

    /// <summary>Partidos que juega una run ganada.</summary>
    public const string MatchesPerWonRun = "matchesPerWonRun";

    /// <summary>Partidos que juega una run perdida (curva-de-dificultad.md §2.2: perder tiene que ser barato).</summary>
    public const string MatchesPerLostRun = "matchesPerLostRun";

    /// <summary>Nodos que recorre una run ganada.</summary>
    public const string NodesPerWonRun = "nodesPerWonRun";

    /// <summary>Nodos que recorre una run perdida.</summary>
    public const string NodesPerLostRun = "nodesPerLostRun";

    /// <summary>
    /// Tasa de victoria mínima de la run (fase2-diseno.md §10, corregida por la ADR 0040). La banda es
    /// <b>20-30%</b> y no la 25-40% de partida: el producto de las tres celdas "muy buena" de la tabla de
    /// la ADR 0033 da 29,5%, así que el techo antiguo estaba por encima de lo que la propia curva permite
    /// aunque se juegue perfecto, y la trayectoria que la ADR describe —buena antes del primer jefe, muy
    /// buena al final— da entre 21,8% y 28,2%.
    /// </summary>
    public const double RunWinRateMin = 20.0;

    /// <summary>Tasa de victoria máxima de la run (ADR 0040).</summary>
    public const double RunWinRateMax = 30.0;

    /// <summary>Techo del porcentaje de derrotas por quedarse sin plantilla.</summary>
    public const double RosterDefeatShareMax = 35.0;

    /// <summary>Partidos mínimos de una run completa.</summary>
    public const double MatchesPerFullRunMin = 18.0;

    /// <summary>Partidos máximos de una run completa.</summary>
    public const double MatchesPerFullRunMax = 22.0;

    /// <summary>Muertes por run mínimas.</summary>
    public const double DeathsPerRunMin = 0.5;

    /// <summary>Muertes por run máximas.</summary>
    public const double DeathsPerRunMax = 2.0;

    /// <summary>Sumideros pagables por acto: mínimo.</summary>
    public const double SinksMin = 2.0;

    /// <summary>Sumideros pagables por acto: máximo. Todos a la vez sería RF-114k incumplido.</summary>
    public const double SinksMax = 3.0;

    /// <summary>Fracción mínima del surtido asequible (ADR 0037): por debajo, la tienda es decorado.</summary>
    public const double AffordableShareMin = 20.0;

    /// <summary>Fracción máxima del surtido asequible (ADR 0037): por encima no hay elección.</summary>
    public const double AffordableShareMax = 35.0;

    /// <summary>Compras mínimas por visita al mercado.</summary>
    public const double PurchasesPerMarketMin = 1.0;

    /// <summary>Compras máximas por visita al mercado: se compra poco y se piensa.</summary>
    public const double PurchasesPerMarketMax = 2.0;

    /// <summary>Techo del oro sobrante al terminar la run.</summary>
    public const double LeftoverGoldShareMax = 15.0;

    /// <summary>Mínimo de runs que se quedan sin poder comprar en un mercado: ha de doler a veces.</summary>
    public const double BrokeMarketRunShareMin = 10.0;

    /// <summary>Máximo de runs que se quedan sin poder comprar en un mercado: no puede ser la norma.</summary>
    public const double BrokeMarketRunShareMax = 25.0;

    /// <summary>Ventaja mínima de la contextual sobre las dos puras, en puntos de tasa de victoria.</summary>
    public const double ContextualAdvantageMin = 8.0;

    /// <summary>
    /// Sumideros vivos en fase 2 (RF-114k): mercado, clínica, <b>huecos de plantilla</b> (ADR 0046),
    /// rerolls y salarios. El nodo de inscripción es el quinto y entró con la plantilla corta: no estaba
    /// en la lista de RF-114k porque no existía, pero es oro que sale de la run y compite con los demás,
    /// que es lo único que la métrica pregunta.
    /// </summary>
    public const int SinkCount = 5;

    /// <summary>
    /// Contrasta las runs de la política <b>contextual</b> con los rangos de fase2-diseno.md §10 y de la
    /// ADR 0037, y añade la comparación con las dos doctrinas puras, que es la métrica que decide si la
    /// decisión de comprar existe.
    /// </summary>
    /// <param name="contextual">Runs jugadas con <see cref="PurchaseDoctrine.Contextual"/>.</param>
    /// <param name="spender">Runs jugadas con <see cref="PurchaseDoctrine.Spender"/> sobre las mismas semillas.</param>
    /// <param name="saver">Runs jugadas con <see cref="PurchaseDoctrine.Saver"/> sobre las mismas semillas.</param>
    public static List<MetricResult> Compute(
        IReadOnlyList<RunPlayResult> contextual,
        IReadOnlyList<RunPlayResult> spender,
        IReadOnlyList<RunPlayResult> saver,
        EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(contextual);
        ArgumentNullException.ThrowIfNull(spender);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(economy);

        var rows = Describe(contextual, economy);
        if (rows.Count == 0)
        {
            return rows;
        }

        double contextualRate = WinRate(contextual);
        double spenderRate = WinRate(spender);
        double saverRate = WinRate(saver);
        double best = Math.Max(spenderRate, saverRate);

        rows.Insert(0, Banded(ContextualAdvantage, contextualRate - best, ContextualAdvantageMin, null));
        rows.Add(Info("runWinRate_spender", spenderRate));
        rows.Add(Info("runWinRate_saver", saverRate));
        rows.Add(Info("leftoverGoldShare_spender", LeftoverShare(spender)));
        rows.Add(Info("leftoverGoldShare_saver", LeftoverShare(saver)));
        rows.Add(Info("purchasesPerMarket_spender", PurchaseRate(spender)));
        rows.Add(Info("purchasesPerMarket_saver", PurchaseRate(saver)));
        return rows;
    }

    /// <summary>Tasa de victoria de un conjunto de runs, en porcentaje.</summary>
    public static double WinRate(IReadOnlyList<RunPlayResult> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        if (runs.Count == 0)
        {
            return 0.0;
        }

        int won = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            if (runs[i].Won)
            {
                won++;
            }
        }

        return 100.0 * won / runs.Count;
    }

    /// <summary>Métricas de un solo conjunto de runs (una doctrina), con sus bandas y su desglose INFO.</summary>
    public static List<MetricResult> Describe(IReadOnlyList<RunPlayResult> runs, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(economy);

        var rows = new List<MetricResult>();
        if (runs.Count == 0)
        {
            return rows;
        }

        int victories = 0, defeats = 0, rosterDefeats = 0, bossDefeats = 0;
        int deaths = 0, fullRuns = 0, fullRunMatches = 0, matches = 0, marketRuns = 0, brokeRuns = 0;
        int wonMatches = 0, wonNodes = 0, lostMatches = 0, lostNodes = 0, rewardsTaken = 0, rewardsDeclined = 0;
        var defeatsByAct = new int[RunRules.Acts];
        long goldEarned = 0, market = 0, clinic = 0, enrollment = 0, reroll = 0, wages = 0, left = 0;
        long slots = 0;
        long roster = 0, level = 0, perks = 0, starterPerks = 0, items = 0, injuries = 0, severe = 0, counters = 0, ownInjuries = 0, matchInjuries = 0;
        long offers = 0, affordable = 0, purchases = 0, marketVisits = 0, goldAtMarket = 0;
        var actReached = new int[RunRules.Acts + 1];
        var goldByAct = new long[RunRules.Acts];
        var matchesByAct = new long[RunRules.Acts];
        var actSamples = new int[RunRules.Acts];
        int sinkSamples = 0, allFourAffordable = 0;
        long sinkTotal = 0;

        foreach (var run in runs)
        {
            if (run.Won)
            {
                victories++;
            }
            else if (run.Outcome == RunOutcomeKind.Defeat)
            {
                defeats++;
                if (run.Cause == DefeatCause.NotEnoughPlayers)
                {
                    rosterDefeats++;
                }
                else if (run.Cause == DefeatCause.BossMatchLost)
                {
                    bossDefeats++;
                }
            }

            // ADR 0043 / curva-de-dificultad.md §2.2: dónde se pierde y cuánto cuesta perder. En el
            // género una run perdida cuesta un tercio de una ganada (23 min contra 64), y aquí se mide en
            // nodos y partidos, que es la unidad que el bucle produce.
            if (run.Won)
            {
                wonMatches += run.Matches;
                wonNodes += run.NodesVisited;
            }
            else if (run.Outcome == RunOutcomeKind.Defeat)
            {
                lostMatches += run.Matches;
                lostNodes += run.NodesVisited;
                defeatsByAct[Math.Clamp(run.ActReached, 1, RunRules.Acts) - 1]++;
            }

            rewardsTaken += run.RewardsTaken;
            rewardsDeclined += run.RewardsDeclined;
            deaths += run.Deaths;
            matches += run.Matches;
            goldEarned += run.GoldEarned;
            market += run.GoldSpentMarket;
            clinic += run.GoldSpentClinic;
            enrollment += run.GoldSpentEnrollment;
            slots += run.SlotsBought;
            reroll += run.GoldSpentReroll;
            wages += run.GoldSpentWages;
            left += run.GoldLeft;
            roster += run.FinalRosterSize;
            level += run.AverageLevelTimes100;
            perks += run.PerksOnRoster;
            starterPerks += run.PerksOnStarters;
            items += run.ItemsOnRoster;
            injuries += run.Injuries;
            severe += run.SevereInjuriesSuffered;
            ownInjuries += run.OwnInjuries;
            matchInjuries += run.MatchInjuries;
            counters += run.AccumulatedCounters;
            offers += run.OffersSeen;
            affordable += run.OffersAffordable;
            goldAtMarket += run.GoldAtMarketArrival;
            purchases += run.Purchases;
            marketVisits += run.MarketsVisited;
            if (run.VisitedMarket)
            {
                marketRuns++;
            }

            if (run.BrokeMarketVisits > 0)
            {
                brokeRuns++;
            }

            actReached[Math.Clamp(run.ActReached, 0, RunRules.Acts)]++;

            // "Run completa" = la que llega a jugar el jefe del acto 3, la gane o la pierda. Es la
            // duración que la métrica de §10 describe; promediar sobre las que mueren en el acto 1
            // mediría otra cosa (cuánto se muere), que ya mide la tasa de victoria.
            if (run.BossesBeaten >= RunRules.Acts - 1 && run.MatchesByAct[RunRules.Acts - 1] > 0)
            {
                fullRuns++;
                fullRunMatches += run.Matches;
            }

            for (int act = 0; act < RunRules.Acts; act++)
            {
                if (run.MatchesByAct[act] == 0)
                {
                    continue;
                }

                actSamples[act]++;
                goldByAct[act] += run.GoldEarnedByAct[act];
                matchesByAct[act] += run.MatchesByAct[act];

                int sinks = SinksAffordable(
                    run.GoldEarnedByAct[act], run.MatchesByAct[act], run.WinsByAct[act], run.MarketsByAct[act], economy);
                sinkTotal += sinks;
                sinkSamples++;
                if (sinks >= SinkCount)
                {
                    allFourAffordable++;
                }
            }
        }

        rows.Add(Banded(RunWinRate, 100.0 * victories / runs.Count, RunWinRateMin, RunWinRateMax));
        rows.Add(Banded(
            RosterDefeatShare,
            defeats > 0 ? 100.0 * rosterDefeats / defeats : 0.0,
            null,
            RosterDefeatShareMax));
        rows.Add(Banded(
            MatchesPerFullRun,
            fullRuns > 0 ? (double)fullRunMatches / fullRuns : 0.0,
            MatchesPerFullRunMin,
            MatchesPerFullRunMax));
        rows.Add(Banded(DeathsPerRun, (double)deaths / runs.Count, DeathsPerRunMin, DeathsPerRunMax));
        rows.Add(Banded(
            SinksAffordablePerAct,
            sinkSamples > 0 ? (double)sinkTotal / sinkSamples : 0.0,
            SinksMin,
            SinksMax));
        rows.Add(Banded(
            AffordableShare,
            offers > 0 ? 100.0 * affordable / offers : 0.0,
            AffordableShareMin,
            AffordableShareMax));
        rows.Add(Banded(
            PurchasesPerMarket,
            marketVisits > 0 ? (double)purchases / marketVisits : 0.0,
            PurchasesPerMarketMin,
            PurchasesPerMarketMax));
        rows.Add(Banded(
            LeftoverGoldShare,
            goldEarned > 0 ? 100.0 * left / goldEarned : 0.0,
            null,
            LeftoverGoldShareMax));
        rows.Add(Banded(
            BrokeMarketRunShare,
            100.0 * brokeRuns / runs.Count,
            BrokeMarketRunShareMin,
            BrokeMarketRunShareMax));

        rows.Add(Info("runs", runs.Count));
        rows.Add(Info("defeats", defeats));
        rows.Add(Info("defeatShare_bossMatchLost", defeats > 0 ? 100.0 * bossDefeats / defeats : 0.0));

        // Dónde se pierde (ADR 0043: la mayoría de las derrotas debe caer en el acto 2) y qué cuesta
        // perder frente a ganar.
        for (int act = 0; act < RunRules.Acts; act++)
        {
            rows.Add(Info($"{DefeatShareByActPrefix}{act + 1}", defeats > 0 ? 100.0 * defeatsByAct[act] / defeats : 0.0));
        }

        rows.Add(Info(MatchesPerWonRun, victories > 0 ? (double)wonMatches / victories : 0.0));
        rows.Add(Info(MatchesPerLostRun, defeats > 0 ? (double)lostMatches / defeats : 0.0));
        rows.Add(Info(NodesPerWonRun, victories > 0 ? (double)wonNodes / victories : 0.0));
        rows.Add(Info(NodesPerLostRun, defeats > 0 ? (double)lostNodes / defeats : 0.0));
        rows.Add(Info("lostRunCostShare", wonNodes > 0 && victories > 0 && defeats > 0
            ? 100.0 * ((double)lostNodes / defeats) / ((double)wonNodes / victories)
            : 0.0));
        rows.Add(Info("rewardsTakenPerRun", (double)rewardsTaken / runs.Count));
        rows.Add(Info("rewardsDeclinedPerRun", (double)rewardsDeclined / runs.Count));
        rows.Add(Info("rewardsDeclinedShare", rewardsTaken + rewardsDeclined > 0
            ? 100.0 * rewardsDeclined / (rewardsTaken + rewardsDeclined)
            : 0.0));
        rows.Add(Info("matchesPerRun", (double)matches / runs.Count));
        rows.Add(Info("actsWithAllSinksAffordable", sinkSamples > 0 ? 100.0 * allFourAffordable / sinkSamples : 0.0));
        rows.Add(Info("goldEarnedPerRun", (double)goldEarned / runs.Count));
        rows.Add(Info("goldSpentMarketPerRun", (double)market / runs.Count));
        rows.Add(Info("goldSpentClinicPerRun", (double)clinic / runs.Count));
        rows.Add(Info("goldSpentEnrollmentPerRun", (double)enrollment / runs.Count));
        rows.Add(Info("rosterSlotsBoughtPerRun", (double)slots / runs.Count));
        rows.Add(Info("goldSpentRerollPerRun", (double)reroll / runs.Count));
        rows.Add(Info("goldSpentWagesPerRun", (double)wages / runs.Count));
        rows.Add(Info("goldLeftPerRun", (double)left / runs.Count));
        rows.Add(Info("marketVisitShare", 100.0 * marketRuns / runs.Count));
        rows.Add(Info("goldAtMarketArrival", marketVisits > 0 ? (double)goldAtMarket / marketVisits : 0.0));
        rows.Add(Info("finalRosterSize", (double)roster / runs.Count));
        rows.Add(Info("finalAverageLevel", level / 100.0 / runs.Count));
        rows.Add(Info("perksOnRoster", (double)perks / runs.Count));
        rows.Add(Info("perksOnStarters", (double)starterPerks / runs.Count));
        rows.Add(Info("itemsOnRoster", (double)items / runs.Count));
        rows.Add(Info("accumulatedCountersPerRun", (double)counters / runs.Count));
        rows.Add(Info("injuredAtEnd", (double)injuries / runs.Count));
        rows.Add(Info("severeInjuriesPerRun", (double)severe / runs.Count));
        rows.Add(Info("ownInjuriesPerRun", (double)ownInjuries / runs.Count));
        rows.Add(Info("ownInjuriesPerMatch", matches > 0 ? (double)ownInjuries / matches : 0.0));
        rows.Add(Info("injuriesPerMatchBothTeams", matches > 0 ? (double)matchInjuries / matches : 0.0));

        for (int act = 0; act < RunRules.Acts; act++)
        {
            rows.Add(Info($"reachedAct{act + 1}", 100.0 * ReachedAct(actReached, act + 1) / runs.Count));
            rows.Add(Info($"goldEarnedAct{act + 1}", actSamples[act] > 0 ? (double)goldByAct[act] / actSamples[act] : 0.0));
            rows.Add(Info($"matchesAct{act + 1}", actSamples[act] > 0 ? (double)matchesByAct[act] / actSamples[act] : 0.0));
        }

        return rows;
    }

    /// <summary>
    /// Cuántos sumideros permite pagar el oro ganado en un acto (RF-114k). "Usar un sumidero" no es
    /// tocarlo una vez: es <b>usarlo durante el acto</b>, que es lo que la métrica describe y lo único
    /// que da un número comparable entre sumideros de tamaños muy distintos.
    /// <list type="bullet">
    /// <item><b>Mercado</b>: un perk y un objeto comunes en cada uno de los mercados del acto.</item>
    /// <item><b>Clínica</b>: un tratamiento (RF-094: coste alto, resultado garantizado).</item>
    /// <item><b>Inscripción</b>: el primer hueco de plantilla del acto (ADR 0046). Se cuenta el primero y
    /// no el segundo porque la métrica pregunta qué cabe en <b>un</b> acto, y los dos huecos de una run no
    /// caben en el mismo.</item>
    /// <item><b>Rerolls</b>: repetir la tirada en cada partido ganado del acto, con el coste creciente de
    /// RF-071b contado desde cero dentro del acto.</item>
    /// <item><b>Salarios</b>: un mercenario raro durante todos los partidos del acto (RF-111).</item>
    /// </list>
    /// Se cuentan de más barato a más caro: el número que sale es <b>cuántos caben</b>, no cuántos usó la
    /// política.
    /// </summary>
    public static int SinksAffordable(int goldInAct, int matchesInAct, int winsInAct, int marketsInAct, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(economy);

        int rerolls = 0;
        for (int i = 0; i < winsInAct; i++)
        {
            rerolls += economy.RerollCost(i);
        }

        var costs = new List<int>(SinkCount)
        {
            marketsInAct * (economy.Market.PerkPrice.Common + economy.Market.ItemPrice.Common),
            economy.ClinicCost,
            economy.EnrollmentCost(0),
            rerolls,
            economy.MercenaryWage(Model.Rarity.Uncommon) * matchesInAct,
        };

        costs.Sort();
        int budget = goldInAct, affordable = 0;
        for (int i = 0; i < costs.Count && budget >= costs[i]; i++)
        {
            budget -= costs[i];
            affordable++;
        }

        return affordable;
    }

    private static double LeftoverShare(IReadOnlyList<RunPlayResult> runs)
    {
        long earned = 0, left = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            earned += runs[i].GoldEarned;
            left += runs[i].GoldLeft;
        }

        return earned > 0 ? 100.0 * left / earned : 0.0;
    }

    private static double PurchaseRate(IReadOnlyList<RunPlayResult> runs)
    {
        long visits = 0, purchases = 0;
        for (int i = 0; i < runs.Count; i++)
        {
            visits += runs[i].MarketsVisited;
            purchases += runs[i].Purchases;
        }

        return visits > 0 ? (double)purchases / visits : 0.0;
    }

    private static int ReachedAct(IReadOnlyList<int> actReached, int act)
    {
        int total = 0;
        for (int i = act; i < actReached.Count; i++)
        {
            total += actReached[i];
        }

        return total;
    }

    private static MetricResult Banded(string name, double value, double? min, double? max) =>
        new(name, value, min, max, (min is null || value >= min.Value) && (max is null || value <= max.Value) ? "IN" : "OUT");

    private static MetricResult Info(string name, double value) => new(name, value, null, null, "INFO");
}
