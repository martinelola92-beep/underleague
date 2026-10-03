using System.Diagnostics;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.View;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Performance;

/// <summary>
/// BX-19: cuánto cuesta en <c>/Sim</c> lo que hace <c>RunController.Answer</c> al cambiar de orden táctica a mitad de
/// partido, por partes. Sonda: imprime, no afirma nada de comportamiento.
/// </summary>
public sealed class ReplanCostProbeTests
{
    private readonly ITestOutputHelper _output;

    public ReplanCostProbeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void TimeTheOrderChangeReplan()
    {
        var files = TestData.LoadAllFiles();
        var catalog = TestData.LoadCatalog();
        foreach (ulong seed in new ulong[] { 20260905, 20260906, 20260907, 20260908 })
        {
            var systems = StandardRunSystems.FromJson(files);
            var bossSystems = new BossRunSystems(BossCatalog.FromJson(files), systems);
            var setup0 = systems.NewRunSetup("human_abattoir", Race.Human, files);
            var state = bossSystems.AssignBosses(RunEngine.Start(setup0, seed, catalog, bossSystems));
            var node = RunEngine.AvailableNodes(state).First(n => n.IsMatch);
            var decisions = MatchDecisions.None with { OrderChanges = new[] { new OrderChange(600, Mentality.Offensive) } };

            // Calentamiento (JIT).
            MatchPlaybacks.Of(state, node.Id, catalog, bossSystems, trace: true, decisions);
            RunEngine.EnterMatch(state, node.Id, catalog, bossSystems, decisions);

            var built = RunEngine.BuildMatch(
                state, node.Id, catalog, bossSystems, decisions.ManualActivations, decisions.Substitutions, decisions.PlayOns, decisions.OrderChanges);
            var cfg = bossSystems.MatchConfig(state, node, catalog);
            long Min(Action a)
            {
                long best = long.MaxValue;
                for (int r = 0; r < 5; r++)
                {
                    var sw = Stopwatch.StartNew();
                    a();
                    best = Math.Min(best, sw.ElapsedMilliseconds);
                }

                return best;
            }

            long tBuild = Min(() => RunEngine.BuildMatch(
                state, node.Id, catalog, bossSystems, decisions.ManualActivations, decisions.Substitutions, decisions.PlayOns, decisions.OrderChanges));
            long tCfg = Min(() => bossSystems.MatchConfig(state, node, catalog));
            long tRun = Min(() => Simulator.Run(built.Setup, built.Seed, catalog, cfg));
            long tRunTrace = Min(() => Simulator.Run(built.Setup, built.Seed, catalog, cfg with { Trace = true }));
            long tResolve = Min(() => SubstitutionPoints.ResolveAutomatically(built.Setup, built.Seed, catalog, cfg with { Trace = true }, static t => t == 1, decisions.Declines));
            long tResolveAll = Min(() => SubstitutionPoints.ResolveAutomatically(built.Setup, built.Seed, catalog, cfg, null, decisions.Declines));
            long tPlay = Min(() => MatchPlaybacks.OfResolvingBlockedPoints(state, node.Id, catalog, bossSystems, true, decisions, 0, out _));
            long tEnter = Min(() => RunEngine.EnterMatch(state, node.Id, catalog, bossSystems, decisions));
            long tSave = Min(() => RunSave.Save(state, new PendingMatch(node.Id, decisions, 1000)));
            var playback = MatchPlaybacks.Of(state, node.Id, catalog, bossSystems, trace: true, decisions);
            long tMoments = Min(() => MatchMomentView.Build(playback.Setup, playback.Result, catalog, 0, decisions.Declines));
            long tLog = Min(() => MatchLogView.Build(playback, catalog.Tuning.RegulationTicks));
            int subs = playback.Setup.Home.Substitutions.Count + playback.Setup.Away.Substitutions.Count;

            _output.WriteLine(
                $"semilla {seed}: frames {playback.Result.Trace!.FrameCount} subs {subs} | BuildMatch {tBuild} · MatchConfig {tCfg} · Run {tRun} · Run+traza {tRunTrace} · " +
                $"Resolve(rival,traza) {tResolve} · Resolve(ambos) {tResolveAll} · Playback {tPlay} · EnterMatch {tEnter} · RunSave {tSave} · Momentos {tMoments} · Log {tLog} (ms, mínimo de 5)");
        }
    }
}
