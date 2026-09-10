using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Reglas invariantes del partido: nunca hay empate (RF-055c), la incomparecencia con menos de 5
/// jugadores en campo (RF-059), el portero nunca sale del área (RF-057b) y la secuencia de eventos
/// está ordenada y delimitada por MatchStart/MatchEnd (RF-066, RT-013).
/// </summary>
public sealed class MatchRulesTests
{
    private const int Matches = 50;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void NeverEndsInADraw()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Run(seed);
            var report = result.Report;

            Assert.True(report.Winner is 0 or 1, $"semilla {seed}: ganador {report.Winner}");

            // La incomparecencia (RF-059) decide el partido por encima del marcador: un equipo que se
            // queda con menos de cinco jugadores pierde aunque fuera ganando, así que ese caso no puede
            // exigir que gane quien más goles tiene (paquete U: con la violencia calibrada las
            // incomparecencias dejaron de ser anecdóticas y esta rama empezó a ejercitarse).
            if (!report.Forfeit && report.Goals[0] != report.Goals[1])
            {
                Assert.Equal(report.Goals[0] > report.Goals[1] ? 0 : 1, report.Winner);
            }

            var end = result.Events[^1];
            Assert.Equal(EventType.MatchEnd, end.Type);
            Assert.Contains(end.Detail, new[] { "regulation", "goldenGoal", "tiebreak", "forfeit" });
        }
    }

    [Fact]
    public void GoldenGoalIsOnlyPlayedWhenRegulationIsLevel()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var report = Run(seed).Report;
            if (report.WentToGoldenGoal)
            {
                Assert.True(report.Ticks > Catalog.Tuning.RegulationTicks, $"semilla {seed}: gol de oro sin agotar el reglamentario");
            }
            else if (!report.Forfeit)
            {
                Assert.NotEqual(report.Goals[0], report.Goals[1]);
            }
        }
    }

    [Fact]
    public void ForfeitWhenATeamDropsBelowFivePlayers()
    {
        int forfeits = 0;
        var setup = TestMatches.Brutal(Catalog);

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            if (!result.Report.Forfeit)
            {
                continue;
            }

            forfeits++;
            var end = result.Events[^1];
            Assert.Equal(EventType.MatchEnd, end.Type);
            Assert.Equal("forfeit", end.Detail);

            int loser = 1 - result.Report.Winner;
            int loserOnPitch = OnPitch(setup, result, loser);
            int winnerOnPitch = OnPitch(setup, result, result.Report.Winner);
            Assert.True(loserOnPitch < 5, $"semilla {seed}: el perdedor conserva 5 o más jugadores");

            // Los dos equipos pueden quedarse por debajo de cinco en el mismo tick (§3.8, §3.9): ahí gana
            // el que conserve más jugadores y, si empatan, la cadena de desempate del gol de oro. Con el
            // bloqueo sin balón (ADR 0030 §2) el caso simultáneo deja de ser una rareza teórica.
            Assert.True(
                winnerOnPitch >= 5 || winnerOnPitch >= loserOnPitch,
                $"semilla {seed}: el ganador tenía menos jugadores en campo que el perdedor");
        }

        Assert.True(forfeits > 0, "el emparejamiento de prueba debe producir al menos una incomparecencia en 20 semillas");
    }

    [Fact]
    public void GoalkeeperNeverLeavesTheArea()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            Assert.False(Run(seed).Report.GoalkeeperLeftArea, $"semilla {seed}: un portero salió de su área");
        }
    }

    /// <summary>
    /// AW-R (docs/pendientes.md): durante cualquier balón muerto (saque de banda, córner, de puerta o de
    /// centro) el equipo entero ya no se congela — antes solo se bajaba <c>TickStateTimer</c> y los dos
    /// enfriamientos, así que ningún jugador de campo se movía ni un centímetro durante la ventana de
    /// reanudación. Se recorren los eventos <c>Recovery</c> con detail "throwIn"/"corner"/"goalKick"/
    /// "kickoff" (el ejecutor los emite al resolverse el saque, <c>MatchEngine.TakeRestart</c>/
    /// <c>TakeGoalKick</c>/<c>TakeKickoff</c>) y, para cada uno, se compara la posición de un jugador de
    /// campo <b>que no es el ejecutor</b> entre el fotograma justo antes de que empiece la ventana
    /// (<c>e.Tick - ticks</c>, la duración de <c>data/sim/tuning.json</c> → <c>restart.*Ticks</c>) y el
    /// fotograma justo antes de que termine (<c>e.Tick - 1</c>, el último tick congelado antes de que
    /// <c>ResolveRestart</c> teletransporte al ejecutor). Antes de este cambio la distancia era siempre 0;
    /// ahora al menos un jugador se mueve una distancia no trivial (&gt; 0,3 casillas).
    /// </summary>
    [Fact]
    public void FieldPlayersKeepMovingDuringADeadBall()
    {
        float maxMoved = 0f;
        int windowsChecked = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Recovery)
                {
                    continue;
                }

                int ticks = e.Detail switch
                {
                    "throwIn" => Catalog.Tuning.Restart.ThrowInTicks,
                    "corner" => Catalog.Tuning.Restart.CornerTicks,
                    "goalKick" => Catalog.Tuning.Restart.GoalKickTicks,
                    "kickoff" => Catalog.Tuning.Restart.KickoffTicks,
                    _ => -1,
                };
                if (ticks <= 1)
                {
                    continue;
                }

                int startFrame = trace.FrameOfTick(e.Tick - ticks);
                int endFrame = trace.FrameOfTick(e.Tick - 1);
                if (endFrame <= startFrame)
                {
                    // Ventana recortada contra el arranque de la traza (el saque inicial de partido cae
                    // aquí): no hay margen para medir movimiento, así que no cuenta ni suma al total.
                    continue;
                }

                windowsChecked++;
                for (int player = 0; player < trace.Players.Count; player++)
                {
                    var info = trace.Players[player];
                    if (info.Id == e.Actor
                        || info.Role == Position.Goalkeeper
                        || !trace.OnPitchAt(startFrame, player)
                        || !trace.OnPitchAt(endFrame, player))
                    {
                        continue;
                    }

                    float moved = Vec2.Distance(trace.PositionAt(startFrame, player), trace.PositionAt(endFrame, player));
                    if (moved > maxMoved)
                    {
                        maxMoved = moved;
                    }
                }
            }
        }

        Assert.True(windowsChecked > 0, "el escenario tenía que producir al menos una reanudación de banda/córner/puerta/centro");
        Assert.True(
            maxMoved > 0.3f,
            $"algún jugador de campo debía moverse de forma no trivial durante la ventana de reanudación, máximo observado {maxMoved}");
    }

    /// <summary>
    /// AZ-A (docs/plan-segunda-partida.md): el sacador que designa <c>MatchEngine.BeginRestart</c>
    /// (<c>_restartTaker</c>) se congela desde el primer tick de la cuenta atrás — antes, con AW-R, corría
    /// la IA normal como el resto del equipo y podía alejarse del punto de saque mientras esperaba, para
    /// volver de un salto solo en el último tick. Mismo recorrido de eventos <c>Recovery</c> que
    /// <see cref="FieldPlayersKeepMovingDuringADeadBall"/>, pero aquí se comprueba al ejecutor (<c>e.Actor</c>).
    ///
    /// <para>"Congelado" no es "posición idéntica byte a byte": <c>UpdatePlayer</c> no se le llama, así que
    /// nunca decide ni camina por su cuenta, pero <c>BodySeparation.Resolve</c> corre para todo el mundo
    /// cada tick (§2.1) y puede seguir moviéndolo si alguien lo empuja — un solape normal
    /// (<c>bodies.maxPushPerTickMilli</c>) o, más fuerte, el empuje elevado de una entrada o un bloqueo sin
    /// balón contra él (<c>bodies.tacklePushMultiplier</c>, ADR 0030 §2: un bloqueo no comprueba si el
    /// balón está en juego, así que el sacador congelado sigue siendo un objetivo válido) — por eso
    /// <c>TakeRestart</c> conserva la asignación final de posición, para absorber ese arrastre al resolver.
    /// La cota de este test es el tope teórico exacto de ese empuje elevado
    /// (<c>maxPushPerTickMilli × tacklePushMultiplier</c>): cualquier paso por encima solo se explica
    /// caminando por su cuenta, que es justo lo que <c>UpdatePlayer</c> saltado le impide hacer.</para>
    ///
    /// <para>En el saque de centro, además, todo el equipo vuelve a <c>HomeCenter</c> de un salto al
    /// <b>abrir</b> la reanudación (<c>BeginRestart</c> llama a <c>ResetPositions</c> ahí, no al
    /// resolverla): en el fotograma de resolución (<c>e.Tick</c>, donde antes <c>TakeKickoff</c> volvía a
    /// reformar el equipo) nadie debe desplazarse más que un paso normal.</para>
    /// </summary>
    [Fact]
    public void TheRestartTakerStandsStillDuringTheDeadBall()
    {
        // Tope teórico exacto del empuje de un solo tick de BodySeparation contra el sacador congelado
        // (data/sim/tuning.json, bodies): el solape normal es maxPushPerTickMilli, pero una entrada o un
        // bloqueo sin balón contra él (ADR 0030 §2, no comprueba si el balón está en juego) eleva su tope a
        // maxPushPerTickMilli × tacklePushMultiplier — el mismo cálculo que BodySeparation.AddTacklePush.
        // Con margen de precisión de punto flotante.
        float maxPushPerTickCells =
            (Catalog.Tuning.Bodies.MaxPushPerTickMilli * Catalog.Tuning.Bodies.TacklePushMultiplier / 100 / 1000f) + 0.002f;

        // Cota generosa sobre el paso máximo real (~0,13-0,19 casillas/tick con
        // movement.baseCellsPerTickMilli/speedCellsPerTickMilliPer99 y el +8% de Fast, data/sim/tuning.json,
        // data/traits/traits.json): solo hace falta distinguir "un paso normal" de "un salto de varias
        // casillas" (el fallo que este test cubre si ResetPositions volviera a colarse en la resolución).
        const float maxNormalStepCells = 0.6f;

        int windowsChecked = 0;
        int kickoffResolutionsChecked = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Recovery)
                {
                    continue;
                }

                int ticks = e.Detail switch
                {
                    "throwIn" => Catalog.Tuning.Restart.ThrowInTicks,
                    "corner" => Catalog.Tuning.Restart.CornerTicks,
                    "goalKick" => Catalog.Tuning.Restart.GoalKickTicks,
                    "kickoff" => Catalog.Tuning.Restart.KickoffTicks,
                    _ => -1,
                };
                if (ticks <= 1)
                {
                    continue;
                }

                int startFrame = trace.FrameOfTick(e.Tick - ticks);
                int endFrame = trace.FrameOfTick(e.Tick - 1);
                if (endFrame <= startFrame)
                {
                    continue;
                }

                int takerIndex = -1;
                for (int player = 0; player < trace.Players.Count; player++)
                {
                    if (trace.Players[player].Id == e.Actor)
                    {
                        takerIndex = player;
                        break;
                    }
                }

                if (takerIndex < 0 || !trace.OnPitchAt(startFrame, takerIndex))
                {
                    continue;
                }

                windowsChecked++;
                for (int frame = startFrame; frame < endFrame; frame++)
                {
                    if (!trace.OnPitchAt(frame, takerIndex) || !trace.OnPitchAt(frame + 1, takerIndex))
                    {
                        // Puede lesionarse o ser expulsado por otra jugada mientras espera (poco probable
                        // pero no imposible): deja de tener posición que comprobar, no es un fallo de AZ-A.
                        continue;
                    }

                    float step = Vec2.Distance(trace.PositionAt(frame, takerIndex), trace.PositionAt(frame + 1, takerIndex));
                    Assert.True(
                        step <= maxPushPerTickCells,
                        $"semilla {seed}, tick {e.Tick} ({e.Detail}): el sacador {e.Actor} se movió {step} casillas entre los ticks {trace.TickAt(frame)} y {trace.TickAt(frame + 1)}, más que el empuje máximo de BodySeparation — parece que decidió y caminó por su cuenta");
                }

                if (e.Detail != "kickoff")
                {
                    continue;
                }

                int resolveFrame = trace.FrameOfTick(e.Tick);
                if (resolveFrame <= endFrame)
                {
                    continue;
                }

                kickoffResolutionsChecked++;
                for (int player = 0; player < trace.Players.Count; player++)
                {
                    if (!trace.OnPitchAt(endFrame, player) || !trace.OnPitchAt(resolveFrame, player))
                    {
                        continue;
                    }

                    float moved = Vec2.Distance(trace.PositionAt(endFrame, player), trace.PositionAt(resolveFrame, player));
                    Assert.True(
                        moved <= maxNormalStepCells,
                        $"semilla {seed}, tick {e.Tick}: jugador {trace.Players[player].Id} se desplazó {moved} casillas en el fotograma de resolución del saque de centro");
                }
            }
        }

        Assert.True(windowsChecked > 0, "el escenario tenía que producir al menos una reanudación de banda/córner/puerta/centro");
        Assert.True(kickoffResolutionsChecked > 0, "el escenario tenía que producir al menos un saque de centro medible");
    }

    /// <summary>
    /// AW-C (docs/pendientes.md): un tiro fuera cae junto al poste, no en la bandera de córner. Se mide
    /// directamente sobre el cálculo del destino (accesible como <c>internal static</c> solo para esto),
    /// para las dos filas de origen del tirador, sin depender de que el motor produzca un fallo con esta
    /// semilla concreta.
    /// </summary>
    [Fact]
    public void OffTargetShotsLandNearTheGoalNotTheCorner()
    {
        var goal = Pitch.GoalCenter(0);

        var fromTop = MatchEngine.OffTargetShotTarget(goal, shooterRow: 0f);
        var fromBottom = MatchEngine.OffTargetShotTarget(goal, shooterRow: Pitch.Rows);

        // Misma columna que la portería: el balón no se escapa a lo largo de la línea de fondo.
        Assert.Equal(goal.X, fromTop.X);
        Assert.Equal(goal.X, fromBottom.X);

        // Entre 1 y 1,5 casillas del centro de la portería (RT-023: aritmética entera para todo salvo
        // posiciones, aquí sí toca), lejos de las filas 0 y 5 que antes eran la esquina del campo.
        Assert.InRange(MathF.Abs(fromTop.Y - PitchConstants.CenterRow), 1f, 1.5f);
        Assert.InRange(MathF.Abs(fromBottom.Y - PitchConstants.CenterRow), 1f, 1.5f);
        Assert.NotEqual(0f, fromTop.Y);
        Assert.NotEqual(Pitch.Rows, fromBottom.Y);

        // Conserva el lado hacia el que se fue: origen en la mitad superior desvía hacia arriba, origen
        // en la inferior hacia abajo.
        Assert.True(fromTop.Y < PitchConstants.CenterRow, $"fromTop.Y={fromTop.Y} debería quedar por encima del centro");
        Assert.True(fromBottom.Y > PitchConstants.CenterRow, $"fromBottom.Y={fromBottom.Y} debería quedar por debajo del centro");
    }

    /// <summary>
    /// AW-B (docs/pendientes.md): el portero no se desplaza con el bloque táctico. Se ejecuta un partido
    /// completo con traza (§ MatchTrace) y se compara, fotograma a fotograma, la casilla-hogar efectiva
    /// de cada jugador con la fija que le asigna la alineación: la del portero no debe moverse nunca,
    /// mientras que algún jugador de campo sí, o el test sería vacuo (el bloque nunca se desplazaría).
    /// </summary>
    [Fact]
    public void GoalkeeperEffectiveHomeNeverShiftsWithTheBlock()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var result = Simulator.Run(setup, 1, Catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        bool anyOutfieldMoved = false;
        for (int p = 0; p < trace.Players.Count; p++)
        {
            var tracePlayer = trace.Players[p];
            var team = tracePlayer.Team == 0 ? setup.Home : setup.Away;
            var slot = team.Lineup.Slots.Single(s => s.PlayerId == tracePlayer.Id);
            int column = tracePlayer.Team == 0 ? slot.HomeCell.Column : Pitch.Columns - 1 - slot.HomeCell.Column;
            var expectedHome = Pitch.CellCenter(new Cell(column, slot.HomeCell.Row));

            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                var home = trace.ZoneAt(frame, p).Home;
                bool matchesFixedHome = Vec2.Distance(home, expectedHome) < 0.001f;

                if (tracePlayer.Role == Position.Goalkeeper)
                {
                    Assert.True(
                        matchesFixedHome,
                        $"portero {tracePlayer.Id}, fotograma {frame}: casilla-hogar efectiva {home} distinta de la fija {expectedHome}");
                }
                else if (!matchesFixedHome)
                {
                    anyOutfieldMoved = true;
                }
            }
        }

        Assert.True(anyOutfieldMoved, "ningún jugador de campo desplazó su casilla-hogar en todo el partido: el bloque táctico no se ejercitó");
    }

    [Fact]
    public void EventTicksAreNonDecreasing()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var events = Run(seed).Events;
            for (int i = 1; i < events.Count; i++)
            {
                Assert.True(
                    events[i].Tick >= events[i - 1].Tick,
                    $"semilla {seed}: el evento {i} ({events[i].Type}, t={events[i].Tick}) va detrás de t={events[i - 1].Tick}");
            }
        }
    }

    [Fact]
    public void MatchStartIsFirstAndMatchEndIsLast()
    {
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Run(seed);
            var events = result.Events;

            Assert.Equal(EventType.MatchStart, events[0].Type);
            Assert.Equal(0, events[0].Tick);
            Assert.Equal(EventType.MatchEnd, events[^1].Type);
            Assert.Equal(result.Report.Ticks, events[^1].Tick);

            for (int i = 1; i < events.Count - 1; i++)
            {
                Assert.NotEqual(EventType.MatchStart, events[i].Type);
                Assert.NotEqual(EventType.MatchEnd, events[i].Type);
            }
        }
    }

    [Fact]
    public void ReportCountersAgreeWithTheEventStream()
    {
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var result = Run(seed);
            var report = result.Report;
            int goals = 0;
            int shots = 0;
            int saves = 0;
            int shotsBlocked = 0;
            int tackles = 0;
            int blocks = 0;
            int fouls = 0;
            int injuries = 0;
            int deaths = 0;

            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case EventType.Goal:
                        goals++;
                        break;
                    case EventType.Shot:
                        shots++;
                        break;
                    case EventType.Save:
                        saves++;
                        break;
                    case EventType.ShotBlocked:
                        shotsBlocked++;
                        break;
                    case EventType.Tackle:
                        // El bloqueo sin balón (ADR 0030 §2) reutiliza el tipo TACKLE con Detail propio y
                        // se cuenta aparte: report.Tackles sigue siendo "disputas del balón" (RT-056).
                        if (e.Detail.StartsWith("block", StringComparison.Ordinal))
                        {
                            blocks++;
                        }
                        else
                        {
                            tackles++;
                        }

                        break;
                    case EventType.Foul:
                        fouls++;
                        break;
                    case EventType.Injury:
                        injuries++;
                        break;
                    case EventType.Death:
                        deaths++;
                        break;
                    default:
                        break;
                }
            }

            Assert.Equal(report.Goals[0] + report.Goals[1], goals);
            Assert.Equal(report.Shots[0] + report.Shots[1], shots);
            Assert.Equal(report.Saves[0] + report.Saves[1], saves);
            Assert.Equal(
                report.PassesIntercepted[0] + report.PassesIntercepted[1],
                result.Events.Count(e => e.Type == EventType.PassFailed && e.Detail == "intercepted"));
            Assert.Equal(
                report.PassesLoose[0] + report.PassesLoose[1],
                result.Events.Count(e => e.Type == EventType.PassFailed && e.Detail == "loose"));
            Assert.Equal(
                report.PassesBeaten[0] + report.PassesBeaten[1],
                result.Events.Count(e => e.Type == EventType.PassFailed && e.Detail == "beaten"));
            Assert.Equal(report.ShotsBlocked[0] + report.ShotsBlocked[1], shotsBlocked);
            Assert.Equal(report.Tackles, tackles);
            Assert.Equal(report.Blocks, blocks);
            Assert.Equal(report.Fouls, fouls);
            Assert.Equal(report.Injuries, injuries);
            Assert.Equal(0, deaths);
            Assert.Equal(0, report.Deaths);
        }
    }

    [Fact]
    public void InvalidSetupIsRejectedWithAClearMessage()
    {
        var valid = TestMatches.Reference(Catalog, 1);

        var fourStarters = new Lineup(valid.Home.Lineup.Slots.Take(4).ToList());
        var tooFew = valid with { Home = valid.Home with { Lineup = fourStarters } };
        var missingStarters = Assert.Throws<ArgumentException>(() => Simulator.Run(tooFew, 1, Catalog, SimConfig.Default));
        Assert.Contains("titulares", missingStarters.Message, StringComparison.Ordinal);

        var withoutGoalkeeper = new Lineup(valid.Home.Lineup.Slots.Skip(1).ToList());
        var noKeeper = valid with { Home = valid.Home with { Lineup = withoutGoalkeeper } };
        var keeper = Assert.Throws<ArgumentException>(() => Simulator.Run(noKeeper, 1, Catalog, SimConfig.Default));
        Assert.Contains("porteros", keeper.Message, StringComparison.Ordinal);

        var shared = valid with { Away = valid.Away with { Players = valid.Home.Players, Lineup = valid.Home.Lineup } };
        var duplicated = Assert.Throws<ArgumentException>(() => Simulator.Run(shared, 1, Catalog, SimConfig.Default));
        Assert.Contains("únicos", duplicated.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UtilityDumpIsCapturedForTheRequestedPlayerAndTick()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var result = Simulator.Run(setup, 1, Catalog, new SimConfig(CollectLog: false, DumpUtility: (4, 100)));

        var dump = result.Report.UtilityDump;
        Assert.NotNull(dump);
        Assert.Equal(4, dump!.PlayerId);

        // El jugador solo decide cada tuning.decisionIntervalTicks ticks, desplazado por su id, y solo si
        // su estado lo permite: lo que el motor garantiza (y lo que documenta MatchEngine.Decide) es la
        // PRIMERA decisión de ese jugador en un tick >= el pedido, no exactamente ese tick. Exigir la
        // igualdad convertía este test en un test del reparto de estados de una semilla concreta.
        Assert.True(dump.Tick >= 100, $"el volcado debía capturarse en un tick >= 100, fue {dump.Tick}");
        Assert.True(StateMachine.IsDecisionState(dump.State));
        Assert.NotEmpty(dump.Rows);
        Assert.Contains(dump.Rows, row => row.Action == dump.Chosen && !row.Rejected);
    }

    [Fact]
    public void LogHasOneLinePerEventAndOnlyWhenRequested()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var withLog = Simulator.Run(setup, 1, Catalog, new SimConfig(CollectLog: true));
        var withoutLog = Simulator.Run(setup, 1, Catalog, new SimConfig(CollectLog: false));

        Assert.Equal(withLog.Events.Count, withLog.Report.Log.Count);
        Assert.Empty(withoutLog.Report.Log);
        Assert.All(withLog.Report.Log, line => Assert.StartsWith("[t=", line, StringComparison.Ordinal));
    }

    private static MatchResult Run(ulong seed) =>
        Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false));

    /// <summary>Jugadores del equipo que siguen en el campo al acabar, según lesiones y rojas del log de eventos.</summary>
    private static int OnPitch(MatchSetup setup, MatchResult result, int team)
    {
        var starters = team == 0 ? setup.Home.Lineup.Slots : setup.Away.Lineup.Slots;
        int count = starters.Count;
        foreach (var e in result.Events)
        {
            bool isTeamPlayer = false;
            for (int i = 0; i < starters.Count; i++)
            {
                if (starters[i].PlayerId == e.Actor)
                {
                    isTeamPlayer = true;
                    break;
                }
            }

            if (!isTeamPlayer)
            {
                continue;
            }

            if (e.Type == EventType.Injury || (e.Type == EventType.Card && e.Detail == "red"))
            {
                count--;
            }
        }

        return count;
    }
    /// <summary>
    /// AZ-D (ADR 0090): la falta <b>señalada</b> reanuda con el balón para el equipo que la sufre —una
    /// <c>Recovery</c> con detalle <c>freeKick</c> de un jugador del equipo del que recibió la falta, dentro
    /// de la cuenta atrás— y la <b>no señalada</b> (<c>Foul</c> con detalle <c>unseen</c>, AZ-E) no reanuda
    /// nada: el juego sigue. Y las dos existen: con <c>whistlePercent</c> 80 sobre 50 partidos hay de sobra de
    /// cada una.
    /// </summary>
    [Fact]
    public void AWhistledFoulRestartsWithAFreeKickForTheFouledTeamAndAnUnseenOneDoesNot()
    {
        int whistled = 0, freeKicks = 0, unseen = 0, unseenFollowedByFreeKick = 0, sameTeam = 0;
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false));
            var events = result.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type != EventType.Foul)
                {
                    continue;
                }

                // La primera Recovery tras la falta: si es el saque de falta, el balón es del rival del infractor.
                bool restarted = false;
                for (int j = i + 1; j < events.Count && events[j].Tick <= e.Tick + 12; j++)
                {
                    if (events[j].Type != EventType.Recovery)
                    {
                        continue;
                    }

                    if (events[j].Detail == "freeKick")
                    {
                        restarted = true;
                        sameTeam += events[j].Team == e.Team ? 1 : 0;
                    }

                    break;
                }

                if (e.Detail == "unseen")
                {
                    unseen++;
                    unseenFollowedByFreeKick += restarted ? 1 : 0;
                }
                else
                {
                    whistled++;
                    freeKicks += restarted ? 1 : 0;
                }
            }
        }

        Assert.True(whistled > 50, $"faltas señaladas en 50 partidos: {whistled}");
        Assert.True(unseen > 10, $"faltas no señaladas en 50 partidos: {unseen}");
        Assert.True(freeKicks * 100 / whistled >= 75, $"solo {freeKicks} de {whistled} faltas señaladas reanudaron con saque de falta (las demás dieron penalti o el partido acabó; medido 115 de 136)");
        // Una falta de bloqueo cometida DURANTE la cuenta atrás de otra hereda el saque pendiente de la
        // primera, que es del rival de la primera y no del suyo: es raro (medido, ~1 %) y no es un fallo
        // del saque de falta, así que se tolera como excepción acotada en vez de exigir el cero.
        Assert.True(sameTeam * 100 <= freeKicks * 5, $"{sameTeam} de {freeKicks} saques de falta fueron para el equipo que la cometió");
        // Misma excepción para la no señalada cometida durante una cuenta atrás: hereda el saque pendiente.
        Assert.True(unseenFollowedByFreeKick * 100 <= unseen * 10, $"{unseenFollowedByFreeKick} de {unseen} faltas no señaladas fueron seguidas de un saque de falta");
    }
}
