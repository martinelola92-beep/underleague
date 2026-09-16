using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-B, tercer intento (docs/pendientes/BB-B.md, docs/analisis/bb-b-barrera-geometrica-diseno.md §16):
/// nadie roba el balón al sacador de una reanudación mientras lo conserva. Sustituye la versión anterior
/// de este fichero (revertida): esa medía "hubo algún Tackle en el partido durante la ventana", que
/// contaba como violación un bloqueo contra un compañero al otro lado del campo, sin relación con "robar
/// el saque". El independent-reviewer midió que esas disputas nunca ocurren a menos de 2,08 casillas del
/// balón (la barrera no se rompe por ahí) y que la métrica estaba mal planteada. Esta versión mide dos
/// cosas correctas: si el SACADOR fue disputado (el problema literal de BB-B) y si algún rival estuvo
/// alguna vez dentro del radio de exclusión durante toda la ventana, no solo en el tick de resolución.
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

                int releaseFrame = trace.FrameOfTick(e.Tick);
                while (releaseFrame < trace.FrameCount && trace.BallOwnerAt(releaseFrame) == taker)
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

        // El córner no ocurrió ni una vez en 60 semillas (BB-N, docs/pendientes/BB-N.md): se mide si
        // aparece, pero no se exige cobertura mínima como a las otras cuatro.
        foreach (var kind in ClearanceRestartDetails.Where(k => k != "corner"))
        {
            Assert.True(checkedByKind[kind] > 0, $"cero reanudaciones de tipo '{kind}' en 60 partidos: la prueba no cubre nada");
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
    /// hermanos de banda/puerta que fallaban a mitad de cuenta atrás). Tolerancia de 0,25 casillas: el
    /// desfase de un tick que <c>EnforceRestartClearance</c> tiene por diseño (corrige la posición que
    /// usará el tick siguiente, no la de sí mismo).
    /// </summary>
    [Fact]
    public void NoRivalIsEverCloserThanTheClearanceDuringTheWindow()
    {
        float clearance = Catalog.Tuning.Restart.RestartClearanceCells;
        const float oneTickStepTolerance = 0.25f;
        var samplesByKind = new Dictionary<string, int>();
        var violationsByKind = new Dictionary<string, int>();
        float worst = float.MaxValue;
        foreach (var kind in ClearanceRestartDetails)
        {
            samplesByKind[kind] = 0;
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

                int takerTeam = trace.Players[taker].Team;
                int beginFrame = trace.FrameOfTick(e.Tick);
                int releaseFrame = beginFrame;
                while (releaseFrame < trace.FrameCount && trace.BallOwnerAt(releaseFrame) == taker)
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

                        nearest = Math.Min(nearest, Vec2.Distance(trace.PositionAt(frame, p), ball));
                    }

                    if (nearest == float.MaxValue)
                    {
                        continue;
                    }

                    samplesByKind[e.Detail]++;
                    worst = Math.Min(worst, nearest);
                    if (nearest < clearance - oneTickStepTolerance)
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
                $"{violationsByKind[kind]} de {samplesByKind[kind]} fotogramas de '{kind}' con un rival a menos de {clearance} casillas del balón (peor distancia medida: {worst:F2})");
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
        // [Theory]/[InlineData] no puede llevar MatchEngine.RestartKind en la firma pública del método
        // (CS0051: el enum es internal, aunque InternalsVisibleTo lo haga visible aquí) — de ahí las seis
        // aserciones sueltas en vez de datos parametrizados.
        Assert.True(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.ThrowIn));
        Assert.True(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.Corner));
        Assert.True(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.GoalKick));
        Assert.True(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.Kickoff));
        Assert.True(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.FreeKick));
        Assert.False(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.Penalty));
        Assert.False(MatchEngine.IsClearanceRestart(MatchEngine.RestartKind.None));
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
