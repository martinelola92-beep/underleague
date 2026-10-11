using System.Text.RegularExpressions;
using Underleague.Sim.Data;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// El catálogo real descrito como lo ve el jugador en Recompensa, Mercado e Informe: con lo que los perks pagan a la run
/// (<see cref="PerkRunEffects"/>). Captura `recompensa` del 11 oct 2026: «En una muerte, +1 al contador
/// lifeInsuranceClaims» y «Cuando le lesionan…» en tres perks que se activan cuando el portador lesiona a un rival.
/// </summary>
public sealed partial class CatalogDescriptionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly PerkRunEffects RunEffects =
        PerkRunEffectsBuilder.Build(Catalog.Perks, EconomyLoader.FromJson(TestData.LoadAllFiles()));

    [GeneratedRegex(@"\b[a-z]+[A-Z][A-Za-z]*\b")]
    private static partial Regex CamelCase();

    [GeneratedRegex(@"\b[a-z0-9]+_[a-z0-9_]+\b")]
    private static partial Regex SnakeCase();

    [GeneratedRegex(@"\b[A-Z]{2,}(_[A-Z]+)*\b")]
    private static partial Regex UpperSnake();

    [GeneratedRegex(@"\b[a-z]+\b")]
    private static partial Regex Word();

    /// <summary>
    /// Ningún identificador interno en lo que lee el jugador, perks y consumibles, en los dos idiomas. Hermano de BY-A: el
    /// detector sólo veía camelCase (los contadores), y un id en snake_case (perks, objetos), un evento en UPPER_SNAKE o un
    /// id de una palabra no lo habría visto. Los ids de una palabra sólo se buscan en español, donde un id inglés tal cual
    /// es una fuga; en inglés pueden ser una palabra normal. Medido el 11 oct 2026: ninguna fuga con los cuatro patrones;
    /// sin <see cref="PerkRunEffects"/> salen las doce de los contadores, lo que comprueba que el detector ve.
    /// </summary>
    [Fact]
    public void NoPerkDescriptionShowsAnInternalIdentifier()
    {
        var consumables = ConsumableLoader.FromJson(TestData.LoadAllFiles());
        var perkIds = Catalog.Perks.All.Select(p => p.Id).ToHashSet();
        var leaks = new List<string>();
        foreach (var language in Catalog.Localization.Languages)
        {
            var templates = Catalog.Localization.Get(language);
            var texts = Catalog.Perks.All.Select(p => (p.Id, DescriptionGenerator.Describe(p, templates, Catalog.Perks, RunEffects)))
                .Concat(consumables.All.Select(c => (c.Id, DescriptionGenerator.DescribeEffects(c.Effects, templates))));
            foreach (var (id, text) in texts)
            {
                foreach (var pattern in new[] { CamelCase(), SnakeCase(), UpperSnake() })
                {
                    leaks.AddRange(pattern.Matches(text).Select(m => $"{id} ({language}): {m.Value}"));
                }

                if (language == "es")
                {
                    leaks.AddRange(Word().Matches(text).Where(m => perkIds.Contains(m.Value)).Select(m => $"{id} ({language}): {m.Value}"));
                }
            }
        }

        Assert.True(leaks.Count == 0, string.Join(" | ", leaks));
    }

    /// <summary>El detector ve lo que dice ver (Regla J): cada patrón contra un caso conocido.</summary>
    [Theory]
    [InlineData("+1 al contador boxOfficeMatches")]
    [InlineData("activa life_insurance")]
    [InlineData("al recibir INJURY")]
    [InlineData("en MATCH_START")]
    public void TheLeakDetectorSeesEachKindOfIdentifier(string text) =>
        Assert.True(CamelCase().IsMatch(text) || SnakeCase().IsMatch(text) || UpperSnake().IsMatch(text), text);

    [Theory]
    [InlineData("box_office", "Al terminar el partido, el club cobra 3 de oro.")]
    [InlineData("local_idol", "Al marcar, el club cobra 5 de oro.")]
    [InlineData("ad_machine", "Cuando lesiona a un rival, el club cobra 8 de oro.")]
    [InlineData("life_insurance", "Si muere, el club cobra 8 de oro.")]
    [InlineData("loan", "Al venderlo, ni lesionado ni muerto se cobra por menos del 100 % de su precio.")]
    [InlineData("inheritance", "Si muere, el compañero de su columna hereda el 20 % de cada atributo suyo, hasta +2 por atributo.")]
    public void ARunEffectPerkSaysWhatTheRunGets(string perkId, string expected)
    {
        string text = DescriptionGenerator.Describe(Catalog.Perks.Get(perkId), Catalog.Localization.Get("es"), Catalog.Perks, RunEffects);
        Assert.StartsWith(expected, text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutRunEffectsTheDescriptionIsTheOldOne()
    {
        string text = DescriptionGenerator.Describe(Catalog.Perks.Get("box_office"), Catalog.Localization.Get("es"), Catalog.Perks);
        Assert.Contains("boxOfficeMatches", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInjuryPerkWithOpponentScopeReadsAsTheOneWhoInjures()
    {
        // MatchEngine emite INJURY con la víctima de actor y el causante de rival: con scope opponent el portador es quien
        // lesiona (_doc de gathering_thirst, iron_price y ad_machine).
        foreach (var id in new[] { "gathering_thirst", "iron_price", "ad_machine" })
        {
            Assert.Equal(PerkScope.Opponent, Catalog.Perks.Get(id).Scope);
            string text = DescriptionGenerator.Describe(Catalog.Perks.Get(id), Catalog.Localization.Get("es"), Catalog.Perks, RunEffects);
            Assert.StartsWith("Cuando lesiona a un rival", text, StringComparison.Ordinal);
        }
    }
}
