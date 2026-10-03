using System.Globalization;
using System.Text;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Mobs;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0175 (RF-055b): la turba estrecha el campo una fila por lado (las exteriores, siempre las mismas) y sube un
/// 15 % la velocidad. El tiempo reglamentario queda byte a byte igual (RT-024); quien está en una fila invadida al
/// empezar la turba se aparta andando (RF-053, ADR 0143); el balón sale por el borde de la banda, no de la
/// cuadrícula. Las cifras son las provisionales de <c>tuning.mob</c>.
/// </summary>
public sealed class MobNarrowingTests
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    // Huellas con la turba sin tocar (semillas 1..60 de TestMatches.Reference). Medidas por primera vez antes del cambio, sobre
    // el árbol de la ADR 0167, y renovadas al rebasar sobre main (que movió el reglamentario por otras ADR); el test de abajo
    // exige además que el reglamentario coincida con el de la turba a 0/0, que es la prueba de que esta ADR no lo toca:
    // el conjunto de los partidos que no llegan a la turba, y el de TODOS los partidos.
    // Renovadas el 3 oct 2026 por la ADR 0184 (positioningHoldBonus 0 -> 50) y de la ADR 0186 (BV-B: seguir a la víctima, la falta tira a la víctima, escapar del alcance de la decisión): cambian las trayectorias de todos los
    // partidos). Antes de renovarlas se comprobó que con el dato a 0 los tres valores viejos (1085505645508475356, 44,
    // 9633395058359667205) seguían saliendo: la sostenida apagada es el motor de antes, bit a bit.
    private const ulong RegulationOnlyBefore = 14554122096672490435UL;
    private const int RegulationOnlyMatches = 38;
    private const ulong EveryMatchBefore = 12655774248078806088UL;

    // Las mismas tres huellas antes de la ADR 0184; con positioningHoldBonus = 0 tienen que seguir saliendo.
    private const ulong RegulationOnlyBeforeAdr0184 = 1085505645508475356UL;
    private const int RegulationOnlyMatchesBeforeAdr0184 = 44;
    private const ulong EveryMatchBeforeAdr0184 = 9633395058359667205UL;

    private const float Eps = 0.001f;

    private static readonly Catalog Current = TestData.LoadCatalog();
    private static readonly Catalog NoMobChanges = WithKnobs(rows: 0, percent: 0);
    private static readonly Catalog NoSpeedUp = WithKnobs(rows: 1, percent: 0);

    private static Catalog WithKnobs(int rows, int percent)
    {
        var files = TestData.LoadAllFiles();
        string text = files["sim/tuning.json"];
        Assert.Contains("\"narrowRowsPerSide\": 1", text, StringComparison.Ordinal);
        Assert.Contains("\"speedPercent\": 15", text, StringComparison.Ordinal);
        files["sim/tuning.json"] = text
            .Replace("\"narrowRowsPerSide\": 1", $"\"narrowRowsPerSide\": {rows}", StringComparison.Ordinal)
            .Replace("\"speedPercent\": 15", $"\"speedPercent\": {percent}", StringComparison.Ordinal);
        return DataLoader.FromJson(files);
    }

    private static ulong Fingerprint(MatchResult result)
    {
        ulong hash = FnvOffset;
        foreach (var e in result.Events)
        {
            string text = string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)e.Type}|{e.Tick}|{e.Team}|{e.Actor}|{e.Target}|{e.Opponent}|{e.Cell.Column}|{e.Cell.Row}|{(int)e.Zone}|{(int)e.Phase}|{e.Bias}|{e.DistanceToGoal}|{e.Detail}");
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= FnvPrime;
            }

            hash ^= (byte)'\n';
            hash *= FnvPrime;
        }

        return hash;
    }

    private static MatchResult Play(Catalog catalog, ulong seed, bool trace = false) =>
        Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, new SimConfig(CollectLog: false, Trace: trace));

    /// <summary>Los partidos de las semillas 1..60 que llegan a la turba, con su traza.</summary>
    private static List<(ulong Seed, MatchResult Result)> MobMatches(Catalog catalog)
    {
        var list = new List<(ulong, MatchResult)>();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Play(catalog, seed, trace: true);
            if (result.Report.WentToGoldenGoal)
            {
                list.Add((seed, result));
            }
        }

        return list;
    }

    private static int FirstMobFrame(MatchTrace trace)
    {
        for (int f = 0; f < trace.FrameCount; f++)
        {
            if (trace.PhaseAt(f) == MatchPhase.MobGoldenGoal)
            {
                return f;
            }
        }

        return -1;
    }

    private static bool Invaded(float y) => y < 1f - Eps || y > 6f + Eps;

    private static float BandDistance(float y) => y < 1f ? 1f - y : y > 6f ? y - 6f : 0f;

    [Fact]
    public void TheRegulationMatchIsByteForByteTheOneBeforeTheChange()
    {
        ulong hash = FnvOffset;
        int played = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Play(Current, seed);
            if (result.Report.WentToGoldenGoal)
            {
                continue;
            }

            hash = (hash ^ Fingerprint(result)) * FnvPrime;
            played++;
        }

        Assert.Equal(RegulationOnlyMatches, played);
        Assert.Equal(RegulationOnlyBefore, hash);

        ulong untouched = FnvOffset;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Play(NoMobChanges, seed);
            if (!result.Report.WentToGoldenGoal)
            {
                untouched = (untouched ^ Fingerprint(result)) * FnvPrime;
            }
        }

        Assert.Equal(untouched, hash);
    }

    /// <summary>
    /// ADR 0184 y 0186: con <c>positioningHoldBonus</c> = 0 y las tres reglas de BV-B apagadas
    /// (<c>followVictimWhileTackling</c>, <c>whistledFoulDownsVictim</c>, <c>escapeBeyondDecisionReach</c>) el motor es el de antes, bit a bit. Las tres
    /// huellas de antes de la ADR 0184 tienen que seguir saliendo (el reglamentario, su número de partidos y todos los
    /// partidos con la turba a 0/0). El arreglo de <c>SetOwner</c> (quien recoge el balón bloqueando pasa a portador)
    /// no tiene dato: no ocurría en estos 60 partidos con el motor de antes, y por eso las huellas no lo ven.
    /// </summary>
    [Fact]
    public void WithTheNewRulesOffEveryTraceIsTheOneBeforeAdr0184()
    {
        var current = Before(Current);
        var noMob = Before(NoMobChanges);

        ulong regulation = FnvOffset;
        int played = 0;
        ulong every = FnvOffset;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Play(current, seed);
            if (!result.Report.WentToGoldenGoal)
            {
                regulation = (regulation ^ Fingerprint(result)) * FnvPrime;
                played++;
            }

            every = (every ^ Fingerprint(Play(noMob, seed))) * FnvPrime;
        }

        Assert.Equal(RegulationOnlyBeforeAdr0184, regulation);
        Assert.Equal(RegulationOnlyMatchesBeforeAdr0184, played);
        Assert.Equal(EveryMatchBeforeAdr0184, every);
    }

    private static Catalog Before(Catalog catalog)
    {
        // ADR 0188 (BV-D): el motor de antes de la ADR 0184 tenía también las bases de lesión de antes (140 / 60); con las de
        // ahora (90 / 110) estas trayectorias sí cruzan la franja que se movió y la huella deja de ser la de antes.
        var held = OscillationProbeTests.WithHold(InjuryChanceTests.WithOldInjuryBases(catalog), 0);
        return held with
        {
            Tuning = held.Tuning with
            {
                Tackle = held.Tuning.Tackle with { FollowVictimWhileTackling = false, WhistledFoulDownsVictim = false, EscapeBeyondDecisionReach = false },
            },
        };
    }

    [Fact]
    public void WithBothMobKnobsAtZeroEveryTraceIsTheOldOneEvenWithMob()
    {
        ulong hash = FnvOffset;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            hash = (hash ^ Fingerprint(Play(NoMobChanges, seed))) * FnvPrime;
        }

        Assert.Equal(EveryMatchBefore, hash);
    }

    [Fact]
    public void ThePlayedMobIsDeterministicAndDifferentFromTheOldOne()
    {
        bool anyMob = false;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var a = Play(Current, seed);
            if (!a.Report.WentToGoldenGoal)
            {
                continue;
            }

            anyMob = true;
            Assert.Equal(Fingerprint(a), Fingerprint(Play(Current, seed)));
            Assert.NotEqual(Fingerprint(Play(NoMobChanges, seed)), Fingerprint(a));
        }

        Assert.True(anyMob, "ninguna de las 60 semillas llega a la turba: el test no comprobó nada");
    }

    [Fact]
    public void FromTheKickoffOfTheMobNobodyStandsOnAnInvadedRowAndTheBallStaysInTheBand()
    {
        int checks = 0;
        foreach (var (seed, result) in MobMatches(Current))
        {
            var trace = result.Trace!;
            int start = FirstMobFrame(trace);
            Assert.True(start > 0, $"seed {seed}: no hay fase de turba");
            for (int f = start; f < trace.FrameCount; f++)
            {
                var ball = trace.BallAt(f);
                Assert.False(Invaded(ball.Y), $"seed {seed}, fotograma {f}: el balón está en una fila invadida (y {ball.Y:F2})");
                for (int p = 0; p < trace.Players.Count; p++)
                {
                    // El derribado se levanta donde cayó (18 ticks) y luego anda: no es un salto, es un caído.
                    if (!trace.OnPitchAt(f, p) || trace.StateAt(f, p) is PlayerState.KnockedDown)
                    {
                        continue;
                    }

                    float y = trace.PositionAt(f, p).Y;
                    Assert.False(Invaded(y), $"seed {seed}, fotograma {f}: el jugador {trace.Players[p].Id} está en y {y:F2}, fila invadida");
                    checks++;
                }
            }
        }

        Assert.True(checks > 1000, "muy pocas comprobaciones: el test no midió la turba");
    }

    [Fact]
    public void WhoIsOnAnInvadedRowWhenTheMobStartsWalksOutAndNobodyJumps()
    {
        int walkers = 0;
        foreach (var (seed, result) in MobMatches(Current))
        {
            var trace = result.Trace!;
            int mobFrame = trace.FrameOfTick(result.Events.First(e => e.Type == EventType.MobStart).Tick);
            int start = FirstMobFrame(trace);

            for (int p = 0; p < trace.Players.Count; p++)
            {
                if (!trace.OnPitchAt(mobFrame, p) || !Invaded(trace.PositionAt(mobFrame, p).Y)
                    || trace.StateAt(mobFrame, p) is PlayerState.KnockedDown)
                {
                    continue;
                }

                walkers++;
                float previous = BandDistance(trace.PositionAt(mobFrame, p).Y);
                for (int f = mobFrame + 1; f <= start && trace.OnPitchAt(f, p); f++)
                {
                    float distance = BandDistance(trace.PositionAt(f, p).Y);

                    // Se acerca a la banda, nunca se aleja, y nadie salta: el paso es el de andar (RF-053).
                    Assert.True(distance <= previous + Eps, $"seed {seed}, jugador {trace.Players[p].Id}: se aleja de la banda en el fotograma {f}");
                    Assert.True(
                        Vec2.Distance(trace.PositionAt(f, p), trace.PositionAt(f - 1, p)) <= 0.5f,
                        $"seed {seed}, jugador {trace.Players[p].Id}: salta en el fotograma {f}");
                    previous = distance;
                }
            }
        }

        Assert.True(walkers > 10, "nadie estaba en una fila invadida al empezar la turba: el test no comprobó nada");
    }

    [Fact]
    public void EveryRestartOfTheMobIsTakenInsideTheBand()
    {
        int restarts = 0;
        foreach (var (seed, result) in MobMatches(Current))
        {
            var trace = result.Trace!;
            int start = FirstMobFrame(trace);
            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Recovery || trace.FrameOfTick(e.Tick) < start
                    || e.Detail is not ("throwIn" or "corner" or "goalKick"))
                {
                    continue;
                }

                var ball = trace.BallAt(trace.FrameOfTick(e.Tick));
                Assert.False(Invaded(ball.Y), $"seed {seed}, tick {e.Tick}: el saque {e.Detail} sale de y {ball.Y:F2}");
                restarts++;
            }
        }

        Assert.True(restarts > 10, $"muy pocos saques en turba ({restarts}): el test no comprobó nada");
    }

    /// <summary>
    /// Regresión medida al hacer la ADR 0175: con los destinos de la utilidad y de los pases acotados sólo al
    /// campo entero, el balón volaba a las filas invadidas y salía de banda 7,57 veces por turba (0,48 antes). Con
    /// los destinos acotados a la banda vuelve a 0,47.
    /// </summary>
    [Fact]
    public void TheBallDoesNotKeepGoingOutOnTheInvadedRows()
    {
        int throwIns = 0;
        int mobs = 0;
        foreach (var (_, result) in MobMatches(Current))
        {
            mobs++;
            int start = result.Events.First(e => e.Type == EventType.MobStart).Tick;
            throwIns += result.Events.Count(e => e.Tick >= start && e.Type == EventType.Recovery && e.Detail == "throwIn");
        }

        Assert.True(mobs > 10, "muy pocas turbas: el test no comprobó nada");
        Assert.True((double)throwIns / mobs < 2.5, $"{throwIns} saques de banda en {mobs} turbas: el balón vuelve a salir por las filas invadidas");
    }

    [Fact]
    public void TheMobRunsFifteenPercentFasterOnTheFeetAndOnTheBall()
    {
        double stepFast = 0, stepBase = 0, ballFast = 0, ballBase = 0;
        long stepsFast = 0, stepsBase = 0, ballsFast = 0, ballsBase = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            Accumulate(Play(Current, seed, trace: true), ref stepFast, ref stepsFast, ref ballFast, ref ballsFast);
            Accumulate(Play(NoSpeedUp, seed, trace: true), ref stepBase, ref stepsBase, ref ballBase, ref ballsBase);
        }

        Assert.True(stepsFast > 5000 && stepsBase > 5000, "muy pocos pasos medidos");
        double onFeet = (stepFast / stepsFast) / (stepBase / stepsBase);
        Assert.InRange(onFeet, 1.08, 1.20);
        Assert.True(ballsFast > 50 && ballsBase > 50, "muy pocos vuelos medidos");
        double onBall = (ballFast / ballsFast) / (ballBase / ballsBase);
        Assert.InRange(onBall, 1.05, 1.25);
    }

    /// <summary>Paso medio de quien camina (0,02-0,5 casillas/tick, sin saltos) y del balón en vuelo, sólo en la turba.</summary>
    private static void Accumulate(MatchResult result, ref double steps, ref long stepCount, ref double flights, ref long flightCount)
    {
        var trace = result.Trace!;
        int start = FirstMobFrame(trace);
        if (start < 0)
        {
            return;
        }

        for (int f = start + 1; f < trace.FrameCount; f++)
        {
            for (int p = 0; p < trace.Players.Count; p++)
            {
                if (!trace.OnPitchAt(f, p) || !trace.OnPitchAt(f - 1, p))
                {
                    continue;
                }

                float step = Vec2.Distance(trace.PositionAt(f, p), trace.PositionAt(f - 1, p));
                if (step is > 0.02f and < 0.5f)
                {
                    steps += step;
                    stepCount++;
                }
            }

            if (trace.BallInFlightAt(f) && trace.BallInFlightAt(f - 1))
            {
                float flight = Vec2.Distance(trace.BallAt(f), trace.BallAt(f - 1));
                if (flight > 0.05f)
                {
                    flights += flight;
                    flightCount++;
                }
            }
        }
    }

    [Fact]
    public void TheMobTuningRejectsABandThatLeavesNoField()
    {
        var files = TestData.LoadAllFiles();
        files["sim/tuning.json"] = files["sim/tuning.json"].Replace("\"narrowRowsPerSide\": 1", "\"narrowRowsPerSide\": 4", StringComparison.Ordinal);
        Assert.Throws<DataException>(() => DataLoader.FromJson(files));
    }

    private static MatchEngine NewEngine(Catalog catalog) =>
        new(TestMatches.Reference(catalog, 1), 1, catalog, SimConfig.Default);

    /// <summary>El +15 % es exacto y entero: base × 115 / 100 en cada unidad, y en el reglamentario no existe.</summary>
    [Fact]
    public void TheSpeedBonusIsExactPerUnit()
    {
        var engine = NewEngine(Current);
        var ball = Current.Tuning.Ball;
        var save = Current.Tuning.Save;
        int[] milli = { ball.PassSpeedCellsPerTickMilli, ball.ShotSpeedCellsPerTickMilli, ball.HeaderSpeedCellsPerTickMilli, save.ParrySpeedCellsPerTickMilli };
        int[] player = new int[4];
        for (int i = 0; i < player.Length; i++)
        {
            Assert.Equal(milli[i], engine.BallSpeedMilliForTest(milli[i]));
            player[i] = engine.SpeedPerTickMilliForTest(i + 1);
        }

        engine.EnterMobPhaseForTest();
        for (int i = 0; i < milli.Length; i++)
        {
            Assert.Equal(milli[i] * 115 / 100, engine.BallSpeedMilliForTest(milli[i]));
            Assert.True(player[i] > 0);
            Assert.Equal(player[i] * 115 / 100, engine.SpeedPerTickMilliForTest(i + 1));
        }
    }

    /// <summary>El suplente que entra en la turba no aparece en una fila del público.</summary>
    [Fact]
    public void ASubstituteEnteringDuringTheMobStandsInsideTheBand()
    {
        // Un titular con la casilla-hogar en la fila 0 (extremo puro): el suplente que hereda su sitio.
        var setup = TestMatches.Reference(Current, 1);
        var slots = setup.Home.Lineup.Slots.ToList();
        slots[2] = slots[2] with { HomeCell = new Cell(3, 0) };
        setup = setup with { Home = setup.Home with { Lineup = new Lineup(slots) } };
        var engine = new MatchEngine(setup, 1, Current, SimConfig.Default);
        engine.EnterMobPhaseForTest();
        var band = engine.BandForTest;
        MatchPlayer? wide = null;
        for (int i = 0; i < 14 && wide is null; i++)
        {
            var candidate = engine.PlayerAtForTest(i);
            if (candidate.HomeCenter.Y < 1f)
            {
                wide = candidate;
            }
        }

        Assert.NotNull(wide);
        wide!.EnterPitch(band);
        Assert.True(band.Contains(wide.Position.Y), $"el suplente entró en y {wide.Position.Y:F2}");

        // Y el balón aparcado (BB-O) tampoco cae en una fila invadida.
        engine.ParkBallForTest(new Vec2(8f, 0.2f));
        Assert.True(band.Contains(engine.BallPositionForTest.Y));
    }

    /// <summary>La reanudación espera al lento que sigue en una fila invadida; en reglamentario esa fila no cuenta.</summary>
    [Fact]
    public void TheRestartWaitsForWhoIsStillOnAnInvadedRow()
    {
        var engine = NewEngine(Current);
        int outfield = engine.OutfieldIndexForTest(0, 1);
        engine.PlaceForTest(outfield, new Vec2(3f, 0.3f));
        Assert.True(engine.EveryoneInPlaceForTest(), "sin turba, la fila 0 es campo");
        engine.EnterMobPhaseForTest();
        Assert.False(engine.EveryoneInPlaceForTest(), "en la turba, el saque no sale con alguien en la fila del público");
        engine.PlaceForTest(outfield, new Vec2(3f, 1.5f));
        Assert.True(engine.EveryoneInPlaceForTest());
    }

    /// <summary>Control positivo: sin estrechamiento el mismo instrumento SÍ ve el balón en las filas exteriores.</summary>
    [Fact]
    public void TheBandInstrumentSeesTheBallOutsideWhenTheNarrowingIsOff()
    {
        bool seen = false;
        foreach (var (_, result) in MobMatches(NoMobChanges))
        {
            var trace = result.Trace!;
            for (int f = FirstMobFrame(trace); f >= 0 && f < trace.FrameCount && !seen; f++)
            {
                seen = Invaded(trace.BallAt(f).Y);
            }
        }

        Assert.True(seen, "el instrumento no detecta el balón en filas exteriores ni sin estrechamiento: no mide nada");
    }

    [Fact]
    public void TheRuleTextComesFromTheDataInBothLanguages()
    {
        var type = MobLoader.FromJson(TestData.LoadAllFiles()).Find("plain")!;
        var es = MobView.Describe(type, Current, "es").Rule;
        var en = MobView.Describe(type, Current, "en").Rule;
        Assert.Contains("15", es, StringComparison.Ordinal);
        Assert.Contains("estrecha", es, StringComparison.Ordinal);
        Assert.Contains("15", en, StringComparison.Ordinal);
        Assert.Contains("narrows", en, StringComparison.Ordinal);
        Assert.Equal(string.Empty, MobView.Describe(type, NoMobChanges, "es").Rule);
        Assert.DoesNotContain("estrecha", MobView.Describe(type, WithKnobs(rows: 0, percent: 15), "es").Rule, StringComparison.Ordinal);
    }
}
