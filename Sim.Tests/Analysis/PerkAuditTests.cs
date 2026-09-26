using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Auditoría estática (§16 de docs/analisis/protocolo-balanceo-automatizado.md) de los perks reales:
/// sin simular ningún partido, solo consulta de datos ya cargados. Fija el resultado agregado como
/// regresión (si cambia, es porque el catálogo o el clasificador cambiaron, y hay que mirar por qué) y
/// comprueba sistemáticamente (no a mano) que el clasificador nunca marca `READY` un caso estructuralmente
/// no medible (§16, paso 6).
/// </summary>
public sealed class PerkAuditTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly IReadOnlyList<PerkAuditEntry> Entries = PerkAudit.AuditCatalog(Catalog.Perks.All, Catalog);

    private readonly ITestOutputHelper _output;
    public PerkAuditTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PrintAggregateReport()
    {
        var summary = PerkAudit.Summarize(Entries);

        _output.WriteLine($"Total perks: {summary.Total}");
        foreach (var (readiness, count) in summary.ByReadiness.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"{readiness}: {count}");
        }

        _output.WriteLine("");
        _output.WriteLine("NOT_READY por motivo:");
        foreach (var (reason, count) in summary.NotReadyByReason.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"  {reason}: {count}");
        }

        _output.WriteLine("");
        _output.WriteLine("DESIGN_REVIEW por motivo:");
        foreach (var (reason, count) in summary.DesignReviewByReason.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"  {reason}: {count}");
        }

        _output.WriteLine("");
        _output.WriteLine("perk | effectTypes | targetShapes | readiness | blockingReason | primaryMetric");
        foreach (var e in Entries.OrderBy(e => e.PerkId, StringComparer.Ordinal))
        {
            string blocking = e.NotReadyReason != NotReadyReason.None ? e.NotReadyReason.ToString()
                : e.DesignReviewReason != DesignReviewReason.None ? e.DesignReviewReason.ToString()
                : "-";
            _output.WriteLine($"{e.PerkId} | {string.Join("+", e.EffectTypes)} | {string.Join("+", e.TargetShapes)} | {e.FinalReadiness} | {blocking} | {e.PrimaryMetric}");
        }
    }

    [Fact]
    public void AggregateCountsAreFixedAsRegression()
    {
        // Fija el resultado del 18 sep 2026 sobre el catálogo real: si esto falla, o el catálogo cambió
        // (nuevo perk, nuevo EffectType en uso) o el clasificador cambió — cualquiera de las dos merece
        // revisar por qué antes de aceptar el nuevo número, no ajustar el test para que pase.
        var summary = PerkAudit.Summarize(Entries);

        // Sube cuando el catálogo crece a propósito: al cuadrar razas y rasgos (19 sep 2026) pasó de 94 a
        // 105 (+12 nuevos, -1 unlikely_bulwark). Baja cuando se borra contenido aprobado por el revisor:
        // el 18 sep 2026 se borraron pack_mentality (MultiTarget, target=withTag:Brute) y fine_orchestra
        // (MultiTarget, target=team+opposingTeam), y shadow_marker (ReadyForScreening, target=actor) — de
        // 105 a 102, -1 en ReadyForScreening y -2 en MultiTarget, DesignReview/NotReady/RunLevel sin
        // cambio. Si cambia sin que nadie haya tocado /data/perks, es una regresión de la auditoría, y
        // entonces se investiga antes de tocar el número.
        // 110 desde el 26 sep 2026: ocho perks nuevos escritos con el vocabulario que ya existía
        // (`point_blank`, `nutmeg`, `ankle_bite`, `bull_rush`, `silver_tongue`, `eyed_coward`,
        // `never_tracks_back`, `shouting_wall`). Se reparten +4 ReadyForScreening, +3 MultiTarget y +1
        // DesignReview; los cinco cubos siguen sumando el total. 109 el mismo día: `ankle_bite` se retira
        // porque perjudicaba a quien lo llevaba (ADR 0087), y sale de MultiTarget (27 -> 26).
        Assert.Equal(109, summary.Total);
        // 25 desde el experimento de legibilidad del 25 sep 2026: `duelist` y `own_third_anchor` cambian
        // su cuota de entrada por un DERRIBO (setState sobre el rival) y salen del lote — el arnés de
        // cribado mide multiplicadores sobre el portador y no sabe atribuir un acto sobre un rival. No
        // desaparecen: pasan a MultiTarget, que sube de 22 a 24. Los cinco cubos suman 102.
        Assert.Equal(29, summary.ByReadiness[AuditReadiness.ReadyForScreening]);
        Assert.Equal(26, summary.ByReadiness[AuditReadiness.MultiTarget]);
        Assert.Equal(21, summary.ByReadiness[AuditReadiness.DesignReview]);
        Assert.Equal(27, summary.ByReadiness[AuditReadiness.NotReady]);
        Assert.Equal(6, summary.ByReadiness[AuditReadiness.RunLevel]);
    }

    // ------------------------------------------------------------------------------------------------
    // §16 paso 6: comprobación de consistencia SISTEMÁTICA, no una lista manual de perks concretos.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void NoPerkIsReadyForScreeningWithoutAKnownBandedOrBehavioralMetric()
    {
        foreach (var e in Entries.Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening))
        {
            bool usesUnbandedNumericMetric = MatchMetrics.InfoOnlyMetricNames.Contains(e.PrimaryMetric);
            Assert.False(
                usesUnbandedNumericMetric,
                $"{e.PerkId} está READY_FOR_SCREENING pero su métrica primaria ({e.PrimaryMetric}) es INFO, sin banda");
        }
    }

    [Fact]
    public void NoPerkIsReadyForScreeningWithMultiTargetEffects()
    {
        foreach (var e in Entries.Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening))
        {
            Assert.True(
                e.TargetShapes.All(s => s == EffectTargetShape.SingleOwner),
                $"{e.PerkId} está READY_FOR_SCREENING pero tiene un destinatario no soportado ({string.Join(",", e.TargetShapes)})");
        }
    }

    [Fact]
    public void NoPerkIsReadyForScreeningWithUnresolvedCrossCategoryEffects()
    {
        foreach (var e in Entries.Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening))
        {
            Assert.False(
                e.HasCrossCategoryEffects,
                $"{e.PerkId} está READY_FOR_SCREENING pero mezcla efectos de categorías distintas sin resolver cuál se ajusta");
        }
    }

    [Fact]
    public void NoPerkIsReadyForScreeningWhenItNeedsTheCampaignHarness()
    {
        foreach (var e in Entries.Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening))
        {
            Assert.NotEqual(MetricReadiness.NeedsCampaignHarness, e.PrimaryMetricReadiness);
        }
    }

    [Fact]
    public void EveryNotReadyEntryHasAConcreteReason()
    {
        // El encargo pide explícitamente: "evitar un sistema que simplemente diga NOT_READY sin explicar
        // qué falta". Ningún NOT_READY se queda con NotReadyReason.None.
        foreach (var e in Entries.Where(e => e.FinalReadiness == AuditReadiness.NotReady))
        {
            Assert.NotEqual(NotReadyReason.None, e.NotReadyReason);
        }
    }

    [Fact]
    public void EveryDesignReviewEntryHasAConcreteReason()
    {
        foreach (var e in Entries.Where(e => e.FinalReadiness == AuditReadiness.DesignReview))
        {
            Assert.NotEqual(DesignReviewReason.None, e.DesignReviewReason);
        }
    }

    [Fact]
    public void ReasonsAreMutuallyExclusivePerEntry()
    {
        // Un perk no puede tener a la vez un motivo de NOT_READY y uno de DESIGN_REVIEW (§16: "los
        // motivos deben ser mutuamente comprensibles... no solaparse").
        foreach (var e in Entries)
        {
            bool hasNotReady = e.NotReadyReason != NotReadyReason.None;
            bool hasDesignReview = e.DesignReviewReason != DesignReviewReason.None;
            Assert.False(hasNotReady && hasDesignReview, $"{e.PerkId} tiene motivo de NOT_READY y de DESIGN_REVIEW a la vez");
        }
    }

    [Fact]
    public void LimitIsNeverTreatedAsInsufficientExposureByItself()
    {
        // §5.4/§16: un Limit sobre un disparador ya raro por diseño (INJURY/GOAL/DEATH/FOUL) no debe
        // producir ninguna conclusión automática de exposición insuficiente — la auditoría estática solo
        // anota la categoría de frecuencia, nunca decide NOT_READY/INSUFFICIENT_* solo por tener Limit.
        foreach (var e in Entries.Where(e => e.HasLimit))
        {
            Assert.NotNull(e.LimitNote);
            bool blockedSolelyByLimit = (e.FinalReadiness == AuditReadiness.NotReady || e.FinalReadiness == AuditReadiness.DesignReview)
                && e.NotReadyReason == NotReadyReason.None && e.DesignReviewReason == DesignReviewReason.None;
            Assert.False(blockedSolelyByLimit, $"{e.PerkId} está bloqueado sin motivo concreto, solo por tener Limit");
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Casos concretos que motivaron la corrección del clasificador (§16) — regresión dirigida.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void PoacherInstinctNeedsCampaignHarnessNotPlainProbabilityBonus()
    {
        // addCounter + modifyProbability(shotOnTarget) escalado por contador, AccumulatesAcrossMatches.
        var entry = Entries.Single(e => e.PerkId == "poacher_instinct");

        Assert.Equal(AuditReadiness.NotReady, entry.FinalReadiness);
        Assert.Equal(NotReadyReason.NeedsCampaignHarness, entry.NotReadyReason);
    }

    [Fact]
    public void NoCatalogPerkMixesEffectCategories()
    {
        // `unlikely_bulwark` (modifyProbability(tackle) + modifyLeash) era el ÚNICO perk real que
        // ejercitaba la ruta de atribución multi-efecto, y se borró el 19 sep 2026 al cuadrar razas y
        // rasgos: violaba la regla de una sola condición de raza/rasgo (race=Elf + tagsRequired Bulwark).
        // La ruta del clasificador sigue existiendo y sin consumidores, como la de maestros (BB-R).
        //
        // Lo que se fija aquí es el invariante que queda: hoy NINGÚN perk del catálogo mezcla categorías.
        // Si aparece uno, este test lo dice y hay que volver a comprobar que se clasifica como NotReady
        // por MultiEffectAttribution, no que pase en silencio.
        var crossCategory = Entries.Where(e => e.HasCrossCategoryEffects).Select(e => e.PerkId).ToList();

        Assert.Empty(crossCategory);
    }

    [Fact]
    public void DeepRunSameCategoryMultiEffectStaysReady()
    {
        // shiftHome + modifyZoneShape: misma categoría (Geometry), sin ambigüedad de atribución.
        var entry = Entries.Single(e => e.PerkId == "deep_run");

        Assert.Equal(AuditReadiness.ReadyForScreening, entry.FinalReadiness);
        Assert.True(entry.IsMultiEffect);
        Assert.False(entry.HasCrossCategoryEffects);
    }

    [Theory]
    [InlineData("bodyguard")]      // target=linked (población de vinculados)
    [InlineData("blood_tithe")]    // target=team/opposingTeam — sustituto de pack_mentality (target=withTag:Brute, borrado del catálogo)
    [InlineData("pivot_duo")]      // target=linked
    [InlineData("dirty_play")]     // target=opponent
    public void PerksWithNonOwnerTargetsAreMultiTargetNotSilentlyReady(string perkId)
    {
        var entry = Entries.Single(e => e.PerkId == perkId);
        Assert.Equal(AuditReadiness.MultiTarget, entry.FinalReadiness);
    }

    [Fact]
    public void StrengthAttributePerksAreDesignReviewUnlessAnotherIssueTakesPriority()
    {
        // Strength era AmbiguousPrimaryMetric (§16) en CUATRO perks reales: brute_boots, comeback_spirit,
        // scar_veteran y pack_mentality. Dos tenían ADEMÁS otro bloqueo que se reportaba primero
        // (pack_mentality: target multi-jugador; scar_veteran: AccumulatesAcrossMatches+UsesCounter,
        // necesita el harness de campaña) — un perk solo aparece en UN bucket final (§16: "no solaparse").
        // pack_mentality se borró del catálogo (revisor, 18 sep 2026) sin sustituto de Strength+MultiTarget
        // (ninguno de los perks nuevos usa modifyAttribute(strength) con destinatario Population): de los
        // cuatro Strength originales quedan tres, y ninguno de los que queda tiene ya ese segundo bloqueo
        // de tipo MultiTarget. Aquí solo se exige que brute_boots y comeback_spirit —sin ningún otro
        // bloqueo— caigan limpiamente en DESIGN_REVIEW/ambiguo; scar_veteran sigue teniendo el suyo propio.
        foreach (var perkId in new[] { "brute_boots", "comeback_spirit" })
        {
            var entry = Entries.Single(e => e.PerkId == perkId);
            Assert.Equal(AuditReadiness.DesignReview, entry.FinalReadiness);
            Assert.Equal(DesignReviewReason.AmbiguousPrimaryMetric, entry.DesignReviewReason);
        }

        Assert.Equal(AuditReadiness.NotReady, Entries.Single(e => e.PerkId == "scar_veteran").FinalReadiness);
        Assert.Equal(NotReadyReason.NeedsCampaignHarness, Entries.Single(e => e.PerkId == "scar_veteran").NotReadyReason);
    }

    [Fact]
    public void RunLevelPerksAreNotCountedAsNotReady()
    {
        // Perks de contador puro (loan, box_office, ad_machine, local_idol, inheritance, life_insurance):
        // RUN_LEVEL no es un fallo, es otro instrumento (§3/§9).
        var runLevelIds = new[] { "loan", "box_office", "ad_machine", "local_idol", "inheritance", "life_insurance" };
        foreach (var id in runLevelIds)
        {
            var entry = Entries.Single(e => e.PerkId == id);
            Assert.Equal(AuditReadiness.RunLevel, entry.FinalReadiness);
        }
    }
}
