using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0185 (hermano encontrado midiendo, independiente del arranque): una reanudación pedida a mitad del bucle de jugadores
/// —una falta se resuelve dentro de él— dejaba el balón vivo para la utilidad el resto de ese tick (<c>BallDead</c> se leía
/// al principio). Quien decidía después podía empezar una entrada sin balón o una carga, que se resolvía ya en la cuenta
/// atrás: medido en 300 partidos de referencia, 9 contactos resueltos con un saque de falta pendiente, con y sin arranque
/// (semilla 81, tick 1287: un bloqueo tumbó al sacador de la falta). RF-057 / ADR 0132: sin jugada no hay contacto.
/// </summary>
public sealed class RestartContactTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Ningún TACKLE resuelto (entrada, entrada sin balón o carga) con una reanudación pendiente en el fotograma anterior y
    /// en el suyo. Valor conocido del instrumento: antes del arreglo, las semillas 18, 31 y 68 de las 100 primeras (motor sin
    /// arranque) daban uno cada una.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void NoContactIsResolvedDuringARestartCountdown(int accelTicks)
    {
        var catalog = AccelerationTests.WithAccel(Catalog, accelTicks);
        int tackles = 0;
        var during = new List<string>();
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Tackle || e.Detail == "attempted")
                {
                    continue;
                }

                tackles++;
                int f = trace.FrameOfTick(e.Tick);
                if (f > 0 && trace.RestartAt(f - 1) != RestartKind.None && trace.RestartAt(f) != RestartKind.None)
                {
                    during.Add($"{seed}:{e.Tick}:{e.Detail}");
                }
            }
        }

        Assert.True(tackles > 1000, $"muestra escasa: {tackles}");
        Assert.True(during.Count == 0, "contacto resuelto en la cuenta atrás de una reanudación: " + string.Join(", ", during));
    }
}
