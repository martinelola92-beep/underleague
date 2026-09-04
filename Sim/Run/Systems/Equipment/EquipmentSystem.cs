using Underleague.Sim.Model;
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
/// esquema del paquete W). Así que asignar un objeto a alguien que ya lleva otro vende automáticamente el
/// objeto desplazado a la fracción de mercado, en vez de destruirlo gratis o rechazar la operación.</para>
/// </summary>
public static class EquipmentSystem
{
    /// <summary>Contador del jugador: partidos jugados con el objeto frágil actual equipado (RF-077).</summary>
    public const string FragileUsesCounter = "item_uses";

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

        return displaced is null ? state : SellItemGold(state, displaced, economy, items);
    }

    /// <summary>
    /// Objeto frágil que se rompe tras N partidos jugados o si el portador se lesiona (RF-077). Se llama
    /// desde <see cref="StandardRunSystems.AfterMatch"/> para cada partido resuelto, ganado o perdido:
    /// solo mira a los titulares (<c>summary.PlayedPlayerIds</c>), porque un suplente no ha "usado" nada.
    /// </summary>
    public static RunState ProcessFragileItems(RunState state, RunMatchSummary summary, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(items);

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

            // W-10: un titular que termina el partido con un estado físico distinto de sano ha sido
            // lesionado en ESTE partido (las leves que ya arrastraba se resetean a sano antes de aplicar
            // las lesiones nuevas). Es la señal que RF-077 pide: "si el portador se lesiona".
            bool injuredThisMatch = player.PhysicalState != PhysicalState.Healthy;
            int uses = player.Counter(FragileUsesCounter) + 1;

            state = state.WithPlayer(injuredThisMatch || uses >= item.UsesLimit
                ? ClearItem(player)
                : player.WithCounter(FragileUsesCounter, uses));
        }

        return state;
    }

    /// <summary>
    /// Asigna un objeto recién adquirido (comprado en el mercado o elegido como recompensa) a un
    /// jugador. Si ya lleva otro, el desplazado se vende automáticamente a la fracción de mercado, igual
    /// que una transferencia (RF-114e extendido a objetos, mismo criterio que <see cref="Apply"/>).
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

    private static RunPlayer ClearItem(RunPlayer player) => player with
    {
        Item = null,
        Counters = WithoutFragileCounter(player.Counters),
    };

    private static RunPlayer AssignItem(RunPlayer player, string itemId) => player with
    {
        Item = itemId,
        Counters = WithoutFragileCounter(player.Counters),
    };

    private static IReadOnlyDictionary<string, int> WithoutFragileCounter(IReadOnlyDictionary<string, int> counters)
    {
        if (!counters.ContainsKey(FragileUsesCounter))
        {
            return counters;
        }

        var copy = new Dictionary<string, int>(counters, StringComparer.Ordinal);
        copy.Remove(FragileUsesCounter);
        return new SortedDictionary<string, int>(copy, StringComparer.Ordinal);
    }

    private static RunState SellItemGold(RunState state, string itemId, EconomyConfig economy, ItemCatalog items)
    {
        var item = items.Get(itemId);
        int price = economy.Market.ItemPrice.Of(item.Rarity) * economy.Market.ItemSellFractionPercent / 100;
        return state.AddGold(price);
    }
}
