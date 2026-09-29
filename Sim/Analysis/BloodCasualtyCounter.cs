using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Analysis;

/// <summary>
/// ADR 0168, el instrumento de la métrica guardiana de la sangre: bajas de sangre propias de un partido. Se cuenta
/// desde los hechos del partido, no desde la diferencia de estado (revisión independiente: la subida de graves por
/// nodo no veía el partido que termina la run ni una grave tapada por otra que muere).
/// </summary>
public static class BloodCasualtyCounter
{
    /// <summary>
    /// Jugadores propios (del local: en la run el jugador es siempre local, W-15) <b>distintos</b> que sufren en el
    /// partido una lesión grave o una muerte no anuladas por un perk. Una grave que acaba en muerte es una baja, no
    /// dos; una lesión leve (también la de la turba, ADR 0167) no cuenta.
    /// </summary>
    public static int Count(MatchSetup setup, MatchResult result)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        var own = new HashSet<int>();
        for (int i = 0; i < setup.Home.Players.Count; i++)
        {
            own.Add(setup.Home.Players[i].Id);
        }

        var hurt = new HashSet<int>();
        for (int i = 0; i < result.Events.Count; i++)
        {
            var e = result.Events[i];
            bool blood = e.Type == EventType.Death
                || (e.Type == EventType.Injury && e.Detail.StartsWith("severe", StringComparison.Ordinal));
            if (blood && !e.Detail.EndsWith(":cancelled", StringComparison.Ordinal) && own.Contains(e.Actor))
            {
                hurt.Add(e.Actor);
            }
        }

        return hurt.Count;
    }
}
