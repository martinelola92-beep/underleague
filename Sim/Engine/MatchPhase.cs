namespace Underleague.Sim.Engine;

/// <summary>Máquina de estados del partido.</summary>
public enum MatchPhase
{
    Kickoff,
    OpenPlay,
    Restart,
    Penalty,
    RegulationEnd,
    MobGoldenGoal,
    Finished,
}

/// <summary>Estado táctico de un equipo respecto a la posesión del balón.</summary>
public enum TacticalState
{
    InPossession,
    OutOfPossession,
    OffensiveTransition,
    DefensiveTransition,
}

/// <summary>Máquina de estados de un jugador durante el partido.</summary>
public enum PlayerState
{
    Positioning,
    Chasing,
    Dribbling,
    Passing,
    Shooting,
    Tackling,
    KnockedDown,
    Injured,
    Celebrating,
    SentOff,
}

/// <summary>
/// Acción que un jugador puede decidir ejecutar. <see cref="FindSpace"/> y <see cref="PressCarrier"/>
/// son las dos acciones sin balón de la ADR 0022; se añaden <b>al final</b> del enum a propósito: el
/// desempate de utilidad es por orden de declaración (RT-097), así que colocarlas al final deja intactas
/// las prioridades relativas de las siete acciones anteriores.
/// </summary>
public enum PlayerAction
{
    ChaseBall,
    MarkOpponent,
    OfferSupport,
    CoverSpace,
    Pass,
    Dribble,
    Shoot,
    Tackle,
    Retreat,

    /// <summary>Buscar el mejor hueco para recibir (ADR 0022, §2.3): sustituye al punto fijo de OfferSupport.</summary>
    FindSpace,

    /// <summary>Presionar al poseedor rival, o al portero rival en su salida (ADR 0022, §2.3).</summary>
    PressCarrier,
}
