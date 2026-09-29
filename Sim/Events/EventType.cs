namespace Underleague.Sim.Events;

/// <summary>Catálogo de tipos de evento del partido (RF-066).</summary>
public enum EventType
{
    MatchStart,
    MatchEnd,
    MobStart,
    RefereeLeaves,
    PlayStart,
    PlayEnd,
    PassAttempted,
    PassCompleted,
    PassFailed,
    DribbleAttempted,
    DribbleWon,
    DribbleLost,
    AerialDuel,
    Tackle,
    Recovery,
    Shot,
    Goal,
    Save,
    ShotBlocked,

    /// <summary>
    /// El disparo dio en el marco (ADR 0135 paso 2b). <c>Detail</c> distingue <c>post</c> de
    /// <c>crossbar</c>. Tiene evento propio y no un detalle de <see cref="ShotBlocked"/> porque no es un
    /// bloqueo —nadie lo hizo, lo hizo la madera— y porque contaminaría <c>ShotsBlocked</c>, que mide el
    /// mérito defensivo. Y porque un tiro al palo es de las cosas que un jugador recuerda de un partido:
    /// los principios del proyecto prefieren el evento explícito a la transición invisible.
    /// </summary>
    ShotPost,
    Foul,
    Card,
    Injury,
    Death,
    Substitution,
    ConsumableUsed,

    /// <summary>
    /// C9: un perk se ha activado. <c>Detail</c> lleva el id del perk y <c>Actor</c> su portador.
    /// Existe para que la pantalla de partido pueda ATRIBUIR lo que ocurre —un aviso sobre la cabeza del
    /// jugador— sin calcular ni decidir nada (RT-014). Hasta ahora la activación solo llegaba al informe
    /// de después del partido, así que ningún perk se podía ver mientras se jugaba.
    /// </summary>
    PerkTriggered,

    /// <summary>
    /// Centro (ADR 0136). <c>Detail</c> distingue <c>attempted</c> (sale el centro), <c>volleyed</c> (un
    /// compañero lo remató de primeras) y <c>loose</c> (llegó y no lo remató nadie). Tiene evento propio
    /// y no un detalle de <see cref="PassCompleted"/> porque la jugada que cuenta no es «llegó el pase»
    /// sino «llegó el balón al área y alguien lo empujó»: es la unidad narrativa que el jugador recuerda,
    /// y los principios del proyecto prefieren el evento explícito a la transición invisible.
    /// <para>Se añade al FINAL del enum a propósito: así ningún valor numérico de los anteriores cambia.</para>
    /// </summary>
    Cross,

    /// <summary>
    /// Despeje (Gameplay AI Foundations Pass, P4). <c>Detail</c> distingue <c>attempted</c> (sale el
    /// despeje) y <c>duel</c> (alguien lo disputa en el aire). Tiene evento propio porque para el jugador
    /// es una jugada reconocible —«la sacó de la línea»— y no un pase que salió mal, que es como lo leería
    /// si reutilizara <see cref="PassFailed"/>.
    /// <para>Al FINAL del enum, por el mismo motivo que <see cref="Cross"/>.</para>
    /// </summary>
    Clearance,

    /// <summary>
    /// Reinicio tras un gol (ADR 0151): al terminar la celebración el motor coloca a todos en su sitio de
    /// saque en un solo tick. <c>Detail</c> es <c>goal</c>. Es un evento <b>de presentación</b>, como
    /// <see cref="PerkTriggered"/>: existe para que la pantalla tape el salto con una cortinilla sabiendo
    /// que es un corte, en vez de adivinarlo con un umbral de distancia. No es disparador de perks ni narra
    /// nada en el log (<see cref="EventTypeNames.IsPresentationOnly"/>).
    /// <para>Al FINAL del enum, por el mismo motivo que <see cref="Cross"/>.</para>
    /// </summary>
    TeamsReset,

    /// <summary>
    /// La tirada del destino (ADR 0171): una tirada de lesión <b>grave</b> o de <b>muerte</b> con
    /// probabilidad apreciable. <c>Actor</c> es quien se la juega, <c>Opponent</c> quien tira y
    /// <c>Detail</c> es <c>severe|death:puntosBase:hit|saved</c> —la probabilidad real de esa tirada, en
    /// base 10.000, ya con todos sus modificadores—. Presentación pura, como <see cref="PerkTriggered"/>:
    /// la pantalla enseña el porcentaje que le da el motor y no calcula nada (RT-014). Se emite en el tick
    /// de la tirada, antes del INJURY/DEATH que la sigue, y también cuando <b>no</b> pasa nada (el «se salva»).
    /// <para>Al FINAL del enum, por el mismo motivo que <see cref="Cross"/>.</para>
    /// </summary>
    FateRoll,
}

/// <summary>Conversión de EventType a la forma UPPER_SNAKE usada en datos y logs.</summary>
public static class EventTypeNames
{
    /// <summary>Convierte t a su representación UPPER_SNAKE (p. ej. MatchStart -> "MATCH_START").</summary>
    public static string ToUpperSnake(EventType t) => t switch
    {
        EventType.MatchStart => "MATCH_START",
        EventType.MatchEnd => "MATCH_END",
        EventType.MobStart => "MOB_START",
        EventType.RefereeLeaves => "REFEREE_LEAVES",
        EventType.PlayStart => "PLAY_START",
        EventType.PlayEnd => "PLAY_END",
        EventType.PassAttempted => "PASS_ATTEMPTED",
        EventType.PassCompleted => "PASS_COMPLETED",
        EventType.PassFailed => "PASS_FAILED",
        EventType.DribbleAttempted => "DRIBBLE_ATTEMPTED",
        EventType.DribbleWon => "DRIBBLE_WON",
        EventType.DribbleLost => "DRIBBLE_LOST",
        EventType.AerialDuel => "AERIAL_DUEL",
        EventType.Tackle => "TACKLE",
        EventType.Recovery => "RECOVERY",
        EventType.Shot => "SHOT",
        EventType.Goal => "GOAL",
        EventType.Save => "SAVE",
        EventType.ShotBlocked => "SHOT_BLOCKED",
        EventType.ShotPost => "SHOT_POST",
        EventType.Foul => "FOUL",
        EventType.Card => "CARD",
        EventType.Injury => "INJURY",
        EventType.Death => "DEATH",
        EventType.Substitution => "SUBSTITUTION",
        EventType.ConsumableUsed => "CONSUMABLE_USED",
        EventType.PerkTriggered => "PERK_TRIGGERED",
        EventType.Cross => "CROSS",
        EventType.Clearance => "CLEARANCE",
        EventType.TeamsReset => "TEAMS_RESET",
        EventType.FateRoll => "FATE_ROLL",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    /// <summary>
    /// Eventos que el motor emite <b>para la pantalla</b> y no son jugadas: ningún perk puede colgarse de
    /// ellos (el cargador los rechaza como disparador), no tienen plantilla de descripción y no narran en
    /// el log. Antes eran casos sueltos de <see cref="EventType.PerkTriggered"/> en tres sitios.
    /// </summary>
    public static bool IsPresentationOnly(EventType t) => t is EventType.PerkTriggered or EventType.TeamsReset or EventType.FateRoll;
}
