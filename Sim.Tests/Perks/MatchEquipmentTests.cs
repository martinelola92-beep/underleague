using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// El equipamiento dentro del partido: objetos (RF-075..078), sus tres arquetipos obligatorios (RF-077),
/// consumibles (RF-080..085) y las dos vías de muerte de RF-093. Es lo que cierra el hueco que los
/// paquetes W y X dejaron anotado: "el estado los guarda pero <c>MatchSetup</c> no los recibe".
/// </summary>
public sealed class MatchEquipmentTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Objeto llano: +20 de fuerza al portador mientras esté equipado.</summary>
    private static MatchItem Strong(int value = 20) => new(
        "test_strength",
        Rarity.Common,
        new[] { new EffectDefinition(EffectType.ModifyAttribute, Attribute: AttributeKind.Strength, Value: value, Duration: EffectDuration.Run) });

    [Fact]
    public void AnEquippedItem_ChangesTheAttributeInsideTheMatch()
    {
        // RF-075..078: el objeto tiene efecto en el partido, y no ocupa slot de perk (RF-076): el jugador
        // no lleva ninguno y aun así el objeto funciona.
        var setup = TestMatches.Reference(Catalog, 11);
        int baseStrength = setup.Home.Players[1].Attributes.Strength;
        var withItem = WithItem(setup, playerId: 1, Strong());

        var engine = TestPerks.Engine(Catalog, withItem);
        Assert.Equal(baseStrength + 20, engine.PlayerById(1)!.Strength);
        Assert.Empty(setup.Home.Players[1].Perks);

        // Y se registra aparte de los perks (RT-043), que es lo que permite medirlos por separado.
        var result = Underleague.Sim.Engine.Simulator.Run(withItem, 11, Catalog, new SimConfig(CollectLog: false));
        var activation = Assert.Single(result.Report.ItemActivations);
        Assert.Equal("test_strength", activation.ItemId);
        Assert.Equal(1, activation.OwnerId);
        Assert.Equal(1, activation.Effects);
        Assert.Equal("equipped", activation.Detail);
        Assert.DoesNotContain(result.Report.PerkActivations, a => a.PerkId == "test_strength");
    }

    [Fact]
    public void CursedItem_AppliesItsDrawbackToo()
    {
        // RF-077, maldito: efecto potente y contrapartida permanente, las dos a la vez y sin condición.
        var item = new MatchItem(
            "test_cursed",
            Rarity.Rare,
            new[] { new EffectDefinition(EffectType.ModifyAttribute, Attribute: AttributeKind.Strength, Value: 18, Duration: EffectDuration.Run) })
        {
            DrawbackEffects = new[]
            {
                new EffectDefinition(EffectType.ModifyProbability, Probability: ProbabilityKind.Injury, Value: 1000, Duration: EffectDuration.Run),
            },
        };

        var setup = TestMatches.Reference(Catalog, 12);
        int baseStrength = setup.Home.Players[2].Attributes.Strength;
        var engine = TestPerks.Engine(Catalog, WithItem(setup, 2, item));

        var bearer = engine.PlayerById(2)!;
        Assert.Equal(baseStrength + 18, bearer.Strength);
        Assert.Equal(1000, engine.Effects!.Modifiers.Probability(bearer, ProbabilityKind.Injury));
        var activation = Assert.Single(engine.Report.ItemActivations);
        Assert.Equal("cursed", activation.Detail);
        Assert.Equal(2, activation.Effects);
    }

    [Fact]
    public void RestrictedItem_OnlyWorksOnABearerWithTheTag()
    {
        // RF-077, restringido. El mismo objeto, dos portadores: uno con la etiqueta y otro sin ella.
        var item = Strong() with { Id = "test_restricted", RequiredTag = "Scrap" };
        var setup = TestMatches.Reference(Catalog, 13);
        int baseStrength = setup.Home.Players[3].Attributes.Strength;

        var without = TestPerks.Engine(Catalog, WithItem(setup, 3, item));
        Assert.Equal(baseStrength, without.PlayerById(3)!.Strength);
        var inactive = Assert.Single(without.Report.ItemActivations);
        Assert.Equal("restricted:Scrap", inactive.Detail);
        Assert.Equal(0, inactive.Effects);

        var tagged = WithItem(setup, 3, item);
        tagged = tagged with { Home = WithTag(tagged.Home, 3, "Scrap") };
        var with = TestPerks.Engine(Catalog, tagged);
        Assert.Equal(baseStrength + 20, with.PlayerById(3)!.Strength);
        Assert.Equal("equipped", Assert.Single(with.Report.ItemActivations).Detail);
    }

    [Fact]
    public void AnItemThatDoesNotApply_DoesNotChangeASingleEvent()
    {
        // Determinismo: el equipamiento no consume azar. Un objeto restringido que no aplica produce
        // exactamente el mismo partido que no llevar ninguno, evento a evento, aunque en un caso el motor
        // de efectos exista y en el otro no.
        var setup = TestMatches.Reference(Catalog, 14);
        var inert = WithItem(setup, 4, Strong() with { Id = "test_inert", RequiredTag = "Undead" });

        var baseline = Underleague.Sim.Engine.Simulator.Run(setup, 14, Catalog, new SimConfig(CollectLog: false));
        var equipped = Underleague.Sim.Engine.Simulator.Run(inert, 14, Catalog, new SimConfig(CollectLog: false));

        Assert.Equal(baseline.Events.Count, equipped.Events.Count);
        for (int i = 0; i < baseline.Events.Count; i++)
        {
            Assert.Equal(baseline.Events[i], equipped.Events[i]);
        }
    }

    [Fact]
    public void ConditionalConsumable_ResolvesWhenItsTriggerHolds()
    {
        // RF-081..083: el condicional se ejecuta solo, deja su evento en la secuencia y su entrada en el
        // informe, y no se gasta dos veces (RF-085).
        var consumable = new MatchConsumable(
            "test_charm",
            Rarity.Common,
            new[] { new EffectDefinition(EffectType.ModifyProbability, Probability: ProbabilityKind.ShotOnTarget, Value: 1500, Duration: EffectDuration.Run) },
            ConsumableTrigger.LastSeconds);

        var setup = TestMatches.Reference(Catalog, 15);
        setup = setup with { Home = setup.Home with { Consumables = new[] { consumable } } };

        var result = Underleague.Sim.Engine.Simulator.Run(setup, 15, Catalog, new SimConfig(CollectLog: false));
        var used = Assert.Single(result.Report.ConsumableActivations);
        Assert.Equal("test_charm", used.ConsumableId);
        Assert.Equal(0, used.Team);
        Assert.Equal("lastSeconds", used.Trigger);
        Assert.Single(result.Events, e => e.Type == EventType.ConsumableUsed && e.Detail == "test_charm");
    }

    [Fact]
    public void ManualConsumable_OnlyResolvesWithTheActivationInTheInitialState()
    {
        // RF-082 con la semántica de docs/arquitectura.md: la pulsación es un dato del estado inicial.
        // Sin ella —el caso de /Balance, donde no hay quien la pulse— el canal está abierto y no pasa nada.
        var effects = new[]
        {
            new EffectDefinition(EffectType.ModifyAttribute, Attribute: AttributeKind.Speed, Value: 8, Duration: EffectDuration.Run),
        };

        var setup = TestMatches.Reference(Catalog, 16);
        var never = setup with
        {
            Home = setup.Home with
            {
                Consumables = new[] { new MatchConsumable("test_boost", Rarity.Common, effects, ConsumableTrigger.Manual) },
            },
        };

        var pressed = setup with
        {
            Home = setup.Home with
            {
                Consumables = new[]
                {
                    new MatchConsumable("test_boost", Rarity.Common, effects, ConsumableTrigger.Manual) { ManualTick = 120 },
                },
            },
        };

        var withoutPress = Underleague.Sim.Engine.Simulator.Run(never, 16, Catalog, new SimConfig(CollectLog: false));
        Assert.Empty(withoutPress.Report.ConsumableActivations);

        var withPress = Underleague.Sim.Engine.Simulator.Run(pressed, 16, Catalog, new SimConfig(CollectLog: false));
        var used = Assert.Single(withPress.Report.ConsumableActivations);
        Assert.Equal(120, used.Tick);
        Assert.Equal("manual", used.Trigger);

        // Y sin pulsar, el partido es exactamente el de siempre.
        var baseline = Underleague.Sim.Engine.Simulator.Run(setup, 16, Catalog, new SimConfig(CollectLog: false));
        Assert.Equal(baseline.Events.Count, withoutPress.Events.Count);
    }

    [Fact]
    public void AHealthyPlayerNeverDies()
    {
        // RF-093, la regla que no se negocia. El partido "brutal" es el escenario más violento que el
        // motor admite (siete bestias contra cinco frágiles) y aun así no mata a nadie.
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = Underleague.Sim.Engine.Simulator.Run(
                TestMatches.Brutal(Catalog), seed, Catalog, new SimConfig(CollectLog: false));
            Assert.Equal(0, result.Report.Deaths);
        }
    }

    [Fact]
    public void AStarterWithAnUntreatedSevereInjury_DiesIfHeIsInjuredAgain()
    {
        // RF-093 vía 1. Se recorren semillas hasta encontrar un partido en el que el lesionado vuelva a
        // lesionarse: lo que se comprueba no es la frecuencia, es que cuando ocurre el resultado es la
        // muerte, con su evento en la secuencia.
        int deaths = 0;
        for (ulong seed = 1; seed <= 40 && deaths == 0; seed++)
        {
            var setup = TestMatches.Brutal(Catalog);
            var wounded = setup.Home.Players
                .Select(p => p.Id == 1 ? p with { PhysicalState = PhysicalState.SevereInjury } : p)
                .ToList();
            setup = setup with { Home = setup.Home with { Players = wounded } };

            var result = Underleague.Sim.Engine.Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            if (result.Report.Deaths == 0)
            {
                continue;
            }

            deaths = result.Report.Deaths;
            var death = Assert.Single(result.Events, e => e.Type == EventType.Death);
            Assert.Equal(1, death.Actor);
            Assert.Equal("severeInjury", death.Detail);
            Assert.Contains(result.Events, e => e.Type == EventType.Injury && e.Actor == 1);
        }

        Assert.True(deaths > 0, "ninguna de las 40 semillas volvió a lesionar al jugador: el escenario no prueba nada");
    }

    [Fact]
    public void ALethalPerk_KillsOnlyAnOpponentWhoWasAlreadyHurt()
    {
        // RF-093 vía 2. El mismo perk letal, dos víctimas: una que llegó con una lesión leve arrastrada y
        // otra sana. Solo muere la primera; la sana sale ilesa de la misma jugada.
        const string Lethal = """
        {
          "id": "test_lethal",
          "name": { "es": "letal", "en": "lethal" },
          "rarity": "legendary",
          "kind": "ruleBreaker",
          "axis": "identity",
          "race": null,
          "links": [],
          "trigger": "MATCH_START",
          "scope": "any",
          "condition": "",
          "effects": [ { "type": "modifyProbability", "target": "opposingTeam", "probability": "injury", "value": 5, "duration": "match" } ],
          "elseEffects": [],
          "accumulatesAcrossMatches": false,
          "lethal": true,
          "positionOnly": null,
          "tagsRequired": [],
          "tagsForbidden": []
        }
        """;

        var catalog = TestPerks.CatalogWith(("test_lethal", Lethal));
        var setup = TestMatches.Reference(catalog, 21);

        // El visitante lleva el perk letal; en el local, el jugador 1 arrastra una lesión leve y el 2 está sano.
        var home = setup.Home.Players
            .Select(p => p.Id == 1 ? p with { PhysicalState = PhysicalState.MinorInjury } : p)
            .ToList();
        var away = setup.Away.Players
            .Select(p => p.Id == 100 ? p with { Perks = new[] { "test_lethal" } } : p)
            .ToList();
        setup = setup with
        {
            Home = setup.Home with { Players = home },
            Away = setup.Away with { Players = away },
        };

        var result = Underleague.Sim.Engine.Simulator.Run(setup, 21, catalog, new SimConfig(CollectLog: false));
        var death = Assert.Single(result.Events, e => e.Type == EventType.Death);
        Assert.Equal(1, death.Actor);
        Assert.Equal("perk:test_lethal", death.Detail);
        Assert.Equal(1, result.Report.Deaths);

        // Y el informe de ojeo lo destaca antes de jugar (RF-013).
        var threats = Scouting.LethalPerks(setup.Away, catalog);
        var threat = Assert.Single(threats);
        Assert.Equal(100, threat.PlayerId);
        Assert.Equal("test_lethal", threat.PerkId);
        Assert.Empty(Scouting.LethalPerks(setup.Home, catalog));
    }

    [Fact]
    public void ALethalPerkThatCannotReachAnOpponent_IsRejectedOnLoad()
    {
        // RF-013: si un perk se anuncia como letal, tiene que poder matar. Uno que solo se aplica a sí
        // mismo sería una etiqueta de peligro sin peligro.
        const string OwnerOnly = """
        {
          "id": "test_lethal_owner",
          "name": { "es": "letal", "en": "lethal" },
          "rarity": "legendary",
          "kind": "ruleBreaker",
          "axis": "identity",
          "race": null,
          "links": [],
          "trigger": "MATCH_START",
          "scope": "any",
          "condition": "",
          "effects": [ { "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 10, "duration": "match" } ],
          "elseEffects": [],
          "accumulatesAcrossMatches": false,
          "lethal": true,
          "positionOnly": null,
          "tagsRequired": [],
          "tagsForbidden": []
        }
        """;

        var error = Assert.Throws<DataException>(() => TestPerks.Load("test_lethal_owner", OwnerOnly));
        Assert.Contains("letal", error.Message, StringComparison.Ordinal);
    }

    private static MatchSetup WithItem(MatchSetup setup, int playerId, MatchItem item)
    {
        var players = setup.Home.Players
            .Select(p => p.Id == playerId ? p with { Item = item } : p)
            .ToList();
        return setup with { Home = setup.Home with { Players = players } };
    }

    private static TeamSetup WithTag(TeamSetup team, int playerId, string tag)
    {
        var players = team.Players.Select(p =>
        {
            if (p.Id != playerId)
            {
                return p;
            }

            var tags = new List<string>(p.Tags) { tag };
            return p with { Tags = tags };
        }).ToList();

        return team with { Players = players };
    }
}
