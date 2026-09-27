using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// La traza graba la energía de cada jugador (ADR 0142) para que la ficha del partido pinte la barra de
/// fatiga. Es presentación: grabarla no cambia el partido.
/// </summary>
public sealed class TraceEnergyTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public TraceEnergyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EnergyStartsFullAndDropsWithTheEffort()
    {
        var trace = Simulator.Run(TestMatches.Reference(Catalog, 3), 3, Catalog, SimConfig.Default with { Trace = true }).Trace!;
        int lowest = 100;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            Assert.Equal(100, trace.EnergyPercentAt(0, i));
            for (int f = 0; f < trace.FrameCount; f++)
            {
                int energy = trace.EnergyPercentAt(f, i);
                Assert.InRange(energy, 0, 100);
                lowest = Math.Min(lowest, energy);
            }
        }

        _output.WriteLine($"energía más baja del partido: {lowest} %");
        Assert.True(lowest < 90, $"nadie bajó del 90 % de energía en todo el partido ({lowest} %): la traza no la está grabando");
    }
}
