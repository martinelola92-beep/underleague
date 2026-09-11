using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Systems.Economy;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems.Events;

/// <summary>
/// Nodo de evento (RF-011, <b>ADR 0100</b>). Hasta esta ADR se resolvía solo y pagaba 1-3 de oro, cuando un
/// partido de liga paga veinte: era relleno de capa. Ahora es una <b>carta con opciones</b> definida en
/// <c>data/events/</c>, y lo que la hace legítima es que <b>todo coste se ve antes de elegir</b> (RF-012d):
/// un evento no te quita nada por sorpresa, te lo ofrece. No hay tiradas dentro de una opción —la apuesta
/// es la elección, no el dado—, así que lo único aleatorio es <b>qué carta sale</b>, y eso se deriva del
/// nodo (<c>RngStreams.Rewards</c>), no se guarda: dos llamadas con el mismo estado ven la misma carta.
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
    /// Resuelve la opción elegida. Lanza si la opción no existe, si pide un objetivo que no se ha dado (o
    /// que no está disponible), o si el coste en oro no se puede pagar: un evento no deja deudas.
    /// </summary>
    public static RunState Choose(
        RunState state,
        ChooseEventOption decision,
        EventCatalog events,
        Data.Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(events);
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
            state = Apply(state, option.Effects[i], target?.Id ?? -1, catalog);
        }

        return state;
    }

    private static RunState Apply(RunState state, EventEffect effect, int targetId, Data.Catalog catalog) => effect.Kind switch
    {
        EventEffectKind.Gold => state.AddGold(effect.Value),
        EventEffectKind.GoldShare => state.AddGold(state.Gold * effect.Value / 100),
        EventEffectKind.Heal => Heal(state),
        EventEffectKind.Experience => Experience(state, effect.Value, catalog, onlyStarters: true, targetId: -1),
        EventEffectKind.ExperienceTarget => Experience(state, effect.Value, catalog, onlyStarters: false, targetId: targetId),
        EventEffectKind.Injure => Injure(state, targetId, effect.Value),
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
}
