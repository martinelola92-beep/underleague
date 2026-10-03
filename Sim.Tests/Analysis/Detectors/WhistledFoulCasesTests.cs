using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.View;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis.Detectors;

/// <summary>
/// ADR 0186 (visual-review): faltas pitadas del partido que reproduce Godot con <c>-- movimiento &lt;carpeta&gt; &lt;semilla&gt;</c>
/// (primer partido del acto 1 de una run de <c>human_abattoir</c>), con quién queda en el suelo en el tick del suceso.
/// Sirve para elegir la <c>ventana</c> de la captura: una falta dura (infractor en el suelo) y una que no.
/// </summary>
public sealed class WhistledFoulCasesTests
{
    private readonly ITestOutputHelper _output;

    public WhistledFoulCasesTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void ListWhistledFoulsOfTheCaptureMatch()
    {
        var files = TestData.LoadAllFiles();
        var catalog = TestData.LoadCatalog();
        foreach (ulong seed in new ulong[] { 20260905, 20260906, 20260907 })
        {
            var systems = StandardRunSystems.FromJson(files);
            var bossSystems = new BossRunSystems(BossCatalog.FromJson(files), systems);
            var setup0 = systems.NewRunSetup("human_abattoir", Race.Human, files);
            var state = bossSystems.AssignBosses(RunEngine.Start(setup0, seed, catalog, bossSystems));
            var node = RunEngine.AvailableNodes(state).First(n => n.IsMatch);
            var result = MatchPlaybacks.Of(state, node.Id, catalog, bossSystems, trace: true).Result;
            var trace = result.Trace!;
            var byId = new Dictionary<int, int>();
            for (int i = 0; i < trace.Players.Count; i++)
            {
                byId[trace.Players[i].Id] = i;
            }

            foreach (var e in result.Events.Where(e => e.Type == EventType.Foul && e.Detail == "foul"))
            {
                if (!byId.TryGetValue(e.Actor, out int a) || !byId.TryGetValue(e.Opponent, out int v))
                {
                    continue;
                }

                int f = trace.FrameOfTick(e.Tick);
                _output.WriteLine($"semilla {seed} tick {e.Tick}: infractor {e.Actor} ({trace.Players[a].Team}) {trace.StateAt(f, a)} · víctima {e.Opponent} ({trace.Players[v].Team}) {trace.StateAt(f, v)}");
            }
        }
    }
}
