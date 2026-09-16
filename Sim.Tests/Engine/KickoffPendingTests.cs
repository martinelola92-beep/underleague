using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-B: nadie disputa el saque de centro hasta que el sacador lo pone en juego. Antes de este cambio,
/// durante la ventana de reanudación solo el sacador quedaba congelado; en cuanto la fase volvía a
/// OpenPlay el rival podía entrarle en el mismo tick en que recibía el balón. La precondición vive en
/// <c>UtilityContext.KickoffPending</c>, explícita, no un offset de distancia ni una penalización global.
/// </summary>
public sealed class KickoffPendingTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Durante toda la ventana en la que el sacador conserva el balón, ningún TACKLE se dispara en el
    /// partido — de ningún jugador, contra nadie: es el estado, no un objetivo concreto, el que se
    /// descarta (ver el hallazgo de la rama sin balón en <c>Utility.EvaluateTackle</c>).
    /// </summary>
    [Fact]
    public void NobodyTacklesWhileTheKickoffIsStillPending()
    {
        int kickoffsChecked = 0;
        int tacklesAfterKickoff = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var events = result.Events;

            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type != EventType.Recovery || e.Detail != "kickoff")
                {
                    continue;
                }

                kickoffsChecked++;
                int taker = IndexOf(trace, e.Actor);
                if (taker < 0)
                {
                    continue;
                }

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
                        tacklesAfterKickoff++;
                    }
                }
            }
        }

        Assert.True(kickoffsChecked >= 30, $"solo se vieron {kickoffsChecked} saques de centro en 60 partidos: la prueba no cubre nada");
        Assert.Equal(0, tacklesAfterKickoff);
    }

    /// <summary>
    /// Tras la puesta en juego, el comportamiento normal vuelve: se ven entradas en el resto del
    /// partido. Sin esto, un fallo que descartara `Tackle` para siempre pasaría la prueba de arriba.
    /// </summary>
    [Fact]
    public void TacklingResumesNormallyAfterKickoff()
    {
        int totalTackles = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default);
            totalTackles += result.Events.Count(e => e.Type == EventType.Tackle);
        }

        Assert.True(totalTackles > 0, "ninguna entrada en 10 partidos: el gate podría estar bloqueando Tackle permanentemente");
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
