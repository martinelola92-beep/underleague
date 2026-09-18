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
    public void NoCatalogPerkUsesAWithTagTarget()
    {
        // pack_mentality (modifyAttribute(strength), target=withTag:Brute) era el ÚNICO perk real que
        // ejercitaba el soporte estructural de Population para EffectTarget.WithTag, y se borró del
        // catálogo (revisor, 18 sep 2026). La ruta del resolutor (EffectPopulationResolver.Resolve para
        // WithTag) sigue existiendo y sin consumidores reales, igual que la de maestros (BB-R) o la de
        // atribución multi-efecto (PerkAuditTests.NoCatalogPerkMixesEffectCategories, el modelo exacto de
        // este test).
        //
        // Lo que se fija aquí es el invariante que queda: hoy NINGÚN perk del catálogo usa
        // target=withTag:. Si aparece uno, este test lo dice y hay que volver a comprobar que resuelve a
        // más de un jugador en al menos una plantilla real (lo que hacía la versión anterior de este
        // test), no que pase en silencio con una población colapsada al portador.
        var withTagPerks = Catalog.Perks.All
            .Where(p => p.Effects.Any(e => e.Target == EffectTarget.WithTag))
            .Select(p => p.Id)
            .ToList();

        Assert.Empty(withTagPerks);
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
