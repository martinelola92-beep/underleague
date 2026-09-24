using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BC-A (docs/pendientes/BC-A.md): cuando el rival saca de centro, el equipo que acaba de marcar tiene que
/// estar <b>en su propio campo</b>. Lo reportó el revisor jugando: el goleador se quedaba plantado en campo
/// contrario mientras el otro equipo sacaba.
///
/// <para>Es el hermano de <see cref="GoalCelebrationPositionTests"/> y la otra mitad del mismo defecto.
/// Aquel prohíbe teletransportar al que celebra; éste exige que, aun sin teletransporte, acabe volviendo.
/// Sin los dos a la vez, cualquiera de los dos arreglos rompe el otro: no saltar es fácil si nadie te pide
/// que vuelvas, y volver es fácil si se te permite saltar.</para>
/// </summary>
public sealed class KickoffFormationTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Media casilla de gracia sobre la línea de medio campo: la tolerancia con la que la reanudación da
    /// a un jugador por colocado (<c>restart.inPlaceCells</c>) más el paso de un tick. Lo que este test
    /// caza no es medio metro, es un jugador entero plantado en el área rival.
    /// </summary>
    private const float HalfwayToleranceCells = 0.9f;

    [Fact]
    public void NobodyFromTheScoringTeamIsStillInTheRivalHalfWhenTheKickoffIsTaken()
    {
        int kickoffsChecked = 0;
        int offending = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                // RECOVERY con detalle "kickoff" es el tick en el que el sacador toca el balón: el saque
                // ya se ha producido, así que es el fotograma en el que la formación tiene que estar hecha.
                if (e.Type != EventType.Recovery || e.Detail != "kickoff")
                {
                    continue;
                }

                int frame = trace.FrameOfTick(e.Tick);
                int taker = IndexOf(trace, e.Actor);
                if (frame <= 0 || taker < 0)
                {
                    continue;
                }

                // El saque de centro del arranque del partido no prueba nada: nadie venía de ningún sitio.
                int scoringTeam = 1 - trace.Players[taker].Team;
                if (e.Tick <= 1)
                {
                    continue;
                }

                kickoffsChecked++;
                for (int slot = 0; slot < trace.Players.Count; slot++)
                {
                    if (trace.Players[slot].Team != scoringTeam || !trace.OnPitchAt(frame, slot))
                    {
                        continue;
                    }

                    float column = trace.PositionAt(frame, slot).X;
                    float middle = Pitch.Columns / 2f;
                    bool inRivalHalf = Pitch.AttackDirection(scoringTeam) > 0
                        ? column > middle + HalfwayToleranceCells
                        : column < middle - HalfwayToleranceCells;

                    if (inRivalHalf)
                    {
                        offending++;
                    }
                }
            }
        }

        Assert.True(kickoffsChecked > 0, "ningún saque de centro tras gol en 60 semillas: el test no comprobó nada");
        Assert.Equal(0, offending);
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int slot = 0; slot < trace.Players.Count; slot++)
        {
            if (trace.Players[slot].Id == playerId)
            {
                return slot;
            }
        }

        return -1;
    }
}
