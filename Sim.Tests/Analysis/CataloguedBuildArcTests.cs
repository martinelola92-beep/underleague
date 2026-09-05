using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Las builds catalogadas de <c>data/balance/builds/</c> tienen que ser builds <b>legales</b> bajo la ADR
/// 0051: una build que lleva un maestro sin su línea, o que mezcla un maestro con la línea que ese
/// maestro cierra, mide algo que ninguna run puede construir, y entonces la curva de puertas de la ADR
/// 0033 deja de decir lo que dice.
/// </summary>
public sealed class CataloguedBuildArcTests
{
    [Fact]
    public void EveryCataloguedBuildIsReachableInARun()
    {
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);

        foreach (var (id, build) in builds.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            var ids = build.Perks.Select(p => p.Perk).Distinct(StringComparer.Ordinal).ToList();
            var perks = ids.Select(catalog.Perks.Get).ToList();

            foreach (var master in perks.Where(p => p.IsMaster))
            {
                int held = perks.Count(p =>
                    !p.IsMaster && string.Equals(p.Family, master.Requires!.Family, StringComparison.Ordinal));
                Assert.True(
                    held >= master.Requires!.Count,
                    $"'{id}' lleva el maestro '{master.Id}', que exige {master.Requires.Count} perks de "
                        + $"'{master.Requires.Family}', y solo lleva {held}");

                foreach (var other in perks)
                {
                    Assert.False(
                        other.HasFamily && master.Blocks.Families.Contains(other.Family, StringComparer.Ordinal),
                        $"'{id}' mezcla el maestro '{master.Id}' con '{other.Id}', de la línea '{other.Family}' que ese maestro cierra");

                    Assert.False(
                        master.Blocks.Perks.Contains(other.Id, StringComparer.Ordinal),
                        $"'{id}' mezcla el maestro '{master.Id}' con '{other.Id}', al que ese maestro cierra");
                }
            }
        }
    }

    /// <summary>
    /// Los dos ejemplares de la ADR 0051 existen y son de verdad excluyentes: misma raza, maestros
    /// opuestos, y ni un solo perk en común de las líneas que cierran. Es la demostración de RF-032 —tres
    /// builds viables y <b>distintas</b> por raza— que hasta ahora solo se cumplía por qué perks tocaban.
    /// </summary>
    [Fact]
    public void TheTwoMasterBuildsOfTheSameRaceExcludeEachOther()
    {
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var granite = builds["human_granite"];
        var blood = builds["human_bloodrange"];

        Assert.Equal(granite.Race, blood.Race);

        var left = granite.Perks.Select(p => catalog.Perks.Get(p.Perk)).ToList();
        var right = blood.Perks.Select(p => catalog.Perks.Get(p.Perk)).ToList();
        Assert.Contains(left, p => p.IsMaster);
        Assert.Contains(right, p => p.IsMaster);

        // Cada maestro de una cierra una línea de la que la otra vive.
        foreach (var master in left.Where(p => p.IsMaster))
        {
            Assert.Contains(right, p => master.Blocks.Families.Contains(p.Family, StringComparer.Ordinal));
        }

        foreach (var master in right.Where(p => p.IsMaster))
        {
            Assert.Contains(left, p => master.Blocks.Families.Contains(p.Family, StringComparer.Ordinal));
        }

        // Y no comparten ni un perk: no es que se parezcan poco, es que no pueden coexistir.
        var shared = left.Select(p => p.Id).Intersect(right.Select(p => p.Id), StringComparer.Ordinal).ToList();
        Assert.True(shared.Count == 0, "las dos builds comparten " + string.Join(", ", shared));
    }
}
