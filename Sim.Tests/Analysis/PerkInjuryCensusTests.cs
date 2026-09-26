using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Cuánta carne añade un perk: lesiones y muertes por partido con el perk armado en toda la línea de
/// campo, contra el MISMO partido sin él (mismas plantillas, mismas semillas, ADR 0087).
///
/// <para>Existe porque el valor en victorias no lo enseña: un perk puede no ganar un partido más y aun
/// así sacar de banda <c>injuriesPerMatch</c> (RT-056: 0,3-0,8), que es un presupuesto del juego entero
/// y no del perk. No es una puerta: vuelca una tabla.</para>
/// </summary>
public sealed class PerkInjuryCensusTests
{
    private const int Quality = 50;
    private const int Level = 4;
    private const int Rosters = 60;
    private const ulong Seed = 7;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Los perks que se miden. Se cambian a mano según la tanda.</summary>
    private static readonly string[] Measured = { "dirty_play", "ankle_bite", "skullsplitter" };

    private readonly ITestOutputHelper _output;

    public PerkInjuryCensusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void HowMuchFleshDoesAPerkAdd()
    {
        _output.WriteLine($"{Rosters * 2} partidos por perk, armado contra espejo · banda RT-056: 0,3-0,8 lesiones/partido");
        _output.WriteLine("perk              | lesiones armado | espejo | Δ      | ± ET  | muertes armado | espejo");
        foreach (var id in Measured)
        {
            var perk = Catalog.Perks.All.Single(p => p.Id == id);
            var diffs = new List<double>();
            double inj = 0, injC = 0, dth = 0, dthC = 0;
            int n = 0;
            for (int roster = 0; roster < Rosters; roster++)
            {
                var sRng = RngStreams.Generation(Seed, roster);
                var mRng = RngStreams.Generation(Seed, 500 + roster);
                var subject = TeamGenerator.Generate(ref sRng, Catalog, "s", Race.Human, Quality, 1, Level);
                var mirror = TeamGenerator.Generate(ref mRng, Catalog, "m", Race.Human, Quality, 100001, Level);

                var players = subject.Players.ToList();
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Position != Position.Goalkeeper && PerkAssignment.Eligible(players[i], Catalog).Any(c => c.Id == id))
                    {
                        players[i] = players[i] with { Perks = new[] { id } };
                    }
                }

                var armed = subject with { Players = players };
                for (int dir = 0; dir < 2; dir++)
                {
                    ulong ms = RngStreams.MatchSeed(Seed, (roster * 2) + dir);
                    var a = Simulator.Run(dir == 0 ? new MatchSetup(armed, mirror, Referee) : new MatchSetup(mirror, armed, Referee), ms, Catalog, new SimConfig(CollectLog: false));
                    var c = Simulator.Run(dir == 0 ? new MatchSetup(subject, mirror, Referee) : new MatchSetup(mirror, subject, Referee), ms, Catalog, new SimConfig(CollectLog: false));
                    int ia = Count(a.Events, EventType.Injury), ic = Count(c.Events, EventType.Injury);
                    inj += ia; injC += ic;
                    dth += Count(a.Events, EventType.Death); dthC += Count(c.Events, EventType.Death);
                    diffs.Add(ia - ic);
                    n++;
                }
            }

            double mean = diffs.Average();
            double sd = Math.Sqrt(diffs.Sum(d => (d - mean) * (d - mean)) / (diffs.Count - 1));
            _output.WriteLine($"{id,-17} | {inj / n,15:0.00} | {injC / n,6:0.00} | {mean,+6:0.00} | {sd / Math.Sqrt(n),5:0.00} | {dth / n,14:0.000} | {dthC / n,6:0.000}");
        }

        Assert.True(true);
    }

    private static int Count(IReadOnlyList<MatchEvent> events, EventType type) =>
        events.Count(e => e.Type == type && !e.Detail.EndsWith(":cancelled", StringComparison.Ordinal));
}
