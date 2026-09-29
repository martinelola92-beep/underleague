using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.Systems.Equipment;

/// <summary>
/// Equipamiento (RF-075..078): un objeto por jugador, transferible fuera de partido y vendible por una
/// fracción de su valor.
///
/// <para><b>Vender un objeto reutiliza <see cref="TransferItem"/>.</b> El paquete W cerró la jerarquía de
/// <c>RunDecision</c> sin un <c>SellItem</c> (solo <c>SellPlayer</c> para jugadores) y el paquete X no
/// toca <c>RunSetup.cs</c> (fichero raíz de W, fuera de sus fronteras): en vez de eso,
/// <c>ToPlayerId &lt; 0</c> en <see cref="TransferItem"/> significa "vender" en lugar de "mover a ese
/// jugador". Queda anotado como decisión de implementación del paquete X en <c>fase2-diseno.md</c>.</para>
///
/// <para><b>Sin hueco de jugador vacío.</b> RF-076 exige que cada jugador lleve como mucho un objeto y
/// <c>RunState</c> no tiene un "almacén" de objetos sin asignar (no hay forma de añadir uno sin tocar el
/// esquema del paquete W). Desde la ADR 0161 sí lo hay (<c>StoredItems</c>): pasar o equipar un objeto a
/// alguien que ya lleva otro manda el desplazado al almacén; sólo comprar (<c>AssignPurchasedItem</c>)
/// vende el desplazado, y vender explícito sigue siendo <c>ToPlayerId &lt; 0</c>.</para>
/// </summary>
public static class EquipmentSystem
{
    /// <summary>Contador de run: objetos frágiles rotos, para el informe post-partido y para /Balance (RF-077).</summary>
    public const string ItemsBrokenCounter = "itemsBroken";

    public static RunState Apply(RunState state, TransferItem decision, EconomyConfig economy, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);

        var from = state.GetPlayer(decision.FromPlayerId);
        if (from.Item is null)
        {
            throw new ArgumentException(
                $"el jugador {from.Id} no lleva ningún objeto que transferir o vender (RF-075)", nameof(decision));
        }

        string itemId = from.Item;

        if (decision.ToPlayerId < 0)
        {
            NodeGuards.RequireOpen(state, NodeKind.Market, "vender un objeto");
            state = state.WithPlayer(ClearItem(from));
            return SellItemGold(state, itemId, economy, items);
        }

        if (decision.ToPlayerId == decision.FromPlayerId)
        {
            throw new ArgumentException("el origen y el destino de la transferencia son el mismo jugador", nameof(decision));
        }

        var to = state.GetPlayer(decision.ToPlayerId);
        if (to.PhysicalState == PhysicalState.Dead)
        {
            throw new ArgumentException($"el jugador {to.Id} está muerto y no puede recibir un objeto", nameof(decision));
        }

        string? displaced = to.Item;
        state = state.WithPlayer(ClearItem(from));
        state = state.WithPlayer(AssignItem(state.GetPlayer(to.Id), itemId));


        // ADR 0161 §3 (revisión independiente): pasar un objeto a quien ya lleva otro NO vende el suyo en
        // silencio —el jugador sólo pidió mover, y el oro sólo debe entrar por un gesto de vender
        // (ToPlayerId < 0)—: el desplazado vuelve al almacén, como al equipar desde el cofre.
        return displaced is null ? state : state.WithStockedItem(displaced, 1);
    }

    /// <summary>
    /// Rotura de los objetos frágiles (RF-077, ADR 0036). Se llama desde
    /// <see cref="StandardRunSystems.AfterMatch"/> para cada partido resuelto, ganado o perdido, y solo
    /// mira a los titulares (<c>summary.PlayedPlayerIds</c>): un suplente no ha usado nada.
    ///
    /// <para><b>La tirada se resuelve al TERMINAR el partido, nunca durante</b> (ADR 0036): así no altera
    /// en secreto un partido en curso y el informe post-partido puede anunciarla. La probabilidad está a
    /// la vista en la ficha del objeto desde antes de equiparlo (<see cref="ItemDescriptions"/>), que es
    /// lo que RF-012d exige y lo que separa esto del "azar post-acción negativo" del §8.</para>
    ///
    /// <para>El dado sale del flujo de recompensas del nodo (RT-022): cambiar una rotura no puede alterar
    /// un partido con la misma semilla.</para>
    /// </summary>
    public static RunState ProcessFragileItems(RunState state, RunMatchSummary summary, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(items);

        var rng = OfferStream.For(state.Seed, summary.NodeId, rerollCount: FragileRollStream);
        int broken = 0;

        for (int i = 0; i < summary.PlayedPlayerIds.Count; i++)
        {
            var player = state.GetPlayer(summary.PlayedPlayerIds[i]);
            if (player.Item is null)
            {
                continue;
            }

            var item = items.Find(player.Item);
            if (item is not { Archetype: ItemArchetype.Fragile })
            {
                continue;
            }

            // El dado se tira SIEMPRE por cada frágil alineado, se rompa o no: si solo se tirara cuando
            // se rompe, el flujo de RNG dependería del resultado y dejaría de ser reproducible.
            if (rng.Range(0, 100) < item.BreakChancePercent)
            {
                state = state.WithPlayer(ClearItem(player));
                broken++;
            }
        }

        return broken == 0 ? state : state.WithCounter(ItemsBrokenCounter, state.Counter(ItemsBrokenCounter) + broken);
    }

    /// <summary>
    /// Desplazamiento del flujo de recompensas del nodo con el que se tiran las roturas. Está por encima
    /// de cualquier número de rerolls posible (RF-071b: uno por nodo) para no colisionar con el surtido.
    /// </summary>
    private const int FragileRollStream = 5000;

    /// <summary>
    /// Asigna un objeto recién adquirido (comprado en el mercado o elegido como recompensa) a un
    /// jugador. Si ya lleva otro, el desplazado se vende automáticamente a la fracción de mercado (RF-114e
    /// extendido a objetos): aquí el que entra se ha comprado o elegido, no es un simple movimiento.
    /// </summary>
    public static RunState AssignPurchasedItem(RunState state, int playerId, string itemId, EconomyConfig economy, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);

        var player = state.GetPlayer(playerId);
        if (player.PhysicalState == PhysicalState.Dead)
        {
            throw new ArgumentException($"el jugador {playerId} está muerto y no puede recibir un objeto", nameof(playerId));
        }

        string? displaced = player.Item;
        state = state.WithPlayer(AssignItem(player, itemId));
        return displaced is null ? state : SellItemGold(state, displaced, economy, items);
    }

    /// <summary>
    /// Saca del almacén un objeto (heredado de un muerto, ADR 0048 condición 4, o botín de liga o
    /// reliquia, ADR 0161 §1 y §2) y se lo pone a un jugador vivo. No cuesta oro —ya estaba pagado— y sigue
    /// la misma regla de un objeto por jugador (RF-076): si el jugador ya llevaba otro, <b>vuelve al
    /// almacén</b> (ADR 0161 §3: "un objeto equipado que se sustituye vuelve al cofre, nunca desaparece"),
    /// a diferencia de <see cref="AssignPurchasedItem"/> —esa sí vende el desplazado, porque ahí el que
    /// entra se ha COMPRADO— y de <see cref="Apply(RunState, TransferItem, EconomyConfig, ItemCatalog)"/>,
    /// que si el receptor ya lleva otro también lo manda al almacén, y no lo vende. Se puede hacer en cualquier nodo,
    /// igual que transferir: hacer sitio o rehacer una build no puede depender de estar en un mercado.
    /// </summary>
    public static RunState Apply(RunState state, EquipStoredItem decision, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(items);

        if (state.StockOf(decision.ItemId) <= 0)
        {
            throw new ArgumentException(
                $"el almacén no tiene ninguna copia de '{decision.ItemId}' (ADR 0048: solo entra el equipo de un muerto)",
                nameof(decision));
        }

        // Valida que el id exista en el catálogo antes de mover nada: un almacén con un id corrupto es un
        // dato inválido, y RT-032 exige que sea un error explícito, no un jugador con un objeto fantasma.
        _ = items.Get(decision.ItemId);

        var player = state.GetPlayer(decision.PlayerId);
        if (player.PhysicalState == PhysicalState.Dead)
        {
            throw new ArgumentException($"el jugador {player.Id} está muerto y no puede recibir un objeto", nameof(decision));
        }

        string? displaced = player.Item;
        state = state.WithStockedItem(decision.ItemId, -1);
        state = state.WithPlayer(AssignItem(player, decision.ItemId));
        return displaced is null ? state : state.WithStockedItem(displaced, 1);
    }

    /// <summary>
    /// Guarda en el almacén el objeto que lleva puesto el jugador (ADR 0161 §3, decisión <see cref="StoreItem"/>
    /// del cofre de Equipo). Mismo gesto que <see cref="Apply(RunState, TransferItem, EconomyConfig, ItemCatalog)"/>
    /// con <c>ToPlayerId &lt; 0</c> salvo que aquí el objeto no se vende: aterriza en <c>RunState.StoredItems</c>,
    /// listo para volver a equiparse sin pasar por el mercado.
    /// </summary>
    public static RunState Apply(RunState state, StoreItem decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        var player = state.GetPlayer(decision.PlayerId);
        if (player.Item is not { } itemId)
        {
            throw new ArgumentException(
                $"el jugador {player.Id} no lleva ningún objeto que guardar en el cofre", nameof(decision));
        }

        return state.WithPlayer(ClearItem(player)).WithStockedItem(itemId, 1);
    }

    private static RunPlayer ClearItem(RunPlayer player) => player with { Item = null };

    private static RunPlayer AssignItem(RunPlayer player, string itemId) => player with { Item = itemId };

    private static RunState SellItemGold(RunState state, string itemId, EconomyConfig economy, ItemCatalog items)
    {
        // El precio de venta sale del VALOR del objeto (ADR 0038), no de su rareza: vender un +10 de
        // fuerza tiene que devolver más que vender un +10 de resistencia, igual que comprarlo cuesta más.
        var item = items.Get(itemId);
        return state.AddGold(ItemPricing.SalePrice(item, items.Scale, economy.Market));
    }
}
