using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Rewards;

namespace Underleague.Sim.Run.View;

/// <summary>Qué es una opción de recompensa (RF-071): perk, jugador u objeto de equipamiento.</summary>
public enum RewardKind
{
    /// <summary>Un perk, que hay que asignar a un portador y <b>no se puede retirar</b> (RF-072).</summary>
    Perk,

    /// <summary>Un jugador nuevo, que ocupa un hueco de plantilla (RF-020).</summary>
    Player,

    /// <summary>Un objeto de equipamiento, que hay que dárselo a alguien (RF-075..078).</summary>
    Item,
}

/// <summary>Por qué una opción no se puede cobrar ahora mismo. La frase la pone la interfaz (RT-073).</summary>
public enum RewardBlock
{
    /// <summary>Se puede cobrar.</summary>
    None,

    /// <summary>La plantilla está llena (RF-020, ADR 0046): hay que rechazar o hacer sitio.</summary>
    RosterFull,

    /// <summary>Nadie de la plantilla puede llevar ese perk: sin slot libre, ya lo lleva, o no cumple sus etiquetas.</summary>
    NoCarrier,
}

/// <summary>
/// Un jugador de la plantilla al que se le puede dar la recompensa (RF-071: "elige además a qué jugador
/// se lo asigna"). <paramref name="FreeSlots"/> es lo que le queda libre para su rareza (RF-023).
/// </summary>
public sealed record RewardCarrier(
    int PlayerId,
    string Name,
    Position Position,
    Rarity Rarity,
    int Level,
    PhysicalState PhysicalState,
    int FreeSlots,
    string? CurrentItemId);

/// <summary>
/// Una de las opciones de la elección (RF-071, ADR 0049: dos en liga, tres en élite y en jefe).
/// <paramref name="Description"/> viene <b>generada</b> del efecto o del dato (RT-035): en esta pantalla
/// no hay ni una frase escrita a mano.
/// </summary>
public sealed record RewardOptionView(
    int Index,
    RewardKind Kind,
    string Id,
    string Name,
    Rarity Rarity,
    string Description,
    string Headline,
    bool NeedsCarrier,
    IReadOnlyList<RewardCarrier> Carriers,
    RewardBlock Block);

/// <summary>
/// La pantalla de recompensa entera (RF-071, RF-071b, ADR 0043, ADR 0049).
/// </summary>
/// <param name="Picks">Elecciones que da el nodo: 1 en liga y élite, 2 en el jefe.</param>
/// <param name="PicksTaken">Elecciones ya resueltas (cobradas o rechazadas).</param>
/// <param name="RerollCost">Coste del siguiente reroll, creciente con los usados en la run (RF-071b).</param>
/// <param name="CanReroll">False si ya se usó el de este nodo o si no llega el oro.</param>
public sealed record RewardScreenView(
    int NodeId,
    NodeKind NodeKind,
    int Act,
    int Gold,
    int Picks,
    int PicksTaken,
    int RerollCost,
    bool CanReroll,
    bool RerollUsedHere,
    IReadOnlyList<RewardOptionView> Options);

/// <summary>
/// Compone la pantalla de recompensa desde el estado. Puro: el surtido lo sortea
/// <see cref="RewardSystem.Options"/> con el flujo de recompensas (RT-022), aquí solo se le pone nombre,
/// descripción y lista de portadores posibles.
/// </summary>
public static class RewardView
{
    /// <summary>Vista de la recompensa pendiente del nodo abierto; null si no hay ninguna que elegir.</summary>
    public static RewardScreenView? Build(
        RunState state, Catalog catalog, EconomyConfig economy, ItemCatalog items, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(items);

        if (state.PendingNodeId < 0)
        {
            return null;
        }

        var node = state.GetNode(state.PendingNodeId);
        if (!node.IsMatch || RewardSystem.AlreadyClaimed(state, node, economy))
        {
            return null;
        }

        var templates = catalog.Localization.Get(language);
        var options = RewardSystem.Options(state, node, catalog, economy, items);
        var views = new List<RewardOptionView>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            views.Add(Describe(state, catalog, templates, items, options[i], i, language));
        }

        int cost = economy.RerollCost(state.RerollsUsed);
        return new RewardScreenView(
            node.Id,
            node.Kind,
            node.Act,
            state.Gold,
            RewardSystem.PicksFor(node, economy),
            RewardSystem.PicksTaken(state, node.Id),
            cost,
            state.NodeRerolls == 0 && state.Gold >= cost,
            state.NodeRerolls > 0,
            views);
    }

    private static RewardOptionView Describe(
        RunState state,
        Catalog catalog,
        DescriptionTemplates templates,
        ItemCatalog items,
        RewardOption option,
        int index,
        string language)
    {
        switch (option)
        {
            case PerkRewardOption perkOption:
            {
                var perk = catalog.Perks.Get(perkOption.PerkId);
                var carriers = Carriers(state, catalog, PerkPool.EligibleCarriers(state, perk, catalog));
                return new RewardOptionView(
                    index,
                    RewardKind.Perk,
                    perk.Id,
                    perk.Name.Es,
                    perk.Rarity,
                    DescriptionGenerator.Describe(perk, templates),
                    string.Empty,
                    NeedsCarrier: true,
                    carriers,
                    carriers.Count == 0 ? RewardBlock.NoCarrier : RewardBlock.None);
            }

            case ItemRewardOption itemOption:
            {
                var item = items.Get(itemOption.ItemId);
                var carriers = Carriers(state, catalog, AllLivingIds(state));
                return new RewardOptionView(
                    index,
                    RewardKind.Item,
                    item.Id,
                    item.Name.Es,
                    item.Rarity,
                    ItemDescriptions.Describe(item, language),
                    string.Empty,
                    NeedsCarrier: true,
                    carriers,
                    carriers.Count == 0 ? RewardBlock.NoCarrier : RewardBlock.None);
            }

            case PlayerRewardOption playerOption:
            {
                var player = playerOption.Player;
                return new RewardOptionView(
                    index,
                    RewardKind.Player,
                    player.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    player.Name,
                    player.Rarity,
                    PlayerDescriptions.AttributeLine(player, templates),
                    PlayerDescriptions.Headline(player, catalog, templates),
                    NeedsCarrier: false,
                    Array.Empty<RewardCarrier>(),
                    state.HasRosterSpace ? RewardBlock.None : RewardBlock.RosterFull);
            }

            default:
                throw new InvalidOperationException($"tipo de recompensa no reconocido: {option.GetType().Name}");
        }
    }

    /// <summary>Fichas mínimas de los portadores posibles, por id ascendente (RT-041).</summary>
    private static IReadOnlyList<RewardCarrier> Carriers(RunState state, Catalog catalog, IReadOnlyList<int> ids)
    {
        var carriers = new List<RewardCarrier>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            var player = state.FindPlayer(ids[i]);
            if (player is null)
            {
                continue;
            }

            carriers.Add(new RewardCarrier(
                player.Id,
                player.Name,
                player.Position,
                player.Rarity,
                player.Level,
                player.PhysicalState,
                Progression.Progression.PerkSlots(player.Rarity) - player.Perks.Count,
                player.Item));
        }

        return carriers;
    }

    /// <summary>Quien puede recibir un objeto: cualquiera que no esté muerto (RF-075..078).</summary>
    private static IReadOnlyList<int> AllLivingIds(RunState state)
    {
        var ids = new List<int>(state.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState != PhysicalState.Dead)
            {
                ids.Add(state.Roster[i].Id);
            }
        }

        return ids;
    }
}

/// <summary>
/// Descripción generada de un jugador (RT-035 llevado a su caso: un jugador tampoco lleva texto escrito
/// a mano). Se compone de lo que la ficha ya enseña —nivel, rareza, raza, posición y los cinco
/// atributos— con el vocabulario localizado de <c>data/l10n</c>.
/// </summary>
public static class PlayerDescriptions
{
    /// <summary>Los cinco atributos en una línea: "fuerza 61 · velocidad 48 · ...".</summary>
    public static string AttributeLine(RunPlayer player, DescriptionTemplates templates)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(templates);
        var attributes = player.Attributes;
        return string.Join(" · ", new[]
        {
            Pair(templates, "strength", attributes.Strength),
            Pair(templates, "speed", attributes.Speed),
            Pair(templates, "technique", attributes.Technique),
            Pair(templates, "stamina", attributes.Stamina),
            Pair(templates, "leash", attributes.Leash),
        });
    }

    /// <summary>Cabecera: raza, posición y nivel, con el vocabulario del catálogo.</summary>
    public static string Headline(RunPlayer player, Catalog catalog, DescriptionTemplates templates)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(templates);
        return string.Join(" · ", new[]
        {
            catalog.Race(player.Race).Name.Es,
            templates.Get("positions", player.Position.ToString()),
        });
    }

    private static string Pair(DescriptionTemplates templates, string attribute, int value) =>
        templates.Get("attributes", attribute) + " " + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
