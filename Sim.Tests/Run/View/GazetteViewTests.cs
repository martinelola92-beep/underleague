using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Gazette;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>ADR 0163: la Gaceta de fin de run es determinista, no deja textos vacíos ni marcadores sin sustituir, y cuenta lo que pasó.</summary>
public sealed class GazetteViewTests
{
    private static Catalog Catalog => Run.Systems.SystemsTestSupport.Catalog;
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
        foreach (string key in new[] { "headline.victory", "headline.defeatBoss", "headline.defeatPlayers", "epitaph.byRival", "epitaph.byOpponent", "epitaph.noAuthor", "epitaph.sacrifice", "epitaph.quack", "epitaph.unknown" })
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
        // Dos lesiones acreditadas a la MISMA víctima son un lesionado (víctimas distintas, no eventos).
        Assert.Equal(1, villain.Injuries);
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

    // ------------------------------------------------------------------ revisión independiente (29 sep 2026)

    private static readonly int RivalOffset = Underleague.Sim.Run.Systems.Rivals.RivalTeamBuilder.OpponentFirstPlayerId;

    /// <summary>Índice -1: sin matador; -2: matador de un jefe o rival procedural (fuera del catálogo de clanes).</summary>
    private static MatchEvent Casualty(EventType type, int tick, int victimId, int rivalIndex, string detail) =>
        new(type, tick, 0, victimId, -1, rivalIndex == -1 ? -1 : rivalIndex == -2 ? 1_000_004 : RivalOffset + rivalIndex, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, detail);

    /// <summary>Juega un partido sintético contra el primer clan del catálogo con esos eventos y devuelve el estado resuelto.</summary>
    private static RunState ApplyMatch(RunState state, IReadOnlyList<MatchEvent> events)
    {
        var node = new MapNode(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), Systems.Rivals.All[0].Id, 3);
        var starters = state.Roster.Take(7).Select(p => p.ToDefinition(Catalog)).ToList();
        var lineup = new MatchLineup(starters, Array.Empty<PlayerDefinition>(), Lineup.Default(starters), EmergencyGoalkeeperId: -1);
        var builder = new MatchReportBuilder { Winner = 0, Ticks = 900 };
        foreach (var player in state.Roster.Take(7))
        {
            builder.Players.Add(new PlayerMatchStats(player.Id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 900, 0, 0));
        }

        return MatchResolution.Apply(state, node, lineup, new MatchResult(events, builder.Build(), Array.Empty<PlayerCounterDelta>()), Catalog).State;
    }

    /// <summary>
    /// El villano cuenta <b>víctimas distintas</b>: la pareja real que emite el motor cuando alguien con una
    /// lesión grave sin tratar vuelve a lesionarse (INJURY severe y luego DEATH del mismo jugador) es un
    /// muerto, no un muerto y un lesionado; y lesionar dos veces al mismo es un lesionado.
    /// </summary>
    [Fact]
    public void TheVillainCountsDistinctVictimsAndADeadPlayerIsNotAlsoInjured()
    {
        var state = Base(31000UL);
        var events = new[]
        {
            Casualty(EventType.Injury, 100, state.Roster[1].Id, 2, "severe"),
            Casualty(EventType.Death, 100, state.Roster[1].Id, 2, "severeInjury"),
            Casualty(EventType.Injury, 200, state.Roster[2].Id, 2, "minor"),
            Casualty(EventType.Injury, 300, state.Roster[2].Id, 2, "minor"),
        };

        var villain = Build(ApplyMatch(state, events) with { Result = new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5) }).Villain;

        Assert.NotNull(villain);
        Assert.Equal(Systems.Rivals.All[0].Players[2].Name, villain!.Name);
        Assert.Equal(1, villain.Deaths);
        Assert.Equal(1, villain.Injuries);
    }

    /// <summary>Cada vía de muerte deja su causa y la esquela cuenta la que fue, no «cayó en el campo».</summary>
    [Fact]
    public void EachDeathCauseGetsItsOwnEpitaph()
    {
        var state = Base(31100UL) with { Result = new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5) };
        var economy = Systems.Economy;
        var items = Systems.Items;

        state = DeathConsequences.Kill(state, state.Roster[1].Id, PlayerDeathCause.Sacrifice, Catalog, economy, items);
        state = DeathConsequences.Kill(state, state.Roster[2].Id, PlayerDeathCause.Quack, Catalog, economy, items);
        int unknown = state.Roster[3].Id;
        state = state.WithPlayer(state.Roster[3] with { PhysicalState = PhysicalState.Dead });

        // En partido: sin matador identificable, y con un matador que no es del catálogo de rivales.
        var matchState = ApplyMatch(Base(31100UL), new[]
        {
            Casualty(EventType.Death, 50, Base(31100UL).Roster[4].Id, -1, "severeInjury"),
            Casualty(EventType.Death, 60, Base(31100UL).Roster[5].Id, -2, "perk:skullsplitter"),
        });
        Assert.Equal(PlayerDeathCause.MatchNoAuthor, matchState.DeathCauseOf(matchState.Roster[4].Id));
        Assert.Equal(PlayerDeathCause.MatchByOpponent, matchState.DeathCauseOf(matchState.Roster[5].Id));

        Assert.Equal(PlayerDeathCause.Sacrifice, state.DeathCauseOf(state.Roster[1].Id));
        Assert.Equal(PlayerDeathCause.Quack, state.DeathCauseOf(state.Roster[2].Id));
        Assert.Equal(PlayerDeathCause.Unknown, state.DeathCauseOf(unknown));

        foreach (string language in Languages)
        {
            var obituaries = Build(state, language).Obituaries;
            Assert.Contains(obituaries.Single(o => o.PlayerId == state.Roster[1].Id).Epitaph, Systems.Gazette.Variants("epitaph.sacrifice", language));
            Assert.Contains(obituaries.Single(o => o.PlayerId == state.Roster[2].Id).Epitaph, Systems.Gazette.Variants("epitaph.quack", language));
            Assert.Contains(obituaries.Single(o => o.PlayerId == unknown).Epitaph, Systems.Gazette.Variants("epitaph.unknown", language));

            var matchObituaries = Build(matchState with { Result = state.Result }, language).Obituaries;
            Assert.Contains(matchObituaries.Single(o => o.PlayerId == matchState.Roster[4].Id).Epitaph, Systems.Gazette.Variants("epitaph.noAuthor", language));
            Assert.Contains(matchObituaries.Single(o => o.PlayerId == matchState.Roster[5].Id).Epitaph, Systems.Gazette.Variants("epitaph.byOpponent", language));
        }
    }

    /// <summary>Un matador que sí es del catálogo (hay crédito) sigue nombrándose: el crédito manda sobre la causa genérica.</summary>
    [Fact]
    public void ACatalogRivalKillerIsStillNamed()
    {
        var start = Base(31200UL);
        var state = ApplyMatch(start, new[] { Casualty(EventType.Death, 50, start.Roster[3].Id, 4, "perk:skullsplitter") });
        var epitaph = Build(state with { Result = new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5) })
            .Obituaries.Single(o => o.PlayerId == start.Roster[3].Id).Epitaph;

        Assert.Contains(Systems.Rivals.All[0].Players[4].Name, epitaph);
    }

    /// <summary>Con sólo partidos jugados no hay MVP: la Gaceta dice <c>mvp.none</c> y nunca «fue el mejor del club: .».</summary>
    [Fact]
    public void AnMvpNeedsFactsAndNobodyMeansTheNoneLine()
    {
        var state = Base(31300UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) };
        state = WithCareer(state, 2, RunCareer.None with { Matches = 12 });
        state = WithCareer(state, 3, RunCareer.None with { Matches = 8 });

        foreach (string language in Languages)
        {
            var report = Build(state, language);
            Assert.Null(report.Mvp);
            Assert.Contains(report.MvpNone, Systems.Gazette.Variants("mvp.none", language));
        }

        // Con un solo hecho ya es MVP, y su línea nunca queda con la lista vacía.
        state = WithCareer(state, 3, RunCareer.None with { Matches = 8, TacklesWon = GazetteView.MvpMinimumFacts });
        var mvp = Build(state).Mvp;
        Assert.Equal(state.Roster[3].Id, mvp!.PlayerId);
        Assert.DoesNotContain(": .", mvp.Line);
    }

    /// <summary>Empate de puntos: más partidos; luego quien no es portero; luego el id menor.</summary>
    [Fact]
    public void MvpTiesGoToMoreMatchesThenNonKeepersThenLowerId()
    {
        var state = Base(31400UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) };
        var keeper = state.Roster.First(p => p.Position == Position.Goalkeeper);
        var outfield = state.Roster.Where(p => p.Position != Position.Goalkeeper).OrderBy(p => p.Id).Take(2).ToList();

        // Mismos puntos (9) con distinta mezcla: 4·1 gol + 5 partidos contra 1 entrada + 8 partidos.
        var fewerMatches = state.WithPlayer(keeper with { Career = RunCareer.None with { Matches = 5, Goals = 1 } });
        fewerMatches = fewerMatches.WithPlayer(outfield[0] with { Career = RunCareer.None with { Matches = 8, TacklesWon = 1 } });
        Assert.Equal(outfield[0].Id, Build(fewerMatches).Mvp!.PlayerId);

        // Idéntica carrera: el portero, con el id menor de la plantilla, ya no gana el desempate.
        var same = RunCareer.None with { Matches = 6, Goals = 1 };
        var tie = state.WithPlayer(keeper with { Career = same }).WithPlayer(outfield[1] with { Career = same });
        Assert.NotEqual(keeper.Id, Build(tie).Mvp!.PlayerId);
        Assert.Equal(outfield[1].Id, Build(tie).Mvp!.PlayerId);

        // Entre dos que no son porteros, el id menor.
        var two = state.WithPlayer(outfield[0] with { Career = same }).WithPlayer(outfield[1] with { Career = same });
        Assert.Equal(outfield[0].Id, Build(two).Mvp!.PlayerId);
    }

    /// <summary>Los destacados van de lo más memorable a lo menos: muertes y lesiones causadas antes que asistencias.</summary>
    [Fact]
    public void HighlightsListTheMostMemorableFactsFirst()
    {
        var state = Base(31500UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) };
        state = WithCareer(state, 4, RunCareer.None with { Matches = 9, Goals = 5, Assists = 2, TacklesWon = 6, InjuriesCaused = 1, DeathsCaused = 1 });
        var line = Build(state).Mvp!.Line;

        int death = line.IndexOf("1 muerte ajena", StringComparison.Ordinal);
        int injury = line.IndexOf("1 lesión ajena", StringComparison.Ordinal);
        int goals = line.IndexOf("5 goles", StringComparison.Ordinal);
        Assert.True(death >= 0 && death < injury && injury < goals, line);
        Assert.DoesNotContain("asistencias", line);
    }

    /// <summary>Una plantilla que usa un marcador que la Gaceta no da es un error de datos al cargar, no un hueco en pantalla.</summary>
    [Fact]
    public void ATemplateWithAnUnknownPlaceholderIsRejectedAtLoad()
    {
        var files = TestData.LoadAllFiles();
        files["gazette/gazette.json"] = files["gazette/gazette.json"].Replace("Las causas de su muerte se han perdido", "Las causas de {zzz} se han perdido", StringComparison.Ordinal);

        var error = Assert.Throws<DataException>(() => GazetteLoader.FromJson(files));
        Assert.Contains("zzz", error.Message);
    }

    /// <summary>Y si un catálogo montado a mano se cuela con uno, la vista falla en vez de borrarlo en silencio.</summary>
    [Fact]
    public void ViewFailsLoudlyOnAnUnknownPlaceholderInsteadOfDroppingIt()
    {
        var es = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (string key in GazetteCatalog.RequiredKeys)
        {
            es[key] = Systems.Gazette.Variants(key, "es");
        }

        es["masthead"] = new[] { "EL {zzz}" };
        var broken = new GazetteCatalog(es, es);
        var state = Base(31600UL) with { Result = new RunOutcome(RunOutcomeKind.Victory) };

        Assert.Throws<InvalidOperationException>(() => GazetteView.Build(state, Catalog, Systems.Nicknames, Systems.Rivals, broken));
    }

    // ------------------------------------------------------------------ plurales

    private static RunState WithDead(RunState state, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            state = WithCareer(state, i, RunCareer.None with { Matches = 2 }, PhysicalState.Dead);
        }

        return state;
    }

    private static RunState WithVillain(RunState state, int killed, int hurt)
    {
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in state.Counters)
        {
            counters[key] = value;
        }

        string clan = Systems.Rivals.All[0].Id;
        for (int i = 0; i < killed; i++)
        {
            counters[$"{RunState.RivalCreditPrefix}{clan}:1:{state.Roster[1 + i].Id}:sufferedDeath"] = 1;
        }

        for (int i = 0; i < hurt; i++)
        {
            counters[$"{RunState.RivalCreditPrefix}{clan}:1:{state.Roster[6 + i].Id}:sufferedInjury"] = 1;
        }

        return state with { Counters = counters };
    }

    /// <summary>
    /// Ninguna frase sale con «1 bajas», «1 lápidas» o «0 lesionados»: los plurales se eligen por la cifra
    /// dentro de la propia plantilla (<c>{deaths|# baja|# bajas}</c>), con forma para el cero donde hace falta.
    /// </summary>
    [Fact]
    public void NoSentenceSaysOneBajasOrZeroLesionados()
    {
        var wrong = new System.Text.RegularExpressions.Regex(@"(?<![\d])1 \w*s\b|(?<![\d])0 ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var outcomes = new[]
        {
            new RunOutcome(RunOutcomeKind.Victory),
            new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5),
            new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.NotEnoughPlayers, 5),
        };

        foreach (var outcome in outcomes)
        {
            for (int dead = 0; dead <= 3; dead++)
            {
                for (int killed = 0; killed <= 3; killed++)
                {
                    for (int hurt = 0; hurt <= 2; hurt++)
                    {
                        if (killed > dead || (killed == 0 && hurt == 0))
                        {
                            continue;
                        }

                        for (ulong seed = 31700; seed < 31708; seed++)
                        {
                            var state = WithVillain(WithDead(Base(seed), dead), killed, hurt) with { Result = outcome };
                            foreach (string language in Languages)
                            {
                                var report = Build(state, language);
                                var texts = new List<string> { report.Headline, report.Lede };
                                texts.Add(report.Villain!.Line);
                                Assert.All(texts, text => Assert.False(wrong.IsMatch(text), text));
                                Assert.Equal(killed, report.Villain.Deaths);
                                Assert.Equal(hurt, report.Villain.Injuries);
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ThePluralMarkerPicksTheZeroOneAndManyForms()
    {
        var es = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (string key in GazetteCatalog.RequiredKeys)
        {
            es[key] = Systems.Gazette.Variants(key, "es");
        }

        es["masthead"] = new[] { "{deaths|ninguna baja|# baja|# bajas}/{deaths|# baja|# bajas}" };
        var catalog = new GazetteCatalog(es, es);
        var expected = new[] { "ninguna baja/0 bajas", "1 baja/1 baja", "2 bajas/2 bajas" };
        for (int dead = 0; dead <= 2; dead++)
        {
            var state = WithDead(Base(31800UL), dead) with { Result = new RunOutcome(RunOutcomeKind.Victory) };
            Assert.Equal(expected[dead], GazetteView.Build(state, Catalog, Systems.Nicknames, Systems.Rivals, catalog).Masthead);
        }
    }

    [Theory]
    [InlineData("{deaths|solo una}")]
    [InlineData("{deaths|a|b|c|d}")]
    [InlineData("{zzz|a|b}")]
    public void MalformedPluralMarkersAreRejectedAtLoad(string template)
    {
        var files = TestData.LoadAllFiles();
        files["gazette/gazette.json"] = files["gazette/gazette.json"].Replace("EL SILBATO NEGRO", template, StringComparison.Ordinal);

        Assert.Throws<DataException>(() => GazetteLoader.FromJson(files));
    }
}
