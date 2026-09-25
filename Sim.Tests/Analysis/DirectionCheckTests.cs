using Underleague.Sim.Analysis;
using Underleague.Sim.Data;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// La precondición de §18.2 corregida (§29). Puro: ni un partido simulado. Cubre los cuatro casos
/// exigidos — escalar compatible que sigue comprobándose, geometría que deja de comprobarse, la otra
/// operación geométrica del catálogo, y una contradicción real que debe seguir escalando.
/// </summary>
public sealed class DirectionCheckTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    // --- 1. Efecto escalar compatible: la comprobación sigue funcionando exactamente como antes --------

    [Theory]
    [InlineData(PerkBalanceCategory.ProbabilityBonus, 100, 0.15, DirectionVerdict.Consistent)]
    [InlineData(PerkBalanceCategory.ProbabilityBonus, 100, -0.15, DirectionVerdict.Contradiction)]
    [InlineData(PerkBalanceCategory.ProbabilityBonus, -50, -0.15, DirectionVerdict.Consistent)]
    [InlineData(PerkBalanceCategory.ProbabilityBonus, -50, 0.15, DirectionVerdict.Contradiction)]
    [InlineData(PerkBalanceCategory.TraitScalar, 10, 0.2, DirectionVerdict.Consistent)]
    [InlineData(PerkBalanceCategory.TraitScalar, 10, -0.2, DirectionVerdict.Contradiction)]
    public void CompatibleCategoriesKeepTheOriginalBehaviour(
        PerkBalanceCategory category, int effectValue, double delta, DirectionVerdict expected)
    {
        Assert.True(DirectionCheck.AppliesTo(category));
        Assert.Equal(expected, DirectionCheck.Evaluate(category, effectValue, delta));
    }

    // --- 2 y 3. Geometría: NO se aplica, ni con delta negativo ni con ninguno -------------------------

    [Theory]
    [InlineData(2, -2.2095)]  // high_line real: shiftHome(+2), delta medido en §23.5
    [InlineData(2, 2.2095)]
    [InlineData(-3, -1.0)]
    public void GeometryNeverGetsTheDirectionCheck(int effectValue, double delta)
    {
        Assert.False(DirectionCheck.AppliesTo(PerkBalanceCategory.Geometry));
        Assert.Equal(DirectionVerdict.NotApplicable, DirectionCheck.Evaluate(PerkBalanceCategory.Geometry, effectValue, delta));
    }

    [Fact]
    public void EveryRealGeometryPerkInTheCatalogIsExemptNotJustHighLine()
    {
        // La regla es por categoría, no por perk: los seis de geometría del catálogo quedan exentos por el
        // mismo motivo, y high_line no recibe ningún trato especial.
        var geometryPerks = Catalog.Perks.All
            .Where(p => p.Effects.Count > 0)
            .Where(p => PerkBalanceClassifier.ClassifyEffectTypeCategory(PerkBalanceClassifier.GetPrimaryEffect(p).Type) == PerkBalanceCategory.Geometry)
            .Select(p => p.Id)
            .ToList();

        Assert.Contains("high_line", geometryPerks);
        Assert.True(geometryPerks.Count > 1, "debería haber más perks de geometría que high_line");
        foreach (var id in geometryPerks)
        {
            var perk = Catalog.Perks.All.Single(p => p.Id == id);
            var category = PerkBalanceClassifier.ClassifyEffectTypeCategory(PerkBalanceClassifier.GetPrimaryEffect(perk).Type);
            Assert.Equal(DirectionVerdict.NotApplicable, DirectionCheck.Evaluate(category, PerkBalanceClassifier.GetPrimaryEffect(perk).Value, -1.0));
        }
    }

    // --- 4. Una contradicción REAL debe seguir detectándose -------------------------------------------

    [Fact]
    public void ARealSignContradictionInACompatibleCategoryStillEscalates()
    {
        // last_ditch es ProbabilityBonus con Value positivo sobre tacklesPerMatch. Si midiéramos un
        // delta negativo, eso SÍ es una contradicción con significado y debe seguir escalando.
        //
        // El ejemplo cambia de perk, no de expectativa: `own_third_anchor` dejó de ser un bono de
        // probabilidad el 25 sep 2026 (pasó a derribar), y `last_ditch` es la misma forma.
        var perk = Catalog.Perks.All.Single(p => p.Id == "last_ditch");
        var effect = PerkBalanceClassifier.GetPrimaryEffect(perk);
        var category = PerkBalanceClassifier.ClassifyEffectTypeCategory(effect.Type);

        Assert.Equal(PerkBalanceCategory.ProbabilityBonus, category);
        Assert.True(effect.Value > 0);
        Assert.Equal(DirectionVerdict.Contradiction, DirectionCheck.Evaluate(category, effect.Value, -0.9));
        Assert.Equal(DirectionVerdict.Consistent, DirectionCheck.Evaluate(category, effect.Value, 0.9));
    }

    // --- Cobertura: ninguna categoría fuera de la lista blanca recibe la comprobación ------------------

    [Fact]
    public void WhitelistFailsSafeForEveryOtherCategory()
    {
        foreach (var category in Enum.GetValues<PerkBalanceCategory>())
        {
            bool expected = category is PerkBalanceCategory.ProbabilityBonus or PerkBalanceCategory.TraitScalar;
            Assert.Equal(expected, DirectionCheck.AppliesTo(category));
        }
    }
}
