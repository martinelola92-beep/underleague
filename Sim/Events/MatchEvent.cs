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
    string Detail);
