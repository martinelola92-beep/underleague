using Underleague.Balance;
using Underleague.Sim.Data;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Los medidores de valor (ADR 0087) generan el sujeto de la plantilla r con la semilla índice*1000 + r y
/// su espejo con índice*1000 + 500 + r. Por encima de 500 plantillas el sujeto r reutilizaría el equipo del
/// espejo r-500 y la muestra dejaría de ser independiente sin avisar: BL-A midió con 768 y la revisión
/// independiente lo encontró. El instrumento se niega en vez de mentir.
/// </summary>
public sealed class ValueRunnerSeedSpaceTests
{
    [Fact]
    public void PerkValueRunnerRejectsMoreRostersThanItsSeedSpaceHolds()
    {
        var catalog = TestData.LoadCatalog();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PerkValueRunner.Run(catalog, 5, PerkValueRunner.MaxRosters + 1, 1));
    }

    [Fact]
    public void BothValueRunnersShareTheSameSeedSpaceLimit() =>
        Assert.Equal(PerkValueRunner.MaxRosters, ItemValueRunner.MaxRosters);
}
