using System.Globalization;
using System.Text;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis.Detectors;

/// <summary>
/// Barrido de la batería de detectores de síntomas (<see cref="SymptomDetectors"/>) sobre N partidos de la
/// build actual: por detector, casos por partido con su error típico, % de partidos afectados y los tres
/// peores casos con semilla y tick. Solo lee la traza (RT-098): no toca <c>/Sim</c> ni <c>/data</c>.
///
/// <para><b>Comando único</b>: <c>tools/barrido-detectores.sh [partidos]</c>. Variables: <c>UL_DET_MATCHES</c>
/// (100 por defecto; el mínimo del encargo), <c>UL_DET_SEED0</c> (1), <c>UL_DET_THREADS</c> (2: la máquina es
/// de 4 núcleos y comparte), <c>UL_DET_OUT</c> (carpeta de salida, por defecto
/// <c>Game/screenshots/detectores/</c>).</para>
///
/// <para>Los partidos son <see cref="TestMatches.Reference"/> con la semilla como identificador: reproducibles
/// con esa semilla. El detector de BB-I necesita jugadores con el perk <c>box_predator</c>, que la referencia
/// no lleva, así que se barre aparte un segundo lote con todos los jugadores de campo armados con él (misma
/// semilla, otro partido: el perk cambia el curso). Salida en <c>.txt</c>/<c>.md</c> porque <c>*.csv</c> está
/// en el <c>.gitignore</c>.</para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class SymptomSweepTests
{
    private readonly ITestOutputHelper _output;

    public SymptomSweepTests(ITestOutputHelper output) => _output = output;

    private sealed record MatchOutcome(ulong Seed, bool Armed, Dictionary<string, IReadOnlyList<Hit>> Hits, Dictionary<string, double> Metrics);

    [Fact]
    public void SweepTheBattery()
    {
        int matches = EnvInt("UL_DET_MATCHES", 100);
        ulong seed0 = (ulong)EnvInt("UL_DET_SEED0", 1);
        int threads = EnvInt("UL_DET_THREADS", 2);
        string outDir = Environment.GetEnvironmentVariable("UL_DET_OUT") ?? Path.Combine(RepoRoot(), "Game", "screenshots", "detectores");
        Directory.CreateDirectory(outDir);

        var plain = Play(matches, seed0, threads, armed: false);
        var armed = Play(matches, seed0, threads, armed: true);

        var report = new StringBuilder();
        report.AppendLine($"# Barrido de detectores — {matches} partidos de referencia (semillas {seed0}..{seed0 + (ulong)matches - 1}) + {matches} armados con box_predator");
        report.AppendLine();
        report.AppendLine("| Detector | Casos totales | Casos/partido ± e.t. | % partidos afectados | Peores (semilla@tick, magnitud) |");
        report.AppendLine("|---|---|---|---|---|");
        var cases = new StringBuilder();
        var worst = new StringBuilder();
        worst.AppendLine("detector\tseed\ttick\tlength\tmagnitude\tarmed\tnote");

        foreach (string key in plain[0].Hits.Keys.Concat(armed[0].Hits.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
        {
            var set = armed[0].Hits.ContainsKey(key) && !plain[0].Hits.ContainsKey(key) ? armed : plain;
            Aggregate(key, set, report, cases, worst);
        }

        report.AppendLine();
        report.AppendLine("## Métricas auxiliares (media por partido ± e.t.)");
        report.AppendLine();
        report.AppendLine("| Métrica | Media | e.t. | Partidos |");
        report.AppendLine("|---|---|---|---|");
        foreach (string m in plain.SelectMany(o => o.Metrics.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal))
        {
            AppendMetric(report, m, plain);
        }

        foreach (string m in armed.SelectMany(o => o.Metrics.Keys).Distinct().Except(plain.SelectMany(o => o.Metrics.Keys)).OrderBy(k => k, StringComparer.Ordinal))
        {
            AppendMetric(report, m + " [armado]", armed, m);
        }

        File.WriteAllText(Path.Combine(outDir, "resumen.md"), report.ToString());
        File.WriteAllText(Path.Combine(outDir, "casos.txt"), cases.ToString());
        File.WriteAllText(Path.Combine(outDir, "peores.tsv"), worst.ToString());
        _output.WriteLine(report.ToString());
        Assert.True(File.Exists(Path.Combine(outDir, "resumen.md")));
    }

    private static List<MatchOutcome> Play(int matches, ulong seed0, int threads, bool armed)
    {
        var outcomes = new MatchOutcome[matches];
        Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
        {
            ulong seed = seed0 + (ulong)i;
            var catalog = ThreadCatalogs.Current;
            var setup = TestMatches.Reference(catalog, seed);
            if (armed)
            {
                setup = setup with { Home = Arm(setup.Home), Away = Arm(setup.Away) };
            }

            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true });
            outcomes[i] = Detect(seed, armed, DetectorTrace.From(result));
        });
        return outcomes.ToList();
    }

    private static TeamSetup Arm(TeamSetup team)
    {
        var players = team.Players
            .Select(p => p.Position == Position.Goalkeeper ? p : p with { Perks = new[] { "box_predator" } })
            .ToList();
        return team with { Players = players };
    }

    private static MatchOutcome Detect(ulong seed, bool armed, DetectorTrace t)
    {
        var hits = new Dictionary<string, IReadOnlyList<Hit>>();
        var m = new Dictionary<string, double>();
        if (armed)
        {
            hits["BB-I perk sin tiro"] = SymptomDetectors.PerkWithoutShot(t, "box_predator", out int trig, out int blocked);
            m["BB-I disparos del perk"] = trig;
            m["BB-I con tiro bloqueado al instante"] = blocked;
            return new MatchOutcome(seed, true, hits, m);
        }

        hits["BB-K baile"] = SymptomDetectors.Dance(t);
        var loose = SymptomDetectors.LooseBall(t);
        hits["BC-G balón suelto quieto >=15 ticks"] = loose;
        hits["BC-G balón suelto quieto >=60 ticks"] = loose.Where(h => h.Length >= 60).ToList();
        hits["BA-J sin repliegue tras parada"] = SymptomDetectors.NoRetreatAfterSave(t, out var stragglers);
        m["BA-J rezagados medios por parada retenida"] = stragglers.Count == 0 ? 0 : stragglers.Average();
        m["BA-J paradas retenidas"] = stragglers.Count;
        hits["BF-C delantero pega sin balón"] = SymptomDetectors.ForwardOffBallHit(t, out int allOff);
        m["BF-C placajes sin balón (todos los puestos)"] = allOff;
        hits["BB-G2 portero perseguidor (abrazo mortal)"] = SymptomDetectors.GoalkeeperChaser(t, out int chase);
        m["BB-G2 ticks del portero eligiendo ChaseBall fuera del área"] = chase;
        hits["BN-A amontonamiento sobre el portero"] = SymptomDetectors.GoalkeeperCrowd(t);
        hits["BO-A portador y rival atascados >3 s"] = SymptomDetectors.CarrierStuck(t);
        hits["BB-A/L salto inexplicado"] = SymptomDetectors.Teleports(t, out int explained, out int leaving);
        m["BB-A salto explicado (reposición, control)"] = explained;
        m["BB-L salida del campo tras salto"] = leaving;
        hits["BB-B robo antes del saque de centro"] = SymptomDetectors.StealBeforeRestart(t, kickoffOnly: true);
        hits["BB-B robo antes de cualquier saque"] = SymptomDetectors.StealBeforeRestart(t, kickoffOnly: false);
        hits["BB-C celebración (salto o en su campo)"] = SymptomDetectors.Celebration(t);
        hits["BH-A congelación"] = SymptomDetectors.Freeze(t, out int ghost);
        m["BH-A ticks con el dueño fuera del campo"] = ghost;
        hits["BA-E gol sin ángulo"] = SymptomDetectors.GoalsWithoutAngle(t, out int shots, out int low, out int goals);
        m["BA-E tiros"] = shots;
        m["BA-E tiros sin ángulo (apertura<0,5)"] = low;
        m["BA-E goles"] = goals;
        return new MatchOutcome(seed, false, hits, m);
    }

    private static void Aggregate(string key, List<MatchOutcome> set, StringBuilder report, StringBuilder cases, StringBuilder worst)
    {
        var counts = set.Select(o => (double)(o.Hits.TryGetValue(key, out var h) ? h.Count : 0)).ToArray();
        double mean = counts.Average();
        double se = counts.Length > 1 ? Math.Sqrt(counts.Sum(c => (c - mean) * (c - mean)) / (counts.Length - 1) / counts.Length) : 0;
        int affected = counts.Count(c => c > 0);

        var all = set
            .SelectMany(o => (o.Hits.TryGetValue(key, out var h) ? h : Array.Empty<Hit>()).Select(x => (o.Seed, o.Armed, Hit: x)))
            .OrderByDescending(x => x.Hit.Magnitude).ThenBy(x => x.Seed).ThenBy(x => x.Hit.Tick)
            .ToList();
        string top = string.Join("; ", all.Take(3).Select(x => FormattableString.Invariant($"{x.Seed}@{x.Hit.Tick} ({x.Hit.Magnitude:0.##})")));
        report.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"| {key} | {counts.Sum():0} | {mean:0.000} ± {se:0.000} | {100.0 * affected / counts.Length:0.0} % ({affected}/{counts.Length}) | {(top.Length == 0 ? "—" : top)} |"));
        foreach (var x in all)
        {
            cases.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{key}\t{x.Seed}\t{x.Hit.Tick}\t{x.Hit.Length}\t{x.Hit.Magnitude:0.###}\t{x.Hit.Note}"));
        }

        foreach (var x in all.Take(3))
        {
            worst.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{key}\t{x.Seed}\t{x.Hit.Tick}\t{x.Hit.Length}\t{x.Hit.Magnitude:0.###}\t{(x.Armed ? 1 : 0)}\t{x.Hit.Note}"));
        }
    }

    private static void AppendMetric(StringBuilder report, string label, List<MatchOutcome> set, string? key = null)
    {
        key ??= label;
        var values = set.Select(o => o.Metrics.TryGetValue(key, out double v) ? v : 0.0).ToArray();
        double mean = values.Average();
        double se = values.Length > 1 ? Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1) / values.Length) : 0;
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {label} | {mean:0.000} | {se:0.000} | {values.Length} |"));
    }

    private static int EnvInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "global.json")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("no se encuentra la raíz del repositorio (global.json)");
    }
}
