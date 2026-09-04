using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;

namespace Underleague.Sim.Tests.Run;

/// <summary>Modo de depuración (RT-062): construir un estado arbitrario sin jugar los nodos previos.</summary>
public class RunDebugTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void ADebugStateJumpsToActTwoWithAGivenRoster()
    {
        // El ejemplo literal de RT-062: "acto 2 con una plantilla concreta".
        var start = RunEngine.Start(TestRuns.Setup(), 1001, Catalog);
        var roster = start.Roster.Take(7).Select(p => p with { Level = 5 }).ToList();

        var state = RunStateBuilder.From(TestRuns.Setup(), 1001, Catalog)
            .AtAct(2)
            .WithGold(750)
            .WithRoster(roster)
            .Build();

        Assert.Equal(2, state.Act);
        Assert.Equal(-1, state.CurrentNodeId);
        Assert.Empty(state.NodeHistory);
        Assert.Equal(750, state.Gold);
        Assert.Equal(7, state.Roster.Count);
        Assert.All(state.Roster, p => Assert.Equal(5, p.Level));
        Assert.Equal(7, state.AvailablePlayerCount);
        Assert.Equal(RunPhase.OnMap, state.Phase);

        // Y es jugable de verdad: los nodos accesibles son los de entrada del acto 2.
        var nodes = RunEngine.AvailableNodes(state);
        Assert.NotEmpty(nodes);
        Assert.All(nodes, n => Assert.Equal(2, n.Act));
        Assert.Equal(state.MapOf(2).EntryNodeIds, nodes.Select(n => n.Id).ToList());
    }

    [Fact]
    public void ADebugStateCanSitOnAnyNodeOfItsAct()
    {
        var state = RunStateBuilder.From(TestRuns.Setup(), 2002, Catalog).AtAct(3).Build();
        var target = state.CurrentMap.Nodes.First(n => n.Layer == 1);

        state = RunStateBuilder.From(state).AtNode(target.Id).Build();

        Assert.Equal(target.Id, state.CurrentNodeId);
        Assert.Equal(target.Next, RunEngine.AvailableNodes(state).Select(n => n.Id).ToList());

        // Un nodo de otro acto no vale: el estado de depuración sigue siendo consistente.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RunStateBuilder.From(state).AtNode(state.MapOf(1).BossNodeId).Build());
    }

    [Fact]
    public void BeforeBoss_LeavesTheBossAsTheOnlyThingLeft()
    {
        var state = RunStateBuilder.From(TestRuns.Setup(), 3003, Catalog).AtAct(2).BeforeBoss().Build();
        var nodes = RunEngine.AvailableNodes(state);

        Assert.Contains(nodes, n => n.Id == state.CurrentMap.BossNodeId);
        Assert.All(nodes, n => Assert.Equal(NodeKind.Boss, n.Kind));
    }

    [Fact]
    public void WithAvailablePlayers_LandsExactlyOnTheMinimum()
    {
        for (int available = 5; available <= 8; available++)
        {
            var state = RunStateBuilder.From(TestRuns.Setup(), 4004, Catalog)
                .WithAvailablePlayers(available)
                .Build();

            Assert.Equal(available, state.AvailablePlayerCount);   // RF-002e
            Assert.False(state.IsBelowMinimum);
        }

        var below = RunStateBuilder.From(TestRuns.Setup(), 4004, Catalog).WithAvailablePlayers(4).Build();
        Assert.True(below.IsBelowMinimum);
        Assert.Equal(DefeatCause.NotEnoughPlayers, RunEngine.Outcome(below).Cause);
    }

    [Fact]
    public void WithPlayerState_SetsTheStateAndItsCounters()
    {
        var state = RunStateBuilder.From(TestRuns.Setup(), 5005, Catalog)
            .WithPlayerState(2, PhysicalState.MinorInjury)
            .WithPlayerState(3, PhysicalState.Dead)
            .Build();

        Assert.Equal(PhysicalState.MinorInjury, state.GetPlayer(2).PhysicalState);
        Assert.True(state.GetPlayer(2).MinorInjuries >= 1);
        Assert.True(state.GetPlayer(2).IsAvailable);
        Assert.False(state.GetPlayer(3).IsAvailable);
        Assert.Equal(9, state.AvailablePlayerCount);
    }

    [Fact]
    public void ADebugStateSurvivesTheSaveFormat()
    {
        // La otra vía de RT-062: /Game y /Balance cargan un estado predefinido con --state.
        var state = RunStateBuilder.From(TestRuns.Setup(), 6006, Catalog)
            .AtAct(2)
            .BeforeBoss()
            .WithGold(1200)
            .WithAvailablePlayers(6)
            .Build();

        var loaded = RunSave.Load(RunSave.Save(state, indented: true));

        Assert.Equal(state.Act, loaded.Act);
        Assert.Equal(state.CurrentNodeId, loaded.CurrentNodeId);
        Assert.Equal(state.Gold, loaded.Gold);
        Assert.Equal(6, loaded.AvailablePlayerCount);
        Assert.Equal(
            RunEngine.AvailableNodes(state).Select(n => n.Id).ToList(),
            RunEngine.AvailableNodes(loaded).Select(n => n.Id).ToList());
    }
}
