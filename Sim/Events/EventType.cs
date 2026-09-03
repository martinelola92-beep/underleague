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
    Foul,
    Card,
    Injury,
    Death,
    Substitution,
    ConsumableUsed,
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
        EventType.Foul => "FOUL",
        EventType.Card => "CARD",
        EventType.Injury => "INJURY",
        EventType.Death => "DEATH",
        EventType.Substitution => "SUBSTITUTION",
        EventType.ConsumableUsed => "CONSUMABLE_USED",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };
}
