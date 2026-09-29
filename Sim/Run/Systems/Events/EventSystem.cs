using Underleague.Sim.Data;
using Underleague.Sim.Generation;
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
/// ocho efectos nuevos: "cuál objeto" o "cuál consumible" se sortea entre los elegibles con un flujo
/// <b>derivado de la carta</b> (<see cref="OfferStream"/>, desplazamiento 7100 + índice del efecto), no con
/// una tirada del jugador ni con un flujo compartido, y la vista enseña el objeto concreto antes de elegir.
///
/// <para><b>Una carta se elige una vez</b> (revisión independiente, 29 sep 2026): elegir una opción resuelve
/// el nodo (<see cref="RunState.NodeResolvedCounter"/>) y una segunda elección lanza; solo queda salir.</para>
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

    /// <summary>Desplazamiento de <see cref="OfferStream"/> del canterano de <c>recruit</c> (tabla en <see cref="OfferStream"/>).</summary>
    private const int RecruitStream = 7000;

    /// <summary>Base de los desplazamientos de <c>grantItem</c>/<c>grantConsumable</c>: 7100 + índice del efecto.</summary>
    private const int GrantStreamBase = 7100;

    /// <summary>
    /// Resuelve la opción elegida <b>y con ella el nodo</b>: una carta se elige una vez, y una segunda
    /// elección lanza (<see cref="RunState.NodeResolvedCounter"/>). Lanza si la opción no existe, si pide un
    /// objetivo que no se ha dado, que no está disponible o que la opción no puede señalar
    /// (<see cref="IsEligibleTarget"/>: es <b>la misma</b> regla con la que <c>EventView</c> deshabilita), si
    /// el coste en oro no se puede pagar (un evento no deja deudas), o si un efecto no tiene dónde aterrizar
    /// (<see cref="IsViable"/>: sin hueco de plantilla para <c>recruit</c>, sin objeto de esa rareza, sin
    /// heredero de perk para <c>sacrifice</c>, sin margen sobre el mínimo de cinco de RF-002b). La vista
    /// filtra antes de que el jugador pueda elegir; esto es la última red, no la primera.
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
        NodeGuards.RequireUnresolved(state, node, "elegir una opción");
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

            if (!IsEligibleTarget(state, catalog, target, option, forSecondTarget: false))
            {
                throw new ArgumentException(
                    $"el jugador {target.Id} no puede ser el señalado de '{option.Id}' de '{card.Id}' (ya tiene el rasgo, no puede perder nivel, atributo al límite, sin perk que pasar...)",
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

            if (!IsEligibleTarget(state, catalog, secondTarget, option, forSecondTarget: true, target?.Id ?? -1))
            {
                throw new ArgumentException(
                    $"el jugador {secondTarget.Id} no puede ser el segundo señalado de '{option.Id}' de '{card.Id}' (para el sacrificio: no puede heredar el perk que pasa)",
                    nameof(decision));
            }
        }

        if (!EffectsResolvable(state, node, option, items, consumables))
        {
            throw new InvalidOperationException(
                $"la opción '{option.Id}' de '{card.Id}' no tiene dónde aterrizar (sin hueco de plantilla, sin objeto o consumible de lo que pide, o sin margen sobre el mínimo de la plantilla)");
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
            state = Apply(state, node, i, effect, effectTargetId, target?.Id ?? -1, secondTarget?.Id ?? -1, items, consumables, economy, catalog);
        }

        return NodeGuards.MarkResolved(state, node);
    }

    private static RunState Apply(
        RunState state,
        MapNode node,
        int effectIndex,
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
        EventEffectKind.GrantItem => GrantItem(state, node, effectIndex, effect.Rarity, items),
        EventEffectKind.GrantConsumable => GrantConsumable(state, node, effectIndex, effect.Family, consumables),
        EventEffectKind.GrantTrait => GrantTrait(state, effectTargetId, effect.Trait),
        EventEffectKind.RemoveTrait => RemoveTrait(state, effectTargetId, effect.Trait),
        EventEffectKind.Attribute => Attribute(state, effectTargetId, effect.Attribute, effect.Value),
        EventEffectKind.Level => LevelDown(state, effectTargetId, effect.Value, catalog),
        EventEffectKind.RefereeGrudge => RefereeGrudge(state, node, effect.Value, catalog),
        EventEffectKind.Recruit => Recruit(state, node, economy, catalog),
        EventEffectKind.Sacrifice => Sacrifice(state, primaryTargetId, secondTargetId, catalog, economy, items),
        _ => state,
    };

    /// <summary>
    /// ADR 0170: el efecto <c>heal</c> cura sólo las lesiones <b>leves</b> (antes también las graves, como la tarifa plana
    /// de la clínica). Las graves se arrastran: sólo las cura la clínica, el herrero o el matasanos, y cuestan oro.
    /// </summary>
    private static RunState Heal(RunState state) => Medical.MedicalSystem.HealMinorInjuries(state);

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
    /// Un objeto de esa rareza, al almacén (ADR 0159): <b>sorteado</b> entre los que esta run puede
    /// ofrecer (<see cref="ItemCatalog.OfferableTo(Race, int)"/>, misma raza y acto que usaría el mercado),
    /// con el flujo derivado de la carta y el índice del efecto (<see cref="GrantStreamBase"/>). No hay tirada
    /// del jugador dentro de la opción (ADR 0100): mismo estado, mismo objeto, y <see cref="ItemFor"/> es lo
    /// que la vista usa para decir cuál es antes de elegir. Llegar aquí sin ninguno es un error de datos.
    /// </summary>
    private static RunState GrantItem(RunState state, MapNode node, int effectIndex, Rarity rarity, ItemCatalog items)
    {
        var item = ItemFor(state, node, effectIndex, rarity, items)
            ?? throw new InvalidOperationException(
                $"no hay ningún objeto de rareza {rarity} disponible para {state.ClubRace} en el acto {node.Act}");
        return state.WithStockedItem(item.Id, 1);
    }

    /// <summary>
    /// El objeto concreto que daría el efecto número <paramref name="effectIndex"/> de la carta de este
    /// nodo, o null si la run no puede ofrecer ninguno de esa rareza. Determinista: el flujo sale de
    /// (semilla, nodo, índice de efecto).
    /// </summary>
    internal static ItemDefinition? ItemFor(RunState state, MapNode node, int effectIndex, Rarity rarity, ItemCatalog items)
    {
        var pool = items.OfferableTo(state.ClubRace, node.Act);
        var candidates = new List<ItemDefinition>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Rarity == rarity)
            {
                candidates.Add(pool[i]);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var rng = OfferStream.For(state.Seed, node.Id, GrantStreamBase + effectIndex);
        return candidates[rng.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Un consumible de esa familia, a un hueco libre y ya equipado (ADR 0159, ADR 0172): sorteado con el flujo
    /// de la carta. <see cref="EffectsResolvable"/> ya ha comprobado que hay hueco.
    /// </summary>
    private static RunState GrantConsumable(RunState state, MapNode node, int effectIndex, ConsumableFamily family, ConsumableCatalog consumables)
    {
        var consumable = ConsumableFor(state, node, effectIndex, family, consumables)
            ?? throw new InvalidOperationException($"no hay ningún consumible de la familia {family} que la run pueda llevar ahora");
        return state.WithTakenConsumable(consumable.Id);
    }

    /// <summary>
    /// El consumible concreto que daría ese efecto (mismo criterio que <see cref="ItemFor"/>), o null si el
    /// catálogo no tiene ninguno de esa familia <b>que la run pueda llevar</b>: los que ya lleva quedan fuera
    /// del sorteo (ADR 0172, no se lleva el mismo dos veces), no se sortean para luego rechazarlos.
    /// </summary>
    internal static ConsumableDefinition? ConsumableFor(
        RunState state, MapNode node, int effectIndex, ConsumableFamily family, ConsumableCatalog consumables)
    {
        var all = consumables.All;
        var candidates = new List<ConsumableDefinition>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Family == family && !state.CarriesConsumable(all[i].Id))
            {
                candidates.Add(all[i]);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        var rng = OfferStream.For(state.Seed, node.Id, GrantStreamBase + effectIndex);
        return candidates[rng.Range(0, candidates.Count)];
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
        if (!LevelLoss.CanLose(player))
        {
            throw new ArgumentException(
                $"el jugador {targetId} está en el nivel 1: perder un nivel no le cuesta nada y la opción no se ofrece",
                nameof(targetId));
        }

        // Regla I: bajar el nivel sin bajar también la experiencia se deshace solo la próxima vez que el
        // jugador gane cualquier cosa; LevelLoss la deja en el mínimo del nivel nuevo.
        return state.WithPlayer(LevelLoss.Apply(player, levels, catalog));
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

        // Desplazamiento propio (7000, tabla en OfferStream): el 1 que usaba antes era el reroll de la
        // recompensa, un dado compartido.
        var rng = OfferStream.For(state.Seed, node.Id, RecruitStream);
        var position = GeneratedPlayers.PickOutfield(ref rng);
        var youth = GeneratedPlayers.Youth(ref rng, catalog, state.ClubRace, economy.Market.YouthQuality, position);

        // BA-G, ADR 0169: el canterano del evento tampoco se llama como nadie de la plantilla (después del sorteo, así
        // que el flujo del evento gasta lo mismo que antes).
        youth = RunNames.Distinct(state, node, new[] { youth }, new[] { 0 }, catalog)[0];
        return RunNames.Admit(state, youth, catalog);
    }

    /// <summary>
    /// El señalado muere; su perk pasa al segundo señalado (ADR 0159, enmienda de RF-072). Qué perk pasa lo
    /// decide <see cref="SacrificePerk"/> y quién puede heredarlo <see cref="Heirs"/>, con la elegibilidad de
    /// perks que ya usan las recompensas (<see cref="PerkPool.EligibleCarriers"/>: posición, etiquetas, hueco
    /// y duplicados). La muerte tiene <b>las mismas consecuencias de run que una muerte de partido</b>
    /// (<see cref="DeathConsequences.Kill"/>: objeto al almacén, reliquia, Herencia, oro de muerte) y saca al
    /// muerto de la alineación guardada. Necesita margen sobre el mínimo de la plantilla (RF-002b).
    /// </summary>
    private static RunState Sacrifice(
        RunState state, int victimId, int heirId, Data.Catalog catalog, EconomyConfig economy, ItemCatalog items)
    {
        if (state.AvailablePlayerCount <= RunRules.MinimumAvailablePlayers)
        {
            throw new InvalidOperationException(
                $"con {state.AvailablePlayerCount} disponibles no se puede sacrificar a nadie: el mínimo es {RunRules.MinimumAvailablePlayers} (RF-002b)");
        }

        var victim = state.GetPlayer(victimId);
        string perkId = SacrificePerk(state, catalog, victim)
            ?? throw new ArgumentException(
                $"el jugador {victimId} no tiene ningún perk que un compañero pueda heredar", nameof(victimId));
        if (!Heirs(state, catalog, victim, perkId).Contains(heirId))
        {
            throw new ArgumentException(
                $"el jugador {heirId} no puede heredar el perk '{perkId}' (ya lo lleva, sin hueco o no lo admite su posición o sus etiquetas)",
                nameof(heirId));
        }

        var heir = state.GetPlayer(heirId);
        state = state.WithPlayer(PerkPool.WithPerk(heir, perkId));
        return DeathConsequences.Kill(state, victimId, PlayerDeathCause.Sacrifice, catalog, economy, items);
    }

    /// <summary>
    /// El perk que pasaría si <paramref name="victim"/> fuera el sacrificado: el de mayor rareza y, a
    /// igualdad, id ascendente (RT-041) <b>entre los que algún compañero puede heredar</b>
    /// (<see cref="Heirs"/>); null si ninguno. La vista lo nombra antes de elegir (RF-012d).
    /// </summary>
    internal static string? SacrificePerk(RunState state, Data.Catalog catalog, RunPlayer victim)
    {
        var transferable = new List<string>(victim.Perks.Count);
        for (int i = 0; i < victim.Perks.Count; i++)
        {
            if (Heirs(state, catalog, victim, victim.Perks[i]).Count > 0)
            {
                transferable.Add(victim.Perks[i]);
            }
        }

        return BestPerk(catalog, transferable);
    }

    /// <summary>
    /// Ids de los disponibles que pueden heredar ese perk de <paramref name="victim"/>: los mismos que
    /// <see cref="PerkPool.EligibleCarriers"/> daría a una recompensa (sin ese perk, con hueco de rareza,
    /// posición y etiquetas válidas), sin el propio sacrificado. Orden de id ascendente.
    /// </summary>
    internal static IReadOnlyList<int> Heirs(RunState state, Data.Catalog catalog, RunPlayer victim, string perkId)
    {
        var perk = catalog.Perks.Find(perkId);
        if (perk is null)
        {
            return Array.Empty<int>();
        }

        var carriers = PerkPool.EligibleCarriers(state, perk, catalog);
        var heirs = new List<int>(carriers.Count);
        for (int i = 0; i < carriers.Count; i++)
        {
            if (carriers[i] != victim.Id && state.GetPlayer(carriers[i]).IsAvailable)
            {
                heirs.Add(carriers[i]);
            }
        }

        return heirs;
    }

    /// <summary>
    /// Si <paramref name="player"/> puede ser el primer objetivo (<paramref name="forSecondTarget"/>
    /// falso) o el segundo (ADR 0159) de esa opción. Fuera quedan: el que ya tiene el rasgo que
    /// <c>grantTrait</c> daría (o no le cabe otro), el que no tiene el que <c>removeTrait</c> quitaría, el de
    /// nivel 1 ante una pérdida de nivel (saldría gratis), el que ya está en 99 (o en 1) en el atributo que
    /// se mueve, y en el sacrificio: la plantilla sin margen sobre el mínimo, el que no tiene un perk que
    /// alguien pueda heredar y el heredero que no puede llevar <b>el perk que pasa</b> del primero
    /// (<paramref name="firstTargetId"/>). Compartido por <c>EventView</c> (filtra la lista que ve el
    /// jugador), <c>RunPolicy</c> (elige por él) y <see cref="Choose"/> (la última red): las tres tienen
    /// que estar de acuerdo, o una dejaría pasar lo que otra rechaza.
    /// </summary>
    internal static bool IsEligibleTarget(
        RunState state, Data.Catalog catalog, RunPlayer player, EventOption option, bool forSecondTarget, int firstTargetId = -1)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(option);
        if (!player.IsAvailable || (forSecondTarget && player.Id == firstTargetId))
        {
            return false;
        }

        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            if (effect.Kind == EventEffectKind.Sacrifice)
            {
                if (state.AvailablePlayerCount <= RunRules.MinimumAvailablePlayers)
                {
                    return false;
                }

                if (!forSecondTarget)
                {
                    if (SacrificePerk(state, catalog, player) is null)
                    {
                        return false;
                    }
                }
                else
                {
                    if (firstTargetId < 0 || !state.Roster.Any(p => p.Id == firstTargetId))
                    {
                        return false;
                    }

                    var victim = state.GetPlayer(firstTargetId);
                    string? perkId = SacrificePerk(state, catalog, victim);
                    if (perkId is null || !Heirs(state, catalog, victim, perkId).Contains(player.Id))
                    {
                        return false;
                    }
                }

                continue;
            }

            if (effect.UsesSecondTarget != forSecondTarget)
            {
                continue;
            }

            switch (effect.Kind)
            {
                case EventEffectKind.GrantTrait:
                    if (player.Traits.Contains(effect.Trait) || player.Traits.Count >= RunRules.MaxTraits)
                    {
                        return false;
                    }

                    break;
                case EventEffectKind.RemoveTrait:
                    if (!player.Traits.Contains(effect.Trait))
                    {
                        return false;
                    }

                    break;
                case EventEffectKind.Level:
                    if (!LevelLoss.CanLose(player))
                    {
                        return false;
                    }

                    break;
                case EventEffectKind.Attribute:
                    int current = player.Attributes.Get(effect.Attribute);
                    if ((effect.Value > 0 && current >= AttributeCap) || (effect.Value < 0 && current <= AttributeFloor))
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    /// <summary>Tope y suelo de un atributo (<c>Attributes.Clamp</c>): mover más allá no cambia nada.</summary>
    internal const int AttributeCap = 99;

    internal const int AttributeFloor = 1;

    /// <summary>
    /// Los efectos que no dependen de un jugador señalado pero sí de que exista dónde aterrizar: un objeto de
    /// esa rareza, un consumible de esa familia y un hueco libre donde llevarlo (ADR 0172), hueco de plantilla para el canterano, margen sobre el mínimo
    /// de RF-002b para el sacrificio. Es la mitad de la viabilidad que <see cref="IsEligibleTarget"/> no
    /// cubre; <see cref="Choose"/> y <c>EventView</c> la comparten.
    /// </summary>
    internal static bool EffectsResolvable(
        RunState state, MapNode node, EventOption option, ItemCatalog items, ConsumableCatalog consumables)
    {
        int consumablesGranted = 0;
        for (int i = 0; i < option.Effects.Count; i++)
        {
            var effect = option.Effects[i];
            switch (effect.Kind)
            {
                case EventEffectKind.GrantItem when ItemFor(state, node, i, effect.Rarity, items) is null:
                    return false;
                case EventEffectKind.GrantConsumable:
                    // Sin hueco libre no hay dónde aterrizar (RF-080, ADR 0172): la opción no es viable, como
                    // la del canterano sin sitio en la plantilla.
                    consumablesGranted++;
                    if (ConsumableFor(state, node, i, effect.Family, consumables) is null
                        || state.Consumables.Count + consumablesGranted > RunRules.ConsumableSlots)
                    {
                        return false;
                    }

                    break;
                case EventEffectKind.Recruit when !state.HasRosterSpace:
                    return false;
                case EventEffectKind.Sacrifice when state.AvailablePlayerCount <= RunRules.MinimumAvailablePlayers:
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Los disponibles que esa opción puede señalar como primer objetivo (<paramref name="forSecondTarget"/>
    /// falso) o como segundo (dado el primero en <paramref name="firstTargetId"/>). Id ascendente.
    /// </summary>
    internal static IReadOnlyList<RunPlayer> EligibleTargets(
        RunState state, Data.Catalog catalog, EventOption option, bool forSecondTarget, int firstTargetId = -1)
    {
        var rows = new List<RunPlayer>(state.Roster.Count);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (IsEligibleTarget(state, catalog, state.Roster[i], option, forSecondTarget, firstTargetId))
            {
                rows.Add(state.Roster[i]);
            }
        }

        return rows;
    }

    /// <summary>
    /// Si la opción se puede resolver ahora: hay oro para su coste, dónde aterrizan sus efectos y una
    /// combinación de señalados válida (un primero con al menos un segundo distinto que también valga). Es
    /// <b>exactamente</b> lo que <see cref="Choose"/> aceptaría, y es lo que <c>EventView</c> pone en
    /// <c>Affordable</c> y <c>RunPolicy</c> consulta antes de elegir.
    /// </summary>
    internal static bool IsViable(
        RunState state, Data.Catalog catalog, MapNode node, EventOption option, ItemCatalog items, ConsumableCatalog consumables)
    {
        if (state.Gold + GoldDelta(option, state) < 0 || !EffectsResolvable(state, node, option, items, consumables))
        {
            return false;
        }

        if (!option.NeedsTarget)
        {
            return true;
        }

        var firsts = EligibleTargets(state, catalog, option, forSecondTarget: false);
        if (!option.NeedsSecondTarget)
        {
            return firsts.Count > 0;
        }

        for (int i = 0; i < firsts.Count; i++)
        {
            if (EligibleTargets(state, catalog, option, forSecondTarget: true, firstTargetId: firsts[i].Id).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lo que esa opción suma o resta al oro, para poder decir si se puede pagar antes de elegir.</summary>
    public static int GoldDelta(EventOption option, RunState state)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(state);
        int delta = 0;
        for (int i = 0; i < option.Effects.Count; i++)
        {
            delta += option.Effects[i].Kind switch
            {
                EventEffectKind.Gold => option.Effects[i].Value,
                EventEffectKind.GoldShare => state.Gold * option.Effects[i].Value / 100,
                _ => 0,
            };
        }

        return delta;
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
