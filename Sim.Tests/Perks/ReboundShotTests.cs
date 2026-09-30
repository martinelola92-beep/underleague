using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BC-C (ADR 0180): «Doble disparo» y «A bocajarro» remachan el rechace de su propio tiro. En partidos
/// reales cada activación ocurre justo después de que el tiro del portador termine en balón suelto
/// (bloqueo, rechace del portero o palo) y va seguida de un tiro suyo en el mismo tick.
/// </summary>
public sealed class ReboundShotTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;

    public ReboundShotTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("double_shot")]
    [InlineData("point_blank")]
    public void EveryActivationFollowsARebound​OfTheOwnersShotAndIsFollowedByAnotherShot(string perkId)
    {
        int activations = 0;
        for (int slot = 4; slot <= 6; slot++)
        {
            for (int i = 0; i < 250; i++)
            {
                var homeRng = RngStreams.Generation(1, i);
                var awayRng = RngStreams.Generation(1, 10_000 + i);
                var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
                var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
                var players = home.Players.ToList();
                players[slot] = players[slot] with { Perks = new[] { perkId } };
                var setup = new MatchSetup(home with { Players = players }, away, Referee);
                var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
                int owner = players[slot].Id;

                var events = result.Events;
                for (int k = 0; k < events.Count; k++)
                {
                    if (events[k].Type != EventType.PerkTriggered || events[k].Detail != perkId)
                    {
                        continue;
                    }

                    activations++;
                    var tick = events.Where(e => e.Tick == events[k].Tick).ToList();
                    bool rebound = tick.Any(e =>
                        (e.Type == EventType.ShotBlocked && e.Opponent == owner)
                        || (e.Type == EventType.Save && e.Detail == "parried" && e.Opponent == owner)
                        || (e.Type == EventType.ShotPost && e.Actor == owner));
                    bool secondShot = tick.Any(e => e.Type == EventType.Shot && e.Actor == owner);
                    Assert.True(rebound, $"{perkId}: partido {i}, tick {events[k].Tick}: se activó sin un rebote de su tiro");
                    Assert.True(secondShot, $"{perkId}: partido {i}, tick {events[k].Tick}: se activó y no hubo segundo tiro");
                }
            }
        }

        _output.WriteLine($"{perkId}: {activations} activaciones en 750 partidos");
        Assert.True(activations > 0, "el perk no llega a activarse: la prueba no demuestra nada");
    }
}
