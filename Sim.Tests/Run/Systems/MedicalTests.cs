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
    /// <summary>AZ-G (ADR 0090): la clínica cura también la leve, a su propio precio, menor que el de la grave.</summary>
    [Fact]
    public void TreatingAMinorInjuryCuresItAtTheMinorCost()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 555UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 50);
        var bruised = state.Roster[0] with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 2 };
        state = state.WithPlayer(bruised);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);

        var healed = MedicalSystem.Treat(state, new TreatPlayer(bruised.Id), economy);

        Assert.True(economy.ClinicMinorCost < economy.ClinicCost);
        Assert.Equal(state.Gold - economy.ClinicMinorCost, healed.Gold);
        Assert.Equal(PhysicalState.Healthy, healed.GetPlayer(bruised.Id).PhysicalState);
        Assert.Equal(0, healed.GetPlayer(bruised.Id).MinorInjuries);
    }

    /// <summary>
    /// ADR 0099: la tarifa plana cura a toda la plantilla y no mira cuántos hay. Con un solo herido es un
    /// mal negocio y con cuatro es el bueno: eso es la decisión que la clínica no tenía.
    /// </summary>
    [Fact]
    public void TreatingTheWholeSquadCuresEveryoneForOneFlatPrice()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 777UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicSquadCost + 10);
        state = state
            .WithPlayer(state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury })
            .WithPlayer(state.Roster[1] with { PhysicalState = PhysicalState.SevereInjury })
            .WithPlayer(state.Roster[2] with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 3 });
        int healthyBefore = state.Roster.Count(p => p.PhysicalState == PhysicalState.Healthy);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);

        var healed = MedicalSystem.TreatSquad(state, economy);

        Assert.Equal(state.Gold - economy.ClinicSquadCost, healed.Gold);
        Assert.Equal(healthyBefore + 3, healed.Roster.Count(p => p.PhysicalState == PhysicalState.Healthy));
        Assert.DoesNotContain(healed.Roster, p => p.MinorInjuries > 0);

        // Pagar por nada no es una jugada: con la plantilla sana, la tarifa plana se rechaza.
        Assert.Throws<ArgumentException>(() => MedicalSystem.TreatSquad(healed, economy));
    }

    /// <summary>
    /// ADR 0099: el matasanos cobra una fracción y no garantiza nada. Sobre una tanda de nodos distintos
    /// salen los tres desenlaces —cura, no cura, empeora— y el oro se va siempre, porque cobra por
    /// intentarlo. El porcentaje se ve antes de elegir (RF-012d), que es lo que lo hace legítimo.
    /// </summary>
    [Fact]
    public void TheQuackIsCheaperAndSometimesMakesItWorse()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        Assert.True(MedicalSystem.RiskyCost(economy.ClinicCost, economy) < economy.ClinicCost);

        int cured = 0, failed = 0, worse = 0;
        for (ulong seed = 4242UL; seed < 4252UL; seed++)
        {
            for (int node = 0; node < 8; node++)
            {
                var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
                    .WithGold(100);
                var injured = state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury };
                state = SystemsTestSupport.WithFakePendingNode(state.WithPlayer(injured), NodeKind.Clinic, node);

                var after = MedicalSystem.Treat(state, new TreatPlayer(injured.Id, Risky: true), economy);

                Assert.Equal(state.Gold - MedicalSystem.RiskyCost(economy.ClinicCost, economy), after.Gold);
                switch (after.GetPlayer(injured.Id).PhysicalState)
                {
                    case PhysicalState.Healthy: cured++; break;
                    case PhysicalState.SevereInjury: failed++; break;
                    case PhysicalState.Dead: worse++; break;
                }
            }
        }

        Assert.True(cured > 0 && failed > 0 && worse > 0, $"curados={cured} fallos={failed} peores={worse}");
        Assert.True(cured > failed, $"el matasanos cura más veces de las que falla: curados={cured} fallos={failed}");
    }

    /// <summary>El mismo nodo y la misma semilla dan la misma tirada, y dos tratamientos seguidos no la repiten (RT-024).</summary>
    [Fact]
    public void TheQuackRollIsReproducibleAndAdvancesWithEachTreatment()
    {
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 909UL, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(200);
        var first = state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury };
        var second = state.Roster[1] with { PhysicalState = PhysicalState.SevereInjury };
        state = SystemsTestSupport.WithFakePendingNode(state.WithPlayer(first).WithPlayer(second), NodeKind.Clinic);

        var once = MedicalSystem.Treat(state, new TreatPlayer(first.Id, Risky: true), economy);
        var again = MedicalSystem.Treat(state, new TreatPlayer(first.Id, Risky: true), economy);
        Assert.Equal(once.GetPlayer(first.Id).PhysicalState, again.GetPlayer(first.Id).PhysicalState);

        var twice = MedicalSystem.Treat(once, new TreatPlayer(second.Id, Risky: true), economy);
        Assert.Equal(1, once.Counter(RunState.ClinicRollsPrefix + once.PendingNodeId));
        Assert.Equal(2, twice.Counter(RunState.ClinicRollsPrefix + twice.PendingNodeId));
    }
}
