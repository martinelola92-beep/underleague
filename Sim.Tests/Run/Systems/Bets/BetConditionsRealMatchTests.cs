using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Sim.Tests.Run.Systems.Bets;

/// <summary>
/// ADR 0157, Regla J: las once condiciones contra partidos REALES del motor. Cada condición se contrasta con
/// un oráculo que sale de otra vía (el informe por jugador, o el registro recontado a mano) y se exige que en
/// el lote haya partidos donde se cumple y donde no: una condición que nunca se cumple, o siempre, no la
/// valida ningún test.
/// </summary>
public sealed class BetConditionsRealMatchTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private sealed record Played(string Batch, ulong Seed, MatchSetup Setup, MatchResult Result, int TargetId)
    {
        public bool IsYouth(int id) => id % 2 == 0;

        public BetContext Context => new(Setup, Result, IsYouth, TargetId);
    }

    private static readonly Lazy<IReadOnlyList<Played>> Batch = new(PlayBatch);

    private static IReadOnlyList<Played> PlayBatch()
    {
        var played = new List<Played>();

        void Play(string batch, ulong seed, MatchSetup setup)
        {
            // El objetivo de hunt_the_star sale del mismo cálculo que la oferta (BetSystem.TargetFor).
            int target = BetSystem.TargetFor(setup.Away)?.Id ?? -1;
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default);
            played.Add(new Played(batch, seed, setup, result, target));
        }

        for (ulong seed = 1; seed <= 300; seed++)
        {
            Play("even", seed, TestMatches.Build(Catalog, seed, 50, 50));
        }

        for (ulong seed = 1; seed <= 150; seed++)
        {
            Play("strong-home", seed, TestMatches.Build(Catalog, seed, 90, 30));
        }

        for (ulong seed = 1; seed <= 150; seed++)
        {
            var setup = TestMatches.Build(Catalog, seed, 70, 50);
            var slots = setup.Home.Lineup.Slots.Take(6).ToList();
            Play("short-handed", seed, setup with { Home = setup.Home with { Lineup = new Lineup(slots) } });
        }

        for (ulong seed = 1; seed <= 150; seed++)
        {
            var setup = TestMatches.Build(Catalog, seed, 50, 50);
            Play("lenient-referee", seed, setup with { Referee = new RefereeSetup("Lenient", RefereeTrait.Lenient, 0) });
        }

        return played;
    }

    // ---------------------------------------------------------------- oráculos (otra vía que BetConditions)

    private static bool Live(MatchEvent e) => !e.Detail.EndsWith(":cancelled", StringComparison.Ordinal);

    private static bool Won(Played p) => p.Result.Report.Winner == 0;

    private static IEnumerable<PlayerMatchStats> Stats(Played p, int team) => p.Result.Report.Players.Where(s => s.Team == team);

    private static bool Oracle(BetKind kind, Played p)
    {
        var live = p.Result.Events.Where(Live).ToList();
        var goals = live.Where(e => e.Type == EventType.Goal).ToList();
        var injuries = live.Where(e => e.Type == EventType.Injury).ToList();
        var report = p.Result.Report;
        switch (kind)
        {
            case BetKind.BloodBeforeGoals:
                return injuries.Count > 0 && (goals.Count == 0 || live.IndexOf(injuries[0]) < live.IndexOf(goals[0]));

            case BetKind.HuntTheStar:
                // Vía informe: el nombrado dejó el campo (el rival no tiene sustituciones tácticas).
                return Stats(p, 1).Any(s => s.PlayerId == p.TargetId && s.LeftPitchTick >= 0);

            case BetKind.EyeForEye:
                return Won(p) && Stats(p, 0).Any(s => s.Injured) && Stats(p, 0).Sum(s => s.InjuriesCaused) >= 1;

            case BetKind.Comeback:
                {
                    var diffs = new List<int>();
                    int diff = 0;
                    foreach (var g in goals)
                    {
                        diff += g.Team == 0 ? 1 : -1;
                        diffs.Add(diff);
                    }

                    return Won(p) && diffs.Any(d => d < 0);
                }

            case BetKind.IntoTheMob:
                // Vía registro: hubo MOB_START y gana el equipo propio. Incluye el desempate al agotarse el tiempo de
                // la prórroga (MATCH_END "tiebreak", sin gol de oro): también es «ganar tras la turba».
                return Won(p) && p.Result.Events.Any(e => e.Type == EventType.MobStart);

            case BetKind.CleanHands:
                return Won(p) && Stats(p, 0).Sum(s => s.Cards) == 0 && Stats(p, 0).Sum(s => s.InjuriesCaused) >= 1;

            case BetKind.Thrashing:
                {
                    // Recontado desde el registro: titulares - salidas + entradas, sin mirar LeftPitchTick.
                    var onPitch = p.Setup.Away.Lineup.Slots.Select(s => s.PlayerId).ToHashSet();
                    foreach (var e in live)
                    {
                        if (e.Team == 1 && e.Type is EventType.Injury or EventType.Death
                            || (e.Type == EventType.Card && e.Team == 1 && e.Detail == "red"))
                        {
                            onPitch.Remove(e.Actor);
                        }
                        else if (e.Type == EventType.Substitution && onPitch.Contains(e.Target))
                        {
                            onPitch.Remove(e.Target);
                            onPitch.Add(e.Actor);
                        }
                    }

                    return Won(p) && report.Goals[0] - report.Goals[1] >= 3 && onPitch.Count <= 6;
                }

            case BetKind.ThreeNames:
                return Won(p) && Stats(p, 0).Count(s => s.Goals > 0) >= 3;

            case BetKind.YouthDecides:
                {
                    var own = goals.Where(g => g.Team == 0).ToList();
                    int winning = report.Goals[1];
                    return Won(p) && own.Count > winning && p.IsYouth(own[winning].Actor);
                }

            case BetKind.ShortAndClean:
                return Won(p) && p.Setup.Home.Lineup.Slots.Count < 7 && report.Goals[1] == 0;

            case BetKind.RefereeBlind:
                return Won(p) && p.Result.Events
                    .TakeWhile(e => e.Type != EventType.MobStart)
                    .Count(e => Live(e) && e.Type == EventType.Foul && e.Team == 0 && e.Detail == "unseen") >= 3;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    // ---------------------------------------------------------------- tests

    [Fact]
    public void TheBatchIsBigEnoughAndFinishedEveryMatch()
    {
        Assert.True(Batch.Value.Count >= 700);
        Assert.All(Batch.Value, p => Assert.InRange(p.Result.Report.Winner, 0, 1));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ConditionAgreesWithItsOracleOnEveryRealMatch(BetKind kind)
    {
        foreach (var p in Batch.Value)
        {
            bool actual = BetConditions.Evaluate(kind, p.Context);
            bool expected = Oracle(kind, p);
            Assert.True(
                actual == expected,
                $"{kind} en {p.Batch} semilla {p.Seed}: BetConditions dice {actual}, el oráculo {expected}");
        }
    }

    /// <summary>
    /// Sin ejemplos de las dos caras el test de acuerdo no prueba nada (Regla J): la condición tiene que
    /// cumplirse en algún partido real del lote y no cumplirse en otros.
    /// </summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void ConditionIsMetInSomeRealMatchesAndNotInOthers(BetKind kind)
    {
        int met = Batch.Value.Count(p => BetConditions.Evaluate(kind, p.Context));
        Assert.True(met > 0, $"{kind}: nunca se cumple en {Batch.Value.Count} partidos reales");
        Assert.True(met < Batch.Value.Count, $"{kind}: se cumple en TODOS los partidos reales del lote");
    }

    /// <summary>Control negativo conocido: un jugador nombrado que no existe en el partido nunca se «caza».</summary>
    [Fact]
    public void HuntTheStarWithANonexistentTargetNeverHolds()
    {
        foreach (var p in Batch.Value)
        {
            Assert.False(BetConditions.Evaluate(BetKind.HuntTheStar, p.Context with { TargetPlayerId = 999_999 }));
        }
    }

    /// <summary>Control negativo conocido: sin canteranos, «el chaval decide» no se cumple jamás.</summary>
    [Fact]
    public void YouthDecidesWithoutYouthsNeverHolds()
    {
        foreach (var p in Batch.Value)
        {
            Assert.False(BetConditions.Evaluate(BetKind.YouthDecides, p.Context with { IsYouth = _ => false }));
        }
    }

    /// <summary>Control negativo conocido: los equipos a siete titulares nunca cumplen «pocos y limpios».</summary>
    [Fact]
    public void ShortAndCleanNeverHoldsWithSevenStarters()
    {
        foreach (var p in Batch.Value.Where(p => p.Setup.Home.Lineup.Slots.Count == 7))
        {
            Assert.False(BetConditions.Evaluate(BetKind.ShortAndClean, p.Context));
        }
    }

    /// <summary>Control negativo conocido: un partido que gana el rival no cumple ninguna condición de victoria.</summary>
    [Fact]
    public void LostMatchesNeverMeetAWinCondition()
    {
        var winKinds = new[]
        {
            BetKind.EyeForEye, BetKind.Comeback, BetKind.IntoTheMob, BetKind.CleanHands, BetKind.Thrashing,
            BetKind.ThreeNames, BetKind.YouthDecides, BetKind.ShortAndClean, BetKind.RefereeBlind,
        };
        var lost = Batch.Value.Where(p => !Won(p)).ToList();
        Assert.NotEmpty(lost);
        foreach (var p in lost)
        {
            foreach (var kind in winKinds)
            {
                Assert.False(BetConditions.Evaluate(kind, p.Context), $"{kind} se cumple en un partido perdido ({p.Batch} {p.Seed})");
            }
        }
    }

    public static TheoryData<BetKind> Kinds()
    {
        var data = new TheoryData<BetKind>();
        foreach (var kind in Enum.GetValues<BetKind>())
        {
            data.Add(kind);
        }

        return data;
    }
}
