using Underleague.Sim.Data;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Nicknames;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>ADR 0163: el informe post-partido enseña las estadísticas por jugador y los apodos ganados.</summary>
public sealed class PostMatchViewNicknameTests
{
    private static readonly Catalog Catalog = Run.Systems.SystemsTestSupport.Catalog;
    private static readonly Underleague.Sim.Run.Systems.StandardRunSystems Systems = Run.Systems.SystemsTestSupport.Systems;

    private static (PostMatchReport Report, Underleague.Sim.Engine.MatchReport Match, RunState After) Play(ulong seed, NicknameCatalog? nicknames)
    {
        var start = RunEngine.Start(Run.Systems.SystemsTestSupport.Setup(), seed, Catalog, Systems);
        var (walked, node) = TestRuns.WalkToMatch(start, Catalog, Systems);
        var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, Systems);
        var playback = MatchPlaybacks.Of(walked, node.Id, Catalog, Systems);
        var report = PostMatchView.Build(playback, entry.State, entry.Summary, Catalog, Systems.Economy, Systems.Items, "es", nicknames);
        return (report, playback.Result.Report, entry.State);
    }

    [Fact]
    public void PlayerStatsMatchTheMatchReportExactly()
    {
        var (report, match, _) = Play(18001UL, Systems.Nicknames);

        var own = match.Players.Where(p => p.Team == 0 && p.TicksOnPitch > 0).OrderBy(p => p.PlayerId).ToList();
        Assert.NotEmpty(own);
        Assert.Equal(own.Count, report.PlayerStats.Count);
        for (int i = 0; i < own.Count; i++)
        {
            var row = report.PlayerStats[i];
            Assert.Equal(own[i].PlayerId, row.PlayerId);
            Assert.Equal(own[i].Goals, row.Goals);
            Assert.Equal(own[i].Assists, row.Assists);
            Assert.Equal(own[i].TacklesWon, row.TacklesWon);
            Assert.Equal(own[i].Fouls, row.Fouls);
            Assert.Equal(own[i].InjuriesCaused, row.InjuriesCaused);
            Assert.False(string.IsNullOrWhiteSpace(row.PlayerName));
        }

        // Los goles por jugador suman los goles del equipo propio (ninguno en propia puerta se atribuye al rival).
        Assert.True(report.PlayerStats.Sum(r => r.Goals) <= report.GoalsFor);
    }

    [Fact]
    public void ANicknameEarnedInTheMatchIsAnnouncedWithItsPreviousOne()
    {
        // Con «un partido jugado» como condición, todos los que pisaron el campo lo ganan en este partido.
        var everyone = new NicknameCatalog(new[]
        {
            new NicknameDefinition("rookie", new LocalizedName("el Novato", "the Rookie"), NicknameStat.Matches, 1, 10),
        });
        var (report, _, _) = Play(18002UL, everyone);

        Assert.Equal(report.PlayerStats.Count, report.NicknamesEarned.Count);
        Assert.All(report.NicknamesEarned, gain =>
        {
            Assert.Equal("rookie", gain.NicknameId);
            Assert.Equal("el Novato", gain.Nickname);
            Assert.Equal(string.Empty, gain.PreviousNickname);
        });
        Assert.All(report.PlayerStats, row => Assert.Equal("el Novato", row.Nickname));
    }

    [Fact]
    public void NoCatalogMeansNoNicknamesButStillStats()
    {
        var (report, _, _) = Play(18003UL, null);
        Assert.Empty(report.NicknamesEarned);
        Assert.NotEmpty(report.PlayerStats);
        Assert.All(report.PlayerStats, row => Assert.Equal(string.Empty, row.Nickname));
    }

    [Fact]
    public void AnAlreadyHeldNicknameIsNotAnnouncedAgain()
    {
        var rookie = new NicknameCatalog(new[]
        {
            new NicknameDefinition("rookie", new LocalizedName("el Novato", "the Rookie"), NicknameStat.Matches, 1, 10),
        });
        var start = RunEngine.Start(Run.Systems.SystemsTestSupport.Setup(), 18004UL, Catalog, Systems);
        var (walked, node) = TestRuns.WalkToMatch(start, Catalog, Systems);
        // Todos llegan con un partido ya jugado: el apodo ya lo tenían.
        for (int i = 0; i < walked.Roster.Count; i++)
        {
            var p = walked.Roster[i];
            walked = walked.WithPlayer(p with { Career = p.Career with { Matches = 3 } });
        }

        var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, Systems);
        var playback = MatchPlaybacks.Of(walked, node.Id, Catalog, Systems);
        var report = PostMatchView.Build(playback, entry.State, entry.Summary, Catalog, Systems.Economy, Systems.Items, "es", rookie);
        Assert.Empty(report.NicknamesEarned);
    }
}
