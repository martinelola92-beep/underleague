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

    /// <summary>
    /// Cargando contra un rival <b>sin balón</b> (ADR 0030 §2). Es el gemelo sin balón de
    /// <see cref="Tackling"/>: dura sus propios ticks y al expirar resuelve el derribo. Va al final del
    /// enum por el mismo motivo que las acciones nuevas: no mover los valores de los estados anteriores.
    /// </summary>
    Blocking,
}

/// <summary>
/// Acción que un jugador puede decidir ejecutar. <see cref="FindSpace"/> y <see cref="PressCarrier"/>
/// son las dos acciones sin balón de la ADR 0022; <see cref="ShortPass"/>, <see cref="LongPass"/> y
/// <see cref="Block"/>, las tres de la ADR 0030. Todas ellas se añaden <b>al final</b> del enum a
/// propósito: el desempate de utilidad es por orden de declaración (RT-097), así que colocarlas al final
/// deja intactas las prioridades relativas de las acciones anteriores.
/// <para>
/// La antigua <c>Pass</c> ya no existe: la ADR 0030 §1 la parte en pase corto y pase largo, que compiten
/// entre sí en la tabla de utilidad. Quitarla del centro del enum no altera el desempate, porque lo que
/// ordena es la posición <b>relativa</b> de las que quedan, y esa no cambia.
/// </para>
/// </summary>
public enum PlayerAction
{
    ChaseBall,
    MarkOpponent,
    OfferSupport,
    CoverSpace,
    Dribble,
    Shoot,
    Tackle,
    Retreat,

    /// <summary>Buscar el mejor hueco para recibir (ADR 0022, §2.3): sustituye al punto fijo de OfferSupport.</summary>
    FindSpace,

    /// <summary>Presionar al poseedor rival, o al portero rival en su salida (ADR 0022, §2.3).</summary>
    PressCarrier,

    /// <summary>Pase a un compañero a corta distancia (ADR 0030 §1). El pase por defecto de todo el mundo.</summary>
    ShortPass,

    /// <summary>
    /// Pase a un compañero lejano (ADR 0030 §1): peso base bajo, escalado por la técnica y por los rasgos
    /// de visión. Un centrocampista técnico abre el juego; uno torpe casi nunca lo intenta.
    /// </summary>
    LongPass,

    /// <summary>
    /// Derribar a un rival <b>que no lleva el balón</b> para abrir espacio (ADR 0030 §2). Solo contra
    /// rivales dentro de la jugada activa (RF-057) y es falta casi segura si el árbitro la ve.
    /// </summary>
    Block,
}
