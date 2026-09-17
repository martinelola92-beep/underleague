using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// §31: ¿confiere <c>high_line</c> una ventaja real y consistente? Precondición medible de la pregunta
/// "¿hay evidencia suficiente para llevarlo a revisión de diseño por poder gratis?".
///
/// <para><b>Carencia declarada antes de medir</b>: el proyecto NO tiene definición operativa de "poder
/// gratis". Solo existe como la pregunta 7 de <c>game-design-review/SKILL.md</c> ("todo lo que da algo
/// quita algo… si no hay coste de oportunidad legible"), un juicio cualitativo asignado a revisión de
/// diseño. No se inventa aquí ningún umbral para ello. Lo que sí se mide es la mitad que tiene criterio.</para>
///
/// <para><b>Protocolo fijado antes de ejecutar</b>:
/// <list type="bullet">
/// <item>Muestra: <b>200 partidos por brazo</b> (100 plantillas × 2 direcciones).</item>
/// <item>Condiciones idénticas salvo <c>high_line</c>: mismas plantillas, mismas semillas, mismo generador
///   — lo garantiza <c>PairedBalanceHarness</c>, el mismo de todos los diagnósticos anteriores.</item>
/// <item>Métrica primaria: <b>tasa de victoria emparejada del equipo del portador</b>, expresada en
///   <c>PerkValueRunner.PairedValueMilli</c> (la unidad de valor de perk del proyecto, ADR 0087).</item>
/// <item>Dirección esperada si hubiera ventaja: tasa armada &gt; tasa de control (valor positivo).</item>
/// <item>Criterio estadístico: <c>BalancePowerCheck.HasSufficientPower</c> con el multiplicador 2,0 ya
///   existente (§5.5), con varianza de proporción p(1−p) — exactamente como
///   <c>CampaignBalanceHarness.ToWinRateObservation</c>. No se introduce ningún umbral nuevo.</item>
/// <item>Referencia adicional: la banda ±<c>rowDeviation</c>=7 de la ADR 0087, anotando que se midió con el
///   harness de campaña y no con este, así que vale como orientación y no como corte.</item>
/// <item>Secundarias, todas ya definidas: las 7 métricas obligatorias de RT-056, más goles y tiros
///   encajados y pases en profundidad del rival (los campos de <c>MatchReport</c> usados en §30).</item>
/// </list></para>
/// </summary>
public sealed class HighLineFreePowerTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Rosters = 100; // × 2 direcciones = 200 partidos por brazo
    private const ulong Seed = 1;

    private readonly ITestOutputHelper _output;
    public HighLineFreePowerTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DoesHighLineConferAMeasurableAdvantageOverItsPairedControl()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "high_line");
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, Rosters, Seed);

        // El portador juega en casa en las direcciones pares y fuera en las impares (PairedBalanceHarness
        // añade siempre dirección 0 y luego 1 por plantilla, en los dos brazos por igual).
        var armedWins = new List<double>();
        var controlWins = new List<double>();
        for (int i = 0; i < run.ArmedMatches.Count; i++)
        {
            int carrierTeam = i % 2 == 1 ? 1 : 0;
            armedWins.Add(run.ArmedMatches[i].Winner == carrierTeam ? 1.0 : 0.0);
            controlWins.Add(run.ControlMatches[i].Winner == carrierTeam ? 1.0 : 0.0);
        }

        int matches = armedWins.Count;
        int armedTotal = (int)armedWins.Sum();
        int controlTotal = (int)controlWins.Sum();
        double armedRate = 100.0 * armedTotal / matches;
        double controlRate = 100.0 * controlTotal / matches;
        int pairedValue = Underleague.Balance.PerkValueRow.PairedValueMilli(armedTotal, controlTotal, matches);

        // Criterio existente: varianza de proporción p(1−p) y el power-check de §5.5, sin umbral nuevo.
        double pArmed = (double)armedTotal / matches;
        double pControl = (double)controlTotal / matches;
        double delta = pArmed - pControl;
        double varianceArmed = pArmed * (1 - pArmed);
        double varianceControl = pControl * (1 - pControl);
        double standardError = BalancePowerCheck.StandardError(varianceArmed, matches, varianceControl, matches);
        bool distinguishable = BalancePowerCheck.HasSufficientPower(delta, varianceArmed, matches, varianceControl, matches);

        _output.WriteLine($"high_line | {matches} partidos por brazo | mismas plantillas y semillas en los dos");
        _output.WriteLine("");
        _output.WriteLine($"  victorias armado  : {armedTotal}/{matches} ({armedRate:F2}%)");
        _output.WriteLine($"  victorias control : {controlTotal}/{matches} ({controlRate:F2}%)");
        _output.WriteLine($"  delta de tasa     : {armedRate - controlRate:+0.00;-0.00} puntos");
        _output.WriteLine($"  PairedValueMilli  : {pairedValue:+#;-#;0}  (unidad de valor de perk, ADR 0087)");
        _output.WriteLine($"  error estándar    : {standardError:F4} (proporción); 2×SE = {2 * standardError:F4}");
        _output.WriteLine($"  |delta| = {Math.Abs(delta):F4}");
        _output.WriteLine($"  ¿distinguible del ruido según §5.5 (2×SE)? {(distinguishable ? "SÍ" : "NO")}");
        _output.WriteLine($"  referencia ADR 0087 (±rowDeviation=7, medida con el harness de campaña): " +
            $"{(Math.Abs(pairedValue) > 7 ? "fuera de la banda" : "dentro de la banda")}");

        _output.WriteLine("");
        _output.WriteLine("=== secundarias ya definidas: las 7 obligatorias de RT-056 (brazo armado) ===");
        var mandatory = MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>())
            .Where(m => m.RangeMin is not null && m.RangeMax is not null)
            .ToList();
        var controlMandatory = MatchMetrics.Compute(run.ControlMatches, Array.Empty<MetricPairing>())
            .Where(m => m.RangeMin is not null && m.RangeMax is not null)
            .ToList();
        foreach (var m in mandatory)
        {
            var c = controlMandatory.Single(x => x.Name == m.Name);
            _output.WriteLine($"  {m.Name,-22} armado {m.Value,8:F3} | control {c.Value,8:F3} | delta {m.Value - c.Value,+7:F3} | {m.Status}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== el COSTE declarado, al mismo tamaño de muestra, con power-check ===");
        ReportCostSide(run, matches);

        Assert.Equal(200, matches); // la muestra fijada antes de medir: 100 plantillas × 2 direcciones por brazo
    }

    /// <summary>
    /// Los indicadores de coste de §30 (goles encajados) recalculados a 200 partidos por brazo y pasados
    /// por el mismo criterio de §5.5, para saber si el indicio de §30 (n=40, sin power-check) aguanta.
    /// </summary>
    private void ReportCostSide(PairedBalanceHarness.PairedResult run, int matches)
    {
        var armedConceded = new List<double>();
        var controlConceded = new List<double>();
        for (int i = 0; i < matches; i++)
        {
            int carrierTeam = i % 2 == 1 ? 1 : 0;
            armedConceded.Add(carrierTeam == 0 ? run.ArmedMatches[i].AwayGoals : run.ArmedMatches[i].HomeGoals);
            controlConceded.Add(carrierTeam == 0 ? run.ControlMatches[i].AwayGoals : run.ControlMatches[i].HomeGoals);
        }

        double armedMean = armedConceded.Average();
        double controlMean = controlConceded.Average();
        double delta = armedMean - controlMean;
        double varArmed = BalancePowerCheck.SampleVariance(armedConceded);
        double varControl = BalancePowerCheck.SampleVariance(controlConceded);
        bool distinguishable = BalancePowerCheck.HasSufficientPower(delta, varArmed, matches, varControl, matches);
        double standardError = BalancePowerCheck.StandardError(varArmed, matches, varControl, matches);

        _output.WriteLine($"  goles encajados/partido: armado {armedMean:F3} | control {controlMean:F3} | delta {delta:+0.000;-0.000}");
        _output.WriteLine($"  error estándar {standardError:F4}; 2×SE = {2 * standardError:F4}; |delta| = {Math.Abs(delta):F4}");
        _output.WriteLine($"  ¿distinguible del ruido según §5.5? {(distinguishable ? "SÍ" : "NO")}");
    }
}
