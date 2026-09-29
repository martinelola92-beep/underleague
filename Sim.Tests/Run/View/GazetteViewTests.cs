using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Gazette;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>ADR 0163: la Gaceta de fin de run es determinista, no deja textos vacíos ni marcadores sin sustituir, y cuenta lo que pasó.</summary>
public sealed class GazetteViewTests
{
    private static readonly Catalog Catalog = Run.Systems.SystemsTestSupport.Catalog;
    private static readonly Underleague.Sim.Run.Systems.StandardRunSystems Systems = Run.Systems.SystemsTestSupport.Systems;
    private static readonly string[] Languages = { "es", "en" };

    private static RunState Base(ulong seed) => RunEngine.Start(Run.Systems.SystemsTestSupport.Setup(), seed, Catalog, Systems);

    private static GazetteReport Build(RunState state, string language = "es") =>
        GazetteView.Build(state, Catalog, Systems.Nicknames, Systems.Rivals, Systems.Gazette, language);

    private static RunState WithCareer(RunState state, int index, RunCareer career, PhysicalState? physical = null)
    {
        var player = state.Roster[index] with { Career = career };
        if (physical is { } p)
        {
            player = player with { PhysicalState = p };
        }

        return state.WithPlayer(player);
    }

    /// <summary>Una run terminada con caídos, un MVP claro y un villano acreditado en <c>RivalCredits</c>.</summary>
    private static RunState Rich(ulong seed, RunOutcome outcome)
    {
        var state = Base(seed);
        state = WithCareer(state, 0, RunCareer.None with { Matches = 9, Goals = 6, Assists = 2, TacklesWon = 4, InjuriesCaused = 3 });
        state = WithCareer(state, 1, RunCareer.None with { Matches = 4, Goals = 1 }, PhysicalState.Dead);
        state = WithCareer(state, 2, new RunCareer(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), PhysicalState.Dead);
        string clan = Systems.Rivals.All[0].Id;
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in state.Counters)
        {
            counters[key] = value;
        }

        counters[$"{RunState.RivalCreditPrefix}{clan}:1:{state.Roster[1].Id}:sufferedDeath"] = 1;
        counters[$"{RunState.RivalCreditPrefix}{clan}:1:{state.Roster[3].Id}:sufferedInjury"] = 2;
        return state with { Counters = counters, Result = outcome };
    }

    [Fact]
    public void TemplatesLoadWithEveryRequiredKeyInBothLanguages()
    {
        var gazette = Systems.Gazette;
        foreach (string key in GazetteCatalog.RequiredKeys)
        {
            foreach (string language in Languages)
            {
                Assert.NotEmpty(gazette.Variants(key, language));
            }
        }

        // Los titulares y las esquelas tienen variedad de verdad, no una sola frase.
        foreach (string key in new[] { "headline.victory", "headline.defeatBoss", "headline.defeatPlayers", "epitaph.byRival", "epitaph.unknown" })
        {
            Assert.True(gazette.Variants(key, "es").Count >= 3, key);
            Assert.Equal(gazette.Variants(key, "es").Count, gazette.Variants(key, "en").Count);
        }
    }

    [Fact]
    public void LoaderRejectsAMissingRequiredKey()
    {
        const string incomplete = """{"texts":[{"key":"masthead","es":["a"],"en":["a"]}]}""";
        Assert.Throws<DataException>(() => GazetteLoader.FromJson(new Dictionary<string, string> { ["gazette/gazette.json"] = incomplete }));
    }

    [Fact]
    public void SameRunGivesTheSameGazette()
    {
        var outcome = new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5);
        foreach (string language in Languages)
        {
            var a = Build(Rich(30001UL, outcome), language);
            var b = Build(Rich(30001UL, outcome), language);
            Assert.Equal(a.Masthead, b.Masthead);
            Assert.Equal(a.Headline, b.Headline);
            Assert.Equal(a.Lede, b.Lede);
            Assert.Equal(a.Mvp, b.Mvp);
            Assert.Equal(a.Villain, b.Villain);
            Assert.Equal(a.Obituaries, b.Obituaries);
        }
    }

    [Fact]
    public void NoTextIsEmptyAndNoPlaceholderSurvivesAcrossOutcomesSeedsAndLanguages()
    {
        var outcomes = new[]
        {
            new RunOutcome(RunOutcomeKind.Victory),
            new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5),
            new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, 5),
        };

        foreach (var outcome in outcomes)
        {
            for (ulong seed = 30100; seed < 30124; seed++)
            {
                foreach (string language in Languages)
                {
                    foreach (var state in new[] { Rich(seed, outcome), Base(seed) with { Result = outcome } })
                    {
                        var report = Build(state, language);
                        var texts = new List<string>
                        {
                            report.Masthead, report.Headline, report.Lede, report.MvpTitle, report.VillainTitle,
                            report.ObituariesTitle, report.ObituariesNone,
                        };
                        if (report.Mvp is not null)
                        {
                            texts.Add(report.Mvp.Line);
                        }

                        if (report.Villain is not null)
                        {
                            texts.Add(report.Villain.Line);
                        }

                        foreach (var obituary in report.Obituaries)
                        {
                            texts.AddRange(new[] { obituary.Title, obituary.Career, obituary.Epitaph });
                        }

                        Assert.All(texts, text =>
                        {
                            Assert.False(string.IsNullOrWhiteSpace(text));
                            Assert.DoesNotContain("{", text);
                            Assert.DoesNotContain("}", text);
                        });
                    }
                }
            }
        }
    }

    [Fact]
    public void VictoryWithoutDeathsUsesTheCleanHeadlineAndDefeatTheirOwn()
    {
        var clean = Build(Base(30200UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) });
        Assert.Contains(clean.Headline, Systems.Gazette.Variants("headline.victoryClean", "es"));
        Assert.Empty(clean.Obituaries);
        Assert.Null(clean.Villain);

        var bossState = Rich(30201UL, new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5));
        var boss = Build(bossState);
        var bossHeadlines = Systems.Gazette.Variants("headline.defeatBoss", "es")
            .Select(v => v.Replace("{act}", bossState.Act.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Contains(boss.Headline, bossHeadlines);
        var players = Build(Rich(30202UL, new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, 5)));
        Assert.Contains(players.Headline, Systems.Gazette.Variants("headline.defeatPlayers", "es"));
        Assert.False(players.Victory);
    }

    [Fact]
    public void HeadlinesVaryWithTheSeed()
    {
        var outcome = new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, 5);
        var distinct = new HashSet<string>();
        for (ulong seed = 30300; seed < 30320; seed++)
        {
            distinct.Add(Build(Rich(seed, outcome)).Headline);
        }

        Assert.True(distinct.Count >= 2, "veinte semillas dieron el mismo titular: la variante no depende de la semilla");
    }

    [Fact]
    public void TheMvpIsTheBestByTheDocumentedMetricAndCarriesTheirNickname()
    {
        // Los goles del MVP son el umbral de «el Verdugo de Porteros» leído del catálogo, no una cifra
        // copiada: el censo de apodos (ADR 0163) mueve los umbrales y este test no debe enterarse.
        var bane = Systems.Nicknames.Find("keepers_bane")!;
        var state = Rich(30400UL, new RunOutcome(RunOutcomeKind.Victory));
        state = WithCareer(state, 0, RunCareer.None with { Matches = 9, Goals = bane.Threshold, Assists = 2, TacklesWon = 4, InjuriesCaused = 3 });
        var report = Build(state);

        Assert.NotNull(report.Mvp);
        Assert.Equal(state.Roster[0].Id, report.Mvp!.PlayerId);
        int expected = (bane.Threshold * 4) + (2 * 3) + 4 + (3 * 2) + 9;
        Assert.Equal(expected, GazetteView.MvpScore(state.Roster[0].Career));

        // Cumple «el Verdugo de Porteros» (prioridad 60) y quizá otros de menos: gana el de mayor
        // prioridad, y la línea lo enseña.
        Assert.Equal(bane.Name.Es, report.Mvp.Nickname);
        Assert.Contains("«" + bane.Name.Es + "»", report.Mvp.Line);
        Assert.Contains(state.Roster[0].Name, report.Mvp.Line);
    }

    [Fact]
    public void AMvpTieGoesToTheLowerId()
    {
        var state = Base(30500UL);
        var equal = RunCareer.None with { Matches = 3, Goals = 2 };
        state = WithCareer(state, 4, equal);
        state = WithCareer(state, 2, equal);
        var report = Build(state with { Result = new RunOutcome(RunOutcomeKind.Victory) });
        Assert.Equal(Math.Min(state.Roster[2].Id, state.Roster[4].Id), report.Mvp!.PlayerId);
    }

    [Fact]
    public void NoOneHasPlayedMeansNoMvp()
    {
        Assert.Null(Build(Base(30600UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) }).Mvp);
    }

    [Fact]
    public void TheVillainComesFromRivalCreditsAndIsOmittedWithoutThem()
    {
        var state = Rich(30700UL, new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5));
        var villain = Build(state).Villain;

        Assert.NotNull(villain);
        var clan = Systems.Rivals.All[0];
        Assert.Equal(clan.Players[1].Name, villain!.Name);
        Assert.Equal(1, villain.Deaths);
        Assert.Equal(2, villain.Injuries);
        Assert.Contains(villain.Name, villain.Line);

        Assert.Null(Build(Base(30701UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) }).Villain);
    }

    [Fact]
    public void EachFallenPlayerGetsAnObituaryWithTheirNameCareerAndKiller()
    {
        var state = Rich(30800UL, new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5));
        var report = Build(state);

        Assert.Equal(RunSummary.Fallen(state).Count, report.Obituaries.Count);
        var withRival = report.Obituaries.Single(o => o.PlayerId == state.Roster[1].Id);
        Assert.Contains(state.Roster[1].Name, withRival.Title);
        Assert.Contains("4 partidos", withRival.Career);
        Assert.Contains(Systems.Rivals.All[0].Players[1].Name, withRival.Epitaph);

        var withoutCareer = report.Obituaries.Single(o => o.PlayerId == state.Roster[2].Id);
        Assert.Contains(withoutCareer.Epitaph, Systems.Gazette.Variants("epitaph.unknown", "es"));
        Assert.Contains(withoutCareer.Career, Systems.Gazette.Variants("epitaph.noCareer", "es"));
    }

    [Fact]
    public void EnglishTextsDifferFromSpanish()
    {
        var state = Rich(30900UL, new RunOutcome(RunOutcomeKind.Victory));
        Assert.NotEqual(Build(state, "es").Headline, Build(state, "en").Headline);
    }
}
