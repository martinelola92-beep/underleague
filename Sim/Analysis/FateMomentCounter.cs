using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Analysis;

/// <summary>
/// ADR 0171, el instrumento de frecuencia de la tirada del destino: cuántas tiradas <c>FATE_ROLL</c> caen sobre un
/// jugador propio (el del local, W-15) en un partido. Son los momentos que la retransmisión convierte en cámara lenta
/// y sello, así que su media por partido es lo que el jugador va a sentir como «lo raro».
/// </summary>
public static class FateMomentCounter
{
    /// <summary>Tiradas del destino del partido cuyo sujeto es un jugador del local.</summary>
    public static int Count(MatchSetup setup, MatchResult result)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        var own = new HashSet<int>();
        for (int i = 0; i < setup.Home.Players.Count; i++)
        {
            own.Add(setup.Home.Players[i].Id);
        }

        int moments = 0;
        for (int i = 0; i < result.Events.Count; i++)
        {
            var e = result.Events[i];
            if (e.Type == EventType.FateRoll && own.Contains(e.Actor))
            {
                moments++;
            }
        }

        return moments;
    }
}
