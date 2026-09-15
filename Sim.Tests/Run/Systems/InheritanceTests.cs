using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// El traspaso de atributos de "Herencia" (paquete BB, primitiva "run-level: oro y atributos al salir de
/// la plantilla", <c>docs/analisis/perks-catalogo-unificado.md</c> §3.2): lo que era del muerto pasa, en
/// parte, al compañero vinculado. Dos niveles: <see cref="InheritanceSystem.Apply"/> a solas (el traspaso
/// en sí, sin pasar por un partido) y <c>MatchResolution</c> completo (quién es "el vinculado", resuelto
/// con la misma geometría estática que el motor).
/// </summary>
public sealed class InheritanceTests
{
    private static readonly ulong Seed = 130713UL;

    private static RunState RunTestState() =>
        RunEngine.Start(SystemsTestSupport.Setup(), Seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

    private static EconomyConfig EconomyWithInheritance(string perkId, int percent, int maxPerAttribute)
    {
        var table = InheritanceTable.FromJson(new Dictionary<string, string>
        {
            [InheritanceTable.Path] =
                $$"""{ "transfers": { "{{perkId}}": { "percent": {{percent}}, "maxPerAttribute": {{maxPerAttribute}} } } } """,
        });

        return SystemsTestSupport.Systems.Economy with { Inheritance = table };
    }

    private static RunMatchSummary SummaryWithDeath(PlayerDeathDetail detail)
    {
        var builder = new MatchReportBuilder();
        builder.Goals[0] = 1;
        builder.Winner = 0;
        builder.Ticks = 500;
        var report = builder.Build();

        return new RunMatchSummary(
            NodeId: 101,
            Kind: NodeKind.LeagueMatch,
            Won: true,
            GoalsFor: 1,
            GoalsAgainst: 0,
            Ticks: 500,
            WentToGoldenGoal: false,
            PlayedPlayerIds: Array.Empty<int>(),
            BenchedPlayerIds: Array.Empty<int>(),
            OwnInjuries: 0,
            OwnDeaths: 1,
            Report: report)
        {
            DeathDetails = new[] { detail },
        };
    }

    // ---------------------------------------------------------------- InheritanceSystem.Apply a solas

    /// <summary>El heredero sale del partido con más atributo, y el muerto no cambia el suyo.</summary>
    [Fact]
    public void TheHeirGetsMoreAttributeAndTheDeadDoesNot()
    {
        var state = RunTestState();
        var dead = state.Roster[0];
        var heir = state.Roster[1];

        const int Percent = 20;
        const int MaxPerAttribute = 8;
        var economy = EconomyWithInheritance("inheritance", Percent, MaxPerAttribute);
        var summary = SummaryWithDeath(new PlayerDeathDetail(dead.Id, new[] { "inheritance" }, heir.Id));

        var next = InheritanceSystem.Apply(state, summary, economy);

        var expected = Attributes.Clamp(new Attributes(
            heir.Attributes.Strength + Math.Min(dead.Attributes.Strength * Percent / 100, MaxPerAttribute),
            heir.Attributes.Speed + Math.Min(dead.Attributes.Speed * Percent / 100, MaxPerAttribute),
            heir.Attributes.Technique + Math.Min(dead.Attributes.Technique * Percent / 100, MaxPerAttribute),
            heir.Attributes.Stamina + Math.Min(dead.Attributes.Stamina * Percent / 100, MaxPerAttribute),
            heir.Attributes.Leash + Math.Min(dead.Attributes.Leash * Percent / 100, MaxPerAttribute)));

        Assert.Equal(expected, next.GetPlayer(heir.Id).Attributes);
        Assert.True(next.GetPlayer(heir.Id).Attributes.Average >= heir.Attributes.Average);

        // El muerto no hereda de sí mismo: sus propios atributos no cambian.
        Assert.Equal(dead.Attributes, next.GetPlayer(dead.Id).Attributes);
    }

    /// <summary>Sin tarifa para ese perk, no hay traspaso: el estado vuelve exactamente igual.</summary>
    [Fact]
    public void WithoutARateNothingIsTransferred()
    {
        var state = RunTestState();
        var dead = state.Roster[0];
        var heir = state.Roster[1];

        var economy = SystemsTestSupport.Systems.Economy with { Inheritance = InheritanceTable.Empty };
        var summary = SummaryWithDeath(new PlayerDeathDetail(dead.Id, new[] { "inheritance" }, heir.Id));

        var next = InheritanceSystem.Apply(state, summary, economy);

        Assert.Equal(heir.Attributes, next.GetPlayer(heir.Id).Attributes);
    }

    /// <summary>Sin vinculado resuelto (-1), no hay traspaso.</summary>
    [Fact]
    public void WithoutALinkedPlayerNothingIsTransferred()
    {
        var state = RunTestState();
        var dead = state.Roster[0];
        var economy = EconomyWithInheritance("inheritance", 20, 8);
        var summary = SummaryWithDeath(new PlayerDeathDetail(dead.Id, new[] { "inheritance" }, -1));

        var next = InheritanceSystem.Apply(state, summary, economy);

        Assert.Equal(state.Roster, next.Roster);
    }

    /// <summary>
    /// Si el vinculado TAMBIÉN ha muerto en este mismo partido, no hay traspaso: no se redirige a un
    /// tercero (decisión del paquete BB).
    /// </summary>
    [Fact]
    public void IfTheLinkedPlayerHasAlsoDiedNothingIsTransferred()
    {
        var state = RunTestState();
        var dead = state.Roster[0];
        var alsoDead = state.Roster[1] with { PhysicalState = PhysicalState.Dead };
        var roster = new List<RunPlayer>(state.Roster) { [1] = alsoDead };
        state = state.WithRoster(roster);

        var economy = EconomyWithInheritance("inheritance", 20, 8);
        var summary = SummaryWithDeath(new PlayerDeathDetail(dead.Id, new[] { "inheritance" }, alsoDead.Id));

        var next = InheritanceSystem.Apply(state, summary, economy);

        Assert.Equal(alsoDead.Attributes, next.GetPlayer(alsoDead.Id).Attributes);
    }

    // ---------------------------------------------------------------- MatchResolution: quién es "el vinculado"

    /// <summary>
    /// <c>MatchResolution</c> resuelve el vinculado con la MISMA geometría estática que el motor
    /// (<c>LinkGeometry</c>), a partir de <c>PerkDefinition.Links</c> del perk del muerto: dos titulares
    /// "Beside" (misma columna, fila contigua) en la alineación inicial, uno de ellos muere con un perk
    /// que declara <c>links: ["Beside"]</c>, y el otro sale resuelto como su vinculado.
    /// </summary>
    [Fact]
    public void MatchResolutionResolvesTheLinkedTeammateWithTheSameGeometryAsTheEngine()
    {
        const string PerkId = "inheritance_test_link";
        var catalog = TestPerks.CatalogWith(
            (PerkId, TestPerks.Json(
                PerkId, "MATCH_START", """[{ "type": "modifyBias", "value": 0 }]""", links: "[\"beside\"]")));

        var state = RunEngine.Start(SystemsTestSupport.Setup(), Seed, catalog, SystemsTestSupport.Systems);
        var dead = state.Roster[0] with { Perks = new[] { PerkId } };
        var heir = state.Roster[1];
        var roster = new List<RunPlayer>(state.Roster) { [0] = dead };
        state = state.WithRoster(roster);

        // Beside: misma columna, fila contigua.
        var deadCell = new Cell(2, 2);
        var heirCell = new Cell(2, 3);
        var lineup = new MatchLineup(
            new[] { dead.ToDefinition(catalog), heir.ToDefinition(catalog) },
            Array.Empty<PlayerDefinition>(),
            new Lineup(new[] { new LineupSlot(dead.Id, deadCell), new LineupSlot(heir.Id, heirCell) }),
            EmergencyGoalkeeperId: -1);

        var builder = new MatchReportBuilder();
        builder.Winner = 0;
        builder.Ticks = 200;
        var report = builder.Build();
        var events = new[]
        {
            new MatchEvent(
                EventType.Death, 100, 0, dead.Id, -1, -1, deadCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, string.Empty),
        };
        var result = new MatchResult(events, report, Array.Empty<PlayerCounterDelta>());
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

        var applied = MatchResolution.Apply(state, node, lineup, result, catalog);

        var detail = Assert.Single(applied.Summary.DeathDetails);
        Assert.Equal(dead.Id, detail.PlayerId);
        Assert.Contains(PerkId, detail.Perks);
        Assert.Equal(heir.Id, detail.LinkedPlayerId);
    }

    /// <summary>Un perk sin <c>links</c> declarado nunca resuelve vinculado: -1.</summary>
    [Fact]
    public void APerkWithoutLinksNeverResolvesALinkedTeammate()
    {
        const string PerkId = "no_link_test_perk";
        var catalog = TestPerks.CatalogWith(
            (PerkId, TestPerks.Json(PerkId, "MATCH_START", """[{ "type": "modifyBias", "value": 0 }]""")));

        var state = RunEngine.Start(SystemsTestSupport.Setup(), Seed, catalog, SystemsTestSupport.Systems);
        var dead = state.Roster[0] with { Perks = new[] { PerkId } };
        var heir = state.Roster[1];
        var roster = new List<RunPlayer>(state.Roster) { [0] = dead };
        state = state.WithRoster(roster);

        var deadCell = new Cell(2, 2);
        var heirCell = new Cell(2, 3);
        var lineup = new MatchLineup(
            new[] { dead.ToDefinition(catalog), heir.ToDefinition(catalog) },
            Array.Empty<PlayerDefinition>(),
            new Lineup(new[] { new LineupSlot(dead.Id, deadCell), new LineupSlot(heir.Id, heirCell) }),
            EmergencyGoalkeeperId: -1);

        var builder = new MatchReportBuilder();
        builder.Winner = 0;
        builder.Ticks = 200;
        var report = builder.Build();
        var events = new[]
        {
            new MatchEvent(
                EventType.Death, 100, 0, dead.Id, -1, -1, deadCell, Zone.Own, MatchPhase.OpenPlay, 0, 0, string.Empty),
        };
        var result = new MatchResult(events, report, Array.Empty<PlayerCounterDelta>());
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

        var applied = MatchResolution.Apply(state, node, lineup, result, catalog);

        var detail = Assert.Single(applied.Summary.DeathDetails);
        Assert.Equal(-1, detail.LinkedPlayerId);
    }
}
