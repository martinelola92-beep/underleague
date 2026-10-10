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

    internal sealed record Sample(float AtResolution, float AtDecision, string Detail, bool VictimDownIfWon, float BeforeResolving, float VictimRan = 0f, float TacklerRan = 0f, bool VictimHadBall = false);

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
                // Lo que corrieron los dos durante la entrada (de la decisión al principio del tick de la resolución).
                float victimRan = Vec2.Distance(trace.PositionAt(d, victim), trace.PositionAt(f - 1, victim));
                float tacklerRan = Vec2.Distance(trace.PositionAt(d, tackler), trace.PositionAt(f - 1, tackler));
                bool hadBall = trace.BallOwnerAt(d) == victim;
                samples.Add(new Sample(atResolution, atDecision, e.Detail, down, before, victimRan, tacklerRan, hadBall));
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

        // Control: con la regla apagada, el instrumento ve las entradas al aire de BV-B. Sobre el motor sin arranque (ADR 0185),
        // donde se midió BV-B: con el arranque la víctima sale más despacio y se aleja menos, otro remedio del mismo síntoma
        // (medido con accelTicks 3 y el arranque acotado al juego abierto: 192 de 1.338, el 14,3 %, contra el 24 % de antes).
        var off = Measure(AccelerationTests.WithAccel(With(follow: false, foulDownsVictim: true), 0), 1, 100);
        Report(_output, "sin seguir a la víctima", off);
        // Listón del control al 10 % (10 oct 2026, BX-10): con la intercepción en la máxima aproximación las semillas dan
        // 152 de 1.039, el 14,6 %, contra el 0,8 % con la regla puesta; el control sigue separando los dos casos por 18×.
        Assert.True(off.Count(x => x.BeforeResolving > 0.9f) * 100 > off.Count * 10, "control: sin seguir a la víctima debían verse entradas resueltas a más de 0,9");
    }

    /// <summary>
    /// ADR 0186, enmienda (valor conocido): una víctima que se ha escapado más allá del alcance con el que se decidió
    /// la entrada (<c>tackleDistanceMaxCells</c>) la deja en nada: ni suceso ni contacto. Con el dato apagado, el
    /// margen de 0,3 de antes la alcanza a la misma distancia.
    /// </summary>
    [Fact]
    public void AVictimWhoEscapedTheDecisionReachMakesTheTackleFail()
    {
        float decisionReach = Catalog.Ai.Context.TackleDistanceMaxCells;
        Assert.True(Catalog.Tuning.Tackle.EscapeBeyondDecisionReach, "los datos reales debían traer la regla encendida");
        Assert.Equal(0, TackleEventsAt(Catalog, decisionReach + 0.15f));
        Assert.Equal(1, TackleEventsAt(Catalog, decisionReach - 0.15f));

        var off = Catalog with { Tuning = Catalog.Tuning with { Tackle = Catalog.Tuning.Tackle with { EscapeBeyondDecisionReach = false } } };
        Assert.Equal(1, TackleEventsAt(off, decisionReach + 0.15f));
    }

    /// <summary>
    /// ADR 0186 (valor conocido, revisión independiente): quien entra sigue a SU velocidad de movimiento. Una víctima
    /// que corre más que él durante los <c>TacklingTicks</c> sale del alcance de la decisión y la entrada falla; una que
    /// corre menos es alcanzada y la entrada se resuelve.
    /// </summary>
    [Fact]
    public void AFasterVictimOutrunsTheTackleAndASlowerOneIsCaught()
    {
        var engine = new MatchEngine(TestMatches.Reference(Catalog, 5), 5, Catalog, SimConfig.Default);
        int tackler = engine.OutfieldIndexForTest(0, 0);
        float own = engine.SpeedPerTickMilliForTest(tackler) / 1000f;

        Assert.Equal(0, ChaseAndResolve(own + 0.06f));
        Assert.Equal(1, ChaseAndResolve(own - 0.06f));
    }

    /// <summary>La víctima arranca a 0,9 y huye en línea recta a <paramref name="victimStep"/> casillas por tick durante tres ticks.</summary>
    private static int ChaseAndResolve(float victimStep)
    {
        var engine = new MatchEngine(TestMatches.Reference(Catalog, 5), 5, Catalog, SimConfig.Default);
        int victim = engine.OutfieldIndexForTest(1, 5);
        int tackler = engine.OutfieldIndexForTest(0, 0);
        var at = new Vec2(7f, 3.5f);
        engine.GiveBallForTest(victim, at);
        engine.PlaceForTest(tackler, new Vec2(at.X - 0.9f, at.Y));
        for (int tick = 0; tick < Catalog.Tuning.States.TacklingTicks; tick++)
        {
            at = new Vec2(at.X + victimStep, at.Y);
            engine.PlaceForTest(victim, at);
            engine.TacklingStepForTest(tackler, victim);
        }

        int before = engine.EventsForTest.Count;
        engine.ResolveTackleForTest(tackler, victim);
        return engine.EventsForTest.Skip(before).Count(e => e.Type == EventType.Tackle && e.Detail != "attempted");
    }

    /// <summary>Sucesos TACKLE resueltos de un defensa local sobre el portador rival a <paramref name="gap"/> casillas.</summary>
    private static int TackleEventsAt(Catalog catalog, float gap)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        int victim = engine.OutfieldIndexForTest(1, 5);
        int tackler = engine.OutfieldIndexForTest(0, 0);
        engine.GiveBallForTest(victim, new Vec2(6f, 3.5f));
        engine.PlaceForTest(tackler, new Vec2(6f - gap, 3.5f));
        int before = engine.EventsForTest.Count;
        engine.ResolveTackleForTest(tackler, victim);
        return engine.EventsForTest.Skip(before).Count(e => e.Type == EventType.Tackle && e.Detail != "attempted");
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
        // El control cuenta sólo las faltas de ENTRADA: en un bloqueo que gana y además es falta («blockFoul») la víctima cae
        // por el propio bloqueo (ResolveBlock la derriba si gana, ADR 0030 §2), no por la regla de la ADR 0186. Medido con el
        // arranque de la ADR 0185 (más bloqueos): con la regla apagada, 29 de las 34 víctimas en el suelo venían de un
        // bloqueo ganado, y el control mezclaba los dos mecanismos (34 de 325, al borde del 10 %).
        var (offFouls, offVictims, offOffenders) = WhistledFouls(With(follow: true, foulDownsVictim: false), excludeBlocks: true);
        _output.WriteLine($"regla apagada (sin bloqueos): faltas pitadas {offFouls}: víctima en el suelo {offVictims}, infractor en el suelo {offOffenders}");
        Assert.True(offFouls > 50, $"muestra escasa en el control: {offFouls} faltas de entrada pitadas");
        Assert.True(offVictims * 10 < offFouls, "control: con la regla apagada la víctima casi nunca está en el suelo");
        Assert.Equal(offFouls, offOffenders);
    }

    /// <summary>
    /// ADR 0186 (revisión independiente), falta a falta en 100 partidos de referencia:
    /// <list type="bullet">
    /// <item>el infractor cae si y sólo si la entrada fue dura. Con las plantillas de referencia, toda falta de un jugador con
    /// <c>Aggressive</c>/<c>Dirty</c> lo deja en el suelo. Y en las mismas plantillas SIN esos rasgos y con el umbral de
    /// fuerza fuera de alcance (ninguna entrada es dura), ningún infractor cae;</item>
    /// <item>si la víctima llevaba el balón, al caer lo suelta (<c>ParkBall</c>): no queda dueño de un balón en el suelo;</item>
    /// <item>un derribado no saca la falta: en ningún fotograma de saque de falta el sacador está en el suelo.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void FoulByFoulTheHardOnesTopplesTheOffenderAndTheBallIsDropped()
    {
        var hard = CheckFouls(Catalog, stripTraits: false);
        var soft = CheckFouls(
            Catalog with { Tuning = Catalog.Tuning with { Tackle = Catalog.Tuning.Tackle with { HardTackleThreshold = 100_000 } } },
            stripTraits: true);
        _output.WriteLine($"con rasgo: faltas {hard.Fouls}, infractores en el suelo {hard.OffendersDown}, víctimas con balón {hard.Carriers}, fotogramas de saque de falta {hard.FreeKickFrames}");
        _output.WriteLine($"sin entradas duras: faltas {soft.Fouls}, infractores en el suelo {soft.OffendersDown}");

        Assert.True(hard.Fouls > 50 && soft.Fouls > 50 && hard.Carriers > 20 && hard.FreeKickFrames > 100, "muestra escasa");
        Assert.Equal(hard.Fouls, hard.OffendersDown);
        Assert.Equal(0, soft.OffendersDown);
    }

    private sealed record FoulCheck(int Fouls, int OffendersDown, int Carriers, int FreeKickFrames);

    /// <summary>
    /// Recorre las faltas pitadas. Con <paramref name="stripTraits"/> cuenta todas; sin él, sólo las de infractores con
    /// rasgo de entrada dura. Comprueba de paso el balón soltado y que el sacador de una falta no está en el suelo.
    /// </summary>
    private static FoulCheck CheckFouls(Catalog catalog, bool stripTraits)
    {
        int fouls = 0, offendersDown = 0, carriers = 0, freeKickFrames = 0;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var setup = TestMatches.Reference(catalog, seed);
            if (stripTraits)
            {
                setup = setup with { Home = WithoutHardTraits(setup.Home), Away = WithoutHardTraits(setup.Away) };
            }

            var defs = setup.Home.Players.Concat(setup.Away.Players).ToDictionary(p => p.Id);
            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var byId = new Dictionary<int, int>();
            for (int i = 0; i < trace.Players.Count; i++)
            {
                byId[trace.Players[i].Id] = i;
            }

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Foul || e.Detail != "foul"
                    || !byId.TryGetValue(e.Actor, out int offender) || !byId.TryGetValue(e.Opponent, out int victim)
                    || !defs.TryGetValue(e.Actor, out var a))
                {
                    continue;
                }

                int f = trace.FrameOfTick(e.Tick);
                if (f < 1 || !trace.OnPitchAt(f, offender) || !trace.OnPitchAt(f, victim)
                    || trace.StateAt(f, offender) is PlayerState.Injured or PlayerState.SentOff)
                {
                    continue;
                }

                if (trace.BallOwnerAt(f - 1) == victim)
                {
                    carriers++;
                    Assert.NotEqual(victim, trace.BallOwnerAt(f));
                }

                bool trait = a.Traits.Contains(Trait.Aggressive) || a.Traits.Contains(Trait.Dirty);
                if (!stripTraits && !trait)
                {
                    continue;
                }

                fouls++;
                offendersDown += trace.StateAt(f, offender) == PlayerState.KnockedDown ? 1 : 0;
            }

            for (int g = 0; g < trace.FrameCount; g++)
            {
                if (trace.RestartAt(g) == RestartKind.FreeKick && trace.RestartTakerAt(g) >= 0)
                {
                    freeKickFrames++;
                    Assert.NotEqual(PlayerState.KnockedDown, trace.StateAt(g, trace.RestartTakerAt(g)));
                }
            }
        }

        return new FoulCheck(fouls, offendersDown, carriers, freeKickFrames);
    }

    private static TeamSetup WithoutHardTraits(TeamSetup team) =>
        team with
        {
            Players = team.Players
                .Select(p => p with { Traits = p.Traits.Where(t => t is not (Trait.Aggressive or Trait.Dirty)).ToList() })
                .ToList(),
        };

    private static (int Fouls, int VictimsDown, int OffendersDown) WhistledFouls(Catalog catalog, bool excludeBlocks = false)
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

            var events = result.Events;
            for (int k = 0; k < events.Count; k++)
            {
                var e = events[k];
                if (e.Type != EventType.Foul || e.Detail != "foul"
                    || !byId.TryGetValue(e.Actor, out int offender) || !byId.TryGetValue(e.Opponent, out int victim))
                {
                    continue;
                }

                // El suceso TACKLE que la precede (el mismo actor, el mismo tick) dice si fue un bloqueo.
                if (excludeBlocks && k > 0 && events[k - 1].Type == EventType.Tackle && events[k - 1].Detail == "blockFoul")
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

    /// <summary>
    /// ADR 0186 (elf_none): ¿escapa alguien por velocidad cuando quien entra le sigue a SU velocidad? Cuenta en cuántas
    /// entradas la víctima corrió más que quien entra durante <c>Tackling</c>, con y sin balón, y por raza de la víctima.
    /// </summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void WhoOutrunsTheTackler()
    {
        var s = Measure(Catalog, 1, 100);
        foreach (var g in s.GroupBy(x => x.VictimHadBall))
        {
            int faster = g.Count(x => x.VictimRan > x.TacklerRan + 0.01f);
            _output.WriteLine($"víctima con balón {g.Key}: entradas {g.Count()} · la víctima corre más que quien entra en {faster} · " +
                $"p50 víctima {Percentile(g.Select(x => x.VictimRan).ToList(), 50):F2} casillas, quien entra {Percentile(g.Select(x => x.TacklerRan).ToList(), 50):F2}");
        }
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
