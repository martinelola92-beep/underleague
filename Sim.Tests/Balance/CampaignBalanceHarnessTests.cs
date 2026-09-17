using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Demuestra el punto 1 del encargo del 18 sep 2026: <c>perk real → classifier → campaign harness
/// correcto → control/treatment → métricas → decision engine</c>, con un perk real
/// <see cref="MetricReadiness.NeedsCampaignHarness"/> — sin reimplementar <c>PerkValueRunner</c>, sin
/// cambiar ningún valor de <c>/data</c>.
/// </summary>
public sealed class CampaignBalanceHarnessTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;
    public CampaignBalanceHarnessTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void RealCampaignPerkFlowsFromClassifierToDecisionEngine()
    {
        const string perkId = "pit_veteran"; // modifyProbability(tackle)+addCounter, AccumulatesAcrossMatches, trigger=TACKLE (frecuente)
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);

        // 1. classifier
        var classification = PerkBalanceClassifier.Classify(perk);
        Assert.Equal(MetricReadiness.NeedsCampaignHarness, classification.Readiness);
        Assert.Equal(PerkBalanceCategory.AccumulatedStateBonus, classification.Category);

        // 2. selección de harness a partir de la auditoría (no una decisión manual)
        var auditEntry = PerkAudit.Audit(perk, Catalog);
        var harness = HarnessSelector.SelectHarness(auditEntry);
        Assert.Equal(HarnessKind.Campaign, harness);
        _output.WriteLine($"{perkId}: classifier={classification.Category}/{classification.Readiness} -> harness={harness}");

        // 3. campaign harness correcto (Balance/PerkValueRunner.cs, sin reimplementar) -> control/treatment
        var row = CampaignBalanceHarness.Run(Catalog, perkId, seed: 1, rosters: 20);
        Assert.NotNull(row);
        _output.WriteLine(
            $"campaña: {row!.Value.Matches} partidos, armado {row.Value.Wins}W ({row.Value.WinRate:F1}%), "
            + $"control {row.Value.ControlWins}W ({row.Value.ControlWinRate:F1}%), activaciones={row.Value.Diagnostics.Activations}");

        Assert.True(row.Value.Matches > 0, "la campaña no jugó ningún partido");
        Assert.True(row.Value.Diagnostics.Activations > 0, "el efecto (modifyProbability tackle) nunca se disparó — TACKLE debería ser frecuente");

        // 4. métricas -> observación utilizable por power-check/decision engine
        var (deltaWinRate, varianceArmed, varianceControl, matches) = CampaignBalanceHarness.ToWinRateObservation(row.Value);
        _output.WriteLine($"observación: deltaWinRate={deltaWinRate:F4} varianceArmed={varianceArmed:F4} varianceControl={varianceControl:F4} matches={matches}");

        bool hasPower = BalancePowerCheck.HasSufficientPower(deltaWinRate, varianceArmed, matches, varianceControl, matches);
        _output.WriteLine($"potencia suficiente a esta escala (20 plantillas): {hasPower}");

        // 5. decision engine: aunque la potencia no alcance a esta escala reducida (no se piden cientos
        // de partidos todavía), el resultado debe ser un estado reconocido, nunca una excepción ni un
        // ACCEPT/BALANCED implícito.
        var mandatoryMetrics = Array.Empty<PairedMetric>(); // la campaña mide victorias, no RT-056 todavía
        bool failsSafety = BalanceDecisionRules.CandidateFailsSafety(mandatoryMetrics);
        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: deltaWinRate > 0,
            SafetyMetricsInBand: !failsSafety,
            EvidenceSufficient: hasPower,
            ReplicatesAcrossSeeds: false, // no se ha medido una segunda semilla en este demo (§7: no se balancea todavía)
            DeterministicRerun: true, // ya verificado a nivel de motor (RT-024), no se repite aquí
            NoDesignDegeneracy: true,
            NoUnexplainedSystemicMove: true);
        var finalState = BalanceDecisionRules.EvaluateValidation(checklist);
        _output.WriteLine($"estado final (demo, no una decisión de balance real): {finalState}");

        Assert.True(Enum.IsDefined(finalState));
    }

    [Fact]
    public void SingleMatchHarnessWouldMisrepresentACampaignScaledPerk()
    {
        // Comprobación negativa: si alguien intentara medir pit_veteran con el harness de partido suelto
        // (contador siempre a cero), el resultado NO representaría el mecanismo real (§16: "un partido
        // suelto con el contador a cero no representa la magnitud típica a mitad/final de run"). Esta
        // prueba no ejecuta esa medición equivocada — solo confirma que el selector nunca la propondría.
        var perk = Catalog.Perks.All.Single(p => p.Id == "pit_veteran");
        var entry = PerkAudit.Audit(perk, Catalog);

        Assert.NotEqual(HarnessKind.SingleMatch, HarnessSelector.SelectHarness(entry));
    }
}
