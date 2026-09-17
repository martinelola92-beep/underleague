namespace Underleague.Sim.Analysis;

/// <summary>
/// Lo que hay que guardar de un perk que Screening deja en <c>NEEDS_TUNING</c> (§18 punto 9 del encargo
/// del 19 sep 2026): "guarda baseline/valor actual/dirección/efecto observado/rango objetivo/estrategia
/// sugerida y continúa con el resto — NO empieces a buscar el valor óptimo todavía". <see cref="TargetRange"/>
/// es <c>null</c> a propósito: ningún perk de este lote tiene una banda de calidad de efecto propia
/// definida hoy (solo las bandas de seguridad de RT-056, que son otra cosa) — no se inventa una.
/// </summary>
public sealed record TuningCandidateInfo(
    double BaselineValue,
    double CurrentValue,
    string Direction,
    double ObservedEffect,
    string? TargetRange,
    SearchStrategyKind SearchStrategy);

/// <summary>Coste real del screening de un perk (§18 punto 6): lo que el propio motor de balanceo cuesta, no el juego.</summary>
public sealed record ScreeningCost(
    long WallTimeMs,
    int MatchesSimulated,
    long TicksSimulated,
    int Batches,
    string EarlyStopReason);

/// <summary>
/// Resultado completo de aplicar Screening a un perk (§18). Reutiliza <see cref="BalanceState"/> (nada de
/// un enum nuevo, instrucción explícita del encargo) — <see cref="DisplayState"/> traduce al vocabulario
/// pedido en el informe (<c>SCREENING_PASS</c>, <c>SCREENING_NEEDS_TUNING</c>, etc.) sin introducir un
/// segundo estado paralelo.
/// </summary>
public sealed record ScreeningResult(
    string PerkId,
    BalanceState FinalState,
    string Reason,
    double? ExposureFraction,
    double? PrimaryDelta,
    bool? PowerSufficient,
    IReadOnlyList<string> SafetyMetricsOut,
    IReadOnlyList<string> SystemicSignals,
    TuningCandidateInfo? TuningInfo,
    ScreeningCost Cost,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Traducción de <see cref="BalanceState"/> al vocabulario exacto del encargo del 19 sep 2026 (§18):
    /// <c>NotReady</c> aquí significa "no superó la re-verificación del contrato ANTES de screening"
    /// (nunca "sin categoría"), <c>Tuning</c>/<c>Validating</c> significan "screening terminó pidiendo/no
    /// pidiendo tuning", no sus nombres literales de la máquina de estados de §9.
    /// </summary>
    public string DisplayState => FinalState switch
    {
        BalanceState.NotReady => "BLOCKED_BEFORE_SCREENING",
        BalanceState.Tuning => "SCREENING_NEEDS_TUNING",
        BalanceState.Validating => "SCREENING_PASS",
        BalanceState.InsufficientExposure => "INSUFFICIENT_EXPOSURE",
        BalanceState.InsufficientEvidence => "INSUFFICIENT_EVIDENCE",
        BalanceState.SafetyLimit => "SAFETY_LIMIT",
        BalanceState.DesignReview => "DESIGN_ESCALATION",
        _ => FinalState.ToString().ToUpperInvariant(),
    };
}
