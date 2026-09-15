using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Medical;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// La exención de la factura de clínica (perk "Curtido", inmunidad <c>minorInjuryClinicCost</c>): se cura
/// solo lo que a los demás les cuesta dinero. Es la mitad de campaña de la carnicería administrada — el
/// desgaste se paga en oro (ADR 0099), y este perk lo esquiva <b>solo</b> para la lesión leve.
/// </summary>
public sealed class MedicalImmunityTests
{
    /// <summary>Un perk universal de un solo efecto: la exención y nada más.</summary>
    private static readonly string Tough = TestPerks.Json(
        "test_tough",
        "MATCH_START",
        """[ { "type": "immunity", "target": "owner", "immunity": "minorInjuryClinicCost" } ]""");

    [Fact]
    public void TheExemptPlayerHealsHisMinorInjuryWithoutPayingAndTheOthersPay()
    {
        var catalog = TestPerks.CatalogWith(("test_tough", Tough));
        var economy = SystemsTestSupport.Systems.Economy;
        var start = RunEngine.Start(SystemsTestSupport.Setup(), 4242UL, catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 200);
        var node = SystemsTestSupport.WithFakePendingNode(start, NodeKind.Clinic);

        // El mismo estado físico en dos jugadores, y la única diferencia es el perk: así lo que se mide es
        // la exención y no la gravedad, el oro de partida ni el orden de la plantilla.
        var exempt = node.Roster[0] with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 1, Perks = new[] { "test_tough" } };
        var paying = node.Roster[1] with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 1 };
        var state = node.WithPlayer(exempt).WithPlayer(paying);

        var afterExempt = MedicalSystem.Treat(state, new TreatPlayer(exempt.Id), economy, catalog);
        Assert.Equal(PhysicalState.Healthy, afterExempt.GetPlayer(exempt.Id).PhysicalState);
        Assert.Equal(0, afterExempt.GetPlayer(exempt.Id).MinorInjuries);
        Assert.Equal(state.Gold, afterExempt.Gold);

        var afterPaying = MedicalSystem.Treat(state, new TreatPlayer(paying.Id), economy, catalog);
        Assert.Equal(PhysicalState.Healthy, afterPaying.GetPlayer(paying.Id).PhysicalState);
        Assert.Equal(state.Gold - economy.ClinicMinorCost, afterPaying.Gold);
    }

    /// <summary>
    /// La grave nunca se exime. El perk dice que se cura solo lo que a los demás les cuesta dinero, no que
    /// sea invulnerable: si eximiera la grave, el desgaste dejaría de ser el recurso central de la run.
    /// </summary>
    [Fact]
    public void TheExemptionNeverCoversASevereInjury()
    {
        var catalog = TestPerks.CatalogWith(("test_tough", Tough));
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 4243UL, catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 200);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);
        var broken = state.Roster[0] with { PhysicalState = PhysicalState.SevereInjury, Perks = new[] { "test_tough" } };
        state = state.WithPlayer(broken);

        var healed = MedicalSystem.Treat(state, new TreatPlayer(broken.Id), economy, catalog);

        Assert.Equal(PhysicalState.Healthy, healed.GetPlayer(broken.Id).PhysicalState);
        Assert.Equal(state.Gold - economy.ClinicCost, healed.Gold);
    }

    /// <summary>
    /// Sin catálogo, la clínica se comporta exactamente como antes de la exención. Es lo que protege a
    /// todo el código que llama al `Treat` de tres argumentos: añadir la primitiva no cambia el precio de
    /// nadie que no lleve el perk.
    /// </summary>
    [Fact]
    public void WithoutACatalogNobodyIsExempt()
    {
        var catalog = TestPerks.CatalogWith(("test_tough", Tough));
        var economy = SystemsTestSupport.Systems.Economy;
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 4244UL, catalog, SystemsTestSupport.Systems)
            .WithGold(economy.ClinicCost + 200);
        state = SystemsTestSupport.WithFakePendingNode(state, NodeKind.Clinic);
        var bruised = state.Roster[0] with { PhysicalState = PhysicalState.MinorInjury, MinorInjuries = 1, Perks = new[] { "test_tough" } };
        state = state.WithPlayer(bruised);

        var healed = MedicalSystem.Treat(state, new TreatPlayer(bruised.Id), economy);

        Assert.Equal(PhysicalState.Healthy, healed.GetPlayer(bruised.Id).PhysicalState);
        Assert.Equal(state.Gold - economy.ClinicMinorCost, healed.Gold);
    }
}
