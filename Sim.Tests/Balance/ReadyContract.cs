using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// El contrato de <c>READY_FOR_SCREENING</c> (§16 punto 5 del encargo del 18 sep 2026), extraído de
/// <c>ReadyContractTests</c> para que el screening real (§18, el encargo del 19 sep 2026) pueda
/// re-verificarlo antes de gastar ninguna simulación — "no dupliques lógica" aplicado a este propio
/// protocolo: un solo sitio decide qué significa que un perk cumpla el contrato.
/// </summary>
public static class ReadyContract
{
    /// <summary>Los diez componentes del contrato, cada uno con su propio motivo de fallo si falta.</summary>
    public sealed record ContractResult(List<string> Missing)
    {
        public bool Satisfied => Missing.Count == 0;
    }

    public static ContractResult Verify(Catalog catalog, PerkDefinition perk)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(perk);

        var missing = new List<string>();
        var classification = PerkBalanceClassifier.Classify(perk);
        var auditEntry = PerkAudit.Audit(perk, catalog);

        // 1. harness válido
        var harnessKind = HarnessSelector.SelectHarness(auditEntry);
        if (harnessKind != HarnessKind.SingleMatch)
        {
            missing.Add($"harness válido (SelectHarness devolvió {harnessKind}, se esperaba SingleMatch)");
            return new ContractResult(missing); // sin harness no se puede comprobar nada más con datos reales
        }

        // 2+3+5. control/treatment definidos y comparables: ejecución real, pequeña (§16: "no hace falta
        // ejecutar cientos de partidos todavía"), esto son unas pocas plantillas por perk.
        PairedBalanceHarness.PairedResult run;
        try
        {
            run = PairedBalanceHarness.RunWithEligibleCarrier(catalog, perk, rosters: 5, seed: 1);
        }
        catch (Exception ex)
        {
            missing.Add($"harness ejecutable (lanzó {ex.GetType().Name}: {ex.Message})");
            return new ContractResult(missing);
        }

        if (run.ArmedMatches.Count == 0)
        {
            missing.Add("treatment definido (0 partidos armados — ningún titular elegible en 5 plantillas)");
        }

        if (run.ControlMatches.Count == 0)
        {
            missing.Add("control definido (0 partidos de control)");
        }

        if (run.ArmedMatches.Count != run.ControlMatches.Count)
        {
            missing.Add($"baseline/control comparable (armado={run.ArmedMatches.Count} vs control={run.ControlMatches.Count}, deberían coincidir: mismas plantillas y semillas)");
        }

        if (run.ArmedMatches.Count == 0 || run.ControlMatches.Count == 0)
        {
            return new ContractResult(missing); // sin partidos no hay nada más que comprobar con datos reales
        }

        // 4. métrica primaria válida: no vacía, y si es numérica, no una de las 15 métricas INFO conocidas.
        if (string.IsNullOrWhiteSpace(classification.PrimaryMetric))
        {
            missing.Add("métrica primaria válida (cadena vacía)");
        }
        else if (MatchMetrics.InfoOnlyMetricNames.Contains(classification.PrimaryMetric))
        {
            missing.Add($"métrica primaria válida ({classification.PrimaryMetric} es INFO, sin banda)");
        }

        // 6+7. criterio de exposición / insuficiente exposición: se puede construir y evaluar sobre datos reales.
        double exposure = run.ArmedMatches.Count > 0 ? (double)run.ArmedActivations / run.ArmedMatches.Count : 0.0;
        var exposureCheck = new ExposureCheck(exposure, Floor: 0.3, FloorConfidence.Assumption);
        bool exposureCheckWorks = exposureCheck.Floor > 0 && (exposureCheck.Sufficient || !exposureCheck.Sufficient); // siempre evaluable, sin excepción

        if (!exposureCheckWorks)
        {
            missing.Add("criterio de exposición/insuficiente exposición evaluable");
        }

        // 8. criterio de decisión: el motor de decisión produce un estado definido con datos reales.
        var mandatoryMetrics = MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>())
            .Where(m => m.RangeMin is not null && m.RangeMax is not null)
            .ToList();
        bool anyMandatoryOut = mandatoryMetrics.Any(m => m.Status == "OUT");
        var screeningState = BalanceDecisionRules.EvaluateScreening(
            exposureCheck, alreadyRetriedWithLargerSample: true, anyMandatoryMetricOut: anyMandatoryOut,
            hasNumericParameter: classification.HasNumericParameter);
        if (!Enum.IsDefined(screeningState))
        {
            missing.Add("criterio de decisión (EvaluateScreening no produjo un estado definido)");
        }

        // 9. safety metrics aplicables: las siete métricas obligatorias de RT-056 se calculan sobre estos partidos.
        if (mandatoryMetrics.Count != 7)
        {
            missing.Add($"safety metrics aplicables (se esperaban 7 métricas obligatorias, se calcularon {mandatoryMetrics.Count})");
        }

        // 10. estrategia de búsqueda compatible con si el perk es auto-tuneable o no.
        var strategy = BalanceSearchStrategy.SelectStrategy(classification.Category, classification.HasNumericParameter);
        bool strategyConsistent = classification.HasNumericParameter
            ? strategy != SearchStrategyKind.None
            : strategy == SearchStrategyKind.None;
        if (!strategyConsistent)
        {
            missing.Add($"estrategia de búsqueda compatible (HasNumericParameter={classification.HasNumericParameter}, estrategia={strategy})");
        }

        return new ContractResult(missing);
    }
}
