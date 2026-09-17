using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>Comprobación rápida antes del lote real de 24 (§18): un perk de cada forma esperada.</summary>
public sealed class ScreeningRunnerSmokeTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;
    public ScreeningRunnerSmokeTests(ITestOutputHelper output) => _output = output;

    private void Print(ScreeningResult r)
    {
        _output.WriteLine($"{r.PerkId} -> {r.DisplayState}");
        _output.WriteLine($"  reason: {r.Reason}");
        _output.WriteLine($"  exposure={r.ExposureFraction:P1} delta={r.PrimaryDelta} power={r.PowerSufficient}");
        _output.WriteLine($"  safetyOut=[{string.Join(",", r.SafetyMetricsOut)}]");
        _output.WriteLine($"  systemic=[{string.Join(" | ", r.SystemicSignals)}]");
        _output.WriteLine($"  tuning={r.TuningInfo}");
        _output.WriteLine($"  cost: {r.Cost}");
        foreach (var n in r.Notes)
        {
            _output.WriteLine($"  note: {n}");
        }
        _output.WriteLine("");
    }

    [Theory]
    [InlineData("own_third_anchor")] // tacklesPerMatch, exposición previsiblemente alta (§16.2: 100%)
    [InlineData("iron_gate")]        // injuriesPerMatch, con Limit — caso de la nota de exposición confundida
    [InlineData("blood_scent")]      // sin parámetro numérico (bespoke) -> debería ir a Validating/SCREENING_PASS
    [InlineData("cannon")]           // shotsPerMatch
    public void RunsWithoutThrowingAndProducesADefinedState(string perkId)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var result = ScreeningRunner.RunPerk(Catalog, perk, seed: 1);
        Print(result);
        Assert.True(Enum.IsDefined(result.FinalState));
    }
}
