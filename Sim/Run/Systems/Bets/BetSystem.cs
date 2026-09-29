using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems.Bets;

/// <summary>
/// La apuesta que un corredor ofrece antes de un nodo de partido (ADR 0157): qué condición, cuánto cuesta
/// tomarla y cuánto cobra si se cumple. <see cref="TargetPlayerId"/> y <see cref="TargetPlayerName"/> solo
/// existen en <see cref="BetKind.HuntTheStar"/> (-1 y vacío en el resto).
/// </summary>
/// <param name="Stake">Oro que cuesta tomarla: fijo por acto (no proporcional al oro, ADR 0157 §9a).</param>
/// <param name="PayoutPercent">Cobro bruto como porcentaje de la apuesta, según la dificultad del nodo.</param>
public sealed record BetOffer(
    string BetId,
    BetKind Kind,
    int Stake,
    int PayoutPercent,
    int TargetPlayerId,
    string TargetPlayerName)
{
    /// <summary>Oro bruto que se cobra si se cumple (incluye la apuesta, que ya se pagó al tomarla).</summary>
    public int Payout => Stake * PayoutPercent / 100;
}

/// <summary>
/// Deriva la apuesta de un nodo de partido (ADR 0157, punto 1). <b>No se guarda</b>: sale de
/// (semilla, nodo) con un flujo propio, <c>OfferStream.For(seed, nodeId, 8000)</c>, así que es la misma antes
/// de jugar (para enseñarla en el ojeo, W-12, RF-012d) y después (para resolverla), y no consume ni
/// desplaza ninguno de los demás sorteos del nodo (mercado, recompensa, botín, evento: tabla de
/// desplazamientos en <see cref="OfferStream"/>).
/// </summary>
public static class BetSystem
{
    /// <summary>Desplazamiento de <see cref="OfferStream"/> de la apuesta del nodo (tabla en <see cref="OfferStream"/>).</summary>
    public const int OfferStreamOffset = 8000;

    /// <summary>
    /// Apuesta que se ofrece en ese nodo, o null si el nodo no es de partido (liga, élite o jefe) o el
    /// catálogo de apuestas está vacío. Determinista por (semilla, nodo). Para
    /// <see cref="BetKind.HuntTheStar"/> nombra al rival concreto (<see cref="TargetFor"/>) del equipo que
    /// devuelve <paramref name="systems"/> para ese nodo, el mismo que se jugará.
    /// </summary>
    public static BetOffer? OfferFor(RunState state, MapNode node, BetCatalog bets, IRunSystems systems, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(bets);
        ArgumentNullException.ThrowIfNull(systems);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!node.IsMatch || bets.All.Count == 0)
        {
            return null;
        }

        var rng = OfferStream.For(state.Seed, node.Id, OfferStreamOffset);
        var bet = bets.All[rng.Range(0, bets.All.Count)];

        int targetId = -1;
        string targetName = string.Empty;
        if (bet.Kind == BetKind.HuntTheStar)
        {
            var star = TargetFor(systems.OpponentFor(state, node, catalog));
            if (star is not null)
            {
                targetId = star.Id;
                targetName = star.Name;
            }
        }

        return new BetOffer(
            bet.Id,
            bet.Kind,
            bet.StakeFor(node.Act),
            bet.PayoutPercentFor(node.Difficulty),
            targetId,
            targetName);
    }

    /// <summary>
    /// El jugador rival de mayor rareza entre los que empiezan el partido (los de la alineación); a igual
    /// rareza, el de id menor (RT-041). Null si el equipo no tiene alineación.
    /// </summary>
    public static PlayerDefinition? TargetFor(TeamSetup rival)
    {
        ArgumentNullException.ThrowIfNull(rival);
        PlayerDefinition? best = null;
        var slots = rival.Lineup.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            PlayerDefinition? candidate = null;
            for (int j = 0; j < rival.Players.Count; j++)
            {
                if (rival.Players[j].Id == slots[i].PlayerId)
                {
                    candidate = rival.Players[j];
                    break;
                }
            }

            if (candidate is null)
            {
                continue;
            }

            if (best is null
                || candidate.Rarity > best.Rarity
                || (candidate.Rarity == best.Rarity && candidate.Id < best.Id))
            {
                best = candidate;
            }
        }

        return best;
    }
}
