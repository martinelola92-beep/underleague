using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// El contrato de <c>READY_FOR_SCREENING</c> (§16, punto 5 del encargo del 18 sep 2026): para TODO perk
/// clasificado así, comprueba de forma EJECUTABLE (no solo "la función existe") que los diez componentes
/// están presentes. Si falta alguno, `READY_FOR_SCREENING` es un estado inválido y este test debe fallar
/// — es lo que convierte la etiqueta del clasificador en un contrato verificable.
/// </summary>
public sealed class ReadyContractTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;
    public ReadyContractTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> ReadyForScreeningPerkIds()
    {
        var entries = PerkAudit.AuditCatalog(Catalog.Perks.All, Catalog);
        return entries
            .Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening)
            .Select(e => new object[] { e.PerkId });
    }

    [Theory]
    [MemberData(nameof(ReadyForScreeningPerkIds))]
    public void EveryReadyForScreeningPerkSatisfiesAllTenContractComponents(string perkId)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var result = ReadyContract.Verify(Catalog, perk);
        if (!result.Satisfied)
        {
            _output.WriteLine($"{perkId}: FALTAN {result.Missing.Count} componente(s):");
            foreach (var m in result.Missing)
            {
                _output.WriteLine($"  - {m}");
            }
        }

        Assert.True(result.Satisfied, $"{perkId} está READY_FOR_SCREENING pero no cumple el contrato: {string.Join("; ", result.Missing)}");
    }

    [Fact]
    public void ContractCatchesAKnownBadCaseIfSomeoneMisclassifiesIt()
    {
        // Prueba negativa (§16: "el test debe fallar" si falta algo) — sobre un perk real que NO está
        // Ready (mob_instigator: CancelEvent FOUL, DesignReview/MissingBand), el contrato debe detectar
        // que su harness no es el correcto (SelectHarness no da SingleMatch para un DesignReview).
        var perk = Catalog.Perks.All.Single(p => p.Id == "mob_instigator");
        var entry = PerkAudit.Audit(perk, Catalog);

        Assert.NotEqual(AuditReadiness.ReadyForScreening, entry.FinalReadiness);
        Assert.NotEqual(HarnessKind.SingleMatch, HarnessSelector.SelectHarness(entry));
    }
}
