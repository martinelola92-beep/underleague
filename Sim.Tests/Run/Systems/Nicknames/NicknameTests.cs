using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Nicknames;

namespace Underleague.Sim.Tests.Run.Systems.Nicknames;

/// <summary>ADR 0163: carga de <c>data/nicknames/nicknames.json</c> y derivación del apodo desde <see cref="RunCareer"/>.</summary>
public sealed class NicknameTests
{
    private static NicknameCatalog Catalog => NicknameLoader.FromJson(TestData.LoadAllFiles());

    private static NicknameDefinition Def(string id, NicknameStat stat, int threshold, int priority) =>
        new(id, new LocalizedName(id, id), stat, threshold, priority);

    private static RunCareer Career(int goals = 0, int matches = 0, int injuriesCaused = 0, int injuriesSuffered = 0, int deaths = 0) =>
        RunCareer.None with
        {
            Goals = goals,
            Matches = matches,
            InjuriesCaused = injuriesCaused,
            InjuriesSuffered = injuriesSuffered,
            DeathsCaused = deaths,
        };

    [Fact]
    public void DataLoadsWithUniqueIdsAndBothLanguages()
    {
        var catalog = Catalog;
        Assert.InRange(catalog.All.Count, 16, 20);
        Assert.Equal(catalog.All.Count, catalog.All.Select(n => n.Id).Distinct().Count());
        foreach (var nickname in catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(nickname.Name.Es));
            Assert.False(string.IsNullOrWhiteSpace(nickname.Name.En));
        }
    }

    /// <summary>
    /// ADR 0170: ningún apodo por partidos jugados puede pedir más partidos de los que tiene el peor camino de la run: un
    /// jugador juega como mucho un partido por nodo de partido, así que un umbral por encima de esa cifra es un apodo que
    /// nadie puede ganar (con 11/12/12 nodos el peor camino eran 20 partidos y `eternal` pedía 20; con 8/9/9 son 14).
    /// </summary>
    [Fact]
    public void NoMatchesThresholdIsAboveTheMatchesOfTheWorstPathOfTheMap()
    {
        var files = TestData.LoadAllFiles();
        var map = Underleague.Sim.Run.Systems.Map.MapLoader.FromJson(files);
        int worstPath = 0;
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            worstPath += MapInvariants.WorstCaseMatches(MapGenerator.Generate(1, act, new MapOptions(map.Of(act))));
        }

        foreach (var nickname in NicknameLoader.FromJson(files).All.Where(n => n.Stat == NicknameStat.Matches))
        {
            Assert.True(
                nickname.Threshold <= worstPath,
                $"{nickname.Id} pide {nickname.Threshold} partidos y el peor camino de la run tiene {worstPath}: nadie puede ganarlo");
        }
    }

    [Fact]
    public void EveryCareerFieldOfTheBriefHasANickname()
    {
        var stats = Catalog.All.Select(n => n.Stat).ToHashSet();
        foreach (var required in new[]
        {
            NicknameStat.Goals, NicknameStat.Assists, NicknameStat.TacklesWon, NicknameStat.Fouls, NicknameStat.Cards,
            NicknameStat.InjuriesCaused, NicknameStat.DeathsCaused, NicknameStat.InjuriesSuffered, NicknameStat.Matches,
        })
        {
            Assert.Contains(required, stats);
        }
    }

    [Fact]
    public void NoNicknameBelowItsThreshold()
    {
        var catalog = new NicknameCatalog(new[] { Def("scorer", NicknameStat.Goals, 3, 10) });
        Assert.Null(NicknameSystem.For(Career(goals: 2), catalog));
        Assert.Equal("scorer", NicknameSystem.For(Career(goals: 3), catalog)?.Id);
    }

    [Fact]
    public void HigherPriorityWinsWhenSeveralConditionsHold()
    {
        var catalog = new NicknameCatalog(new[]
        {
            Def("veteran", NicknameStat.Matches, 5, 10),
            Def("butcher", NicknameStat.InjuriesCaused, 2, 55),
            Def("scorer", NicknameStat.Goals, 1, 40),
        });

        Assert.Equal("butcher", NicknameSystem.For(Career(goals: 4, matches: 9, injuriesCaused: 2), catalog)?.Id);
    }

    [Fact]
    public void EqualPriorityBreaksTiesByIdAscendingRegardlessOfFileOrder()
    {
        var a = Def("b_second", NicknameStat.Goals, 1, 30);
        var b = Def("a_first", NicknameStat.Assists, 1, 30);
        var career = RunCareer.None with { Goals = 1, Assists = 1 };

        Assert.Equal("a_first", NicknameSystem.For(career, new NicknameCatalog(new[] { a, b }))?.Id);
        Assert.Equal("a_first", NicknameSystem.For(career, new NicknameCatalog(new[] { b, a }))?.Id);
    }

    /// <summary>Estabilidad: con la carrera creciendo, el apodo nunca baja de prioridad.</summary>
    [Fact]
    public void NicknameNeverLosesPriorityAsTheCareerGrows()
    {
        var catalog = Catalog;
        int lastPriority = 0;
        for (int step = 0; step <= 60; step++)
        {
            var career = RunCareer.None with
            {
                Matches = step / 2,
                Goals = step / 6,
                Assists = step / 8,
                TacklesWon = step,
                Fouls = step / 4,
                Cards = step / 15,
                InjuriesCaused = step / 10,
                DeathsCaused = step / 40,
                InjuriesSuffered = step / 12,
            };
            int priority = NicknameSystem.For(career, catalog)?.Priority ?? 0;
            Assert.True(priority >= lastPriority, $"paso {step}: la prioridad bajó de {lastPriority} a {priority}");
            lastPriority = priority;
        }
    }

    [Fact]
    public void EarnedIsOnlyTheNicknameThatChangedInTheMatch()
    {
        var catalog = new NicknameCatalog(new[]
        {
            Def("butcher", NicknameStat.InjuriesCaused, 2, 55),
            Def("abattoir", NicknameStat.InjuriesCaused, 5, 70),
        });

        Assert.Null(NicknameSystem.Earned(Career(injuriesCaused: 0), Career(injuriesCaused: 1), catalog));
        Assert.Equal("butcher", NicknameSystem.Earned(Career(injuriesCaused: 1), Career(injuriesCaused: 2), catalog)?.Id);
        Assert.Null(NicknameSystem.Earned(Career(injuriesCaused: 2), Career(injuriesCaused: 4), catalog));
        Assert.Equal("abattoir", NicknameSystem.Earned(Career(injuriesCaused: 4), Career(injuriesCaused: 5), catalog)?.Id);
    }

    [Fact]
    public void BeforeMatchIsTheExactInverseOfWithCareerFrom()
    {
        var player = RunEngine.Start(TestRuns.Setup(), 11, TestData.LoadCatalog()).Roster[0];
        var stats = new PlayerMatchStats(player.Id, 0, Goals: 2, Assists: 1, Shots: 4, PassesAttempted: 9, PassesCompleted: 6,
            Tackles: 3, OffBallTackles: 1, TacklesWon: 2, Fouls: 1, Cards: 1, Injured: true, TicksOnPitch: 900,
            InjuriesCaused: 2, DeathsCaused: 1);

        var after = player.WithCareerFrom(stats).Career;
        Assert.Equal(player.Career, NicknameSystem.BeforeMatch(after, stats));
    }

    [Fact]
    public void DisplayAppendsTheNicknameOrLeavesTheNameAlone()
    {
        Assert.Equal("Grok «el Carnicero»", NicknameSystem.Display("Grok", "el Carnicero"));
        Assert.Equal("Grok", NicknameSystem.Display("Grok", string.Empty));
    }

    [Fact]
    public void LoaderRejectsDuplicateIdsAndUnknownStats()
    {
        const string duplicate = """
            {"nicknames":[
              {"id":"a","name":{"es":"uno","en":"one"},"stat":"goals","threshold":1,"priority":1},
              {"id":"a","name":{"es":"dos","en":"two"},"stat":"goals","threshold":2,"priority":2}]}
            """;
        Assert.Throws<DataException>(() => NicknameLoader.FromJson(new Dictionary<string, string> { ["nicknames/nicknames.json"] = duplicate }));

        const string unknown = """
            {"nicknames":[{"id":"a","name":{"es":"uno","en":"one"},"stat":"dribbles","threshold":1,"priority":1}]}
            """;
        Assert.Throws<DataException>(() => NicknameLoader.FromJson(new Dictionary<string, string> { ["nicknames/nicknames.json"] = unknown }));
    }
}
