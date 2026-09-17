using System.Diagnostics;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// El primer screening REAL del catálogo (§18 de docs/analisis/protocolo-balanceo-automatizado.md,
/// encargo del 19 sep 2026): aplica literalmente la fase <c>READY_FOR_SCREENING → SCREENING →
/// clasificación automática</c> del protocolo — muestreo adaptativo (§5.1/§5.4/§5.5), motor de decisión
/// existente (<see cref="BalanceDecisionRules"/>, sin reimplementarlo), circuito de seguridad por perk y
/// por lote (§9/§9.1). <b>No hace tuning</b>: un perk que llega a <c>NEEDS_TUNING</c> se registra
/// (baseline, dirección, efecto observado, estrategia sugerida) y el lote sigue con el siguiente perk.
/// </summary>
public static class ScreeningRunner
{
    /// <summary>1 semilla, 20 plantillas × 2 direcciones = 40 partidos/brazo (§5.1, "muestra inicial", literal).</summary>
    public const int InitialRosters = 20;

    /// <summary>Remuestreo único si la exposición queda por debajo del suelo (§5.1: "subir la muestra una vez, a 120 plantillas").</summary>
    public const int ExposureRetryRosters = 120;

    /// <summary>
    /// Suelo de exposición discreta (§5.4: "provisional 50% de partidos con ≥1 activación") —
    /// <c>[ASUNCIÓN — PENDIENTE DE CALIBRAR]</c>, no se toca durante este screening (§18 punto 4).
    /// </summary>
    public const double DiscreteExposureFloor = 0.5;

    /// <summary>
    /// Límite de tiempo de reloj por perk (§9: "10 minutos de reloj de harness, sumando
    /// Screening+Tuning+Validation"). Aquí solo cubre Screening, pero es el mismo techo — no se inventa
    /// uno distinto para esta fase.
    /// </summary>
    public static readonly TimeSpan PerPerkBudget = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Duplicar la muestra una vez si la potencia estadística no distingue el efecto del ruido (§5.5,
    /// "duplicar la muestra... antes de decidir"; §9, la misma regla escrita para Tuning, aplicada aquí a
    /// la propia decisión de Screening de si hay o no un efecto real que tunear — es la pieza que faltaba
    /// para que Screening no acepte un `NEEDS_TUNING` con una muestra que en realidad no lo demuestra).
    /// </summary>
    private const int MaxPowerDoublings = 1;

    public static ScreeningResult RunPerk(Catalog catalog, PerkDefinition perk, ulong seed = 1)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(perk);

        var stopwatch = Stopwatch.StartNew();
        var notes = new List<string>();

        // Re-verificación del contrato: un perk que hoy figura ReadyForScreening en la auditoría estática
        // puede no cumplirlo (catálogo cambiado, bug de tooling) — si no lo cumple, NO se ejecuta ni un
        // partido (§18 punto 1).
        var contract = ReadyContract.Verify(catalog, perk);
        if (!contract.Satisfied)
        {
            return new ScreeningResult(
                perk.Id, BalanceState.NotReady,
                $"no cumple el contrato de READY_FOR_SCREENING: {string.Join("; ", contract.Missing)}",
                null, null, null, Array.Empty<string>(), Array.Empty<string>(), null,
                new ScreeningCost(stopwatch.ElapsedMilliseconds, 0, 0, 0, "contract_failed"), notes);
        }

        var classification = PerkBalanceClassifier.Classify(perk);
        var auditEntry = PerkAudit.Audit(perk, catalog);
        if (auditEntry.HasLimit)
        {
            notes.Add(
                "perk con Limit: la exposición cruda por activaciones puede confundir 'raro por el límite' " +
                "con 'raro por exposición insuficiente' — hueco de tooling documentado (§5.4/§13), no resuelto aquí.");
        }

        // --- Exposición (§5.1/§5.4): muestra inicial, un remuestreo único si hace falta. ---
        int rosters = InitialRosters;
        int batches = 0;
        bool exposureRetried = false;
        PairedBalanceHarness.PairedResult run;
        double exposureFraction;

        while (true)
        {
            batches++;
            run = PairedBalanceHarness.RunWithEligibleCarrier(catalog, perk, rosters, seed);
            exposureFraction = DiscreteExposureFraction(run);

            var exposureProbe = new ExposureCheck(exposureFraction, DiscreteExposureFloor, FloorConfidence.Assumption);
            var probe = BalanceDecisionRules.EvaluateScreening(
                exposureProbe, alreadyRetriedWithLargerSample: exposureRetried,
                anyMandatoryMetricOut: false, hasNumericParameter: classification.HasNumericParameter);

            if (probe != BalanceState.Screening || stopwatch.Elapsed > PerPerkBudget)
            {
                break; // exposición suficiente, o ya se remuestreó una vez, o se agotó el presupuesto de tiempo
            }

            exposureRetried = true;
            rosters = ExposureRetryRosters;
        }

        if (exposureFraction < DiscreteExposureFloor)
        {
            // Los tres suelos de exposición son [ASUNCIÓN] hoy (§5.4): nunca INSUFFICIENT_EXPOSURE con la
            // confianza de un umbral medido — siempre INSUFFICIENT_EVIDENCE.
            return new ScreeningResult(
                perk.Id, BalanceState.InsufficientEvidence,
                $"exposición {exposureFraction:P1} < suelo {DiscreteExposureFloor:P0} [ASUNCIÓN] tras remuestrear a {rosters} plantillas " +
                "— el umbral no está calibrado (§5.4), esto no es una conclusión sobre el perk, es una calibración pendiente.",
                exposureFraction, null, null, Array.Empty<string>(), Array.Empty<string>(), null,
                new ScreeningCost(stopwatch.ElapsedMilliseconds, run.ArmedMatches.Count + run.ControlMatches.Count, run.TotalSimulatedTicks, batches, "exposure_insufficient_after_retry"),
                notes);
        }

        // --- Potencia estadística sobre la métrica primaria (§5.5), solo si hay parámetro numérico. ---
        double? primaryDelta = null;
        bool? powerSufficient = null;
        int powerDoublings = 0;

        if (classification.HasNumericParameter)
        {
            (primaryDelta, powerSufficient) = PrimaryPower(classification.PrimaryMetric, run);

            while (powerSufficient == false && powerDoublings < MaxPowerDoublings && stopwatch.Elapsed <= PerPerkBudget)
            {
                powerDoublings++;
                rosters *= 2;
                batches++;
                run = PairedBalanceHarness.RunWithEligibleCarrier(catalog, perk, rosters, seed);
                exposureFraction = DiscreteExposureFraction(run); // no debería empeorar con más muestra, se recalcula por rigor, no se decide de nuevo sobre ella
                (primaryDelta, powerSufficient) = PrimaryPower(classification.PrimaryMetric, run);
            }

            if (powerSufficient == false)
            {
                return new ScreeningResult(
                    perk.Id, BalanceState.InsufficientEvidence,
                    $"delta={primaryDelta:F4} en {classification.PrimaryMetric} no se distingue del ruido tras duplicar la muestra una vez " +
                    "(§5.5: |delta| < 2×error_estándar) — nunca se decide NEEDS_TUNING con una muestra que no lo demuestra.",
                    exposureFraction, primaryDelta, false, Array.Empty<string>(), Array.Empty<string>(), null,
                    new ScreeningCost(stopwatch.ElapsedMilliseconds, run.ArmedMatches.Count + run.ControlMatches.Count, run.TotalSimulatedTicks, batches, "power_insufficient_after_doubling"),
                    notes);
            }
        }

        // --- Seguridad (RT-056, sobre el brazo armado) y señal sistémica (§8, sin declarar réplica). ---
        var armedMandatory = MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>())
            .Where(m => m.RangeMin is not null && m.RangeMax is not null)
            .ToList();
        var safetyOut = armedMandatory.Where(m => m.Status == "OUT").Select(m => $"{m.Name}={m.Value:F3} (rango {m.RangeMin:F2}..{m.RangeMax:F2})").ToList();
        var systemicSignals = SystemicSignals(classification.PrimaryMetric, armedMandatory, run);

        var exposureCheck = new ExposureCheck(exposureFraction, DiscreteExposureFloor, FloorConfidence.Assumption);
        var decision = BalanceDecisionRules.EvaluateScreening(
            exposureCheck, alreadyRetriedWithLargerSample: true, anyMandatoryMetricOut: safetyOut.Count > 0,
            hasNumericParameter: classification.HasNumericParameter);

        var cost = new ScreeningCost(
            stopwatch.ElapsedMilliseconds, run.ArmedMatches.Count + run.ControlMatches.Count, run.TotalSimulatedTicks,
            batches, decision == BalanceState.SafetyLimit ? "safety_limit" : "evidence_sufficient");

        if (decision == BalanceState.SafetyLimit)
        {
            notes.Add(
                $"las bandas de RT-056 son absolutas (población, no comparativas armado/control) y se comprueban aquí sobre solo " +
                $"{run.ArmedMatches.Count} partidos armados (§5.1: comprobación barata, no una prueba de potencia) — a esta escala " +
                "un falso positivo por varianza de muestra pequeña es posible; no se confirma como regresión real sin una muestra " +
                "mayor (Validation, §5.3), y NO se recalibra la banda aquí para decidir en un sentido u otro (§18 punto 4).");
            return new ScreeningResult(
                perk.Id, BalanceState.SafetyLimit,
                $"al menos una métrica obligatoria de RT-056 queda fuera de banda en el brazo armado: {string.Join("; ", safetyOut)}",
                exposureFraction, primaryDelta, powerSufficient, safetyOut, systemicSignals, null, cost, notes);
        }

        if (decision == BalanceState.Validating)
        {
            // Sin parámetro numérico (selección de objetivo, §6.5): el screening no detecta necesidad de
            // tuning porque no hay parámetro que tunear — SCREENING_PASS no implica BALANCED.
            return new ScreeningResult(
                perk.Id, BalanceState.Validating,
                "sin parámetro numérico (selección de objetivo, §6.5): screening no detecta necesidad de tuning. " +
                "Necesitaría una auditoría de distribución conductual para ir más allá — no implementada en este lote.",
                exposureFraction, primaryDelta, powerSufficient, safetyOut, systemicSignals, null, cost, notes);
        }

        // decision == Tuning (única rama restante: exposición suficiente, sin fallo de seguridad, con
        // parámetro numérico y potencia ya confirmada arriba).
        var primaryEffect = PerkBalanceClassifier.GetPrimaryEffect(perk);
        string expectedDirection = primaryEffect.Value >= 0 ? "increase" : "decrease";
        string observedDirection = primaryDelta is >= 0 ? "increase" : "decrease";

        if (expectedDirection != observedDirection)
        {
            // El motor de decisión existente (§6.1) no contempla "efecto real pero en dirección contraria
            // a lo que el propio valor del efecto predice" — gap explícito (§18 punto 4): se escala, no se
            // decide con una regla nueva inventada para que el perk encaje.
            return new ScreeningResult(
                perk.Id, BalanceState.DesignReview,
                $"efecto distinguible del ruido (delta={primaryDelta:F4}) pero en dirección {observedDirection}, contraria a la " +
                $"esperada por el signo del efecto ({expectedDirection}) — no cubierto por BalanceDecisionRules.EvaluateScreening, " +
                "se escala en vez de decidir con una regla no existente (§18 punto 4).",
                exposureFraction, primaryDelta, powerSufficient, safetyOut, systemicSignals, null, cost, notes);
        }

        var searchStrategy = BalanceSearchStrategy.SelectStrategy(classification.Category, true);
        var tuningInfo = new TuningCandidateInfo(
            BaselineValue: primaryEffect.Value, CurrentValue: primaryEffect.Value, Direction: expectedDirection,
            ObservedEffect: primaryDelta ?? 0.0, TargetRange: null, SearchStrategy: searchStrategy);

        return new ScreeningResult(
            perk.Id, BalanceState.Tuning,
            $"efecto real y distinguible del ruido en {classification.PrimaryMetric} (delta={primaryDelta:F4}, dirección {observedDirection} " +
            "según lo esperado) — necesita tuning. NO se ha buscado ningún valor nuevo (§18 punto 9): solo se registra el baseline.",
            exposureFraction, primaryDelta, powerSufficient, safetyOut, systemicSignals, tuningInfo, cost, notes);
    }

    /// <summary>
    /// El lote completo de los 24 (§18): orden determinista (por id), circuito de seguridad de §9.1 punto
    /// 3 (para el LOTE antes del siguiente perk, no a mitad de uno), checkpoint opcional por perk.
    /// </summary>
    public static IReadOnlyList<ScreeningResult> RunBatch(
        Catalog catalog, IReadOnlyList<PerkDefinition> perks, ulong seed = 1, string? checkpointDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(perks);

        var ordered = perks.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var results = new List<ScreeningResult>();
        int escalatedCount = 0;

        foreach (var perk in ordered)
        {
            string? checkpointPath = checkpointDirectory is null ? null : Path.Combine(checkpointDirectory, $"{perk.Id}.json");
            if (checkpointPath is not null && BalanceRegistryFile.Load(checkpointPath) is { } cached)
            {
                results.Add(FromRegistryEntry(cached));
                if (BatchEscalation.IsEscalatedState(cached.State))
                {
                    escalatedCount++;
                }

                continue;
            }

            if (BatchEscalation.ShouldStopBatch(results.Count, escalatedCount))
            {
                break; // §9.1 punto 3: circuito de seguridad de lote — no se procesa el siguiente perk.
            }

            var result = RunPerk(catalog, perk, seed);
            results.Add(result);
            if (BatchEscalation.IsEscalatedState(result.FinalState))
            {
                escalatedCount++;
            }

            if (checkpointPath is not null)
            {
                BalanceRegistryFile.Save(checkpointPath, ToRegistryEntry(result));
            }
        }

        return results;
    }

    private static double DiscreteExposureFraction(PairedBalanceHarness.PairedResult run) =>
        run.ArmedActivationsPerMatch.Count == 0
            ? 0.0
            : (double)run.ArmedActivationsPerMatch.Count(a => a > 0) / run.ArmedActivationsPerMatch.Count;

    private static (double Delta, bool Sufficient) PrimaryPower(string primaryMetric, PairedBalanceHarness.PairedResult run)
    {
        var armedValues = run.ArmedMatches.Select(m => PrimaryMetricPerMatch.Value(primaryMetric, m)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var controlValues = run.ControlMatches.Select(m => PrimaryMetricPerMatch.Value(primaryMetric, m)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (armedValues.Count < 2 || controlValues.Count < 2)
        {
            return (0.0, false);
        }

        double delta = armedValues.Average() - controlValues.Average();
        double varianceArmed = BalancePowerCheck.SampleVariance(armedValues);
        double varianceControl = BalancePowerCheck.SampleVariance(controlValues);
        bool sufficient = BalancePowerCheck.HasSufficientPower(delta, varianceArmed, armedValues.Count, varianceControl, controlValues.Count);
        return (delta, sufficient);
    }

    /// <summary>
    /// Señal sistémica (§8/§18 punto 5): métricas obligatorias DISTINTAS de la primaria del perk cuyo
    /// delta armado/control ya distingue del ruido (power-check), sin vía causal declarada para su
    /// categoría. Un solo seed — nunca se declara <c>SYSTEMIC_REGRESSION</c> (eso exige réplica, §8); solo
    /// se registra la observación para revisión humana.
    /// </summary>
    private static List<string> SystemicSignals(string primaryMetric, List<MetricResult> armedMandatory, PairedBalanceHarness.PairedResult run)
    {
        var signals = new List<string>();
        foreach (var metric in armedMandatory)
        {
            if (string.Equals(metric.Name, primaryMetric, StringComparison.Ordinal))
            {
                continue;
            }

            var armedValues = run.ArmedMatches.Select(m => PrimaryMetricPerMatch.Value(metric.Name, m)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            var controlValues = run.ControlMatches.Select(m => PrimaryMetricPerMatch.Value(metric.Name, m)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (armedValues.Count < 2 || controlValues.Count < 2)
            {
                continue; // sin traducción por-partido conocida (PrimaryMetricPerMatch) — no se inventa una
            }

            double delta = armedValues.Average() - controlValues.Average();
            double varianceArmed = BalancePowerCheck.SampleVariance(armedValues);
            double varianceControl = BalancePowerCheck.SampleVariance(controlValues);
            if (BalancePowerCheck.HasSufficientPower(delta, varianceArmed, armedValues.Count, varianceControl, controlValues.Count))
            {
                signals.Add($"{metric.Name}: delta={delta:F3} distinguible del ruido, sin vía causal declarada para esta categoría (single-seed, sin replicar)");
            }
        }

        return signals;
    }

    private static BalanceRegistryEntry ToRegistryEntry(ScreeningResult result) => new(
        PerkId: result.PerkId,
        ProtocolVersion: "18-2026-09-19",
        Timestamp: DateTime.UtcNow.ToString("o"),
        State: result.FinalState,
        Category: "",
        Readiness: "",
        PrimaryMetric: "",
        ValuesTried: result.TuningInfo is null ? Array.Empty<double>() : new[] { result.TuningInfo.CurrentValue },
        Candidates: Array.Empty<BalanceCandidateRecord>(),
        ReasonForChange: null,
        ReasonForOutcome: result.Reason);

    private static ScreeningResult FromRegistryEntry(BalanceRegistryEntry entry) => new(
        entry.PerkId, entry.State, entry.ReasonForOutcome ?? "(reanudado desde checkpoint, sin motivo registrado)",
        null, null, null, Array.Empty<string>(), Array.Empty<string>(), null,
        new ScreeningCost(0, 0, 0, 0, "resumed_from_checkpoint"), Array.Empty<string>());
}
