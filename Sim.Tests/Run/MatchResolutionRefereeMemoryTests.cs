using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0158 §4: al terminar un partido que pitó, el árbitro pasa a tener <c>Grudge</c> = mitad del
/// criterio final, acotado a ±40; el siguiente partido con ese árbitro empieza ahí
/// (<c>IRunSystems.RefereeFor</c>). Solo cambia el árbitro de ESE nodo.
/// </summary>
public sealed class MatchResolutionRefereeMemoryTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static RunState BaseState(ulong seed = 555UL) =>
        RunEngine.Start(TestRuns.Setup(), seed, Catalog);

    private static MapNode Node(int id = 101) =>
        new(id, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

    private static MatchLineup LineupFor(IReadOnlyList<RunPlayer> starters)
    {
        var startDefs = starters.Select(p => p.ToDefinition(Catalog)).ToList();
        return new MatchLineup(startDefs, Array.Empty<PlayerDefinition>(), Lineup.Default(startDefs), EmergencyGoalkeeperId: -1);
    }

    private static PlayerMatchStats Stats(int playerId, int team, int ticksOnPitch = 900) =>
        new(playerId, team, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, ticksOnPitch, 0, 0);

    private static MatchResult ResultWith(IReadOnlyList<PlayerMatchStats> stats, int finalBias)
    {
        var builder = new MatchReportBuilder();
        builder.Winner = 0;
        builder.Ticks = 900;
        builder.FinalBias = finalBias;
        builder.Players.AddRange(stats);
        return new MatchResult(Array.Empty<MatchEvent>(), builder.Build(), Array.Empty<PlayerCounterDelta>());
    }

    [Fact]
    public void TheRefereeWhoCalledItRemembersHalfTheFinalBias()
    {
        var state = BaseState().WithReferees(new[] { new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" } });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        var stats = new[] { Stats(starter.Id, 0) };
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0);

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, -30), Catalog, setup);

        Assert.Equal(-15, applied.State.Referees[0].Grudge);
    }

    /// <summary>El rango de <c>Grudge</c> es -40..40 aunque <c>FinalBias</c> llegue a ±100 (ADR 0158 §4).</summary>
    [Fact]
    public void TheGrudgeIsClampedToFortyEvenWithAnExtremeFinalBias()
    {
        var state = BaseState().WithReferees(new[] { new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" } });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        var stats = new[] { Stats(starter.Id, 0) };
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0);

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, 100), Catalog, setup);

        Assert.Equal(40, applied.State.Referees[0].Grudge);
    }

    /// <summary>Solo cambia el árbitro que pitó ESTE partido; el resto del plantel se queda igual.</summary>
    [Fact]
    public void OnlyTheRefereeOfThisNodeChanges()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" },
            new RunReferee(1, "Sor Paciencia", RefereeTrait.Neutral, 0) { DefinitionId = "sor_paciencia", Grudge = 12 },
        });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        var stats = new[] { Stats(starter.Id, 0) };
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0);

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, 20), Catalog, setup);

        Assert.Equal(10, applied.State.Referees[0].Grudge);
        Assert.Equal(12, applied.State.Referees[1].Grudge);
    }

    /// <summary>La turba también deja memoria (ADR 0158 §4: "si el partido fue sin árbitro relevante, usa igualmente el FinalBias").</summary>
    [Fact]
    public void AGoldenGoalMatchStillLeavesMemory()
    {
        var state = BaseState().WithReferees(new[] { new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" } });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        var stats = new[] { Stats(starter.Id, 0) };
        var builder = new MatchReportBuilder { Winner = 0, Ticks = 900, FinalBias = 16, WentToGoldenGoal = true };
        builder.Players.AddRange(stats);
        var result = new MatchResult(Array.Empty<MatchEvent>(), builder.Build(), Array.Empty<PlayerCounterDelta>());
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0);

        var applied = MatchResolution.Apply(state, Node(), lineup, result, Catalog, setup);

        Assert.Equal(8, applied.State.Referees[0].Grudge);
    }

    /// <summary>Sin árbitro (llamadores que no lo necesitan, como el resto de tests de este paquete), no se toca nada.</summary>
    [Fact]
    public void WithoutARefereeSetupNothingChanges()
    {
        var state = BaseState().WithReferees(new[] { new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" } });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        var stats = new[] { Stats(starter.Id, 0) };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, 90), Catalog);

        Assert.Equal(0, applied.State.Referees[0].Grudge);
    }

    [Fact]
    public void TheNextRefereeForStartsFromTheRememberedGrudge()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto", Grudge = -15 },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(-15, setup.InitialBias);
    }

    /// <summary>ADR 0158 §2/4: el casero arranca en Grudge - 20 (RF-061, enmienda de "casero").</summary>
    [Fact]
    public void HomerStartsTwentyBelowItsGrudge()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Forastero", RefereeTrait.Homer, 0) { DefinitionId = "forastero_cadwallon", Grudge = 5 },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(-15, setup.InitialBias);
    }

    /// <summary>El primer partido de la run, sin memoria previa (Grudge 0), un casero arranca exactamente en -20.</summary>
    [Fact]
    public void HomerStartsAtMinusTwentyWithNoMemory()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Forastero", RefereeTrait.Homer, 0) { DefinitionId = "forastero_cadwallon" },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(-20, setup.InitialBias);
    }

    /// <summary>El lado ciego del tuerto viaja del plantel de la run al <c>RefereeSetup</c> del partido.</summary>
    [Fact]
    public void RefereeForCarriesTheBlindSideIntoTheSetup()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Tuerto", RefereeTrait.OneEyed, 0) { DefinitionId = "tuerto_balbuena", BlindSide = RefereeSide.Top },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(RefereeSide.Top, setup.BlindSide);
    }
}
