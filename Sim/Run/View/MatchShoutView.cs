using Underleague.Sim.Model;
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
    /// Id que lleva un <see cref="ActiveShout"/> impuesto por la entrada de la turba (ADR 0167), que no viene de
    /// ningún consumible: <c>mob:</c> + id del tipo. Dura hasta el final del partido.
    /// </summary>
    public const string MobPrefix = "mob:";

    /// <summary>Ticks de una conducta impuesta por la turba al entrar: hasta el final (igual que el motor).</summary>
    public const int MobUntilTheEnd = 1_000_000;

    /// <summary>
    /// Gritos activos de <paramref name="team"/> en <paramref name="tick"/>, por orden de activación. Con
    /// <paramref name="mob"/> (ADR 0167) cuenta también lo que impone la turba —al entrar, hasta el final; y al
    /// provocarla con el consumible, de cualquiera de los dos equipos—, por la misma regla que el motor.
    /// </summary>
    /// <param name="events">Eventos del partido (<c>MatchResult.Events</c>).</param>
    /// <param name="consumables">Los consumibles equipados por ese equipo (<c>TeamSetup.Consumables</c>).</param>
    /// <param name="mob">Tipo de turba del partido (<c>MatchSetup.Mob</c>); null sin tipo.</param>
    /// <param name="otherConsumables">Los del otro equipo: su «Provocar a la grada» también mueve a éste.</param>
    public static IReadOnlyList<ActiveShout> ActiveAt(
        IReadOnlyList<MatchEvent> events,
        IReadOnlyList<MatchConsumable> consumables,
        int team,
        int tick,
        MobSetup? mob = null,
        IReadOnlyList<MatchConsumable>? otherConsumables = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(consumables);
        otherConsumables ??= Array.Empty<MatchConsumable>();

        // Como en el motor (MatchEngine.StartShout), un grito de orden sustituye al de orden anterior y una
        // consigna de presión a la anterior: por categoría sólo cuenta el último activado hasta este tick, y
        // cuando ése acaba el equipo vuelve a su orden, aunque al anterior le quedara tiempo (revisión de la
        // ADR 0166). La orden y la presión son independientes y pueden convivir. La turba entra por la misma
        // puerta (ADR 0167).
        ActiveShout? order = null;
        ActiveShout? press = null;
        for (int i = 0; i < events.Count; i++)
        {
            var used = events[i];
            if (used.Tick > tick)
            {
                continue;
            }

            if (used.Type == EventType.MobStart && mob is not null)
            {
                ApplyMob(mob, MobPrefix + mob.Id, used.Tick, MobUntilTheEnd, team, tick, ref order, ref press);
                continue;
            }

            if (used.Type != EventType.ConsumableUsed)
            {
                continue;
            }

            var owned = used.Team == team ? consumables : otherConsumables;
            for (int c = 0; c < owned.Count; c++)
            {
                if (!string.Equals(owned[c].Id, used.Detail, StringComparison.Ordinal))
                {
                    continue;
                }

                var effects = owned[c].Effects;
                for (int e = 0; e < effects.Count; e++)
                {
                    int total = effects[e].Value * TicksPerSecond;
                    if (effects[e].Type == EffectType.ProvokeMob)
                    {
                        if (mob is not null)
                        {
                            ApplyMob(mob, used.Detail, used.Tick, total, team, tick, ref order, ref press);
                        }

                        continue;
                    }

                    if (effects[e].Type != EffectType.Shout || used.Team != team)
                    {
                        continue;
                    }

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

    /// <summary>Lo que la turba impone a <paramref name="team"/> (ADR 0167), como <c>MatchEngine.ApplyMob</c>.</summary>
    private static void ApplyMob(
        MobSetup mob, string id, int startTick, int total, int team, int tick, ref ActiveShout? order, ref ActiveShout? press)
    {
        int left = startTick + total - tick;
        for (int e = 0; e < mob.Effects.Count; e++)
        {
            if (mob.Effects[e] == MobEffectKind.PressBoth)
            {
                press = left > 0 ? new ActiveShout(id, ShoutKind.Press, left, total) : null;
            }
            else if (mob.Effects[e] == MobEffectKind.TheirOffensive && team == 1)
            {
                order = left > 0 ? new ActiveShout(id, ShoutKind.Offensive, left, total) : null;
            }
        }
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
