using Underleague.Game.Match;
using Underleague.Sim.Engine;
using Underleague.Sim.Run.View;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// ADR 0173 (BB-D), <see cref="PlayStops"/>: «este suceso detiene el juego» se lee de la traza y sólo cuenta el
/// saque de falta o el penalti que el suceso provoca. Es también el <b>censo reproducible</b> de las pausas por
/// partido (la cifra de la ADR: ~3 por partido, ≤ +1,8 s a 0,6 s), con dos casos de respuesta conocida como
/// instrumento (Regla J): la falta que el árbitro no vio nunca pausa, y la pitada siempre (salvo la que un gol del mismo tick anula).
/// </summary>
public sealed class PlayStopsTests
{
    private const int Matches = 120;

    private readonly ITestOutputHelper _output;

    public PlayStopsTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void OnlyTheKindsThatCanStopPlayAreAskedAbout()
    {
        Assert.All(new[] { MomentKind.Foul, MomentKind.Yellow, MomentKind.Red, MomentKind.MinorInjury, MomentKind.SevereInjury },
            kind => Assert.True(PlayStops.CanStopPlay(kind)));
        Assert.All(new[] { MomentKind.Goal, MomentKind.Death, MomentKind.FullTime, MomentKind.Kickoff, MomentKind.Mob, MomentKind.RefereeLeaves, MomentKind.Consumable, MomentKind.Substitution },
            kind => Assert.False(PlayStops.CanStopPlay(kind)));
    }

    [Fact]
    public void TheCensusOfHoldsIsReproducibleAndTheInstrumentAnswersTheKnownCases()
    {
        var catalog = TestData.LoadCatalog();
        int holds = 0;
        int seenFouls = 0;
        int seenFoulHolds = 0;
        int unseenFouls = 0;
        int unseenHolds = 0;
        int otherRestartOnly = 0;
        int supersededByGoal = 0;
        for (ulong seed = 0; seed < Matches; seed++)
        {
            var setup = TestMatches.Reference(catalog, seed);
            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            foreach (var moment in MatchMomentView.Build(setup, result, catalog).Moments)
            {
                bool held = PlayStops.Holds(moment, trace);
                holds += held ? 1 : 0;

                bool freeKickOrPenalty = false;
                bool anyRestart = false;
                for (int f = moment.Frame; f <= Math.Min(moment.LastFrame, trace.FrameCount - 1); f++)
                {
                    var restart = trace.RestartAt(f);
                    anyRestart |= restart != RestartKind.None;
                    freeKickOrPenalty |= restart is RestartKind.FreeKick or RestartKind.Penalty;
                }

                // La regla: sólo el saque de falta o el penalti cuenta, y sólo para los sucesos que pueden parar.
                Assert.Equal(PlayStops.CanStopPlay(moment.Kind) && !moment.Cancelled && freeKickOrPenalty, held);
                if (PlayStops.CanStopPlay(moment.Kind) && anyRestart && !freeKickOrPenalty)
                {
                    otherRestartOnly++;
                    Assert.False(held, "un saque que no provoca el suceso no puede pausarlo");
                }

                if (moment.Kind == MomentKind.Foul && !moment.Cancelled)
                {
                    bool unseen = result.Events[moment.EventIndices[^1]].Detail == "unseen";
                    if (unseen)
                    {
                        unseenFouls++;
                        unseenHolds += held ? 1 : 0;
                    }
                    else
                    {
                        seenFouls++;
                        seenFoulHolds += held ? 1 : 0;
                        if (!held)
                        {
                            var kinds = new List<string>();
                            for (int f = moment.Frame - 2; f <= Math.Min(moment.LastFrame + 3, trace.FrameCount - 1); f++)
                            {
                                kinds.Add(trace.RestartAt(f).ToString());
                            }

                            // La única causa admitida: un gol en el mismo tick anula el saque de falta (el juego lo
                            // detuvo el gol, que tiene su propio congelado), y el siguiente saque es el de centro.
                            Assert.Contains("Kickoff", kinds);
                            supersededByGoal++;
                        }
                    }
                }
            }
        }

        double perMatch = holds / (double)Matches;
        _output.WriteLine($"{Matches} partidos: {perMatch:F2} pausas por partido, +{perMatch * DirectorTimings.Default.Hold:F1} s a {DirectorTimings.Default.Hold} s");
        _output.WriteLine($"faltas no vistas {unseenFouls} con pausa {unseenHolds}; pitadas {seenFouls} con pausa {seenFoulHolds} (anuladas por un gol en el mismo tick: {supersededByGoal}); sucesos con otro saque y sin el suyo: {otherRestartOnly}");

        Assert.True(unseenFouls > 0 && seenFouls > 0, "el censo no tiene los dos casos conocidos: el instrumento no mide nada");
        Assert.Equal(0, unseenHolds);
        Assert.Equal(seenFouls - supersededByGoal, seenFoulHolds);

        // Valla anti-regresión, no banda de diseño: medido 2,96 por partido en 300 partidos (ADR 0173).
        Assert.InRange(perMatch, 1.5, 5.0);
    }
}
