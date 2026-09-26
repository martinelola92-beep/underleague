using Underleague.Sim.Data;
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

    /// <summary>
    /// Habilidad racial automática (<c>catalog.Race(race).Ability</c>): se asigna a TODA la plantilla de
    /// esa raza, no ocupa slot y no es una elección de portador — el harness de portador único no puede
    /// medirla (mismo criterio de exclusión que <c>PerkValueRunner.Run</c>, no una regla nueva).
    /// Encontrado ejecutando el contrato de <c>READY_FOR_SCREENING</c> contra datos reales (§16.6).
    /// </summary>
    RacialAbility,
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

    /// <summary>
    /// El matiz de <c>Limit</c> frente a exposición (§5.4/§16), o null si el perk no tiene límite. Está
    /// aparte porque la rama de habilidad racial también lo necesita: devolvía <c>HasLimit</c> sin nota, y
    /// la pareja (con límite, sin nota) era imposible hasta que una racial llevó límite por primera vez.
    /// </summary>
    private static string? LimitNoteFor(PerkDefinition perk)
    {
        if (perk.Limit is null)
        {
            return null;
        }

        var frequency = ClassifyTriggerFrequency(perk.Trigger);
        return frequency is TriggerFrequencyCategory.Rare or TriggerFrequencyCategory.Occasional
            ? $"disparador {perk.Trigger} ya es {frequency} por diseño — una activación baja aquí NO debe leerse como INSUFFICIENT_EXPOSURE sin más"
            : $"disparador {perk.Trigger} es {frequency} — una activación baja medida en Screening sí sería una señal real de exposición insuficiente, no del límite";
    }

    public static PerkAuditEntry Audit(PerkDefinition perk, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);
        var classification = PerkBalanceClassifier.Classify(perk);

        // Habilidad racial automática (§16.6, encontrado ejecutando el contrato de READY_FOR_SCREENING
        // contra datos reales — elf_touch se asigna a TODA la plantilla élfica y no ocupa slot; el
        // harness de portador único nunca encuentra un "titular elegible" porque no es una elección de
        // slot). Mismo criterio de exclusión que ya usa Balance/PerkValueRunner.cs; no es una regla nueva.
        if (perk.Race is { } perkRace && string.Equals(perk.Id, catalog.Race(perkRace).Ability, StringComparison.Ordinal))
        {
            return new PerkAuditEntry(
                perk.Id, perk.Effects.Select(e => e.Type).ToList(), Array.Empty<EffectTargetShape>(), false, false,
                classification.Category, classification.Readiness, classification.PrimaryMetric,
                AuditReadiness.NotReady, NotReadyReason.RacialAbility, DesignReviewReason.None,
                perk.Limit is not null, ClassifyTriggerFrequency(perk.Trigger), LimitNoteFor(perk),
                $"habilidad racial automática de {perkRace}: se asigna a toda la plantilla, no ocupa slot, no es medible con el harness de portador único (§16.6)");
        }

        var effectTypes = perk.Effects.Select(e => e.Type).ToList();
        var targetShapes = perk.Effects
            .Select(e => PerkBalanceClassifier.ClassifyTargetShape(perk.Scope, e.Target))
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
        string? limitNote = LimitNoteFor(perk);

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

    public static IReadOnlyList<PerkAuditEntry> AuditCatalog(IEnumerable<PerkDefinition> perks, Catalog catalog) =>
        perks.Select(p => Audit(p, catalog)).ToList();

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
