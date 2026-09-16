using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-C (docs/pendientes/BB-C.md): un goleador celebrando no se teletransporta a su casilla-hogar con el
/// resto del equipo cuando <see cref="MatchEngine"/> reforma la alineación para el saque de centro
/// siguiente. Antes de la corrección, <c>ResetPositions</c> movía la posición de todo jugador en el campo
/// sin comprobar su estado, así que el marcador aparecía "celebrando" ya de vuelta en su sitio, en el
/// mismo tick del gol (medido: hasta 9,37 casillas de salto en una semilla real).
/// </summary>
public sealed class GoalCelebrationPositionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Misma cota que <see cref="MatchRulesTests.TheRestartTakerStandsStillDuringTheDeadBall"/>
    /// (independent-reviewer, BB-C): el paso máximo real observado con el arreglo puesto es 0,21
    /// casillas/tick; el salto de <c>ResetPositions</c> sin el arreglo mide entre 4,61 y 10,05 (mínimo y
    /// máximo sobre 113 goles reales). 0,6 separa ambos con margen amplio a los dos lados, a diferencia de
    /// una cota floja que solo detecte el caso más extremo.
    /// </summary>
    private const float MaxNormalStepCells = 0.6f;

    [Fact]
    public void TheScorerDoesNotJumpToItsHomePositionOnTheSameTickAsTheGoal()
    {
        int goalsChecked = 0;

        for (ulong seed = 1; seed <= 60; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var evt in result.Events)
            {
                // Solo el gol "de verdad": el de oro (goldenGoal) acaba el partido sin pasar por
                // ScheduleKickoff/ResetPositions, y uno anulado por un perk (":cancelled", EmitCancellable)
                // tampoco entra en Celebrating ni dispara la reanudación. Ninguno de los dos ejercita lo
                // que este test comprueba, y contarlos como "gol comprobado" infla goalsChecked sin probar
                // nada (independent-reviewer, BB-C: 22 de 135 goles de una muestra eran de oro).
                if (evt.Type != EventType.Goal || evt.Detail != "goal")
                {
                    continue;
                }

                int scorerSlot = FindSlot(trace, evt.Actor);
                int goalFrame = trace.FrameOfTick(evt.Tick);
                if (goalFrame <= 0 || scorerSlot < 0)
                {
                    continue;
                }

                // El invariante real no es solo "no se movió mucho": es "sigue celebrando" (si la
                // celebración dejara de dispararse, el jugador volvería a HomeCenter legítimamente sin
                // saltar, y una comprobación de solo distancia no lo distinguiría del bug).
                Assert.Equal(PlayerState.Celebrating, trace.StateAt(goalFrame, scorerSlot));

                var beforeShot = trace.PositionAt(goalFrame - 1, scorerSlot);
                var atGoalTick = trace.PositionAt(goalFrame, scorerSlot);
                float jump = Vec2.Distance(beforeShot, atGoalTick);

                Assert.True(
                    jump <= MaxNormalStepCells,
                    $"seed {seed}, tick {evt.Tick}: el goleador (id {evt.Actor}) saltó {jump:F2} casillas " +
                    "en el mismo tick del gol - sugiere que ResetPositions le movió a su casilla-hogar mientras celebraba.");

                goalsChecked++;
            }
        }

        Assert.True(goalsChecked > 0, "ningún gol real (no de oro, no anulado) encontrado en 60 semillas: el test no comprobó nada");
    }

    private static int FindSlot(MatchTrace trace, int playerId)
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
