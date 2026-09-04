using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Carga de <c>data/perks/*.json</c> (RT-032, RT-033). Un dato inválido es un error explícito al cargar,
/// nunca un comportamiento raro en partido: aquí se comprueba cada regla de validación de §2.
/// </summary>
public sealed class PerkLoaderTests
{
    private const string OneEffect = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 3, "duration": "match" }]""";

    [Fact]
    public void LoadsRealCatalog()
    {
        var catalog = TestData.LoadCatalog();
        Assert.NotNull(catalog.Perks.Find("bloodlust"));
        Assert.NotNull(catalog.Perks.Find("veteran"));
    }

    [Fact]
    public void CatalogIsOrderedByIdOrdinal()
    {
        var catalog = TestData.LoadCatalog();
        var ids = catalog.Perks.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.OrderBy(i => i, StringComparer.Ordinal).ToList(), ids);
    }

    [Fact]
    public void BloodlustMatchesTheSpecifiedFormat()
    {
        var perk = TestData.LoadCatalog().Perks.Get("bloodlust");

        Assert.Equal("Sed de sangre", perk.Name.Es);
        Assert.Equal("Bloodlust", perk.Name.En);
        Assert.Equal(Rarity.Rare, perk.Rarity);
        Assert.Equal(PerkKind.Conditional, perk.Kind);
        Assert.Equal(EventType.Tackle, perk.Trigger);
        Assert.Equal(PerkScope.Actor, perk.Scope);
        Assert.False(perk.AccumulatesAcrossMatches);
        Assert.False(perk.Lethal);
        Assert.Null(perk.PositionOnly);

        var effect = Assert.Single(perk.Effects);
        Assert.Equal(EffectType.ModifyAttribute, effect.Type);
        Assert.Equal(EffectTarget.Actor, effect.Target);
        Assert.Equal(AttributeKind.Strength, effect.Attribute);
        Assert.Equal(3, effect.Value);
        Assert.Equal(EffectDuration.Play, effect.Duration);

        Assert.Equal(new LimitDefinition(LimitScope.Match, 2), perk.Limit);
    }

    [Fact]
    public void VeteranAccumulatesAcrossMatches()
    {
        var perk = TestData.LoadCatalog().Perks.Get("veteran");

        Assert.True(perk.AccumulatesAcrossMatches);
        Assert.Equal(EventType.MatchStart, perk.Trigger);
        Assert.Equal(2, perk.Effects.Count);

        Assert.True(perk.Effects[0].UsesCounter);
        Assert.Equal("matches", perk.Effects[0].Counter);
        Assert.Equal(1, perk.Effects[0].ValuePerCounter);
        Assert.Equal(8, perk.Effects[0].MaxValue);
        Assert.Equal(1, perk.Effects[0].CounterDivisor);

        Assert.Equal(EffectType.AddCounter, perk.Effects[1].Type);
        Assert.Equal(EffectDuration.Instant, perk.Effects[1].Duration);
    }

    [Fact]
    public void ParsesEveryTargetForm()
    {
        var perk = TestPerks.Load("target_forms", TestPerks.Json(
            "target_forms",
            "MATCH_START",
            """
            [
              { "type": "modifyAttribute", "target": "withTag:Brute", "attribute": "strength", "value": 1, "duration": "match" },
              { "type": "modifyAttribute", "target": "adjacentWithTag:Fine", "attribute": "speed", "value": 1, "duration": "match" },
              { "type": "modifyLeash", "target": "adjacent", "value": 1, "duration": "play" },
              { "type": "modifyProbability", "target": "team", "probability": "save", "value": 100, "duration": "match" }
            ]
            """));

        Assert.Equal(EffectTarget.WithTag, perk.Effects[0].Target);
        Assert.Equal("Brute", perk.Effects[0].TargetTag);
        Assert.Equal(EffectTarget.AdjacentWithTag, perk.Effects[1].Target);
        Assert.Equal("Fine", perk.Effects[1].TargetTag);
        Assert.Equal(EffectTarget.Adjacent, perk.Effects[2].Target);
        Assert.Equal(ProbabilityKind.Save, perk.Effects[3].Probability);
    }

    [Theory]
    [InlineData("\"trigger\": \"TACKLE\"", "\"trigger\": \"NOT_AN_EVENT\"", "disparador")]
    [InlineData("\"rarity\": \"common\"", "\"rarity\": \"mythic\"", "rareza")]
    [InlineData("\"kind\": \"filler\"", "\"kind\": \"weird\"", "tipo de perk")]
    public void UnknownEnumValueIsALoadError(string original, string replacement, string expected)
    {
        string json = TestPerks.Json("broken", "TACKLE", OneEffect).Replace(original, replacement, StringComparison.Ordinal);
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("broken", json));
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelEventOnlyWithFoulCardOrInjury()
    {
        const string Cancel = """[{ "type": "cancelEvent" }]""";
        Assert.NotNull(TestPerks.Load("saved", TestPerks.Json("saved", "CARD", Cancel)));

        var ex = Assert.Throws<DataException>(() => TestPerks.Load("bad", TestPerks.Json("bad", "GOAL", Cancel)));
        Assert.Contains("cancelEvent", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModifyAttributeNeedsARealDuration()
    {
        string json = TestPerks.Json(
            "bad",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 3, "duration": "instant" }]""");
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("bad", json));
        Assert.Contains("duración", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCounterMustBeInstant()
    {
        string json = TestPerks.Json(
            "bad",
            "MATCH_END",
            """[{ "type": "addCounter", "counter": "matches", "value": 1, "duration": "match" }]""");
        Assert.Throws<DataException>(() => TestPerks.Load("bad", json));
    }

    [Fact]
    public void SetStateOnlyKnocksDownOpponents()
    {
        const string Friendly = """[{ "type": "setState", "target": "team", "state": "KnockedDown", "ticks": 10 }]""";
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("bad", TestPerks.Json("bad", "TACKLE", Friendly)));
        Assert.Contains("rivales", ex.Message, StringComparison.Ordinal);

        const string Enemy = """[{ "type": "setState", "target": "opponent", "state": "KnockedDown", "ticks": 10 }]""";
        Assert.NotNull(TestPerks.Load("ok", TestPerks.Json("ok", "TACKLE", Enemy)));
    }

    [Fact]
    public void LethalIsRejectedInPhaseOne()
    {
        string json = TestPerks.Json("bad", "TACKLE", OneEffect).Replace("\"lethal\": false", "\"lethal\": true", StringComparison.Ordinal);
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("bad", json));
        Assert.Contains("lethal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptionFieldIsRejected()
    {
        // RT-035: la descripción se genera desde el efecto, así que no existe texto escrito a mano.
        string json = TestPerks.Json("bad", "TACKLE", OneEffect)
            .Replace("\"lethal\": false", "\"description\": \"texto a mano\", \"lethal\": false", StringComparison.Ordinal);
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("bad", json));
        Assert.Contains("description", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateIdIsALoadError()
    {
        var ex = Assert.Throws<DataException>(() => TestPerks.CatalogWith(
            ("copy_of_veteran", TestPerks.Json("veteran", "MATCH_START", OneEffect))));
        Assert.Contains("repetido", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValuePerCounterNeedsACounter()
    {
        string json = TestPerks.Json(
            "bad",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "valuePerCounter": 1, "duration": "match" }]""");
        Assert.Throws<DataException>(() => TestPerks.Load("bad", json));
    }

    [Fact]
    public void ElseEffectsAreLoaded()
    {
        var perk = TestPerks.Load("lone", TestPerks.Json(
            "lone",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 8, "duration": "match" }]""",
            condition: "adjacentCount(owner, 'Brute') == 0",
            elseEffects: """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": -6, "duration": "match" }]"""));

        Assert.Single(perk.Effects);
        Assert.Equal(-6, Assert.Single(perk.ElseEffects).Value);
    }
}
