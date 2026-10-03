using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems.Medical;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0187 (decisión del revisor del 3 oct 2026): tope de tres prótesis; a partir de ahí, una lesión grave deja
/// al jugador lisiado: sin cura en la clínica, sin alinearse, pero en la plantilla.
/// </summary>
public sealed class CrippledTests
{
    private static readonly string[] Ids = { "iron_arm", "peg_leg", "glass_eye" };

    private static RunPlayer WithProstheses(RunPlayer player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            player = MedicalSystem.Install(player, SystemsTestSupport.Systems.Prostheses.Find(Ids[i])!);
        }

        return player;
    }

    private static (RunState State, RunPlayer Patient) Clinic(int prostheses, PhysicalState physical)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7, SystemsTestSupport.Catalog, SystemsTestSupport.Systems)
            .WithGold(200);
        var patient = WithProstheses(state.Roster[1], prostheses) with { PhysicalState = physical };
        state = SystemsTestSupport.WithFakePendingNode(state.WithPlayer(patient), NodeKind.Clinic, 0);
        return (state, patient);
    }

    [Fact]
    public void ThreeProsthesesPlusSevereInjuryIsCrippled_TwoIsNot_AndHealthyIsNot()
    {
        Assert.True(Clinic(3, PhysicalState.SevereInjury).Patient.IsCrippled);
        Assert.False(Clinic(2, PhysicalState.SevereInjury).Patient.IsCrippled);
        Assert.False(Clinic(3, PhysicalState.Healthy).Patient.IsCrippled);
        Assert.False(Clinic(0, PhysicalState.SevereInjury).Patient.IsCrippled);
    }

    [Fact]
    public void WithTwoProsthesesTheBlacksmithStillInstallsTheThird()
    {
        var (state, patient) = Clinic(2, PhysicalState.SevereInjury);
        var economy = SystemsTestSupport.Systems.Economy;
        var prostheses = SystemsTestSupport.Systems.Prostheses;
        bool installed = false;
        for (ulong seed = 1; seed <= 60 && !installed; seed++)
        {
            var s = state with { Seed = seed };
            var after = MedicalSystem.Forge(s, new ForgePlayer(patient.Id, 0), economy, prostheses).GetPlayer(patient.Id);
            installed = after.Prostheses.Count == 3;
            if (installed)
            {
                Assert.Contains(MedicalSystem.AutomatonTag, after.Tags);
                Assert.Equal(PhysicalState.Healthy, after.PhysicalState);
            }
        }

        Assert.True(installed, "con dos prótesis, alguna tirada del herrero tiene que instalar la tercera");
    }

    [Fact]
    public void ACrippledPlayerCannotBeTreatedByAnyService()
    {
        var (state, patient) = Clinic(3, PhysicalState.SevereInjury);
        var economy = SystemsTestSupport.Systems.Economy;
        Assert.False(MedicalSystem.NeedsTreatment(patient));
        Assert.Throws<ArgumentException>(() => MedicalSystem.Treat(state, new TreatPlayer(patient.Id, false), economy));
        Assert.Throws<ArgumentException>(() => MedicalSystem.Treat(state, new TreatPlayer(patient.Id, true), economy));
        Assert.Throws<ArgumentException>(() =>
            MedicalSystem.Forge(state, new ForgePlayer(patient.Id, 0), economy, SystemsTestSupport.Systems.Prostheses));
        Assert.Throws<ArgumentException>(() => MedicalSystem.TreatSquad(state, economy));
        Assert.Equal(PhysicalState.SevereInjury, state.GetPlayer(patient.Id).PhysicalState);
    }

    [Fact]
    public void ControlASevereWithTwoProsthesesStillHasEveryService()
    {
        var (state, patient) = Clinic(2, PhysicalState.SevereInjury);
        var economy = SystemsTestSupport.Systems.Economy;
        Assert.True(MedicalSystem.NeedsTreatment(patient));
        var treated = MedicalSystem.Treat(state, new TreatPlayer(patient.Id, false), economy);
        Assert.Equal(PhysicalState.Healthy, treated.GetPlayer(patient.Id).PhysicalState);
    }

    [Fact]
    public void ACrippledPlayerCannotStartEvenMarkedAsRisk_ButAControlWithTwoCan()
    {
        var (state, crippled) = Clinic(3, PhysicalState.SevereInjury);
        string key = RunLineup.RiskCounterPrefix + crippled.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(RunLineup.CanStart(state.WithCounter(key, 1), crippled));
        var (state2, control) = Clinic(2, PhysicalState.SevereInjury);
        string key2 = RunLineup.RiskCounterPrefix + control.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(RunLineup.CanStart(state2.WithCounter(key2, 1), control));
        Assert.False(crippled.IsAvailable);
    }

    [Fact]
    public void ACrippledPlayerStaysInTheRosterAndOutOfTheAvailableCount()
    {
        var (state, crippled) = Clinic(3, PhysicalState.SevereInjury);
        Assert.Contains(state.Roster, p => p.Id == crippled.Id);
        var healthy = state.WithPlayer(crippled with { PhysicalState = PhysicalState.Healthy });
        Assert.Equal(healthy.AvailablePlayerCount - 1, state.AvailablePlayerCount);
    }

    [Fact]
    public void AnOldSaveWithAThreeProsthesisSevereInjuryLoadsAndIsCrippled()
    {
        var (state, patient) = Clinic(3, PhysicalState.SevereInjury);
        string json = RunSave.Save(state).Replace("\"schemaVersion\":9", "\"schemaVersion\":8", StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\":8", json, StringComparison.Ordinal);
        var loaded = RunSave.Load(json);
        Assert.True(loaded.GetPlayer(patient.Id).IsCrippled);
        Assert.Equal(3, loaded.GetPlayer(patient.Id).Prostheses.Count);
    }
}
