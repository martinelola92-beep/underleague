using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// §33: diagnóstico DESCRIPTIVO de los perks de geometría con <see cref="TerritorialBalance"/>.
///
/// <para><b>Qué NO es</b>: no aplica ningún umbral nuevo, no produce veredictos pass/fail, no cambia el
/// protocolo, no toca <c>/data</c> ni ningún perk, y no adopta la métrica territorial como criterio —
/// sigue sin banda y sin que la consulte nadie (§32). Es un instrumento de lectura: <i>qué hace
/// realmente cada uno de estos perks en una partida</i>.</para>
///
/// <para>El conjunto de perks NO se elige a mano: sale de <see cref="PerkBalanceClassifier.Classify"/>
/// sobre el catálogo real, filtrando <see cref="PerkBalanceCategory.Geometry"/>. Así el diagnóstico
/// cubre exactamente lo que el clasificador considera geometría, ni más ni menos.</para>
/// </summary>
public sealed class GeometryPerkDiagnosticTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Rosters = 100; // × 2 direcciones = 200 partidos por brazo
    private const ulong Seed = 1;

    private readonly ITestOutputHelper _output;
    public GeometryPerkDiagnosticTests(ITestOutputHelper output) => _output = output;

    // El harness emparejado añade siempre dirección 0 (portador en casa) y luego 1 (fuera) por plantilla.
    private static int CarrierTeamAt(int index) => index % 2 == 1 ? 1 : 0;

    private static IReadOnlyList<PerkDefinition> GeometryPerks() =>
        Catalog.Perks.All
            .Where(p => PerkBalanceClassifier.Classify(p).Category == PerkBalanceCategory.Geometry)
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();

    /// <summary>El conjunto, tal como lo define el clasificador — y por qué cae fuera cada vecino.</summary>
    [Fact]
    public void WhichPerksCountAsGeometry()
    {
        var geometry = GeometryPerks();
        _output.WriteLine($"Geometry según PerkBalanceClassifier: {geometry.Count} perks");
        foreach (var p in geometry)
        {
            var c = PerkBalanceClassifier.Classify(p);
            var e = PerkBalanceClassifier.GetPrimaryEffect(p);
            _output.WriteLine(
                $"  {p.Id,-20} efecto primario {e.Type,-18} target {e.Target,-8} value {e.Value,+3} " +
                $"| métrica primaria {c.PrimaryMetric} ({c.Readiness}) | multiTarget={c.NeedsMultiTargetHarness}");
        }

        _output.WriteLine("");
        _output.WriteLine("Vecinos que TOCAN geometría pero el clasificador NO cuenta como Geometry:");
        foreach (var p in Catalog.Perks.All.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            bool touchesGeometry = p.Effects.Any(e =>
                PerkBalanceClassifier.ClassifyEffectTypeCategory(e.Type) == PerkBalanceCategory.Geometry);
            var category = PerkBalanceClassifier.Classify(p).Category;
            if (touchesGeometry && category != PerkBalanceCategory.Geometry)
            {
                _output.WriteLine($"  {p.Id,-20} clasificado como {category} (efecto primario: {PerkBalanceClassifier.GetPrimaryEffect(p).Type})");
            }
        }

        Assert.NotEmpty(geometry);
    }

    /// <summary>Una fila por perk de geometría: qué es, dónde cae, cuánto se activa y qué le hace al campo.</summary>
    [Fact]
    public void WhatEachGeometryPerkActuallyDoes()
    {
        foreach (var perk in GeometryPerks())
        {
            Describe(perk);
            _output.WriteLine("");
        }
    }

    private void Describe(PerkDefinition perk)
    {
        var primary = PerkBalanceClassifier.GetPrimaryEffect(perk);
        var carrier = ResolveCarrier(perk);

        _output.WriteLine($"### {perk.Id} — {perk.Name.Es}");
        _output.WriteLine($"  trigger      : {perk.Trigger}  scope={perk.Scope}  condición=\"{(perk.Condition.Length == 0 ? "(ninguna)" : perk.Condition)}\"");
        _output.WriteLine($"  restricción  : positionOnly={perk.PositionOnly?.ToString() ?? "(ninguna)"}  race={perk.Race?.ToString() ?? "(ninguna)"}  tags=[{string.Join(",", perk.TagsRequired)}]  links=[{string.Join(",", perk.Links)}]");
        _output.WriteLine($"  carrier real : {(carrier.Slot < 0 ? "NINGUNO ELEGIBLE" : $"slot {carrier.Slot} = {carrier.Position}")}");
        _output.WriteLine($"  efectos      : {string.Join(" + ", perk.Effects.Select(FormatEffect))}");

        if (carrier.Slot < 0)
        {
            _output.WriteLine("  (sin portador elegible en plantilla neutral: no se mide)");
            return;
        }

        var run = PairedBalanceHarness.RunWithEligibleCarrier(Catalog, perk, Rosters, Seed);
        int matches = run.ArmedMatches.Count;
        double exposure = run.ArmedActivationsPerMatch.Count == 0
            ? 0.0
            : 100.0 * run.ArmedActivationsPerMatch.Count(a => a > 0) / run.ArmedActivationsPerMatch.Count;
        double activationsPerMatch = matches == 0 ? 0.0 : (double)run.ArmedActivations / matches;

        double armedBalance = TerritorialBalance.ForBatch(run.ArmedMatches, CarrierTeamAt);
        double controlBalance = TerritorialBalance.ForBatch(run.ControlMatches, CarrierTeamAt);

        var armedMax = MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>()).Single(m => m.Name == MatchMetrics.BallThirdMaxShare);
        var controlMax = MatchMetrics.Compute(run.ControlMatches, Array.Empty<MetricPairing>()).Single(m => m.Name == MatchMetrics.BallThirdMaxShare);

        var (scoredA, concededA) = GoalsFor(run.ArmedMatches);
        var (scoredC, concededC) = GoalsFor(run.ControlMatches);

        _output.WriteLine($"  exposición   : {exposure:F1}% de los partidos con ≥1 activación; {activationsPerMatch:F2} activaciones/partido ({matches} partidos/brazo)");
        // Criterio EXISTENTE del proyecto (§5.5, 2×SE), no uno nuevo: solo dice si el delta territorial
        // se distingue del ruido a esta muestra. No es una banda ni un veredicto.
        var armedPerMatch = PerMatchBalance(run.ArmedMatches);
        var controlPerMatch = PerMatchBalance(run.ControlMatches);
        double perMatchDelta = armedPerMatch.Average() - controlPerMatch.Average();
        bool distinguishable = BalancePowerCheck.HasSufficientPower(
            perMatchDelta,
            BalancePowerCheck.SampleVariance(armedPerMatch), armedPerMatch.Count,
            BalancePowerCheck.SampleVariance(controlPerMatch), controlPerMatch.Count);

        _output.WriteLine($"  territorio   : armado {armedBalance,7:F3} | control {controlBalance,7:F3} | delta {armedBalance - controlBalance,+7:F3} | ¿distinguible del ruido (§5.5)? {(distinguishable ? "SÍ" : "NO")}");
        _output.WriteLine($"  ballThirdMax : armado {armedMax.Value,7:F3} | control {controlMax.Value,7:F3} | delta {armedMax.Value - controlMax.Value,+7:F3}");
        _output.WriteLine($"  goles a favor: armado {scoredA,7:F3} | control {scoredC,7:F3} | delta {scoredA - scoredC,+7:F3}");
        _output.WriteLine($"  goles contra : armado {concededA,7:F3} | control {concededC,7:F3} | delta {concededA - concededC,+7:F3}");
        _output.WriteLine(
            $"  FILA|{perk.Id}|{carrier.Position}|{FormatEffect(primary)}|{exposure:F1}|" +
            $"{armedBalance - controlBalance:F3}|{armedMax.Value - controlMax.Value:F3}|" +
            $"{scoredA - scoredC:F3}|{concededA - concededC:F3}|{(distinguishable ? "distinguible" : "ruido")}");
    }

    private static string FormatEffect(EffectDefinition e)
    {
        string value = e.UsesCounter
            ? $"+{e.ValuePerCounter}/contador {e.Counter} (÷{e.CounterDivisor}, máx {e.MaxValue})"
            : $"{e.Value:+#;-#;0}";
        string dimension = e.Type == EffectType.ModifyZoneShape ? $" {e.ZoneDimension}" : string.Empty;
        return $"{e.Type}{dimension}({value}, target={e.Target}, {e.Duration})";
    }

    private static (int Slot, Position Position) ResolveCarrier(PerkDefinition perk)
    {
        var rng = RngStreams.Generation(Seed, 0);
        var home = TeamGenerator.Generate(ref rng, Catalog, "home", perk.Race ?? Race.Human, 50, 1, 4);
        int slot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
        return slot < 0 ? (-1, Position.Goalkeeper) : (slot, home.Players[slot].Position);
    }

    private static List<double> PerMatchBalance(IReadOnlyList<MatchSummary> matches)
    {
        var values = new List<double>(matches.Count);
        for (int i = 0; i < matches.Count; i++)
        {
            values.Add(TerritorialBalance.ForMatch(matches[i], CarrierTeamAt(i)));
        }

        return values;
    }

    private static (double Scored, double Conceded) GoalsFor(IReadOnlyList<MatchSummary> matches)
    {
        double scored = 0, conceded = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            bool carrierHome = CarrierTeamAt(i) == 0;
            scored += carrierHome ? matches[i].HomeGoals : matches[i].AwayGoals;
            conceded += carrierHome ? matches[i].AwayGoals : matches[i].HomeGoals;
        }

        double n = Math.Max(1, matches.Count);
        return (scored / n, conceded / n);
    }
}
