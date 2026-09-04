using Underleague.Sim.Engine;

namespace Underleague.Sim.Perks;

/// <summary>
/// Los cuatro jugadores que una condición puede nombrar (§2). Son identificadores NCalc sin comillas,
/// así que llegan al evaluador como parámetros; se resuelven a este enum una sola vez por evaluación.
/// </summary>
internal enum WhoRef
{
    Actor,
    Target,
    Opponent,
    Owner,
}

/// <summary>
/// Vista de solo lectura del partido que necesitan las funciones de condición (§2). La implementa
/// <see cref="MatchEngine"/>; existe como interfaz para que <see cref="EffectEngine"/> y las condiciones
/// no dependan del motor completo y para poder ejercitarlas en los tests.
/// </summary>
internal interface IPerkWorld
{
    /// <summary>Tick actual del partido.</summary>
    int Tick { get; }

    /// <summary>True si el partido está en el gol de oro de la turba (§3.9).</summary>
    bool IsMob { get; }

    /// <summary>Criterio del árbitro desde el punto de vista de team (positivo = favorable).</summary>
    int BiasFor(int team);

    /// <summary>Goles propios menos goles rivales para team.</summary>
    int ScoreDiff(int team);

    /// <summary>Tercio del campo en el que está el jugador, relativo a su propio equipo.</summary>
    Model.Zone ZoneOf(MatchPlayer player);

    /// <summary>Distancia en casillas (redondeo hacia abajo) del jugador a la portería rival.</summary>
    int DistanceToGoalCells(MatchPlayer player);

    /// <summary>Compañeros con esa etiqueta cuya casilla-hogar es contigua a la de player (RF-044).</summary>
    int AdjacentCount(MatchPlayer player, string tag);

    /// <summary>Compañeros en campo con esa etiqueta, excluido player.</summary>
    int TeammatesWithTag(MatchPlayer player, string tag);

    /// <summary>Contador del jugador (RF-070); 0 si no existe.</summary>
    int Counter(MatchPlayer player, string name);
}

/// <summary>
/// Contexto de una evaluación de condición (§2). Struct sin asignaciones: el motor lo compone en la pila
/// y lo pasa por <c>in</c> a <see cref="CompiledCondition.Evaluate"/>.
/// </summary>
internal readonly struct ConditionContext
{
    public ConditionContext(
        IPerkWorld world,
        MatchPlayer owner,
        MatchPlayer? actor,
        MatchPlayer? target,
        MatchPlayer? opponent,
        string detail)
    {
        World = world;
        Owner = owner;
        Actor = actor;
        Target = target;
        Opponent = opponent;
        Detail = detail;
    }

    public IPerkWorld World { get; }

    /// <summary>Portador del perk que se está evaluando.</summary>
    public MatchPlayer Owner { get; }

    public MatchPlayer? Actor { get; }

    public MatchPlayer? Target { get; }

    public MatchPlayer? Opponent { get; }

    /// <summary><see cref="Events.MatchEvent.Detail"/> del evento disparador.</summary>
    public string Detail { get; }

    /// <summary>Resuelve un identificador de condición a un jugador; null si el evento no lo trae.</summary>
    public MatchPlayer? Who(WhoRef who) => who switch
    {
        WhoRef.Actor => Actor,
        WhoRef.Target => Target,
        WhoRef.Opponent => Opponent,
        _ => Owner,
    };
}
