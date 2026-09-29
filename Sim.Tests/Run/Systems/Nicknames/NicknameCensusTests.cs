using Underleague.Balance;
using Underleague.Sim.Analysis;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Nicknames;

namespace Underleague.Sim.Tests.Run.Systems.Nicknames;

/// <summary>
/// ADR 0163, «Censo»: el instrumento se valida antes de creerse la medida (Regla J). Mide a quien dice medir
/// (sólo jugadores con partidos), es determinista bajo paralelismo y sus totales cuadran.
/// </summary>
public sealed class NicknameCensusTests
{
    private static readonly Dictionary<string, string> Files = TestData.LoadAllFiles();

    [Fact]
    public void TheCensusIsDeterministicUnderParallelismAndItsTotalsAddUp()
    {
        var catalog = TestData.LoadCatalog();
        const int runs = 10;

        var first = NicknameCensusRunner.Run(catalog, Files, 1UL, runs, () => ThreadCatalogs.Current);
        var second = NicknameCensusRunner.Run(catalog, Files, 1UL, runs, () => ThreadCatalogs.Current);
        Assert.Equal(first.PlayersWhoPlayed, second.PlayersWhoPlayed);
        Assert.Equal(first.PlayersWithNickname, second.PlayersWithNickname);
        Assert.Equal(first.Cells, second.Cells);

        Assert.True(first.PlayersWhoPlayed > 0);
        Assert.Equal(first.PlayersWithNickname, first.Cells.Sum(c => c.Players));
        Assert.True(first.PlayersWithNickname <= first.PlayersWhoPlayed);
        Assert.All(first.Cells, c => Assert.InRange(c.RunsWith, 0, runs));
        Assert.All(first.Cells, c => Assert.True(c.RunsWith <= c.Players));
    }

    /// <summary>El caso cuya respuesta se sabe: contando a mano sobre la run jugada sale lo mismo que el censo de una sola run.</summary>
    [Fact]
    public void ACensusOfOneRunMatchesTheHandCountOfItsFinalRoster()
    {
        var catalog = TestData.LoadCatalog();
        var standard = StandardRunSystems.FromJson(Files);
        var bosses = BossCatalog.FromJson(Files);
        var races = FullRunRunner.LaunchRaces(catalog);
        var played = RunPolicy.Play(
            FullRunRunner.SetupFor(races[0], standard, Files),
            BetCensusRunner.RunSeed(3UL, 0),
            catalog,
            standard,
            bosses,
            RunPolicyOptions.For(PurchaseDoctrine.Contextual));

        var state = played.FinalState;
        Assert.NotNull(state);
        Assert.True(RunEngine.Outcome(state).IsOver);
        int withMatches = state.Roster.Count(p => p.Career.Matches > 0);
        int named = state.Roster.Count(p => p.Career.Matches > 0 && NicknameSystem.For(p, standard.Nicknames) is not null);

        var census = NicknameCensusRunner.Run(catalog, Files, 3UL, 1, () => ThreadCatalogs.Current);
        Assert.Equal(withMatches, census.PlayersWhoPlayed);
        Assert.Equal(named, census.PlayersWithNickname);
    }
}
