using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-B, tercer intento (docs/pendientes/BB-B.md, docs/analisis/bb-b-barrera-geometrica-diseno.md §16-17):
/// nadie roba el balón al sacador de una reanudación mientras lo conserva. Sustituye la versión del
/// segundo intento (revertida): esa medía "hubo algún Tackle en el partido durante la ventana", que
/// contaba como violación un bloqueo contra un compañero al otro lado del campo, sin relación con "robar
/// el saque" — el independent-reviewer midió que esas disputas nunca ocurren a menos de 2,08 casillas del
/// balón. 200 semillas (no 60: a 60 la muestra es no vacua solo para kickoff; a 200 lo es para tres de
/// las cinco — independent-reviewer, tercer intento). Mide dos cosas correctas: si el SACADOR fue
/// disputado (el problema literal de BB-B) y si algún rival entró alguna vez en el alcance real de
/// Tackle/Block durante la ventana, en cada fotograma, no solo en el de resolución.
/// </summary>
public sealed class RestartClearanceTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static readonly string[] ClearanceRestartDetails = { "kickoff", "throwIn", "corner", "goalKick", "freeKick" };

    /// <summary>Detalles de Tackle que significan "se disputó al portador del balón" (Utility.EvaluateTackle,
    /// rama del poseedor) y no un marcaje sin balón (<c>offBall*</c>) ni una carga (<c>block*</c>).</summary>
    private static readonly string[] BallCarrierTackleDetails = { "won", "missed", "foul" };

    /// <summary>
    /// El problema literal de BB-B: durante la ventana en la que el sacador conserva el balón, nadie le
    /// dispara un <c>Tackle</c> contra ÉL —no cualquier contacto en el partido, contra el sacador
    /// concretamente—, en ninguna de las cinco reanudaciones que llevan la barrera (el penalti no la
    /// lleva, se comprueba aparte en <see cref="OnlyTheFiveNonPenaltyRestartsCarryTheClearance"/>).
    /// </summary>
    [Fact]
    public void NobodyTacklesTheRestartTakerWhileTheyStillHaveTheBall()
    {
        var checkedByKind = new Dictionary<string, int>();
        var violationsByKind = new Dictionary<string, int>();
        foreach (var kind in ClearanceRestartDetails)
        {
            checkedByKind[kind] = 0;
            violationsByKind[kind] = 0;
        }

        for (ulong seed = 1; seed <= 200; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var events = result.Events;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type != EventType.Recovery || !checkedByKind.ContainsKey(e.Detail))
                {
                    continue;
                }

                int taker = IndexOf(trace, e.Actor);
                if (taker < 0)
                {
                    continue;
                }

                checkedByKind[e.Detail]++;

                // El bucle también corta si el sacador deja el campo (independent-reviewer, tercer
                // intento): un "dueño fantasma" del balón -jugador fuera del campo que conserva la
                // posesión hasta el final, bug preexistente y ajeno a BB-B, ver docs/pendientes/BB-O.md-
                // convertiría "la ventana" en el resto del partido y cualquier Tackle contra ese jugador
                // contaría como una falsa violación.
                int releaseFrame = trace.FrameOfTick(e.Tick);
                while (releaseFrame < trace.FrameCount && trace.BallOwnerAt(releaseFrame) == taker && trace.OnPitchAt(releaseFrame, taker))
                {
                    releaseFrame++;
                }

                int releaseTick = releaseFrame < trace.FrameCount ? trace.TickAt(releaseFrame) : int.MaxValue;

                for (int j = 0; j < events.Count; j++)
                {
                    var candidate = events[j];
                    if (candidate.Type == EventType.Tackle
                        && candidate.Opponent == e.Actor
                        && BallCarrierTackleDetails.Contains(candidate.Detail)
                        && candidate.Tick >= e.Tick && candidate.Tick < releaseTick)
                    {
                        violationsByKind[e.Detail]++;
                    }
                }
            }
        }

        // El córner no ocurrió ni una vez en 400 partidos medidos entre esta prueba y la del revisor
        // (BB-N, docs/pendientes/BB-N.md): se mide si aparece, pero no se exige cobertura mínima como a
        // las otras cuatro.
        foreach (var kind in ClearanceRestartDetails.Where(k => k != "corner"))
        {
            Assert.True(checkedByKind[kind] > 0, $"cero reanudaciones de tipo '{kind}' en 200 partidos: la prueba no cubre nada");
        }

        foreach (var kind in ClearanceRestartDetails)
        {
            Assert.True(
                violationsByKind[kind] == 0,
                $"{violationsByKind[kind]} disputa(s) contra el sacador durante la ventana de '{kind}' (de {checkedByKind[kind]} reanudaciones medidas)");
        }
    }

    /// <summary>
    /// Evidencia de distancia (punto 11 del proceso de BB-B), corregida: muestrea CADA fotograma de la
    /// ventana, no solo el de resolución (el segundo intento solo miraba ese, y por eso no vio los
    /// hermanos de banda/puerta que fallaban a mitad de cuenta atrás).
    ///
    /// <para><b>Contra qué umbral, y por qué no es <c>restartClearanceCells</c> a secas</b>
    /// (independent-reviewer, tercer intento): <c>EnforceRestartClearance</c> corrige contra la posición
    /// del balón AL EMPEZAR el tick, antes de <c>UpdateBall</c>; si el sacador regatea, el balón sigue
    /// moviéndose con él después de la corrección, así que la distancia real al fotograma siguiente puede
    /// caer por debajo de <c>restartClearanceCells</c> sin que la barrera se haya roto —medido hasta 1,595
    /// en <c>freeKick</c> con 200 semillas—. Lo que la barrera SÍ garantiza, y lo único que importa para el
    /// problema de BB-B, es que ningún rival entra en el alcance real de <c>Tackle</c>
    /// (<c>tackleDistanceMaxCells</c>, 1,0) ni de <c>Block</c> (<c>blockReachMaxCells</c>, 1,2): el umbral
    /// de esta prueba es ese alcance más un margen, no el radio nominal de la barrera.</para>
    /// </summary>
    [Fact]
    public void NoRivalIsEverCloserThanTheActionRangeDuringTheWindow()
    {
        float actionRangeFloor = Math.Max(Catalog.Ai.Context.TackleDistanceMaxCells, Catalog.Ai.Context.BlockReachMaxCells) + 0.1f;
        var samplesByKind = new Dictionary<string, int>();
        var violationsByKind = new Dictionary<string, int>();
        float worst = float.MaxValue;
        foreach (var kind in ClearanceRestartDetails)
        {
            samplesByKind[kind] = 0;
            violationsByKind[kind] = 0;
        }

        for (ulong seed = 1; seed <= 200; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Recovery || !samplesByKind.ContainsKey(e.Detail))
                {
                    continue;
                }

                int taker = IndexOf(trace, e.Actor);
                if (taker < 0)
                {
                    continue;
                }

                int takerTeam = trace.Players[taker].Team;
                int beginFrame = trace.FrameOfTick(e.Tick);
                int releaseFrame = beginFrame;
                while (releaseFrame < trace.FrameCount && trace.BallOwnerAt(releaseFrame) == taker && trace.OnPitchAt(releaseFrame, taker))
                {
                    releaseFrame++;
                }

                // El techo de duración (MatchEngine.RestartClearanceMaxTicks, RF-052) libera la barrera a
                // propósito si el sacador retiene el balón más de la cuenta: medido un caso real de 37
                // ticks reteniendo en un saque de falta, donde CUATRO rivales convergen sobre el balón en
                // cuanto expira el techo (offset 34-36) -comportamiento correcto, no la brecha que esta
                // prueba busca-. Solo se exige la barrera dentro del techo.
                int cappedReleaseFrame = Math.Min(releaseFrame, beginFrame + MatchEngine.RestartClearanceMaxTicks);
                for (int frame = beginFrame; frame < cappedReleaseFrame; frame++)
                {
                    var ball = trace.BallAt(frame);
                    float nearest = float.MaxValue;
                    for (int p = 0; p < trace.Players.Count; p++)
                    {
                        if (trace.Players[p].Team == takerTeam || !trace.OnPitchAt(frame, p))
                        {
                            continue;
                        }

                        // EXCEPCIÓN DOCUMENTADA, no una rebaja del listón (ADR 0142, y está escrita en
                        // EnforceRestartClearance desde BB-B): cuando el punto de saque cae muy cerca del
                        // área, empujar al portero fuera de la barrera lo sacaría de su área, y RF-057b
                        // manda sobre la barrera cuando compiten — así que se le devuelve dentro aunque eso
                        // lo deje más cerca del balón. El motor ya resolvía el conflicto así a propósito;
                        // lo que faltaba era que el test lo dijera en vez de pasar por suerte. Sigue siendo
                        // estricto con todos los demás, y con el portero fuera de su área.
                        var position = trace.PositionAt(frame, p);
                        if (trace.Players[p].Role == Underleague.Sim.Model.Position.Goalkeeper
                            && Underleague.Sim.Model.Pitch.IsInArea(position, trace.Players[p].Team))
                        {
                            continue;
                        }

                        // SEGUNDA EXCEPCIÓN DOCUMENTADA (27 sep 2026, revisión independiente de la ADR 0155): un
                        // rival en una ESQUINA del campo cuando la esquina cae dentro de la barrera. La barrera
                        // lo desliza por la línea hacia su lado y ahí se acaba el campo; cruzarlo por delante
                        // del balón es el salto de 3,85 casillas que SlideAlongPitchToClear prohíbe a propósito
                        // («la geometría no da más de sí», escrito en el motor desde la ADR 0142). Medido: una
                        // falta a 0,62 de la línea de fondo y 0,78 de la banda, rival clavado en (16, 0)
                        // durante 5 fotogramas. Sólo exime al que está en la esquina, no a nadie más.
                        if (IsTrappedInACorner(position, ball, Catalog.Tuning.Restart.RestartClearanceCells))
                        {
                            continue;
                        }

                        nearest = Math.Min(nearest, Vec2.Distance(position, ball));
                    }

                    if (nearest == float.MaxValue)
                    {
                        continue;
                    }

                    samplesByKind[e.Detail]++;
                    worst = Math.Min(worst, nearest);
                    if (nearest < actionRangeFloor)
                    {
                        violationsByKind[e.Detail]++;
                    }
                }
            }
        }

        foreach (var kind in ClearanceRestartDetails.Where(k => k != "corner"))
        {
            Assert.True(samplesByKind[kind] > 0, $"ninguna muestra de distancia para '{kind}': la prueba no cubre nada");
        }

        foreach (var kind in ClearanceRestartDetails)
        {
            Assert.True(
                violationsByKind[kind] == 0,
                $"{violationsByKind[kind]} de {samplesByKind[kind]} fotogramas de '{kind}' con un rival a menos de {actionRangeFloor} casillas del balón -alcance real de Tackle/Block- (peor distancia medida: {worst:F2})");
        }
    }

    /// <summary>
    /// BX-10b y BZ-B: cuando se saca, ningún rival está dentro de la barrera, tampoco pegado a una línea. Desde BZ-B los rivales
    /// salen de ella ANDANDO durante la cuenta atrás y la reanudación espera a que esté limpia, así que lo que se exige es el
    /// fotograma del saque, no todos los de la cuenta atrás. La holgura es la de la espera (0,1: la separación de cuerpos devuelve
    /// unas centésimas hacia dentro a quien llega al borde). El portero en su área queda fuera (RF-057b, ver arriba). Medido
    /// en estos 400 partidos antes de BX-10b: 328 fotogramas de rival dentro, todos sobre una línea, el peor a 0,94.
    /// </summary>
    [Fact]
    public void WhenTheRestartIsTakenNoRivalIsInsideTheClearance()
    {
        float clearance = Catalog.Tuning.Restart.RestartClearanceCells;
        int takes = 0;
        var inside = new List<string>();
        for (ulong seed = 1; seed <= 400; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            // El fotograma del saque: la espera se comprueba en ese tick, después de que los que salen andando hayan dado su paso.
            for (int f = 1; f < trace.FrameCount; f++)
            {
                var kind = trace.RestartAt(f - 1);
                int taker = trace.RestartTakerAt(f - 1);
                if (kind is not (RestartKind.FreeKick or RestartKind.ThrowIn or RestartKind.Corner or RestartKind.GoalKick)
                    || trace.RestartAt(f) == kind || taker < 0)
                {
                    continue;
                }

                takes++;
                var ball = trace.BallAt(f - 1);
                for (int p = 0; p < trace.Players.Count; p++)
                {
                    if (trace.Players[p].Team == trace.Players[taker].Team || !trace.OnPitchAt(f, p))
                    {
                        continue;
                    }

                    var position = trace.PositionAt(f, p);
                    if (trace.Players[p].Role == Underleague.Sim.Model.Position.Goalkeeper
                        && Underleague.Sim.Model.Pitch.IsInArea(position, trace.Players[p].Team))
                    {
                        continue;
                    }

                    float distance = Vec2.Distance(position, ball);
                    if (distance < clearance - 0.11f)
                    {
                        inside.Add($"semilla {seed}, fotograma {f} ({kind}): ({position.X:F2}, {position.Y:F2}) a {distance:F2}");
                    }
                }
            }
        }

        Assert.True(takes > 2000, $"muestra escasa: {takes} saques");
        Assert.True(inside.Count == 0, $"{inside.Count} rivales dentro de la barrera al sacar: {string.Join("; ", inside.Take(5))}");
    }

    /// <summary>
    /// BZ-B (decisión del revisor del 24 sep 2026: «en todas las paradas de juego… sin teletransportes»): en la cuenta atrás de
    /// banda, puerta, córner y falta nadie se desplaza en un fotograma más de lo que se anda. Antes la barrera colocaba de
    /// golpe a quien estaba dentro, casi siempre al infractor de la falta, a veces tumbado. Medido en estos 200 partidos: antes,
    /// 1956 saltos de más de 0,3 casillas en 503 849 fotogramas (0,39 %), hasta 2,08. Después quedan los del saque de puerta,
    /// que no se anda (pendiente del revisor, `docs/pendientes/BZ-B.md`), y ninguno de la barrera en banda, córner y falta.
    /// La cota (1 de cada 5000) separa las dos versiones por un orden de magnitud; la de abajo exige cero donde se anda.
    /// <para>Al sacar, que es lo que protege BB-B, lo vigila <see cref="WhenTheRestartIsTakenNoRivalIsInsideTheClearance"/>,
    /// que pasa en las dos versiones: es la guarda de que esperar no rompe la barrera.</para>
    /// </summary>
    [Fact]
    public void NobodyJumpsDuringTheCountdownOfARestartWithClearance()
    {
        int frames = 0;
        int over = 0;
        float worst = 0f;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                if (trace.RestartAt(f) is not (RestartKind.FreeKick or RestartKind.ThrowIn or RestartKind.Corner or RestartKind.GoalKick))
                {
                    continue;
                }

                for (int p = 0; p < trace.Players.Count; p++)
                {
                    if (!trace.OnPitchAt(f, p) || !trace.OnPitchAt(f - 1, p))
                    {
                        continue;
                    }

                    float step = Vec2.Distance(trace.PositionAt(f - 1, p), trace.PositionAt(f, p));
                    frames++;
                    worst = Math.Max(worst, step);
                    if (step > 0.3f)
                    {
                        over++;
                    }
                }
            }
        }

        Assert.True(frames > 100_000, $"muestra escasa: {frames} fotogramas");
        Assert.True(over * 5000 < frames, $"{over} de {frames} fotogramas con un salto de más de 0,3 casillas (máximo {worst:F2}): la barrera vuelve a colocar de golpe");
    }

    /// <summary>
    /// BX-10b (revisión independiente): casos conocidos de la barrera contra las cuatro líneas del campo (Regla J). Un rival
    /// pegado a la línea con el balón a menos de la barrera de ella acaba a la distancia de la barrera, deslizado por la
    /// línea; también si está alineado con el balón, en las dos bandas por igual (antes, en y = 7 se quedaba a 1,0).
    /// </summary>
    [Theory]
    [InlineData(1.24f, 3.0f, 0f, 3.5f)]    // línea de fondo izquierda, por encima del balón
    [InlineData(1.24f, 3.0f, 0f, 3.0f)]    // línea de fondo izquierda, alineado
    [InlineData(14.76f, 3.0f, 16f, 3.0f)]  // línea de fondo derecha, alineado
    [InlineData(1.0f, 0f, 0.3f, 0f)]       // banda de abajo, alineado
    [InlineData(1.0f, 7f, 0.3f, 7f)]       // banda de arriba, alineado
    [InlineData(15.0f, 7f, 15.7f, 7f)]     // banda de arriba, alineado, al otro lado
    [InlineData(8.0f, 0.5f, 8.0f, 0f)]     // banda de abajo, en la vertical del balón
    public void TheClearancePushesARivalOnALineOutToItsFullDistance(float ballX, float ballY, float rivalX, float rivalY)
    {
        var engine = new MatchEngine(TestMatches.Reference(Catalog, 5), 5, Catalog, SimConfig.Default);
        var point = new Vec2(ballX, ballY);
        engine.BeginRestartForTest(RestartKind.FreeKick, 0, point, 20);
        int rival = engine.OutfieldIndexForTest(1, 2);
        engine.PlaceForTest(rival, new Vec2(rivalX, rivalY));

        engine.EnforceRestartClearanceForTest();

        var position = engine.PlayerAtForTest(rival).Position;
        float clearance = Catalog.Tuning.Restart.RestartClearanceCells;
        Assert.True(Vec2.Distance(position, point) >= clearance - 0.011f, $"el rival se quedó en ({position.X:F2}, {position.Y:F2}), a {Vec2.Distance(position, point):F2} del balón");
        Assert.True(position.X is >= 0f and <= 16f && position.Y is >= 0f and <= 7f, "la barrera no saca a nadie del campo");
    }

    /// <summary>
    /// BX-10b (revisión independiente): la barrera no saca al portero de su área (RF-057b manda sobre ella). Una falta
    /// pegada al área deja al portero dentro de la barrera, y así se queda.
    /// </summary>
    [Fact]
    public void TheClearanceNeverTakesTheGoalkeeperOutOfHisArea()
    {
        var engine = new MatchEngine(TestMatches.Reference(Catalog, 5), 5, Catalog, SimConfig.Default);
        int keeper = Enumerable.Range(0, 14).First(i => engine.PlayerAtForTest(i).Team == 1 && !engine.PlayerAtForTest(i).IsOutfield);
        var area = engine.PlayerAtForTest(keeper).Position;
        foreach (var point in new[] { new Vec2(area.X - 1.0f, area.Y), new Vec2(area.X - 0.6f, area.Y + 0.4f), new Vec2(area.X - 1.2f, area.Y - 1.0f) })
        {
            engine.BeginRestartForTest(RestartKind.FreeKick, 0, point, 20);
            engine.PlaceForTest(keeper, area);
            engine.EnforceRestartClearanceForTest();
            Assert.True(Underleague.Sim.Model.Pitch.IsInArea(engine.PlayerAtForTest(keeper).Position, 1), $"el portero salió de su área con la falta en ({point.X:F2}, {point.Y:F2})");
        }
    }

    /// <summary>
    /// BZ-B: donde la barrera se anda (banda, córner y falta), ninguna barrera coloca de golpe a nadie en la cuenta atrás. Se
    /// excluye el fotograma en que se pita (el derribo del propio contacto puede mover a alguien ~0,3) y se exige cero.
    /// </summary>
    [Fact]
    public void WhereTheClearanceIsWalkedNobodyIsPlacedByIt()
    {
        float clearance = Catalog.Tuning.Restart.RestartClearanceCells;
        var placed = new List<string>();
        int frames = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            for (int f = 2; f < trace.FrameCount; f++)
            {
                var kind = trace.RestartAt(f);
                if (kind is not (RestartKind.FreeKick or RestartKind.ThrowIn or RestartKind.Corner) || trace.RestartAt(f - 1) != kind)
                {
                    continue;
                }

                var ball = trace.BallAt(f);
                for (int p = 0; p < trace.Players.Count; p++)
                {
                    if (!trace.OnPitchAt(f, p) || !trace.OnPitchAt(f - 1, p))
                    {
                        continue;
                    }

                    frames++;
                    float step = Vec2.Distance(trace.PositionAt(f - 1, p), trace.PositionAt(f, p));
                    // La firma de la colocación de golpe: un paso de más de lo que se anda que deja al jugador en el borde.
                    if (step > 0.3f && MathF.Abs(Vec2.Distance(trace.PositionAt(f, p), ball) - clearance) < 0.02f)
                    {
                        placed.Add($"semilla {seed}, fotograma {f} ({kind}): jugador {trace.Players[p].Id}, {step:F2}");
                    }
                }
            }
        }

        Assert.True(frames > 50_000, $"muestra escasa: {frames} fotogramas");
        Assert.True(placed.Count == 0, $"{placed.Count} colocaciones de golpe: {string.Join("; ", placed.Take(5))}");
    }

    /// <summary>
    /// BZ-B: esperar a que se salga andando de la barrera no alarga sin fin la reanudación. Ninguna llega al tope de espera
    /// (<c>kickoffMaxWaitTicks</c>), y la falta, la que espera (el infractor está casi siempre dentro), dura como mucho su
    /// cuenta atrás más 20 ticks. Medido en estos 200 partidos: mediana 26, máximo 30, sobre 20 de cuenta atrás.
    /// </summary>
    [Fact]
    public void WaitingForTheClearanceNeverReachesTheCap()
    {
        var restart = Catalog.Tuning.Restart;
        var longest = new Dictionary<RestartKind, int>();
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true)).Trace!;
            int run = 0;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                var kind = trace.RestartAt(f);
                if (kind != RestartKind.None && trace.RestartAt(f - 1) == kind)
                {
                    run++;
                    continue;
                }

                var previous = trace.RestartAt(f - 1);
                if (previous is RestartKind.FreeKick or RestartKind.ThrowIn or RestartKind.Corner)
                {
                    longest[previous] = Math.Max(longest.GetValueOrDefault(previous), run + 1);
                }

                run = 0;
            }
        }

        Assert.True(longest.GetValueOrDefault(RestartKind.FreeKick) > 0, "ninguna falta en 200 partidos");
        Assert.True(longest[RestartKind.FreeKick] <= restart.FreeKickTicks + 20, $"una falta duró {longest[RestartKind.FreeKick]} ticks");
        Assert.True(longest.Values.All(v => v < restart.KickoffMaxWaitTicks), $"alguna reanudación llegó al tope de espera: {string.Join(", ", longest.Select(x => $"{x.Key} {x.Value}"))}");
    }

    /// <summary>
    /// El penalti NO lleva la barrera (RF-054, ADR 0090 lo dejó intocado; el segundo intento de BB-B lo
    /// tocó sin querer porque la guarda de <c>Step</c> comprobaba <c>wasRestarting</c> sin mirar el tipo de
    /// reanudación). Prueba directa y barata de <see cref="MatchEngine.IsClearanceRestart"/> —expuesta
    /// <c>internal</c> para esto— en vez de montar un partido completo a buscar un penalti: exactamente
    /// las cinco que <c>TakeRestart</c> ya sabe sacar (AZ-A) llevan la barrera, y el penalti, que tiene su
    /// propio <c>TakePenalty</c>, no.
    /// </summary>
    [Fact]
    public void OnlyTheFiveNonPenaltyRestartsCarryTheClearance()
    {
        // [Theory]/[InlineData] no puede llevar RestartKind en la firma pública del método
        // (CS0051: el enum es internal, aunque InternalsVisibleTo lo haga visible aquí) — de ahí las seis
        // aserciones sueltas en vez de datos parametrizados.
        Assert.True(MatchEngine.IsClearanceRestart(RestartKind.ThrowIn));
        Assert.True(MatchEngine.IsClearanceRestart(RestartKind.Corner));
        Assert.True(MatchEngine.IsClearanceRestart(RestartKind.GoalKick));
        Assert.True(MatchEngine.IsClearanceRestart(RestartKind.Kickoff));
        Assert.True(MatchEngine.IsClearanceRestart(RestartKind.FreeKick));
        Assert.False(MatchEngine.IsClearanceRestart(RestartKind.Penalty));
        Assert.False(MatchEngine.IsClearanceRestart(RestartKind.None));
    }

    /// <summary>
    /// El techo de duración (independent-reviewer, tercer intento: "no tiene test propio, es barato y es
    /// el 100% de la corrección 2"). Busca en la muestra una retención real más larga que el techo —las
    /// hay: un saque de falta puede llegar a 37 ticks reteniendo— y comprueba la frontera exacta: dentro
    /// del techo, ningún rival en el alcance real de acción; en cuanto se supera, deja de exigirse (no que
    /// falle necesariamente, que dentro del techo no se pueda decir nada — es la liberación a propósito,
    /// no una brecha).
    /// </summary>
    [Fact]
    public void TheClearanceReleasesExactlyAtTheDurationCapAndNotBefore()
    {
        // REPLANTEADO (ADR 0144). Buscaba en partidos reales un sacador que RETUVIERA el balón más de
        // treinta ticks, y dejó de encontrarlo ni en seiscientos: desde que el sacador decide dentro de su
        // reanudación (ADR 0143) y puede dársela a un compañero presionado (ADR 0141), encuentra
        // destinatario en unos pocos ticks. La red de seguridad de RF-052 sigue haciendo falta —un sacador
        // atascado no puede congelar la barrera— pero perseguirla por semillas es buscar un caso que el
        // motor ya no produce. Se afirma la regla, que es lo que el nombre del test dice.
        int cap = MatchEngine.RestartClearanceMaxTicks;

        // Mientras la tenga y no se pase del techo, la barrera AGUANTA.
        Assert.False(MatchEngine.ShouldReleaseClearance(stillHasTheBall: true, ticksHeld: 1));
        Assert.False(MatchEngine.ShouldReleaseClearance(stillHasTheBall: true, ticksHeld: cap - 1));
        Assert.False(MatchEngine.ShouldReleaseClearance(stillHasTheBall: true, ticksHeld: cap));

        // Justo al pasarse, se libera: ni antes ni más tarde.
        Assert.True(MatchEngine.ShouldReleaseClearance(stillHasTheBall: true, ticksHeld: cap + 1));

        // Y soltar el balón la libera en el acto, que es el caso normal.
        Assert.True(MatchEngine.ShouldReleaseClearance(stillHasTheBall: false, ticksHeld: 1));
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// ¿Está el jugador pegado a una esquina del campo que cae dentro de la barrera del balón? Es el único
    /// sitio desde el que la barrera no puede sacarlo sin cruzarlo por delante del balón.
    /// </summary>
    private static bool IsTrappedInACorner(Vec2 position, Vec2 ball, float clearance)
    {
        foreach (var corner in new[]
                 {
                     new Vec2(0f, 0f), new Vec2(0f, Underleague.Sim.Model.Pitch.Rows),
                     new Vec2(Underleague.Sim.Model.Pitch.Columns, 0f), new Vec2(Underleague.Sim.Model.Pitch.Columns, Underleague.Sim.Model.Pitch.Rows),
                 })
        {
            if (Vec2.Distance(corner, ball) < clearance && Vec2.Distance(position, corner) < 0.25f)
            {
                return true;
            }
        }

        return false;
    }
}
