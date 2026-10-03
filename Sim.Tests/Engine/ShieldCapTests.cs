using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Tests.Analysis.Detectors;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BO-A (barrido de detectores del 3 oct: 0,024 → 0,047 tramos de más de 3 s por partido tras la ADR 0184). Todos los
/// tramos tenían entre 40 y 49 ticks de protección, por encima del tope de la ADR 0153 (<c>shieldMaxTicks</c> = 36,
/// «tres compromisos de 12»): el tope se miraba al elegir y el último compromiso de 12 se cumplía entero. Ahora el
/// compromiso es lo que queda del tope.
/// </summary>
public sealed class ShieldCapTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Valor conocido: con 0, 30 y 35 ticks ya protegidos el compromiso es 12, 6 y 1. Control sin tope: 12.</summary>
    [Fact]
    public void TheLastShieldCommitmentIsWhatIsLeftOfTheCap()
    {
        Assert.Equal(36, Catalog.Ai.Context.ShieldMaxTicks);
        Assert.Equal(12, Catalog.Tuning.States.ShieldingTicks);
        Assert.Equal(12, CommitAfter(Catalog, 0));
        Assert.Equal(6, CommitAfter(Catalog, 30));
        Assert.Equal(1, CommitAfter(Catalog, 35));

        var noCap = Catalog with { Ai = Catalog.Ai.WithContext(Catalog.Ai.Context with { ShieldMaxTicks = 0 }) };
        Assert.Equal(12, CommitAfter(noCap, 30));
    }

    private static int CommitAfter(Catalog catalog, int shielded)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        int carrier = engine.OutfieldIndexForTest(0, 4);
        engine.PlayerAtForTest(carrier).ShieldedTicks = shielded;
        return engine.ShieldCommitTicksForTest(carrier);
    }

    /// <summary>
    /// El caso real, fijado por semilla: <c>ref:100@1270</c>, 53 ticks con el mismo portador y el mismo rival pegado, 48
    /// de ellos protegiendo (tope 36). Ahora ningún portador protege más del tope en una posesión y la semilla no tiene
    /// ningún tramo de más de 3 s. (El peor del barrido, <c>ref:29@1081</c>, no sirve: protege 36-39 ticks y el resto es
    /// conducción con el defensa encima; es el residuo que el tope deja por diseño.)
    /// </summary>
    [Fact]
    public void TheRealCaseNoLongerShieldsPastTheCap()
    {
        var (setup, seed, config) = WorstCaseProbeTests.Build("ref", 100, Catalog);
        var t = DetectorTrace.From(Simulator.Run(setup, seed, Catalog, config));
        // El tope admite la cadencia de decisión: al acabar un compromiso el portador sigue en Shielding hasta su turno
        // de decidir (decisionIntervalTicks = 2). Sin el arreglo esta semilla da 48.
        int max = MaxShieldPerPossession(t);
        Assert.True(max <= Catalog.Ai.Context.ShieldMaxTicks + Catalog.Tuning.DecisionIntervalTicks, $"protección de {max} ticks en una posesión");
        Assert.Empty(SymptomDetectors.CarrierStuck(t));
    }

    internal static int MaxShieldPerPossession(DetectorTrace t)
    {
        int max = 0;
        int owner = -1;
        int count = 0;
        for (int f = 0; f < t.Frames; f++)
        {
            if (t.Owner[f] != owner)
            {
                owner = t.Owner[f];
                count = 0;
            }

            if (owner >= 0 && t.State[t.Slot(f, owner)] == PlayerState.Shielding)
            {
                count++;
                max = Math.Max(max, count);
            }
        }

        return max;
    }
}
