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
/// Lo que las funciones de condición del rediseño espacial (fase1b-diseno.md §1.5) necesitan y
/// <see cref="IPerkWorld"/> no puede dar: los vínculos direccionales resueltos al construir el partido
/// (ADR 0021), la proximidad real en el momento del evento y las estadísticas que el motor ya lleva para
/// el informe post-partido (RF-119).
/// <para>
/// La implementa <see cref="EffectEngine"/> y no <see cref="MatchEngine"/> a propósito: los vínculos, las
/// estadísticas de perk y los modificadores por par son estado del motor de **perks**, y con 0 perks no
/// existe ninguno de los tres (§3, coste cero).
/// </para>
/// </summary>
internal interface IPerkLinks
{
    /// <summary>True si player tiene vinculado en esa relación (RF-044, ADR 0021).</summary>
    bool HasLink(MatchPlayer player, LinkRelation relation);

    /// <summary>True si hay un compañero con esa etiqueta a <paramref name="cells"/> casillas o menos, ahora.</summary>
    bool NearAlly(MatchPlayer player, string tag, int cells);

    /// <summary>True si hay un rival con esa etiqueta a <paramref name="cells"/> casillas o menos, ahora.</summary>
    bool NearOpponent(MatchPlayer player, string tag, int cells);

    /// <summary>Estadística del jugador en el partido en curso (RF-119).</summary>
    int Stat(MatchPlayer player, MatchStat stat);
}

/// <summary>
/// Contexto de una evaluación de condición (§2). Struct sin asignaciones: el motor lo compone en la pila
/// y lo pasa por <c>in</c> a <see cref="CompiledCondition.Evaluate"/>.
/// </summary>
internal readonly struct ConditionContext
{
    public ConditionContext(
        IPerkWorld world,
        IPerkLinks perks,
        MatchPlayer owner,
        MatchPlayer? actor,
        MatchPlayer? target,
        MatchPlayer? opponent,
        string detail)
    {
        World = world;
        Perks = perks;
        Owner = owner;
        Actor = actor;
        Target = target;
        Opponent = opponent;
        Detail = detail;
    }

    public IPerkWorld World { get; }

    /// <summary>Vínculos, proximidad y estadísticas del motor de perks (fase1b-diseno.md §1.5).</summary>
    public IPerkLinks Perks { get; }

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
