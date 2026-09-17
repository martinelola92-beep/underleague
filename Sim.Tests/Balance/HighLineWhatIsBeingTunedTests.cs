using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// §30: qué está evaluando realmente el `SCREENING_NEEDS_TUNING` de <c>high_line</c>. Vuelve al juego, no
/// al instrumento.
///
/// <para><b>Corrige una medición propia</b>: §28.3 leyó "tercio 2 = tercio atacante". Es falso —
/// <c>MatchReport.BallTicksByThird</c> se indexa por la X ABSOLUTA del campo
/// (<c>MatchEngine.cs:3058-3061</c>) y <c>PairedBalanceHarness</c> alterna direcciones, así que en la
/// mitad de los partidos el tercio atacante del portador es el 0. Aquí el territorio se mide RELATIVO al
/// equipo del portador con <c>Pitch.ZoneOf(ball, team)</c>, que es el primitivo correcto.</para>
///
/// <para>No cambia métricas, ni umbrales, ni el perk, ni <c>/data</c>. Solo mide.</para>
/// </summary>
public sealed class HighLineWhatIsBeingTunedTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public HighLineWhatIsBeingTunedTests(ITestOutputHelper output) => _output = output;

    private sealed record Side(
        double OwnThird, double Middle, double OpposingThird,
        double GoalsConceded, double ShotsConceded, double OpponentThroughPasses, double OpponentThroughCompleted);

    [Fact]
    public void WhatDoesHighLineActuallyDoToTerritoryAndToItsDeclaredCost()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "high_line");
        var config = new SimConfig(CollectLog: false, Trace: true);

        double[] armed = new double[7];
        double[] control = new double[7];
        int matches = 0;

        for (int roster = 0; roster < 20; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, Quality, 100001, Level);

            int slot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
            if (slot < 0)
            {
                continue;
            }

            var armedPlayers = home.Players.ToList();
            armedPlayers[slot] = armedPlayers[slot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[slot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                bool carrierIsAway = direction == 1;
                ulong seed = RngStreams.MatchSeed(1, (roster * 2) + direction);

                var armedSetup = carrierIsAway ? new MatchSetup(away, armedHome, Referee) : new MatchSetup(armedHome, away, Referee);
                var controlSetup = carrierIsAway ? new MatchSetup(away, home, Referee) : new MatchSetup(home, away, Referee);

                Accumulate(Simulator.Run(armedSetup, seed, Catalog, config), carrierId, carrierIsAway ? 1 : 0, armed);
                Accumulate(Simulator.Run(controlSetup, seed, Catalog, config), carrierId, carrierIsAway ? 1 : 0, control);
                matches++;
            }
        }

        var a = Normalise(armed, matches);
        var c = Normalise(control, matches);

        _output.WriteLine($"high_line: shiftHome(+2) sobre un Defensa | {matches} partidos por brazo | territorio RELATIVO al equipo del portador");
        _output.WriteLine("");
        _output.WriteLine("                              | armado  | control | delta");
        Row("territorio: tercio propio %", a.OwnThird, c.OwnThird);
        Row("territorio: centro %", a.Middle, c.Middle);
        Row("territorio: tercio rival %", a.OpposingThird, c.OpposingThird);
        _output.WriteLine("");
        _output.WriteLine("coste declarado por el _doc ('un pase en profundidad y no hay nadie detrás'):");
        Row("goles encajados/partido", a.GoalsConceded, c.GoalsConceded);
        Row("tiros encajados/partido", a.ShotsConceded, c.ShotsConceded);
        Row("pases en profundidad del rival", a.OpponentThroughPasses, c.OpponentThroughPasses);
        Row("...de esos, completados", a.OpponentThroughCompleted, c.OpponentThroughCompleted);

        void Row(string label, double armedValue, double controlValue) =>
            _output.WriteLine($"{label,-29} | {armedValue,7:F3} | {controlValue,7:F3} | {armedValue - controlValue,+7:F3}");
    }

    private static void Accumulate(MatchResult result, int carrierId, int carrierTeam, double[] sink)
    {
        var trace = result.Trace;
        if (trace is not null)
        {
            for (int frame = 0; frame < trace.FrameCount; frame++)
            {
                switch (Pitch.ZoneOf(trace.BallAt(frame), carrierTeam))
                {
                    case Zone.Own: sink[0]++; break;
                    case Zone.Middle: sink[1]++; break;
                    default: sink[2]++; break;
                }
            }
        }

        int opponent = carrierTeam == 0 ? 1 : 0;
        sink[3] += result.Report.Goals[opponent];
        sink[4] += result.Report.Shots[opponent];
        sink[5] += result.Report.ThroughPasses[opponent];
        sink[6] += result.Report.ThroughPassesCompleted[opponent];
    }

    private static Side Normalise(double[] sums, int matches)
    {
        double ticks = Math.Max(1, sums[0] + sums[1] + sums[2]);
        double n = Math.Max(1, matches);
        return new Side(
            100.0 * sums[0] / ticks, 100.0 * sums[1] / ticks, 100.0 * sums[2] / ticks,
            sums[3] / n, sums[4] / n, sums[5] / n, sums[6] / n);
    }
}
