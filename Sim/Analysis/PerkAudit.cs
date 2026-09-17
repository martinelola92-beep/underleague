using Underleague.Sim.Events;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Veredicto final de la auditoría (§16 de docs/analisis/protocolo-balanceo-automatizado.md): reutiliza
/// <see cref="BalanceState"/> donde ya alcanza (<c>RunLevel</c>) y añade solo lo que de verdad hace falta
/// y no existía — <c>MultiTarget</c> como bucket propio (el encargo lo pide explícitamente junto a
/// <c>NotReady</c>/<c>DesignReview</c>, no como una razón más dentro de <c>NotReady</c>).
/// </summary>
public enum AuditReadiness
{
    ReadyForScreening,
    MultiTarget,
    DesignReview,
    NotReady,
    RunLevel,
}

/// <summary>Motivo, dentro de <see cref="AuditReadiness.NotReady"/>, mutuamente excluyentes (§16).</summary>
public enum NotReadyReason
{
    None,

    /// <summary>No existe ninguna métrica, ni bandeada ni INFO (<see cref="MetricReadiness.NotReadyNoMetric"/>).</summary>
    MissingPrimaryMetric,

    /// <summary>El dato existe por partido pero no hay fila agregada en MatchMetrics (<see cref="MetricReadiness.NotReadyMissingAggregate"/>).</summary>
    MissingAggregateMetric,

    /// <summary>AccumulatesAcrossMatches + UsesCounter: necesita el harness de campaña, no el de partido independiente.</summary>
    NeedsCampaignHarness,

    /// <summary>≥2 efectos (sin contar addCounter) de categorías de balanceo distintas: no está definido qué parámetro se ajusta.</summary>
    MultiEffectAttribution,
}

/// <summary>Motivo, dentro de <see cref="AuditReadiness.DesignReview"/>, mutuamente excluyentes (§16).</summary>
public enum DesignReviewReason
{
    None,

    /// <summary>La métrica natural existe pero es INFO en MatchMetrics, sin banda (<see cref="MetricReadiness.NotReadyNoBand"/>).</summary>
    MissingBand,

    /// <summary>El efecto toca varias resoluciones y ningún dato del perk permite elegir la primaria (<see cref="MetricReadiness.AmbiguousPrimaryMetric"/>).</summary>
    AmbiguousPrimaryMetric,
}

/// <summary>
/// Qué tan frecuente es, en términos estructurales (sin simular nada — por el propio <see cref="EventType"/>),
/// el disparador de un perk. Se usa solo para no confundir "activación baja porque el disparador ya es
/// raro por diseño" con "activación baja por exposición insuficiente" (§5.4/§16, el matiz de <c>Limit</c>).
/// </summary>
public enum TriggerFrequencyCategory
{
    /// <summary>MATCH_START/PLAY_START/MATCH_END: ocurre como mucho una vez por partido, siempre.</summary>
    AlwaysOnce,

    /// <summary>TACKLE/SHOT/PASS_COMPLETED/PASS_FAILED/DRIBBLE_ATTEMPTED/DRIBBLE_WON/RECOVERY/SAVE: decenas de veces por partido.</summary>
    Frequent,

    /// <summary>FOUL: varias veces por partido, pero menos que una acción de juego normal.</summary>
    Occasional,

    /// <summary>INJURY/GOAL/DEATH: unas pocas veces por partido como mucho, a menudo cero.</summary>
    Rare,
}

/// <summary>Resultado completo de auditar un perk (§16).</summary>
public sealed record PerkAuditEntry(
    string PerkId,
    IReadOnlyList<EffectType> EffectTypes,
    IReadOnlyList<EffectTargetShape> TargetShapes,
    bool IsMultiEffect,
    bool HasCrossCategoryEffects,
    PerkBalanceCategory PrimaryCategory,
    MetricReadiness PrimaryMetricReadiness,
    string PrimaryMetric,
    AuditReadiness FinalReadiness,
    NotReadyReason NotReadyReason,
    DesignReviewReason DesignReviewReason,
    bool HasLimit,
    TriggerFrequencyCategory TriggerFrequency,
    string? LimitNote,
    string Notes);

/// <summary>Agregado de una auditoría completa del catálogo (§16, "informe agregado").</summary>
public sealed record AuditSummary(
    int Total,
    IReadOnlyDictionary<AuditReadiness, int> ByReadiness,
    IReadOnlyDictionary<NotReadyReason, int> NotReadyByReason,
    IReadOnlyDictionary<DesignReviewReason, int> DesignReviewByReason);

/// <summary>
/// Auditoría estática (sin simular ningún partido) del catálogo real contra el protocolo de balanceo
/// (§16 de docs/analisis/protocolo-balanceo-automatizado.md). Compone <see cref="PerkBalanceClassifier"/>
/// con las comprobaciones que el clasificador por sí solo no hacía: atribución multi-efecto,
/// forma del destinatario más allá de un booleano, y el matiz de <c>Limit</c> frente a exposición.
/// </summary>
public static class PerkAudit
{
    public static TriggerFrequencyCategory ClassifyTriggerFrequency(EventType trigger) => trigger switch
    {
        EventType.MatchStart or EventType.PlayStart or EventType.MatchEnd => TriggerFrequencyCategory.AlwaysOnce,
        EventType.Foul => TriggerFrequencyCategory.Occasional,
        EventType.Injury or EventType.Goal or EventType.Death => TriggerFrequencyCategory.Rare,
        _ => TriggerFrequencyCategory.Frequent, // Tackle, Shot, PassCompleted, PassFailed, DribbleAttempted, DribbleWon, Recovery, Save, ...
    };

    public static PerkAuditEntry Audit(PerkDefinition perk)
    {
        ArgumentNullException.ThrowIfNull(perk);
        var classification = PerkBalanceClassifier.Classify(perk);

        var effectTypes = perk.Effects.Select(e => e.Type).ToList();
        var targetShapes = perk.Effects
            .Select(e => PerkBalanceClassifier.ClassifyTargetShape(e.Target))
            .Distinct()
            .ToList();
        bool multiTarget = targetShapes.Any(s => s != EffectTargetShape.SingleOwner);

        var nonCounterEffects = perk.Effects.Where(e => e.Type != EffectType.AddCounter).ToList();
        var distinctCategories = nonCounterEffects
            .Select(e => PerkBalanceClassifier.ClassifyEffectTypeCategory(e.Type))
            .Distinct()
            .ToList();
        bool crossCategory = distinctCategories.Count > 1;

        var triggerFrequency = ClassifyTriggerFrequency(perk.Trigger);
        bool hasLimit = perk.Limit is not null;
        string? limitNote = !hasLimit
            ? null
            : triggerFrequency is TriggerFrequencyCategory.Rare or TriggerFrequencyCategory.Occasional
                ? $"disparador {perk.Trigger} ya es {triggerFrequency} por diseño — una activación baja aquí NO debe leerse como INSUFFICIENT_EXPOSURE sin más"
                : $"disparador {perk.Trigger} es {triggerFrequency} — una activación baja medida en Screening sí sería una señal real de exposición insuficiente, no del límite";

        AuditReadiness finalReadiness;
        var notReadyReason = NotReadyReason.None;
        var designReviewReason = DesignReviewReason.None;
        string notes = classification.Note;

        if (perk.Effects.Count == 0)
        {
            finalReadiness = AuditReadiness.NotReady;
            notReadyReason = NotReadyReason.MissingPrimaryMetric;
        }
        else if (classification.Category == PerkBalanceCategory.RunLevelCounter)
        {
            finalReadiness = AuditReadiness.RunLevel;
        }
        else if (classification.Readiness == MetricReadiness.NeedsCampaignHarness)
        {
            finalReadiness = AuditReadiness.NotReady;
            notReadyReason = NotReadyReason.NeedsCampaignHarness;
        }
        else if (multiTarget)
        {
            finalReadiness = AuditReadiness.MultiTarget;
            notes = $"{notes} — destinatario no soportado por el harness de un solo portador ({string.Join(",", targetShapes)})";
        }
        else if (crossCategory)
        {
            finalReadiness = AuditReadiness.NotReady;
            notReadyReason = NotReadyReason.MultiEffectAttribution;
            notes = $"{notes} — {nonCounterEffects.Count} efectos de categorías distintas ({string.Join(",", distinctCategories)}): no está definido qué parámetro se ajusta";
        }
        else
        {
            (finalReadiness, notReadyReason, designReviewReason) = classification.Readiness switch
            {
                MetricReadiness.Ready or MetricReadiness.ReadyViaBehavioralAudit or MetricReadiness.Inferred =>
                    (AuditReadiness.ReadyForScreening, NotReadyReason.None, DesignReviewReason.None),
                MetricReadiness.NotReadyNoBand =>
                    (AuditReadiness.DesignReview, NotReadyReason.None, DesignReviewReason.MissingBand),
                MetricReadiness.AmbiguousPrimaryMetric =>
                    (AuditReadiness.DesignReview, NotReadyReason.None, DesignReviewReason.AmbiguousPrimaryMetric),
                MetricReadiness.NotReadyNoMetric =>
                    (AuditReadiness.NotReady, NotReadyReason.MissingPrimaryMetric, DesignReviewReason.None),
                MetricReadiness.NotReadyMissingAggregate =>
                    (AuditReadiness.NotReady, NotReadyReason.MissingAggregateMetric, DesignReviewReason.None),
                _ => (AuditReadiness.NotReady, NotReadyReason.MissingPrimaryMetric, DesignReviewReason.None),
            };
        }

        return new PerkAuditEntry(
            perk.Id, effectTypes, targetShapes, perk.Effects.Count > 1, crossCategory,
            classification.Category, classification.Readiness, classification.PrimaryMetric,
            finalReadiness, notReadyReason, designReviewReason, hasLimit, triggerFrequency, limitNote, notes);
    }

    public static IReadOnlyList<PerkAuditEntry> AuditCatalog(IEnumerable<PerkDefinition> perks) =>
        perks.Select(Audit).ToList();

    public static AuditSummary Summarize(IReadOnlyList<PerkAuditEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var byReadiness = Enum.GetValues<AuditReadiness>().ToDictionary(r => r, r => 0);
        var notReadyByReason = Enum.GetValues<NotReadyReason>().Where(r => r != NotReadyReason.None).ToDictionary(r => r, r => 0);
        var designReviewByReason = Enum.GetValues<DesignReviewReason>().Where(r => r != DesignReviewReason.None).ToDictionary(r => r, r => 0);

        foreach (var entry in entries)
        {
            byReadiness[entry.FinalReadiness]++;
            if (entry.NotReadyReason != NotReadyReason.None)
            {
                notReadyByReason[entry.NotReadyReason]++;
            }

            if (entry.DesignReviewReason != DesignReviewReason.None)
            {
                designReviewByReason[entry.DesignReviewReason]++;
            }
        }

        return new AuditSummary(entries.Count, byReadiness, notReadyByReason, designReviewByReason);
    }
}
