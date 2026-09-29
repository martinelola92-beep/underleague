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
    public void Thrashing_WinByThreeAndRivalAtSixOrFewerOnThePitchIsMet()
    {
        // Un rival expulsado y sin sustituto: acaba con 6 (los otros seis siguen en el campo).
        var rivals = Enumerable.Range(101, 7).Select(id => Stats(id, 1, leftTick: id == 103 ? 400 : -1));
        Assert.True(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 0, players: rivals)));
    }

    [Fact]
    public void Thrashing_ControlsSevenRivalsOrASmallMarginOrALossFail()
    {
        var sevenRivals = Enumerable.Range(101, 7).Select(id => Stats(id, 1));
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 0, players: sevenRivals)));

        var sixRivals = Enumerable.Range(101, 7).Select(id => Stats(id, 1, leftTick: id == 103 ? 400 : -1));
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 1, players: sixRivals)));   // solo +2
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 0, 3, players: sixRivals)));   // pierde

        // Un suplente rival que entró cuenta: 7 que empezaron - 1 que salió + 1 que entró = 7.
        var replaced = sixRivals.Append(Stats(108, 1));
        Assert.False(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 0, players: replaced)));

        // Un suplente que NO llegó a pisar el campo (0 ticks) no cuenta.
        var benchwarmer = new PlayerMatchStats(108, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0);
        Assert.True(Eval(BetKind.Thrashing, Result(Array.Empty<MatchEvent>(), 3, 0, players: sixRivals.Append(benchwarmer))));
    }

    // ---------------------------------------------------------------- three_names

    [Fact]
    public void ThreeNames_ThreeDistinctScorersAndWinIsMet()
    {
        Assert.True(Eval(BetKind.ThreeNames, Result(new[] { Goal(0, 7), Goal(0, 6), Goal(0, 5), Goal(0, 7) }, 4, 0)));
    }

    [Fact]
    public void ThreeNames_ControlsTwoNamesRepeatedScorerCancelledGoalOrLossFail()
    {
        Assert.False(Eval(BetKind.ThreeNames, Result(new[] { Goal(0, 7), Goal(0, 7), Goal(0, 7), Goal(0, 6) }, 4, 0)));
        Assert.False(Eval(BetKind.ThreeNames, Result(new[] { Goal(0, 7), Goal(0, 6), Goal(0, 5, "goal" + Cancelled) }, 2, 0)));
        Assert.False(Eval(BetKind.ThreeNames, Result(new[] { Goal(0, 7), Goal(0, 6), Goal(0, 5), Goal(1, 105), Goal(1, 105), Goal(1, 106), Goal(1, 106) }, 3, 4)));

        // Tres nombres del RIVAL no valen.
        Assert.False(Eval(BetKind.ThreeNames, Result(new[] { Goal(1, 105), Goal(1, 106), Goal(1, 107), Goal(0, 7), Goal(0, 7), Goal(0, 7), Goal(0, 7) }, 4, 3)));
    }

    // ---------------------------------------------------------------- youth_decides

    [Fact]
    public void YouthDecides_TheWinningGoalIsTheOneThatNeverGetsOvertaken()
    {
        bool Youth(int id) => id == 6;

        // 1-0 (7), 1-1 (rival), 2-1 (6): el gol de la victoria es el 2-1, de un canterano.
        Assert.True(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 7), Goal(1, 105), Goal(0, 6) }, 2, 1), youth: Youth));

        // 3-1 con los goles de 7, 6 y 7: el gol de la victoria es el SEGUNDO propio (2-1 ya no se remonta), el de 6.
        Assert.True(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 7), Goal(1, 105), Goal(0, 6), Goal(0, 7) }, 3, 1), youth: Youth));
    }

    [Fact]
    public void YouthDecides_ControlsAVeteranScoringTheDecisiveGoalNoYouthsOrALossFail()
    {
        bool Youth(int id) => id == 6;

        // El canterano marca el 1-0 y luego un veterano el 2-1 decisivo: no es «el chaval decide».
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 6), Goal(1, 105), Goal(0, 7) }, 2, 1), youth: Youth));

        // Un canterano marca el gol INÚTIL (el tercero del 3-1) y no el decisivo.
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 7), Goal(1, 105), Goal(0, 7), Goal(0, 6) }, 3, 1), youth: Youth));

        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 7), Goal(0, 6), Goal(1, 105), Goal(1, 105), Goal(1, 105) }, 2, 3), youth: Youth));
        Assert.False(Eval(BetKind.YouthDecides, Result(new[] { Goal(0, 6) }, 1, 0), youth: null));
    }

    // ---------------------------------------------------------------- short_and_clean

    [Fact]
    public void ShortAndClean_LessThanSevenStartersWinWithoutConcedingIsMet()
    {
        Assert.True(Eval(BetKind.ShortAndClean, Result(new[] { Goal(0, 7) }, 1, 0), ownStarters: 6));
        Assert.True(Eval(BetKind.ShortAndClean, Result(new[] { Goal(0, 7) }, 1, 0), ownStarters: 5));
    }

    [Fact]
    public void ShortAndClean_ControlsSevenStartersAConcededGoalOrALossFail()
    {
        Assert.False(Eval(BetKind.ShortAndClean, Result(new[] { Goal(0, 7) }, 1, 0), ownStarters: 7));
        Assert.False(Eval(BetKind.ShortAndClean, Result(new[] { Goal(0, 7), Goal(1, 105), Goal(0, 6) }, 2, 1), ownStarters: 6));
        Assert.False(Eval(BetKind.ShortAndClean, Result(Array.Empty<MatchEvent>(), 0, 1), ownStarters: 6));
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
