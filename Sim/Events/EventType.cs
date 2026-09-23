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
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };
}
