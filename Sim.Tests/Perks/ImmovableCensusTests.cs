using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-U, Regla J: antes de llamar «inerte» a <c>immovable</c>, cuántas veces se dispara. Enanos con estilo
/// Bulwark forzado contra humanos, con y sin el perk sobre el MISMO jugador y las mismas semillas.
/// Distingue «no hace nada» de «nunca se dispara»: activaciones por partido, entradas que sufre el
/// portador, y diferencia pareada de victorias con un portador y con todos los titulares portando.
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class ImmovableCensusTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;

    public ImmovableCensusTests(ITestOutputHelper output) => _output = output;

    private static PlayerDefinition AsBulwark(PlayerDefinition p, bool perk) => p with
    {
        StyleTag = StyleTag.Bulwark,
        Tags = p.Tags.Where(t => !Enum.TryParse<StyleTag>(t, out _)).Append("Bulwark").ToList(),
        Perks = perk ? new[] { "immovable" } : p.Perks,
    };

    private record Tally(int Matches, int Activations, int TacklesOnCarriers, int Wins, int Draws);

    /// <param name="carriers">Cuántos titulares (índices 1..n) llevan el perk; 0 = control.</param>
    private static Tally Run(int carriers, int matchesPerSeed = 3000)
    {
        int activations = 0, tackles = 0, wins = 0, draws = 0, matches = 0;
        for (int i = 0; i < matchesPerSeed; i++)
        {
            var homeRng = RngStreams.Generation(1, i);
            var awayRng = RngStreams.Generation(1, 10_000 + i);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Dwarf, 50, 1, 4);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);
            var ids = new HashSet<int>();
            var players = home.Players
                .Select((p, idx) =>
                {
                    bool carries = idx >= 1 && idx <= carriers;
                    if (carries)
                    {
                        ids.Add(p.Id);
                    }

                    return AsBulwark(p, carries);
                })
                .ToList();
            var setup = new MatchSetup(home with { Players = players }, away, Referee);
            var result = Simulator.Run(setup, RngStreams.MatchSeed(1, i), Catalog, new SimConfig(CollectLog: false));
            matches++;
            foreach (var e in result.Events)
            {
                if (e.Type == EventType.PerkTriggered && e.Detail == "immovable")
                {
                    activations++;
                }
                else if (e.Type == EventType.Tackle && ids.Contains(e.Opponent))
                {
                    tackles++;
                }
            }

            if (result.Report.Winner == 0)
            {
                wins++;
            }
            else if (result.Report.Winner != 1)
            {
                draws++;
            }
        }

        return new Tally(matches, activations, tackles, wins, draws);
    }

    [Fact]
    public void ImmovableFiresAndMovesTheResult()
    {
        foreach (int carriers in new[] { 0, 1, 6 })
        {
            var t = Run(carriers);
            _output.WriteLine(
                $"CENSO portadores {carriers}: partidos {t.Matches} activaciones/partido {t.Activations / (double)t.Matches:F3} "
                + $"entradas sufridas por portadores/partido {t.TacklesOnCarriers / (double)t.Matches:F3} "
                + $"victorias {100.0 * t.Wins / t.Matches:F2} % empates {100.0 * t.Draws / t.Matches:F2} %");
        }

        Assert.True(Run(1, 100).Activations > 0, "el perk tiene que dispararse alguna vez");
    }
}
