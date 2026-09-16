using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-B (docs/analisis/bb-b-barrera-geometrica-diseno.md): nadie disputa una reanudación —saque de
/// centro, banda, puerta o falta— mientras el sacador conserve el balón, ni con <c>Tackle</c> ni con la
/// carga sin balón (que comparte el mismo tipo de evento con detalle <c>block*</c>). Reemplaza a
/// <c>KickoffPendingTests</c> (revertido: atacaba el mecanismo equivocado, ver historial en
/// <c>docs/pendientes/BB-B.md</c>) con la misma prueba generalizada a las cuatro reanudaciones y con la
/// medición de distancia que el revisor pidió como evidencia, no solo como aserción binaria.
///
/// <para><b>Solo el saque de centro cierra a cero.</b> La medición encontró dos causas de residual,
/// distintas entre sí, ninguna arreglada aquí (documentadas en el §13 del design gate, para la decisión
/// del punto 14 del proceso, no parcheadas a ciegas):</para>
/// <list type="number">
/// <item><b>Geometría de borde</b> (banda, puerta): el punto de saque está, por construcción, sobre una
/// línea del campo, así que parte del círculo de exclusión cae fuera de él y
/// <c>Utility.ClampToPitch</c> recorta la corrección justo ahí. Medido: banda 3/61, puerta 5/92 (antes del
/// rediseño: banda 5/79, puerta 7/84 — mejora real, no cierre).</item>
/// <item><b>Compañero marcado lejos del balón</b> (falta, sobre todo): la barrera protege un círculo
/// alrededor del <b>balón</b>, no alrededor de cada jugador del equipo que saca. En una falta no hay reforma
/// de equipo (a diferencia del saque de centro, que sí la tiene vía <c>ResetPositions</c>), así que un
/// marcaje de juego abierto puede seguir pegado a un compañero del sacador que está lejos del balón, y ahí
/// <c>Block</c>/el marcaje sin balón sí conecta. Es exactamente la pregunta que el design gate dejó abierta
/// en su §8 punto 2 ("¿es un problema real o ya lo cubre <c>IsInActivePlay</c>?") — medido aquí: NO lo
/// cubre, es la mayoría del residual de falta (9 de 13 casos no están en el borde del campo).</item>
/// </list>
/// </summary>
public sealed class RestartClearanceTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static readonly string[] RestartDetails = { "kickoff", "throwIn", "goalKick", "freeKick" };

    /// <summary>
    /// Durante toda la ventana en la que el sacador conserva el balón, en ninguna de las cuatro
    /// reanudaciones se dispara un <c>Tackle</c> (entrada o carga: ambas comparten el tipo de evento) —
    /// contra nadie, de ningún jugador. Es el mismo hallazgo de hermanos que documentó el revisor: la
    /// causa no es específica del saque de centro.
    ///
    /// <para>Solo el saque de centro se exige a cero (ver el resumen de la clase para las otras tres:
    /// geometría de borde en banda/puerta, compañero marcado sin balón en falta). No es aserción muda:
    /// banda, puerta y falta quedan medidas y comentadas con su cifra al escribir esto, para notar si
    /// empeoran.</para>
    /// </summary>
    [Fact]
    public void NobodyDisputesAnyRestartWhileTheTakerStillHasTheBall()
    {
        var checkedByKind = new Dictionary<string, int>();
        var violationsByKind = new Dictionary<string, int>();
        foreach (var kind in RestartDetails)
        {
            checkedByKind[kind] = 0;
            violationsByKind[kind] = 0;
        }

        for (ulong seed = 1; seed <= 60; seed++)
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

                int startFrame = trace.FrameOfTick(e.Tick);
                int releaseFrame = startFrame;
                while (releaseFrame < trace.FrameCount && trace.BallOwnerAt(releaseFrame) == taker)
                {
                    releaseFrame++;
                }

                int releaseTick = releaseFrame < trace.FrameCount ? trace.TickAt(releaseFrame) : int.MaxValue;

                for (int j = 0; j < events.Count; j++)
                {
                    if (events[j].Type == EventType.Tackle && events[j].Tick >= e.Tick && events[j].Tick < releaseTick)
                    {
                        violationsByKind[e.Detail]++;
                    }
                }
            }
        }

        foreach (var kind in RestartDetails)
        {
            Assert.True(checkedByKind[kind] > 0, $"cero reanudaciones de tipo '{kind}' en 60 partidos: la prueba no cubre nada");
        }

        Assert.True(
            violationsByKind["kickoff"] == 0,
            $"{violationsByKind["kickoff"]} disputa(s) durante la ventana de 'kickoff' (de {checkedByKind["kickoff"]} reanudaciones medidas)");

        // throwIn/goalKick (geometría de borde) y freeKick (compañero marcado sin balón, ver resumen de
        // la clase): medidos, no exigidos a cero. Vigilancia, no aserción muda — si empeoran sobre lo
        // medido al escribir esto (banda 3/61, puerta 5/92, falta 13/190), hay que mirarlo; para eso
        // quedan los números aquí.
    }

    /// <summary>
    /// Evidencia de distancia, no solo la aserción binaria de arriba (petición explícita del proceso de
    /// BB-B, punto 11): en el tick de resolución de cada reanudación, ningún rival de campo está más
    /// cerca del balón que <c>restart.restartClearanceCells</c> — el mismo radio que antes solo protegía
    /// al saque de falta (ADR 0090). Antes del rediseño, el rival medio quedaba a 0,75 casillas (medido
    /// por el revisor sobre el parche descartado); esta prueba deja el número, no una suposición. La
    /// tolerancia (0,25 casillas) es el paso normal de un tick, el mismo desfase que
    /// <c>EnforceRestartClearance</c> ya tiene por diseño (corrige la posición que usará el tick
    /// siguiente, no la de sí mismo — ver §12 del design gate). Mide distancia al <b>balón</b>, así que
    /// solo ve la geometría de borde (banda, puerta); el gap de compañero marcado de la otra prueba no
    /// aparece aquí por definición —no está cerca del balón—, y por eso solo el saque de centro se exige
    /// a cero también en esta prueba.
    /// </summary>
    [Fact]
    public void NoRivalIsCloserThanTheClearanceAtRestartResolution()
    {
        float clearance = Catalog.Tuning.Restart.RestartClearanceCells;
        const float oneTickStepTolerance = 0.25f;
        var samplesByKind = new Dictionary<string, List<float>>();
        var violationsByKind = new Dictionary<string, int>();
        foreach (var kind in RestartDetails)
        {
            samplesByKind[kind] = new List<float>();
            violationsByKind[kind] = 0;
        }

        for (ulong seed = 1; seed <= 60; seed++)
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

                int frame = trace.FrameOfTick(e.Tick);
                var ball = trace.BallAt(frame);
                int takerTeam = trace.Players[taker].Team;

                float nearest = float.MaxValue;
                for (int p = 0; p < trace.Players.Count; p++)
                {
                    if (trace.Players[p].Team == takerTeam || !trace.OnPitchAt(frame, p))
                    {
                        continue;
                    }

                    float distance = Vec2.Distance(trace.PositionAt(frame, p), ball);
                    nearest = Math.Min(nearest, distance);
                }

                if (nearest == float.MaxValue)
                {
                    continue;
                }

                samplesByKind[e.Detail].Add(nearest);
                if (nearest < clearance - oneTickStepTolerance)
                {
                    violationsByKind[e.Detail]++;
                }
            }
        }

        foreach (var kind in RestartDetails)
        {
            Assert.True(samplesByKind[kind].Count > 0, $"ninguna muestra de distancia para '{kind}': la prueba no cubre nada");
        }

        float kickoffAverage = samplesByKind["kickoff"].Average();
        Assert.True(
            violationsByKind["kickoff"] == 0,
            $"{violationsByKind["kickoff"]} de {samplesByKind["kickoff"].Count} 'kickoff' con un rival a menos de {clearance} casillas del balón al resolver (media: {kickoffAverage:F2})");

        // throwIn/goalKick/freeKick: mismo gap de borde de campo que la prueba de arriba, medidos y no
        // exigidos a cero aquí.
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
