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

    /// <summary>
    /// Las cinco habilidades raciales (RF-031b, ADR 0026) son parte de <c>data/perks/</c> y se cargan
    /// siempre: sin ellas ninguna raza puede resolver su campo <c>ability</c>.
    /// </summary>
    [Fact]
    public void LoadsTheRacialAbilitiesFromData()
    {
        var catalog = TestPerks.CatalogWith();
        foreach (var (id, race) in new[]
        {
            ("quick_learner", Race.Human), ("hot_blooded", Race.Orc), ("elf_touch", Race.Elf),
            ("roots", Race.Dwarf), ("numb", Race.Undead),
        })
        {
            var perk = catalog.Perks.Get(id);
            Assert.Equal(race, perk.Race);
            Assert.Equal(id, catalog.Race(race).Ability);
        }
    }

    [Fact]
    public void CatalogIsOrderedByIdOrdinal()
    {
        var catalog = TestPerks.CatalogWith(
            ("zulu", TestPerks.Json("zulu", "MATCH_START", OneEffect)),
            ("alpha", TestPerks.Json("alpha", "MATCH_START", OneEffect)));
        var ids = catalog.Perks.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.OrderBy(i => i, StringComparer.Ordinal).ToList(), ids);
    }

    [Fact]
    public void BloodlustMatchesTheSpecifiedFormat()
    {
        var perk = TestPerks.Load("bloodlust", TestPerks.Json(
            "bloodlust",
            "TACKLE",
            """[{ "type": "modifyAttribute", "target": "actor", "attribute": "strength", "value": 3, "duration": "play" }]""",
            rarity: "rare",
            kind: "conditional",
            condition: "hasTag(actor, 'Brute') && bias() < 0",
            limit: """{ "per": "match", "times": 2 }"""));

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
        var perk = TestPerks.Load("veteran", TestPerks.Json(
            "veteran",
            "MATCH_START",
            """
            [
              { "type": "modifyAttribute", "target": "owner", "attribute": "strength",
                "valuePerCounter": 1, "counter": "matches", "maxValue": 8, "duration": "match" },
              { "type": "addCounter", "counter": "matches", "value": 1 }
            ]
            """,
            axis: "accumulation",
            accumulates: true));

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
              { "type": "modifyProbability", "target": "team", "probability": "save", "value": 10, "duration": "match" }
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
            ("first_copy", TestPerks.Json("twin", "MATCH_START", OneEffect)),
            ("second_copy", TestPerks.Json("twin", "MATCH_START", OneEffect))));
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

    // ---------------------------------------------------------------- formato revisado (§1.4)

    /// <summary>
    /// Los puntos porcentuales del dato se convierten a la base interna de 10.000, y solo en
    /// modifyProbability: los puntos de atributo, las casillas de correa y los ticks son otras unidades.
    /// </summary>
    [Fact]
    public void PercentagePointsBecomeBasePoints()
    {
        var perk = TestPerks.Load("scaled", TestPerks.Json(
            "scaled",
            "MATCH_START",
            """
            [
              { "type": "modifyProbability", "target": "owner", "probability": "pass", "value": 15, "duration": "match" },
              { "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 3, "duration": "match" }
            ]
            """));

        Assert.Equal(1500, perk.Effects[0].Value);
        Assert.Equal(3, perk.Effects[1].Value);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(100)]
    [InlineData(1500)]
    [InlineData(0)]
    public void AProbabilityValueOutsideTheScaleIsALoadError(int value)
    {
        string json = TestPerks.Json(
            "off_scale",
            "MATCH_START",
            $$"""[{ "type": "modifyProbability", "target": "owner", "probability": "pass", "value": {{value}}, "duration": "match" }]""");
        Assert.Throws<DataException>(() => TestPerks.Load("off_scale", json));
    }

    [Theory]
    [InlineData(-50)]
    [InlineData(-5)]
    [InlineData(25)]
    [InlineData(50)]
    public void TheWholeScaleIsAccepted(int value)
    {
        var perk = TestPerks.Load("on_scale", TestPerks.Json(
            "on_scale",
            "MATCH_START",
            $$"""[{ "type": "modifyProbability", "target": "owner", "probability": "pass", "value": {{value}}, "duration": "match" }]"""));
        Assert.Equal(value * 100, Assert.Single(perk.Effects).Value);
    }

    [Fact]
    public void AxisIsRequired()
    {
        string json = """
        {
          "id": "no_axis",
          "name": { "es": "x", "en": "x" },
          "rarity": "common",
          "kind": "filler",
          "trigger": "MATCH_START",
          "effects": [{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 1, "duration": "match" }]
        }
        """;
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("no_axis", json));
        Assert.Contains("axis", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("identity", PerkAxis.Identity)]
    [InlineData("alignment", PerkAxis.Alignment)]
    [InlineData("startZone", PerkAxis.StartZone)]
    [InlineData("proximity", PerkAxis.Proximity)]
    public void AxisIsLoaded(string text, PerkAxis expected) => Assert.Equal(
        expected,
        TestPerks.Load("axis", TestPerks.Json("axis", "MATCH_START", OneEffect, axis: text)).Axis);

    [Fact]
    public void RaceIsNullForUniversalPerksAndSetForExclusiveOnes()
    {
        Assert.Null(TestPerks.Load("universal", TestPerks.Json("universal", "MATCH_START", OneEffect)).Race);
        Assert.Equal(
            Race.Orc,
            TestPerks.Load("only_orc", TestPerks.Json("only_orc", "MATCH_START", OneEffect, race: "Orc")).Race);
    }

    /// <summary>
    /// ADR 0023 / RF-065b: en un club monoraza una condición de especie se cumple siempre o nunca, así que
    /// no es una decisión. El cargador la rechaza en la condición, en las etiquetas y en el objetivo.
    /// </summary>
    [Theory]
    [InlineData("hasTag(owner, 'Orc')")]
    [InlineData("adjacent(owner, 'Elf')")]
    [InlineData("teammatesWithTag(owner, 'Dwarf') > 2")]
    [InlineData("nearAlly(owner, 'Undead', 2)")]
    public void AUniversalPerkCannotAskForTheSpeciesTag(string condition)
    {
        var ex = Assert.Throws<DataException>(() => TestPerks.Load(
            "sneaky", TestPerks.Json("sneaky", "MATCH_START", OneEffect, condition: condition)));
        Assert.Contains("especie", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpeciesTagsAreAlsoRejectedInTagListsAndTargets()
    {
        Assert.Throws<DataException>(() => TestPerks.Load(
            "req", TestPerks.Json("req", "MATCH_START", OneEffect, tagsRequired: """["Orc"]""")));
        Assert.Throws<DataException>(() => TestPerks.Load(
            "forb", TestPerks.Json("forb", "MATCH_START", OneEffect, tagsForbidden: """["Elf"]""")));
        Assert.Throws<DataException>(() => TestPerks.Load("tgt", TestPerks.Json(
            "tgt",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "withTag:Human", "attribute": "strength", "value": 1, "duration": "match" }]""")));
    }

    /// <summary>Un perk exclusivo sí puede: para eso declara su raza.</summary>
    [Fact]
    public void AnExclusivePerkMayAskForItsOwnSpeciesTag()
    {
        var perk = TestPerks.Load("orcish", TestPerks.Json(
            "orcish", "MATCH_START", OneEffect, race: "Orc", condition: "hasTag(owner, 'Orc')"));
        Assert.Equal(Race.Orc, perk.Race);
    }

    [Fact]
    public void LinksAreLoadedInOrderAndRejectRepeats()
    {
        var perk = TestPerks.Load("pair", TestPerks.Json(
            "pair", "MATCH_START", OneEffect, axis: "alignment", links: """["behind", "beside"]"""));
        Assert.Equal(new[] { LinkRelation.Behind, LinkRelation.Beside }, perk.Links);

        Assert.Throws<DataException>(() => TestPerks.Load("twice", TestPerks.Json(
            "twice", "MATCH_START", OneEffect, axis: "alignment", links: """["beside", "beside"]""")));
        Assert.Throws<DataException>(() => TestPerks.Load("weird", TestPerks.Json(
            "weird", "MATCH_START", OneEffect, axis: "alignment", links: """["sideways"]""")));
    }

    [Fact]
    public void ALinkedTargetNeedsDeclaredLinks()
    {
        string effects =
            """[{ "type": "modifyProbability", "target": "linked", "probability": "pass", "value": 10, "duration": "match" }]""";

        var ex = Assert.Throws<DataException>(() => TestPerks.Load(
            "orphan", TestPerks.Json("orphan", "MATCH_START", effects, axis: "alignment")));
        Assert.Contains("links", ex.Message, StringComparison.Ordinal);

        var perk = TestPerks.Load("linked_ok", TestPerks.Json(
            "linked_ok", "MATCH_START", effects, axis: "alignment", links: """["ahead"]"""));
        Assert.Equal(EffectTarget.Linked, Assert.Single(perk.Effects).Target);
    }

    [Fact]
    public void LinkedWithTagCarriesItsTag()
    {
        var perk = TestPerks.Load("linked_tag", TestPerks.Json(
            "linked_tag",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "linkedWithTag:Brute", "attribute": "strength", "value": 1, "duration": "match" }]""",
            axis: "alignment",
            links: """["beside"]"""));

        var effect = Assert.Single(perk.Effects);
        Assert.Equal(EffectTarget.LinkedWithTag, effect.Target);
        Assert.Equal("Brute", effect.TargetTag);
    }

    [Fact]
    public void ImmunityIsLoadedAndOnlyTargetsTheOwnSide()
    {
        var perk = TestPerks.Load("stone", TestPerks.Json(
            "stone", "MATCH_START", """[{ "type": "immunity", "target": "owner", "immunity": "push" }]"""));
        Assert.Equal(ImmunityKind.Push, Assert.Single(perk.Effects).Immunity);

        Assert.Throws<DataException>(() => TestPerks.Load("bad_target", TestPerks.Json(
            "bad_target", "MATCH_START", """[{ "type": "immunity", "target": "opposingTeam", "immunity": "push" }]""")));
        Assert.Throws<DataException>(() => TestPerks.Load("bad_kind", TestPerks.Json(
            "bad_kind", "MATCH_START", """[{ "type": "immunity", "target": "owner", "immunity": "bullets" }]""")));
    }

    [Fact]
    public void ExperienceOnlyLivesOutsideTheMatch()
    {
        var perk = TestPerks.Load("learner", TestPerks.Json(
            "learner", "MATCH_END", """[{ "type": "modifyExperience", "target": "owner", "value": 25, "duration": "run" }]"""));

        // No se convierte a base 10.000: es un porcentaje sobre la experiencia, no una probabilidad.
        Assert.Equal(25, Assert.Single(perk.Effects).Value);

        Assert.Throws<DataException>(() => TestPerks.Load("bad_duration", TestPerks.Json(
            "bad_duration", "MATCH_END", """[{ "type": "modifyExperience", "target": "owner", "value": 25, "duration": "match" }]""")));
        Assert.Throws<DataException>(() => TestPerks.Load("bad_owner", TestPerks.Json(
            "bad_owner", "MATCH_END", """[{ "type": "modifyExperience", "target": "team", "value": 25, "duration": "run" }]""")));
    }

    [Fact]
    public void KnockdownTicksActOnWhoTackles()
    {
        var perk = TestPerks.Load("hot", TestPerks.Json(
            "hot", "MATCH_START", """[{ "type": "modifyKnockdownTicks", "target": "owner", "value": 5, "duration": "match" }]"""));
        Assert.Equal(5, Assert.Single(perk.Effects).Value);

        Assert.Throws<DataException>(() => TestPerks.Load("cold", TestPerks.Json(
            "cold", "MATCH_START", """[{ "type": "modifyKnockdownTicks", "target": "opponent", "value": 5, "duration": "match" }]""")));
    }

    /// <summary>
    /// El pool de una run solo ve los universales y los exclusivos de su raza (ADR 0023): un perk orco no
    /// puede salir como recompensa en una run élfica, donde sería una opción muerta de tres.
    /// </summary>
    [Fact]
    public void TheRunPoolFiltersByRace()
    {
        var catalog = TestPerks.CatalogWith(
            ("universal", TestPerks.Json("universal", "MATCH_START", OneEffect)),
            ("orcish", TestPerks.Json("orcish", "MATCH_START", OneEffect, race: "Orc", rarity: "rare")));

        var orcPool = catalog.Perks.AvailableTo(Race.Orc).Select(p => p.Id).ToList();
        Assert.Contains("universal", orcPool);
        Assert.Contains("orcish", orcPool);
        Assert.Contains("hot_blooded", orcPool);
        Assert.DoesNotContain("elf_touch", orcPool);

        var elfPool = catalog.Perks.AvailableTo(Race.Elf).Select(p => p.Id).ToList();
        Assert.Contains("universal", elfPool);
        Assert.DoesNotContain("orcish", elfPool);
    }

    [Fact]
    public void AxisDistributionIsCountable()
    {
        var catalog = TestPerks.CatalogWith(
            ("a", TestPerks.Json("a", "MATCH_START", OneEffect, axis: "alignment", links: """["ahead"]""")),
            ("b", TestPerks.Json("b", "MATCH_START", OneEffect, axis: "alignment", links: """["ahead"]""")),
            ("c", TestPerks.Json("c", "MATCH_START", OneEffect, axis: "proximity")));

        var counts = catalog.Perks.CountByAxis();
        Assert.Equal(2, counts[(int)PerkAxis.Alignment]);
        Assert.Equal(1, counts[(int)PerkAxis.Proximity]);

        // Las cinco habilidades raciales son de eje de identidad.
        Assert.Equal(5, counts[(int)PerkAxis.Identity]);
    }

    /// <summary>
    /// El eje de acumulación necesita crecer en los tres canales que suman un número, no solo en el de
    /// atributo: un perk que mejora la intercepción partido a partido es tan natural como uno que sube la
    /// fuerza. En modifyProbability lo que va en puntos porcentuales es el incremento por unidad y el tope.
    /// </summary>
    [Fact]
    public void CounterScaledEffectsWorkOnProbabilityAndLeash()
    {
        var perk = TestPerks.Load("grower", TestPerks.Json(
            "grower",
            "MATCH_START",
            """
            [
              { "type": "modifyProbability", "target": "owner", "probability": "intercept",
                "valuePerCounter": 2, "counter": "matches", "maxValue": 10, "duration": "match" },
              { "type": "modifyLeash", "target": "owner",
                "valuePerCounter": 5, "counter": "matches", "maxValue": 10, "counterDivisor": 4, "duration": "match" }
            ]
            """,
            axis: "accumulation",
            accumulates: true));

        Assert.True(perk.Effects[0].UsesCounter);
        Assert.Equal(200, perk.Effects[0].ValuePerCounter);
        Assert.Equal(1000, perk.Effects[0].MaxValue);

        // La correa son casillas: ni el incremento ni el tope se convierten.
        Assert.Equal(5, perk.Effects[1].ValuePerCounter);
        Assert.Equal(10, perk.Effects[1].MaxValue);
    }

    [Fact]
    public void ACounterScaledPairIsRejected()
    {
        var ex = Assert.Throws<DataException>(() => TestPerks.Load("both", TestPerks.Json(
            "both",
            "MATCH_START",
            """
            [{ "type": "modifyProbability", "target": "linked", "probability": "pass",
               "valuePerCounter": 5, "counter": "matches", "maxValue": 25, "duration": "match" }]
            """,
            axis: "alignment",
            links: """["beside"]""")));
        Assert.Contains("par", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CounterScaledEffectsAreStillRejectedWhereTheyMakeNoSense()
    {
        Assert.Throws<DataException>(() => TestPerks.Load("bias", TestPerks.Json(
            "bias", "MATCH_START", """[{ "type": "modifyBias", "valuePerCounter": 1, "counter": "matches" }]""")));
    }
}
