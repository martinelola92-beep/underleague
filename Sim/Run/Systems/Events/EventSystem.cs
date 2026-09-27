using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems.Events;

/// <summary>
/// Nodo de evento (RF-011, <b>ADR 0100</b>, ampliado por la <b>ADR 0159</b>). Hasta la ADR 0100 se
/// resolvía solo y pagaba 1-3 de oro, cuando un partido de liga paga veinte: era relleno de capa. Ahora es
/// una <b>carta con opciones</b> definida en <c>data/events/</c>, y lo que la hace legítima es que
/// <b>todo coste se ve antes de elegir</b> (RF-012d): un evento no te quita nada por sorpresa, te lo
/// ofrece. No hay tiradas dentro de una opción —la apuesta es la elección, no el dado—, así que lo único
/// aleatorio es <b>qué carta sale</b>, y eso se deriva del nodo (<c>RngStreams.Rewards</c>), no se guarda:
/// dos llamadas con el mismo estado ven la misma carta. La ADR 0159 mantiene el mismo principio para sus
/// ocho efectos nuevos: "cuál objeto" o "cuál consumible" se elige de forma <b>determinista</b> (el de
/// menor id que cumple lo pedido), nunca con una tirada dentro de la opción.
///
/// <para>Las dos familias del primer catálogo salen de dos problemas medidos. La del <b>oro parado</b>
/// cobra un <i>porcentaje de lo que llevas encima</i>, así que quien atesora paga caro: es la palanca que
/// la ADR 0098 dejó anotada al ver que no comprar sigue ganando el 7,8 % de las runs. La de <b>carne por
/// ventaja</b> pide un cuerpo a cambio de algo, que es la identidad del juego dicha en un menú.</para>
/// </summary>
public static class EventSystem
{
    /// <summary>La carta de ese nodo. Derivada, no guardada (W-12): mismo estado, misma carta.</summary>
    public static EventCard Card(RunState state, MapNode node, EventCatalog events)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(events);
        var pool = events.ForAct(node.Act);
        if (pool.Count == 0)
        {
            throw new InvalidOperationException($"no hay ninguna carta de evento disponible en el acto {node.Act}");
        }

        int total = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            total += pool[i].Weight;
        }

        var rng = RngStreams.Rewards(state.Seed, node.Id);
        int roll = rng.Range(0, total);
        int cumulative = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight;
            if (roll < cumulative)
            {
                return pool[i];
            }
        }

        return pool[^1];
    }

    /// <summary>
    /// El árbitro que "sale" en la carta de este nodo (ADR 0159): el mismo cálculo que
    /// <c>RunSystems.RefereeFor</c> usa para decidir quién pita un partido -"el mismo flujo de
    /// recompensas" que pide la ADR-, así que dos llamadas con el mismo estado ven siempre al mismo
    /// árbitro, sin guardar nada nuevo. <c>null</c> si la run todavía no tiene plantel de árbitros (tests
    /// que no lo necesitan).
    /// </summary>
    public static RunReferee? ReferenceReferee(RunState state, MapNode node)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        return state.Referees.Count == 0 ? null : state.Referees[node.Id % state.Referees.Count];
    }

    /// <summary>
    /// Resuelve la opción elegida. Lanza si la opción no existe, si pide un objetivo que no se ha dado (o
    /// que no está disponible), si el coste en oro no se puede pagar (un evento no deja deudas), o si un
    /// efecto de la ADR 0159 no tiene dónde aterrizar (sin hueco de plantilla para <c>recruit</c>, sin
    /// hueco de perk para el heredero de <c>sacrifice</c>, un rasgo que ya tiene o que nunca tuvo). La
    /// vista (<c>EventView</c>) filtra los objetivos inviables antes de que el jugador pueda elegirlos;
    /// esto es la última red, no la primera.
    /// </summary>
    public static RunState Choose(
        RunState state,
        ChooseEventOption decision,
        EventCatalog events,
        ItemCatalog items,
        ConsumableCatalog consumables,
        EconomyConfig economy,
        Data.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(consumables);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(catalog);
        var node = NodeGuards.RequireOpen(state, NodeKind.Event, "resolver un evento");
        var card = Card(state, node, events);
        if (decision.OptionIndex < 0 || decision.OptionIndex >= card.Options.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decision),
                decision.OptionIndex,
                $"la carta '{card.Id}' tiene {card.Options.Count} opciones (0..{card.Options.Count - 1})");
        }

        var option = card.Options[decision.OptionIndex];
        RunPlayer? target = null;
        if (option.NeedsTarget)
        {
            if (decision.TargetPlayerId < 0)
            {
                throw new ArgumentException(
                    $"la opción '{option.Id}' de '{card.Id}' necesita un jugador señalado",
                    nameof(decision));
            }

            target = state.GetPlayer(decision.TargetPlayerId);
            if (!target.IsAvailable)
            {
                throw new ArgumentException(
                    $"el jugador {target.Id} no está disponible: un evento no se cobra en alguien que ya está fuera",
                    nameof(decision));
            }
        }

        RunPlayer? secondTarget = null;
        if (option.NeedsSecondTarget)
        {
            if (decision.SecondTargetPlayerId < 0)
            {
                throw new ArgumentException(
                    $"la opción '{option.Id}' de '{card.Id}' necesita un segundo jugador señalado",
                    nameof(decision));
            }

            if (decision.SecondTargetPlayerId == decision.TargetPlayerId)
            {
                throw new ArgumentException(
                    "el segundo señalado tiene que ser distinto del primero", nameof(decision));
            }

            secondTarget = state.GetPlayer(decision.SecondTargetPlayerId);
            if (!secondTarget.IsAvailable)
            {
                throw new ArgumentException(
                    $"el jugador {secondTarget.Id} no está disponible: un evento no se cobra en alguien que ya está fuera",
                    nameof(decision));
            }
        }

        int cost = 0;
        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            int gold = effect.Kind switch
            {
                EventEffectKind.Gold => effect.Value,
                EventEffectKind.GoldShare => state.Gold * effect.Value / 100,
                _ => 0,
            };
            cost += gold;
        }

        if (state.Gold + cost < 0)
        {
            throw new ArgumentException(
                $"la opción '{option.Id}' cuesta {-cost} de oro y la run solo tiene {state.Gold}",
                nameof(decision));
        }

        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            int effectTargetId = effect.UsesSecondTarget ? secondTarget?.Id ?? -1 : target?.Id ?? -1;
            state = Apply(state, node, effect, effectTargetId, target?.Id ?? -1, secondTarget?.Id ?? -1, items, consumables, economy, catalog);
        }

        return state;
    }

    private static RunState Apply(
        RunState state,
        MapNode node,
        EventEffect effect,
        int effectTargetId,
        int primaryTargetId,
        int secondTargetId,
        ItemCatalog items,
        ConsumableCatalog consumables,
        EconomyConfig economy,
        Data.Catalog catalog) => effect.Kind switch
    {
        EventEffectKind.Gold => state.AddGold(effect.Value),
        EventEffectKind.GoldShare => state.AddGold(state.Gold * effect.Value / 100),
        EventEffectKind.Heal => Heal(state),
        EventEffectKind.Experience => Experience(state, effect.Value, catalog, onlyStarters: true, targetId: -1),
        EventEffectKind.ExperienceTarget => Experience(state, effect.Value, catalog, onlyStarters: false, targetId: effectTargetId),
        EventEffectKind.Injure => Injure(state, effectTargetId, effect.Value),
        EventEffectKind.GrantItem => GrantItem(state, node, effect.Rarity, items),
        EventEffectKind.GrantConsumable => GrantConsumable(state, effect.Family, consumables),
        EventEffectKind.GrantTrait => GrantTrait(state, effectTargetId, effect.Trait),
        EventEffectKind.RemoveTrait => RemoveTrait(state, effectTargetId, effect.Trait),
        EventEffectKind.Attribute => Attribute(state, effectTargetId, effect.Attribute, effect.Value),
        EventEffectKind.Level => LevelDown(state, effectTargetId, effect.Value, catalog),
        EventEffectKind.RefereeGrudge => RefereeGrudge(state, node, effect.Value, catalog),
        EventEffectKind.Recruit => Recruit(state, node, economy, catalog),
        EventEffectKind.Sacrifice => Sacrifice(state, primaryTargetId, secondTargetId, catalog),
        _ => state,
    };

    private static RunState Heal(RunState state)
    {
        var roster = new List<RunPlayer>(state.Roster);
        for (int i = 0; i < roster.Count; i++)
        {
            if (Medical.MedicalSystem.NeedsTreatment(roster[i]))
            {
                roster[i] = roster[i] with { PhysicalState = PhysicalState.Healthy, MinorInjuries = 0 };
            }
        }

        return state.WithRoster(roster);
    }

    private static RunState Experience(RunState state, int amount, Data.Catalog catalog, bool onlyStarters, int targetId)
    {
        var lineup = state.Lineup;
        var roster = new List<RunPlayer>(state.Roster);
        for (int i = 0; i < roster.Count; i++)
        {
            var player = roster[i];
            bool reached = targetId >= 0
                ? player.Id == targetId
                : player.IsAvailable && (!onlyStarters || IsInLineup(lineup, player.Id));
            if (!reached)
            {
                continue;
            }

            int total = player.Experience + amount;
            int level = ProgressionRules.LevelFor(total, catalog.Progression);
            if (level == player.Level)
            {
                roster[i] = player.WithExperience(total);
                continue;
            }

            var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
            definition = ProgressionRules.LevelUp(definition, level, catalog.Progression);
            roster[i] = player with { Experience = total, Level = definition.Level, Attributes = definition.Attributes };
        }

        return state.WithRoster(roster);
    }

    private static bool IsInLineup(Lineup? lineup, int playerId)
    {
        if (lineup is null)
        {
            return true;
        }

        for (int i = 0; i < lineup.Slots.Count; i++)
        {
            if (lineup.Slots[i].PlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    private static RunState Injure(RunState state, int targetId, int severity)
    {
        var player = state.GetPlayer(targetId);
        return severity >= 2
            ? state.WithPlayer(player with { PhysicalState = PhysicalState.SevereInjury, MinorInjuries = 0 })
            : state.WithPlayer(player with
            {
                PhysicalState = PhysicalState.MinorInjury,
                MinorInjuries = player.MinorInjuries + 1,
            });
    }

    /// <summary>
    /// Un objeto de esa rareza, al almacén (ADR 0159): el de menor id que puede salir en esta run
    /// (<c>ItemCatalog.OfferableTo</c>, misma raza y acto que usaría el mercado). Determinista -sin
    /// tirada dentro de la opción (ADR 0100)-: la variedad la pone qué carta sale, no qué objeto exacto
    /// entrega una carta ya elegida. <see cref="EventView"/> deshabilita la opción si no hay ningún
    /// candidato; llegar aquí sin ninguno es un error de datos (una carta pide una rareza que la run no
    /// puede ofrecer nunca a esta raza).
    /// </summary>
    private static RunState GrantItem(RunState state, MapNode node, Rarity rarity, ItemCatalog items)
    {
        var item = BestItem(state, node, rarity, items)
            ?? throw new InvalidOperationException(
                $"no hay ningún objeto de rareza {rarity} disponible para {state.ClubRace} en el acto {node.Act}");
        return state.WithStockedItem(item.Id, 1);
    }

    /// <summary>El objeto de esa rareza con menor id que esta run puede ofrecer, o null si no hay ninguno.</summary>
    internal static ItemDefinition? BestItem(RunState state, MapNode node, Rarity rarity, ItemCatalog items)
    {
        var pool = items.OfferableTo(state.ClubRace, node.Act);
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Rarity == rarity)
            {
                return pool[i];
            }
        }

        return null;
    }

    /// <summary>Un consumible de esa familia, al inventario (ADR 0159): el de menor id, determinista.</summary>
    private static RunState GrantConsumable(RunState state, ConsumableFamily family, ConsumableCatalog consumables)
    {
        var consumable = BestConsumable(family, consumables)
            ?? throw new InvalidOperationException($"no hay ningún consumible de la familia {family} en el catálogo");
        return state.WithCounter(
            RunState.ConsumableOwnedPrefix + consumable.Id,
            state.ConsumablesOwned(consumable.Id) + 1);
    }

    /// <summary>El consumible de esa familia con menor id, o null si el catálogo no tiene ninguno.</summary>
    internal static ConsumableDefinition? BestConsumable(ConsumableFamily family, ConsumableCatalog consumables)
    {
        var all = consumables.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Family == family)
            {
                return all[i];
            }
        }

        return null;
    }

    private static RunState GrantTrait(RunState state, int targetId, Trait trait)
    {
        var player = state.GetPlayer(targetId);
        if (player.Traits.Contains(trait))
        {
            throw new ArgumentException($"el jugador {targetId} ya tiene el rasgo {trait}", nameof(targetId));
        }

        if (player.Traits.Count >= RunRules.MaxTraits)
        {
            throw new ArgumentException(
                $"el jugador {targetId} ya tiene {RunRules.MaxTraits} rasgos (RF-022c): no le cabe otro",
                nameof(targetId));
        }

        var traits = new List<Trait>(player.Traits) { trait };
        return state.WithPlayer(player with { Traits = traits });
    }

    private static RunState RemoveTrait(RunState state, int targetId, Trait trait)
    {
        var player = state.GetPlayer(targetId);
        if (!player.Traits.Contains(trait))
        {
            throw new ArgumentException($"el jugador {targetId} no tiene el rasgo {trait}", nameof(targetId));
        }

        var traits = new List<Trait>(player.Traits);
        traits.Remove(trait);
        return state.WithPlayer(player with { Traits = traits });
    }

    private static RunState Attribute(RunState state, int targetId, AttributeKind attribute, int delta)
    {
        var player = state.GetPlayer(targetId);
        var attributes = Attributes.Clamp(player.Attributes.With(attribute, player.Attributes.Get(attribute) + delta));
        return state.WithPlayer(player with { Attributes = attributes });
    }

    /// <summary><c>levels</c> niveles menos (mínimo 1), con la misma aritmética que sube un nivel (RF-027).</summary>
    private static RunState LevelDown(RunState state, int targetId, int levels, Data.Catalog catalog)
    {
        var player = state.GetPlayer(targetId);
        var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
        var down = ProgressionRules.LevelDown(definition, levels, catalog.Progression);
        if (down.Level == player.Level)
        {
            return state;
        }

        // Regla I: bajar el nivel sin bajar también la experiencia se deshace solo la próxima vez que el
        // jugador gane cualquier cosa (Progression.LevelFor recalcularía el nivel viejo desde la
        // experiencia acumulada). Se acota justo por debajo del umbral del nivel nuevo, conservando el
        // progreso intermedio que ya tuviera dentro de ese nivel.
        int cap = ProgressionRules.MinExperienceForLevel(down.Level + 1, catalog.Progression) - 1;
        int experience = cap >= 0 ? Math.Min(player.Experience, cap) : player.Experience;
        return state.WithPlayer(player with { Level = down.Level, Attributes = down.Attributes, Experience = experience });
    }

    /// <summary>
    /// La memoria del árbitro derivado de la carta (ADR 0159, ADR 0158 tras la revisión independiente):
    /// mueve <see cref="RunReferee.Memory"/> directamente, con la misma cota de datos que usa la memoria
    /// de partido (<c>tuning.referee.memory.memoryCap</c>), no una constante de C#.
    /// </summary>
    private static RunState RefereeGrudge(RunState state, MapNode node, int delta, Data.Catalog catalog)
    {
        var referee = ReferenceReferee(state, node);
        if (referee is null)
        {
            return state;
        }

        int cap = catalog.Tuning.Referee.Memory.MemoryCap;
        int memory = Math.Clamp(referee.Memory + delta, -cap, cap);
        var referees = new List<RunReferee>(state.Referees);
        int index = referees.FindIndex(r => r.Id == referee.Id);
        referees[index] = referee with { Memory = memory };
        return state.WithReferees(referees);
    }

    /// <summary>Un canterano gratis (ADR 0159), con la misma generación procedural que el mercado.</summary>
    private static RunState Recruit(RunState state, MapNode node, EconomyConfig economy, Data.Catalog catalog)
    {
        if (!state.HasRosterSpace)
        {
            throw new InvalidOperationException(
                $"la plantilla está llena: no hay hueco para el canterano (RF-020, ADR 0046)");
        }

        // Índice distinto del que usa Card() (node.Id a secas) para no correlar "qué carta sale" con
        // "qué canterano exacto sale" (mismo convenio que RewardSystem.OfferStream / GeneratedPlayers).
        var rng = RngStreams.Rewards(state.Seed, checked((node.Id * 10_000) + RecruitOffset));
        var position = GeneratedPlayers.PickOutfield(ref rng);
        var youth = GeneratedPlayers.Youth(ref rng, catalog, state.ClubRace, economy.Market.YouthQuality, position);
        return state.WithNewPlayer(youth);
    }

    private const int RecruitOffset = 1;

    /// <summary>
    /// El señalado muere; su mejor perk pasa al segundo señalado (ADR 0159, enmienda de RF-072). "Mejor"
    /// es determinista, RT-041: rareza descendente y, a igualdad, id de perk ascendente -el mismo criterio
    /// que ordena los perks simultáneos de un partido-. Sin perks que transferir, solo muere. El objeto
    /// del muerto vuelve al almacén, igual que una muerte de partido (ADR 0048, condición 4).
    /// </summary>
    private static RunState Sacrifice(RunState state, int victimId, int recipientId, Data.Catalog catalog)
    {
        var victim = state.GetPlayer(victimId);
        var recipient = state.GetPlayer(recipientId);
        string? bestPerk = BestPerk(catalog, victim.Perks);
        if (bestPerk is not null)
        {
            int slots = ProgressionRules.PerkSlots(recipient.Rarity);
            if (recipient.Perks.Count >= slots)
            {
                throw new ArgumentException(
                    $"el jugador {recipientId} no tiene hueco de perk ({recipient.Perks.Count}/{slots}, RF-023): no puede heredar",
                    nameof(recipientId));
            }

            var perks = new List<string>(recipient.Perks) { bestPerk };
            state = state.WithPlayer(recipient with { Perks = perks });
        }

        if (victim.Item is { } itemId)
        {
            state = state.WithStockedItem(itemId, 1);
            victim = victim with { Item = null };
        }

        return state.WithPlayer(victim with { PhysicalState = PhysicalState.Dead });
    }

    /// <summary>El mejor perk de esa lista de ids, o null si está vacía (RT-041: rareza desc., id asc.).</summary>
    internal static string? BestPerk(Data.Catalog catalog, IReadOnlyList<string> perkIds)
    {
        string? best = null;
        Rarity bestRarity = Rarity.Common;
        for (int i = 0; i < perkIds.Count; i++)
        {
            var definition = catalog.Perks.Find(perkIds[i]);
            if (definition is null)
            {
                continue;
            }

            if (best is null || definition.Rarity > bestRarity
                || (definition.Rarity == bestRarity && string.CompareOrdinal(perkIds[i], best) < 0))
            {
                best = perkIds[i];
                bestRarity = definition.Rarity;
            }
        }

        return best;
    }
}
