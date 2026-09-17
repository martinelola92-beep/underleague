using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Verifica el clasificador de §13.4/§3.2 (docs/analisis/protocolo-balanceo-automatizado.md) contra
/// perks reales del catálogo — sin simular ningún partido, solo consulta de datos ya cargados. Es la
/// prueba determinista de la pieza "selección automática de métricas" (§12/tooling).
/// </summary>
public sealed class PerkBalanceClassifierTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static PerkDefinition Find(string id) =>
        Catalog.Perks.All.Single(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    [Fact]
    public void ProbabilityBonusOnTackleIsReady()
    {
        // own_third_anchor: modifyProbability tackle +100%, condicionado a startsIn(owner,'OwnThird').
        var result = PerkBalanceClassifier.Classify(Find("own_third_anchor"));

        Assert.Equal(PerkBalanceCategory.ProbabilityBonus, result.Category);
        Assert.Equal(MetricReadiness.Ready, result.Readiness);
        Assert.Equal(MatchMetrics.TacklesPerMatch, result.PrimaryMetric);
        Assert.True(result.HasNumericParameter);
        Assert.False(result.NeedsMultiTargetHarness);
    }

    [Fact]
    public void TraitScalarWithRealPrecedentIsReady()
    {
        // iron_price: modifyTraitScalar injuryChanceBonus +10, disparado por INJURY.
        var result = PerkBalanceClassifier.Classify(Find("iron_price"));

        Assert.Equal(PerkBalanceCategory.TraitScalar, result.Category);
        Assert.Equal(MetricReadiness.Ready, result.Readiness);
        Assert.Equal(MatchMetrics.InjuriesPerMatch, result.PrimaryMetric);
    }

    [Fact]
    public void AccumulatedStateEffectIsDistinguishedFromPureRunLevel()
    {
        // iron_lungs: addCounter + modifyAttribute(stamina) escalado por el contador,
        // AccumulatesAcrossMatches=true — necesita el harness de campaña, no es economía pura (§16).
        var result = PerkBalanceClassifier.Classify(Find("iron_lungs"));

        Assert.Equal(PerkBalanceCategory.AccumulatedStateBonus, result.Category);
        Assert.Equal(MetricReadiness.NeedsCampaignHarness, result.Readiness);
        Assert.True(result.HasNumericParameter);
    }

    [Fact]
    public void StandaloneAddCounterIsRunLevel()
    {
        // loan: addCounter en solitario, sin modifyProbability que lo acompañe.
        var result = PerkBalanceClassifier.Classify(Find("loan"));

        Assert.Equal(PerkBalanceCategory.RunLevelCounter, result.Category);
    }

    [Fact]
    public void RefereeBiasAggregateExistsButStillHasNoBand()
    {
        // diver / home_ref: modifyBias. RefereeBiasMetrics.MeanBiasFavoringCarrier YA construye el
        // agregado (18 sep 2026, punto 2 del encargo) — "la métrica existe" ya no es el problema; sigue
        // sin banda ni ADR que diga qué sesgo es aceptable, así que sigue en DESIGN_REVIEW, no Ready.
        var result = PerkBalanceClassifier.Classify(Find("diver"));

        Assert.Equal(PerkBalanceCategory.RefereeBias, result.Category);
        Assert.Equal(MetricReadiness.NotReadyNoBand, result.Readiness);
    }

    [Fact]
    public void GeometryEffectIsReady()
    {
        // deep_run: shiftHome + modifyZoneShape, positionOnly Forward.
        var result = PerkBalanceClassifier.Classify(Find("deep_run"));

        Assert.Equal(PerkBalanceCategory.Geometry, result.Category);
        Assert.Equal(MetricReadiness.Ready, result.Readiness);
        Assert.Equal(MatchMetrics.BallThirdMaxShare, result.PrimaryMetric);
    }

    [Fact]
    public void TargetSelectionEffectHasNoNumericParameter()
    {
        // blood_scent: modifyTackleBias, selección de objetivo sin escala continua.
        var result = PerkBalanceClassifier.Classify(Find("blood_scent"));

        Assert.Equal(PerkBalanceCategory.TargetSelection, result.Category);
        Assert.False(result.HasNumericParameter);
        Assert.Equal(MetricReadiness.ReadyViaBehavioralAudit, result.Readiness);
    }

    [Fact]
    public void MultiTargetEffectIsFlagged()
    {
        // pack_mentality: modifyAttribute(strength), target=withTag:Brute — afecta a varios jugadores.
        var result = PerkBalanceClassifier.Classify(Find("pack_mentality"));

        Assert.Equal(PerkBalanceCategory.Attribute, result.Category);
        Assert.True(result.NeedsMultiTargetHarness);
    }

    [Fact]
    public void SingleOwnerAttributeEffectDoesNotNeedMultiTargetHarness()
    {
        // brute_boots: modifyAttribute(strength), target=owner. Strength en sí es AmbiguousPrimaryMetric
        // (§16: ningún perk real de Strength da una señal estructural para elegir la métrica) — lo que
        // esta prueba comprueba es que el TARGET (owner) no dispara la bandera multi-objetivo.
        var result = PerkBalanceClassifier.Classify(Find("brute_boots"));

        Assert.Equal(PerkBalanceCategory.Attribute, result.Category);
        Assert.False(result.NeedsMultiTargetHarness);
        Assert.Equal(MetricReadiness.AmbiguousPrimaryMetric, result.Readiness);
    }

    [Fact]
    public void ModifyUtilityIsReadyViaBehavioralAuditEvenWithoutARealPerkYet()
    {
        // C1 (docs/analisis/c1-piloto-cazagoles-diseno.md): ningún perk real lo usa todavía, pero el
        // clasificador debe reconocer el mecanismo si se le pasa un PerkDefinition construido en memoria
        // (el mismo patrón de test usado para medir los candidatos de Cazagoles).
        var perk = new PerkDefinition(
            Id: "utility_bonus_test_fixture",
            Name: new LocalizedName("Fixture", "Fixture"),
            Rarity: Rarity.Uncommon,
            Kind: PerkKind.Conditional,
            Axis: PerkAxis.Geometry,
            Race: null,
            Links: Array.Empty<LinkRelation>(),
            Trigger: Underleague.Sim.Events.EventType.MatchStart,
            Scope: PerkScope.Any,
            Condition: "",
            CompiledCondition: CompiledCondition.AlwaysTrue,
            Effects: new[]
            {
                new EffectDefinition(
                    Type: EffectType.ModifyUtility,
                    Value: 24,
                    Duration: EffectDuration.Match,
                    UtilityAction: Underleague.Sim.Engine.PlayerAction.Shoot,
                    UtilityZone: Zone.Opposing),
            },
            ElseEffects: Array.Empty<EffectDefinition>(),
            Limit: null,
            AccumulatesAcrossMatches: false,
            Lethal: false,
            LethalChance: 0,
            PositionOnly: Position.Forward,
            TagsRequired: Array.Empty<string>(),
            TagsForbidden: Array.Empty<string>(),
            MinAct: 1,
            Frequency: 100,
            Family: "",
            Requires: null,
            Blocks: PerkBlock.None);

        var result = PerkBalanceClassifier.Classify(perk);

        Assert.Equal(PerkBalanceCategory.UtilityBonus, result.Category);
        Assert.Equal(MetricReadiness.ReadyViaBehavioralAudit, result.Readiness);
        Assert.True(result.HasNumericParameter);
    }

    /// <summary>
    /// Cobertura: los 94 perks reales del catálogo clasifican sin lanzar excepción — ninguno cae fuera
    /// de la matriz de §3.1 (aunque su Readiness pueda ser NotReady*).
    /// </summary>
    [Fact]
    public void EveryRealPerkInTheCatalogClassifiesWithoutThrowing()
    {
        foreach (var perk in Catalog.Perks.All)
        {
            var result = PerkBalanceClassifier.Classify(perk);
            Assert.True(Enum.IsDefined(result.Category));
            Assert.True(Enum.IsDefined(result.Readiness));
        }
    }
}
