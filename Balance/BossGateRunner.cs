using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Bosses;

namespace Underleague.Balance;

/// <summary>Resultado de <see cref="BossGateRunner.Run"/>: celdas jugadas y métricas ya contrastadas.</summary>
public sealed record BossGateResult(
    IReadOnlyList<BossGateCell> Cells,
    IReadOnlyList<MetricResult> Metrics,
    int TotalMatches,
    TimeSpan Elapsed);

/// <summary>
/// Modo <c>--boss-gate</c>: la curva de puertas de la ADR 0033 medida con <b>partidos directos
/// build-contra-jefe</b>. Es el instrumento con el que se calibran los jefes de <c>data/bosses/</c>
/// contra la tabla (y no al revés) y el que reproduce fuera de la puerta de <c>Sim.Tests</c> lo que esa
/// puerta comprueba.
///
/// <para>Cada celda enfrenta la build de un nivel de calidad (<c>qualityLevels</c> de
/// <c>data/balance/groups.json</c>) con un jefe, con la plantilla del jugador al nivel que la run le da
/// a esa altura (<c>gate.playerLevel</c> del jefe) y con los modificadores de regla del jefe aplicados.
/// El agregado por (jefe, nivel) es lo que se compara con la ADR; el desglose por raza va como
/// <c>INFO</c>.</para>
/// </summary>
public static class BossGateRunner
{
    public static BossGateResult Run(
        Catalog catalog,
        BossCatalog bosses,
        IReadOnlyDictionary<string, BuildConfig> builds,
        IReadOnlyDictionary<string, IReadOnlyList<string>> qualityLevels,
        ulong seed,
        int rosters,
        int matchesPerRoster)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(bosses);
        ArgumentNullException.ThrowIfNull(builds);
        ArgumentNullException.ThrowIfNull(qualityLevels);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<BossGateCell>();
        int matchIndex = 0;

        foreach (var boss in bosses.All)
        {
            foreach (var level in BossGateMetrics.Levels)
            {
                if (!qualityLevels.TryGetValue(level, out var ids))
                {
                    continue;
                }

                foreach (var buildId in ids.OrderBy(id => id, StringComparer.Ordinal))
                {
                    if (!builds.TryGetValue(buildId, out var build))
                    {
                        throw new ArgumentException(
                            $"la build '{buildId}' de qualityLevels.{level} no existe en data/balance/builds/");
                    }

                    // La plantilla del jugador llega a la puerta con el nivel que le da la progresión de
                    // la run (gate.playerLevel); la construcción es lo único que cambia entre escalones.
                    var atGate = build with { Level = boss.GatePlayerLevel };
                    var cell = BossGateMetrics.PlayCell(
                        catalog, boss, level, buildId,
                        (roster, idBase) =>
                        {
                            var rng = RngStreams.Generation(seed, roster);
                            return atGate.ToTeamSetup(ref rng, catalog, idBase);
                        },
                        seed, rosters, matchesPerRoster, matchIndex, (int)build.Race);

                    matchIndex += cell.Matches;
                    cells.Add(cell);
                }
            }
        }

        stopwatch.Stop();
        return new BossGateResult(cells, BossGateMetrics.Compute(cells, bosses.All), matchIndex, stopwatch.Elapsed);
    }

    /// <summary>Tabla de la curva de puertas por consola: filas = jefe, columnas = nivel de calidad de build.</summary>
    public static void PrintTable(IReadOnlyList<MetricResult> metrics, BossCatalog bosses)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(bosses);

        Console.WriteLine("curva de puertas (ADR 0033): tasa de superación por nivel de build");
        Console.WriteLine($"{"jefe",-16} {"incoherente",-18} {"correcta",-18} {"buena",-18} {"muy buena",-18}");
        foreach (var boss in bosses.All)
        {
            var line = new System.Text.StringBuilder();
            line.Append($"{boss.Id,-16} ");
            foreach (var level in BossGateMetrics.Levels)
            {
                var row = metrics.FirstOrDefault(m => m.Name == $"{BossGateMetrics.GatePrefix}{boss.Id}_{level}");
                var target = boss.TargetFor(level);
                string band = target is null
                    ? "-"
                    : $"{target.MinPercent?.ToString("F0") ?? ""}-{target.MaxPercent?.ToString("F0") ?? ""}";
                double value = row?.Value ?? 0.0;
                string mark = row?.Status == "IN" ? "  " : row?.Status == "OUT" ? "!!" : "..";
                line.Append($"{value,6:F1} [{band,6}] {mark} ");
            }

            Console.WriteLine(line.ToString());
        }

        Console.WriteLine();
        foreach (var metric in metrics.Where(m => m.Name.StartsWith(BossGateMetrics.BuildPrefix, StringComparison.Ordinal)))
        {
            Console.WriteLine($"  {metric.Name,-48} {metric.Value,6:F2}");
        }

        foreach (var metric in metrics.Where(m => m.Name.StartsWith(BossGateMetrics.DefeatConditionPrefix, StringComparison.Ordinal)))
        {
            Console.WriteLine($"  {metric.Name,-48} {metric.Value,6:F2}  (victorias anuladas por la condición propia del jefe)");
        }
    }
}
