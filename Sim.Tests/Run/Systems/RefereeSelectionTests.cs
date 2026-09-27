using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0158 §1: el plantel de 6-8 árbitros de la run sale de <c>data/referees/</c>, no de neutros
/// genéricos (D-22), con un flujo de RNG propio (<c>RngStreams.Referees</c>, RT-022).
/// </summary>
public sealed class RefereeSelectionTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    [Fact]
    public void SameSeedAndCountAlwaysPicksTheSamePlantel()
    {
        var first = SystemsTestSupport.Systems.CreateReferees(12345UL, 7, Catalog);
        var second = SystemsTestSupport.Systems.CreateReferees(12345UL, 7, Catalog);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void PicksExactlyTheRequestedCountWithoutRepeatingAnyone(int count)
    {
        var referees = SystemsTestSupport.Systems.CreateReferees(999UL, count, Catalog);

        Assert.Equal(count, referees.Count);
        Assert.Equal(count, referees.Select(r => r.DefinitionId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(referees, r => Assert.False(string.IsNullOrEmpty(r.DefinitionId)));

        // Cada uno arranca sin memoria (RF-061b): la primera vez que se elige un plantel no hay partido
        // previo del que acordarse.
        Assert.All(referees, r => Assert.Equal(0, r.Memory));
    }

    /// <summary>El lado ciego de datos viaja con el árbitro elegido, no se pierde en la selección.</summary>
    [Fact]
    public void OneEyedRefereesKeepTheirBlindSideFromTheCatalog()
    {
        var referees = SystemsTestSupport.Systems.CreateReferees(42UL, 8, Catalog);
        foreach (var referee in referees)
        {
            var definition = SystemsTestSupport.Systems.Referees.Find(referee.DefinitionId);
            Assert.NotNull(definition);
            Assert.Equal(definition!.BlindSide, referee.BlindSide);
            Assert.Equal(referee.Trait == RefereeTrait.OneEyed, referee.BlindSide != RefereeSide.None);
        }
    }

    [Fact]
    public void DifferentSeedsCanPickADifferentPlantel()
    {
        bool anyDiffers = false;
        for (ulong seed = 1; seed <= 20 && !anyDiffers; seed++)
        {
            var a = SystemsTestSupport.Systems.CreateReferees(seed, 6, Catalog)
                .Select(r => r.DefinitionId).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var b = SystemsTestSupport.Systems.CreateReferees(seed + 1000, 6, Catalog)
                .Select(r => r.DefinitionId).OrderBy(x => x, StringComparer.Ordinal).ToList();
            anyDiffers = !a.SequenceEqual(b, StringComparer.Ordinal);
        }

        Assert.True(anyDiffers, "20 pares de semillas y ningún plantel de árbitros cambió");
    }

    [Fact]
    public void AskingForMoreRefereesThanTheCatalogHasThrowsAnExplicitError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SystemsTestSupport.Systems.CreateReferees(1UL, 99, Catalog));
    }

    /// <summary>
    /// <c>DefaultRunSystems</c> (paquete W) se queda con neutros y nombre <c>referee_i</c>: lo usan tests
    /// antiguos que no cargan <c>data/referees/</c>, y cambiar su forma habría sido tocar más de lo que
    /// pide esta ADR.
    /// </summary>
    [Fact]
    public void DefaultRunSystemsStillCreatesNeutralPlaceholders()
    {
        var referees = DefaultRunSystems.Instance.CreateReferees(1UL, 6, Catalog);

        Assert.Equal(6, referees.Count);
        Assert.All(referees, r => Assert.Equal(RefereeTrait.Neutral, r.Trait));
        Assert.All(referees, r => Assert.Equal(string.Empty, r.DefinitionId));
    }
}
