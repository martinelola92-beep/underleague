using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BV-B (i) (docs/pendientes/BV-B.md): entradas que «golpean al aire». En la retransmisión, 11 de 28 entradas se
/// resolvían con los dos a más de 0,9 casillas: se deciden a ~0,7 y durante los <c>TacklingTicks</c> el rival se aleja.
///
/// <para><b>El instrumento.</b> Para cada suceso <c>TACKLE</c> resuelto (ganada, fallada, falta, sin balón) se mide la
/// distancia entre quien entra y quien la recibe en el fotograma del suceso (el tick de la resolución) y en el
/// fotograma de la decisión (<c>TacklingTicks</c> antes). Validado contra un caso de respuesta conocida: en una entrada
/// ganada, quien la recibe está derribado en ese mismo fotograma.</para>
/// </summary>
public sealed class TackleReachTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public TackleReachTests(ITestOutputHelper output) => _output = output;

    internal sealed record Sample(float AtResolution, float AtDecision, string Detail, bool VictimDownIfWon, float BeforeResolving);

    internal static List<Sample> Measure(Catalog catalog, int firstSeed, int matches)
    {
        var samples = new List<Sample>();
        int tacklingTicks = catalog.Tuning.States.TacklingTicks;
        for (int m = 0; m < matches; m++)
        {
            ulong seed = (ulong)(firstSeed + m);
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var byId = new Dictionary<int, int>();
            for (int i = 0; i < trace.Players.Count; i++)
            {
                byId[trace.Players[i].Id] = i;
            }

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Tackle || e.Detail == "attempted" || e.Detail.StartsWith("block", StringComparison.Ordinal)
                    || !byId.TryGetValue(e.Actor, out int tackler) || !byId.TryGetValue(e.Opponent, out int victim))
                {
                    continue;
                }

                int f = trace.FrameOfTick(e.Tick);
                int d = trace.FrameOfTick(e.Tick - tacklingTicks);
                if (f < 0 || d < 0 || !trace.OnPitchAt(f, victim))
                {
                    continue;
                }

                float atResolution = Vec2.Distance(trace.PositionAt(f, tackler), trace.PositionAt(f, victim));
                float atDecision = Vec2.Distance(trace.PositionAt(d, tackler), trace.PositionAt(d, victim));
                bool down = e.Detail != "won" || trace.StateAt(f, victim) == PlayerState.KnockedDown;
                float before = Vec2.Distance(trace.PositionAt(f - 1, tackler), trace.PositionAt(f - 1, victim));
                samples.Add(new Sample(atResolution, atDecision, e.Detail, down, before));
            }
        }

        return samples;
    }

    internal static float Percentile(List<float> values, int p)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0f : sorted[Math.Min(sorted.Count - 1, sorted.Count * p / 100)];
    }

    internal static void Report(ITestOutputHelper output, string label, List<Sample> s)
    {
        var res = s.Select(x => x.AtResolution).ToList();
        var dec = s.Select(x => x.AtDecision).ToList();
        int far = s.Count(x => x.AtResolution > 0.9f);
        output.WriteLine(
            $"{label}: entradas {s.Count} · al resolver p50 {Percentile(res, 50):F2} p90 {Percentile(res, 90):F2} · al decidir p50 {Percentile(dec, 50):F2} · "
            + $"> 0,9 casillas {far} ({100.0 * far / Math.Max(1, s.Count):F1} %) · al empezar el tick de la resolución p50 {Percentile(s.Select(x => x.BeforeResolving).ToList(), 50):F2}, > 0,9 {s.Count(x => x.BeforeResolving > 0.9f)} · ganadas sin derribo {s.Count(x => !x.VictimDownIfWon)}");
        foreach (var g in s.GroupBy(x => x.Detail).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            output.WriteLine($"  {g.Key}: {g.Count()} · > 0,9: {g.Count(x => x.AtResolution > 0.9f)} · al decidir p50 {Percentile(g.Select(x => x.AtDecision).ToList(), 50):F2} · al resolver p50 {Percentile(g.Select(x => x.AtResolution).ToList(), 50):F2} p90 {Percentile(g.Select(x => x.AtResolution).ToList(), 90):F2}");
        }
    }

    /// <summary>
    /// BV-B (i), ADR 0186: quien entra sigue a quien la recibe durante <c>Tackling</c>. Medido en 100 partidos de
    /// referencia, al empezar el tick de la resolución: sin seguirle, p50 0,75 casillas y 294 de 1.232 entradas a más
    /// de 0,9; siguiéndole hasta 0,6 casillas, p50 0,64 y 14 de 1.269. En el fotograma del suceso (que ya incluye el paso que da la
    /// víctima después de la resolución, en el mismo tick): 58,6 % → 16,2 % a más de 0,9.
    /// </summary>
    [Fact]
    public void TacklesAreResolvedAtLegReach()
    {
        var s = Measure(Catalog, 1, 100);
        Report(_output, "datos", s);
        int farBefore = s.Count(x => x.BeforeResolving > 0.9f);
        int farAtEvent = s.Count(x => x.AtResolution > 0.9f);
        Assert.True(s.Count > 500, $"muestra escasa: {s.Count} entradas");
        Assert.True(farBefore * 100 < s.Count * 4, $"entradas resueltas a más de 0,9 al empezar el tick: {farBefore} de {s.Count} (tope 4 %; sin seguir a la víctima, 24 %)");
        Assert.True(farAtEvent * 100 < s.Count * 20, $"entradas a más de 0,9 en el fotograma del suceso: {farAtEvent} de {s.Count} (tope 20 %; sin seguir a la víctima, 59 %)");

        // Control: con la regla apagada, el instrumento ve las entradas al aire de BV-B.
        var off = Measure(With(follow: false, foulDownsVictim: true), 1, 100);
        Report(_output, "sin seguir a la víctima", off);
        Assert.True(off.Count(x => x.BeforeResolving > 0.9f) * 100 > off.Count * 15, "control: sin seguir a la víctima debían verse entradas resueltas a más de 0,9");
    }

    private static Catalog With(bool follow, bool foulDownsVictim) =>
        Catalog with
        {
            Tuning = Catalog.Tuning with
            {
                Tackle = Catalog.Tuning.Tackle with { FollowVictimWhileTackling = follow, WhistledFoulDownsVictim = foulDownsVictim },
            },
        };

    /// <summary>
    /// BV-B (ii), ADR 0186: en una falta PITADA cae quien la recibe, y quien la comete sólo si fue dura (plancha).
    /// Valor conocido: antes del cambio la víctima de una falta pitada no caía nunca y el infractor caía siempre.
    /// </summary>
    [Fact]
    public void TheVictimOfAWhistledFoulGoesDown()
    {
        var (fouls, victimsDown, offendersDown) = WhistledFouls(Catalog);
        _output.WriteLine($"faltas pitadas {fouls}: víctima en el suelo {victimsDown}, infractor en el suelo {offendersDown}");
        Assert.True(fouls > 50, $"muestra escasa: {fouls} faltas pitadas");
        Assert.Equal(fouls, victimsDown);
        Assert.True(offendersDown < fouls, "el infractor sólo cae si la entrada fue dura: no pueden caer todos");
        Assert.True(offendersDown > 0, "las entradas duras siguen tirando al infractor");

        // Control: con la regla apagada, el motor de antes (la víctima de pie, el infractor siempre al suelo).
        var (offFouls, offVictims, offOffenders) = WhistledFouls(With(follow: true, foulDownsVictim: false));
        _output.WriteLine($"regla apagada: faltas pitadas {offFouls}: víctima en el suelo {offVictims}, infractor en el suelo {offOffenders}");
        Assert.True(offVictims * 10 < offFouls, "control: con la regla apagada la víctima casi nunca está en el suelo");
        Assert.Equal(offFouls, offOffenders);
    }

    private static (int Fouls, int VictimsDown, int OffendersDown) WhistledFouls(Catalog catalog)
    {
        int fouls = 0, victimsDown = 0, offendersDown = 0;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var byId = new Dictionary<int, int>();
            for (int i = 0; i < trace.Players.Count; i++)
            {
                byId[trace.Players[i].Id] = i;
            }

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Foul || e.Detail != "foul"
                    || !byId.TryGetValue(e.Actor, out int offender) || !byId.TryGetValue(e.Opponent, out int victim))
                {
                    continue;
                }

                int f = trace.FrameOfTick(e.Tick);
                if (f < 0 || !trace.OnPitchAt(f, victim) || !trace.OnPitchAt(f, offender))
                {
                    continue;
                }

                fouls++;
                victimsDown += trace.StateAt(f, victim) is PlayerState.KnockedDown or PlayerState.Injured ? 1 : 0;
                offendersDown += trace.StateAt(f, offender) is PlayerState.KnockedDown or PlayerState.Injured ? 1 : 0;
            }
        }

        return (fouls, victimsDown, offendersDown);
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void HardFoulsCensus()
    {
        int fouls = 0, traitHard = 0, strengthHard = 0;
        int threshold = Catalog.Tuning.Tackle.HardTackleThreshold;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var defs = setup.Home.Players.Concat(setup.Away.Players).ToDictionary(p => p.Id);
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            foreach (var e in result.Events.Where(e => e.Type == EventType.Foul && e.Detail == "foul"))
            {
                if (!defs.TryGetValue(e.Actor, out var a) || !defs.TryGetValue(e.Opponent, out var v))
                {
                    continue;
                }

                fouls++;
                bool t = a.Traits.Contains(Trait.Aggressive) || a.Traits.Contains(Trait.Dirty);
                traitHard += t ? 1 : 0;
                strengthHard += !t && (a.Attributes.Strength - v.Attributes.Strength) * 100 >= threshold ? 1 : 0;
            }
        }

        _output.WriteLine($"faltas pitadas {fouls}: duras por rasgo {traitHard}, por fuerza (sin rasgo, atributo base) {strengthHard}");
    }

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void CensusOfTackleReach()
    {
        Report(_output, "datos", Measure(Catalog, 1, 100));
    }
}
