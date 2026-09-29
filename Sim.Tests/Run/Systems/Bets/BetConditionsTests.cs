using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Sim.Tests.Run.Systems.Bets;

/// <summary>
/// ADR 0157: las once condiciones, con partidos construidos a mano (positivo, negativo y control negativo
/// conocido de cada una, Regla J) y, en <see cref="BetConditionsRealMatchTests"/>, con partidos reales.
/// Equipo propio = 0 (ids 1..7), rival = 1 (ids 101..107).
/// </summary>
public sealed class BetConditionsTests
{
    private const string Cancelled = ":cancelled";

    private static PlayerDefinition P(int id, Position position) => new(
        id, "p" + id, Race.Human, position, Rarity.Common, 1, new Attributes(50, 50, 50, 50, 50),
        Array.Empty<Trait>(), Array.Empty<string>(), PhysicalState.Healthy);

    private static TeamSetup Team(int firstId, int starters = 7)
    {
        var positions = new[]
        {
            Position.Goalkeeper, Position.Defender, Position.Defender, Position.Midfielder,
            Position.Midfielder, Position.Midfielder, Position.Forward,
        };
        var players = positions.Select((pos, i) => P(firstId + i, pos)).ToList();
        var lineup = Lineup.Default(players);
        var slots = lineup.Slots.Take(starters).ToList();
        return new TeamSetup("t" + firstId, "t" + firstId, Race.Human, players, new Lineup(slots));
    }

    private static MatchSetup Setup(int ownStarters = 7) =>
        new(Team(1, ownStarters), Team(101), new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));

    private static MatchEvent E(EventType type, int team, int actor = -1, int opponent = -1, string detail = "") =>
        new(type, 0, team, actor, -1, opponent, new Cell(0, 0), Zone.Middle, MatchPhase.OpenPlay, 0, 0, detail);

    private static MatchEvent Goal(int team, int scorer, string detail = "goal") => E(EventType.Goal, team, scorer, detail: detail);

    private static MatchEvent Injury(int victimTeam, int victim, int causer = -1, string detail = "minor") =>
        E(EventType.Injury, victimTeam, victim, causer, detail);

    /// <summary>Resultado a mano. Goles y ganador se dan explícitos porque el informe es lo que el motor rellena.</summary>
    private static MatchResult Result(
        IEnumerable<MatchEvent> events, int goalsOwn, int goalsRival, bool goldenGoal = false, IEnumerable<PlayerMatchStats>? players = null)
    {
        var builder = new MatchReportBuilder { Winner = goalsOwn > goalsRival ? 0 : 1, Ticks = 900, WentToGoldenGoal = goldenGoal };
        builder.Goals[0] = goalsOwn;
        builder.Goals[1] = goalsRival;
        if (players is not null)
        {
            builder.Players.AddRange(players);
        }

        return new MatchResult(events.ToList(), builder.Build(), Array.Empty<PlayerCounterDelta>());
    }

    private static PlayerMatchStats Stats(int id, int team, int leftTick = -1) =>
        new(id, team, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 900, 0, 0, leftTick);

    private static bool Eval(BetKind kind, MatchResult result, int ownStarters = 7, int target = -1, Func<int, bool>? youth = null) =>
        BetConditions.Evaluate(kind, new BetContext(Setup(ownStarters), result, youth, target));

    // ---------------------------------------------------------------- blood_before_goals

    [Fact]
    public void BloodBeforeGoals_InjuryFirstIsMet_GoalFirstIsNot()
    {
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Injury(1, 101, 3), Goal(0, 7) }, 1, 0)));
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Injury(0, 2, 102), Goal(1, 105) }, 0, 1)));
        Assert.False(Eval(BetKind.BloodBeforeGoals, Result(new[] { Goal(0, 7), Injury(1, 101, 3) }, 1, 0)));
    }

    [Fact]
    public void BloodBeforeGoals_WithoutGoalsAnyInjuryCounts_ControlNoInjuryNoGoalsFails()
    {
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Injury(1, 101, 3, "severe") }, 0, 0)));
        Assert.False(Eval(BetKind.BloodBeforeGoals, Result(Array.Empty<MatchEvent>(), 0, 0)));
    }

    [Fact]
    public void BloodBeforeGoals_IgnoresACancelledInjuryAndACancelledGoal()
    {
        // La lesión anulada por un perk no ocurrió: el primer hecho real es el gol.
        Assert.False(Eval(BetKind.BloodBeforeGoals, Result(new[] { Injury(1, 101, 3, "minor" + Cancelled), Goal(0, 7) }, 1, 0)));

        // El gol anulado tampoco: la lesión es lo primero que pasó de verdad.
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Goal(0, 7, "goal" + Cancelled), Injury(1, 101, 3) }, 0, 0)));
    }

    // ---------------------------------------------------------------- muertes sin INJURY (revisión independiente)

    private static MatchEvent Death(int victimTeam, int victim, int killer = -1) =>
        E(EventType.Death, victimTeam, victim, killer, "lethal");

    /// <summary>
    /// Una muerte, sin un INJURY delante, cuenta como baja: como sangre antes de los goles, como baja propia
    /// de «ojo por ojo» y como baja causada por el jugador (en «ojo por ojo» y «manos limpias»).
    /// </summary>
    [Fact]
    public void ADeathWithoutAnInjuryCountsAsBloodAndAsACasualty()
    {
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Death(1, 104, 5), Goal(0, 7) }, 1, 0)));
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Death(0, 2, 103), Goal(1, 105) }, 0, 1)));
        Assert.True(Eval(BetKind.BloodBeforeGoals, Result(new[] { Death(1, 104, 5) }, 0, 0)));

        // Ojo por ojo: baja propia por muerte + baja rival causada por muerte.
        Assert.True(Eval(BetKind.EyeForEye, Result(new[] { Death(0, 2, 103), Injury(1, 104, 5), Goal(0, 7) }, 1, 0)));
        Assert.True(Eval(BetKind.EyeForEye, Result(new[] { Injury(0, 2, 103), Death(1, 104, 5), Goal(0, 7) }, 1, 0)));
        Assert.True(Eval(BetKind.EyeForEye, Result(new[] { Death(0, 2, 103), Death(1, 104, 5), Goal(0, 7) }, 1, 0)));

        // Manos limpias: la baja causada es una muerte.
        Assert.True(Eval(BetKind.CleanHands, Result(new[] { Death(1, 104, 5), Goal(0, 7) }, 1, 0)));
    }

    [Fact]
    public void ControlsADeathThatIsCancelledOrNotCausedByUsOrAfterTheFirstGoalDoesNotCount()
    {
        Assert.False(Eval(BetKind.BloodBeforeGoals, Result(new[] { Goal(0, 7), Death(1, 104, 5) }, 1, 0)));
        Assert.False(Eval(BetKind.BloodBeforeGoals, Result(new[] { E(EventType.Death, 1, 104, 5, "lethal" + Cancelled), Goal(0, 7) }, 1, 0)));
        Assert.False(Eval(BetKind.CleanHands, Result(new[] { Death(1, 104, -1), Goal(0, 7) }, 1, 0)));     // sin causante propio
        Assert.False(Eval(BetKind.CleanHands, Result(new[] { Death(1, 104, 102), Goal(0, 7) }, 1, 0)));    // lo mató un rival
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Death(1, 104, 5), Goal(0, 7) }, 1, 0)));      // no nos han lesionado
    }

    // ---------------------------------------------------------------- hunt_the_star

    [Fact]
    public void HuntTheStar_InjuryDeathOrRedCardOfTheNamedPlayerIsMet()
    {
        Assert.True(Eval(BetKind.HuntTheStar, Result(new[] { Injury(1, 104, 4) }, 0, 0), target: 104));
        Assert.True(Eval(BetKind.HuntTheStar, Result(new[] { E(EventType.Death, 1, 104, 4, "lethal") }, 0, 0), target: 104));
        Assert.True(Eval(BetKind.HuntTheStar, Result(new[] { E(EventType.Card, 1, 104, detail: "red") }, 0, 0), target: 104));
    }

    [Fact]
    public void HuntTheStar_OtherPlayersAnulledEventsYellowCardsAndNoTargetAreControlsThatFail()
    {
        Assert.False(Eval(BetKind.HuntTheStar, Result(new[] { Injury(1, 105, 4) }, 0, 0), target: 104));
        Assert.False(Eval(BetKind.HuntTheStar, Result(new[] { Injury(1, 104, 4, "minor" + Cancelled) }, 0, 0), target: 104));
        Assert.False(Eval(BetKind.HuntTheStar, Result(new[] { E(EventType.Card, 1, 104, detail: "yellow") }, 0, 0), target: 104));
        Assert.False(Eval(BetKind.HuntTheStar, Result(new[] { Injury(1, 104, 4) }, 0, 0), target: -1));

        // Un propio con el mismo id que el nombrado no cuenta: la caza es del rival (equipo 1).
        Assert.False(Eval(BetKind.HuntTheStar, Result(new[] { Injury(0, 104, 4) }, 0, 0), target: 104));
    }

    [Fact]
    public void HuntTheStar_DoesNotRequireWinning()
    {
        Assert.True(Eval(BetKind.HuntTheStar, Result(new[] { Injury(1, 104, 4), Goal(1, 105), Goal(1, 105) }, 0, 2), target: 104));
    }

    // ---------------------------------------------------------------- eye_for_eye

    [Fact]
    public void EyeForEye_OwnInjuryPlusRivalInjuryWeCausedPlusWinIsMet()
    {
        var events = new[] { Injury(0, 2, 103), Injury(1, 104, 5), Goal(0, 7) };
        Assert.True(Eval(BetKind.EyeForEye, Result(events, 1, 0)));
    }

    [Fact]
    public void EyeForEye_ControlsMissingAnyPartFail()
    {
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Injury(1, 104, 5), Goal(0, 7) }, 1, 0)));           // no nos lesionan
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Injury(0, 2, 103), Goal(0, 7) }, 1, 0)));           // no lesionamos
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Injury(0, 2, 103), Injury(1, 104, 5), Goal(1, 105) }, 0, 1))); // pierde

        // La lesión rival sin causante propio (un perk, un compañero suyo) no es «causada por el jugador».
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Injury(0, 2, 103), Injury(1, 104, -1), Goal(0, 7) }, 1, 0)));
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Injury(0, 2, 103), Injury(1, 104, 102), Goal(0, 7) }, 1, 0)));

        // La lesión anulada no cuenta.
        Assert.False(Eval(BetKind.EyeForEye, Result(new[] { Injury(0, 2, 103, "minor" + Cancelled), Injury(1, 104, 5), Goal(0, 7) }, 1, 0)));
    }

    // ---------------------------------------------------------------- comeback

    [Fact]
    public void Comeback_TrailingAtSomePointAndWinningIsMet()
    {
        var events = new[] { Goal(1, 105), Goal(0, 7), Goal(0, 6) };
        Assert.True(Eval(BetKind.Comeback, Result(events, 2, 1)));
    }

    [Fact]
    public void Comeback_ControlsNeverTrailingLosingOrTheTrailingGoalCancelledFail()
    {
        Assert.False(Eval(BetKind.Comeback, Result(new[] { Goal(0, 7), Goal(1, 105), Goal(0, 6) }, 2, 1)));  // 1-0, 1-1, 2-1: nunca por debajo
        Assert.False(Eval(BetKind.Comeback, Result(new[] { Goal(1, 105), Goal(0, 7) }, 1, 1 + 1)));          // pierde (cierre en 1-2)
        Assert.False(Eval(BetKind.Comeback, Result(new[] { Goal(1, 105, "goal" + Cancelled), Goal(0, 7) }, 1, 0)));
    }

    // ---------------------------------------------------------------- into_the_mob

    [Fact]
    public void IntoTheMob_GoldenGoalWonIsMet_LostOrRegulationWinFails()
    {
        Assert.True(Eval(BetKind.IntoTheMob, Result(new[] { Goal(0, 7, "goldenGoal") }, 1, 0, goldenGoal: true)));
        Assert.False(Eval(BetKind.IntoTheMob, Result(new[] { Goal(1, 105, "goldenGoal") }, 0, 1, goldenGoal: true)));
        Assert.False(Eval(BetKind.IntoTheMob, Result(new[] { Goal(0, 7) }, 1, 0)));
    }

    // ---------------------------------------------------------------- clean_hands

    [Fact]
    public void CleanHands_WinWithACausedInjuryAndNoOwnCardsIsMet()
    {
        Assert.True(Eval(BetKind.CleanHands, Result(new[] { Injury(1, 104, 5), Goal(0, 7) }, 1, 0)));

        // Las tarjetas del RIVAL no cuentan.
        Assert.True(Eval(BetKind.CleanHands, Result(new[] { Injury(1, 104, 5), E(EventType.Card, 1, 103, detail: "yellow"), Goal(0, 7) }, 1, 0)));
    }

    [Fact]
    public void CleanHands_ControlsAnyOwnCardNoInjuryLossOrCancelledCardBehaveAsSpecified()
    {
        Assert.False(Eval(BetKind.CleanHands, Result(new[] { Injury(1, 104, 5), E(EventType.Card, 0, 3, detail: "yellow"), Goal(0, 7) }, 1, 0)));
        Assert.False(Eval(BetKind.CleanHands, Result(new[] { Injury(1, 104, 5), E(EventType.Card, 0, 3, detail: "red"), Goal(0, 7) }, 1, 0)));
        Assert.False(Eval(BetKind.CleanHands, Result(new[] { Goal(0, 7) }, 1, 0)));
        Assert.False(Eval(BetKind.CleanHands, Result(new[] { Injury(1, 104, 5), Goal(1, 105), Goal(1, 105) }, 0, 2)));

        // Una tarjeta anulada por un perk no se ha visto: no rompe las manos limpias.
        Assert.True(Eval(BetKind.CleanHands, Result(new[] { Injury(1, 104, 5), E(EventType.Card, 0, 3, detail: "yellow" + Cancelled), Goal(0, 7) }, 1, 0)));
    }

    // ---------------------------------------------------------------- thrashing

    [Fact]
    public void Thrashing_WinByThreeOrMoreIsMet()
    {
        Assert.True(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 0)));
        Assert.True(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 5, 2)));
    }

    [Fact]
    public void Thrashing_ControlsASmallMarginOrALossFail()
    {
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 1)));   // solo +2
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 1, 0)));
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 0, 3)));   // pierde por 3
    }

    // ---------------------------------------------------------------- split_the_goals

    [Fact]
    public void SplitTheGoals_TwoDistinctScorersAndWinIsMet()
    {
        Assert.True(Eval(BetKind.SplitTheGoals, Result(new[] { Goal(0, 7), Goal(0, 6) }, 2, 0)));
        Assert.True(Eval(BetKind.SplitTheGoals, Result(new[] { Goal(0, 7), Goal(1, 105), Goal(0, 6), Goal(0, 7) }, 3, 1)));
    }

    [Fact]
    public void SplitTheGoals_ControlsOneNameCancelledGoalRivalNamesOrLossFail()
    {
        Assert.False(Eval(BetKind.SplitTheGoals, Result(new[] { Goal(0, 7), Goal(0, 7), Goal(0, 7) }, 3, 0)));
        Assert.False(Eval(BetKind.SplitTheGoals, Result(new[] { Goal(0, 7), Goal(0, 6, "goal" + Cancelled) }, 1, 0)));
        Assert.False(Eval(BetKind.SplitTheGoals, Result(new[] { Goal(1, 105), Goal(1, 106), Goal(0, 7), Goal(0, 7), Goal(0, 7) }, 3, 2)));
        Assert.False(Eval(BetKind.SplitTheGoals, Result(new[] { Goal(0, 7), Goal(0, 6), Goal(1, 105), Goal(1, 105), Goal(1, 106) }, 2, 3)));
    }

    // ---------------------------------------------------------------- youth_decides

    [Fact]
    public void YouthDecides_AYouthScoresAndTheTeamWinsIsMet()
    {
        bool Youth(int id) => id == 6;

        // El canterano marca el primero y un veterano remata: basta que marque, ya no tiene que ser el decisivo.
        Assert.True(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 6), Goal(1, 105), Goal(0, 7) }, 2, 1), youth: Youth));
        Assert.True(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 7), Goal(0, 7), Goal(0, 6) }, 3, 0), youth: Youth));
    }

    [Fact]
    public void YouthDecides_ControlsNoYouthScoringACancelledGoalARivalScorerOrALossFail()
    {
        bool Youth(int id) => id == 6;

        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 7), Goal(0, 5) }, 2, 0), youth: Youth));
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 6, "goal" + Cancelled), Goal(0, 7) }, 1, 0), youth: Youth));
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(1, 6), Goal(0, 7) }, 1, 0), youth: Youth));   // id 6 marcando por el rival no es propio
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 6), Goal(1, 105), Goal(1, 105) }, 1, 2), youth: Youth));
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 6) }, 1, 0), youth: null));
    }

    // ---------------------------------------------------------------- referee_blind

    [Fact]
    public void RefereeBlind_ThreeUnseenOwnFoulsWithTheRefereePresentAndWinIsMet()
    {
        var events = new[]
        {
            E(EventType.Foul, 0, 3, 103, "unseen"), E(EventType.Foul, 0, 4, 104, "unseen"),
            E(EventType.Foul, 1, 105, 5, "unseen"), E(EventType.Foul, 0, 3, 103, "unseen"), Goal(0, 7),
        };
        Assert.True(Eval(BetKind.RefereeBlind, Result(events, 1, 0)));
    }

    [Fact]
    public void RefereeBlind_ControlsSeenFoulsRivalFoulsMobFoulsTooFewOrALossFail()
    {
        var two = new[] { E(EventType.Foul, 0, 3, 103, "unseen"), E(EventType.Foul, 0, 4, 104, "unseen"), Goal(0, 7) };
        Assert.False(Eval(BetKind.RefereeBlind, Result(two, 1, 0)));

        // Faltas señaladas (detalle "foul") no son «no señaladas».
        var seen = Enumerable.Range(0, 4).Select(_ => E(EventType.Foul, 0, 3, 103, "foul")).Append(Goal(0, 7));
        Assert.False(Eval(BetKind.RefereeBlind, Result(seen, 1, 0)));

        // Las del rival no valen.
        var rival = Enumerable.Range(0, 4).Select(_ => E(EventType.Foul, 1, 103, 3, "unseen")).Append(Goal(0, 7));
        Assert.False(Eval(BetKind.RefereeBlind, Result(rival, 1, 0)));

        // Con la turba en el campo (MOB_START) no hay árbitro: sus «no vistas» no cuentan.
        var mob = new[]
        {
            E(EventType.Foul, 0, 3, 103, "unseen"), E(EventType.Foul, 0, 4, 104, "unseen"), E(EventType.MobStart, -1, detail: "mob"),
            E(EventType.Foul, 0, 3, 103, "unseen"), E(EventType.Foul, 0, 3, 103, "unseen"), Goal(0, 7, "goldenGoal"),
        };
        Assert.False(Eval(BetKind.RefereeBlind, Result(mob, 1, 0, goldenGoal: true)));

        var lost = Enumerable.Range(0, 3).Select(_ => E(EventType.Foul, 0, 3, 103, "unseen")).Append(Goal(1, 105));
        Assert.False(Eval(BetKind.RefereeBlind, Result(lost, 0, 1)));
    }

    // ---------------------------------------------------------------- contrato

    [Fact]
    public void EveryKindHasAnEvaluator()
    {
        var empty = Result(Array.Empty<MatchEvent>(), 0, 1);
        foreach (var kind in Enum.GetValues<BetKind>())
        {
            // No lanza: un BetKind nuevo sin rama en Evaluate se vería aquí.
            _ = Eval(kind, empty);
        }
    }

    [Fact]
    public void AnUndefinedKindIsAnExplicitError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Eval((BetKind)999, Result(Array.Empty<MatchEvent>(), 0, 1)));
    }
}
