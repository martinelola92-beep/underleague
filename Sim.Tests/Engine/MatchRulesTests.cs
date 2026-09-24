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
                // BC-A: con el RELOJ DEL PARTIDO, no con el tick del motor. `report.Ticks` es tiempo de
                // pared y desde que la reanudación espera supera SIEMPRE el reglamentario (1.724 contra
                // 1.200 en un partido normal), así que la aserción se había vuelto tautológica.
                Assert.True(report.ClockTicks > Catalog.Tuning.RegulationTicks, $"semilla {seed}: gol de oro sin agotar el reglamentario");
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

    /// <summary>
    /// <b>El portero sale del área, pero es raro.</b>
    ///
    /// <para>REPLANTEADO (ADR 0141), y aquí el test cambia porque cambió la regla, no porque estorbara.
    /// Este test afirmaba que el portero <b>nunca</b> salía, y era verdad por un motivo que no era una
    /// decisión de diseño sino una limitación: <c>Move</c> acotaba al área dos veces, así que
    /// <c>GoalkeeperLeftArea</c> era inalcanzable <i>por construcción</i> y el rasgo «Sale mucho» no podía
    /// cumplir su nombre. El encargo del revisor pide expresamente que las dos cosas sean alcanzables.</para>
    ///
    /// <para>Lo que se vigila ahora es la otra mitad de la regla, que es la que de verdad importa y la que
    /// el encargo protege: <b>que no se convierta en comportamiento permanente</b>. Un portero suelto por
    /// el campo en la mayoría de los partidos sería un juego distinto, no un portero que sale.</para>
    /// </summary>
    [Fact]
    public void ElPorteroSaleDelAreaPeroEsRaro()
    {
        int left = 0;
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            if (Run(seed).Report.GoalkeeperLeftArea)
            {
                left++;
            }
        }

        Assert.True(
            left * 2 < Matches,
            $"un portero salió del área en {left} de {Matches} partidos: eso ya no es salir, es vivir fuera");
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

                int endFrame = trace.FrameOfTick(e.Tick - 1);

                // BC-A: LA CUENTA ATRÁS DEL SAQUE DE CENTRO YA NO DURA `KickoffTicks`. Dura eso MÁS lo que
                // el equipo tarde en volver andando a su formación, así que una ventana fija de 15 ticks
                // medía los últimos 15 de una espera en la que ya no se mueve nadie: las dos aserciones de
                // este test pasaban SOLAS. Lo destapó la revisión independiente, y es justo el test que
                // tenía que haber cazado que el sacador del saque de centro seguía teletransportándose.
                //
                // La ventana se toma ahora de la traza: hacia atrás mientras dure la misma fase.
                int startFrame;
                if (e.Detail == "kickoff")
                {
                    startFrame = endFrame;
                    var restartPhase = trace.PhaseAt(endFrame);
                    while (startFrame > 0 && trace.PhaseAt(startFrame - 1) == restartPhase)
                    {
                        startFrame--;
                    }
                }
                else
                {
                    startFrame = trace.FrameOfTick(e.Tick - ticks);
                }

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
    /// AZ-A (docs/plan-segunda-partida.md) y BA-D: el sacador que designa <c>MatchEngine.BeginRestart</c>
    /// (<c>_restartTaker</c>) no corre la IA durante la cuenta atrás — antes, con AW-R, corría la IA normal
    /// como el resto del equipo y podía <b>alejarse</b> del punto de saque mientras esperaba, para volver de
    /// un salto solo en el último tick. Mismo recorrido de eventos <c>Recovery</c> que
    /// <see cref="FieldPlayersKeepMovingDuringADeadBall"/>, pero aquí se comprueba al ejecutor (<c>e.Actor</c>).
    ///
    /// <para><b>Lo que se afirma es el requisito, no la implementación.</b> Hasta BA-D el sacador se
    /// teletransportaba al punto al abrir la reanudación y se quedaba inmóvil, así que este test exigía
    /// «no se mueve» y con eso bastaba. Desde BA-D <b>camina</b> hasta el punto —el teletransporte era
    /// justo lo que el revisor reportó—, así que «no se mueve» era la implementación vieja y no la regla.
    /// La regla es doble y se comprueba tick a tick: cada paso es <b>un paso</b> y no un salto, y la
    /// distancia al punto de saque <b>nunca crece</b>. Las dos juntas son más fuertes que la anterior:
    /// aquella habría dejado pasar un sacador que se alejara despacio.</para>
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
    /// <para>En el saque de centro, además, todo el que vaya a jugar la reanudación vuelve a
    /// <c>HomeCenter</c> de un salto al <b>abrir</b> la reanudación (<c>BeginRestart</c> llama a
    /// <c>ResetPositions</c> ahí, no al resolverla; BB-C: quien siga celebrando un gol queda fuera de ese
    /// salto, pero tampoco puede ser el sacador): en el fotograma de resolución (<c>e.Tick</c>, donde antes
    /// <c>TakeKickoff</c> volvía a reformar el equipo) nadie debe desplazarse más que un paso normal.</para>
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

                    var before = trace.PositionAt(frame, takerIndex);
                    var after = trace.PositionAt(frame + 1, takerIndex);
                    float step = Vec2.Distance(before, after);

                    // Un paso, no un salto. Caminar hacia el punto es legítimo (BA-D); cruzar el campo de
                    // una vez sigue siendo el fallo que este test cubre.
                    Assert.True(
                        step <= maxNormalStepCells,
                        $"semilla {seed}, tick {e.Tick} ({e.Detail}): el sacador {e.Actor} se movió {step} casillas entre los ticks {trace.TickAt(frame)} y {trace.TickAt(frame + 1)}, más que un paso normal — eso es un salto, no un desplazamiento");

                    // Y el requisito de AZ-A: nunca se ALEJA del punto de saque. El balón está aparcado ahí
                    // toda la cuenta atrás (ParkBall), así que sirve de referencia sin que el test tenga que
                    // conocer la geometría de cada tipo de reanudación. La tolerancia es el empuje de un
                    // tick de BodySeparation, que puede apartarlo sin que él haya decidido nada.
                    float toPointBefore = Vec2.Distance(before, trace.BallAt(frame));
                    float toPointAfter = Vec2.Distance(after, trace.BallAt(frame + 1));
                    Assert.True(
                        toPointAfter <= toPointBefore + maxPushPerTickCells,
                        $"semilla {seed}, tick {e.Tick} ({e.Detail}): el sacador {e.Actor} se ALEJÓ del punto de saque, de {toPointBefore} a {toPointAfter} casillas — AZ-A: esperando es cuando se alejaba para volver de un salto en el último tick");
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

                // El saque de centro del FINAL DEL REGLAMENTARIO (el que abre el gol de oro y la turba)
                // reforma el equipo a propósito: se programa y se resuelve en el mismo tick, así que el
                // ResetPositions legítimo de §3.2 cae dentro del intervalo que mide este bloque y los once
                // "saltan" a su casilla-hogar. No es el fallo que este test cubre —ResetPositions colándose
                // en la resolución de un saque NORMAL—, así que se excluye por lo que lo distingue: ocurre
                // exactamente en el tick en que acaba el reglamentario. Antes no aparecía en la muestra
                // porque ninguna de las 50 semillas llegaba empatada a ese tick con saque de centro.
                // BC-A: se compara el RELOJ DEL PARTIDO. Con el tick del motor esta guarda no coincidía ya
                // nunca —el reglamentario termina en un tick de motor mayor que `RegulationTicks`— y se
                // había quedado muerta, dejando pasar el caso que excluye a propósito.
                if (e.ClockTick == Catalog.Tuning.RegulationTicks)
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
            int offBallTackles = 0;
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
                        // El tipo TACKLE lo comparten TRES sucesos distintos, cada uno con su Detail y su
                        // contador: el bloqueo sin balón (ADR 0030 §2), la entrada al marcado sin balón
                        // (ADR 0105) y la entrada al portador. report.Tackles es solo la última —disputar
                        // el balón, que es lo que mide tacklesPerMatch (RT-056)— desde la ADR 0125 D1.
                        if (e.Detail.StartsWith("block", StringComparison.Ordinal))
                        {
                            blocks++;
                        }
                        else if (e.Detail.StartsWith("offBall", StringComparison.Ordinal))
                        {
                            offBallTackles++;
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
            Assert.Equal(report.OffBallTackles, offBallTackles);
            Assert.Equal(report.Blocks, blocks);
            Assert.Equal(report.Fouls, fouls);
            Assert.Equal(report.Injuries, injuries);
            Assert.Equal(0, deaths);
            Assert.Equal(0, report.Deaths);
        }
    }

    /// <summary>
    /// ADR 0125 D1: la entrada al portador y la entrada al marcado sin balón se cuentan por separado, y la
    /// separación es una <b>partición</b> —ninguna resolución se pierde ni se cuenta dos veces—, tanto en
    /// el informe como por jugador. La tercera afirmación es la que impide que el test sea vacuo: la
    /// entrada sin balón se dispara de verdad en el conjunto de referencia, así que si alguien volviera a
    /// sumarlas juntas, la primera aserción fallaría.
    /// </summary>
    [Fact]
    public void CarrierAndOffBallTacklesArePartitionedNotMixed()
    {
        int offBallTotal = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Run(seed);
            var report = result.Report;

            int carrierEvents = result.Events.Count(e => e.Type == EventType.Tackle
                && e.Detail is "foul" or "won" or "missed");
            int offBallEvents = result.Events.Count(e => e.Type == EventType.Tackle
                && e.Detail.StartsWith("offBall", StringComparison.Ordinal));

            Assert.Equal(carrierEvents, report.Tackles);
            Assert.Equal(offBallEvents, report.OffBallTackles);

            // El reparto por jugador es el mismo reparto: players.csv y el agregado no pueden divergir.
            Assert.Equal(report.Tackles, report.Players.Sum(p => p.Tackles));
            Assert.Equal(report.OffBallTackles, report.Players.Sum(p => p.OffBallTackles));

            // Y ningún Detail de TACKLE se queda fuera de las tres familias conocidas. Sin esto, un Detail
            // nuevo caería por descarte en el contador del portador (que es lo que hacía el else de
            // ReportCountersAgreeWithTheEventStream) y nadie se enteraría.
            Assert.All(
                result.Events.Where(e => e.Type == EventType.Tackle),
                e => Assert.True(
                    e.Detail is "attempted" or "foul" or "won" or "missed"
                        || e.Detail.StartsWith("offBall", StringComparison.Ordinal)
                        || e.Detail.StartsWith("block", StringComparison.Ordinal),
                    $"Detail de TACKLE no clasificado: «{e.Detail}»"));

            offBallTotal += report.OffBallTackles;
        }

        Assert.True(offBallTotal > 0, $"ninguna entrada sin balón en {Matches} partidos: el test no demuestra nada");
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
    /// nada: el juego sigue. Y las dos existen: con <c>whistlePercent</c> 80 sobre 150 partidos hay de sobra
    /// de cada una.
    /// <para>
    /// Subido de 50 a 150 partidos (BB-C, independent-reviewer): con 50 semillas fijas, el arreglo de
    /// <see cref="MatchEngine.ResetPositions"/> (celebración, sin relación de código con el saque de
    /// falta) desplazó el consumo de RNG lo suficiente para que <c>sameTeam</c> pasara de 9/167 (5,4 %,
    /// justo por encima del tope) — remedido con 250 semillas: la tasa real es 2,6 % sin el arreglo y
    /// 3,7 % con él, las dos bajo el tope del 5 %. Con 50 partidos la muestra era demasiado pequeña para
    /// esta cola rara (~1-4 %) y cualquier cambio de `/Sim`, tenga o no relación con el saque de falta,
    /// puede cruzar el tope por puro tamaño de muestra — la misma firma que <c>docs/pendientes/BB-P.md</c>
    /// documenta para las puertas de build. 150 reduce ese riesgo sin encarecer el test de forma notable.
    /// </para>
    /// </summary>
    [Fact]
    public void AWhistledFoulRestartsWithAFreeKickForTheFouledTeamAndAnUnseenOneDoesNot()
    {
        int whistled = 0, freeKicks = 0, unseen = 0, unseenFollowedByFreeKick = 0, sameTeam = 0;
        for (ulong seed = 1; seed <= 150; seed++)
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

        Assert.True(whistled > 150, $"faltas señaladas en 150 partidos: {whistled}");
        Assert.True(unseen > 30, $"faltas no señaladas en 150 partidos: {unseen}");
        Assert.True(freeKicks * 100 / whistled >= 75, $"solo {freeKicks} de {whistled} faltas señaladas reanudaron con saque de falta (las demás dieron penalti o el partido acabó; medido 115 de 136)");
        // Una falta de bloqueo cometida DURANTE la cuenta atrás de otra hereda el saque pendiente de la
        // primera, que es del rival de la primera y no del suyo: es raro (medido, ~1 %) y no es un fallo
        // del saque de falta, así que se tolera como excepción acotada en vez de exigir el cero.
        Assert.True(sameTeam * 100 <= freeKicks * 5, $"{sameTeam} de {freeKicks} saques de falta fueron para el equipo que la cometió");
        // Misma excepción para la no señalada cometida durante una cuenta atrás: hereda el saque pendiente.
        Assert.True(unseenFollowedByFreeKick * 100 <= unseen * 10, $"{unseenFollowedByFreeKick} de {unseen} faltas no señaladas fueron seguidas de un saque de falta");
    }
}
