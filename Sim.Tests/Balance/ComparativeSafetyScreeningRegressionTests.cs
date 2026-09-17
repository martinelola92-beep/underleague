using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Regresión de extremo a extremo (con partidos reales, no sintéticos) del chequeo de seguridad
/// comparativo (§19 punto E del encargo del 19 sep 2026): confirma que el hallazgo de §19.3/§19.4
/// (armado y control idénticos, pero un `SAFETY_LIMIT` absoluto los marcaba como violación) ya no ocurre
/// con <c>ScreeningRunner</c> tal cual se ejecuta hoy.
/// </summary>
public sealed class ComparativeSafetyScreeningRegressionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;
    public ComparativeSafetyScreeningRegressionTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("blood_scent")]
    [InlineData("bloodhound")]
    public void PerksWhoseArmedAndControlShareTheSameBaselineNoLongerTriggerSafetyLimit(string perkId)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var result = ScreeningRunner.RunPerk(Catalog, perk, seed: 1);

        _output.WriteLine($"{perkId} -> {result.DisplayState}: {result.Reason}");
        foreach (var note in result.Notes)
        {
            _output.WriteLine($"  note: {note}");
        }

        Assert.NotEqual(BalanceState.SafetyLimit, result.FinalState);
    }
}
