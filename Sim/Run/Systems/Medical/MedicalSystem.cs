using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Run.Systems.Medical;

/// <summary>
/// Clínica (RF-094): coste alto en oro, resultado garantizado, restaura a sano sin efectos secundarios.
/// Solo trata la lesión grave (RF-092: la leve no impide jugar y se gasta sola al jugar el siguiente
/// partido, W-10). Un jugador sano nunca muere (RF-093) y esta clínica nunca lo pone en riesgo: es
/// exactamente lo contrario, la herramienta que RF-012d exige para que el jugador pueda hacer algo con la
/// información que tiene.
/// </summary>
public static class MedicalSystem
{
    public static RunState Treat(RunState state, TreatPlayer decision, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);

        NodeGuards.RequireOpen(state, NodeKind.Clinic, "tratar a un jugador");

        var player = state.GetPlayer(decision.PlayerId);
        if (player.PhysicalState != PhysicalState.SevereInjury)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está {player.PhysicalState}, no lesión grave: la clínica solo trata lesiones graves (RF-092, RF-094)",
                nameof(decision));
        }

        if (state.Gold < economy.ClinicCost)
        {
            throw new ArgumentException(
                $"tratar a {player.Id} cuesta {economy.ClinicCost} de oro y la run solo tiene {state.Gold}",
                nameof(decision));
        }

        return state
            .AddGold(-economy.ClinicCost)
            .WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
    }
}
