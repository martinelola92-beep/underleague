using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Estados del ciclo de vida de un perk en el protocolo de balanceo automatizado
/// (docs/analisis/protocolo-balanceo-automatizado.md §9, revisado 17-18 sep 2026).
/// </summary>
public enum BalanceState
{
    NotReady,
    BlockedInfra,
    RunLevel,
    Screening,
    Tuning,
    Validating,
    Balanced,
    InsufficientExposure,
    InsufficientEvidence,
    SafetyLimit,
    DesignReview,
    WeakEffectCeiling,
    NeedsReplication,
    Reject,
    SystemicRegression,
}

/// <summary>
/// Categoría de balanceo de un perk (§3): agrupa por *qué hay que medir*, no por <see cref="EffectType"/>
/// literal — varios tipos de efecto caen en la misma categoría (p. ej. <see cref="EffectType.ModifyLeash"/>
/// y <see cref="EffectType.ShiftHome"/> son los dos "Geometry").
/// </summary>
public enum PerkBalanceCategory
{
    UtilityBonus,
    ProbabilityBonus,
    TraitScalar,
    Attribute,
    Geometry,
    TargetSelection,
    RefereeBias,
    BinaryEvent,
    RunLevelCounter,
    Singular,
}

/// <summary>
/// Cuánta confianza merece la métrica primaria asignada a un perk (§13.4): distingue un mecanismo con
/// banda RT-056 confirmada de uno sin precedente real o sin banda alguna, para que el motor de decisión
/// (§6) nunca trate una inferencia como si fuera un dato medido.
/// </summary>
public enum MetricReadiness
{
    /// <summary>Banda RT-056 existente y mecanismo con precedente real verificado.</summary>
    Ready,

    /// <summary>Sin banda agregada, pero medible con el histograma de acción restringido a exposición (C1/Tanda 0).</summary>
    ReadyViaBehavioralAudit,

    /// <summary>Mecanismo y métrica existen; sin ningún perk real que confirme que es la sensible.</summary>
    Inferred,

    /// <summary>La métrica natural existe mas es INFO (sin banda) — hace falta DESIGN_REVIEW para fijarla.</summary>
    NotReadyNoBand,

    /// <summary>No existe ninguna métrica, ni bandeada ni INFO, para este efecto — hueco de tooling.</summary>
    NotReadyNoMetric,

    /// <summary>El dato existe por partido pero no hay fila agregada en MatchMetrics — tooling puro, sin ambigüedad de diseño.</summary>
    NotReadyMissingAggregate,
}

/// <summary>Resultado de clasificar un perk: su categoría, qué tan lista está su métrica y cuál es.</summary>
public readonly record struct PerkClassification(
    PerkBalanceCategory Category,
    MetricReadiness Readiness,
    string PrimaryMetric,
    bool HasNumericParameter,
    bool NeedsMultiTargetHarness,
    string Note);

/// <summary>
/// Clasificador determinista de perks para el protocolo de balanceo (§3/§13.4 de
/// docs/analisis/protocolo-balanceo-automatizado.md). Pura consulta de datos ya existentes en el
/// <see cref="PerkDefinition"/> — cero simulación, cero llamada a un modelo. Es la parte "confirmar la
/// métrica antes de Screening" que el documento pedía convertir en código (§13, punto 4).
/// </summary>
public static class PerkBalanceClassifier
{
    /// <summary>
    /// Escalares de <see cref="TraitScalarKind"/> con banda RT-056 confirmada y mecanismo verificado
    /// (§13.4.2): <c>injuryChanceBonus</c>/<c>hardTackleBonus</c>/<c>shootRangeBonusCells</c> ya tienen
    /// perk real; <c>injuryResistanceBonus</c> y <c>leashBonus</c> reutilizan la banda de su categoría
    /// (injuriesPerMatch/ballThirdMaxShare) aunque hoy ningún perk los use.
    /// </summary>
    private static readonly HashSet<TraitScalarKind> ReadyScalars = new()
    {
        TraitScalarKind.InjuryChanceBonus,
        TraitScalarKind.HardTackleBonus,
        TraitScalarKind.ShootRangeBonusCells,
        TraitScalarKind.InjuryResistanceBonus,
        TraitScalarKind.LeashBonus,
    };

    private static readonly HashSet<TraitScalarKind> NoBandScalars = new()
    {
        TraitScalarKind.ShotQualityBonus,
        TraitScalarKind.PassQualityBonus,
        TraitScalarKind.FoulChanceBonus,
        TraitScalarKind.SaveBonusClose,
        TraitScalarKind.SaveBonusFar,
    };

    /// <summary>
    /// <c>ProbabilityKind</c> cuya métrica natural es <c>INFO</c> (sin banda) en <see cref="MatchMetrics"/>:
    /// <c>Foul</c>/<c>Card</c> (§13.4.3, foulsPerMatch/tarjetas) y las que afectan al ÉXITO de una
    /// resolución ya decidida, no a su frecuencia — <c>ShotOnTarget</c>→shotsOnTargetShare,
    /// <c>Save</c>→saveRate, <c>Pass</c>/<c>Intercept</c>/<c>InterceptEvasion</c>/<c>Dribble</c>→
    /// passCompletionRate, todas INFO. Solo <c>Tackle</c>/<c>TackleEvasion</c> (tacklesPerMatch) e
    /// <c>Injury</c>/<c>Injure</c>/<c>SevereInjury</c> (injuriesPerMatch) tienen banda real hoy —
    /// encontrado al ejercitar el clasificador con datos simulados reales (Sim.Tests/Balance), no
    /// anticipado en la primera versión de §13.4, que solo marcaba Foul/Card como excepción.
    /// </summary>
    private static readonly HashSet<ProbabilityKind> NoBandProbabilities = new()
    {
        ProbabilityKind.Foul,
        ProbabilityKind.Card,
        ProbabilityKind.ShotOnTarget,
        ProbabilityKind.Save,
        ProbabilityKind.Pass,
        ProbabilityKind.Intercept,
        ProbabilityKind.InterceptEvasion,
        ProbabilityKind.Dribble,
    };

    /// <summary>Clasifica un perk a partir de su efecto principal (el primero de la lista, §3.2 punto 7 para multi-efecto).</summary>
    public static PerkClassification Classify(PerkDefinition perk)
    {
        ArgumentNullException.ThrowIfNull(perk);
        if (perk.Effects.Count == 0)
        {
            return new PerkClassification(
                PerkBalanceCategory.Singular, MetricReadiness.NotReadyNoMetric, "", false, false,
                "perk sin efectos: nada que balancear");
        }

        bool multiTarget = perk.Effects.Any(e => e.Target is not (EffectTarget.Owner or EffectTarget.Actor));
        bool runLevel = perk.AccumulatesAcrossMatches
            || (perk.Effects.Count == 1 && perk.Effects[0].Type == EffectType.AddCounter);

        if (runLevel)
        {
            return new PerkClassification(
                PerkBalanceCategory.RunLevelCounter, MetricReadiness.Ready, "FullRunMetrics", false, multiTarget,
                "AccumulatesAcrossMatches o addCounter en solitario: se mide con /Balance --full-runs, fuera del bucle rápido (§3, RUN_LEVEL)");
        }

        // El efecto "objetivo" es el primero que no sea addCounter (§3: el contador se registra, no compite).
        var primary = perk.Effects.FirstOrDefault(e => e.Type != EffectType.AddCounter) ?? perk.Effects[0];

        var classification = primary.Type switch
        {
            EffectType.ModifyUtility => new PerkClassification(
                PerkBalanceCategory.UtilityBonus, MetricReadiness.ReadyViaBehavioralAudit,
                "histograma de acción restringido a exposición", true, multiTarget,
                "C1: mismo instrumento que Tanda 0/Cazagoles"),

            EffectType.ModifyProbability when NoBandProbabilities.Contains(primary.Probability) => new PerkClassification(
                PerkBalanceCategory.ProbabilityBonus, MetricReadiness.NotReadyNoBand,
                ProbabilityMetricName(primary.Probability), true, multiTarget,
                $"{primary.Probability}: la métrica natural es INFO en MatchMetrics, sin banda — necesita DESIGN_REVIEW para fijar un rango"),

            EffectType.ModifyProbability => new PerkClassification(
                PerkBalanceCategory.ProbabilityBonus, MetricReadiness.Ready,
                ProbabilityMetricName(primary.Probability), true, multiTarget,
                $"tasa de {primary.Probability} armado vs control"),

            EffectType.ModifyTraitScalar when ReadyScalars.Contains(primary.Scalar) => new PerkClassification(
                PerkBalanceCategory.TraitScalar, MetricReadiness.Ready,
                ScalarMetricName(primary.Scalar), true, multiTarget,
                $"escalar {primary.Scalar} con banda ya asociada"),

            EffectType.ModifyTraitScalar when primary.Scalar == TraitScalarKind.AdjacentTeammateBonusPercent => new PerkClassification(
                PerkBalanceCategory.TraitScalar, MetricReadiness.ReadyViaBehavioralAudit,
                "histograma de acción restringido a exposición", true, multiTarget,
                "AdjacentTeammateBonusPercent es el origen de LeaderBonusPercent: mismo instrumento que C1 (§13.4.2)"),

            EffectType.ModifyTraitScalar when NoBandScalars.Contains(primary.Scalar) => new PerkClassification(
                PerkBalanceCategory.TraitScalar, MetricReadiness.NotReadyNoBand,
                ScalarMetricName(primary.Scalar), true, multiTarget,
                $"{primary.Scalar}: métrica natural INFO sin banda — necesita DESIGN_REVIEW (§13.4.2)"),

            EffectType.ModifyTraitScalar when primary.Scalar is TraitScalarKind.FatigueResistancePercent => new PerkClassification(
                PerkBalanceCategory.TraitScalar, MetricReadiness.NotReadyNoMetric,
                "", true, multiTarget,
                "FatigueResistancePercent: no existe ninguna métrica de rendimiento por fase de partido (§13.4.1/§13.4.2)"),

            EffectType.ModifyTraitScalar when primary.Scalar == TraitScalarKind.SpeedBonusPercent => new PerkClassification(
                PerkBalanceCategory.TraitScalar, MetricReadiness.Inferred,
                MatchMetrics.BallThirdMaxShare, true, multiTarget,
                "SpeedBonusPercent: mecanismo y métrica existen, sin perk real que lo confirme (§13.4.1)"),

            EffectType.ModifyAttribute => ClassifyAttribute(primary.Attribute, multiTarget),

            EffectType.ModifyLeash or EffectType.ShiftHome or EffectType.ModifyZoneShape => new PerkClassification(
                PerkBalanceCategory.Geometry, MetricReadiness.Ready, MatchMetrics.BallThirdMaxShare, true, multiTarget,
                "geometría de zona de acción"),

            EffectType.ModifyMarkBias or EffectType.ModifyTackleBias => new PerkClassification(
                PerkBalanceCategory.TargetSelection, MetricReadiness.ReadyViaBehavioralAudit,
                "distribución de a quién se marca/entra (bespoke)", false, multiTarget,
                "selección de objetivo: sin parámetro numérico que buscar, behavioral audit directo (§6.5)"),

            EffectType.ModifyBias => new PerkClassification(
                PerkBalanceCategory.RefereeBias, MetricReadiness.NotReadyMissingAggregate,
                "FinalBias / faltas por equipo", true, multiTarget,
                "FinalBias existe por partido pero MatchMetrics.Compute no lo resume (§13.4.4) — tooling puro"),

            EffectType.Immunity or EffectType.CancelEvent => new PerkClassification(
                PerkBalanceCategory.BinaryEvent, MetricReadiness.Ready, "tasa del suceso cancelado", false, multiTarget,
                "binario: Screening → Validation directa, sin Tuning (§6.1/§6.5)"),

            EffectType.ExtraAction => new PerkClassification(
                PerkBalanceCategory.BinaryEvent, MetricReadiness.Ready, "frecuencia de la acción repetida", false, multiTarget,
                "sin valor numérico propio declarado hoy"),

            _ => new PerkClassification(
                PerkBalanceCategory.Singular, MetricReadiness.Inferred, "universales + métrica del suceso", false, multiTarget,
                $"{primary.Type}: caso singular, sin plantilla genérica todavía (§3)"),
        };

        return classification;
    }

    private static PerkClassification ClassifyAttribute(AttributeKind attribute, bool multiTarget) => attribute switch
    {
        AttributeKind.Strength => new PerkClassification(
            PerkBalanceCategory.Attribute, MetricReadiness.Ready,
            "tacklesPerMatch/injuriesPerMatch (según Family/PositionOnly del propio perk)", true, multiTarget,
            "Strength toca cinco resoluciones a la vez; la primaria la decide el dato del perk, no una medición (§13.4.1)"),
        AttributeKind.Stamina => new PerkClassification(
            PerkBalanceCategory.Attribute, MetricReadiness.NotReadyNoMetric, "", true, multiTarget,
            "sin métrica de rendimiento por fase de partido (§13.4.1)"),
        AttributeKind.Speed => new PerkClassification(
            PerkBalanceCategory.Attribute, MetricReadiness.Inferred,
            MatchMetrics.BallThirdMaxShare, true, multiTarget,
            "sin perk real que confirme la métrica sensible (§13.4.1)"),
        AttributeKind.Technique => new PerkClassification(
            PerkBalanceCategory.Attribute, MetricReadiness.NotReadyNoBand,
            "passCompletionRate/shotsOnTargetShare", true, multiTarget,
            "métrica natural INFO sin banda (§13.4.1)"),
        AttributeKind.Leash => new PerkClassification(
            PerkBalanceCategory.Attribute, MetricReadiness.Ready, MatchMetrics.BallThirdMaxShare, true, multiTarget,
            "mismo mecanismo que Geometry/modifyLeash (§13.4.1)"),
        _ => throw new ArgumentOutOfRangeException(nameof(attribute)),
    };

    private static string ProbabilityMetricName(ProbabilityKind kind) => kind switch
    {
        ProbabilityKind.Tackle or ProbabilityKind.TackleEvasion => MatchMetrics.TacklesPerMatch,
        ProbabilityKind.ShotOnTarget => MatchMetrics.ShotsOnTargetShare,
        ProbabilityKind.Save => MatchMetrics.SaveRate,
        ProbabilityKind.Pass or ProbabilityKind.Intercept or ProbabilityKind.InterceptEvasion => MatchMetrics.PassCompletionRate,
        ProbabilityKind.Injury or ProbabilityKind.Injure or ProbabilityKind.SevereInjury => MatchMetrics.InjuriesPerMatch,
        ProbabilityKind.Dribble => MatchMetrics.PassCompletionRate,
        ProbabilityKind.Foul => MatchMetrics.FoulsPerMatch,
        ProbabilityKind.Card => MatchMetrics.YellowCardsPerMatch,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ScalarMetricName(TraitScalarKind kind) => kind switch
    {
        TraitScalarKind.InjuryChanceBonus or TraitScalarKind.InjuryResistanceBonus => MatchMetrics.InjuriesPerMatch,
        TraitScalarKind.HardTackleBonus => MatchMetrics.TacklesPerMatch,
        TraitScalarKind.ShootRangeBonusCells => MatchMetrics.ShotsPerMatch,
        TraitScalarKind.LeashBonus => MatchMetrics.BallThirdMaxShare,
        TraitScalarKind.ShotQualityBonus => MatchMetrics.ShotsOnTargetShare,
        TraitScalarKind.PassQualityBonus => MatchMetrics.PassCompletionRate,
        TraitScalarKind.FoulChanceBonus => MatchMetrics.FoulsPerMatch,
        TraitScalarKind.SaveBonusClose or TraitScalarKind.SaveBonusFar => MatchMetrics.SaveRate,
        TraitScalarKind.SpeedBonusPercent => MatchMetrics.BallThirdMaxShare,
        TraitScalarKind.AdjacentTeammateBonusPercent => "histograma de acción",
        TraitScalarKind.FatigueResistancePercent => "",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
