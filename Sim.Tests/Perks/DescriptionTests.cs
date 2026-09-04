using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Descripciones generadas desde el efecto (RT-035). El texto exacto es parte del contrato: si alguien
/// cambia un valor del perk o una plantilla, el test cae y la descripción no puede quedarse obsoleta,
/// que es justo lo que RT-035 quiere hacer imposible.
/// </summary>
public sealed class DescriptionTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void BloodlustInSpanish() => Assert.Equal(
        "al entrar, si el jugador es Bruto y si el criterio es menor que 0: "
            + "el jugador +3 de fuerza durante la jugada (máximo 2 por partido)",
        DescriptionGenerator.Describe(Catalog.Perks.Get("bloodlust"), "es", Catalog));

    [Fact]
    public void BloodlustInEnglish() => Assert.Equal(
        "on a tackle, if the player is Brute and if the referee bias is less than 0: "
            + "the player +3 strength for the play (max 2 per match)",
        DescriptionGenerator.Describe(Catalog.Perks.Get("bloodlust"), "en", Catalog));

    [Fact]
    public void VeteranInSpanish() => Assert.Equal(
        "al empezar el partido: el portador +1 de fuerza por cada partido (máximo 8) durante el partido, "
            + "+1 al contador partido",
        DescriptionGenerator.Describe(Catalog.Perks.Get("veteran"), "es", Catalog));

    [Fact]
    public void VeteranInEnglish() => Assert.Equal(
        "when the match starts: the holder +1 strength per match (max 8) for the match, "
            + "+1 to the match counter",
        DescriptionGenerator.Describe(Catalog.Perks.Get("veteran"), "en", Catalog));

    [Fact]
    public void EveryCatalogPerkIsDescribableInEveryLanguage()
    {
        Assert.Contains("es", Catalog.Localization.Languages);
        Assert.Contains("en", Catalog.Localization.Languages);

        foreach (var perk in Catalog.Perks.All)
        {
            foreach (var language in Catalog.Localization.Languages)
            {
                string text = DescriptionGenerator.Describe(perk, language, Catalog);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{perk.Id} no tiene descripción en {language}");
                Assert.DoesNotContain('{', text);
            }
        }
    }

    [Fact]
    public void ElseEffectsAndProbabilitiesAreDescribed()
    {
        var perk = TestPerks.Load("showboat", TestPerks.Json(
            "showboat",
            "DRIBBLE_ATTEMPTED",
            """[{ "type": "modifyProbability", "target": "actor", "probability": "dribble", "value": 1500, "duration": "play" }]""",
            condition: "hasTag(owner, 'Fine')",
            elseEffects: """[{ "type": "modifyProbability", "target": "actor", "probability": "dribble", "value": -1500, "duration": "play" }]"""));

        var templates = Catalog.Localization.Get("es");
        Assert.Equal(
            "al encarar, si el portador es Fino: el jugador: probabilidad de regate +15%; "
                + "si no, el jugador: probabilidad de regate -15%",
            DescriptionGenerator.Describe(perk, templates));
    }

    [Fact]
    public void CancelEventNamesTheTriggeringEvent()
    {
        var perk = TestPerks.Load("lucky_charm", TestPerks.Json(
            "lucky_charm",
            "INJURY",
            """[{ "type": "cancelEvent" }]""",
            scope: "target",
            rarity: "legendary",
            kind: "ruleBreaker",
            limit: """{ "per": "match", "times": 1 }"""));

        Assert.Equal(
            "en una lesión: anula la lesión (máximo 1 por partido)",
            DescriptionGenerator.Describe(perk, Catalog.Localization.Get("es")));
        Assert.Equal(
            "on an injury: cancels the injury (max 1 per match)",
            DescriptionGenerator.Describe(perk, Catalog.Localization.Get("en")));
    }

    [Fact]
    public void CounterDivisorUsesItsOwnTemplate()
    {
        var perk = TestPerks.Load("bookworm", TestPerks.Json(
            "bookworm",
            "MATCH_START",
            """
            [{ "type": "modifyAttribute", "target": "owner", "attribute": "technique",
               "valuePerCounter": 1, "counter": "passes", "counterDivisor": 25, "maxValue": 6, "duration": "match" }]
            """));

        Assert.Equal(
            "al empezar el partido: el portador +1 de técnica por cada 25 de pase (máximo 6) durante el partido",
            DescriptionGenerator.Describe(perk, Catalog.Localization.Get("es")));
    }
}
