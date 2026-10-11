using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>
/// El informe da una fila por perk, no por portador (captura `informe`, 11 oct 2026: la racial de los siete orcos salía
/// siete veces, todas iguales). Las activaciones se suman y los portadores se cuentan.
/// </summary>
public sealed class PostMatchViewPerkRowsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MapNode Node() => new(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

    [Fact]
    public void EachPerkIsOneRowWithItsCarriersAndActivationsAdded()
    {
        int grouped = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            var playback = new MatchPlayback(Node(), setup, result, seed);
            var state = RunEngine.Start(TestRuns.Setup(), 1UL, Catalog);
            var summary = new RunMatchSummary(
                Node().Id, NodeKind.LeagueMatch, result.Report.Winner == 0,
                result.Report.Goals[0], result.Report.Goals[1], result.Report.Ticks, result.Report.WentToGoldenGoal,
                Array.Empty<int>(), Array.Empty<int>(), 0, 0, result.Report);
            var report = PostMatchView.Build(playback, state, summary, Catalog);

            Assert.Equal(report.Perks.Count, report.Perks.Select(p => p.PerkId).Distinct(StringComparer.Ordinal).Count());

            var own = setup.Home.Players.Select(p => p.Id).ToHashSet();
            var expected = result.Report.PerksSummary.Where(e => own.Contains(e.OwnerId) && e.Activations > 0).ToList();
            Assert.Equal(expected.Count, report.Perks.Sum(p => p.Carriers));
            Assert.Equal(expected.Sum(e => e.Activations), report.Perks.Sum(p => p.Activations));
            grouped += report.Perks.Count(p => p.Carriers > 1);
        }

        Assert.True(grouped > 0, "ningún perk con varios portadores en 10 partidos: el test no comprobó la agrupación");
    }
}
