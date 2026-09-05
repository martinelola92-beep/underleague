using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Map;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Puerta de <b>run completa</b> de la fase 2 (fase2-diseno.md §10, ADR 0037): juega runs enteras con
/// las tres doctrinas de compra y comprueba el bucle de run de punta a punta —mapa, economía, mercado,
/// clínica, recompensas, jefes y las dos vías de derrota— sobre las mismas semillas.
///
/// <para><b>Qué afirma y qué no.</b> La métrica principal de la fase sigue siendo la curva de puertas de
/// la ADR 0033 (<see cref="BossGateTests"/>). Esta puerta afirma lo que hoy se cumple y es estable con
/// esta muestra: la duración de una run completa, que las derrotas por plantilla no dominan, RF-114k
/// (dos o tres sumideros por acto, nunca los cuatro), las compras por visita al mercado, y que las tres
/// doctrinas son de verdad tres. Las bandas de diseño que <b>no</b> se cumplen —tasa de victoria de la
/// run, ventaja de la contextual, muertes por run, fracción asequible del surtido, oro sobrante— están
/// medidas, explicadas y con causa identificada en <c>docs/balance/fase2-resultados.md</c>; aquí
/// aparecen como <b>cotas de no regresión</b> alrededor de lo medido, para que un cambio las mueva y se
/// note, sin afirmar que el diseño está donde debe.</para>
///
/// <para><b>Muestra</b>: semilla 1, <see cref="Runs"/> runs por doctrina, las cinco razas de lanzamiento
/// repartidas por igual. Unos 1.900 partidos, 11 s. Reproducible con
/// <c>dotnet run --project Balance -c Release -- --full-runs 60 --seed 1</c>.</para>
/// </summary>
[Trait("Category", "Gate")]
public sealed class FullRunGateTests
{
    /// <summary>Runs por doctrina. Con 60 la tasa de victoria tiene una desviación de ~4,5 puntos.</summary>
    private const int Runs = 60;

    /// <summary>Semilla del lote.</summary>
    private const ulong Seed = 1;

    private static readonly Lazy<Measured> Result = new(Play);

    private sealed record Measured(
        IReadOnlyDictionary<PurchaseDoctrine, IReadOnlyList<RunPlayResult>> ByDoctrine,
        IReadOnlyList<MetricResult> Metrics);

    /// <summary>
    /// Una run completa se juega de principio a fin y termina en victoria o en una de las dos derrotas
    /// de RF-002b. Es el criterio de salida de la fase 2 en su forma más básica: el bucle cierra.
    /// </summary>
    [Fact]
    public void EveryRunEndsInVictoryOrOneOfTheTwoDefeats()
    {
        foreach (var (_, runs) in Result.Value.ByDoctrine)
        {
            foreach (var run in runs)
            {
                Assert.NotEqual(RunOutcomeKind.InProgress, run.Outcome);
                if (run.Outcome == RunOutcomeKind.Defeat)
                {
                    Assert.Contains(run.Cause, new[] { DefeatCause.BossMatchLost, DefeatCause.NotEnoughPlayers });
                }
                else
                {
                    Assert.Equal(RunRules.Acts, run.BossesBeaten);
                }
            }
        }
    }

    /// <summary>
    /// §10: una run que llega hasta el jefe final dura 18-22 partidos. Con los nodos por acto de
    /// <c>data/map/map.json</c> (11, 12 y 12, D-2/D-10) son exactamente 20, y no depende del azar: el
    /// tope del 60% de RF-003b se cumple sobre el peor camino (W-3).
    /// </summary>
    [Fact]
    public void AFullRunLastsBetween18And22Matches() => AssertIn(FullRunMetrics.MatchesPerFullRun);

    /// <summary>§10: bajar de 5 jugadores no puede ser la causa de más de un tercio de las derrotas.</summary>
    [Fact]
    public void RunningOutOfPlayersIsNotTheUsualWayToLose() => AssertIn(FullRunMetrics.RosterDefeatShare);

    /// <summary>
    /// RF-114k: el oro medio de un acto permite usar dos o tres sumideros, <b>nunca los cuatro</b>. Los
    /// cuatro vivos en fase 2 son mercado, clínica, rerolls y salarios de mercenarios; el coste de "usar"
    /// cada uno durante un acto lo define <see cref="FullRunMetrics.SinksAffordable"/>.
    /// </summary>
    [Fact]
    public void TheGoldOfAnActPaysTwoOrThreeSinksAndNeverFour()
    {
        AssertIn(FullRunMetrics.SinksAffordablePerAct);
        Assert.Equal(0.0, Value("actsWithAllFourSinksAffordable"));
    }

    /// <summary>ADR 0037: se compra poco y se piensa — una o dos compras por visita al mercado.</summary>
    [Fact]
    public void TheMarketIsVisitedForOneOrTwoPurchases() => AssertIn(FullRunMetrics.PurchasesPerMarket);

    /// <summary>
    /// ADR 0037: las tres doctrinas tienen que ser tres. Si compraran lo mismo, la comparación entre
    /// ellas —que es <b>la</b> forma de medir si la decisión de comprar existe— no diría nada.
    /// </summary>
    [Fact]
    public void TheThreeDoctrinesBuyDifferently()
    {
        double contextual = PurchasesPerMarket(PurchaseDoctrine.Contextual);
        double spender = PurchasesPerMarket(PurchaseDoctrine.Spender);
        double saver = PurchasesPerMarket(PurchaseDoctrine.Saver);

        Assert.True(spender > saver, $"la gastadora compra {spender:F2} por mercado y la ahorradora {saver:F2}");
        Assert.True(contextual > saver, $"la contextual compra {contextual:F2} por mercado y la ahorradora {saver:F2}");
        Assert.True(
            LeftoverShare(PurchaseDoctrine.Saver) > LeftoverShare(PurchaseDoctrine.Contextual),
            "la ahorradora debería terminar la run con más oro sin gastar que la contextual");
    }

    /// <summary>
    /// Cotas de <b>no regresión</b> de las bandas de diseño que hoy no se cumplen (fase2-diseno.md §19):
    /// son deliberadamente anchas alrededor de lo medido, para que un cambio de economía o de catálogo
    /// las mueva y se vea, sin afirmar que el diseño está donde debe.
    ///
    /// <para>Las cotas de <c>affordableShareAtMarket</c> y <c>brokeMarketRunShare</c> se ensancharon con
    /// la escala de oro de la ADR 0044: con precios entre 4 y 47 y un acto que gana 23, "llegar a un
    /// mercado sin poder pagar nada" pasa del 47% al 78% de las runs <b>por aritmética entera</b> —por
    /// visita sigue siendo el 15%— y las dos métricas siguen oponiéndose entre sí (Z-K). Medido y
    /// explicado en §19; la cota solo sirve para que no se mueva sin querer.</para>
    /// </summary>
    [Fact]
    public void TheMetricsThatDoNotMeetTheirDesignBandStayWhereTheyWereMeasured()
    {
        AssertBetween(FullRunMetrics.RunWinRate, 5.0, 40.0);
        AssertBetween(FullRunMetrics.AffordableShare, 25.0, 62.0);
        AssertBetween(FullRunMetrics.LeftoverGoldShare, 5.0, 32.0);
        AssertBetween(FullRunMetrics.BrokeMarketRunShare, 20.0, 88.0);
        AssertBetween(FullRunMetrics.DeathsPerRun, 0.0, 0.5);
    }

    /// <summary>
    /// RT-013: la misma semilla y la misma doctrina producen exactamente la misma run. Es lo que hace
    /// que un cambio de economía se lea en la métrica y no en el dado (fase2-diseno.md §10).
    /// </summary>
    [Fact]
    public void TheSameSeedAndDoctrineProduceTheSameRun()
    {
        var catalog = TestData.LoadCatalog();
        var files = TestData.LoadAllFiles();
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var setup = SetupFor(Race.Orc, standard, files);
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        var first = RunPolicy.Play(setup, 4242, catalog, standard, bosses, options);
        var second = RunPolicy.Play(setup, 4242, catalog, standard, bosses, options);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Cause, second.Cause);
        Assert.Equal(first.Matches, second.Matches);
        Assert.Equal(first.GoldEarned, second.GoldEarned);
        Assert.Equal(first.GoldSpentMarket, second.GoldSpentMarket);
        Assert.Equal(first.PerksOnStarters, second.PerksOnStarters);
        Assert.Equal(first.Purchases, second.Purchases);
    }

    /// <summary>
    /// D-2/D-10: los nodos por acto salen de <c>data/map/map.json</c> y una run recorre entre 30 y 36
    /// (RF-003b), con no más del 60% de partidos en el peor camino.
    /// </summary>
    [Fact]
    public void TheMapMatchesTheNodeBudgetOfRf003b()
    {
        var map = MapLoader.FromJson(TestData.LoadAllFiles());
        Assert.InRange(map.TotalNodes, 30, 36);

        int worstCaseMatches = 0;
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var generated = MapGenerator.Generate(1, act, new MapOptions(map.Of(act)));
            worstCaseMatches += MapInvariants.WorstCaseMatches(generated);
        }

        Assert.InRange(worstCaseMatches, 18, 22);
        Assert.True(
            worstCaseMatches * 100 / map.TotalNodes <= MapGenerator.MaxMatchPercent,
            $"{worstCaseMatches} partidos de {map.TotalNodes} nodos superan el {MapGenerator.MaxMatchPercent}% de RF-003b");
    }

    // ------------------------------------------------------------------ interno

    private static double PurchasesPerMarket(PurchaseDoctrine doctrine)
    {
        var runs = Result.Value.ByDoctrine[doctrine];
        double markets = runs.Sum(r => r.MarketsVisited);
        return markets > 0 ? runs.Sum(r => r.Purchases) / markets : 0.0;
    }

    private static double LeftoverShare(PurchaseDoctrine doctrine)
    {
        var runs = Result.Value.ByDoctrine[doctrine];
        double earned = runs.Sum(r => (double)r.GoldEarned);
        return earned > 0 ? 100.0 * runs.Sum(r => (double)r.GoldLeft) / earned : 0.0;
    }

    private static double Value(string metric) =>
        Result.Value.Metrics.Single(m => m.Name == metric).Value;

    private static void AssertIn(string metric)
    {
        var row = Result.Value.Metrics.Single(m => m.Name == metric);
        Assert.True(
            row.Status == "IN",
            $"{row.Name} = {row.Value:F2}, fuera de {row.RangeMin?.ToString("F2") ?? "-"}..{row.RangeMax?.ToString("F2") ?? "-"}");
    }

    private static void AssertBetween(string metric, double min, double max)
    {
        double value = Value(metric);
        Assert.True(value >= min && value <= max, $"{metric} = {value:F2}, fuera de la cota de no regresión {min}..{max}");
    }

    private static RunSetup SetupFor(Race race, StandardRunSystems standard, IReadOnlyDictionary<string, string> files) =>
        standard.NewRunSetup("gate_club", race, files) with { GeneratedQuality = 50 };

    private static Measured Play()
    {
        var catalog = TestData.LoadCatalog();
        var files = TestData.LoadAllFiles();
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);

        var races = catalog.Races.Where(r => r.Launch).Select(r => r.Id).OrderBy(r => r).ToList();
        var byDoctrine = new Dictionary<PurchaseDoctrine, IReadOnlyList<RunPlayResult>>();

        foreach (var doctrine in new[] { PurchaseDoctrine.Contextual, PurchaseDoctrine.Spender, PurchaseDoctrine.Saver })
        {
            var options = RunPolicyOptions.For(doctrine);
            var rows = new List<RunPlayResult>(Runs);
            for (int i = 0; i < Runs; i++)
            {
                var setup = SetupFor(races[i % races.Count], standard, files);
                rows.Add(RunPolicy.Play(setup, Seed + (ulong)i, catalog, standard, bosses, options));
            }

            byDoctrine[doctrine] = rows;
        }

        var metrics = FullRunMetrics.Compute(
            byDoctrine[PurchaseDoctrine.Contextual],
            byDoctrine[PurchaseDoctrine.Spender],
            byDoctrine[PurchaseDoctrine.Saver],
            standard.Economy);

        return new Measured(byDoctrine, metrics);
    }
}
