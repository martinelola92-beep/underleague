using Underleague.Sim.Analysis;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Medical;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>ADR 0164, RF-095: cuándo la política va al herrero y cuándo al médico.</summary>
public sealed class RunPolicyBlacksmithTests
{
    private static StandardRunSystems Systems => SystemsTestSupport.Systems;

    private static (RunState State, RunPlayer Patient) Clinic(int gold, Rarity rarity, int attribute)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7, SystemsTestSupport.Catalog, Systems).WithGold(gold);
        var patient = state.Roster[2] with
        {
            PhysicalState = PhysicalState.SevereInjury,
            Rarity = rarity,
            Attributes = new Attributes(attribute, attribute, attribute, attribute, attribute),
        };
        state = state.WithPlayer(patient);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);
        return (state, patient);
    }

    private static (RunState State, int Forged, int Treated) Visit(RunState state) =>
        RunPolicy.VisitClinicForTest(state, SystemsTestSupport.Catalog, Systems.Economy, Systems, RunPolicyOptions.Default);

    [Fact]
    public void ACommonSevereGoesToTheBlacksmithEvenWhenTheDoctorIsAffordable()
    {
        var economy = Systems.Economy;
        var (state, patient) = Clinic(economy.ClinicCost + 3, Rarity.Common, 90);
        var (after, forged, treated) = Visit(state);
        Assert.Equal(1, forged);
        Assert.Equal(1, treated);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
        Assert.NotEmpty(after.GetPlayer(patient.Id).Prostheses);
        Assert.True(after.Gold >= 0);
    }

    [Fact]
    public void ARareStarterWorthSevereStillGoesToTheDoctor()
    {
        var economy = Systems.Economy;
        var (state, patient) = Clinic(economy.ClinicCost + 3, Rarity.Legendary, 99);
        var (after, forged, treated) = Visit(state);
        Assert.Equal(0, forged);
        Assert.Equal(1, treated);
        Assert.Empty(after.GetPlayer(patient.Id).Prostheses);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
    }

    [Fact]
    public void WithGoldBetweenTheBlacksmithAndTheDoctorTheBlacksmithTreatsTheSevereAndTheQuackKeepsNothingToDo()
    {
        var economy = Systems.Economy;
        int between = MedicalSystem.BlacksmithBasePrice(economy) + 1;
        Assert.True(between < economy.ClinicCost);
        var (state, patient) = Clinic(between, Rarity.Legendary, 99);
        var (after, forged, _) = Visit(state);
        Assert.Equal(1, forged);
        Assert.Equal(PhysicalState.Healthy, after.GetPlayer(patient.Id).PhysicalState);
        Assert.NotEmpty(after.GetPlayer(patient.Id).Prostheses);
    }

    [Fact]
    public void AWholePolicyRunSeesTheProsthesisCatalogAndUsesTheBlacksmith()
    {
        // Regla J: el instrumento se valida contra un caso conocido. Antes RecordingSystems devolvía el catálogo
        // vacío y la política jugaba runs enteras sin ver una sola prótesis; con el catálogo real y la
        // preferencia por el herrero, unas cuantas runs tienen que pasar por él.
        var files = TestData.LoadAllFiles();
        var bosses = Underleague.Sim.Run.Bosses.BossCatalog.FromJson(files);
        int forged = 0;
        int installed = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var setup = Systems.NewRunSetup("blacksmith_club", Race.Human, files) with { GeneratedQuality = 50 };
            var result = RunPolicy.Play(setup, seed, SystemsTestSupport.Catalog, Systems, bosses);
            forged += result.BlacksmithTreatments;
            installed += result.ProsthesesInstalled;
        }

        Assert.True(forged > 0, $"la política no pasó nunca por el herrero: ¿ve el catálogo de prótesis? (protesis {Systems.Prostheses.All.Count})");
        Assert.True(installed >= 0);
    }
}
