using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Nodes;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// RF-020 y ADR 0046: la plantilla base es de diez y solo crece comprando huecos en un nodo de
/// inscripción, hasta el techo de doce. Es la regla que hace del desgaste un recurso (ADR 0045): con la
/// plantilla llena, fichar exige vender o descartar.
/// </summary>
public sealed class EnrollmentTests
{
    private static RunState Start(ulong seed, int gold = 400) =>
        RunEngine.Start(SystemsTestSupport.Setup(startingGold: gold), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

    /// <summary>RF-005 + RF-020: el club empieza con diez y diez es exactamente su techo.</summary>
    [Fact]
    public void TheStartingClubIsAlreadyAtItsRosterCap()
    {
        var state = Start(9001UL);

        Assert.Equal(RunRules.BaseRosterSize, state.Roster.Count);
        Assert.Equal(RunRules.BaseRosterSize, state.RosterSize);
        Assert.Equal(RunRules.BaseRosterSize, state.RosterCapacity);
        Assert.False(state.HasRosterSpace);
        Assert.Equal(RunRules.MaxEnrollmentSlots, state.EnrollmentSlotsLeft);
    }

    /// <summary>
    /// El embudo único: con la plantilla llena no entra nadie por ningún camino. Se comprueba sobre
    /// <c>WithNewPlayer</c> porque es por donde pasan mercado, canteranos, mercenarios y recompensas.
    /// </summary>
    [Fact]
    public void NobodyJoinsAFullRoster()
    {
        var state = Start(9002UL);
        var newcomer = state.Roster[0] with { Id = -1 };

        var error = Assert.Throws<InvalidOperationException>(() => state.WithNewPlayer(newcomer));
        Assert.Contains("RF-020", error.Message, StringComparison.Ordinal);
    }

    /// <summary>ADR 0046: el hueco cuesta oro, sube el techo en uno y su precio es creciente.</summary>
    [Fact]
    public void BuyingASlotRaisesTheCapAndCostsMoreEachTime()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = SystemsTestSupport.WithFakePendingNode(Start(9003UL), NodeKind.Enrollment);

        int firstCost = economy.EnrollmentCost(0);
        int goldBefore = state.Gold;
        state = EnrollmentSystem.Expand(state, economy);

        Assert.Equal(goldBefore - firstCost, state.Gold);
        Assert.Equal(RunRules.BaseRosterSize + 1, state.RosterCapacity);
        Assert.True(state.HasRosterSpace);

        int secondCost = economy.EnrollmentCost(1);
        Assert.True(secondCost > firstCost, $"el segundo hueco cuesta {secondCost} y el primero {firstCost}: debe ser creciente");

        state = EnrollmentSystem.Expand(state, economy);
        Assert.Equal(RunRules.MaxRosterSize, state.RosterCapacity);
        Assert.Equal(0, state.EnrollmentSlotsLeft);

        // Y no hay un tercero: doce es el techo de RF-020.
        Assert.Throws<InvalidOperationException>(() => EnrollmentSystem.Expand(state, economy));
    }

    [Fact]
    public void BuyingASlotWithoutEnoughGoldIsRejected()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = SystemsTestSupport.WithFakePendingNode(Start(9004UL), NodeKind.Enrollment)
            .WithGold(economy.EnrollmentCost(0) - 1);

        Assert.Throws<ArgumentException>(() => EnrollmentSystem.Expand(state, economy));
    }

    /// <summary>La otra mitad de la regla: descartar libera el hueco sin cobrar nada.</summary>
    [Fact]
    public void ReleasingAPlayerFreesTheSlot()
    {
        var state = Start(9005UL);
        int goldBefore = state.Gold;
        int victim = state.Roster[^1].Id;

        var next = EnrollmentSystem.Release(state, new ReleasePlayer(victim));

        Assert.Null(next.FindPlayer(victim));
        Assert.True(next.HasRosterSpace);
        Assert.Equal(goldBefore, next.Gold);
    }

    /// <summary>RF-002b: descartar no puede ser una forma de perder la run desde un menú.</summary>
    [Fact]
    public void ReleasingCannotDropBelowTheMinimum()
    {
        var state = Start(9006UL);
        var roster = state.Roster.ToList();
        for (int i = 0; i < roster.Count - RunRules.MinimumAvailablePlayers; i++)
        {
            state = state.WithPlayer(roster[i] with { PhysicalState = PhysicalState.SevereInjury });
        }

        Assert.Equal(RunRules.MinimumAvailablePlayers, state.AvailablePlayerCount);
        var last = state.Roster.First(p => p.IsAvailable);
        Assert.Throws<ArgumentException>(() => EnrollmentSystem.Release(state, new ReleasePlayer(last.Id)));
    }

    /// <summary>RF-093 y RF-122: el muerto ya no ocupa plantilla, así que su hueco queda libre solo.</summary>
    [Fact]
    public void ADeadPlayerNoLongerTakesUpRoomButStaysInTheRosterForTheMemorial()
    {
        var state = Start(9007UL);
        var fallen = state.Roster[0];
        state = state.WithPlayer(fallen with { PhysicalState = PhysicalState.Dead });

        Assert.Equal(RunRules.BaseRosterSize, state.Roster.Count);
        Assert.Equal(RunRules.BaseRosterSize - 1, state.RosterSize);
        Assert.True(state.HasRosterSpace);
        Assert.NotNull(state.FindPlayer(fallen.Id));
        Assert.Throws<ArgumentException>(() => EnrollmentSystem.Release(state, new ReleasePlayer(fallen.Id)));
    }

    /// <summary>El nodo se abre y se resuelve por la superficie pública del motor, no solo por el sistema.</summary>
    [Fact]
    public void TheEnrollmentNodeIsPlayedThroughTheRunEngine()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = SystemsTestSupport.WithFakePendingNode(Start(9008UL), NodeKind.Enrollment).WithPhase(RunPhase.NodeOpen);

        state = RunEngine.Apply(state, new ExpandRoster(), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.Equal(RunRules.BaseRosterSize + 1, state.RosterCapacity);

        state = RunEngine.Apply(state, new LeaveNode(), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.Equal(RunPhase.OnMap, state.Phase);
    }
}
