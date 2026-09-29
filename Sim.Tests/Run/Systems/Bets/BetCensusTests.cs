using Underleague.Balance;
using Underleague.Sim.Analysis;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;

namespace Underleague.Sim.Tests.Run.Systems.Bets;

/// <summary>
/// ADR 0157, paso 2: el instrumento del censo se valida antes de creerse la medida (Regla J). Cuenta todos
/// los partidos de la run —también el que la termina—, no cambia la run que observa y es determinista con
/// paralelismo.
/// </summary>
public sealed class BetCensusTests
{
    private static readonly Dictionary<string, string> Files = TestData.LoadAllFiles();
    private static readonly StandardRunSystems Standard = StandardRunSystems.FromJson(Files);
    private static readonly BossCatalog Bosses = BossCatalog.FromJson(Files);

    private static RunSetup SetupFor(Race race) => FullRunRunner.SetupFor(race, Standard, Files);

    /// <summary>El observador ve exactamente los partidos que la run contabiliza, incluido el que la termina.</summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    public void TheObserverSeesEveryMatchOfTheRunAndDoesNotChangeIt(ulong seed)
    {
        var catalog = TestData.LoadCatalog();
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);
        var setup = SetupFor(Race.Human);

        int observed = 0;
        int observedKindsNotMatch = 0;
        int withEvents = 0;
        var watched = RunPolicy.Play(setup, seed, catalog, Standard, Bosses, options, (before, node, matchSetup, result, summary) =>
        {
            observed++;
            if (!node.IsMatch)
            {
                observedKindsNotMatch++;
            }

            if (result.Events.Count > 0 && matchSetup.Home.Lineup.Slots.Count > 0 && summary.NodeId == node.Id)
            {
                withEvents++;
            }
        });
        var plain = RunPolicy.Play(setup, seed, catalog, Standard, Bosses, options);

        Assert.Equal(watched.Matches, observed);
        Assert.Equal(0, observedKindsNotMatch);
        Assert.Equal(observed, withEvents);
        Assert.Equal(plain.Outcome, watched.Outcome);
        Assert.Equal(plain.Cause, watched.Cause);
        Assert.Equal(plain.MatchesWon, watched.MatchesWon);
        Assert.Equal(plain.GoldEarned, watched.GoldEarned);
        Assert.Equal(plain.Deaths, watched.Deaths);
        Assert.Equal(plain.OwnInjuries, watched.OwnInjuries);
    }

    [Fact]
    public void TheCensusCountsEveryPlayedMatchAndIsDeterministicUnderParallelism()
    {
        var catalog = TestData.LoadCatalog();
        const int runs = 12;

        var first = BetCensusRunner.Run(catalog, Files, 1UL, runs, () => ThreadCatalogs.Current);
        var second = BetCensusRunner.Run(catalog, Files, 1UL, runs, () => ThreadCatalogs.Current);
        Assert.Equal(first.Matches, second.Matches);
        Assert.Equal(first.Cells, second.Cells);

        // El total de partidos del censo es la suma de los de las mismas runs jugadas una a una.
        long expected = 0;
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);
        var races = FullRunRunner.LaunchRaces(catalog);
        for (int i = 0; i < runs; i++)
        {
            expected += RunPolicy.Play(SetupFor(races[i % races.Count]), BetCensusRunner.RunSeed(1UL, i), catalog, Standard, Bosses, options).Matches;
        }

        Assert.Equal(expected, first.Matches);
        Assert.Equal(first.Matches, first.MatchesByDifficulty.Sum());
    }

    [Fact]
    public void CellsAreConsistent()
    {
        var catalog = TestData.LoadCatalog();
        var census = BetCensusRunner.Run(catalog, Files, 7UL, 8, () => ThreadCatalogs.Current);

        Assert.Equal(Standard.Bets.All.Count * (BetCensusRunner.Difficulties + 1 + 3), census.Cells.Count);
        Assert.All(census.Cells, c =>
        {
            Assert.InRange(c.Hits, 0, c.Matches);
            Assert.True(c.StdErr >= 0);
        });

        // Cada grupo reparte los mismos partidos: por dificultad (con «all») y por tipo de nodo.
        foreach (var bet in Standard.Bets.All)
        {
            var mine = census.Cells.Where(c => c.BetId == bet.Id).ToList();
            long all = mine.Single(c => c.Group == "difficulty" && c.Key == "all").Matches;
            Assert.Equal(census.Matches, all);
            Assert.Equal(all, mine.Where(c => c.Group == "difficulty" && c.Key != "all").Sum(c => c.Matches));
            Assert.Equal(all, mine.Where(c => c.Group == "nodekind").Sum(c => c.Matches));
            Assert.Equal(
                mine.Single(c => c.Group == "difficulty" && c.Key == "all").Hits,
                mine.Where(c => c.Group == "nodekind").Sum(c => c.Hits));
        }
    }

    /// <summary>La semilla de la run es función pura del índice y los lotes de semillas distintas no se solapan.</summary>
    [Fact]
    public void RunSeedsOfDifferentBatchesDoNotOverlap()
    {
        var one = Enumerable.Range(0, 1200).Select(i => BetCensusRunner.RunSeed(1UL, i)).ToHashSet();
        var seven = Enumerable.Range(0, 1200).Select(i => BetCensusRunner.RunSeed(7UL, i)).ToHashSet();
        Assert.Empty(one.Intersect(seven));
        Assert.Equal(BetCensusRunner.RunSeed(1UL, 5), BetCensusRunner.RunSeed(1UL, 5));
    }
}
