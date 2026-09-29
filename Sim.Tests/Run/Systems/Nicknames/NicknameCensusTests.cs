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

    private static RunState WithCareers(ulong seed, params int[] matchesByPlayer)
    {
        var state = RunEngine.Start(Underleague.Sim.Tests.Run.TestRuns.Setup(), seed, TestData.LoadCatalog());
        for (int i = 0; i < state.Roster.Count; i++)
        {
            int matches = i < matchesByPlayer.Length ? matchesByPlayer[i] : 0;
            state = state.WithPlayer(state.Roster[i] with { Career = RunCareer.None with { Matches = matches } });
        }

        return state;
    }

    /// <summary>
    /// Regla J, caso con respuesta sabida: un apodo que se gana con «un partido jugado» lo llevan <b>todos</b>
    /// los que jugaron y ninguno de los que no, en el 100 % de las runs. Tres runs, dos jugadores con partidos
    /// en cada una: 6 jugadores medidos, 6 con apodo, 3 de 3 runs.
    /// </summary>
    [Fact]
    public void ANicknameEveryoneWhoPlayedEarnsIsFoundInAllRunsAndOnlyOnThoseWhoPlayed()
    {
        var everyone = new NicknameCatalog(new[]
        {
            new NicknameDefinition("rookie", new Underleague.Sim.Data.LocalizedName("el Novato", "the Rookie"), NicknameStat.Matches, 1, 10),
        });
        var finals = new[] { WithCareers(1UL, 1, 4), WithCareers(2UL, 0, 2, 0, 7), WithCareers(3UL, 9, 0, 0, 0, 1) };
        // Dos jugadores con partidos por run; los que tienen 0 no cuentan.

        var census = NicknameCensusRunner.Tally(finals, everyone);

        Assert.Equal(3, census.Runs);
        Assert.Equal(6, census.PlayersWhoPlayed);
        Assert.Equal(6, census.PlayersWithNickname);
        var cell = Assert.Single(census.Cells);
        Assert.Equal(3, cell.RunsWith);
        Assert.Equal(6, cell.Players);
        Assert.Equal(3, cell.RunsEligible);
        Assert.Equal(9, cell.MaxValue);
        Assert.Equal(1.0, cell.RunShare(census.Runs));
    }

    /// <summary>El otro extremo con respuesta sabida: un umbral inalcanzable no lo lleva nadie y sale al 0 %.</summary>
    [Fact]
    public void AnUnreachableThresholdIsFoundInNoRunAndOnNoPlayer()
    {
        var never = new NicknameCatalog(new[]
        {
            new NicknameDefinition("legend", new Underleague.Sim.Data.LocalizedName("la Leyenda", "the Legend"), NicknameStat.Matches, 1_000_000, 50),
        });
        var census = NicknameCensusRunner.Tally(new[] { WithCareers(1UL, 5, 6), WithCareers(2UL, 3) }, never);

        Assert.Equal(3, census.PlayersWhoPlayed);
        Assert.Equal(0, census.PlayersWithNickname);
        var cell = Assert.Single(census.Cells);
        Assert.Equal(0, cell.RunsWith);
        Assert.Equal(0, cell.Players);
        Assert.Equal(0, cell.RunsEligible);
        Assert.Equal(6, cell.MaxValue);
        Assert.Equal(0.0, cell.RunShare(census.Runs));
    }

    /// <summary>
    /// Con dos apodos cumplidos lo lleva el de mayor prioridad, y el otro sigue contando como «elegible» (lo
    /// cumple aunque no lo lleve): es la diferencia que el censo enseña entre «nadie lo tiene» y «nadie lo
    /// ve porque otro lo tapa».
    /// </summary>
    [Fact]
    public void TheHigherPriorityNicknameCoversTheOtherButStaysEligible()
    {
        var both = new NicknameCatalog(new[]
        {
            new NicknameDefinition("rookie", new Underleague.Sim.Data.LocalizedName("el Novato", "the Rookie"), NicknameStat.Matches, 1, 10),
            new NicknameDefinition("veteran", new Underleague.Sim.Data.LocalizedName("el Veterano", "the Veteran"), NicknameStat.Matches, 5, 40),
        });
        var census = NicknameCensusRunner.Tally(new[] { WithCareers(1UL, 5, 2) }, both);

        var rookie = census.Cells.Single(c => c.NicknameId == "rookie");
        var veteran = census.Cells.Single(c => c.NicknameId == "veteran");
        Assert.Equal(1, rookie.Players);
        Assert.Equal(1, veteran.Players);
        Assert.Equal(2, rookie.PlayersEligible);
        Assert.Equal(1, veteran.PlayersEligible);
    }
}
