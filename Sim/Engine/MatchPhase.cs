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

    /// <summary>
    /// En el banquillo (ADR 0094): fuera del campo, sin decidir ni contar, y sus perks no disparan hasta que
    /// entra por una sustitución forzada. Va al final del enum por el mismo motivo que los demás añadidos.
    /// </summary>
    Benched,

    /// <summary>
    /// Protegiendo el balón (Gameplay AI Foundations Pass, P2). Estado de decisión <b>con duración</b>,
    /// hermano exacto de <see cref="Dribbling"/> desde la ADR 0137: se entra con un contador, durante ese
    /// tiempo el portador no vuelve a decidir, y si pierde el balón se corta en el acto. La diferencia con
    /// conducir es lo que hace con el cuerpo —no avanza hacia la portería, se interpone— y con qué
    /// atributo resiste.
    /// </summary>
    Shielding,
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

    /// <summary>Pase en profundidad (AZ-B paso 5, ADR 0091): a una casilla vacía por delante de un compañero en carrera; lo gana quien llega antes.</summary>
    ThroughPass,

    /// <summary>
    /// Derribar a un rival <b>que no lleva el balón</b> para abrir espacio (ADR 0030 §2). Solo contra
    /// rivales dentro de la jugada activa (RF-057) y es falta casi segura si el árbitro la ve.
    /// </summary>
    Block,

    /// <summary>
    /// Centrar (ADR 0136): un pase <b>alto</b> al área rival a un compañero con mejor apertura a portería
    /// que la que tiene el pasador, que lo <b>remata sin controlar</b>. Es el primer pase con altura del
    /// motor —la ADR 0135 §2 dejó los pases rasos a propósito y esta acción reabre esa exclusión para un
    /// único caso—, y existe para que el delantero sin ángulo tenga algo que hacer que no sea fusilar
    /// desde el cordel (BA-E).
    /// </summary>
    Cross,

    /// <summary>
    /// Proteger el balón (Gameplay AI Foundations Pass, D4): el portador <b>conserva el balón sin
    /// avanzar</b>, de espaldas al rival que le aprieta, y resiste la entrada con su <b>fuerza</b> en vez
    /// de con su técnica.
    ///
    /// <para>Es acción propia y no «conducir con otro contexto» porque su consecuencia sobre el balón es
    /// distinta: conducir avanza y expone, proteger renuncia a avanzar a cambio de no perderlo. Es la
    /// respuesta que le faltaba al portador presionado, que hoy solo puede elegir entre tirar el balón o
    /// que se lo quiten (regla 17 del encargo: una acción nueva solo si su consecuencia es propia).</para>
    /// </summary>
    Shield,

    /// <summary>
    /// Despejar (Gameplay AI Foundations Pass, D4): el balón sale <b>alto, largo y sin receptor</b> y
    /// queda en disputa. No es un pase malo: es la renuncia deliberada a la posesión para alejar el
    /// peligro de la portería propia, y por eso tiene consecuencia propia —produce un balón aéreo que
    /// nadie posee, que es la entrada del duelo aéreo y de la segunda jugada—.
    /// </summary>
    Clear,
}
