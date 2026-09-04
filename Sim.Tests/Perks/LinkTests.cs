using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Vínculos direccionales (RF-044, ADR 0021, fase1b-diseno.md §2.4): se resuelven una sola vez al
/// construir el partido, en coordenadas relativas al sentido de ataque, con un candidato por relación.
/// <para>
/// La alineación de estos tests se escribe a mano en vez de usar la de por defecto porque esta última
/// coloca a los jugadores en columnas 0, 2, 4 y 6: casi ninguna relación tiene candidato, que es
/// exactamente lo que la ADR busca (la colocación es una decisión con coste) pero no sirve para probar
/// la geometría.
/// </para>
/// </summary>
public sealed class LinkTests
{
    private const string Speed1 =
        """[{ "type": "modifyAttribute", "target": "owner", "attribute": "speed", "value": 1, "duration": "match" }]""";

    /// <summary>
    /// Casillas-hogar (relativas al equipo) que usan estos tests, en el orden de la alineación por
    /// defecto. El jugador del índice 4, en (4,2), es el que tiene vecinos en todas las direcciones salvo
    /// las diagonales.
    /// </summary>
    private static readonly Cell[] Cells =
    {
        new(0, 2), new(2, 1), new(2, 2), new(3, 2), new(4, 2), new(4, 3), new(5, 2),
    };

    [Theory]
    [InlineData("beside", 5)]
    [InlineData("ahead", 6)]
    [InlineData("behind", 3)]
    [InlineData("left", 1)]
    [InlineData("right", 5)]
    public void EachRelationResolvesToItsSingleCandidate(string relation, int slotOfLinked)
    {
        var (engine, ids) = Match(relation);
        var links = engine.Effects!.Links!;
        var hub = engine.PlayerById(ids[4])!;

        var linked = links.Linked(hub, Relation(relation));
        Assert.NotNull(linked);
        Assert.Equal(ids[slotOfLinked], linked.Id);
    }

    [Theory]
    [InlineData("diagonalAhead")]
    [InlineData("diagonalBehind")]
    public void WithoutCandidateThereIsNoLink(string relation)
    {
        var (engine, ids) = Match(relation);
        var hub = engine.PlayerById(ids[4])!;
        Assert.Null(engine.Effects!.Links!.Linked(hub, Relation(relation)));
        Assert.False(engine.Effects.Links.HasLink(hub, Relation(relation)));
    }

    /// <summary>
    /// El visitante refleja columnas **y** bandas (ADR 0021), de modo que un mismo perk describe la misma
    /// estructura para los dos equipos: el "de delante" del visitante también está más cerca de la
    /// portería que ataca, y su "izquierda" es la contraria en filas absolutas.
    /// </summary>
    [Fact]
    public void TheAwayTeamMirrorsColumnsAndFlanks()
    {
        var (engine, ids) = Match("ahead");
        var links = engine.Effects!.Links!;

        var homeHub = engine.PlayerById(ids[4])!;
        var awayHub = engine.PlayerById(ids[4] + 100)!;

        // Mismas casillas relativas: el vinculado es el mismo slot en los dos equipos...
        Assert.Equal(ids[6], links.Linked(homeHub, LinkRelation.Ahead)!.Id);
        Assert.Equal(ids[6] + 100, links.Linked(awayHub, LinkRelation.Ahead)!.Id);

        // ...aunque en columnas absolutas vayan en sentidos opuestos.
        Assert.True(links.Linked(homeHub, LinkRelation.Ahead)!.HomeCell.Column > homeHub.HomeCell.Column);
        Assert.True(links.Linked(awayHub, LinkRelation.Ahead)!.HomeCell.Column < awayHub.HomeCell.Column);

        // Las bandas también se reflejan, y ahí el reflejo sí cambia de vecino: la izquierda del local
        // son filas menores y la del visitante, mayores. Es la banda izquierda del que mira a la portería
        // que ataca, que es lo que dice la convención de orientación de la ADR 0021.
        Assert.Equal(ids[1], links.Linked(homeHub, LinkRelation.Left)!.Id);
        Assert.Equal(ids[5] + 100, links.Linked(awayHub, LinkRelation.Left)!.Id);
        Assert.True(links.Linked(homeHub, LinkRelation.Left)!.HomeCell.Row < homeHub.HomeCell.Row);
        Assert.True(links.Linked(awayHub, LinkRelation.Left)!.HomeCell.Row > awayHub.HomeCell.Row);
    }

    [Fact]
    public void StartZoneAndFlankAreRelativeToTheAttackingDirection()
    {
        // Columna 0 del equipo 0 y columna 15 del equipo 1 son el mismo tercio propio.
        Assert.Equal(StartZone.OwnThird, LinkGeometry.ZoneOfHome(new Cell(0, 2), 0));
        Assert.Equal(StartZone.OwnThird, LinkGeometry.ZoneOfHome(new Cell(15, 2), 1));
        Assert.Equal(StartZone.AttackingThird, LinkGeometry.ZoneOfHome(new Cell(15, 2), 0));
        Assert.Equal(StartZone.Middle, LinkGeometry.ZoneOfHome(new Cell(8, 2), 0));

        Assert.Equal(StartFlank.LeftFlank, LinkGeometry.FlankOfHome(new Cell(4, 0), 0));
        Assert.Equal(StartFlank.RightFlank, LinkGeometry.FlankOfHome(new Cell(4, 0), 1));
        Assert.Equal(StartFlank.Center, LinkGeometry.FlankOfHome(new Cell(4, 2), 0));
        Assert.Equal(StartFlank.Center, LinkGeometry.FlankOfHome(new Cell(4, 2), 1));
    }

    /// <summary>
    /// El modificador por par (§2.4): el bono vale en la resolución que enfrenta al portador con **ese**
    /// vinculado, y en ninguna otra. Es lo que separa "mejora el pase hacia el compañero de su columna"
    /// de "mejora el pase".
    /// </summary>
    [Fact]
    public void PairedProbabilityAppliesOnlyToTheLinkedCounterpart()
    {
        var (engine, ids) = Match(
            "beside",
            """[{ "type": "modifyProbability", "target": "linked", "probability": "pass", "value": 10, "duration": "match" }]""");

        var effects = engine.Effects!;
        var hub = engine.PlayerById(ids[4])!;
        var linked = engine.PlayerById(ids[5])!;
        var other = engine.PlayerById(ids[3])!;

        // El perk se dispara con MATCH_START y deja registrado el modificador por par para todo el
        // partido; lo que decide si se cobra o no es el par de cada resolución posterior.
        effects.Publish(MatchStart(engine));

        effects.Publish(Pass(engine, hub, linked));
        Assert.Equal(1000, effects.Modifiers.Probability(hub, ProbabilityKind.Pass));

        // Mismo portador, otro receptor: el bono no existe.
        effects.Publish(Pass(engine, hub, other));
        Assert.Equal(0, effects.Modifiers.Probability(hub, ProbabilityKind.Pass));

        // Y no es un bono del vinculado: sus propios pases siguen igual.
        effects.Publish(Pass(engine, linked, hub));
        Assert.Equal(0, effects.Modifiers.Probability(linked, ProbabilityKind.Pass));
    }

    /// <summary>El par es de una resolución concreta: otra probabilidad del mismo evento no lo cobra.</summary>
    [Fact]
    public void PairedProbabilityDoesNotLeakIntoOtherResolutions()
    {
        var (engine, ids) = Match(
            "beside",
            """[{ "type": "modifyProbability", "target": "linked", "probability": "pass", "value": 10, "duration": "match" }]""");

        var effects = engine.Effects!;
        var hub = engine.PlayerById(ids[4])!;
        var linked = engine.PlayerById(ids[5])!;

        effects.Publish(MatchStart(engine));
        effects.Publish(Pass(engine, hub, linked));
        Assert.Equal(0, effects.Modifiers.Probability(hub, ProbabilityKind.Tackle));
        Assert.Equal(0, effects.Modifiers.Probability(hub, ProbabilityKind.Dribble));
    }

    /// <summary>Sin candidato para la relación no hay vínculo, y el perk aplica sus elseEffects (§2.4).</summary>
    [Fact]
    public void WithoutLinkTheElseEffectsApply()
    {
        var catalog = TestPerks.CatalogWith(("pair", TestPerks.Json(
            "pair",
            "MATCH_START",
            """[{ "type": "modifyAttribute", "target": "linked", "attribute": "speed", "value": 5, "duration": "match" }]""",
            axis: "alignment",
            links: """["diagonalAhead"]""",
            condition: "linked(owner, 'diagonalAhead')",
            elseEffects: """[{ "type": "modifyAttribute", "target": "owner", "attribute": "speed", "value": -5, "duration": "match" }]""")));

        var (engine, ids) = Match(catalog, "pair");
        var hub = engine.PlayerById(ids[4])!;
        int before = hub.Speed;

        engine.Effects!.Publish(MatchStart(engine));

        Assert.Equal(before - 5, hub.Speed);
        var activation = Assert.Single(engine.Report.PerkActivations, a => a.PerkId == "pair" && a.OwnerId == hub.Id);
        Assert.EndsWith(":else", activation.Detail, StringComparison.Ordinal);
    }

    /// <summary>Un efecto no probabilístico con objetivo vinculado actúa sobre el vinculado, no sobre el par.</summary>
    [Fact]
    public void NonProbabilityEffectsHitTheLinkedPlayer()
    {
        var (engine, ids) = Match(
            "ahead",
            """[{ "type": "modifyAttribute", "target": "linked", "attribute": "speed", "value": 5, "duration": "match" }]""");

        var hub = engine.PlayerById(ids[4])!;
        var linked = engine.PlayerById(ids[6])!;
        int hubBefore = hub.Speed;
        int linkedBefore = linked.Speed;

        engine.Effects!.Publish(MatchStart(engine));

        Assert.Equal(hubBefore, hub.Speed);
        Assert.Equal(linkedBefore + 5, linked.Speed);
    }

    /// <summary>Sin ningún perk que declare relaciones, la tabla de vínculos no llega a construirse (§2.4).</summary>
    [Fact]
    public void NoPerkWithLinksMeansNoLinkTable()
    {
        var catalog = TestPerks.CatalogWith(("plain", TestPerks.Json("plain", "MATCH_START", Speed1)));
        var setup = WithCells(TestPerks.Match(catalog, 7, (0, new[] { "plain" })));
        var engine = TestPerks.Engine(catalog, setup);
        Assert.Null(engine.Effects!.Links);
    }

    private static LinkRelation Relation(string name) =>
        (LinkRelation)Array.IndexOf(new[] { "beside", "ahead", "behind", "left", "right", "diagonalAhead", "diagonalBehind" }, name);

    private static (MatchEngine Engine, int[] Ids) Match(string relation, string? effects = null)
    {
        var catalog = TestPerks.CatalogWith(("pair", TestPerks.Json(
            "pair",
            "MATCH_START",
            effects ?? """[{ "type": "modifyAttribute", "target": "linked", "attribute": "speed", "value": 1, "duration": "match" }]""",
            axis: "alignment",
            links: $"""["{relation}"]""")));

        return Match(catalog, "pair");
    }

    private static (MatchEngine Engine, int[] Ids) Match(Catalog catalog, string perkId)
    {
        var baseSetup = TestPerks.Match(catalog, 7);
        var ids = baseSetup.Home.Lineup.Slots.Select(s => s.PlayerId).ToArray();
        var setup = WithCells(baseSetup);
        setup = setup with
        {
            Home = WithPerk(setup.Home, ids[4], perkId),
            Away = WithPerk(setup.Away, ids[4] + 100, perkId),
        };

        return (TestPerks.Engine(catalog, setup), ids);
    }

    private static MatchSetup WithCells(MatchSetup setup) => setup with
    {
        Home = setup.Home with { Lineup = Relocate(setup.Home.Lineup) },
        Away = setup.Away with { Lineup = Relocate(setup.Away.Lineup) },
    };

    private static Lineup Relocate(Lineup lineup) => new(
        lineup.Slots.Select((slot, i) => slot with { HomeCell = Cells[i] }).ToList());

    private static TeamSetup WithPerk(TeamSetup team, int playerId, string perkId) => team with
    {
        Players = team.Players.Select(p => p.Id == playerId ? p with { Perks = new[] { perkId } } : p).ToList(),
    };

    private static MatchEvent MatchStart(MatchEngine engine) => new(
        EventType.MatchStart, engine.Tick, -1, -1, -1, -1,
        new Cell(0, 0), Zone.Middle, MatchPhase.Kickoff, 0, 0, "kickoff");

    private static MatchEvent Pass(MatchEngine engine, MatchPlayer passer, MatchPlayer receiver) => new(
        EventType.PassAttempted, engine.Tick, passer.Team, passer.Id, receiver.Id, -1,
        passer.HomeCell, Zone.Middle, MatchPhase.OpenPlay, 0, 0, "attempted");
}
