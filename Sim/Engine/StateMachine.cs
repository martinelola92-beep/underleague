namespace Underleague.Sim.Engine;

/// <summary>
/// Tabla explícita de acciones legales por estado de jugador (RT-089c). Positioning y Chasing permiten
/// las acciones sin balón —incluidas FindSpace y PressCarrier, ADR 0022—; Dribbling permite las acciones
/// con balón; el resto de estados no permite ninguna.
/// </summary>
public static class StateMachine
{
    private static readonly PlayerAction[] WithoutBallActions =
    {
        PlayerAction.ChaseBall,
        PlayerAction.MarkOpponent,
        PlayerAction.OfferSupport,
        PlayerAction.CoverSpace,
        PlayerAction.Tackle,
        PlayerAction.Retreat,
        PlayerAction.FindSpace,
        PlayerAction.PressCarrier,
    };

    private static readonly PlayerAction[] WithBallActions =
    {
        PlayerAction.Pass,
        PlayerAction.Dribble,
        PlayerAction.Shoot,
    };

    private static readonly PlayerAction[] NoActions = Array.Empty<PlayerAction>();

    /// <summary>True si el jugador en state puede decidir ejecutar action.</summary>
    public static bool CanPerform(PlayerState state, PlayerAction action)
    {
        var legal = LegalActions(state);
        for (int i = 0; i < legal.Count; i++)
        {
            if (legal[i] == action)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Acciones legales para state, en el orden de declaración de PlayerAction.</summary>
    public static IReadOnlyList<PlayerAction> LegalActions(PlayerState state) => state switch
    {
        PlayerState.Positioning => WithoutBallActions,
        PlayerState.Chasing => WithoutBallActions,
        PlayerState.Dribbling => WithBallActions,
        PlayerState.Passing => NoActions,
        PlayerState.Shooting => NoActions,
        PlayerState.Tackling => NoActions,
        PlayerState.KnockedDown => NoActions,
        PlayerState.Injured => NoActions,
        PlayerState.Celebrating => NoActions,
        PlayerState.SentOff => NoActions,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    /// <summary>True si en state el jugador evalúa la utilidad de sus acciones (Positioning, Chasing, Dribbling).</summary>
    public static bool IsDecisionState(PlayerState state) =>
        state is PlayerState.Positioning or PlayerState.Chasing or PlayerState.Dribbling;
}
