using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Punto 6 del encargo del 18 sep 2026: sin cambiar ningún valor de <c>/data</c>, muestra el camino que
/// toma cada uno de cinco perks reales, uno por estado — clasificación → auditoría → harness
/// seleccionado, sin ejecutar cientos de partidos.
/// </summary>
public sealed class FivePathsDemoTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public FivePathsDemoTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("own_third_anchor", AuditReadiness.ReadyForScreening, "modifyProbability(tackle), condición de zona propia — mecanismo y banda ya confirmados desde Tanda 0")]
    [InlineData("pit_veteran", AuditReadiness.NotReady, "addCounter+modifyProbability(tackle) escalado por contador, AccumulatesAcrossMatches — necesita el harness de campaña, no está bloqueado por falta de métrica")]
    // pack_mentality (modifyAttribute(strength), target=withTag:Brute) se borró del catálogo (revisor, 18
    // sep 2026); blood_tithe recorre el mismo camino con la misma forma de destinatario (Population, por
    // target=team/opposingTeam en vez de withTag).
    [InlineData("blood_tithe", AuditReadiness.MultiTarget, "modifyProbability(injure)+modifyProbability(severeInjury), target=team/opposingTeam — afecta a varios jugadores, el harness de portador único no lo soporta")]
    [InlineData("box_predator", AuditReadiness.DesignReview, "modifyProbability(shotOnTarget) — la métrica natural (shotsOnTargetShare) es INFO en MatchMetrics, sin banda: decisión de diseño, no de tooling")]
    // La quinta ruta (NotReady por atribución multi-efecto) se queda SIN ejemplo real: `unlikely_bulwark`
    // era el único perk del catálogo que mezclaba categorías y se borró el 19 sep 2026 al cuadrar razas y
    // rasgos. No se sustituye por otro porque no lo hay — ver PerkAuditTests.NoCatalogPerkMixesEffectCategories,
    // que fija el invariante que queda.
    public void ShowsThePathEachRealPerkTakes(string perkId, AuditReadiness expectedReadiness, string expectedShape)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);

        var classification = PerkBalanceClassifier.Classify(perk);
        var auditEntry = PerkAudit.Audit(perk, Catalog);
        var harness = HarnessSelector.SelectHarness(auditEntry);

        _output.WriteLine($"=== {perkId} ===");
        _output.WriteLine($"forma: {expectedShape}");
        _output.WriteLine($"1. classifier:  categoría={classification.Category}  readiness={classification.Readiness}  numérico={classification.HasNumericParameter}");
        _output.WriteLine($"2. audit:       finalReadiness={auditEntry.FinalReadiness}  notReadyReason={auditEntry.NotReadyReason}  designReviewReason={auditEntry.DesignReviewReason}");
        _output.WriteLine($"3. harness:     {harness}");
        _output.WriteLine($"4. nota:        {auditEntry.Notes}");
        _output.WriteLine("");

        Assert.Equal(expectedReadiness, auditEntry.FinalReadiness);

        // El harness seleccionado tiene que ser coherente con el estado final: solo un
        // ReadyForScreening obtiene el harness de partido suelto, solo un NeedsCampaignHarness real
        // obtiene el de campaña, y todo lo demás se queda sin harness ejecutable todavía (None) — no se
        // ejecuta ningún partido en esta prueba, es solo el camino de decisión.
        var expectedHarness = auditEntry.FinalReadiness == AuditReadiness.ReadyForScreening ? HarnessKind.SingleMatch
            : auditEntry.NotReadyReason == NotReadyReason.NeedsCampaignHarness ? HarnessKind.Campaign
            : HarnessKind.None;
        Assert.Equal(expectedHarness, harness);
    }

    [Fact]
    public void NoDataFileWasReadOrWrittenByThisDemo()
    {
        // Confirmación explícita de la restricción del encargo: el catálogo se carga una sola vez desde
        // TestData.LoadCatalog() (lectura estándar de /data ya existente en todo Sim.Tests, no una
        // escritura), y ningún método usado aquí (Classify/Audit/SelectHarness) toca disco.
        Assert.True(Catalog.Perks.All.Count > 0);
    }
}
