using Underleague.Sim.Data;
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

    /// <summary>Las 3 opciones del nodo de recompensa abierto, deterministas por (semilla, nodo, rerolls).</summary>
    public static IReadOnlyList<RewardOption> Options(RunState state, MapNode node, Catalog catalog, EconomyConfig economy, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);

        var rng = OfferStream.For(state.Seed, node.Id, state.NodeRerolls);
        var perkPool = PerkPool.Offerable(state, catalog);

        var options = new List<RewardOption>(3);
        for (int i = 0; i < 3; i++)
        {
            options.Add(PickOption(ref rng, perkPool, items, catalog, state, economy, node.Act));
        }

        return options;
    }

    /// <summary>Ha llegado a un nodo de recompensa que ya se cerró: no se puede volver a elegir ni repetir tirada.</summary>
    public static bool AlreadyClaimed(RunState state, int nodeId) => state.Counter(ClaimedCounterPrefix + nodeId) != 0;

    public static RunState Choose(RunState state, ChooseReward decision, Catalog catalog, EconomyConfig economy, ItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);

        var node = NodeGuards.RequireOpenMatch(state, "elegir una recompensa");
        if (AlreadyClaimed(state, node.Id))
        {
            throw new InvalidOperationException("la recompensa de este nodo ya se ha elegido (RF-071: una por partido ganado)");
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

        return next.WithCounter(ClaimedCounterPrefix + node.Id, 1);
    }

    public static RunState Reroll(RunState state, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(economy);

        var node = NodeGuards.RequireOpenMatch(state, "repetir la tirada de recompensa");
        if (AlreadyClaimed(state, node.Id))
        {
            throw new InvalidOperationException("no se puede repetir la tirada: la recompensa ya se ha elegido");
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
        int act)
    {
        int total = economy.RewardPerkWeight + economy.RewardPlayerWeight + economy.RewardItemWeight;
        int roll = rng.Range(0, total);
        bool wantsPerk = roll < economy.RewardPerkWeight;
        bool wantsPlayer = !wantsPerk && roll < economy.RewardPerkWeight + economy.RewardPlayerWeight;

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
        var itemWeights = new List<int>(itemPool.Count);
        for (int i = 0; i < itemPool.Count; i++)
        {
            itemWeights.Add(ItemPricing.OfferWeight(itemPool[i], items.Scale));
        }

        var item = itemPool[WeightedPick.Index(ref rng, itemWeights)];
        return new ItemRewardOption(item.Id);
    }
}
