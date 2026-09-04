using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Motor de efectos (RT-040..RT-043): orden de resolución, límites, cancelación, recursión, alcance y
/// caducidad de los modificadores de jugada. Los equipos salen de <see cref="Underleague.Sim.Generation.TeamGenerator"/>
/// y los perks se asignan con <c>with { Perks = [...] }</c>, como hará la campaña.
/// </summary>
public sealed class EffectEngineTests
{
    private const string Strength1 = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 1, "duration": "match" }]""";

    [Fact]
    public void SimultaneousPerksResolveByRarityThenOwnerThenPerkId()
    {
        // RT-041: rareza descendente, id de jugador ascendente, id de perk ordinal ascendente. Los tres
        // perks se disparan con el mismo evento; el orden del informe es el orden de resolución.
        var catalog = TestPerks.CatalogWith(
            ("zz_legendary", TestPerks.Json("zz_legendary", "MATCH_START", Strength1, rarity: "legendary")),
            ("aa_common", TestPerks.Json("aa_common", "MATCH_START", Strength1, rarity: "common")),
            ("mm_rare", TestPerks.Json("mm_rare", "MATCH_START", Strength1, rarity: "rare")));

        // El jugador 2 lleva los tres; el jugador 1 lleva el común, para comprobar también el desempate
        // por id de jugador dentro de la misma rareza.
        var setup = TestPerks.Match(
            catalog,
            1,
            (2, new[] { "zz_legendary", "aa_common", "mm_rare" }),
            (1, new[] { "aa_common" }));

        var engine = TestPerks.Engine(catalog, setup);
        engine.Effects!.Publish(Event(EventType.MatchStart, engine, 0, "kickoff"));

        var order = engine.Report.PerkActivations.Select(a => $"{a.PerkId}#{a.OwnerId}").ToList();
        Assert.Equal(
            new[] { "zz_legendary#2", "mm_rare#2", "aa_common#1", "aa_common#2" },
            order);
    }

    [Fact]
    public void MatchLimitStopsThePerkAfterItsTimes()
    {
        var catalog = TestPerks.CatalogWith(
            ("twice", TestPerks.Json("twice", "TACKLE", Strength1, limit: """{ "per": "match", "times": 2 }""")));
        var (engine, owner) = Engine(catalog, "twice");

        for (int i = 0; i < 5; i++)
        {
            engine.Effects!.Publish(Tackle(engine, owner));
        }

        Assert.Equal(2, engine.Report.PerkActivations.Count);
        Assert.Equal(2, owner.Effective(AttributeKind.Strength) - owner.BaseAttribute(AttributeKind.Strength));
    }

    [Fact]
    public void PlayLimitResetsWhenThePlayEnds()
    {
        var catalog = TestPerks.CatalogWith(
            ("once_per_play", TestPerks.Json("once_per_play", "TACKLE", Strength1, limit: """{ "per": "play", "times": 1 }""")));
        var (engine, owner) = Engine(catalog, "once_per_play");

        engine.Effects!.Publish(Tackle(engine, owner));
        engine.Effects.Publish(Tackle(engine, owner));
        Assert.Single(engine.Report.PerkActivations);

        engine.Effects.EndPlay();
        engine.Effects.Publish(Tackle(engine, owner));
        Assert.Equal(2, engine.Report.PerkActivations.Count);
    }

    [Fact]
    public void PlayModifiersExpireWhenThePlayEnds()
    {
        const string PlayBonus = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 6, "duration": "play" }]""";
        const string MatchBonus = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "speed", "value": 4, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith(
            ("play_bonus", TestPerks.Json("play_bonus", "TACKLE", PlayBonus)),
            ("match_bonus", TestPerks.Json("match_bonus", "TACKLE", MatchBonus)));

        var setup = TestPerks.Match(catalog, 1, (1, new[] { "play_bonus", "match_bonus" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;
        int baseStrength = owner.BaseAttribute(AttributeKind.Strength);
        int baseSpeed = owner.BaseAttribute(AttributeKind.Speed);

        engine.Effects!.Publish(Tackle(engine, owner));
        Assert.Equal(baseStrength + 6, owner.Effective(AttributeKind.Strength));
        Assert.Equal(baseSpeed + 4, owner.Effective(AttributeKind.Speed));

        engine.Effects.EndPlay();
        Assert.Equal(baseStrength, owner.Effective(AttributeKind.Strength));
        Assert.Equal(baseSpeed + 4, owner.Effective(AttributeKind.Speed));
    }

    [Fact]
    public void LeashModifierChangesTheRadiusAndExpiresWithThePlay()
    {
        const string Leash = """[{ "type": "modifyLeash", "target": "owner", "value": 2, "duration": "play" }]""";
        var catalog = TestPerks.CatalogWith(("long_legs", TestPerks.Json("long_legs", "TACKLE", Leash)));
        var (engine, owner) = Engine(catalog, "long_legs");

        float before = owner.LeashCells;
        engine.Effects!.Publish(Tackle(engine, owner));
        Assert.Equal(before + 2f, owner.LeashCells);

        engine.Effects.EndPlay();
        Assert.Equal(before, owner.LeashCells);
    }

    [Fact]
    public void CancelEventOnACardStopsTheSendingOff()
    {
        const string Cancel = """[{ "type": "cancelEvent" }]""";
        var catalog = TestPerks.CatalogWith(
            ("innocent_face", TestPerks.Json("innocent_face", "CARD", Cancel, rarity: "legendary", kind: "ruleBreaker")));
        var (engine, owner) = Engine(catalog, "innocent_face");

        bool notCancelled = engine.Effects!.Publish(new MatchEvent(
            EventType.Card, 10, owner.Team, owner.Id, -1, -1,
            owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, "red"));

        Assert.False(notCancelled);
        Assert.Single(engine.Report.PerkActivations);
    }

    [Fact]
    public void CancelledEventLeavesTheEventInTheSequenceWithItsSuffix()
    {
        // El motor completo: el perk anula la falta y el evento FOUL queda igualmente registrado, con
        // Detail "foul:cancelled" (§2). Se usa FOUL y no CARD porque la falta es el evento cancelable que
        // el motor produce con regularidad; la cancelación de CARD la cubre el test unitario de arriba.
        const string Cancel = """[{ "type": "cancelEvent" }]""";
        var catalog = TestPerks.CatalogWith(
            ("innocent_face", TestPerks.Json("innocent_face", "FOUL", Cancel, scope: "any", rarity: "legendary", kind: "ruleBreaker")));

        // Se recorren varias semillas y no una fija: que un partido concreto tenga falta depende del
        // ajuste del motor, y este test es sobre la anulación, no sobre la tasa de faltas (RT-056).
        var fouls = new List<MatchEvent>();
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var setup = TestPerks.Match(catalog, seed, (1, new[] { "innocent_face" }));
            var result = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false));

            // La falta sigue contando en el informe -ocurrió-, pero el árbitro no la castiga: no hay
            // tarjeta ni derribo (comentario de ResolveFoul en MatchEngine).
            fouls.AddRange(result.Events.Where(e => e.Type == EventType.Foul));
            Assert.Equal(0, result.Report.YellowCards);
            Assert.Equal(0, result.Report.RedCards);
        }

        Assert.NotEmpty(fouls);
        Assert.All(fouls, c => Assert.EndsWith(":cancelled", c.Detail, StringComparison.Ordinal));
    }

    [Fact]
    public void PublishBeyondMaxDepthIsCutAndCounted()
    {
        // RT-042: la publicación se descarta y se registra el corte; el perk no llega a activarse.
        var catalog = TestPerks.CatalogWith(("deep", TestPerks.Json("deep", "TACKLE", Strength1)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "deep" }));
        var engine = TestPerks.Engine(catalog, setup, maxDepth: 2);
        var owner = engine.PlayerById(1)!;

        Assert.True(engine.Effects!.PublishAtDepth(Tackle(engine, owner), 2));
        Assert.Single(engine.Report.PerkActivations);
        Assert.Equal(0, engine.Report.RecursionCuts);

        Assert.True(engine.Effects.PublishAtDepth(Tackle(engine, owner), 3));
        Assert.Single(engine.Report.PerkActivations);
        Assert.Equal(1, engine.Report.RecursionCuts);
    }

    [Theory]
    [InlineData("actor", 1, -1, true)]
    [InlineData("actor", 2, -1, false)]
    [InlineData("target", 2, 1, true)]
    [InlineData("target", 2, 2, false)]
    [InlineData("team", 2, -1, true)]
    [InlineData("team", 100, -1, false)]
    [InlineData("opposingTeam", 100, -1, true)]
    [InlineData("opposingTeam", 2, -1, false)]
    [InlineData("any", 100, -1, true)]
    public void ScopeDecidesWhoseEventCountsForThePerk(string scope, int actorId, int targetId, bool expected)
    {
        var catalog = TestPerks.CatalogWith(("scoped", TestPerks.Json("scoped", "TACKLE", Strength1, scope: scope)));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "scoped" }));
        var engine = TestPerks.Engine(catalog, setup);
        var actor = engine.PlayerById(actorId)!;

        engine.Effects!.Publish(new MatchEvent(
            EventType.Tackle, 5, actor.Team, actorId, targetId, -1,
            actor.HomeCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, "attempted"));

        Assert.Equal(expected ? 1 : 0, engine.Report.PerkActivations.Count);
    }

    [Fact]
    public void ActorlessEventsAreEvaluatedOncePerPerkWithOwnerAsActor()
    {
        // MATCH_START no tiene actor (§2): el perk del jugador 100 (visitante) se evalúa igual, con
        // actor = owner, aunque su alcance sea "actor".
        var catalog = TestPerks.CatalogWith(("kickoff_bonus", TestPerks.Json("kickoff_bonus", "MATCH_START", Strength1)));
        var setup = TestPerks.Match(catalog, 1, (100, new[] { "kickoff_bonus" }));
        var engine = TestPerks.Engine(catalog, setup);

        engine.Effects!.Publish(Event(EventType.MatchStart, engine, 0, "kickoff"));
        Assert.Equal("kickoff_bonus", Assert.Single(engine.Report.PerkActivations).PerkId);
        Assert.Equal(100, engine.Report.PerkActivations[0].OwnerId);
    }

    [Fact]
    public void ElseEffectsApplyWhenTheConditionIsFalse()
    {
        const string Bonus = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 8, "duration": "match" }]""";
        const string Malus = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": -6, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith(("lone_wolf", TestPerks.Json(
            "lone_wolf", "TACKLE", Bonus, condition: "hasTag(actor, 'Brute')", elseEffects: Malus)));
        var (engine, owner) = Engine(catalog, "lone_wolf");

        engine.Effects!.Publish(Tackle(engine, owner));

        Assert.Equal(-6, owner.Effective(AttributeKind.Strength) - owner.BaseAttribute(AttributeKind.Strength));
        Assert.EndsWith(":else", Assert.Single(engine.Report.PerkActivations).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void CountersAccumulateAndAreReportedAsDeltas()
    {
        const string Count = """[{ "type": "addCounter", "counter": "tackles", "value": 1 }]""";
        var catalog = TestPerks.CatalogWith(
            ("tally", TestPerks.Json("tally", "TACKLE", Count, accumulates: true)));
        var (engine, owner) = Engine(catalog, "tally");

        engine.Effects!.Publish(Tackle(engine, owner));
        engine.Effects.Publish(Tackle(engine, owner));

        Assert.Equal(2, engine.Effects.Counter(owner, "tackles"));
        var delta = Assert.Single(engine.Effects.CounterDeltas());
        Assert.Equal(new PlayerCounterDelta(1, "tackles", 2), delta);
    }

    [Fact]
    public void StoredCountersSeedOnlyAccumulatingPerks()
    {
        const string Count = """[{ "type": "addCounter", "counter": "matches", "value": 1 }]""";
        var catalog = TestPerks.CatalogWith(
            ("keeps", TestPerks.Json("keeps", "TACKLE", Count, accumulates: true)),
            ("forgets", TestPerks.Json("forgets", "TACKLE", Count)));

        Assert.Equal(7, SeededCounter(catalog, "keeps"));
        Assert.Equal(0, SeededCounter(catalog, "forgets"));
    }

    [Fact]
    public void ModifyBiasMovesTheRefereeTowardsTheOwnerTeam()
    {
        const string Bias = """[{ "type": "modifyBias", "value": 15 }]""";
        var catalog = TestPerks.CatalogWith(("fixer", TestPerks.Json("fixer", "TACKLE", Bias)));

        // Local: el criterio sube (positivo favorece al local, RF-060). Visitante: baja.
        Assert.Equal(15, BiasAfter(catalog, 1));
        Assert.Equal(-15, BiasAfter(catalog, 100));
    }

    [Fact]
    public void ZeroPerksMeansNoEffectEngineAtAll()
    {
        var catalog = TestPerks.CatalogWith();
        var engine = TestPerks.Engine(catalog, TestMatches.Reference(catalog, 1));
        Assert.Null(engine.Effects);
    }

    [Fact]
    public void PerkActivationsReachTheMatchReport()
    {
        const string Bonus = """[{ "type": "modifyAttribute", "target": "owner", "attribute": "strength", "value": 2, "duration": "match" }]""";
        var catalog = TestPerks.CatalogWith(("kickoff_bonus", TestPerks.Json("kickoff_bonus", "MATCH_START", Bonus)));
        var setup = TestPerks.Match(catalog, 5, (1, new[] { "kickoff_bonus" }));

        var result = Simulator.Run(setup, 5, catalog, new SimConfig(CollectLog: false));

        // Además del perk asignado, el informe trae las activaciones de la habilidad racial que el motor
        // concede a toda la plantilla (RF-031b, ADR 0026): aquí se mira solo la del perk del test.
        Assert.Equal(0, result.Report.RecursionCuts);
        var activation = Assert.Single(result.Report.PerkActivations, a => a.PerkId == "kickoff_bonus");
        Assert.Equal("kickoff_bonus", activation.PerkId);
        Assert.Equal(EventType.MatchStart, activation.EventType);
        var summary = Assert.Single(result.Report.PerksSummary, p => p.PerkId == "kickoff_bonus");
        Assert.Equal(new PerkActivationSummary("kickoff_bonus", 1, 1), summary);
    }

    private static int SeededCounter(Catalog catalog, string perkId)
    {
        var setup = TestPerks.Match(catalog, 1, (1, new[] { perkId }));
        var home = setup.Home with
        {
            Players = setup.Home.Players
                .Select(p => p.Id == 1 ? p.WithCounters(new Dictionary<string, int> { ["matches"] = 7 }) : p)
                .ToList(),
        };

        var engine = TestPerks.Engine(catalog, setup with { Home = home });
        return engine.Effects!.Counter(engine.PlayerById(1)!, "matches");
    }

    private static int BiasAfter(Catalog catalog, int ownerId)
    {
        var setup = TestPerks.Match(catalog, 1, (ownerId, new[] { "fixer" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(ownerId)!;
        engine.Effects!.Publish(Tackle(engine, owner));
        return engine.BiasFor(0);
    }

    private static (MatchEngine Engine, MatchPlayer Owner) Engine(Catalog catalog, string perkId)
    {
        var setup = TestPerks.Match(catalog, 1, (1, new[] { perkId }));
        var engine = TestPerks.Engine(catalog, setup);
        return (engine, engine.PlayerById(1)!);
    }

    private static MatchEvent Tackle(MatchEngine engine, MatchPlayer owner) => new(
        EventType.Tackle, engine.Tick, owner.Team, owner.Id, -1, -1,
        owner.HomeCell, Zone.Own, MatchPhase.OpenPlay, engine.BiasFor(0), 0, "attempted");

    private static MatchEvent Event(EventType type, MatchEngine engine, int team, string detail) => new(
        type, engine.Tick, team, -1, -1, -1,
        new Cell(0, 0), Zone.Middle, MatchPhase.Kickoff, engine.BiasFor(0), 0, detail);
}
