using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BX-7 (`docs/pendientes/BX-7.md`): el saque de centro que abre la turba recoloca andando, y quien volvía desde campo
/// contrario con su sitio detrás del balón se quedaba clavado en el borde de la barrera de la reanudación hasta el tope de
/// espera. Ahora rodea el círculo (<see cref="MatchEngine.DetourAroundDisc"/>).
/// </summary>
public sealed class MobKickoffDetourTests
{
    private static readonly Vec2 Center = new(8f, 3.5f);

    [Fact]
    public void AStraightPathThatDoesNotCrossTheDiscIsUntouched()
    {
        var to = new Vec2(4.5f, 4.5f);
        Assert.Equal(to, MatchEngine.DetourAroundDisc(new Vec2(5f, 1f), to, Center, 2.05f));
    }

    [Fact]
    public void ADestinationInsideTheDiscReachedFromOutsideIsUntouched()
    {
        // El equipo que no saca tiene su casilla de saque dentro de la barrera (círculo central 1,4 < barrera 2,0): quien
        // viene de su propio campo no lo cruza, sólo se acerca.
        var to = new Vec2(6.5f, 3.5f);
        Assert.Equal(to, MatchEngine.DetourAroundDisc(new Vec2(3f, 3.5f), to, Center, 2.05f));
    }

    [Fact]
    public void APathThroughTheDiscGoesToATangentPointOnTheSideOfTheDestination()
    {
        var from = new Vec2(11f, 3f);
        var to = new Vec2(4.5f, 4.5f);
        var waypoint = MatchEngine.DetourAroundDisc(from, to, Center, 2.05f);

        Assert.Equal(2.05f, Vec2.Distance(waypoint, Center), 3);
        Assert.True(waypoint.Y > Center.Y, $"rodea por abajo, el lado del destino: {waypoint.X},{waypoint.Y}");
    }

    [Fact]
    public void OnTheRingItWalksAlongTheTangent()
    {
        var from = new Vec2(9.9f, 2.89f);
        var to = new Vec2(4.5f, 4.5f);
        var waypoint = MatchEngine.DetourAroundDisc(from, to, Center, 2.05f);

        var radial = from - Center;
        var move = waypoint - from;
        float dot = (radial.X * move.X) + (radial.Y * move.Y);
        Assert.True(MathF.Abs(dot) < 0.01f, $"tangente al círculo: producto escalar {dot}");
    }

    [Fact]
    public void NoMobKickoffWaitsUntilTheCap()
    {
        // Antes del arreglo: 12 de 71 turbas en las semillas 1..200 (3 en 1..60: semillas 10, 26 y 28) agotaban el tope.
        var catalog = TestData.LoadCatalog();
        int cap = catalog.Tuning.Restart.KickoffTicks + catalog.Tuning.Restart.KickoffMaxWaitTicks;
        int mobs = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, new SimConfig(CollectLog: false, Trace: true));
            if (!result.Report.WentToGoldenGoal)
            {
                continue;
            }

            var trace = result.Trace!;
            int first = 0;
            while (first < trace.FrameCount && trace.PhaseAt(first) != MatchPhase.MobGoldenGoal)
            {
                first++;
            }

            int start = first;
            while (start > 0 && trace.RestartAt(start - 1) == RestartKind.Kickoff)
            {
                start--;
            }

            int end = start;
            while (end < trace.FrameCount && trace.RestartAt(end) == RestartKind.Kickoff)
            {
                end++;
            }

            mobs++;

            // Al sacar, nadie del equipo que no saca sigue en campo contrario (antes del arreglo, el atascado sí).
            int last = end - 1;
            int taker = trace.RestartTakerAt(last);
            Assert.True(taker >= 0, $"semilla {seed}: saque sin sacador");
            int takingTeam = trace.Players[taker].Team;
            for (int slot = 0; slot < trace.Players.Count; slot++)
            {
                if (trace.Players[slot].Team == takingTeam || !trace.OnPitchAt(last, slot))
                {
                    continue;
                }

                float column = trace.PositionAt(last, slot).X;
                float middle = Pitch.Columns / 2f;
                bool inRivalHalf = Pitch.AttackDirection(trace.Players[slot].Team) > 0 ? column > middle + 0.05f : column < middle - 0.05f;
                Assert.False(inRivalHalf, $"semilla {seed}: el jugador {slot} sigue en campo contrario al sacar (columna {column:F2})");
            }

            Assert.True(end - start < cap, $"semilla {seed}: el saque de la turba esperó {end - start} ticks (tope {cap})");
        }

        Assert.True(mobs >= 15, $"pocas turbas para que la prueba diga algo: {mobs}");
    }
}
