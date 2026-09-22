using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Tests.Run.Systems.Rivals;

/// <summary>
/// Lectura pura de "quién knaveó a quién" (BE-B, enmienda de la ADR 0124): <see cref="RivalCredits"/> se
/// deriva de <see cref="RunState.Counters"/> con el formato que documenta
/// <see cref="RunState.RivalCreditPrefix"/>, sin tocar el esquema de guardado.
/// <c>Sim.Tests.Run.MatchResolutionRivalCreditTests</c> cubre que el motor escribe la clave; esto cubre
/// que se puede leer de vuelta.
/// </summary>
public sealed class RivalCreditsTests
{
    private const string RivalId = "act1_elf_swiftwing";

    private static string Key(int rivalIndex, int ownPlayerId, string kind, string rivalId = RivalId) =>
        RunState.RivalCreditPrefix + rivalId + ":" + rivalIndex + ":" + ownPlayerId + ":" + kind;

    [Fact]
    public void AgainstParsesAllFourFieldsOfTheKey()
    {
        var state = new RunState().WithCounter(Key(3, 42, "causedInjury"), 2);

        var credits = RivalCredits.Against(state, RivalId);

        var credit = Assert.Single(credits);
        Assert.Equal(RivalId, credit.RivalId);
        Assert.Equal(3, credit.RivalIndex);
        Assert.Equal(42, credit.OwnPlayerId);
        Assert.Equal(RivalCreditKind.CausedInjury, credit.Kind);
        Assert.Equal(2, credit.Count);
    }

    [Fact]
    public void AgainstIgnoresCountersOfOtherRivalsAndOtherSystems()
    {
        var state = new RunState()
            .WithCounter(Key(0, 1, "causedInjury", "other_rival"), 5)
            .WithCounter("itemStock:bandage", 3)
            .WithCounter(Key(0, 1, "causedInjury"), 1);

        var credits = RivalCredits.Against(state, RivalId);

        var credit = Assert.Single(credits);
        Assert.Equal(0, credit.RivalIndex);
        Assert.Equal(1, credit.OwnPlayerId);
    }

    [Fact]
    public void AgainstIgnoresCountersAtZero()
    {
        // WithCounter puede fijar un contador a 0 (por ejemplo tras deshacer algo); una clave a 0 no es un
        // hecho que haya pasado.
        var state = new RunState().WithCounter(Key(0, 1, "causedInjury"), 0);

        Assert.Empty(RivalCredits.Against(state, RivalId));
    }

    [Fact]
    public void MostNotableIsNullWithoutAnyCredit()
    {
        var state = new RunState();

        Assert.Null(RivalCredits.MostNotable(state, RivalId));
    }

    [Fact]
    public void MostNotablePrefersDeathOverAMoreRepeatedInjury()
    {
        var state = new RunState()
            .WithCounter(Key(1, 10, "causedInjury"), 9)
            .WithCounter(Key(2, 11, "causedDeath"), 1);

        var best = RivalCredits.MostNotable(state, RivalId);

        Assert.NotNull(best);
        Assert.Equal(RivalCreditKind.CausedDeath, best!.Kind);
        Assert.Equal(11, best.OwnPlayerId);
    }

    [Fact]
    public void MostNotablePrefersTheHigherCountAtEqualSeverity()
    {
        var state = new RunState()
            .WithCounter(Key(1, 10, "sufferedInjury"), 1)
            .WithCounter(Key(2, 11, "causedInjury"), 4);

        var best = RivalCredits.MostNotable(state, RivalId);

        Assert.NotNull(best);
        Assert.Equal(RivalCreditKind.CausedInjury, best!.Kind);
        Assert.Equal(4, best.Count);
    }
}
