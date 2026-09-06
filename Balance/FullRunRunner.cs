using System.Diagnostics;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;

namespace Underleague.Balance;

/// <summary>Resultado del modo <c>--full-runs</c>: las runs jugadas por las tres doctrinas, sus métricas y el tiempo.</summary>
public sealed record FullRunResult(
    IReadOnlyList<RunPlayResult> Runs,
    IReadOnlyList<RunPlayResult> Marketless,
    IReadOnlyList<MetricResult> Metrics,
    int TotalMatches,
    TimeSpan Elapsed);


/// <summary>
/// Modo <c>--full-runs N</c> de <c>/Balance</c> (fase2-diseno.md §10, ADR 0037): juega N runs completas
/// con <b>cada una de las tres doctrinas de compra</b> sobre las mismas semillas y vuelca
/// <c>runs.csv</c>. Aquí solo vive el pegamento —qué club, qué semilla, cuántas runs y el cronómetro—;
/// jugar y medir es de <c>/Sim</c>, para que la puerta de <c>Sim.Tests</c> y este modo sean el mismo
/// código.
/// </summary>
public static class FullRunRunner
{
    /// <summary>Id del club con el que <c>/Balance</c> juega las runs (no hay <c>data/clubs/</c> todavía).</summary>
    public const string ClubId = "balance_club";

    /// <summary>Calidad de la plantilla inicial generada (RF-005, misma que la referencia de fase 1).</summary>
    public const int StartingQuality = 50;

    /// <summary>Las tres doctrinas, en el orden en que se imprimen.</summary>
    public static IReadOnlyList<PurchaseDoctrine> Doctrines { get; } = new[]
    {
        PurchaseDoctrine.Contextual, PurchaseDoctrine.Spender, PurchaseDoctrine.Saver,
    };

    /// <summary>
    /// Juega <paramref name="runs"/> runs completas <b>por doctrina</b>, repartidas por igual entre las
    /// razas de lanzamiento (la exigencia es de la puerta, no del club: ver Y-9) y sobre las mismas
    /// semillas en las tres, que es lo que hace comparable la tasa de victoria (ADR 0037). Cada run usa
    /// la semilla <c>seed + índice</c>, así que el lote es reproducible y ampliable sin recalcular el
    /// anterior.
    /// </summary>
    public static FullRunResult Run(
        Catalog catalog,
        IReadOnlyDictionary<string, string> dataFiles,
        ulong seed,
        int runs,
        bool ignoreScouting = false,
        int? riskAversion = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dataFiles);
        ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);

        var standard = StandardRunSystems.FromJson(dataFiles);
        var bosses = BossCatalog.FromJson(dataFiles);
        var races = LaunchRaces(catalog);

        var stopwatch = Stopwatch.StartNew();
        var all = new List<RunPlayResult>(runs * Doctrines.Count);
        var byDoctrine = new Dictionary<PurchaseDoctrine, List<RunPlayResult>>();
        int matches = 0;

        foreach (var doctrine in Doctrines)
        {
            var options = RunPolicyOptions.For(doctrine) with { HeedsLethalScouting = !ignoreScouting };
            if (riskAversion is { } aversion)
            {
                options = options with { DeathCostPercent = aversion };
            }
            var rows = new List<RunPlayResult>(runs);
            for (int i = 0; i < runs; i++)
            {
                var setup = SetupFor(races[i % races.Count], standard, dataFiles);
                var result = RunPolicy.Play(setup, seed + (ulong)i, catalog, standard, bosses, options);
                rows.Add(result);
                matches += result.Matches;
            }

            byDoctrine[doctrine] = rows;
            all.AddRange(rows);
        }

        // ADR 0055: la medida de control. La MISMA política contextual sobre las MISMAS semillas, jugando
        // igual de bien todo lo demás, pero esquivando los mercados siempre que el mapa se lo permite. Su
        // tasa de victoria es la respuesta a "¿se puede ganar sin comprar?".
        var marketlessOptions = RunPolicyOptions.For(PurchaseDoctrine.Contextual) with
        {
            HeedsLethalScouting = !ignoreScouting,
            AvoidsMarkets = true,
        };
        if (riskAversion is { } marketlessAversion)
        {
            marketlessOptions = marketlessOptions with { DeathCostPercent = marketlessAversion };
        }

        var marketless = new List<RunPlayResult>(runs);
        for (int i = 0; i < runs; i++)
        {
            var setup = SetupFor(races[i % races.Count], standard, dataFiles);
            var result = RunPolicy.Play(setup, seed + (ulong)i, catalog, standard, bosses, marketlessOptions);
            marketless.Add(result);
            matches += result.Matches;
        }

        stopwatch.Stop();
        var metrics = FullRunMetrics.Compute(
            byDoctrine[PurchaseDoctrine.Contextual],
            byDoctrine[PurchaseDoctrine.Spender],
            byDoctrine[PurchaseDoctrine.Saver],
            standard.Economy);
        metrics.AddRange(FullRunMetrics.Marketless(marketless, byDoctrine[PurchaseDoctrine.Contextual]));

        return new FullRunResult(all, marketless, metrics, matches, stopwatch.Elapsed);
    }

    /// <summary>Configuración de run de <c>/Balance</c>: oro y nodos por acto salen de <c>/data</c>.</summary>
    public static RunSetup SetupFor(
        Race race,
        StandardRunSystems standard,
        IReadOnlyDictionary<string, string> dataFiles)
    {
        ArgumentNullException.ThrowIfNull(standard);

        // El oro de partida, los nodos por acto y los rivales salen de /data a través de los sistemas
        // (StandardRunSystems.NewRunSetup): un RunSetup montado a mano se queda con 0 de oro y llega al
        // primer mercado sin poder comprar nada.
        return standard.NewRunSetup(ClubId, race, dataFiles) with { GeneratedQuality = StartingQuality };
    }

    /// <summary>Razas jugables al lanzamiento, en orden estable.</summary>
    public static List<Race> LaunchRaces(Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var races = new List<Race>();
        for (int i = 0; i < catalog.Races.Count; i++)
        {
            if (catalog.Races[i].Launch)
            {
                races.Add(catalog.Races[i].Id);
            }
        }

        races.Sort();
        return races;
    }
}
