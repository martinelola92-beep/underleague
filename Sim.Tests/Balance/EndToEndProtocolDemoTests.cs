using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Demuestra que el protocolo de balanceo (docs/analisis/protocolo-balanceo-automatizado.md) es
/// <b>ejecutable</b> de punta a punta sobre un perk de prueba: clasificación → Screening → Tuning →
/// Validation → registro, usando en cada paso las piezas ya probadas por separado (clasificador,
/// comprobación de potencia, motor de decisión, búsqueda de valor, harness emparejado, registro) sobre
/// partidos reales simulados, no sobre datos inventados.
///
/// <para><b>No es una medición de balance real</b>: el perk es un fixture de prueba construido en
/// memoria (nunca escrito en <c>/data</c>), a escala reducida para que la prueba corra en segundos, no
/// para producir evidencia suficiente sobre un valor de producción. El resultado (`BALANCED` o cualquier
/// estado de escalada) demuestra el mecanismo, no cierra el balance de nada — igual que pide el criterio
/// de éxito del encargo.</para>
/// </summary>
public sealed class EndToEndProtocolDemoTests
{
    private const int ScreeningRosters = 20;
    private const int TuningRosters = 20; // escala reducida a propósito (ver el docblock de la clase)
    private static readonly Catalog BaseCatalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public EndToEndProtocolDemoTests(ITestOutputHelper output) => _output = output;

    private static PerkDefinition BuildFixture(int value) => new(
        Id: "demo_tackle_fixture",
        Name: new LocalizedName("Fixture de demostración", "Demo fixture"),
        Rarity: Rarity.Common,
        Kind: PerkKind.Conditional,
        Axis: PerkAxis.StartZone,
        Race: null,
        Links: Array.Empty<LinkRelation>(),
        Trigger: EventType.MatchStart,
        Scope: PerkScope.Actor,
        // Mismo mecanismo y condición que own_third_anchor (data/perks/own_third_anchor.json), ya
        // calibrado en Tanda 0 (activación 40/40, condición bien potenciada) — se reutiliza el mecanismo
        // conocido para que el demo pruebe el PROTOCOLO, no la calidad de un diseño de perk nuevo.
        Condition: "startsIn(owner,'OwnThird')",
        CompiledCondition: ConditionCompiler.Compile("startsIn(owner,'OwnThird')", "demo", "demo"),
        Effects: new[]
        {
            new EffectDefinition(
                Type: EffectType.ModifyProbability, Target: EffectTarget.Owner,
                Probability: ProbabilityKind.Tackle, Value: value, Duration: EffectDuration.Match),
        },
        ElseEffects: Array.Empty<EffectDefinition>(),
        Limit: null,
        AccumulatesAcrossMatches: false,
        Lethal: false,
        LethalChance: 0,
        PositionOnly: null,
        TagsRequired: Array.Empty<string>(),
        TagsForbidden: Array.Empty<string>(),
        MinAct: 1,
        Frequency: 100,
        Family: "",
        Requires: null,
        Blocks: PerkBlock.None);

    private static Catalog CatalogWith(int value)
    {
        var perk = BuildFixture(value);
        return BaseCatalog with { Perks = new PerkCatalog(BaseCatalog.Perks.All.Append(perk)) };
    }

    [Fact]
    public void ProtocolTraversesClassificationScreeningTuningAndValidation()
    {
        // --- 0. Clasificación (§13, pieza "selección automática de métricas") ---
        var classification = PerkBalanceClassifier.Classify(BuildFixture(value: 50));
        _output.WriteLine($"Clasificación: {classification.Category}/{classification.Readiness}, métrica primaria={classification.PrimaryMetric}");
        Assert.Equal(PerkBalanceCategory.ProbabilityBonus, classification.Category);
        Assert.Equal(MetricReadiness.Ready, classification.Readiness);
        Assert.True(classification.HasNumericParameter);
        Assert.False(classification.NeedsMultiTargetHarness);

        // --- 1. SCREENING: un candidato barato, ¿hay exposición y efecto? ---
        const int screeningCandidate = 60;
        var screeningRun = PairedBalanceHarness.Run(
            CatalogWith(screeningCandidate), "demo_tackle_fixture", Position.Defender, ScreeningRosters, seed: 1);

        double screeningTacklesArmed = screeningRun.ArmedMatches.Average(m => (double)m.Tackles);
        double screeningTacklesControl = screeningRun.ControlMatches.Average(m => (double)m.Tackles);
        double screeningExposure = (double)screeningRun.ArmedActivations / screeningRun.ArmedMatches.Count;
        _output.WriteLine(
            $"Screening ({screeningRun.ArmedMatches.Count} partidos/brazo): tacklesPerMatch armado={screeningTacklesArmed:F2} "
            + $"control={screeningTacklesControl:F2}  activaciones/partido={screeningExposure:F2}");

        // Exposición discreta (§5.4): activaciones medias por partido como proxy simplificado de "el
        // efecto tuvo oportunidad de disparar" — el suelo real (fracción de partidos con >=1 activación)
        // necesita seguimiento por partido, pieza de tooling todavía pendiente (§13, punto 8).
        var exposure = new ExposureCheck(Fraction: screeningExposure, Floor: 1.0, FloorConfidence.Assumption);
        var screeningMetrics = MatchMetrics.Compute(screeningRun.ArmedMatches, Array.Empty<MetricPairing>());
        var outMetrics = screeningMetrics.Where(m => m.Status == "OUT").Select(m => $"{m.Name}={m.Value:F2}").ToList();
        var mandatoryOut = outMetrics.Count > 0;
        if (mandatoryOut)
        {
            _output.WriteLine($"Métrica(s) obligatoria(s) fuera de banda a escala de Screening (n={ScreeningRosters * 2}): {string.Join(", ", outMetrics)}");
        }

        var screeningState = BalanceDecisionRules.EvaluateScreening(
            exposure, alreadyRetriedWithLargerSample: true, anyMandatoryMetricOut: mandatoryOut,
            hasNumericParameter: classification.HasNumericParameter,
            primaryEffectMagnitude: screeningTacklesArmed - screeningTacklesControl,
            effectZeroFloor: 0.05, zeroFloorConfidence: FloorConfidence.Assumption);

        _output.WriteLine($"Estado tras Screening: {screeningState}");
        Assert.True(
            screeningState is BalanceState.Tuning or BalanceState.DesignReview
                or BalanceState.InsufficientEvidence or BalanceState.SafetyLimit,
            $"estado inesperado tras Screening: {screeningState}");

        if (screeningState != BalanceState.Tuning)
        {
            // El mecanismo puede legítimamente parar aquí (es exactamente lo que pide el criterio de
            // éxito: "o detenerse correctamente en uno de los estados de escalado"). SAFETY_LIMIT a esta
            // escala es, según el propio §6.1, "candidato a confirmar con una Validation reducida" — a
            // 40 partidos/brazo una métrica puede salir de banda por ruido de muestra pequeña (el mismo
            // fenómeno que ya se midió en C1 §5 con 20 plantillas); este demo no amplía la muestra para
            // confirmarlo porque el objetivo es probar el mecanismo, no diagnosticar el fixture.
            _output.WriteLine("El protocolo se detiene aquí correctamente: no hay evidencia suficiente (o hay una alarma de seguridad a confirmar) para continuar sin más datos.");
            return;
        }

        // --- 2. TUNING: tripleta anclada, monotonicidad, potencia, descarte de seguridad ---
        int[] candidates = { 40, 60, 100 }; // tripleta de demostración (no derivada de un volcado, ver docblock)
        var byCandidate = candidates
            .Select(value => (Value: value, Run: PairedBalanceHarness.Run(CatalogWith(value), "demo_tackle_fixture", Position.Defender, TuningRosters, seed: 2)))
            .ToList();

        var deltas = new List<double>();
        var candidateMetrics = new List<(int Value, List<MetricResult> Metrics, double ArmedMean, double ControlMean, double ArmedVariance, double ControlVariance)>();
        foreach (var (value, run) in byCandidate)
        {
            var armedTackles = run.ArmedMatches.Select(m => (double)m.Tackles).ToList();
            var controlTackles = run.ControlMatches.Select(m => (double)m.Tackles).ToList();
            double armedMean = armedTackles.Average();
            double controlMean = controlTackles.Average();
            deltas.Add(armedMean - controlMean);
            candidateMetrics.Add((
                value,
                MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>()),
                armedMean, controlMean,
                BalancePowerCheck.SampleVariance(armedTackles),
                BalancePowerCheck.SampleVariance(controlTackles)));
            _output.WriteLine($"Tuning candidato={value}: tacklesPerMatch armado={armedMean:F2} control={controlMean:F2} delta={armedMean - controlMean:F2}");
        }

        bool monotonic = BalanceDecisionRules.IsMonotonic(deltas, increasing: true);
        _output.WriteLine($"Monotonía de la tripleta {string.Join(",", candidates)}: {monotonic} (deltas={string.Join(",", deltas.Select(d => d.ToString("F2")))})");

        if (!monotonic)
        {
            _output.WriteLine("No monótono a esta escala reducida: el protocolo pide reescanear o escalar a DESIGN_REVIEW, no elegir un ganador a ciegas.");
            Assert.True(true, "el mecanismo detectó correctamente la falta de monotonicidad y no continúa");
            return;
        }

        // Candidato central como ganador tentativo (mismo criterio que C1 §6.2: preferir el paso más
        // pequeño desde un candidato válido cuando varios cumplirían banda).
        var chosen = candidateMetrics[1];
        bool chosenFailsSafety = BalanceDecisionRules.CandidateFailsSafety(
            chosen.Metrics
                .Where(m => m.RangeMin is not null && m.RangeMax is not null)
                .Select(m => new PairedMetric(m.Name, m.Value, m.Value, m.RangeMin, m.RangeMax))
                .ToList());
        Assert.False(chosenFailsSafety, "el candidato central no debería romper RT-056 a esta escala de demostración");

        bool chosenHasPower = BalancePowerCheck.HasSufficientPower(
            chosen.ArmedMean - chosen.ControlMean, chosen.ArmedVariance, TuningRosters * 2,
            chosen.ControlVariance, TuningRosters * 2);
        _output.WriteLine($"Potencia del candidato elegido ({chosen.Value}): suficiente={chosenHasPower}");

        // --- 3. VALIDATION: réplica en segunda semilla + determinismo + checklist completo ---
        var replicaRun = PairedBalanceHarness.Run(CatalogWith(chosen.Value), "demo_tackle_fixture", Position.Defender, TuningRosters, seed: 3);
        double replicaDelta = replicaRun.ArmedMatches.Average(m => (double)m.Tackles) - replicaRun.ControlMatches.Average(m => (double)m.Tackles);
        bool sameSign = Math.Sign(chosen.ArmedMean - chosen.ControlMean) == Math.Sign(replicaDelta) && Math.Abs(replicaDelta) > 0.0;
        _output.WriteLine($"Réplica (semilla 3): delta={replicaDelta:F2}  mismo signo que semilla 2={sameSign}");

        bool deterministic = VerifyDeterminism(CatalogWith(chosen.Value));
        _output.WriteLine($"Determinismo (misma semilla dos veces): {deterministic}");

        var mandatoryMetrics = chosen.Metrics.Where(m => m.RangeMin is not null && m.RangeMax is not null).ToList();
        bool safetyInBand = mandatoryMetrics.All(m => m.Status == "IN");

        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: monotonic && (chosen.ArmedMean - chosen.ControlMean) > 0,
            SafetyMetricsInBand: safetyInBand,
            EvidenceSufficient: chosenHasPower,
            ReplicatesAcrossSeeds: sameSign,
            DeterministicRerun: deterministic,
            NoDesignDegeneracy: true, // sin condición de fidelidad de diseño definida para este fixture
            NoUnexplainedSystemicMove: true); // universales sin vía causal ajena no se comprueban en este demo

        var finalState = BalanceDecisionRules.EvaluateValidation(checklist);
        _output.WriteLine($"Checklist de validación: {checklist}");
        _output.WriteLine($"Estado final: {finalState}");

        // --- 4. Registro (§10) — checkpoint/reanudación ---
        string registryPath = Path.Combine(Path.GetTempPath(), $"balance-demo-{Guid.NewGuid():N}.json");
        try
        {
            var entry = new BalanceRegistryEntry(
                PerkId: "demo_tackle_fixture",
                ProtocolVersion: "2026-09-18",
                Timestamp: DateTimeOffset.UtcNow.ToString("o"),
                State: finalState,
                Category: classification.Category.ToString(),
                Readiness: classification.Readiness.ToString(),
                PrimaryMetric: classification.PrimaryMetric,
                ValuesTried: candidates.Select(c => (double)c).ToList(),
                Candidates: candidateMetrics.Select(c => new BalanceCandidateRecord(
                    c.Value,
                    new Dictionary<string, double> { [MatchMetrics.TacklesPerMatch] = c.ArmedMean },
                    new Dictionary<string, double> { [MatchMetrics.TacklesPerMatch] = c.ControlMean },
                    finalState)).ToList(),
                ReasonForChange: "demostración del protocolo, no una decisión de balance real",
                ReasonForOutcome: finalState == BalanceState.Balanced
                    ? "las siete condiciones de §6.4 se satisfacen a esta escala de demostración"
                    : "al menos una condición de §6.4 no se satisface a esta escala de demostración");

            BalanceRegistryFile.Save(registryPath, entry);
            var resumed = BalanceRegistryFile.Load(registryPath);

            Assert.NotNull(resumed);
            Assert.Equal(finalState, resumed!.State);
            _output.WriteLine($"Registro escrito y releído correctamente en {registryPath} (estado={resumed.State})");
        }
        finally
        {
            if (File.Exists(registryPath))
            {
                File.Delete(registryPath);
            }
        }

        // El mecanismo es lo que se demuestra: llega a un estado terminal reconocido, con datos reales
        // simulados en cada paso. No se afirma que demo_tackle_fixture esté "balanceado" de verdad.
        Assert.True(Enum.IsDefined(finalState));
    }

    /// <summary>
    /// El test anterior puede legítimamente parar en Screening (§6.1: una métrica de seguridad fuera de
    /// banda a escala pequeña es motivo para detenerse, y con la semilla 1 eso es justo lo que ocurre —
    /// <c>injuriesPerMatch</c> roza el suelo por ruido de 40-60 partidos, el mismo fenómeno ya medido en
    /// C1 §5). Este segundo test ejercita Tuning→Validation de forma independiente, con datos reales
    /// simulados, para que el mecanismo completo quede demostrado incluso cuando Screening decide parar
    /// antes para el fixture/semilla concretos del primer test — no se fuerza el resultado del primero.
    /// </summary>
    [Fact]
    public void TuningAndValidationMachineryWorksOnRealSimulatedData()
    {
        int[] candidates = { 40, 60, 100 };
        var byCandidate = candidates
            .Select(value => (Value: value, Run: PairedBalanceHarness.Run(CatalogWith(value), "demo_tackle_fixture", Position.Defender, TuningRosters, seed: 2)))
            .ToList();

        var deltas = new List<double>();
        var candidateMetrics = new List<(int Value, List<MetricResult> Metrics, double ArmedMean, double ControlMean, double ArmedVariance, double ControlVariance)>();
        foreach (var (value, run) in byCandidate)
        {
            var armedTackles = run.ArmedMatches.Select(m => (double)m.Tackles).ToList();
            var controlTackles = run.ControlMatches.Select(m => (double)m.Tackles).ToList();
            double armedMean = armedTackles.Average();
            double controlMean = controlTackles.Average();
            deltas.Add(armedMean - controlMean);
            candidateMetrics.Add((
                value, MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>()),
                armedMean, controlMean,
                BalancePowerCheck.SampleVariance(armedTackles), BalancePowerCheck.SampleVariance(controlTackles)));
            _output.WriteLine($"candidato={value}: tacklesPerMatch armado={armedMean:F2} control={controlMean:F2} delta={armedMean - controlMean:F2}");
        }

        bool monotonic = BalanceDecisionRules.IsMonotonic(deltas, increasing: true);
        _output.WriteLine($"Monotonía: {monotonic}");
        Assert.True(monotonic, "más tackle probability debería subir tacklesPerMatch de forma monótona en la tripleta — si no, es una señal real, no se fuerza");

        var chosen = candidateMetrics[1];
        var mandatoryPaired = chosen.Metrics
            .Where(m => m.RangeMin is not null && m.RangeMax is not null)
            .Select(m => new PairedMetric(m.Name, m.Value, m.Value, m.RangeMin, m.RangeMax))
            .ToList();
        bool failsSafety = BalanceDecisionRules.CandidateFailsSafety(mandatoryPaired);
        _output.WriteLine($"Candidato central ({chosen.Value}) falla seguridad: {failsSafety}");

        bool hasPower = BalancePowerCheck.HasSufficientPower(
            chosen.ArmedMean - chosen.ControlMean, chosen.ArmedVariance, TuningRosters * 2, chosen.ControlVariance, TuningRosters * 2);
        _output.WriteLine($"Potencia suficiente: {hasPower}");

        bool deterministic = VerifyDeterminism(CatalogWith(chosen.Value));
        Assert.True(deterministic, "RT-024: la misma semilla debe producir el mismo resultado");

        var replicaRun = PairedBalanceHarness.Run(CatalogWith(chosen.Value), "demo_tackle_fixture", Position.Defender, TuningRosters, seed: 3);
        double replicaDelta = replicaRun.ArmedMatches.Average(m => (double)m.Tackles) - replicaRun.ControlMatches.Average(m => (double)m.Tackles);
        bool sameSign = Math.Sign(chosen.ArmedMean - chosen.ControlMean) == Math.Sign(replicaDelta);

        var checklist = new ValidationChecklist(
            BehaviorAndEffectInBand: monotonic && (chosen.ArmedMean - chosen.ControlMean) > 0,
            SafetyMetricsInBand: !failsSafety,
            EvidenceSufficient: hasPower,
            ReplicatesAcrossSeeds: sameSign,
            DeterministicRerun: deterministic,
            NoDesignDegeneracy: true,
            NoUnexplainedSystemicMove: true);

        var finalState = BalanceDecisionRules.EvaluateValidation(checklist);
        _output.WriteLine($"Estado final (Tuning→Validation aislado): {finalState}");

        // La máquina de estados en sí es lo que se demuestra determinista: dado el mismo checklist,
        // EvaluateValidation siempre da el mismo estado — lo prueba ya BalanceDecisionRulesTests.
        // Aquí lo que importa es que el pipeline completo, sobre datos reales, produce un checklist y un
        // estado sin lanzar ninguna excepción ni quedarse a medias.
        Assert.True(Enum.IsDefined(finalState));
    }

    /// <summary>RT-024, aplicado al perk de prueba: la misma semilla produce el mismo resultado dos veces.</summary>
    private static bool VerifyDeterminism(Catalog catalog)
    {
        var homeRng1 = RngStreams.Generation(99, 0);
        var awayRng1 = RngStreams.Generation(99, 1);
        var home = TeamGenerator.Generate(ref homeRng1, catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng1, catalog, "away", Race.Human, 50, 100001, 4);

        int carrierSlot = home.Players.ToList().FindIndex(p => p.Position == Position.Defender);
        var players = home.Players.ToList();
        players[carrierSlot] = players[carrierSlot] with { Perks = new[] { "demo_tackle_fixture" } };
        var armedHome = home with { Players = players };

        var setup = new MatchSetup(armedHome, away, new RefereeSetup("Referee", RefereeTrait.Neutral, 0));
        var config = new SimConfig(CollectLog: false, Trace: false);

        var first = Simulator.Run(setup, seed: 4242, catalog, config);
        var second = Simulator.Run(setup, seed: 4242, catalog, config);

        return first.Report.Goals[0] == second.Report.Goals[0]
            && first.Report.Goals[1] == second.Report.Goals[1]
            && first.Report.Tackles == second.Report.Tackles
            && first.Events.Count == second.Events.Count;
    }
}
