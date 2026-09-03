using Underleague.Sim.Engine;

namespace Underleague.Sim.Tests.Engine;

public class StateMachineTests
{
    private static readonly PlayerAction[] WithoutBallActions =
    {
        PlayerAction.ChaseBall, PlayerAction.MarkOpponent, PlayerAction.OfferSupport,
        PlayerAction.CoverSpace, PlayerAction.Tackle, PlayerAction.Retreat,
    };

    private static readonly PlayerAction[] WithBallActions =
    {
        PlayerAction.Pass, PlayerAction.Dribble, PlayerAction.Shoot,
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

    [Fact]
    public void Dribbling_AllowsOnlyWithBallActions()
    {
        foreach (var action in Enum.GetValues<PlayerAction>())
        {
            bool expected = WithBallActions.Contains(action);
            Assert.Equal(expected, StateMachine.CanPerform(PlayerState.Dribbling, action));
        }
    }

    [Theory]
    [InlineData(PlayerState.Passing)]
    [InlineData(PlayerState.Shooting)]
    [InlineData(PlayerState.Tackling)]
    [InlineData(PlayerState.KnockedDown)]
    [InlineData(PlayerState.Injured)]
    [InlineData(PlayerState.Celebrating)]
    [InlineData(PlayerState.SentOff)]
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
