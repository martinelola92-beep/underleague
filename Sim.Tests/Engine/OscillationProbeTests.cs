using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BV-A H8 (docs/pendientes/BV-A.md): trayectorias que oscilan en <c>/Sim</c>. Un jugador a velocidad de
/// carrera invierte el rumbo y lo vuelve a invertir en pocos ticks: va y viene como un indeciso.
///
/// <para><b>El instrumento.</b> Una <i>inversión</i> es un par de pasos consecutivos (desplazamiento de un
/// fotograma de la traza) los dos de al menos <see cref="RunStepCells"/> casillas y con más de 135° entre
/// ellos (coseno &lt; −0,707). Se <i>deshace</i> si en los <see cref="UndoTicks"/> pasos siguientes hay uno de
/// más de <see cref="MinStepCells"/> casillas que vuelve a formar más de 135° con el paso de después de la
/// inversión. Para cada inversión se clasifica la causa con lo que la traza sabe del tick: la acción de la
/// decisión vigente, el destino (<c>TargetPoint</c>), si el paso va hacia el destino y si el balón vuela.</para>
/// </summary>
public sealed class OscillationProbeTests
{
    internal const float RunStepCells = 0.09f;
    internal const float MinStepCells = 0.02f;
    internal const int UndoTicks = 4;
    private const float CosReverse = -0.70711f;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public OscillationProbeTests(ITestOutputHelper output) => _output = output;

    /// <summary>Causa de una inversión, por orden de prioridad.</summary>
    internal enum Cause
    {
        /// <summary>La decisión cambió de acción entre los dos pasos.</summary>
        ActionSwitch,

        /// <summary>Misma acción, pero el destino saltó más de 0,25 casillas.</summary>
        TargetJump,

        /// <summary>Misma acción y destino casi quieto; el primer paso llegó al destino y el segundo vuelve a él (rebasar/llegada).</summary>
        Arrival,

        /// <summary>El segundo paso no va hacia el destino (empuje de separación, acotado a zona/banda/área).</summary>
        AgainstTarget,

        /// <summary>Misma acción, el destino se movió poco y el paso va hacia él: deriva del destino.</summary>
        TargetDrift,

        /// <summary>Fuera de estados de decisión (reanudación, estado con contador...).</summary>
        Other,
    }

    internal sealed class Census
    {
        public int Matches;
        public long PlayerFrames;
        public int Reversals;
        public int Undone;
        public readonly int[] ByCause = new int[6];
        public readonly int[] UndoneByCause = new int[6];
        public readonly Dictionary<string, int> ByAction = new();
        public readonly Dictionary<string, int> UndoneByAction = new();
        public readonly Dictionary<string, int> SwitchPairs = new();
        public readonly Dictionary<string, int> UndoneSwitchPairs = new();
        public int BallInFlight;
        public int UndoneBallInFlight;
        public int StopGo;
        public long RunningSteps;

        public double UndonePercent => Reversals == 0 ? 0 : 100.0 * Undone / Reversals;

        public double PerPlayerSecond => PlayerFrames == 0 ? 0 : Reversals / (PlayerFrames / 15.0);
    }

    internal static Census Measure(Catalog catalog, int firstSeed, int matches)
    {
        var census = new Census { Matches = matches };
        for (int m = 0; m < matches; m++)
        {
            ulong seed = (ulong)(firstSeed + m);
            var trace = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true }).Trace!;
            Accumulate(trace, census);
        }

        return census;
    }

    internal static void Accumulate(MatchTrace trace, Census census)
    {
        int n = trace.FrameCount;
        for (int player = 0; player < trace.Players.Count; player++)
        {
            for (int f = 2; f < n; f++)
            {
                if (!trace.OnPitchAt(f, player) || !trace.OnPitchAt(f - 1, player) || !trace.OnPitchAt(f - 2, player))
                {
                    continue;
                }

                census.PlayerFrames++;
                var p0 = trace.PositionAt(f - 2, player);
                var p1 = trace.PositionAt(f - 1, player);
                var p2 = trace.PositionAt(f, player);
                var a = p1 - p0;
                var b = p2 - p1;
                float la = a.Length, lb = b.Length;

                if (lb >= RunStepCells)
                {
                    census.RunningSteps++;
                }

                // Paso-0-paso: carrera, parada de un tick, carrera.
                if (la >= RunStepCells && lb < MinStepCells && f + 1 < n)
                {
                    var c = trace.PositionAt(f + 1, player) - p2;
                    if (c.Length >= RunStepCells && trace.OnPitchAt(f + 1, player))
                    {
                        census.StopGo++;
                    }
                }

                if (!IsReversal(a, b))
                {
                    continue;
                }

                census.Reversals++;
                bool undone = false;
                for (int k = 1; k <= UndoTicks && f + k < n; k++)
                {
                    if (!trace.OnPitchAt(f + k, player))
                    {
                        break;
                    }

                    var s = trace.PositionAt(f + k, player) - trace.PositionAt(f + k - 1, player);
                    if (Undoes(s, b))
                    {
                        undone = true;
                        break;
                    }
                }

                var cause = Classify(trace, f, player, p1, b);
                var action = trace.ActionAt(f, player);
                string key = $"{trace.StateAt(f, player)}/{(action is null ? "-" : action.ToString())}";
                census.ByCause[(int)cause]++;
                Bump(census.ByAction, key);
                string pair = $"{trace.ActionAt(f - 1, player)}->{action}";
                bool flight = trace.BallInFlightAt(f);
                if (cause is Cause.TargetJump or Cause.Other)
                {
                    string jk = cause + ":" + key + (flight ? " vuelo" : "");
                    Bump(census.SwitchPairs, jk);
                    if (undone)
                    {
                        Bump(census.UndoneSwitchPairs, jk);
                    }
                }

                if (cause == Cause.ActionSwitch)
                {
                    Bump(census.SwitchPairs, pair);
                    if (undone)
                    {
                        Bump(census.UndoneSwitchPairs, pair);
                    }
                }

                if (flight)
                {
                    census.BallInFlight++;
                }

                if (undone)
                {
                    census.Undone++;
                    census.UndoneByCause[(int)cause]++;
                    Bump(census.UndoneByAction, key);
                    if (flight)
                    {
                        census.UndoneBallInFlight++;
                    }
                }
            }
        }
    }

    private static Cause Classify(MatchTrace trace, int f, int player, Vec2 p1, Vec2 b)
    {
        var state = trace.StateAt(f, player);
        if (state is not (PlayerState.Positioning or PlayerState.Chasing or PlayerState.Dribbling)
            || trace.RestartAt(f) != RestartKind.None)
        {
            return Cause.Other;
        }

        if (trace.ActionAt(f, player) != trace.ActionAt(f - 1, player) || trace.StateAt(f - 1, player) != state)
        {
            return Cause.ActionSwitch;
        }

        var t1 = trace.TargetAt(f - 1, player);
        var t2 = trace.TargetAt(f, player);
        if (Vec2.Distance(t1, t2) > 0.25f)
        {
            return Cause.TargetJump;
        }

        var toTarget = t2 - p1;
        if (Dot(toTarget, b) <= 0f)
        {
            return Cause.AgainstTarget;
        }

        if (Vec2.Distance(p1, t1) < 0.02f)
        {
            return Cause.Arrival;
        }

        return Cause.TargetDrift;
    }

    private static float Dot(Vec2 a, Vec2 b) => (a.X * b.X) + (a.Y * b.Y);

    /// <summary>Dos pasos consecutivos a velocidad de carrera con más de 135° entre ellos.</summary>
    internal static bool IsReversal(Vec2 a, Vec2 b) =>
        a.Length >= RunStepCells && b.Length >= RunStepCells && Dot(a, b) / (a.Length * b.Length) < CosReverse;

    /// <summary>Un paso posterior que deshace la inversión cuyo paso de salida fue <paramref name="after"/>.</summary>
    internal static bool Undoes(Vec2 step, Vec2 after) =>
        step.Length > MinStepCells && Dot(step, after) / (step.Length * after.Length) < CosReverse;

    private static void Bump(Dictionary<string, int> d, string key) => d[key] = d.TryGetValue(key, out int v) ? v + 1 : 1;

    internal static void Report(ITestOutputHelper output, string label, Census c)
    {
        output.WriteLine(
            $"{label}: partidos {c.Matches} · inversiones {c.Reversals} ({c.PerPlayerSecond:F3}/s por jugador) · " +
            $"deshechas ≤{UndoTicks} {c.Undone} ({c.UndonePercent:F1} %) · balón en vuelo {c.BallInFlight} (deshechas {c.UndoneBallInFlight}) · " +
            $"paso-0-paso {c.StopGo} · pasos de carrera {c.RunningSteps}");
        foreach (Cause cause in Enum.GetValues<Cause>())
        {
            output.WriteLine($"  causa {cause}: {c.ByCause[(int)cause]} (deshechas {c.UndoneByCause[(int)cause]})");
        }

        foreach (var kv in c.SwitchPairs.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(24))
        {
            output.WriteLine($"  cambio {kv.Key}: {kv.Value} (deshechas {(c.UndoneSwitchPairs.TryGetValue(kv.Key, out int u) ? u : 0)})");
        }

        foreach (var kv in c.ByAction.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(12))
        {
            output.WriteLine($"  {kv.Key}: {kv.Value} (deshechas {(c.UndoneByAction.TryGetValue(kv.Key, out int u) ? u : 0)})");
        }
    }

    internal static Catalog WithHold(Catalog catalog, int hold) =>
        catalog with { Ai = catalog.Ai.WithContext(catalog.Ai.Context with { PositioningHoldBonus = hold }) };

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void DumpOfRetreatCoverFlips()
    {
        int shown = 0;
        for (ulong seed = 1; seed <= 3 && shown < 6; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var trace = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true }).Trace!;
            for (int player = 0; player < trace.Players.Count && shown < 6; player++)
            {
                for (int f = 2; f + 4 < trace.FrameCount && shown < 6; f++)
                {
                    var a = trace.PositionAt(f - 1, player) - trace.PositionAt(f - 2, player);
                    var b = trace.PositionAt(f, player) - trace.PositionAt(f - 1, player);
                    if (a.Length < RunStepCells || b.Length < RunStepCells || Dot(a, b) / (a.Length * b.Length) >= CosReverse)
                    {
                        continue;
                    }

                    var before = trace.ActionAt(f - 1, player);
                    var after = trace.ActionAt(f, player);
                    if (!((before == PlayerAction.Retreat && after == PlayerAction.CoverSpace) || (before == PlayerAction.CoverSpace && after == PlayerAction.Retreat)))
                    {
                        continue;
                    }

                    shown++;
                    int id = trace.Players[player].Id;
                    _output.WriteLine($"semilla {seed} jugador {id} ({trace.Players[player].Role}) fotograma {f} tick {trace.TickAt(f)}: {before} -> {after}");
                    for (int k = -4; k <= 4; k++)
                    {
                        int g = f + k;
                        var zone = trace.ZoneAt(g, player);
                        _output.WriteLine($"   f{k:+0;-0;0} pos {trace.PositionAt(g, player)} acción {trace.ActionAt(g, player)} destino {trace.TargetAt(g, player)} hogar {zone.Home} balón {trace.BallAt(g)} dueño {trace.BallOwnerAt(g)} vuelo {trace.BallInFlightAt(g)}");
                    }

                    for (int k = -3; k <= 2; k++)
                    {
                        int tick = trace.TickAt(f + k);
                        var dump = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { DumpUtility = (id, tick) }).Report.UtilityDump;
                        if (dump is null || dump.Tick != tick)
                        {
                            continue;
                        }

                        var rows = string.Join(" ", dump.Rows.Where(r => !r.Rejected).OrderByDescending(r => r.Score).Take(4).Select(r => $"{r.Action}={r.Score}(b{r.Base}·t{r.TacticalMultiplier}+c{r.Context})"));
                        _output.WriteLine($"   volcado tick {dump.Tick}: elige {dump.Chosen} · {rows}");
                    }
                }
            }
        }
    }

    /// <summary>ADR 0184: el partido brutal de la ADR 0158 §3 con y sin sostenida (criterio final del árbitro).</summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void BrutalMatchBiasWithAndWithoutTheHold()
    {
        foreach (int hold in new[] { 0, 40 })
        {
            var catalog = WithHold(Catalog, hold);
            var setup = TestMatches.Brutal(catalog);
            foreach (int seeds in new[] { 60, 150 })
            {
                int over30 = 0;
                long sumAbs = 0;
                for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                {
                    int abs = Math.Abs(Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false)).Report.FinalBias);
                    sumAbs += abs;
                    over30 += abs > 30 ? 1 : 0;
                }

                _output.WriteLine($"hold {hold}, {seeds} semillas: > ±30 en {over30} ({100.0 * over30 / seeds:F1} %), |criterio| medio {(double)sumAbs / seeds:F1}");
            }
        }
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void SweepOfTheHold()
    {
        foreach (int hold in new[] { 0, 30, 40, 50, 60, 100 })
        {
            Report(_output, $"hold {hold}", Measure(WithHold(Catalog, hold), 1, 40));
        }
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void MarginOfActionSwitches()
    {
        var census = new UtilityCensus();
        for (int m = 1; m <= 40; m++)
        {
            ulong seed = (ulong)m;
            Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Census = census });
        }

        long decisions = census.DecisionsTotal();
        long switches = 0;
        foreach (long v in census.Switches)
        {
            switches += v;
        }

        _output.WriteLine($"decisiones {decisions} · cambios de acción {switches} ({100.0 * switches / decisions:F1} %)");
        var actions = Enum.GetValues<PlayerAction>();
        var pairs = new List<(long n, string k)>();
        foreach (var a in actions)
        {
            foreach (var b in actions)
            {
                long n = census.Switches[(int)a, (int)b];
                if (n > 0)
                {
                    pairs.Add((n, $"{a}->{b}"));
                }
            }
        }

        foreach (var (n, k) in pairs.OrderByDescending(x => x.n).ThenBy(x => x.k, StringComparer.Ordinal).Take(14))
        {
            _output.WriteLine($"  {k}: {n}");
        }

        long outer = 0;
        foreach (long v in census.SwitchesFromOuterLimit)
        {
            outer += v;
        }

        long other = 0;
        foreach (long v in census.SwitchesFromDiscard)
        {
            other += v;
        }

        _output.WriteLine($"cambios forzados por el límite exterior {outer} · por otro descarte {other}");
        foreach (var (a, b) in new[] { (PlayerAction.CoverSpace, PlayerAction.Retreat), (PlayerAction.Retreat, PlayerAction.CoverSpace), (PlayerAction.FindSpace, PlayerAction.Retreat), (PlayerAction.CoverSpace, PlayerAction.FindSpace), (PlayerAction.FindSpace, PlayerAction.CoverSpace), (PlayerAction.ShortPass, PlayerAction.FindSpace), (PlayerAction.Retreat, PlayerAction.FindSpace) })
        {
            _output.WriteLine($"  {a}->{b}: {census.Switches[(int)a, (int)b]} de los que por límite exterior {census.SwitchesFromOuterLimit[(int)a, (int)b]}, por otro descarte {census.SwitchesFromDiscard[(int)a, (int)b]}");
        }

        long total = census.SwitchMargin.Sum();
        long acc = 0;
        for (int i = 0; i < UtilityCensus.SwitchMarginBuckets; i++)
        {
            acc += census.SwitchMargin[i];
            if (i < 12 || i % 5 == 0)
            {
                _output.WriteLine($"  margen < {(i + 1) * UtilityCensus.SwitchMarginBucket}: {100.0 * acc / total:F1} % acumulado");
            }
        }

        Assert.True(switches > 0);
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void CensusOfReversals()
    {
        int matches = int.TryParse(Environment.GetEnvironmentVariable("UNDERLEAGUE_PROBE_MATCHES"), out int m) ? m : 40;
        var c = Measure(Catalog, 1, matches);
        Report(_output, "referencia", c);
        Assert.True(c.Reversals > 0);
    }
}
