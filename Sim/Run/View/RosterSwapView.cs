using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Market;

namespace Underleague.Sim.Run.View;

/// <summary>Qué le pasa al jugador propio que se suelta a cambio de un fichaje con la plantilla llena (BX-5).</summary>
public enum SwapOutcome
{
    /// <summary>Se vende al precio de <see cref="MarketSystem.SalePrice"/> (sólo en el mercado y si el veto de la ADR 0108 lo permite).</summary>
    Sold,

    /// <summary>Se descarta sin cobrar (<see cref="ReleasePlayer"/>): en una recompensa, o un fichaje que aún no ha jugado.</summary>
    Released,
}

/// <summary>Un jugador propio que se puede soltar a cambio, con lo que se verá antes de confirmar (RF-012d).</summary>
public sealed record SwapCandidate(
    int PlayerId,
    string Name,
    Position Position,
    Rarity Rarity,
    int Level,
    PhysicalState PhysicalState,
    SwapOutcome Outcome,
    int Gold);

/// <summary>
/// Los jugadores propios entre los que elegir a quién soltar al fichar con la plantilla llena (BX-5). Puro: lo que
/// enseña es lo que <c>EnrollmentSystem.MakeRoomFor</c> hará —el mismo <c>CanSell</c> y el mismo <c>SalePrice</c>—, no
/// una copia (RT-014). Vacío si la plantilla tiene hueco, porque entonces no hay nada que sustituir. El muerto no
/// aparece: no ocupa plantilla.
/// </summary>
public static class RosterSwapView
{
    /// <param name="state">La run.</param>
    /// <param name="economy">El mercado pasa su economía (vende si puede); una recompensa pasa null (siempre descarta).</param>
    public static IReadOnlyList<SwapCandidate> Candidates(RunState state, EconomyConfig? economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rows = new List<SwapCandidate>(state.Roster.Count);
        if (state.HasRosterSpace)
        {
            return rows;
        }

        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            bool sold = economy is not null && MarketSystem.CanSell(player);
            rows.Add(new SwapCandidate(
                player.Id,
                player.Name,
                player.Position,
                player.Rarity,
                player.Level,
                player.PhysicalState,
                sold ? SwapOutcome.Sold : SwapOutcome.Released,
                sold ? MarketSystem.SalePrice(player, economy!) : 0));
        }

        return rows;
    }
}
