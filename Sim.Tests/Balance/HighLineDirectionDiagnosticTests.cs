using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Diagnóstico mínimo del <c>DESIGN_ESCALATION</c> de <c>high_line</c> (Δ −2,2095 en
/// <c>ballThirdMaxShare</c>, §23.5). Pregunta única: ¿el balón se reparte MÁS uniformemente (el perk
/// funciona y la métrica de máximo baja por eso) o se desplaza HACIA ATRÁS (el mecanismo hace lo
/// contrario de su diseño)?
///
/// <para>Cero instrumentación nueva: <c>BallThird0/1/2</c> ya están en <c>MatchSummary</c>. No toca el
/// perk, ni su valor, ni ningún umbral, ni la regla de §18.2.</para>
/// </summary>
public sealed class HighLineDirectionDiagnosticTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public HighLineDirectionDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void WhereDoesTheBallActuallyGoWithHighLineArmed()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "high_line");
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, rosters: 20, seed: 1);

        static (double Own, double Middle, double Opposing, double Max) Distribution(IReadOnlyList<MatchSummary> matches)
        {
            double t0 = matches.Sum(m => (double)m.BallThird0);
            double t1 = matches.Sum(m => (double)m.BallThird1);
            double t2 = matches.Sum(m => (double)m.BallThird2);
            double total = Math.Max(1, t0 + t1 + t2);
            return (100.0 * t0 / total, 100.0 * t1 / total, 100.0 * t2 / total,
                100.0 * Math.Max(t0, Math.Max(t1, t2)) / total);
        }

        var armed = Distribution(run.ArmedMatches);
        var control = Distribution(run.ControlMatches);

        _output.WriteLine($"high_line: shiftHome(+2) sobre un Defensa | {run.ArmedMatches.Count} partidos por brazo");
        _output.WriteLine("brazo   | tercio 0 | tercio 1 | tercio 2 | máximo (= ballThirdMaxShare)");
        _output.WriteLine($"armado  | {armed.Own,7:F2}% | {armed.Middle,7:F2}% | {armed.Opposing,7:F2}% | {armed.Max,7:F2}%");
        _output.WriteLine($"control | {control.Own,7:F2}% | {control.Middle,7:F2}% | {control.Opposing,7:F2}% | {control.Max,7:F2}%");
        _output.WriteLine($"delta   | {armed.Own - control.Own,+7:F2}  | {armed.Middle - control.Middle,+7:F2}  | {armed.Opposing - control.Opposing,+7:F2}  | {armed.Max - control.Max,+7:F2}");
        _output.WriteLine("");
        _output.WriteLine("Lectura: si el tercio central SUBE y el máximo BAJA, el reparto se aplana — el perk empuja");
        _output.WriteLine("la línea arriba como dice su diseño y la métrica de máximo baja por eso, no a pesar de eso.");
        _output.WriteLine("Si en cambio sube el tercio propio, el mecanismo estaría haciendo lo contrario de lo diseñado.");
    }
}
