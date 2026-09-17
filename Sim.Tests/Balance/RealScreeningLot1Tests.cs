using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// El primer screening REAL del catálogo (§18, encargo del 19 sep 2026): los 24 perks
/// <c>ReadyForScreening</c> reales, <c>READY_FOR_SCREENING → SCREENING → clasificación automática</c>,
/// sin tuning. Este test es el instrumento de medición del lote — su salida (impresa) es la fuente de la
/// tabla y del informe de coste que pide el encargo, no un test de regresión con valores fijos (los
/// resultados dependen de partidos reales; solo se fija que el pipeline entero corre sin excepciones y
/// que cada perk termina en un estado definido).
/// </summary>
public sealed class RealScreeningLot1Tests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;
    public RealScreeningLot1Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ScreensAllReadyForScreeningPerksAndReportsCost()
    {
        var readyIds = PerkAudit.AuditCatalog(Catalog.Perks.All, Catalog)
            .Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening)
            .Select(e => e.PerkId)
            .ToHashSet(StringComparer.Ordinal);
        var perks = Catalog.Perks.All.Where(p => readyIds.Contains(p.Id)).ToList();

        Assert.Equal(24, perks.Count); // regresión de §17.7 — si esto cambia, la auditoría cambió, no este test.

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = ScreeningRunner.RunBatch(Catalog, perks, seed: 1, checkpointDirectory: null);
        totalStopwatch.Stop();

        _output.WriteLine("perk | final_state | matches | exposure | primary_delta | safety | reason");
        foreach (var r in results.OrderBy(r => r.PerkId, StringComparer.Ordinal))
        {
            string exposureStr = r.ExposureFraction is { } e ? $"{e:P1}" : "-";
            string deltaStr = r.PrimaryDelta is { } d ? $"{d:F4}" : "-";
            string safetyStr = r.SafetyMetricsOut.Count == 0 ? "OK" : string.Join(",", r.SafetyMetricsOut);
            _output.WriteLine($"{r.PerkId} | {r.DisplayState} | {r.Cost.MatchesSimulated} | {exposureStr} | {deltaStr} | {safetyStr} | {r.Reason}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== distribución de estados ===");
        foreach (var g in results.GroupBy(r => r.DisplayState).OrderByDescending(g => g.Count()))
        {
            _output.WriteLine($"{g.Key}: {g.Count()}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== coste ===");
        long totalMatches = results.Sum(r => r.Cost.MatchesSimulated);
        long totalTicks = results.Sum(r => r.Cost.TicksSimulated);
        long totalWallMs = results.Sum(r => r.Cost.WallTimeMs);
        var sortedMatches = results.Select(r => r.Cost.MatchesSimulated).OrderBy(x => x).ToList();
        double median = Percentile(sortedMatches, 0.5);
        double p90 = Percentile(sortedMatches, 0.9);
        int earlyStops = results.Count(r => r.Cost.EarlyStopReason is "exposure_insufficient_after_retry" or "power_insufficient_after_doubling" or "safety_limit" or "contract_failed");

        _output.WriteLine($"total wall time (suma por perk, no reloj real del proceso): {totalWallMs} ms");
        _output.WriteLine($"total matches: {totalMatches}");
        _output.WriteLine($"total ticks: {totalTicks}");
        _output.WriteLine($"matches/perk media: {(double)totalMatches / results.Count:F1}");
        _output.WriteLine($"matches/perk mediana: {median:F1}");
        _output.WriteLine($"matches/perk p90: {p90:F1}");
        _output.WriteLine($"early-stop (cualquier motivo antes del máximo de partidos posible): {earlyStops}/{results.Count} ({100.0 * earlyStops / results.Count:F0}%)");
        _output.WriteLine($"reloj real de este test (dotnet test, incluye overhead de host): {totalStopwatch.ElapsedMilliseconds} ms");

        _output.WriteLine("");
        _output.WriteLine("=== NEEDS_TUNING: baseline registrado, sin buscar valor ===");
        foreach (var r in results.Where(r => r.FinalState == BalanceState.Tuning))
        {
            _output.WriteLine($"{r.PerkId}: baseline={r.TuningInfo!.BaselineValue} dirección={r.TuningInfo.Direction} efecto={r.TuningInfo.ObservedEffect:F4} estrategia={r.TuningInfo.SearchStrategy} target_range={r.TuningInfo.TargetRange ?? "no definido"}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== señales sistémicas registradas (sin declarar regresión, single-seed) ===");
        foreach (var r in results.Where(r => r.SystemicSignals.Count > 0))
        {
            _output.WriteLine($"{r.PerkId}: {string.Join(" | ", r.SystemicSignals)}");
        }

        foreach (var r in results)
        {
            Assert.True(Enum.IsDefined(r.FinalState), $"{r.PerkId} terminó en un estado no definido");
        }
    }

    private static double Percentile(IReadOnlyList<int> sortedAscending, double fraction)
    {
        if (sortedAscending.Count == 0)
        {
            return 0.0;
        }

        double rank = fraction * (sortedAscending.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sortedAscending[lower];
        }

        double weight = rank - lower;
        return (sortedAscending[lower] * (1 - weight)) + (sortedAscending[upper] * weight);
    }
}
