using Underleague.Sim.Data;
using Underleague.Sim.Engine;
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
    /// Frecuencia medida mínima (en centésimas de punto porcentual: 200 = 2 %) para que una apuesta se
    /// ofrezca en una dificultad. Por debajo es una lotería que ninguna decisión del jugador inclina y la ADR
    /// 0157 (punto 5) la prohíbe. <b>Provisional</b>: el 2 % lo fijó el revisor, no una medición.
    /// </summary>
    public const int MinOfferedBasisPoints = 200;

    /// <summary>Atajo de <see cref="OfferFor(RunState, MapNode, BetCatalog, IRunSystems, Catalog)"/> con el catálogo de <paramref name="systems"/>: lo que la interfaz enseña en el ojeo.</summary>
    public static BetOffer? OfferFor(RunState state, MapNode node, IRunSystems systems, Catalog catalog) =>
        OfferFor(state, node, systems.Bets, systems, catalog);

    /// <summary>
    /// Apuesta que se ofrece en ese nodo, o null si el nodo no es de partido (liga, élite o jefe) o el
    /// catálogo de apuestas está vacío. Determinista por (semilla, nodo). Para
    /// <see cref="BetKind.HuntTheStar"/> nombra al rival concreto (<see cref="TargetFor"/>) del equipo que
    /// devuelve <paramref name="systems"/> para ese nodo, el mismo que se jugará. No se ofrece una apuesta en
    /// una dificultad donde su frecuencia medida es menor que <see cref="MinOfferedBasisPoints"/>.
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

        // Solo entran en el sorteo las apuestas cuya frecuencia medida en ESTA dificultad llega al mínimo
        // (catálogo ordenado por id: RT-041). El sorteo es uniforme sobre las elegibles.
        var eligible = new List<BetDefinition>(bets.All.Count);
        for (int i = 0; i < bets.All.Count; i++)
        {
            if (bets.All[i].FrequencyBasisPointsFor(node.Difficulty) >= MinOfferedBasisPoints)
            {
                eligible.Add(bets.All[i]);
            }
        }

        if (eligible.Count == 0)
        {
            return null;
        }

        var rng = OfferStream.For(state.Seed, node.Id, OfferStreamOffset);
        var bet = eligible[rng.Range(0, eligible.Count)];

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
    /// Toma la apuesta ofrecida en <see cref="TakeBet.NodeId"/> (ADR 0157 punto 2): la paga al momento y la
    /// guarda en el estado. Solo en el mapa, en un nodo de partido accesible que ofrezca apuesta, con oro
    /// suficiente y una sola por nodo; si había otra tomada para otro nodo, se devuelve y se sustituye.
    /// Lanza <see cref="InvalidOperationException"/> en cualquier otro caso: nada se pierde en silencio.
    /// </summary>
    public static RunState Take(RunState state, TakeBet decision, IRunSystems systems, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(systems);
        ArgumentNullException.ThrowIfNull(catalog);
        if (state.Phase != RunPhase.OnMap)
        {
            throw new InvalidOperationException("la apuesta se toma en el mapa, antes de entrar en el partido");
        }

        MapNode? node = null;
        var available = RunEngine.AvailableNodes(state);
        for (int i = 0; i < available.Count; i++)
        {
            if (available[i].Id == decision.NodeId)
            {
                node = available[i];
            }
        }

        if (node is null || !node.IsMatch)
        {
            throw new InvalidOperationException($"el nodo {decision.NodeId} no es un nodo de partido al que se pueda ir: no admite apuesta");
        }

        if (state.Bet is { } existing && existing.NodeId == node.Id)
        {
            throw new InvalidOperationException($"el nodo {node.Id} ya tiene una apuesta tomada: una por nodo");
        }

        var offer = OfferFor(state, node, systems.Bets, systems, catalog)
            ?? throw new InvalidOperationException($"el nodo {node.Id} no ofrece ninguna apuesta");
        var refunded = state.Bet is { } previous ? state.AddGold(previous.Stake) : state;
        if (refunded.Gold < offer.Stake)
        {
            throw new InvalidOperationException($"la apuesta cuesta {offer.Stake} de oro y solo hay {refunded.Gold}");
        }

        return refunded
            .AddGold(-offer.Stake)
            .WithBet(new AcceptedBet(offer.BetId, node.Id, offer.Stake, offer.PayoutPercent, offer.TargetPlayerId, offer.TargetPlayerName));
    }

    /// <summary>Retira la apuesta tomada y no jugada y devuelve lo apostado. Sin apuesta tomada no hace nada.</summary>
    public static RunState Withdraw(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Bet is { } bet ? state.AddGold(bet.Stake).WithBet(null) : state;
    }

    /// <summary>
    /// Resuelve la apuesta tomada para <paramref name="node"/> con los hechos del partido jugado (nunca con una
    /// tirada, ADR 0157 punto 3). Devuelve el estado con la apuesta cerrada —cumplida: se ingresa el cobro
    /// bruto; fallida: nada más, ya se pagó al tomarla— y el resultado que enseña el informe. Sin apuesta tomada
    /// para ese nodo devuelve <paramref name="stateAfter"/> y null.
    /// </summary>
    /// <param name="stateBefore">Estado antes del partido: de él sale quién es canterano.</param>
    /// <param name="stateAfter">Estado tras aplicar el partido, sobre el que se ingresa el cobro.</param>
    public static (RunState State, BetResult? Result) Resolve(
        RunState stateBefore,
        RunState stateAfter,
        MapNode node,
        MatchSetup setup,
        MatchResult result,
        BetCatalog bets)
    {
        ArgumentNullException.ThrowIfNull(stateBefore);
        ArgumentNullException.ThrowIfNull(stateAfter);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(bets);
        if (stateBefore.Bet is not { } bet || bet.NodeId != node.Id)
        {
            return (stateAfter, null);
        }

        var definition = bets.Find(bet.BetId)
            ?? throw new InvalidOperationException($"la apuesta tomada '{bet.BetId}' ya no existe en data/bets/");
        var context = new BetContext(
            setup,
            result,
            id => stateBefore.FindPlayer(id) is { IsYouth: true },
            bet.TargetPlayerId);
        bool met = BetConditions.Evaluate(definition.Kind, context);
        int paid = met ? bet.Payout : 0;
        var outcome = new BetResult(bet.BetId, definition.Kind, bet.Stake, bet.PayoutPercent, bet.TargetPlayerId, bet.TargetPlayerName, met, paid);
        return (stateAfter.AddGold(paid).WithBet(null), outcome);
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
