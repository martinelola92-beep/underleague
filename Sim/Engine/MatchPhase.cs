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

/// <summary>Acción que un jugador puede decidir ejecutar.</summary>
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
}
