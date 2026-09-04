using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Medical;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>RF-092, RF-094: la clínica cura la lesión grave a coste fijo y resultado garantizado.</summary>
public sealed class MedicalTests
{
    [Fact]
    public void TreatingASeverelyInjuredPlayerAlwaysCuresAtTheFixedCost()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 555UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 50);
        var injured = state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury };
        state = state.WithPlayer(injured);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);

        var healed = MedicalSystem.Treat(state, new TreatPlayer(injured.Id), economy);

        Assert.Equal(PhysicalState.Healthy, healed.GetPlayer(injured.Id).PhysicalState);
        Assert.Equal(0, healed.GetPlayer(injured.Id).MinorInjuries);
        Assert.Equal(state.Gold - economy.ClinicCost, healed.Gold);
    }

    [Fact]
    public void TreatingAHealthyPlayerIsRejected()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 556UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 50);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);
        var healthy = state.Roster.First(p => p.PhysicalState == PhysicalState.Healthy);

        Assert.Throws<ArgumentException>(() => MedicalSystem.Treat(state, new TreatPlayer(healthy.Id), economy));
    }

    [Fact]
    public void TreatingWithoutEnoughGoldIsRejected()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 557UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost - 1);
        var injured = state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury };
        state = state.WithPlayer(injured);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);

        Assert.Throws<ArgumentException>(() => MedicalSystem.Treat(state, new TreatPlayer(injured.Id), economy));
    }

    [Fact]
    public void TreatingOutsideAClinicIsRejected()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 558UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 50);
        var injured = state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury };
        state = state.WithPlayer(injured);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Market);

        Assert.Throws<InvalidOperationException>(() => MedicalSystem.Treat(state, new TreatPlayer(injured.Id), economy));
    }
}
