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

    /// <summary>
    /// El límite de «Doble disparo» (2 por partido, 25 s de enfriamiento) se respeta en partidos reales: nunca más de
    /// dos activaciones por partido y, cuando hay dos, separadas por el enfriamiento entero. Es lo que BC-B dejó escrito
    /// para el perk viejo (que se encadenaba hasta la profundidad máxima) y que el rediseño no debe perder.
    /// </summary>
    [Fact]
    public void DoubleShotKeepsItsLimitAndItsCooldown()
    {
        var limit = Catalog.Perks.All.Single(p => p.Id == "double_shot").Limit!;
        int cooldownTicks = limit.CooldownTicks;
        int matchesWithTwo = 0;
        for (int i = 0; i < 400; i++)
        {
            var homeRng = RngStreams.Generation(1, i);
            var awayRng = RngStreams.Generation(1, 10_000 + i);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
            var players = home.Players.ToList();
            players[6] = players[6] with { Perks = new[] { "double_shot" } };
            var setup = new MatchSetup(home with { Players = players }, away, Referee);
            var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));

            var ticks = result.Events.Where(e => e.Type == EventType.PerkTriggered && e.Detail == "double_shot")
                .Select(e => e.Tick).ToList();
            Assert.InRange(ticks.Count, 0, limit.Times);
            for (int k = 1; k < ticks.Count; k++)
            {
                Assert.True(ticks[k] - ticks[k - 1] >= cooldownTicks,
                    $"partido {i}: dos activaciones separadas por {ticks[k] - ticks[k - 1]} ticks, el enfriamiento es {cooldownTicks}");
            }

            matchesWithTwo += ticks.Count == 2 ? 1 : 0;
        }

        _output.WriteLine($"partidos con dos activaciones: {matchesWithTwo} de 400 (límite {limit.Times}, enfriamiento {cooldownTicks} ticks)");
    }
    /// <summary>
    /// Revisión independiente: dos perks de rechace en el mismo jugador («Doble disparo» y «A bocajarro») cuelgan del
    /// mismo rebote, y el balón sólo se remata una vez. El primero en el orden de RT-041 remata; el segundo no puede
    /// actuar (el balón ya va en vuelo) y <b>no gasta su uso ni anuncia su cartel</b>: nunca se activan los dos en el
    /// mismo tick. Y al volver a haber un rebote, el que quedó intacto sí puede actuar.
    /// </summary>
    [Fact]
    public void TwoReboundPerksOnTheSameReboundSpendOnlyTheOneThatShoots()
    {
        int activations = 0, both = 0, shotsAfter = 0;
        for (int i = 0; i < 400; i++)
        {
            var homeRng = RngStreams.Generation(1, i);
            var awayRng = RngStreams.Generation(1, 10_000 + i);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
            var players = home.Players.ToList();
            players[6] = players[6] with { Perks = new[] { "double_shot", "point_blank" } };
            var setup = new MatchSetup(home with { Players = players }, away, Referee);
            var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
            int owner = players[6].Id;

            foreach (var tick in result.Events.Where(e => e.Type == EventType.PerkTriggered && e.Actor == owner
                && e.Detail is "double_shot" or "point_blank").GroupBy(e => e.Tick))
            {
                int n = tick.Count();
                activations += n;
                both += n > 1 ? 1 : 0;
                shotsAfter += result.Events.Any(e => e.Tick == tick.Key && e.Type == EventType.Shot && e.Actor == owner) ? 1 : 0;
            }
        }

        _output.WriteLine($"activaciones {activations}, ticks con las dos {both}, ticks con segundo tiro {shotsAfter}");
        Assert.True(activations > 20, "la prueba necesita rebotes rematados");
        Assert.Equal(0, both);
    }
}
