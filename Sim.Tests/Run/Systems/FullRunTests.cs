using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// Una run completa jugada de principio a fin con <see cref="Underleague.Sim.Run.Systems.StandardRunSystems"/>
/// termina en un estado coherente (fase2-diseno.md §12, criterio de test del paquete X). La política es
/// deliberadamente simple (curar cuando se puede, elegir la primera recompensa disponible), no jugar bien:
/// lo que se prueba es que el bucle no se rompe, no que gane.
/// </summary>
public sealed class FullRunTests
{
    [Theory]
    [InlineData(1001UL)]
    [InlineData(1002UL)]
    [InlineData(1003UL)]
    [InlineData(1004UL)]
    public void AFullRunEndsInACoherentState(ulong seed)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(clubRace: Race.Human, startingGold: 150), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);

        for (int i = 0; i < 200 && !RunEngine.Outcome(state).IsOver; i++)
        {
            if (state.Phase == RunPhase.NodeOpen)
            {
                state = AutoResolveOpenNode(state);
                continue;
            }

            var nodes = RunEngine.AvailableNodes(state);
            if (nodes.Count == 0)
            {
                break;
            }

            state = RunEngine.Enter(state, nodes[0].Id, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        }

        var outcome = RunEngine.Outcome(state);
        Assert.True(outcome.IsOver, "la run debería haber terminado en victoria o derrota dentro de 200 pasos");
        Assert.True(state.Gold >= 0);

        var ids = state.Roster.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        // Nadie sano está muerto (RF-093): todo Dead debería, en teoría, venir de una de las dos vías
        // permitidas. Aquí solo se comprueba la invariante más básica y barata: la plantilla no tiene
        // huecos ni ids repetidos, y el guardado del paquete W sigue aceptando el estado tal cual lo deja
        // el paquete X (ninguna decisión de economía ha roto el esquema del guardado).
        var json = RunSave.Save(state);
        var reloaded = RunSave.Load(json);
        Assert.Equal(state.Gold, reloaded.Gold);
        Assert.Equal(state.Roster.Count, reloaded.Roster.Count);
    }

    private static RunState AutoResolveOpenNode(RunState state)
    {
        var systems = SystemsTestSupport.Systems;
        var catalog = SystemsTestSupport.Catalog;
        var node = state.GetNode(state.PendingNodeId);

        if (node.Kind == NodeKind.Clinic)
        {
            var injured = state.Roster.FirstOrDefault(p => p.PhysicalState == PhysicalState.SevereInjury);
            if (injured is not null && state.Gold >= systems.Economy.ClinicCost)
            {
                state = RunEngine.Apply(state, new TreatPlayer(injured.Id), catalog, systems);
            }
        }
        else if (node.IsMatch)
        {
            // ADR 0043: un nodo puede dar más de una elección (el jefe da dos).
            while (!RewardSystem.AlreadyClaimed(state, node, systems.Economy))
            {
                var before = state;
                state = TryClaimReward(state, node);
                if (ReferenceEquals(before, state))
                {
                    state = RunEngine.Apply(state, new DeclineReward(), catalog, systems);
                }
            }
        }

        return RunEngine.Apply(state, new LeaveNode(), catalog, systems);
    }

    private static RunState TryClaimReward(RunState state, MapNode node)
    {
        var systems = SystemsTestSupport.Systems;
        var catalog = SystemsTestSupport.Catalog;
        var options = RewardSystem.Options(state, node, catalog, systems.Economy, systems.Items);

        for (int i = 0; i < options.Count; i++)
        {
            int carrier = options[i] switch
            {
                PerkRewardOption perk => FirstEligibleCarrier(state, perk.PerkId, catalog),
                ItemRewardOption => state.Roster.First(p => p.PhysicalState != PhysicalState.Dead).Id,
                _ => -1,
            };

            if (options[i] is PerkRewardOption && carrier < 0)
            {
                continue;
            }

            // RF-020 (ADR 0046): un jugador de recompensa solo se puede cobrar si cabe en la plantilla.
            // Con la plantilla llena, la salida es rechazar (ADR 0043).
            if (options[i] is PlayerRewardOption && !state.HasRosterSpace)
            {
                continue;
            }

            return RunEngine.Apply(state, new ChooseReward(i, carrier), catalog, systems);
        }

        return state;
    }

    /// <summary>
    /// Primer portador posible, o -1 si la opción no se puede cobrar. Además de no tener portador, un
    /// perk puede estar fuera de alcance por su arco (ADR 0051): un maestro cuya línea todavía no está
    /// construida, o una línea que otro maestro cerró. La pantalla los enseña con su motivo y el botón
    /// apagado; aquí, que es una política ciega que coge la primera opción cobrable, se saltan igual que
    /// se salta un perk que nadie puede llevar.
    /// </summary>
    private static int FirstEligibleCarrier(RunState state, string perkId, Catalog catalog)
    {
        var perk = catalog.Perks.Get(perkId);
        if (Underleague.Sim.Run.Systems.PerkPool.Availability(state, perk, catalog)
            != Underleague.Sim.Run.Systems.PerkAvailability.Available)
        {
            return -1;
        }

        var carriers = Underleague.Sim.Run.Systems.PerkPool.EligibleCarriers(state, perk, catalog);
        return carriers.Count > 0 ? carriers[0] : -1;
    }
}
