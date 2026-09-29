using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Un grito del entrenador en curso en un tick (ADR 0166): cuál, qué grita y cuánto le queda.
/// </summary>
/// <param name="ConsumableId">Id del consumible que lo gritó.</param>
/// <param name="Kind">Orden (<see cref="ShoutKind.Defensive"/>, <see cref="ShoutKind.Offensive"/>) o consigna de presión.</param>
/// <param name="TicksLeft">Ticks que le quedan, contando el actual (≥ 1).</param>
/// <param name="TicksTotal">Ticks que dura entero.</param>
public readonly record struct ActiveShout(string ConsumableId, ShoutKind Kind, int TicksLeft, int TicksTotal)
{
    /// <summary>Segundos que le quedan, redondeados hacia arriba: lo que enseña la cuenta atrás (15 ticks/s, RT-020).</summary>
    public int SecondsLeft => (TicksLeft + 14) / 15;
}

/// <summary>
/// Qué gritos tiene activos un equipo en un tick, para que la pantalla los enseñe junto a la orden (ADR
/// 0166: «eventos explícitos > transiciones invisibles»). Es una lectura pura de la secuencia de eventos —el
/// <c>CONSUMABLE_USED</c> marca cuándo empezó— y de la definición del consumible, sin volver a simular nada
/// (RT-014): el motor aplica y retira el grito por ticks (<c>MatchEngine.StartShout</c>), y esto reproduce la
/// misma cuenta.
/// </summary>
public static class MatchShoutView
{
    private const int TicksPerSecond = 15;

    /// <summary>
    /// Gritos activos de <paramref name="team"/> en <paramref name="tick"/>, por orden de activación.
    /// </summary>
    /// <param name="events">Eventos del partido (<c>MatchResult.Events</c>).</param>
    /// <param name="consumables">Los consumibles equipados por ese equipo (<c>TeamSetup.Consumables</c>).</param>
    public static IReadOnlyList<ActiveShout> ActiveAt(
        IReadOnlyList<MatchEvent> events, IReadOnlyList<MatchConsumable> consumables, int team, int tick)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(consumables);

        // Como en el motor (MatchEngine.StartShout), un grito de orden sustituye al de orden anterior y una
        // consigna de presión a la anterior: por categoría sólo cuenta el último activado hasta este tick, y
        // cuando ése acaba el equipo vuelve a su orden, aunque al anterior le quedara tiempo (revisión de la
        // ADR 0166). La orden y la presión son independientes y pueden convivir.
        ActiveShout? order = null;
        ActiveShout? press = null;
        for (int i = 0; i < events.Count; i++)
        {
            var used = events[i];
            if (used.Type != EventType.ConsumableUsed || used.Team != team || used.Tick > tick)
            {
                continue;
            }

            for (int c = 0; c < consumables.Count; c++)
            {
                if (!string.Equals(consumables[c].Id, used.Detail, StringComparison.Ordinal))
                {
                    continue;
                }

                var effects = consumables[c].Effects;
                for (int e = 0; e < effects.Count; e++)
                {
                    if (effects[e].Type != EffectType.Shout)
                    {
                        continue;
                    }

                    int total = effects[e].Value * TicksPerSecond;
                    int left = used.Tick + total - tick;
                    ActiveShout? shout = left > 0 ? new ActiveShout(used.Detail, effects[e].Shout, left, total) : null;
                    if (effects[e].Shout == ShoutKind.Press)
                    {
                        press = shout;
                    }
                    else
                    {
                        order = shout;
                    }
                }

                // Un mismo id equipado dos veces es un solo CONSUMABLE_USED por activación: se lee una vez.
                break;
            }
        }

        List<ActiveShout>? active = null;
        foreach (var shout in new[] { order, press })
        {
            if (shout is { } s)
            {
                active ??= new List<ActiveShout>();
                active.Add(s);
            }
        }

        // Por orden de activación, como promete el resumen.
        active?.Sort((a, b) => (b.TicksTotal - b.TicksLeft).CompareTo(a.TicksTotal - a.TicksLeft));

        return (IReadOnlyList<ActiveShout>?)active ?? Array.Empty<ActiveShout>();
    }

    /// <summary>
    /// La orden con la que juega el equipo: la del último grito de orden en curso, o
    /// <paramref name="playerOrder"/> —la que puso el jugador— si no hay ninguno. Es lo mismo que decide
    /// <c>MatchEngine.EffectiveOrder</c> (un grito de orden posterior sustituye al anterior).
    /// </summary>
    public static Mentality EffectiveOrder(IReadOnlyList<ActiveShout> shouts, Mentality playerOrder)
    {
        ArgumentNullException.ThrowIfNull(shouts);
        var order = playerOrder;
        for (int i = 0; i < shouts.Count; i++)
        {
            if (shouts[i].Kind == ShoutKind.Defensive)
            {
                order = Mentality.Defensive;
            }
            else if (shouts[i].Kind == ShoutKind.Offensive)
            {
                order = Mentality.Offensive;
            }
        }

        return order;
    }
}
