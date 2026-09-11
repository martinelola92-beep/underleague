using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Run.Systems.Medical;

/// <summary>
/// Clínica (RF-094). Hasta la <b>ADR 0099</b> tenía un solo servicio —un jugador, precio fijo, resultado
/// garantizado— y por eso no había nada que decidir salvo a quién. Ahora ofrece tres, y la decisión es cuál:
/// <list type="number">
/// <item><b>Por pieza</b>, garantizado: <see cref="Treat"/> con el precio de la gravedad
/// (<c>clinicCost</c> / <c>clinicMinorCost</c>). Es el de siempre.</item>
/// <item><b>La plantilla entera</b>, garantizado: <see cref="TreatSquad"/> a <c>clinicSquadCost</c>, tarifa
/// plana que no mira cuántos heridos hay. Cara con uno, barata con cuatro.</item>
/// <item><b>El matasanos</b>: <see cref="Treat"/> con <c>risky</c>. Cuesta <c>clinicRiskyPercent</c>% del
/// precio normal y no garantiza nada: puede no curar (<c>clinicRiskyFailPercent</c>) y además puede
/// <b>empeorar</b> un escalón (<c>clinicRiskyWorsePercent</c>) —sano ← leve ← grave ← <b>muerto</b>—.</item>
/// </list>
///
/// <para>El matasanos cumple las cinco condiciones de la ADR 0048 para que un jugador pueda morir: el
/// porcentaje <b>se ve antes de elegir</b> (RF-012d), se puede evitar sin más que pagar el precio normal, el
/// equipo del muerto vuelve al inventario por el mismo camino que cualquier muerte, y es raro. Lo que añade
/// es que por primera vez la muerte puede venir de una <b>decisión de menú</b>, que es exactamente el juego
/// que el revisor pidió: carnicería administrada.</para>
/// </summary>
public static class MedicalSystem
{
    /// <summary>Trata a un jugador. Con <c>decision.Risky</c> es el matasanos: más barato y sin garantía.</summary>
    public static RunState Treat(RunState state, TreatPlayer decision, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);
        var node = NodeGuards.RequireOpen(state, NodeKind.Clinic, "tratar a un jugador");
        var player = state.GetPlayer(decision.PlayerId);
        bool minor = player.PhysicalState == PhysicalState.MinorInjury && player.MinorInjuries > 0;
        if (player.PhysicalState != PhysicalState.SevereInjury && !minor)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} está {player.PhysicalState}: la clínica trata lesiones graves (RF-092, RF-094) y leves (AZ-G, ADR 0090), no a un sano",
                nameof(decision));
        }

        // AZ-G (ADR 0090): la leve se cura a su propio precio, menor que el de la grave.
        int full = minor ? economy.ClinicMinorCost : economy.ClinicCost;
        int cost = decision.Risky ? RiskyCost(full, economy) : full;
        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"tratar a {player.Id} cuesta {cost} de oro y la run solo tiene {state.Gold}",
                nameof(decision));
        }

        state = state.AddGold(-cost);
        if (!decision.Risky)
        {
            return state.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
        }

        // El oro ya está pagado: el matasanos cobra por intentarlo. La tirada es función de (semilla, nodo,
        // tratamientos arriesgados ya hechos en ese nodo), así que repetir la run la reproduce (RT-021/024).
        string rolls = RunState.ClinicRollsPrefix + node.Id;
        int done = state.Counter(rolls);
        var rng = RngStreams.Clinic(state.Seed, node.Id);
        for (int i = 0; i < done; i++)
        {
            rng.Range(0, 100);
        }

        int roll = rng.Range(0, 100);
        state = state.WithCounter(rolls, done + 1);
        if (roll < economy.ClinicRiskyWorsePercent)
        {
            return state.WithPlayer(Worse(player));
        }

        return roll < economy.ClinicRiskyWorsePercent + economy.ClinicRiskyFailPercent
            ? state
            : state.WithPlayer(player with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 });
    }

    /// <summary>
    /// Cura a <b>toda</b> la plantilla por una tarifa plana (ADR 0099). Lanza si no hay a quién curar: pagar
    /// por nada no es una jugada, es un error de la interfaz.
    /// </summary>
    public static RunState TreatSquad(RunState state, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        NodeGuards.RequireOpen(state, NodeKind.Clinic, "curar a la plantilla");
        int cost = economy.ClinicSquadCost;
        if (state.Gold < cost)
        {
            throw new ArgumentException(
                $"curar a la plantilla cuesta {cost} de oro y la run solo tiene {state.Gold}",
                nameof(state));
        }

        var roster = new List<RunPlayer>(state.Roster);
        int treated = 0;
        for (int i = 0; i < roster.Count; i++)
        {
            if (!NeedsTreatment(roster[i]))
            {
                continue;
            }

            roster[i] = roster[i] with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 };
            treated++;
        }

        if (treated == 0)
        {
            throw new ArgumentException("no hay ningún jugador lesionado que curar", nameof(state));
        }

        return state.AddGold(-cost).WithRoster(roster);
    }

    /// <summary>Lesionado que la clínica puede tratar: grave, o leve con lesiones acumuladas (RF-091).</summary>
    public static bool NeedsTreatment(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.PhysicalState == PhysicalState.SevereInjury
            || (player.PhysicalState == PhysicalState.MinorInjury && player.MinorInjuries > 0);
    }

    /// <summary>Precio del matasanos: un porcentaje del normal, nunca menos de uno (cobrar cero no es una decisión).</summary>
    public static int RiskyCost(int fullCost, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(economy);
        return Math.Max(1, fullCost * economy.ClinicRiskyPercent / 100);
    }

    /// <summary>Un escalón hacia abajo: leve → grave, grave → muerto (ADR 0048, ADR 0099).</summary>
    private static RunPlayer Worse(RunPlayer player) => player.PhysicalState switch
    {
        PhysicalState.MinorInjury => player with { PhysicalState = PhysicalState.SevereInjury, MinorInjuries = 0 },
        _ => player with { PhysicalState = PhysicalState.Dead },
    };
}
