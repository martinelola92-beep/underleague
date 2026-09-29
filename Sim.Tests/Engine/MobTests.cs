using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
using Underleague.Sim.Run.Systems.Mobs;
using Underleague.Sim.Run.View;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0167: la turba tiene tipo, y se puede provocar. Cada tipo hace al entrar la turba lo que anuncia y nada
/// antes; la lesión de la turba no tiene autor, no toca al portero y no mata; sin tipo el partido es el de antes;
/// «Provocar a la grada» aplica el tipo en su tick; la pantalla enseña lo mismo que hace el motor.
/// </summary>
public sealed class MobTests
{
    private const int T = 300;

    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly ConsumableCatalog Consumables = ConsumableLoader.FromJson(TestData.LoadAllFiles());
    private static readonly MobCatalog Mobs = MobLoader.FromJson(TestData.LoadAllFiles());

    /// <summary>Semillas de <c>TestMatches.Reference</c> que llegan a la turba sin tipo, calculadas una vez.</summary>
    private static readonly Lazy<IReadOnlyList<ulong>> Drawn = new(() =>
    {
        var seeds = new List<ulong>();
        for (ulong seed = 1; seed <= 400 && seeds.Count < 8; seed++)
        {
            if (Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default).Report.WentToGoldenGoal)
            {
                seeds.Add(seed);
            }
        }

        return seeds;
    });

    private static MobSetup Mob(string id) => (Mobs.Find(id) ?? throw new InvalidOperationException(id)).ToSetup();

    private static MatchSetup With(ulong seed, MobSetup? mob, MatchConsumable? home = null, MatchConsumable? away = null)
    {
        var setup = TestMatches.Reference(Catalog, seed);
        return setup with
        {
            Mob = mob,
            Home = setup.Home with { Consumables = home is null ? Array.Empty<MatchConsumable>() : new[] { home } },
            Away = setup.Away with { Consumables = away is null ? Array.Empty<MatchConsumable>() : new[] { away } },
        };
    }

    private static MatchConsumable Rile(int tick)
    {
        var definition = Consumables.Find("rile_the_crowd") ?? throw new InvalidOperationException("rile_the_crowd");
        return new MatchConsumable(definition.Id, definition.Rarity, definition.Effects, ConsumableTrigger.Manual) { ManualTick = tick };
    }

    private static IReadOnlyList<MatchEvent> Play(MatchSetup setup, ulong seed) =>
        Simulator.Run(setup, seed, Catalog, SimConfig.Default).Events;

    private static int MobTick(IReadOnlyList<MatchEvent> events) => events.First(e => e.Type == EventType.MobStart).Tick;

    /// <summary>Estado de conducta de los dos equipos tras cada tick (índice i = tick i+1).</summary>
    private static List<(Mentality Home, Mentality Away, bool PressHome, bool PressAway)> Trace(MatchSetup setup, ulong seed)
    {
        var trace = new List<(Mentality, Mentality, bool, bool)>();
        var engine = new MatchEngine(setup, seed, Catalog, SimConfig.Default)
        {
            AfterStepForTest = e => trace.Add((e.EffectiveOrder(0), e.EffectiveOrder(1), e.PressActive(0), e.PressActive(1))),
        };
        engine.Run();
        return trace;
    }

    // ------------------------------------------------------------------ datos y sorteo

    [Fact]
    public void TheCatalogHasTheFourAnnouncedTypes()
    {
        Assert.Equal(new[] { "plain", "invader", "frenzy", "their_roar" }, Mobs.Types.Select(t => t.Id).ToArray());
        Assert.Empty(Mobs.Find("plain")!.Effects);
        Assert.Equal(new[] { MobEffectKind.Injure }, Mobs.Find("invader")!.Effects);
    }

    [Fact]
    public void TheTypeIsDrawnFromTheNodeWithTheRunStreamAndEveryTypeAppears()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int id = 1; id <= 200; id++)
        {
            var node = new MapNode(id, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), "act1_orc_ironclad", 1);
            var a = Mobs.For(20260929UL, node)!;
            Assert.Same(a, Mobs.For(20260929UL, node));
            seen.Add(a.Id);
        }

        Assert.Equal(4, seen.Count);
        var market = new MapNode(1, 1, 0, 0, NodeKind.Market, Array.Empty<int>(), string.Empty, 0);
        Assert.Null(Mobs.For(1UL, market));
    }

    [Fact]
    public void TheRunEngineHandsTheNodesTypeToTheMatch()
    {
        var systems = SystemsTestSupport.Systems;
        var state = RunEngine.Start(Underleague.Sim.Tests.Run.TestRuns.Setup(), 7UL, Catalog);
        var node = RunEngine.AvailableNodes(state).First(n => n.IsMatch);

        var (setup, _, _) = RunEngine.BuildMatch(state, node.Id, Catalog, systems);

        Assert.NotNull(setup.Mob);
        Assert.Equal(systems.Mobs.For(state.Seed, node)!.Id, setup.Mob!.Id);
    }

    // ------------------------------------------------------------------ al entrar la turba

    [Fact]
    public void WithoutATypeOrWithPlainTheMatchIsExactlyTheOneBefore()
    {
        foreach (var seed in Drawn.Value)
        {
            var before = Play(With(seed, null), seed);
            Assert.Equal(before, Play(With(seed, Mob("plain")), seed));
        }
    }

    [Fact]
    public void AnInvaderInjuresExactlyOneOutfieldPlayerWithoutAuthorWhenTheMobStartsAndNeverKills()
    {
        Assert.NotEmpty(Drawn.Value);
        foreach (var seed in Drawn.Value)
        {
            var before = Play(With(seed, null), seed);
            var events = Play(With(seed, Mob("invader")), seed);
            int mobTick = MobTick(events);

            // Idéntico hasta la entrada de la turba.
            int prefix = before.ToList().FindIndex(e => e.Type == EventType.MobStart);
            Assert.Equal(before.Take(prefix + 1), events.Take(prefix + 1));

            var injuries = events.Where(e => e.Tick == mobTick && e.Type == EventType.Injury).ToList();
            var injury = Assert.Single(injuries);
            Assert.Equal("minor", injury.Detail);
            Assert.Equal(-1, injury.Opponent);
            var setup = TestMatches.Reference(Catalog, seed);
            var victim = setup.Home.Players.Concat(setup.Away.Players).Single(p => p.Id == injury.Actor);
            Assert.NotEqual(Position.Goalkeeper, victim.Position);
            Assert.DoesNotContain(events, e => e.Type == EventType.Death && e.Tick == mobTick);
        }
    }

    [Fact]
    public void FrenzyMakesBothTeamsPressFromTheMobToTheEndAndNotBefore()
    {
        var seed = Drawn.Value[0];
        var events = Play(With(seed, Mob("frenzy")), seed);
        int mobTick = MobTick(events);
        var trace = Trace(With(seed, Mob("frenzy")), seed);

        for (int tick = 1; tick <= trace.Count; tick++)
        {
            bool expected = tick >= mobTick;
            Assert.Equal(expected, trace[tick - 1].PressHome);
            Assert.Equal(expected, trace[tick - 1].PressAway);
        }
    }

    [Fact]
    public void TheirRoarPutsTheRivalOnTheOffensiveFromTheMobToTheEnd()
    {
        var seed = Drawn.Value[0];
        int mobTick = MobTick(Play(With(seed, Mob("their_roar")), seed));
        var trace = Trace(With(seed, Mob("their_roar")), seed);

        Assert.Equal(Mentality.Neutral, trace[mobTick - 2].Away);
        Assert.Equal(Mentality.Offensive, trace[mobTick - 1].Away);
        Assert.Equal(Mentality.Offensive, trace[^1].Away);
        Assert.Equal(Mentality.Neutral, trace[^1].Home);
    }

    // ------------------------------------------------------------------ provocar a la grada

    [Fact]
    public void RilingTheCrowdAppliesTheTypeAtItsTickAndNothingBefore()
    {
        const ulong Seed = 3;
        var plain = Play(With(Seed, Mob("invader")), Seed);
        var events = Play(With(Seed, Mob("invader"), home: Rile(T)), Seed);

        int prefix = plain.ToList().FindIndex(e => e.Tick >= T);
        Assert.Equal(plain.Take(prefix), events.Take(prefix));
        Assert.Contains(events, e => e.Tick == T && e.Type == EventType.ConsumableUsed && e.Detail == "rile_the_crowd");
        Assert.Single(events, e => e.Tick == T && e.Type == EventType.Injury && e.Opponent == -1);
    }

    [Fact]
    public void RilingAFrenzyMakesBothPressForTenSeconds()
    {
        const ulong Seed = 3;
        var trace = Trace(With(Seed, Mob("frenzy"), home: Rile(T)), Seed);

        Assert.False(trace[T - 2].PressHome);
        Assert.True(trace[T - 1].PressHome && trace[T - 1].PressAway);
        Assert.True(trace[T + 148].PressAway);
        Assert.False(trace[T + 149].PressHome || trace[T + 149].PressAway);
    }

    [Fact]
    public void RilingAPlainCrowdDoesNothing()
    {
        const ulong Seed = 3;
        var none = Play(With(Seed, Mob("plain")), Seed);
        var riled = Play(With(Seed, Mob("plain"), home: Rile(T)), Seed).Where(e => e.Type != EventType.ConsumableUsed);

        Assert.Equal(none, riled);
    }

    /// <summary>
    /// Revisión independiente: con la turba ya dentro, provocarla no hace nada —ni una segunda lesión, ni acortar la
    /// conducta «hasta el final» a 10 s—.
    /// </summary>
    [Fact]
    public void RilingTheCrowdOnceTheMobIsInDoesNothing()
    {
        foreach (var seed in Drawn.Value)
        {
            int mobTick = MobTick(Play(With(seed, Mob("invader")), seed));
            var invader = Play(With(seed, Mob("invader"), home: Rile(mobTick + 5)), seed);
            Assert.Single(invader, e => e.Type == EventType.Injury && e.Opponent == -1);

            var trace = Trace(With(seed, Mob("frenzy"), home: Rile(mobTick + 5)), seed);
            Assert.True(trace[^1].PressHome && trace[^1].PressAway);
        }
    }

    /// <summary>
    /// Revisión independiente: lo que impone la turba es la conducta de base hasta el final. Un grito de presión del
    /// jugador durante el frenesí no la apaga al acabar (antes el equipo dejaba de presionar y el rival no).
    /// </summary>
    [Fact]
    public void APressShoutDuringTheFrenzyGoesBackToTheFrenzyWhenItEnds()
    {
        var definition = Consumables.Find("after_him")!;
        foreach (var seed in Drawn.Value)
        {
            int mobTick = MobTick(Play(With(seed, Mob("frenzy")), seed));
            var shout = new MatchConsumable(definition.Id, definition.Rarity, definition.Effects, ConsumableTrigger.Manual) { ManualTick = mobTick + 3 };
            var setup = With(seed, Mob("frenzy"), home: shout);
            var trace = Trace(setup, seed);
            var events = Play(setup, seed);

            for (int tick = mobTick; tick <= trace.Count; tick++)
            {
                Assert.True(trace[tick - 1].PressHome, $"semilla {seed}, tick {tick}");
                var view = MatchShoutView.ActiveAt(events, setup.Home.Consumables, 0, tick, setup.Mob, setup.Away.Consumables);
                Assert.Contains(view, s => s.Kind == ShoutKind.Press);
            }
        }
    }

    // ------------------------------------------------------------------ lo que se ve

    /// <summary>
    /// El tablero (<c>MatchShoutView</c>) enseña lo que hace el motor tick a tick, para los dos equipos, con la turba
    /// al entrar y con un «Provocar» del rival.
    /// </summary>
    [Theory]
    [InlineData("frenzy")]
    [InlineData("their_roar")]
    public void TheViewShowsWhatTheEngineDoesWithTheMobAndARivalProvocation(string type)
    {
        var seed = Drawn.Value[0];
        var setup = With(seed, Mob(type), away: Rile(T));
        var trace = Trace(setup, seed);
        var events = Play(setup, seed);

        for (int tick = 1; tick <= trace.Count; tick++)
        {
            var home = MatchShoutView.ActiveAt(events, setup.Home.Consumables, 0, tick, setup.Mob, setup.Away.Consumables);
            var away = MatchShoutView.ActiveAt(events, setup.Away.Consumables, 1, tick, setup.Mob, setup.Home.Consumables);
            Assert.Equal(trace[tick - 1].PressHome, home.Any(s => s.Kind == ShoutKind.Press));
            Assert.Equal(trace[tick - 1].PressAway, away.Any(s => s.Kind == ShoutKind.Press));
            Assert.Equal(trace[tick - 1].Away, MatchShoutView.EffectiveOrder(away, Mentality.Neutral));
        }
    }

    /// <summary>RT-035: el anuncio de cada tipo sale de las plantillas de sus efectos, en los dos idiomas.</summary>
    [Fact]
    public void EachTypeIsAnnouncedWithANameAndAGeneratedTextInBothLanguages()
    {
        foreach (var type in Mobs.Types)
        {
            foreach (var language in new[] { "es", "en" })
            {
                var line = MobView.Describe(type, Catalog, language);
                Assert.False(string.IsNullOrWhiteSpace(line.Name));
                Assert.False(string.IsNullOrWhiteSpace(line.Text));
            }
        }

        Assert.Contains("lesiona", MobView.Describe(Mobs.Find("invader")!, Catalog).Text, StringComparison.Ordinal);
        Assert.Contains("injures", MobView.Describe(Mobs.Find("invader")!, Catalog, "en").Text, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ carga

    [Fact]
    public void AnUnknownMobEffectIsAnExplicitError()
    {
        var files = TestData.LoadAllFiles();
        files["mobs/mobs.json"] = files["mobs/mobs.json"].Replace("\"pressBoth\"", "\"setFire\"", StringComparison.Ordinal);
        Assert.Throws<DataException>(() => MobLoader.FromJson(files));
    }
}
