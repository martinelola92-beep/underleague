using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Demuestra el punto 3 del encargo del 18 sep 2026: soporte ESTRUCTURAL (por forma de destinatario, no
/// por perk) para <c>Population</c>, con <c>pack_mentality</c> como caso de prueba real, y comprueba
/// explícitamente que no hay atribución falsa ("effect on N actors ≈ effect on owner").
/// </summary>
public sealed class EffectPopulationResolverTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public EffectPopulationResolverTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PackMentalityResolvesToMoreThanTheOwnerOnAtLeastOneRealRoster()
    {
        // pack_mentality: modifyAttribute(strength), target=withTag:Brute — y su propia condición
        // ("teammatesWithTag(owner,'Brute') > 2") ya dice que, cuando se activa, el portador tiene AL
        // MENOS 3 compañeros con la etiqueta: la población nunca debería colapsar a "solo el portador".
        var perk = Catalog.Perks.All.Single(p => p.Id == "pack_mentality");
        var effect = perk.Effects.Single();
        Assert.Equal(EffectTarget.WithTag, effect.Target);
        Assert.Equal("Brute", effect.TargetTag);

        bool foundPopulationLargerThanOwner = false;
        var populationSizes = new List<int>();

        for (int roster = 0; roster < 30; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

            int ownerIndex = 0; // cualquier titular sirve de portador nominal para esta comprobación
            var result = EffectPopulationResolver.Resolve(effect.Target, effect.TargetTag, home, away, ownerIndex);

            Assert.Equal(PopulationResolution.Resolved, result.Status);
            populationSizes.Add(result.AffectedCount);

            if (result.AffectedCount > 1)
            {
                foundPopulationLargerThanOwner = true;
            }
        }

        _output.WriteLine($"Tamaños de población resueltos en 30 plantillas: {string.Join(",", populationSizes)}");
        Assert.True(
            foundPopulationLargerThanOwner,
            "en 30 plantillas generadas, pack_mentality nunca resolvió a más de un jugador — la resolución de WithTag no está leyendo las etiquetas reales");
    }

    [Fact]
    public void ResolvedPopulationIsNotSilentlyCollapsedToTheOwner()
    {
        // Comprobación negativa explícita contra "effect on N actors ≈ effect on owner": si el efecto
        // afecta a un jugador CON la etiqueta que NO es el portador, ese índice debe aparecer en la
        // población resuelta, distinto del índice del portador.
        var homeRng = RngStreams.Generation(7, 0);
        var awayRng = RngStreams.Generation(7, 10_000);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        int bruteCount = home.Players.Count(p => p.HasTag("Brute"));
        _output.WriteLine($"jugadores con etiqueta Brute en esta plantilla: {bruteCount}");

        var result = EffectPopulationResolver.Resolve(EffectTarget.WithTag, "Brute", home, away, ownerIndex: 0);

        Assert.Equal(bruteCount, result.AffectedCount);
        for (int i = 0; i < home.Players.Count; i++)
        {
            bool hasTag = home.Players[i].HasTag("Brute");
            Assert.Equal(hasTag, result.AffectedPlayerIndexes.Contains(i));
        }
    }

    [Theory]
    [InlineData(EffectTarget.Team)]
    [InlineData(EffectTarget.OpposingTeam)]
    [InlineData(EffectTarget.WithTag)]
    public void StructurallyResolvableTargetShapesReturnResolved(EffectTarget target)
    {
        var homeRng = RngStreams.Generation(3, 0);
        var awayRng = RngStreams.Generation(3, 10_000);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        var result = EffectPopulationResolver.Resolve(target, "Brute", home, away, ownerIndex: 0);
        Assert.Equal(PopulationResolution.Resolved, result.Status);
    }

    [Theory]
    [InlineData(EffectTarget.Adjacent)]
    [InlineData(EffectTarget.AdjacentWithTag)]
    [InlineData(EffectTarget.AdjacentOpponents)]
    [InlineData(EffectTarget.Target)]
    [InlineData(EffectTarget.Opponent)]
    [InlineData(EffectTarget.Linked)]
    [InlineData(EffectTarget.LinkedWithTag)]
    public void DynamicOrLinkDependentTargetShapesAreExplicitlyBlockedNotGuessed(EffectTarget target)
    {
        var homeRng = RngStreams.Generation(3, 0);
        var awayRng = RngStreams.Generation(3, 10_000);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        var result = EffectPopulationResolver.Resolve(target, "", home, away, ownerIndex: 0);

        Assert.NotEqual(PopulationResolution.Resolved, result.Status);
        Assert.Empty(result.AffectedPlayerIndexes);
        Assert.False(string.IsNullOrWhiteSpace(result.Note));
    }

    [Fact]
    public void OwnerAndActorResolveToExactlyOnePlayer()
    {
        var homeRng = RngStreams.Generation(3, 0);
        var awayRng = RngStreams.Generation(3, 10_000);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        foreach (var target in new[] { EffectTarget.Owner, EffectTarget.Actor })
        {
            var result = EffectPopulationResolver.Resolve(target, "", home, away, ownerIndex: 2);
            Assert.Equal(PopulationResolution.Resolved, result.Status);
            Assert.Equal(new[] { 2 }, result.AffectedPlayerIndexes);
        }
    }
}
