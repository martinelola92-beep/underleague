using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Equipment;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Run.Systems.Rewards;

/// <summary>Una de las 3 opciones de recompensa (RF-071): perk, jugador u objeto.</summary>
public abstract record RewardOption;

/// <summary>Opción de perk; el jugador elige portador con <c>ChooseReward.CarrierPlayerId</c> (RF-071).</summary>
public sealed record PerkRewardOption(string PerkId) : RewardOption;

/// <summary>Opción de jugador nuevo (id -1: se asigna al elegirlo).</summary>
public sealed record PlayerRewardOption(RunPlayer Player) : RewardOption;

/// <summary>Opción de objeto de equipamiento; también necesita portador (extensión de RF-114e a recompensas).</summary>
public sealed record ItemRewardOption(string ItemId) : RewardOption;

/// <summary>
/// Recompensas tras un partido ganado (RF-071, RF-071b). El nodo de recompensa es el propio nodo de
/// partido, dejado pendiente por <see cref="StandardRunSystems.AfterMatch"/>
/// (<c>state.WithPendingNode(node.Id)</c>): las tres opciones se derivan de
/// <c>RngStreams.Rewards(seed, node.Id * 10.000 + state.NodeRerolls)</c> (W-12, <see cref="OfferStream"/>),
/// así que no hay que guardarlas y volver a un nodo de recompensa a medio elegir reproduce el mismo
/// surtido.
/// </summary>
public static class RewardSystem
{
    private const string ClaimedCounterPrefix = "rewardClaimed:";

    /// <summary>
    /// Separación entre los flujos de dos elecciones del mismo nodo (ADR 0043: el jefe da dos). Está por
    /// encima de cualquier número de rerolls (RF-071b: uno por nodo) y por debajo del desplazamiento con
    /// el que <c>EquipmentSystem</c> tira las roturas, así que ningún surtido comparte dado con otro.
    /// </summary>
    private const int PickStreamStep = 100;

    /// <summary>
    /// Las opciones de la elección abierta de ese nodo, deterministas por (semilla, nodo, elección,
    /// rerolls). Cuántas son y con qué rareza sale cada una lo dice el tipo de nodo
    /// (<c>economy.nodeRewards</c>, ADR 0043): el partido de élite las sortea con rareza mejorada.
    /// </summary>
    public static IReadOnlyList<RewardOption> Options(RunState state, MapNode node, Catalog catalog, EconomyConfig economy, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);

        var config = economy.RewardFor(node.Kind);
        int taken = PicksTaken(state, node.Id);
        var rng = OfferStream.For(state.Seed, node.Id, state.NodeRerolls + (taken * PickStreamStep));
        var perkPool = PerkPool.Offerable(state, catalog);

        var options = new List<RewardOption>(config.Options);
        for (int i = 0; i < config.Options; i++)
        {
            // La rareza mejorada se sortea opción a opción y ANTES de elegir el tipo, para que el dado sea
            // el mismo en todos los nodos: un nodo de liga tira el mismo número y siempre sale "no".
            bool rare = rng.Range(0, 100) < config.RarityFloorPercent;
            options.Add(PickOption(ref rng, perkPool, items, catalog, state, economy, node.Act, rare));
        }

        return options;
    }

    /// <summary>Elecciones ya cobradas (o rechazadas) en ese nodo (ADR 0043: el jefe da dos).</summary>
    public static int PicksTaken(RunState state, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Counter(ClaimedCounterPrefix + nodeId);
    }

    /// <summary>Elecciones que da ese nodo de partido (<c>economy.nodeRewards</c>).</summary>
    public static int PicksFor(MapNode node, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(economy);
        return economy.RewardFor(node.Kind).Picks;
    }

    /// <summary>
    /// No queda ninguna elección pendiente en ese nodo: ya se han cobrado (o rechazado) todas. Hace falta
    /// el catálogo de economía porque el número de elecciones depende del tipo de nodo (ADR 0043).
    /// </summary>
    public static bool AlreadyClaimed(RunState state, MapNode node, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        return PicksTaken(state, node.Id) >= PicksFor(node, economy);
    }

    public static RunState Choose(RunState state, ChooseReward decision, Catalog catalog, EconomyConfig economy, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        var node = NodeGuards.RequireOpenMatch(state, "elegir una recompensa");
        if (AlreadyClaimed(state, node, economy))
        {
            throw new InvalidOperationException(
                $"las {PicksFor(node, economy)} recompensas de este nodo ya se han resuelto (RF-071, ADR 0043)");
        }

        var options = Options(state, node, catalog, economy, items);
        if (decision.OptionIndex < 0 || decision.OptionIndex >= options.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision.OptionIndex, $"la recompensa tiene {options.Count} opciones (0..{options.Count - 1})");
        }

        var next = options[decision.OptionIndex] switch
        {
            PerkRewardOption perk => ApplyPerk(state, perk, decision, catalog),
            PlayerRewardOption player => state.WithNewPlayer(player.Player),
            ItemRewardOption item => ApplyItem(state, item, decision, economy, items),
            var other => throw new InvalidOperationException($"tipo de recompensa no reconocido: {other.GetType().Name}"),
        };

        return Advance(next, node);
    }

    /// <summary>
    /// <b>Rechazar</b> la recompensa (ADR 0043): con perks irreversibles (RF-072) y slots limitados,
    /// quedarse con la menos mala puede ser peor que no quedarse con nada. Consume la elección —no la
    /// guarda para después— y deja las demás del nodo intactas.
    /// </summary>
    public static RunState Decline(RunState state, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);

        var node = NodeGuards.RequireOpenMatch(state, "rechazar una recompensa");
        if (AlreadyClaimed(state, node, economy))
        {
            throw new InvalidOperationException(
                $"las {PicksFor(node, economy)} recompensas de este nodo ya se han resuelto (RF-071, ADR 0043)");
        }

        return Advance(state, node);
    }

    /// <summary>
    /// Cierra una elección del nodo: la anota. El reroll <b>no</b> se reinicia entre las dos elecciones
    /// del jefe: RF-071b dice uno por nodo y sigue siendo uno por nodo.
    /// </summary>
    private static RunState Advance(RunState state, MapNode node) =>
        state.WithCounter(ClaimedCounterPrefix + node.Id, PicksTaken(state, node.Id) + 1);

    public static RunState Reroll(RunState state, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);

        var node = NodeGuards.RequireOpenMatch(state, "repetir la tirada de recompensa");
        if (AlreadyClaimed(state, node, economy))
        {
            throw new InvalidOperationException("no se puede repetir la tirada: las recompensas de este nodo ya se han resuelto");
        }

        if (state.NodeRerolls > 0)
        {
            throw new InvalidOperationException("ya se ha usado el reroll de este nodo (RF-071b: uno por nodo)");
        }

        int cost = economy.RerollCost(state.RerollsUsed);
        if (state.Gold < cost)
        {
            throw new ArgumentException($"repetir la tirada cuesta {cost} de oro y la run solo tiene {state.Gold}");
        }

        return state.AddGold(-cost).WithRerolls(state.RerollsUsed + 1, state.NodeRerolls + 1);
    }

    private static RunState ApplyPerk(RunState state, PerkRewardOption option, ChooseReward decision, Catalog catalog)
    {
        var perk = catalog.Perks.Get(option.PerkId);
        var carriers = PerkPool.EligibleCarriers(state, perk, catalog);
        if (!carriers.Contains(decision.CarrierPlayerId))
        {
            throw new ArgumentException(
                $"el jugador {decision.CarrierPlayerId} no puede llevar el perk '{option.PerkId}' (RF-071: elige un portador con slot libre)",
                nameof(decision));
        }

        var carrier = state.GetPlayer(decision.CarrierPlayerId);
        return state.WithPlayer(PerkPool.WithPerk(carrier, option.PerkId));
    }

    private static RunState ApplyItem(RunState state, ItemRewardOption option, ChooseReward decision, EconomyConfig economy, ItemCatalog items)
    {
        if (decision.CarrierPlayerId < 0)
        {
            throw new ArgumentException("elegir un objeto de recompensa exige indicar quién lo equipa", nameof(decision));
        }

        return EquipmentSystem.AssignPurchasedItem(state, decision.CarrierPlayerId, option.ItemId, economy, items);
    }

    private static RewardOption PickOption(
        ref Pcg32 rng,
        IReadOnlyList<Perks.PerkDefinition> perkPool,
        ItemCatalog items,
        Catalog catalog,
        RunState state,
        EconomyConfig economy,
        int act,
        bool rarityFloor)
    {
        int total = economy.RewardPerkWeight + economy.RewardPlayerWeight + economy.RewardItemWeight;
        int roll = rng.Range(0, total);
        bool wantsPerk = roll < economy.RewardPerkWeight;
        bool wantsPlayer = !wantsPerk && roll < economy.RewardPerkWeight + economy.RewardPlayerWeight;

        if (rarityFloor)
        {
            // Rareza mejorada (ADR 0043): la opción se sortea solo entre las que superan el común. Si el
            // pool no tiene ninguna, se cae al pool entero en vez de dejar la opción vacía.
            var better = new List<Perks.PerkDefinition>(perkPool.Count);
            for (int i = 0; i < perkPool.Count; i++)
            {
                if (perkPool[i].Rarity != Rarity.Common)
                {
                    better.Add(perkPool[i]);
                }
            }

            if (better.Count > 0)
            {
                perkPool = better;
            }
        }

        if (wantsPerk && perkPool.Count > 0)
        {
            // ADR 0038: el peso del perk en el pool baja con su valor medido. Es la palanca de la vía
            // gratuita, la que el precio no puede tocar (RF-071).
            var weights = new List<int>(perkPool.Count);
            for (int i = 0; i < perkPool.Count; i++)
            {
                weights.Add(economy.PerkValues.WeightOf(perkPool[i].Id));
            }

            return new PerkRewardOption(perkPool[WeightedPick.Index(ref rng, weights)].Id);
        }

        if (wantsPlayer || wantsPerk)
        {
            var player = GeneratedPlayers.Reward(ref rng, catalog, state.ClubRace, economy.RewardPlayerQuality, economy.RecruitLevel(act));
            return new PlayerRewardOption(player);
        }

        // Solo los universales y los restringidos de la raza del club (ADR 0036); el frágil sale más a
        // menudo, que es la otra mitad de su compensación.
        var itemPool = items.OfferableTo(state.ClubRace);
        if (rarityFloor)
        {
            var better = new List<ItemDefinition>(itemPool.Count);
            for (int i = 0; i < itemPool.Count; i++)
            {
                if (itemPool[i].Rarity != Rarity.Common)
                {
                    better.Add(itemPool[i]);
                }
            }

            if (better.Count > 0)
            {
                itemPool = better;
            }
        }

        var itemWeights = new List<int>(itemPool.Count);
        for (int i = 0; i < itemPool.Count; i++)
        {
            itemWeights.Add(ItemPricing.OfferWeight(itemPool[i], items.Scale));
        }

        var item = itemPool[WeightedPick.Index(ref rng, itemWeights)];
        return new ItemRewardOption(item.Id);
    }
}
