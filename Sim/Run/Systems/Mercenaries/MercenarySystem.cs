using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;

namespace Underleague.Sim.Run.Systems.Mercenaries;

/// <summary>
/// Salario y abandono de los mercenarios (RF-111), resuelto tras cada partido -ganado o perdido- desde
/// <see cref="StandardRunSystems.AfterMatch"/>. Los dos contadores que necesita ya existen: el salario se
/// paga siempre que el mercenario siga en la plantilla, <c>RunPlayer.MatchesBenched</c> lo mantiene el
/// paquete W (<c>MatchResolution</c>) y la racha de derrotas del <b>equipo</b> se guarda en
/// <c>RunState.Counters</c> (W-11), no en el jugador: es una racha compartida por todos los mercenarios.
/// </summary>
public static class MercenarySystem
{
    /// <summary>Contador de run: derrotas consecutivas del equipo, para RF-111 ("3 derrotas seguidas").</summary>
    public const string ConsecutiveLossesCounter = "mercenaryLossStreak";

    public static RunState Process(RunState state, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);

        state = PayWages(state, economy);

        int streak = summary.Won ? 0 : state.Counter(ConsecutiveLossesCounter) + 1;
        state = state.WithCounter(ConsecutiveLossesCounter, streak);
        bool lossStreakAbandon = streak >= economy.MercenaryLossStreakAbandon;

        var toRemove = new List<int>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (!player.IsMercenary || player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            if (lossStreakAbandon || player.MatchesBenched >= economy.MercenaryBenchAbandonMatches)
            {
                toRemove.Add(player.Id);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            state = state.WithoutPlayer(toRemove[i]);
        }

        if (lossStreakAbandon)
        {
            // Se ha pagado ya el "castigo" de esta racha: si no se reinicia, la siguiente derrota
            // expulsaría de nuevo a cualquier mercenario nuevo que se fichase justo después.
            state = state.WithCounter(ConsecutiveLossesCounter, 0);
        }

        return state;
    }

    private static RunState PayWages(RunState state, EconomyConfig economy)
    {
        int total = 0;
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.IsMercenary && player.PhysicalState != PhysicalState.Dead)
            {
                total += player.Wage;
            }
        }

        return total == 0 ? state : state.AddGold(-total);
    }
}
