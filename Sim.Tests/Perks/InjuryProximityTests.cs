using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-M: toda lesión tiene un causante **al lado**. Nace de una anotación del revisor —«Sed de médula me
/// lesionó a un jugador lejos de la acción, sin ningún rival cerca»— y de la instrumentación que pidió
/// antes de tocar la ADR 0048: medir sobre el motor, no sobre lo que pinta la pantalla.
///
/// <para>Lo que la medición contestó: el motor está bien. Lo que engaña es que <c>ResolveInjury</c> llama a
/// <c>LeavePitch</c>, que pone la posición del lesionado en <b>(-1,-1)</b>, así que en el tick de la lesión
/// el jugador ya no está donde se lesionó: ha desaparecido del campo. Eso es lo que se ve como «sale
/// volando» (BB-L) y lo que hace que al rebobinar no se le encuentre.</para>
///
/// <para>Este test fija la propiedad real para que no se rompa: en el tick <b>anterior</b> a la lesión, el
/// causante estaba pegado a la víctima.</para>
/// </summary>
public sealed class InjuryProximityTests
{
    /// <summary>Radio de contacto: una entrada o un bloqueo ocurren cuerpo a cuerpo (ADR 0020, ADR 0105).</summary>
    private const double ContactCells = 2.0;

    [Fact]
    public void EveryInjuryHasItsCauserRightNextToTheVictim()
    {
        var catalog = TestData.LoadCatalog();
        int measured = 0;
        int withoutCauser = 0;

        for (ulong seed = 1; seed <= 150; seed++)
        {
            var setup = TestMatches.Reference(catalog, seed);
            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Injury && e.Type != EventType.Death)
                {
                    continue;
                }

                if (e.Opponent < 0)
                {
                    // Una MUERTE por perk letal no lleva causante (Kill la emite sin rival), y eso es
                    // legítimo. Una LESIÓN sin causante no lo sería: se cuenta para afirmarlo aparte.
                    if (e.Type == EventType.Injury)
                    {
                        withoutCauser++;
                    }

                    continue;
                }

                int victim = IndexOf(trace, e.Actor);
                int causer = IndexOf(trace, e.Opponent);
                if (victim < 0 || causer < 0)
                {
                    continue;
                }

                // El tick ANTERIOR: en el de la lesión la víctima ya está en (-1,-1).
                int frame = Math.Max(0, trace.FrameOfTick(e.Tick) - 1);
                var v = trace.PositionAt(frame, victim);
                var c = trace.PositionAt(frame, causer);
                double distance = Math.Sqrt(((v.X - c.X) * (v.X - c.X)) + ((v.Y - c.Y) * (v.Y - c.Y)));

                Assert.True(
                    distance <= ContactCells,
                    $"semilla {seed}, tick {e.Tick}: {e.Type} de {e.Actor} atribuida a {e.Opponent}, "
                        + $"que estaba a {distance:F1} casillas. Una lesión sin causante al lado es daño no "
                        + "anunciado y rompe la regla 11");
                measured++;
            }
        }

        Assert.Equal(0, withoutCauser);
        Assert.True(measured >= 25, $"solo se midieron {measured} bajas en 150 partidos: el test no prueba nada");
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
