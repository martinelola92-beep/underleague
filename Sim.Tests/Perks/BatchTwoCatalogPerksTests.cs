using Underleague.Sim.Data;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Los 20 perks de la tanda 2 del catálogo unificado (`docs/analisis/perks-catalogo-unificado.md` §8.2):
/// que existen, que cargan, que se describen solos en los dos idiomas (RT-035) y —lo que de verdad
/// protege al catálogo cuando crece— que la distribución de RF-069 sigue en banda con 94 perks.
/// </summary>
public sealed class BatchTwoCatalogPerksTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static readonly string[] BatchTwo =
    {
        "high_line", "deep_pivot", "deep_run", "shadow", "cannon", "iron_price", "kamikaze",
        "gathering_thirst", "survivor", "bloodhound", "bodyguard", "blood_scent", "grudge",
        "double_shot", "charge", "steamroller", "life_insurance", "inheritance", "free_man", "loan",
    };

    [Fact]
    public void TheTwentyExistAndDescribeThemselvesInBothLanguages()
    {
        foreach (var id in BatchTwo)
        {
            var perk = Catalog.Perks.Find(id);
            Assert.NotNull(perk);
            Assert.NotEmpty(perk!.Effects);

            // RT-035: la descripción se genera del efecto. Si falta una plantilla, esto lo dice aquí y no
            // en una pantalla con un "{algo}" sin sustituir.
            foreach (var language in new[] { "es", "en" })
            {
                string text = DescriptionGenerator.Describe(perk, language, Catalog);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{id} no se describe en {language}");
                Assert.DoesNotContain("{", text, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// RF-069: 60/30/10 con ±8 puntos de tolerancia. Es la comprobación que importa de esta tanda: el
    /// catálogo pasa de 61 a 94 perks en dos paquetes, y sin esto la distribución se deforma sin que nadie
    /// lo note hasta que una puerta estadística lo grite mucho más tarde.
    /// </summary>
    [Fact]
    public void TheCatalogueStillMeetsTheTargetDistribution()
    {
        var all = Catalog.Perks.All;
        int total = all.Count;
        Assert.True(total >= 94, $"el catálogo tiene {total} perks y las dos tandas deberían dejarlo en 94 o más");

        int filler = all.Count(p => p.Kind == PerkKind.Filler);
        int conditional = all.Count(p => p.Kind == PerkKind.Conditional);
        int ruleBreaker = all.Count(p => p.Kind == PerkKind.RuleBreaker);

        Assert.InRange(100.0 * filler / total, 52.0, 68.0);
        Assert.InRange(100.0 * conditional / total, 22.0, 38.0);
        Assert.InRange(100.0 * ruleBreaker / total, 2.0, 18.0);
    }

    /// <summary>
    /// Los tres perks de economía tienen que llamarse EXACTAMENTE como los nombran las tablas de
    /// `data/economy/`: ahí la clave es el id del perk, y un id que no coincide no falla, simplemente hace
    /// que el perk no pague nada. Un fallo silencioso es justo lo que RT-032 no quiere.
    /// </summary>
    [Fact]
    public void TheThreeEconomyPerksMatchTheIdsTheirTariffTablesName()
    {
        foreach (var id in new[] { "life_insurance", "inheritance", "loan" })
        {
            Assert.NotNull(Catalog.Perks.Find(id));
        }
    }

    /// <summary>ADR 0046: el cupo de letales es 3-5 y esta tanda no añade ninguno.</summary>
    [Fact]
    public void BatchTwoAddsNoLethalPerk()
    {
        foreach (var id in BatchTwo)
        {
            Assert.False(Catalog.Perks.Find(id)!.Lethal, $"{id} es letal y el cupo ya estaba lleno");
        }
    }
}
