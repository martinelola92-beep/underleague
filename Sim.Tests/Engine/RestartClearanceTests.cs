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
}
