using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Perks;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// §34: pasada DIAGNÓSTICA perk a perk sobre los 24 <c>ReadyForScreening</c>.
///
/// <para><b>Esto NO es el lote.</b> El lote (<see cref="RealScreeningLot1Tests"/>) sigue corriendo con su
/// circuito de seguridad intacto y sigue parándose en 5/24 — ese veredicto no se toca, no se recalibra
/// <see cref="BatchEscalation"/> y no se cambia ningún umbral. Lo que hace este test es llamar a
/// <c>ScreeningRunner.RunPerk</c> uno por uno para ver QUÉ diría el screening de los 19 restantes, que es
/// información de diseño que el lote detenido no llega a producir.</para>
///
/// <para>La distinción importa: el circuito de §9.1 existe para que un lote no siga procesando
/// mecánicamente cuando el instrumento está avisando de algo. Aquí precisamente se atiende el aviso —se
/// mira qué está pasando— en vez de ignorarlo subiendo el umbral.</para>
/// </summary>
public sealed class Lot1DiagnosticSweepTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;
    public Lot1DiagnosticSweepTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void WhatWouldScreeningSayAboutEachOfTheTwentyFour()
    {
        var readyIds = PerkAudit.AuditCatalog(Catalog.Perks.All, Catalog)
            .Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening)
            .Select(e => e.PerkId)
            .ToHashSet(StringComparer.Ordinal);
        var perks = Catalog.Perks.All
            .Where(p => readyIds.Contains(p.Id))
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();

        // 24 en §34; 28 al cuadrar razas y rasgos (19 sep 2026); 27 tras borrar pack_mentality,
        // shadow_marker y fine_orchestra (revisor, 18 sep 2026) — solo shadow_marker era
        // ReadyForScreening, los otros dos eran MultiTarget.
        Assert.Equal(27, perks.Count);

        var results = new List<(PerkDefinition Perk, ScreeningResult Result, PerkClassification Classification)>();
        foreach (var perk in perks)
        {
            var result = ScreeningRunner.RunPerk(Catalog, perk, seed: 1);
            results.Add((perk, result, PerkBalanceClassifier.Classify(perk)));
        }

        _output.WriteLine("perk | categoría | métrica primaria | estado | partidos | exposición | delta | motivo");
        foreach (var (perk, r, c) in results)
        {
            string exposure = r.ExposureFraction is { } e ? $"{e:P1}" : "-";
            string delta = r.PrimaryDelta is { } d ? $"{d:F4}" : "-";
            _output.WriteLine(
                $"{perk.Id} | {c.Category} | {c.PrimaryMetric} | {r.DisplayState} | {r.Cost.MatchesSimulated} | " +
                $"{exposure} | {delta} | {r.Reason}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== distribución de estados sobre los 24 ===");
        foreach (var g in results.GroupBy(x => x.Result.DisplayState).OrderByDescending(g => g.Count()))
        {
            _output.WriteLine($"{g.Key}: {g.Count()} — {string.Join(", ", g.Select(x => x.Perk.Id))}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== por categoría de balanceo ===");
        foreach (var g in results.GroupBy(x => x.Classification.Category).OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            _output.WriteLine($"{g.Key}: {g.Count()} — {string.Join(", ", g.Select(x => $"{x.Perk.Id}[{x.Result.DisplayState}]"))}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== exposición medida (ordenada) ===");
        foreach (var (perk, r, _) in results.Where(x => x.Result.ExposureFraction is not null).OrderBy(x => x.Result.ExposureFraction))
        {
            _output.WriteLine($"{perk.Id,-22} {r.ExposureFraction!.Value,7:P1}  trigger={perk.Trigger} condición=\"{(perk.Condition.Length == 0 ? "(ninguna)" : perk.Condition)}\"");
        }

        _output.WriteLine("");
        _output.WriteLine("=== NEEDS_TUNING: baseline registrado, sin buscar valor ===");
        foreach (var (perk, r, _) in results.Where(x => x.Result.FinalState == BalanceState.Tuning))
        {
            _output.WriteLine($"{perk.Id}: baseline={r.TuningInfo!.BaselineValue} dirección={r.TuningInfo.Direction} efecto={r.TuningInfo.ObservedEffect:F4} estrategia={r.TuningInfo.SearchStrategy}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== seguridad y señales sistémicas ===");
        foreach (var (perk, r, _) in results.Where(x => x.Result.SafetyMetricsOut.Count > 0 || x.Result.SystemicSignals.Count > 0))
        {
            _output.WriteLine($"{perk.Id}: seguridad=[{string.Join(",", r.SafetyMetricsOut)}] señales=[{string.Join(" | ", r.SystemicSignals)}]");
        }

        _output.WriteLine("");
        _output.WriteLine("=== coste de la pasada completa ===");
        _output.WriteLine($"total matches: {results.Sum(x => x.Result.Cost.MatchesSimulated)}");
        _output.WriteLine($"total ticks: {results.Sum(x => x.Result.Cost.TicksSimulated)}");
        _output.WriteLine($"total wall (suma por perk): {results.Sum(x => x.Result.Cost.WallTimeMs)} ms");

        foreach (var (perk, r, _) in results)
        {
            Assert.True(Enum.IsDefined(r.FinalState), $"{perk.Id} terminó en un estado no definido");
        }
    }
}
