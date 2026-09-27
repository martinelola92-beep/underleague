using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0158 §4, revisión independiente: la memoria del árbitro es SOLO la conducta propia del jugador.
/// <c>memoria_nueva = clamp(memoria_vieja − desplazadoEnContra × memoryPercent/100 + (desplazadoEnContra
/// == 0 ? cleanMatchBonus : 0), ±memoryCap)</c>, donde <c>desplazadoEnContra</c> es
/// <c>MatchReport.BiasShiftedAgainst[0]</c> -lo que el motor ya desplazó en contra del equipo 0 mientras
/// hubo árbitro (nunca la turba, nunca lo que hizo el rival, nunca el arranque del casero)-. El
/// emparejamiento con <c>RunState.Referees</c> es por <see cref="RefereeSetup.RefereeId"/>, no por nombre.
/// </summary>
public sealed class MatchResolutionRefereeMemoryTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static RefereeMemoryTuning Memory => Catalog.Tuning.Referee.Memory;

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

    /// <param name="shiftedAgainstPlayer">MatchReport.BiasShiftedAgainst[0] del partido construido.</param>
    private static MatchResult ResultWith(IReadOnlyList<PlayerMatchStats> stats, int shiftedAgainstPlayer, bool wentToGoldenGoal = false)
    {
        var builder = new MatchReportBuilder { Winner = 0, Ticks = 900, WentToGoldenGoal = wentToGoldenGoal };
        builder.BiasShiftedAgainst[0] = shiftedAgainstPlayer;
        builder.Players.AddRange(stats);
        return new MatchResult(Array.Empty<MatchEvent>(), builder.Build(), Array.Empty<PlayerCounterDelta>());
    }

    private static (RunState State, RunPlayer Starter, MatchLineup Lineup) StateWithOneReferee(RunReferee referee, ulong seed = 555UL)
    {
        var state = BaseState(seed).WithReferees(new[] { referee });
        var starter = state.Roster[0];
        return (state, starter, LineupFor(new[] { starter }));
    }

    [Fact]
    public void TheRefereeWhoCalledItRemembersProportionallyToWhatWasShiftedAgainstThePlayer()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 20), Catalog, setup);

        int expected = -(20 * Memory.MemoryPercent / 100);
        Assert.Equal(expected, applied.State.Referees[0].Memory);
    }

    /// <summary>Un partido limpio (0 desplazado en contra) suma el bono, no resta nada.</summary>
    [Fact]
    public void ACleanMatchAddsTheCleanMatchBonus()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 0), Catalog, setup);

        Assert.Equal(Memory.CleanMatchBonus, applied.State.Referees[0].Memory);
    }

    /// <summary>La memoria parte de la que ya tenía el árbitro, no de cero.</summary>
    [Fact]
    public void DecayAppliesOnTopOfThePreviousMemory()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto", Memory = 20 });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 10), Catalog, setup);

        int expected = 20 - (10 * Memory.MemoryPercent / 100);
        Assert.Equal(expected, applied.State.Referees[0].Memory);
    }

    /// <summary>El resultado nunca se sale de ±memoryCap, por mucho que se haya desplazado en contra.</summary>
    [Fact]
    public void TheMemoryNeverGoesBeyondTheCap()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto", Memory = -Memory.MemoryCap });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 999), Catalog, setup);

        Assert.Equal(-Memory.MemoryCap, applied.State.Referees[0].Memory);
    }

    /// <summary>Solo cambia el árbitro que pitó ESTE partido; el resto del plantel se queda igual.</summary>
    [Fact]
    public void OnlyTheRefereeOfThisNodeChanges()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" },
            new RunReferee(1, "Sor Paciencia", RefereeTrait.Neutral, 0) { DefinitionId = "sor_paciencia", Memory = 12 },
        });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 20), Catalog, setup);

        Assert.Equal(-(20 * Memory.MemoryPercent / 100), applied.State.Referees[0].Memory);
        Assert.Equal(12, applied.State.Referees[1].Memory);
    }

    /// <summary>
    /// El emparejamiento es por id: dos árbitros con nombres distintos pero uno de ellos compartiendo por
    /// error el mismo nombre que el setup no debe importar -solo el id manda-.
    /// </summary>
    [Fact]
    public void MatchesByIdNotByName()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Nombre Cualquiera", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto", Memory = 3 },
            new RunReferee(1, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "sor_paciencia", Memory = 3 },
        });
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter });
        // El setup dice "Bartolo", pero el id que pitó de verdad es el 0 (Nombre Cualquiera); solo el 0
        // debe cambiar.
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 0), Catalog, setup);

        Assert.Equal(3 + Memory.CleanMatchBonus, applied.State.Referees[0].Memory);
        Assert.Equal(3, applied.State.Referees[1].Memory);
    }

    /// <summary>
    /// La turba no tiene árbitro (RF-055d): <c>MatchEngine.ShiftBiasAgainst</c> no acumula nada con
    /// IsMob, así que un partido que fue a gol de oro solo deja en <c>BiasShiftedAgainst</c> lo que pasó
    /// EN TIEMPO REGLAMENTARIO. Aquí se construye directamente el report ya con ese valor (0: nada pasó
    /// antes de la turba) para comprobar que WentToGoldenGoal, por sí solo, ya no mueve la memoria -al
    /// contrario que la regla vieja, que leía FinalBias y por tanto sí se enteraba de la turba-.
    /// </summary>
    [Fact]
    public void AGoldenGoalMatchWithNothingShiftedBeforeItLeavesNoMemoryBeyondTheCleanBonus()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(
            state, Node(), lineup,
            ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 0, wentToGoldenGoal: true),
            Catalog, setup);

        Assert.Equal(Memory.CleanMatchBonus, applied.State.Referees[0].Memory);
    }

    /// <summary>
    /// Si SÍ hubo algo en contra antes de la turba, eso -y solo eso- es lo que cuenta; la turba no añade
    /// ni quita nada más allá de lo que ya estaba en BiasShiftedAgainst.
    /// </summary>
    [Fact]
    public void AGoldenGoalMatchOnlyRemembersWhatHappenedBeforeTheMobStarted()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0) { RefereeId = 0 };

        var applied = MatchResolution.Apply(
            state, Node(), lineup,
            ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 12, wentToGoldenGoal: true),
            Catalog, setup);

        Assert.Equal(-(12 * Memory.MemoryPercent / 100), applied.State.Referees[0].Memory);
    }

    /// <summary>Sin árbitro (llamadores que no lo necesitan), no se toca nada.</summary>
    [Fact]
    public void WithoutARefereeSetupNothingChanges()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" });

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 20), Catalog);

        Assert.Equal(0, applied.State.Referees[0].Memory);
    }

    /// <summary>Un RefereeSetup sin id identificable (-1, el valor por defecto) tampoco cambia nada.</summary>
    [Fact]
    public void ARefereeSetupWithoutAnIdChangesNothing()
    {
        var (state, starter, lineup) = StateWithOneReferee(new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto" });
        var setup = new RefereeSetup("Bartolo", RefereeTrait.Neutral, 0);

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0) }, shiftedAgainstPlayer: 20), Catalog, setup);

        Assert.Equal(0, applied.State.Referees[0].Memory);
    }

    [Fact]
    public void TheNextRefereeForStartsFromTheRememberedMemory()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Bartolo", RefereeTrait.Neutral, 0) { DefinitionId = "bartolo_recto", Memory = -15 },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(-15, setup.InitialBias);
        Assert.Equal(0, setup.RefereeId);
    }

    /// <summary>ADR 0158 §2/4: el casero arranca en memoria + tuning.referee.memory.homerInitialBias.</summary>
    [Fact]
    public void HomerStartsBelowItsMemoryByTheTunedAmount()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Forastero", RefereeTrait.Homer, 0) { DefinitionId = "forastero_cadwallon", Memory = 5 },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(5 + Memory.HomerInitialBias, setup.InitialBias);
    }

    /// <summary>El primer partido de la run, sin memoria previa, un casero arranca exactamente en homerInitialBias.</summary>
    [Fact]
    public void HomerStartsAtTheTunedInitialBiasWithNoMemory()
    {
        var state = BaseState().WithReferees(new[]
        {
            new RunReferee(0, "Forastero", RefereeTrait.Homer, 0) { DefinitionId = "forastero_cadwallon" },
        });

        var setup = DefaultRunSystems.Instance.RefereeFor(state, Node(), Catalog);

        Assert.Equal(Memory.HomerInitialBias, setup.InitialBias);
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
