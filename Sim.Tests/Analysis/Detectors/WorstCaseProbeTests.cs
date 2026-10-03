using System.Globalization;
using System.Text;
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
/// Sonda temporal (gameplay-debug, Regla A): reproduce un peor caso del barrido por traza, semilla y tick y vuelca
/// el tramo. Variables: UL_PROBE_KIND (ref|run), UL_PROBE_SEED, UL_PROBE_FROM, UL_PROBE_TO, UL_PROBE_STEP,
/// UL_PROBE_DUMP (id de jugador para el volcado RT-098 en UL_PROBE_DUMPTICK).
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class WorstCaseProbeTests
{
    private readonly ITestOutputHelper _output;

    public WorstCaseProbeTests(ITestOutputHelper output) => _output = output;

    internal static (MatchSetup Setup, ulong Seed, SimConfig Config) Build(string kind, ulong seed, Catalog catalog)
    {
        if (kind == "run")
        {
            var files = TestData.LoadAllFiles();
            var systems = StandardRunSystems.FromJson(files);
            var bossSystems = new BossRunSystems(BossCatalog.FromJson(files), systems);
            var setup0 = systems.NewRunSetup("human_abattoir", Race.Human, files);
            var state = bossSystems.AssignBosses(RunEngine.Start(setup0, seed, catalog, bossSystems));
            var node = RunEngine.AvailableNodes(state).First(n => n.IsMatch);
            var pb = MatchPlaybacks.Of(state, node.Id, catalog, bossSystems, trace: true);
            return (pb.Setup, pb.Seed, bossSystems.MatchConfig(state, state.GetNode(node.Id), catalog) with { Trace = true });
        }

        return (TestMatches.Reference(catalog, seed), seed, SimConfig.Default with { Trace = true });
    }

    [Fact]
    public void DumpWorstCase()
    {
        string kind = Environment.GetEnvironmentVariable("UL_PROBE_KIND") ?? "run";
        ulong seed = ulong.Parse(Environment.GetEnvironmentVariable("UL_PROBE_SEED") ?? "130", CultureInfo.InvariantCulture);
        int from = int.Parse(Environment.GetEnvironmentVariable("UL_PROBE_FROM") ?? "1015", CultureInfo.InvariantCulture);
        int to = int.Parse(Environment.GetEnvironmentVariable("UL_PROBE_TO") ?? "1320", CultureInfo.InvariantCulture);
        int step = int.Parse(Environment.GetEnvironmentVariable("UL_PROBE_STEP") ?? "10", CultureInfo.InvariantCulture);
        string? dumpId = Environment.GetEnvironmentVariable("UL_PROBE_DUMP");
        var catalog = TestData.LoadCatalog();
        var (setup, s, config) = Build(kind, seed, catalog);
        var result = Simulator.Run(setup, s, catalog, config);
        var tr = result.Trace!;
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{kind}:{seed} frames={tr.FrameCount}");
        foreach (var e in result.Events.Where(e => e.Tick >= from - 30 && e.Tick <= to))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  ev {e.Tick} {e.Type} actor={e.Actor} opp={e.Opponent} {e.Detail}");
        }

        for (int tick = from; tick <= to; tick += step)
        {
            int f = tr.FrameOfTick(tick);
            var b = tr.BallAt(f);
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"t={tr.TickAt(f)} ph={tr.PhaseAt(f)} rs={tr.RestartAt(f)} ball=({b.X:0.00},{b.Y:0.00}) owner={tr.BallOwnerAt(f)} fl={tr.BallInFlightAt(f)}");
            for (int p = 0; p < tr.Players.Count; p++)
            {
                if (!tr.OnPitchAt(f, p))
                {
                    continue;
                }

                var pos = tr.PositionAt(f, p);
                if (Vec2.Distance(pos, b) > 3.0f)
                {
                    continue;
                }

                var tg = tr.TargetAt(f, p);
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"   p{p} id={tr.Players[p].Id} tm={tr.Players[p].Team} {tr.Players[p].Role} ({pos.X:0.00},{pos.Y:0.00}) {tr.StateAt(f, p)} {tr.ActionAt(f, p)} tgt=({tg.X:0.00},{tg.Y:0.00}) d={Vec2.Distance(pos, b):0.00}");
            }
        }

        if (dumpId is not null)
        {
            int id = int.Parse(dumpId, CultureInfo.InvariantCulture);
            foreach (string tStr in (Environment.GetEnvironmentVariable("UL_PROBE_DUMPTICK") ?? from.ToString(CultureInfo.InvariantCulture)).Split(','))
            {
                int dt = int.Parse(tStr, CultureInfo.InvariantCulture);
                var r2 = Simulator.Run(setup, s, catalog, config with { DumpUtility = (id, dt) });
                var d = r2.Report.UtilityDump;
                sb.AppendLine(CultureInfo.InvariantCulture, $"DUMP id={id} tick={d?.Tick} state={d?.State} chosen={d?.Chosen}");
                foreach (var row in d?.Rows ?? Array.Empty<UtilityRow>())
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"   {row}");
                }
            }
        }

        _output.WriteLine(sb.ToString());
        File.WriteAllText(Environment.GetEnvironmentVariable("UL_PROBE_OUT") ?? Path.Combine(Path.GetTempPath(), "probe.txt"), sb.ToString());
    }

    /// <summary>Censo: fotogramas de juego abierto con el dueño del balón en un estado que no es de portador.</summary>
    [Fact]
    public void OwnerOutOfCarrierStateCensus()
    {
        int matches = int.Parse(Environment.GetEnvironmentVariable("UL_PROBE_N") ?? "200", CultureInfo.InvariantCulture);
        string variant = Environment.GetEnvironmentVariable("UL_PROBE_VARIANT") ?? "current";
        var sb = new StringBuilder();
        foreach (string kind in new[] { "ref", "run" })
        {
            var rows = new (int Frames, int Episodes, int Max, string First)[matches];
            Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i =>
            {
                ulong seed = 1 + (ulong)i;
                var catalog = Variant(ThreadCatalogs.Current, variant);
                var (setup, s, config) = Build(kind, seed, catalog);
                var result = Simulator.Run(setup, s, catalog, config);
                var tr = result.Trace!;
                int frames = 0, episodes = 0, max = 0, run = 0;
                string first = "";
                for (int f = 0; f < tr.FrameCount; f++)
                {
                    int o = tr.BallOwnerAt(f);
                    bool bad = tr.PhaseAt(f) == MatchPhase.OpenPlay && o >= 0
                        && tr.StateAt(f, o) is PlayerState.Positioning or PlayerState.Chasing or PlayerState.Tackling or PlayerState.Blocking;
                    if (bad)
                    {
                        frames++;
                        if (run == 0)
                        {
                            episodes++;
                            int t = tr.TickAt(f);
                            if (first.Length < 300)
                            {
                                first += $"[{t} {tr.StateAt(f, o)} " + string.Join(",", result.Events.Where(e => e.Tick == t).Select(e => e.Type + ":" + e.Detail)) + "]";
                            }
                        }

                        run++;
                        max = Math.Max(max, run);
                    }
                    else
                    {
                        run = 0;
                    }
                }

                rows[i] = (frames, episodes, max, first);
            });
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{variant} {kind}: frames={rows.Sum(r => r.Frames)} episodes={rows.Sum(r => r.Episodes)} matches={rows.Count(r => r.Episodes > 0)} max={rows.Max(r => r.Max)}");
            for (int i = 0; i < matches; i++)
            {
                if (rows[i].Episodes > 0)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"   {kind}:{i + 1} eps={rows[i].Episodes} max={rows[i].Max} {rows[i].First}");
                }
            }
        }

        _output.WriteLine(sb.ToString());
        File.WriteAllText(Environment.GetEnvironmentVariable("UL_PROBE_OUT") ?? Path.Combine(Path.GetTempPath(), "probe.txt"), sb.ToString());
    }

    /// <summary>BO-A y BN-A por variante de datos, con lo que hacen los implicados durante cada caso.</summary>
    [Fact]
    public void StuckAndCrowdByVariant()
    {
        int matches = int.Parse(Environment.GetEnvironmentVariable("UL_PROBE_N") ?? "200", CultureInfo.InvariantCulture);
        string variant = Environment.GetEnvironmentVariable("UL_PROBE_VARIANT") ?? "current";
        var sb = new StringBuilder();
        foreach (string kind in new[] { "ref", "run" })
        {
            var bo = new int[matches];
            var bn = new int[matches];
            var hist = new Dictionary<string, int>[matches];
            Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i =>
            {
                ulong seed = 1 + (ulong)i;
                var catalog = Variant(ThreadCatalogs.Current, variant);
                var (setup, s, config) = Build(kind, seed, catalog);
                var result = Simulator.Run(setup, s, catalog, config);
                var t = DetectorTrace.From(result);
                var h = new Dictionary<string, int>();
                var stuck = SymptomDetectors.CarrierStuck(t);
                bo[i] = stuck.Count;
                foreach (var hit in stuck)
                {
                    int c = t.Owner[hit.Frame];
                    int shieldFrames = 0;
                    for (int f = hit.Frame; f < hit.Frame + hit.Length && f < t.Frames; f++)
                    {
                        shieldFrames += t.State[t.Slot(f, c)] == PlayerState.Shielding ? 1 : 0;
                    }

                    string kb = $"BO shield {shieldFrames / 10 * 10:00} len {hit.Length / 10 * 10:00}";
                    h[kb] = h.GetValueOrDefault(kb) + 1;
                    if (shieldFrames >= 40)
                    {
                        string ks = $"BO seed {seed}@{hit.Tick} shield {shieldFrames} len {hit.Length}";
                        h[ks] = 1;
                    }
                    for (int f = hit.Frame; f < hit.Frame + hit.Length && f < t.Frames; f++)
                    {
                        int r = -1;
                        float best = SymptomDetectors.StuckRadius;
                        for (int p = 0; p < t.Players; p++)
                        {
                            if (t.Team[p] != t.Team[c] && t.On(f, p) && Vec2.Distance(t.Pos(f, p), t.Pos(f, c)) < best)
                            {
                                best = Vec2.Distance(t.Pos(f, p), t.Pos(f, c));
                                r = p;
                            }
                        }

                        string ra = r < 0 ? "-" : (t.Action[t.Slot(f, r)] < 0 ? "none" : ((PlayerAction)t.Action[t.Slot(f, r)]).ToString());
                        string key1 = "BO rival " + (r < 0 ? "-" : t.Role[r] + "/" + t.State[t.Slot(f, r)] + "/" + ra);
                        string key2 = "BO carrier " + t.Role[c] + "/" + t.State[t.Slot(f, c)];
                        h[key1] = h.GetValueOrDefault(key1) + 1;
                        h[key2] = h.GetValueOrDefault(key2) + 1;
                    }
                }

                var crowd = SymptomDetectors.GoalkeeperCrowd(t);
                bn[i] = crowd.Count;
                foreach (var hit in crowd)
                {
                    string how = string.Join(",", result.Events.Where(e => e.Tick == hit.Tick && e.Type is EventType.Save or EventType.Recovery or EventType.PassCompleted).Select(e => e.Type + ":" + e.Detail));
                    string k = "BN how " + (how.Length == 0 ? "?" : how);
                    h[k] = h.GetValueOrDefault(k) + 1;
                    int gk = t.Owner[hit.Frame];
                    int f = hit.Frame + Math.Min(hit.Length - 1, SymptomDetectors.GoalkeeperCrowdPersistTicks);
                    if (!SymptomDetectors.GoalkeeperCrowdPersisting(t).Any(x => x.Frame == hit.Frame))
                    {
                        f = -1;
                    }

                    for (int p = 0; p < t.Players && f >= 0; p++)
                    {
                        if (p != gk && t.Team[p] == t.Team[gk] && t.On(f, p) && Vec2.Distance(t.Pos(f, p), t.Pos(f, gk)) < SymptomDetectors.GoalkeeperCrowdRadius)
                        {
                            string a = t.Action[t.Slot(f, p)] < 0 ? "none" : ((PlayerAction)t.Action[t.Slot(f, p)]).ToString();
                            string k2 = "BN mate@+10 persistente " + t.Role[p] + "/" + t.State[t.Slot(f, p)] + "/" + a;
                            h[k2] = h.GetValueOrDefault(k2) + 1;
                            var tg = result.Trace!.TargetAt(f, p);
                            string where = Pitch.IsInArea(tg, t.Team[gk]) ? "inArea" : (Vec2.Distance(tg, t.Pos(f, gk)) < SymptomDetectors.GoalkeeperCrowdRadius ? "near<2" : "away");
                            string inNow = Pitch.IsInArea(t.Pos(f, gk), t.Team[gk]) ? "gkInArea" : "gkOut";
                            string k4 = "BN target " + a + " " + where + " " + inNow;
                            h[k4] = h.GetValueOrDefault(k4) + 1;
                        }
                    }

                    foreach (int off in new[] { 0, 3, 6, 10, 15 })
                    {
                        if (off >= hit.Length)
                        {
                            continue;
                        }

                        int ff = hit.Frame + off;
                        int m = 0;
                        for (int p = 0; p < t.Players; p++)
                        {
                            if (p != gk && t.Team[p] == t.Team[gk] && t.On(ff, p) && Vec2.Distance(t.Pos(ff, p), t.Pos(ff, gk)) < SymptomDetectors.GoalkeeperCrowdRadius)
                            {
                                m++;
                            }
                        }

                        string kc = $"BN crowd@{off:00} {(m >= 2 ? ">=2" : "<2")}";
                        h[kc] = h.GetValueOrDefault(kc) + 1;
                    }

                    int f0 = Math.Max(0, hit.Frame - 1);
                    for (int p = 0; p < t.Players; p++)
                    {
                        if (p != gk && t.Team[p] == t.Team[gk] && t.On(hit.Frame, p) && Vec2.Distance(t.Pos(hit.Frame, p), t.Pos(hit.Frame, gk)) < SymptomDetectors.GoalkeeperCrowdRadius)
                        {
                            string a0 = t.Action[t.Slot(f0, p)] < 0 ? "none" : ((PlayerAction)t.Action[t.Slot(f0, p)]).ToString();
                            string k5 = "BN at-catch prev " + t.Role[p] + "/" + a0 + (Pitch.IsInArea(t.Pos(hit.Frame, p), t.Team[gk]) ? " inArea" : " out");
                            h[k5] = h.GetValueOrDefault(k5) + 1;
                        }
                    }

                    string k3 = "BN length " + (hit.Length >= 20 ? ">=20" : "<20");
                    h[k3] = h.GetValueOrDefault(k3) + 1;
                }

                hist[i] = h;
            });
            sb.AppendLine(CultureInfo.InvariantCulture, $"{variant} {kind}: BO-A {Rate(bo)}  BN-A {Rate(bn)}");
            foreach (var g in hist.SelectMany(x => x).GroupBy(x => x.Key).Select(g => (g.Key, Sum: g.Sum(x => x.Value))).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"   {g.Key} = {g.Sum}");
            }
        }

        _output.WriteLine(sb.ToString());
        File.WriteAllText(Environment.GetEnvironmentVariable("UL_PROBE_OUT") ?? Path.Combine(Path.GetTempPath(), "probe.txt"), sb.ToString());
    }

    private static string Rate(int[] c)
    {
        double m = c.Average();
        double se = Math.Sqrt(c.Sum(x => (x - m) * (x - m)) / (c.Length - 1) / c.Length);
        return string.Create(CultureInfo.InvariantCulture, $"{m:0.000} ± {se:0.000}");
    }

    internal static Catalog Variant(Catalog c, string variant) => variant switch
    {
        "noescape" => c with { Tuning = c.Tuning with { Tackle = c.Tuning.Tackle with { EscapeBeyondDecisionReach = false } } },
        "nohold" => Underleague.Sim.Tests.Engine.OscillationProbeTests.WithHold(c, 0),
        "nobvb" => c with { Tuning = c.Tuning with { Tackle = c.Tuning.Tackle with { FollowVictimWhileTackling = false, WhistledFoulDownsVictim = false, EscapeBeyondDecisionReach = false } } },
        "none" => Variant(Variant(c, "nohold"), "nobvb"),
        _ => c,
    };
}
