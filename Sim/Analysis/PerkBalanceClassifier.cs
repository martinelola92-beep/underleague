using Underleague.Sim.Events;
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

    /// <summary>
    /// <c>AccumulatesAcrossMatches</c> con un efecto acompañante escalado por el contador
    /// (<c>UsesCounter</c>): distinto de <see cref="RunLevelCounter"/> (que no tiene ningún efecto de
    /// partido, solo economía) — aquí SÍ hay un mecanismo de partido, pero un partido suelto con el
    /// contador a cero no representa su magnitud típica a mitad/final de run (§13, auditoría del 18 sep
    /// 2026). Necesita el harness de campaña que ya existe (<c>Balance/PerkValueRunner.cs</c>, ADR 0087),
    /// no uno nuevo — pero no el harness de partido independiente de este protocolo.
    /// </summary>
    AccumulatedStateBonus,
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

    /// <summary>
    /// El efecto toca varias resoluciones a la vez y ningún dato estructural del perk (Family/
    /// PositionOnly/tags) permite elegir automáticamente cuál es la primaria — verificado contra los
    /// perks reales, no asumido (§16: los cuatro perks de <c>Strength</c> no declaran <c>positionOnly</c>).
    /// Necesita `game-design-review`, no una medición.
    /// </summary>
    AmbiguousPrimaryMetric,

    /// <summary>
    /// <c>AccumulatesAcrossMatches</c> + efecto acompañante con <c>UsesCounter</c>: el partido suelto con
    /// contador a cero no representa el mecanismo real. Necesita el harness de campaña existente
    /// (<c>PerkValueRunner</c>), no el de partido independiente — no es un hueco de tooling nuevo, es una
    /// elección de instrumento (§16).
    /// </summary>
    NeedsCampaignHarness,
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
/// Forma del destinatario de un efecto (§16, auditoría del 18 sep 2026 — más preciso que el booleano
/// <see cref="PerkClassification.NeedsMultiTargetHarness"/>, que solo dice "no es el portador solo").
/// </summary>
public enum EffectTargetShape
{
    /// <summary><see cref="EffectTarget.Owner"/>/<see cref="EffectTarget.Actor"/>: el propio portador — el harness actual ya lo mide.</summary>
    SingleOwner,

    /// <summary>
    /// <see cref="EffectTarget.Target"/>/<see cref="EffectTarget.Opponent"/>: un único jugador por
    /// disparo, pero NO el portador — cambia en cada evento (p. ej. el rival al que se entra). El harness
    /// actual mide las estadísticas del portador, no las del receptor: necesita seguir a quien recibe el
    /// efecto, no a quien lo posee.
    /// </summary>
    SingleOther,

    /// <summary>
    /// <see cref="EffectTarget.Team"/>/<see cref="EffectTarget.OpposingTeam"/>/<see cref="EffectTarget.WithTag"/>/
    /// <see cref="EffectTarget.AdjacentWithTag"/>/<see cref="EffectTarget.Adjacent"/>/
    /// <see cref="EffectTarget.AdjacentOpponents"/>/<see cref="EffectTarget.Linked"/>/
    /// <see cref="EffectTarget.LinkedWithTag"/>: puede resolver a varios jugadores a la vez (un equipo
    /// entero, un subconjunto por etiqueta, los vinculados —plural— del portador). El harness de un solo
    /// portador no está diseñado para esto (§3.2 punto 7, `pack_mentality`).
    /// </summary>
    Population,
}

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

    /// <summary>
    /// El efecto "objetivo" de un perk (§3: el primero que no sea <see cref="EffectType.AddCounter"/>,
    /// que se registra pero no compite por ser "el" mecanismo). Público para que el screening (§18) pueda
    /// leer el signo de <see cref="EffectDefinition.Value"/> del mismo efecto que ya usa la clasificación,
    /// sin repetir el criterio de selección en otro sitio.
    /// </summary>
    public static EffectDefinition GetPrimaryEffect(PerkDefinition perk)
    {
        ArgumentNullException.ThrowIfNull(perk);
        return perk.Effects.FirstOrDefault(e => e.Type != EffectType.AddCounter) ?? perk.Effects[0];
    }

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

        bool multiTarget = perk.Effects.Any(e => ClassifyTargetShape(perk.Scope, e.Target) != EffectTargetShape.SingleOwner);

        // Solo addCounter, sin ningún efecto acompañante: pura economía/progresión, sin mecanismo de
        // partido que medir — RunLevelCounter (§3, RUN_LEVEL).
        bool pureRunLevel = perk.Effects.Count == 1 && perk.Effects[0].Type == EffectType.AddCounter;
        if (pureRunLevel)
        {
            return new PerkClassification(
                PerkBalanceCategory.RunLevelCounter, MetricReadiness.Ready, "FullRunMetrics", false, multiTarget,
                "addCounter en solitario, sin efecto de partido: se mide con /Balance --full-runs, fuera del bucle rápido (§3, RUN_LEVEL)");
        }

        // El efecto "objetivo" es el primero que no sea addCounter (§3: el contador se registra, no compite).
        var primary = GetPrimaryEffect(perk);

        // AccumulatesAcrossMatches con el efecto acompañante escalado por el contador (UsesCounter): un
        // partido suelto con el contador a cero no representa la magnitud típica a mitad/final de run —
        // corregido en la auditoría del 18 sep 2026 (antes esto caía en el mismo "RunLevel" que la
        // economía pura, etiqueta imprecisa: aquí SÍ hay mecanismo de partido, solo que el instrumento
        // correcto es la campaña (PerkValueRunner), no el partido independiente de este protocolo).
        if (perk.AccumulatesAcrossMatches && primary.UsesCounter)
        {
            return new PerkClassification(
                PerkBalanceCategory.AccumulatedStateBonus, MetricReadiness.NeedsCampaignHarness,
                DescribePrimaryMetric(primary), true, multiTarget,
                $"{primary.Type} escalado por un contador que persiste entre partidos (AccumulatesAcrossMatches) — necesita el harness de campaña (Balance/PerkValueRunner.cs, ADR 0087), no el de partido independiente");
        }

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

            // Corrección del 18 sep 2026 (punto 2 del encargo): RefereeBiasMetrics.MeanBiasFavoringCarrier
            // ya construye el agregado de forma objetiva (normaliza FinalBias al equipo del portador, sin
            // inventar nada) — la métrica EXISTE. Pero eso no la hace SUFICIENTE para balanceo automático:
            // no hay ninguna banda RT-056 ni ADR que diga qué sesgo medio es aceptable, así que sigue
            // siendo NotReadyNoBand (→ DESIGN_REVIEW), nunca Ready. "La métrica existe" y "la métrica basta
            // para decidir sola" son dos preguntas distintas — no se confunden.
            EffectType.ModifyBias => new PerkClassification(
                PerkBalanceCategory.RefereeBias, MetricReadiness.NotReadyNoBand,
                "RefereeBiasMetrics.MeanBiasFavoringCarrier (FinalBias normalizado)", true, multiTarget,
                "el agregado ya se puede calcular (Sim/Analysis/RefereeBiasMetrics.cs) pero no existe ninguna banda ni ADR que diga qué sesgo medio es aceptable — sigue en DESIGN_REVIEW, no Ready"),

            // Immunity/CancelEvent/ExtraAction NO son un bloque uniforme "Ready" (corrección de la
            // auditoría del 18 sep 2026, §16): cada uno depende de QUÉ suceso toca (el Trigger del perk
            // para CancelEvent/ExtraAction, el ImmunityKind para Immunity), y varios de esos sucesos no
            // tienen fila agregada o no tienen banda en MatchMetrics — se descubrió al auditar los 94
            // perks reales, no estaba anticipado en la primera versión del clasificador.
            EffectType.CancelEvent => ClassifyCancelEvent(perk.Trigger, multiTarget),

            EffectType.Immunity => ClassifyImmunity(primary.Immunity, multiTarget),

            EffectType.ExtraAction => ClassifyExtraAction(perk.Trigger, multiTarget),

            // ModifyExperience actúa fuera del partido (ADR 0026, Humanos): dentro del partido no deja
            // nada que un harness de partido suelto pueda medir — mismo instrumento que RunLevelCounter
            // (progresión/economía), no un hueco de tooling de partido.
            EffectType.ModifyExperience => new PerkClassification(
                PerkBalanceCategory.RunLevelCounter, MetricReadiness.Ready, "FullRunMetrics", false, multiTarget,
                "ModifyExperience actúa fuera del partido — se mide a nivel de run, no de partido suelto"),

            _ => new PerkClassification(
                PerkBalanceCategory.Singular, MetricReadiness.NotReadyNoMetric, "", false, multiTarget,
                $"{primary.Type}: caso singular sin métrica agregada conocida en MatchMetrics — sin plantilla genérica todavía (§3), no se inventa una"),
        };

        return classification;
    }

    /// <summary>
    /// Categoría de balanceo de un efecto por su solo <see cref="EffectType"/> (§16): más simple que
    /// <see cref="Classify"/> (que además mira el sub-tipo — <c>ProbabilityKind</c>/<c>TraitScalarKind</c>
    /// — y el resto del perk), a propósito, para detectar atribución multi-efecto: si dos efectos de un
    /// mismo perk caen en categorías DISTINTAS por este mapeo simple, ya hay algo que decidir (§3.2 punto
    /// 7/8), sin necesitar el detalle fino del sub-tipo.
    /// </summary>
    public static PerkBalanceCategory ClassifyEffectTypeCategory(EffectType type) => type switch
    {
        EffectType.ModifyUtility => PerkBalanceCategory.UtilityBonus,
        EffectType.ModifyProbability => PerkBalanceCategory.ProbabilityBonus,
        EffectType.ModifyTraitScalar => PerkBalanceCategory.TraitScalar,
        EffectType.ModifyAttribute => PerkBalanceCategory.Attribute,
        EffectType.ModifyLeash or EffectType.ShiftHome or EffectType.ModifyZoneShape => PerkBalanceCategory.Geometry,
        EffectType.ModifyMarkBias or EffectType.ModifyTackleBias => PerkBalanceCategory.TargetSelection,
        EffectType.ModifyBias => PerkBalanceCategory.RefereeBias,
        EffectType.Immunity or EffectType.CancelEvent or EffectType.ExtraAction => PerkBalanceCategory.BinaryEvent,
        EffectType.AddCounter => PerkBalanceCategory.RunLevelCounter,
        _ => PerkBalanceCategory.Singular,
    };

    /// <summary>
    /// Forma del destinatario de un <see cref="EffectTarget"/> (§16): §16 en vez de un booleano. Los tres
    /// objetivos de evento (actor, target, opponent) son el propio portador sólo cuando el alcance del perk
    /// lo pone en ese papel (BM-A): con <c>scope: opponent</c>, <c>actor</c> es el rival que le entra, no él.
    /// </summary>
    public static EffectTargetShape ClassifyTargetShape(PerkScope scope, EffectTarget target) => target switch
    {
        EffectTarget.Owner => EffectTargetShape.SingleOwner,
        EffectTarget.Actor => scope == PerkScope.Actor ? EffectTargetShape.SingleOwner : EffectTargetShape.SingleOther,
        EffectTarget.Target => scope == PerkScope.Target ? EffectTargetShape.SingleOwner : EffectTargetShape.SingleOther,
        EffectTarget.Opponent => scope == PerkScope.Opponent ? EffectTargetShape.SingleOwner : EffectTargetShape.SingleOther,
        _ => EffectTargetShape.Population, // Team/OpposingTeam/WithTag/AdjacentWithTag/Adjacent/AdjacentOpponents/Linked/LinkedWithTag
    };

    /// <summary>Nombre descriptivo de la métrica primaria de un efecto ya identificado como <see cref="MetricReadiness.NeedsCampaignHarness"/>.</summary>
    private static string DescribePrimaryMetric(EffectDefinition effect) => effect.Type switch
    {
        EffectType.ModifyProbability => ProbabilityMetricName(effect.Probability),
        EffectType.ModifyAttribute => "según atributo (ver ClassifyAttribute)",
        EffectType.ModifyLeash or EffectType.ShiftHome or EffectType.ModifyZoneShape => MatchMetrics.BallThirdMaxShare,
        EffectType.ModifyTraitScalar => ScalarMetricName(effect.Scalar),
        _ => "",
    };

    /// <summary>
    /// El evento que cancela un <see cref="EffectType.CancelEvent"/> es <c>perk.Trigger</c> (el efecto
    /// solo cancela lo que lo disparó — RT-034, sin lógica arbitraria). Solo <c>INJURY</c> tiene banda
    /// real hoy (injuriesPerMatch); <c>FOUL</c>/<c>CARD</c>/<c>GOAL</c> son INFO; <c>DEATH</c> no tiene
    /// ninguna fila agregada en <see cref="MatchMetrics"/> (las muertes se cuentan a nivel de run, no de
    /// partido suelto). Verificado contra los 4 perks reales (iron_gate/mob_instigator/hand_of_god/
    /// no_dying), no asumido.
    /// </summary>
    private static PerkClassification ClassifyCancelEvent(EventType trigger, bool multiTarget) => trigger switch
    {
        EventType.Injury => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.Ready, MatchMetrics.InjuriesPerMatch, false, multiTarget,
            "cancela INJURY: injuriesPerMatch ya tiene banda"),
        EventType.Foul => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.NotReadyNoBand, MatchMetrics.FoulsPerMatch, false, multiTarget,
            "cancela FOUL: foulsPerMatch es INFO, sin banda"),
        EventType.Card => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.NotReadyNoBand, MatchMetrics.YellowCardsPerMatch, false, multiTarget,
            "cancela CARD: yellowCardsPerMatch es INFO, sin banda"),
        EventType.Goal => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.NotReadyNoBand, MatchMetrics.GoalsPerMatch, false, multiTarget,
            "cancela GOAL: goalsPerMatch es INFO, sin banda"),
        _ => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.NotReadyNoMetric, "", false, multiTarget,
            $"cancela {trigger}: sin fila agregada en MatchMetrics (p. ej. DEATH se cuenta a nivel de run, no de partido)"),
    };

    /// <summary>
    /// Los cuatro <see cref="ImmunityKind"/> (Push, Mourning, MinorInjuryPenalty, MinorInjuryClinicCost)
    /// son desplazamiento físico o penalización/coste ENTRE partidos (RF-035, RF-094, RF-104) — ninguno
    /// tiene fila agregada en <see cref="MatchMetrics"/>, verificado contra los 3+ perks reales
    /// (half_leg/tough_hide/roots), no asumido. Corrige el "Ready" uniforme de la primera versión.
    /// </summary>
    private static PerkClassification ClassifyImmunity(ImmunityKind kind, bool multiTarget) => new(
        PerkBalanceCategory.BinaryEvent, MetricReadiness.NotReadyNoMetric, "", false, multiTarget,
        $"inmunidad de tipo {kind}: desplazamiento físico o coste entre partidos, sin fila agregada en MatchMetrics (§16)");

    /// <summary>
    /// <see cref="EffectType.ExtraAction"/> repite la acción que lo disparó (Doble disparo, Embestida,
    /// Arrollador) — el disparador solo puede ser <c>SHOT</c>, <c>TACKLE</c> o <c>RECOVERY</c> (RT-032), y
    /// los tres SÍ tienen banda: la repetición es, literalmente, un shotsPerMatch/tacklesPerMatch más.
    ///
    /// <para><c>RECOVERY</c> repite la <b>entrada</b>, no una "recuperación" (BB-Q Alt 0: es la forma de
    /// reaccionar al resultado de una entrada, que TACKLE no puede ver por publicarse antes de
    /// resolverse). La métrica la fija por tanto <c>RepeatTackle</c>, que es lo que el efecto ejecuta
    /// (<c>EffectEngine.ExecuteExtraAction</c>), no el nombre del disparador.</para>
    /// </summary>
    private static PerkClassification ClassifyExtraAction(EventType trigger, bool multiTarget) => trigger switch
    {
        EventType.Shot => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.Ready, MatchMetrics.ShotsPerMatch, false, multiTarget,
            "repite SHOT: cuenta como un shotsPerMatch más, banda ya existente"),
        EventType.Tackle => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.Ready, MatchMetrics.TacklesPerMatch, false, multiTarget,
            "repite TACKLE: cuenta como un tacklesPerMatch más, banda ya existente"),
        EventType.Recovery => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.Ready, MatchMetrics.TacklesPerMatch, false, multiTarget,
            "repite la ENTRADA tras una recuperación (RepeatTackle): cuenta como un tacklesPerMatch más, banda ya existente"),
        _ => new PerkClassification(
            PerkBalanceCategory.BinaryEvent, MetricReadiness.NotReadyNoMetric, "", false, multiTarget,
            $"repite {trigger}: sin métrica agregada conocida para esta acción"),
    };

    private static PerkClassification ClassifyAttribute(AttributeKind attribute, bool multiTarget) => attribute switch
    {
        // Ninguno de los cuatro perks reales de Strength (brute_boots, comeback_spirit, pack_mentality,
        // scar_veteran) declara positionOnly, y solo dos de cuatro tienen family ("butchery", una cadena
        // libre, no un vocabulario controlado) — verificado, no asumido (§16): no hay dato estructural
        // para elegir automáticamente cuál de las cinco resoluciones que toca Strength es la primaria.
        AttributeKind.Strength => new PerkClassification(
            PerkBalanceCategory.Attribute, MetricReadiness.AmbiguousPrimaryMetric,
            "ambiguo: tacklesPerMatch, injuriesPerMatch o shotsOnTargetShare (sin banda) según diseño", true, multiTarget,
            "Strength toca cinco resoluciones a la vez (tackle/foul/shot/dribble/block/injury); los 4 perks reales no dan ninguna señal estructural (positionOnly/family) para elegir la primaria — necesita game-design-review, no se adivina (§13.4.1/§16)"),
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
