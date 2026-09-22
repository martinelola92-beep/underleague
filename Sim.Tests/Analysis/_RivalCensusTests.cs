using Underleague.Sim.Analysis;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Instrumento TEMPORAL para la ADR 0126 (<c>docs/decisiones/0126-clanes-canonicos-identidad-y-potencia-separadas.md</c>,
/// sección "Medición previa"): ¿cuántos rivales distintos ve de verdad una run, y cuántas veces se cruza
/// con el mismo? Sin jugar ni un solo partido — solo <see cref="MapGenerator"/> y el recorrido del grafo
/// que produce, con los mismos <see cref="MapOptions"/> que usa <see cref="RunEngine.Start"/>
/// (<c>RunEngine.cs:106-111</c>, <c>RunSetup.OpponentIdsByAct</c>).
///
/// <para><b>Cómo se recorre el camino.</b> Se llama a la <see cref="RunPolicy.ChooseNode"/> real en cada
/// bifurcación del grafo (no una reimplementación), pero <b>sin</b> el bucle de <see cref="RunPolicy.Play"/>
/// que la alimenta: ese bucle juega partidos de verdad (<c>Simulator.Run</c> vía <c>RunEngine.Enter</c>),
/// que es justo lo que el encargo prohíbe medir. En su lugar se le pasa <b>un único estado fijo</b>, el que
/// deja <see cref="RunEngine.Start"/> recién empezada la run (oro de partida, plantilla sana completa, sin
/// lesiones), y se reutiliza en cada nodo del camino. Es una simplificación necesaria, no una elección
/// libre: sin jugar partidos no hay oro que gastar ni plantilla que desgastar, así que no hay estado que
/// evolucione. El efecto medible es que <c>ChooseNode</c> ve siempre "plantilla completa" (empuja a los
/// nodos de élite, <c>EliteFromAvailable</c> = 8) y "nunca hace falta clínica" — un sesgo optimista hacia
/// más partidos de élite que un run real con bajas ocasionales.
///
/// <para>Como red de comprobación barata se recorren también las <b>dos cotas</b> que pide el encargo si
/// acoplar la política real resultase caro: el camino que siempre coge el carril más bajo disponible y el
/// que siempre coge el más alto. No sustituyen a la política real —que sí se pudo acoplar—, son solo
/// contexto sobre cuánto importa la elección de carril.</para>
///
/// <para><b>El nodo de jefe se excluye</b> (<see cref="NodeKind.Boss"/>): guarda un <c>OpponentId</c>
/// fantasma que ningún sistema usa, y contarlo falsearía el censo. El recorrido para en cuanto la única
/// opción de la capa final es el jefe, sin entrar en él.</para>
///
/// <para>Marcado <c>Diagnostic</c> a propósito: no es una puerta ni un test de comportamiento, es una
/// medición de una sola vez para una decisión de arquitectura pendiente. Se borra o se convierte en otra
/// cosa cuando la ADR 0126 se cierre.</para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class _RivalCensusTests
{
    private const int SeedCount = 500;

    private readonly ITestOutputHelper _output;

    public _RivalCensusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CuantosRivalesDistintosVeUnaRunYCuantasVecesSeRepiten()
    {
        var files = TestData.LoadAllFiles();
        var catalog = TestData.LoadCatalog();
        var systems = StandardRunSystems.FromJson(files);
        var setup = systems.NewRunSetup("rival_census_club", Race.Human, files);
        var options = RunPolicyOptions.Default;

        var real = new List<RunCensus>(SeedCount);
        var firstLane = new List<RunCensus>(SeedCount);
        var lastLane = new List<RunCensus>(SeedCount);

        for (ulong seed = 1; seed <= SeedCount; seed++)
        {
            var state = RunEngine.Start(setup, seed, catalog, systems);

            real.Add(Census(state, nodes => RunPolicy.ChooseNode(state, nodes, systems.Economy, options)));
            firstLane.Add(Census(state, static nodes => ByLane(nodes, lowest: true)));
            lastLane.Add(Census(state, static nodes => ByLane(nodes, lowest: false)));
        }

        _output.WriteLine("Rivales disponibles por acto en /data/rivals/ (para contexto, no es lo medido):");
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            _output.WriteLine($"  acto {act}: {systems.OpponentIdsByAct()[act - 1].Count} clanes en el catálogo");
        }

        _output.WriteLine("");
        _output.WriteLine($"=== Política real (RunPolicy.ChooseNode, {SeedCount} semillas) ===");
        Report(real, _output);

        _output.WriteLine("");
        _output.WriteLine("=== Cota: siempre el carril más bajo disponible (contexto, no la política real) ===");
        ReportShort(firstLane, _output);

        _output.WriteLine("");
        _output.WriteLine("=== Cota: siempre el carril más alto disponible (contexto, no la política real) ===");
        ReportShort(lastLane, _output);

        Assert.True(real.Count == SeedCount);
    }

    // ------------------------------------------------------------------ censo por run

    /// <summary>Censo de una run entera: partidos y rivales por acto y sobre la run completa.</summary>
    private sealed record RunCensus(
        int MatchesTotal,
        IReadOnlyList<int> MatchesByAct,
        int DistinctRivals,
        int MaxRepeat,
        bool HasRepeat,
        bool HasTriplicate,
        IReadOnlyList<int> DistinctRivalsByAct,
        IReadOnlyList<int> MaxRepeatByAct);

    /// <summary>
    /// Recorre los tres actos de <paramref name="state"/> con el criterio de elección de nodo dado y cuenta
    /// rivales. Cada acto tiene su propio espacio de ids de rival (<c>data/rivals/*.json</c> los agrupa por
    /// <c>act</c>), así que sumar los tres actos en el mismo diccionario no mezcla identidades distintas
    /// bajo el mismo id.
    /// </summary>
    private static RunCensus Census(RunState state, Func<IReadOnlyList<MapNode>, MapNode> choose)
    {
        var overall = new Dictionary<string, int>(StringComparer.Ordinal);
        var matchesByAct = new int[RunRules.Acts];
        var distinctByAct = new int[RunRules.Acts];
        var maxRepeatByAct = new int[RunRules.Acts];

        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var map = state.MapOf(act);
            var perAct = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var node in WalkMatches(map, choose))
            {
                perAct[node.OpponentId] = perAct.GetValueOrDefault(node.OpponentId) + 1;
                overall[node.OpponentId] = overall.GetValueOrDefault(node.OpponentId) + 1;
                matchesByAct[act - 1]++;
            }

            distinctByAct[act - 1] = perAct.Count;
            maxRepeatByAct[act - 1] = perAct.Count == 0 ? 0 : perAct.Values.Max();
        }

        int maxRepeat = overall.Count == 0 ? 0 : overall.Values.Max();
        return new RunCensus(
            matchesByAct.Sum(),
            matchesByAct,
            overall.Count,
            maxRepeat,
            maxRepeat >= 2,
            maxRepeat >= 3,
            distinctByAct,
            maxRepeatByAct);
    }

    /// <summary>Los nodos de partido (liga o élite, nunca jefe) que visita el camino elegido en ese acto.</summary>
    private static IEnumerable<MapNode> WalkMatches(ActMap map, Func<IReadOnlyList<MapNode>, MapNode> choose)
    {
        int currentId = -1;
        while (true)
        {
            var ids = currentId < 0 ? map.EntryNodeIds : map.Get(currentId).Next;
            if (ids.Count == 0)
            {
                yield break;
            }

            var nodes = new List<MapNode>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                nodes.Add(map.Get(ids[i]));
            }

            var chosen = choose(nodes);
            if (chosen.Kind == NodeKind.Boss)
            {
                // El jefe es siempre la única opción de la última capa: se para aquí y no se cuenta
                // (su OpponentId es un valor fantasma que ningún sistema usa).
                yield break;
            }

            if (NodeKinds.IsMatch(chosen.Kind))
            {
                yield return chosen;
            }

            currentId = chosen.Id;
        }
    }

    /// <summary>Cota: el nodo con el carril (<see cref="MapNode.IndexInLayer"/>) más bajo o más alto disponible.</summary>
    private static MapNode ByLane(IReadOnlyList<MapNode> nodes, bool lowest)
    {
        var best = nodes[0];
        for (int i = 1; i < nodes.Count; i++)
        {
            bool better = lowest
                ? nodes[i].IndexInLayer < best.IndexInLayer
                : nodes[i].IndexInLayer > best.IndexInLayer;
            if (better)
            {
                best = nodes[i];
            }
        }

        return best;
    }

    // ------------------------------------------------------------------ informe

    private static void Report(IReadOnlyList<RunCensus> runs, ITestOutputHelper output)
    {
        output.WriteLine($"partidos de liga/élite por run: {Stats(runs.Select(r => r.MatchesTotal))}");
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            int a = act;
            output.WriteLine($"  acto {act}: {Stats(runs.Select(r => r.MatchesByAct[a - 1]))}");
        }

        output.WriteLine("");
        output.WriteLine($"rivales distintos por run: {Stats(runs.Select(r => r.DistinctRivals))}");
        output.WriteLine($"  distribución: {Distribution(runs.Select(r => r.DistinctRivals))}");
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            int a = act;
            output.WriteLine($"  acto {act}: {Stats(runs.Select(r => r.DistinctRivalsByAct[a - 1]))}");
        }

        output.WriteLine("");
        output.WriteLine($"repetición del rival más repetido de la run: {Stats(runs.Select(r => r.MaxRepeat))}");
        output.WriteLine($"  distribución: {Distribution(runs.Select(r => r.MaxRepeat))}");
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            int a = act;
            output.WriteLine($"  acto {act}: {Stats(runs.Select(r => r.MaxRepeatByAct[a - 1]))}");
        }

        output.WriteLine("");
        double repeatPct = 100.0 * runs.Count(r => r.HasRepeat) / runs.Count;
        double triplePct = 100.0 * runs.Count(r => r.HasTriplicate) / runs.Count;
        output.WriteLine($"% de runs con al menos un rival repetido: {repeatPct:F1}%");
        output.WriteLine($"% de runs con algún rival 3+ veces: {triplePct:F1}%");
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            int a = act;
            double repeatActPct = 100.0 * runs.Count(r => r.MaxRepeatByAct[a - 1] >= 2) / runs.Count;
            double tripleActPct = 100.0 * runs.Count(r => r.MaxRepeatByAct[a - 1] >= 3) / runs.Count;
            output.WriteLine($"  acto {act}: repetido {repeatActPct:F1}% · 3+ veces {tripleActPct:F1}%");
        }
    }

    private static void ReportShort(IReadOnlyList<RunCensus> runs, ITestOutputHelper output)
    {
        output.WriteLine($"rivales distintos por run: {Stats(runs.Select(r => r.DistinctRivals))}");
        output.WriteLine($"repetición del rival más repetido: {Stats(runs.Select(r => r.MaxRepeat))}");
        double repeatPct = 100.0 * runs.Count(r => r.HasRepeat) / runs.Count;
        double triplePct = 100.0 * runs.Count(r => r.HasTriplicate) / runs.Count;
        output.WriteLine($"% con al menos un rival repetido: {repeatPct:F1}% · % con 3+ veces: {triplePct:F1}%");
    }

    private static string Stats(IEnumerable<int> values)
    {
        var list = values.ToList();
        double mean = list.Average();
        return $"media {mean:F2} · min {list.Min()} · max {list.Max()}";
    }

    private static string Distribution(IEnumerable<int> values)
    {
        var counts = new SortedDictionary<int, int>();
        foreach (int v in values)
        {
            counts[v] = counts.GetValueOrDefault(v) + 1;
        }

        return string.Join(" · ", counts.Select(kv => $"{kv.Key}→{kv.Value}"));
    }
}
