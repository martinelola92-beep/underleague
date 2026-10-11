using System.Text.RegularExpressions;
using Underleague.Sim.Data;
using Underleague.Sim.Perks;
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

    [Fact]
    public void NoPerkDescriptionShowsAnInternalIdentifier()
    {
        var leaks = new List<string>();
        foreach (var language in Catalog.Localization.Languages)
        {
            var templates = Catalog.Localization.Get(language);
            foreach (var perk in Catalog.Perks.All)
            {
                string text = DescriptionGenerator.Describe(perk, templates, Catalog.Perks, RunEffects);
                if (CamelCase().Match(text) is { Success: true } match)
                {
                    leaks.Add($"{perk.Id} ({language}): {match.Value}");
                }
            }
        }

        Assert.True(leaks.Count == 0, string.Join(" | ", leaks));
    }

    [Theory]
    [InlineData("box_office", "Al terminar el partido, el club cobra 3 de oro.")]
    [InlineData("local_idol", "Al marcar, el club cobra 5 de oro.")]
    [InlineData("ad_machine", "Cuando lesiona a un rival, el club cobra 8 de oro.")]
    [InlineData("life_insurance", "En una muerte, el club cobra 8 de oro.")]
    [InlineData("loan", "Se revende por el 100 % de su valor, sin la rebaja habitual.")]
    [InlineData("inheritance", "En una muerte, su vinculado hereda el 20 % de cada atributo del muerto, hasta +2 por atributo.")]
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
