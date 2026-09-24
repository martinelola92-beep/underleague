using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Events;

/// <summary>
/// Evento ordenado del partido (RF-066, RF-067). -1 en cualquier id no aplicable. Team es el equipo
/// del Actor. Detail es texto corto en inglés y estable (usado en tests y CSV).
/// </summary>
public sealed record MatchEvent(
    EventType Type,
    int Tick,
    int Team,
    int Actor,
    int Target,
    int Opponent,
    Cell Cell,
    Zone Zone,
    MatchPhase Phase,
    int Bias,
    int DistanceToGoal,
    string Detail,

    /// <summary>
    /// Tick del RELOJ DEL PARTIDO (BC-A): el que sólo corre con el balón en juego, y del que sale el
    /// minuto que lee el jugador. <see cref="Tick"/> es el tick del motor, que cuenta también las
    /// reanudaciones y por tanto ya no sirve para decir en qué minuto pasó algo — desde que la reanudación
    /// espera a que el equipo se recoloque, un partido de 90 minutos termina pasado el tick 1.700 y el
    /// minuto calculado sobre él se iba a 123.
    /// </summary>
    int ClockTick = 0);
