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
        bool minor = player.PhysicalState == PhysicalState.MinorInjury && player.MinorInjuries > 0;
        if (player.PhysicalState != PhysicalState.SevereInjury && !minor)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está {player.PhysicalState}: la clínica trata lesiones graves (RF-092, RF-094) y leves (AZ-G, ADR 0090), no a un sano",
                nameof(decision));
        }

        // AZ-G (ADR 0090): la leve se cura a su propio precio, menor que el de la grave.
        int cost = minor ? economy.ClinicMinorCost : economy.ClinicCost;
        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"tratar a {player.Id} cuesta {cost} de oro y la run solo tiene {state.Gold}",
                nameof(decision));
        }

        return state
            .AddGold(-cost)
            .WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
    }
}
