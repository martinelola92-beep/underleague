using Underleague.Sim.Engine;

namespace Underleague.Sim.Tests.Engine;

public class StateMachineTests
{
    private static readonly PlayerAction[] WithoutBallActions =
    {
        PlayerAction.ChaseBall, PlayerAction.MarkOpponent, PlayerAction.OfferSupport,
        PlayerAction.CoverSpace, PlayerAction.Tackle, PlayerAction.Retreat,
        PlayerAction.FindSpace, PlayerAction.PressCarrier, PlayerAction.Block,
    };

    private static readonly PlayerAction[] WithBallActions =
    {
        PlayerAction.Dribble, PlayerAction.Shoot, PlayerAction.ShortPass, PlayerAction.LongPass,
        PlayerAction.ThroughPass,
        // ADR 0136: centrar es una acción CON balón, como los otros tres pases.
        PlayerAction.Cross,
        // ADR 0138: las dos respuestas que le faltaban al portador. Proteger conserva el balón sin
        // avanzar; despejar renuncia a él. Las dos exigen llevarlo, así que son acciones CON balón.
        PlayerAction.Shield, PlayerAction.Clear,
    };

    [Theory]
    [InlineData(PlayerState.Positioning)]
    [InlineData(PlayerState.Chasing)]
    public void WithoutBallStates_AllowOnlyWithoutBallActions(PlayerState state)
    {
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            bool expected = WithoutBallActions.Contains(action);
            Assert.Equal(expected, StateMachine.CanPerform(state, action));
        }
    }

    /// <summary>
    /// Proteger es un estado de decisión con duración (ADR 0138), igual que conducir: mientras dura no se
    /// decide, y al expirar el portador vuelve a tener delante <b>todas</b> las acciones con balón. Por eso
    /// comparte tabla con <see cref="PlayerState.Dribbling"/> y no tiene una propia.
    /// </summary>
    [Theory]
    [InlineData(PlayerState.Dribbling)]
    [InlineData(PlayerState.Shielding)]
    public void WithBallStates_AllowOnlyWithBallActions(PlayerState state)
    {
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            bool expected = WithBallActions.Contains(action);
            Assert.Equal(expected, StateMachine.CanPerform(state, action));
        }
    }

    [Theory]
    [InlineData(PlayerState.Passing)]
    [InlineData(PlayerState.Shooting)]
    [InlineData(PlayerState.Tackling)]
    [InlineData(PlayerState.Blocking)]
    [InlineData(PlayerState.KnockedDown)]
    [InlineData(PlayerState.Injured)]
    [InlineData(PlayerState.Celebrating)]
    [InlineData(PlayerState.SentOff)]
    [InlineData(PlayerState.Benched)]
    public void TerminalOrBusyStates_AllowNoActions(PlayerState state)
    {
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            Assert.False(StateMachine.CanPerform(state, action));
        }

        Assert.Empty(StateMachine.LegalActions(state));
    }

    [Theory]
    [InlineData(PlayerState.Positioning, true)]
    [InlineData(PlayerState.Chasing, true)]
    [InlineData(PlayerState.Dribbling, true)]
    [InlineData(PlayerState.Passing, false)]
    [InlineData(PlayerState.Shooting, false)]
    [InlineData(PlayerState.Tackling, false)]
    [InlineData(PlayerState.Blocking, false)]
    [InlineData(PlayerState.KnockedDown, false)]
    [InlineData(PlayerState.Injured, false)]
    [InlineData(PlayerState.Celebrating, false)]
    [InlineData(PlayerState.SentOff, false)]
    public void IsDecisionState_MatchesSpec(PlayerState state, bool expected)
    {
        Assert.Equal(expected, StateMachine.IsDecisionState(state));
    }

    [Fact]
    public void LegalActions_AreListedInEnumOrder()
    {
        var positioning = StateMachine.LegalActions(PlayerState.Positioning);
        Assert.Equal(positioning.OrderBy(a => (int)a).ToList(), positioning);

        var dribbling = StateMachine.LegalActions(PlayerState.Dribbling);
        Assert.Equal(dribbling.OrderBy(a => (int)a).ToList(), dribbling);
    }
}
