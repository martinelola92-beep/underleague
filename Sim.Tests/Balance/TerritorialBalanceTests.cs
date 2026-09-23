using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// §32: valida la métrica territorial contra perks reales de desplazamiento conocido, y para.
///
/// <para>El catálogo ofrece un banco de pruebas mejor que cualquier control inventado: tres perks de
/// <c>shiftHome</c> con valores <b>+2, −1 y −2</b>. Si la métrica mide lo que dice medir, debe darles
/// signo correcto Y ordenarlos. Se añade un cuarto perk sin geometría, que debe quedarse en ~0.</para>
///
/// <para><b>Predicción escrita antes de medir</b>: <c>high_line</c>(+2) positivo; <c>shadow</c>(−1)
/// negativo pequeño; <c>deep_pivot</c>(−2) negativo mayor; <c>iron_price</c> (sin geometría) ≈0. Orden
/// esperado: high_line &gt; shadow &gt; deep_pivot.</para>
///
/// <para>No cambia ningún veredicto, umbral, perk ni protocolo: la métrica no la consulta nadie.</para>
/// </summary>
public sealed class TerritorialBalanceTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Rosters = 100; // 200 partidos por brazo

    private readonly ITestOutputHelper _output;
    public TerritorialBalanceTests(ITestOutputHelper output) => _output = output;

    // El harness emparejado añade siempre dirección 0 (portador en casa) y luego 1 (fuera) por plantilla.
    private static int CarrierTeamAt(int index) => index % 2 == 1 ? 1 : 0;

    [Fact]
    public void TheMetricIsSymmetricByConstruction()
    {
        // Un partido con el balón repartido a partes iguales entre los dos tercios extremos da 0 para los
        // dos equipos; y un partido volcado da valores opuestos. Sin simular nada.
        var balanced = new MatchSummary("home", "away", 0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0,
            BallThird0: 100, BallThird1: 100, BallThird2: 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0.0, TerritorialBalance.ForMatch(balanced, 0), 6);
        Assert.Equal(0.0, TerritorialBalance.ForMatch(balanced, 1), 6);

        var pushedRight = balanced with { BallThird0 = 50, BallThird2 = 150 };
        double forTeam0 = TerritorialBalance.ForMatch(pushedRight, 0);
        double forTeam1 = TerritorialBalance.ForMatch(pushedRight, 1);
        Assert.True(forTeam0 > 0, "el equipo 0 ataca hacia el tercio 2: volcarlo ahí es territorio a favor");
        Assert.Equal(-forTeam0, forTeam1, 6);
    }

    [Theory]
    [InlineData("high_line", 2)]     // shiftHome +2, Defensa
    [InlineData("shadow", -1)]       // shiftHome -1, Medio
    [InlineData("deep_pivot", -2)]   // shiftHome -2, Medio
    [InlineData("iron_price", 0)]    // sin geometría: control
    public void MeasureRealPerksOfKnownDisplacement(string perkId, int shift)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, Rosters, seed: 1);

        double armed = TerritorialBalance.ForBatch(run.ArmedMatches, CarrierTeamAt);
        double control = TerritorialBalance.ForBatch(run.ControlMatches, CarrierTeamAt);

        // La métrica que se usa hoy, para comparar qué ve cada una.
        var armedMax = MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>()).Single(m => m.Name == MatchMetrics.BallThirdMaxShare);
        var controlMax = MatchMetrics.Compute(run.ControlMatches, Array.Empty<MetricPairing>()).Single(m => m.Name == MatchMetrics.BallThirdMaxShare);

        _output.WriteLine(
            $"{perkId,-12} shiftHome {shift,+2} | balance territorial armado {armed,7:F3} control {control,7:F3} " +
            $"delta {armed - control,+7:F3} | ballThirdMaxShare delta {armedMax.Value - controlMax.Value,+7:F3} | {run.ArmedMatches.Count} partidos/brazo");
    }
}
