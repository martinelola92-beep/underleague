using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Data;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems.Events;
using Underleague.Sim.Run.View;
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

    [Fact]
    public void HasFreeProsthesisSlotIsFalseAtThreeOutOfThreeEvenWithFreeSlots()
    {
        var prostheses = SystemsTestSupport.Systems.Prostheses;
        var three = Clinic(3, PhysicalState.Healthy).Patient;
        Assert.True(prostheses.Slots.Count > 3, "debe quedar ranura libre en el catálogo: el tope es el que lo impide");
        Assert.False(MedicalSystem.HasFreeProsthesisSlot(three, prostheses));
        Assert.True(MedicalSystem.HasFreeProsthesisSlot(WithProstheses(three with { Prostheses = Array.Empty<RunProsthesis>(), Tags = Array.Empty<string>() }, 2), prostheses));
    }

    [Fact]
    public void ACrippledPlayerPlacedByHandInTheLineupIsReplacedAndDoesNotPlay()
    {
        var (state, crippled) = Clinic(3, PhysicalState.SevereInjury);
        var hand = new Lineup(state.Lineup.Slots.Where(s => s.PlayerId != crippled.Id).Take(6)
            .Append(new LineupSlot(crippled.Id, new Cell(5, 0))).ToList());
        Assert.Contains(hand.Slots, s => s.PlayerId == crippled.Id);

        var effective = RunLineup.Effective(state, hand);
        Assert.DoesNotContain(effective.Lineup.Slots, s => s.PlayerId == crippled.Id);
        Assert.Equal(RunRules.MaxStarters, effective.Lineup.Slots.Count);

        var confirmed = RunEngine.Apply(state, new SetLineup(hand), SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        Assert.DoesNotContain(RunLineup.Build(confirmed, null).Lineup.Slots, s => s.PlayerId == crippled.Id);
        var warnings = RunEngine.LineupWarnings(state, hand);
        Assert.DoesNotContain(warnings, w => w.PlayerId == crippled.Id && w.Kind == LineupWarningKind.SevereInjuryDeathRisk);
    }

    [Fact]
    public void TheLineupWarnsAStarterAtThreeOutOfThree_AndNotOneWithTwo()
    {
        var (state, capped) = Clinic(3, PhysicalState.Healthy);
        var lineup = state.Lineup.Slots.Any(s => s.PlayerId == capped.Id)
            ? state.Lineup
            : new Lineup(state.Lineup.Slots.Take(6).Append(new LineupSlot(capped.Id, new Cell(5, 0))).ToList());
        Assert.Contains(RunEngine.LineupWarnings(state, lineup), w => w.Kind == LineupWarningKind.ProsthesisCapRisk && w.PlayerId == capped.Id);

        var (state2, control) = Clinic(2, PhysicalState.Healthy);
        var lineup2 = state2.Lineup.Slots.Any(s => s.PlayerId == control.Id)
            ? state2.Lineup
            : new Lineup(state2.Lineup.Slots.Take(6).Append(new LineupSlot(control.Id, new Cell(5, 0))).ToList());
        Assert.DoesNotContain(RunEngine.LineupWarnings(state2, lineup2), w => w.Kind == LineupWarningKind.ProsthesisCapRisk);
    }

    private static EventScreenView EventView(RunState state, params EventEffect[] effects)
    {
        var option = new EventOption("o", new LocalizedName("o", "o"), effects, true, false);
        var events = new EventCatalog(new[] { new EventCard("c", new LocalizedName("c", "c"), new LocalizedName("c", "c"), 1, 1, new[] { option }) });
        return Underleague.Sim.Run.View.EventView.Build(
            state, SystemsTestSupport.Catalog, events, SystemsTestSupport.Systems.Items, SystemsTestSupport.Systems.Consumables, "es")!;
    }

    [Fact]
    public void ASevereInjuryEventWarnsTheTargetAtThreeOutOfThree_AndAMinorOneDoesNot()
    {
        var start = RunEngine.Start(SystemsTestSupport.Setup(), 7, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        var capped = WithProstheses(start.Roster[1], 3);
        var state = SystemsTestSupport.WithFakePendingNode(start.WithPlayer(capped), NodeKind.Event, 0);
        var severe = EventView(state, new EventEffect(EventEffectKind.Injure, 2)).Options[0].Targets.Single(t => t.PlayerId == capped.Id);
        Assert.Contains("lisiado", severe.Detail, StringComparison.Ordinal);
        var other = EventView(state, new EventEffect(EventEffectKind.Injure, 2)).Options[0].Targets.First(t => t.PlayerId != capped.Id);
        Assert.DoesNotContain("lisiado", other.Detail, StringComparison.Ordinal);
        var minor = EventView(state, new EventEffect(EventEffectKind.Injure, 1)).Options[0].Targets.Single(t => t.PlayerId == capped.Id);
        Assert.DoesNotContain("lisiado", minor.Detail, StringComparison.Ordinal);
    }
}
