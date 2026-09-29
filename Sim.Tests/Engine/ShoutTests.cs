using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Consumables;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0166, RF-082/RF-084: los gritos del entrenador cambian la conducta del equipo (su orden o su
/// consigna de presión) durante unos segundos y después vuelven a la orden que tenía. Se aplican por la
/// misma vía que la orden táctica en vivo (ADR 0154/0156).
/// </summary>
public sealed class ShoutTests
{
    private const int Tps = 15;
    private const int T = 300;

    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly ConsumableCatalog Consumables = ConsumableLoader.FromJson(TestData.LoadAllFiles());

    private readonly ITestOutputHelper _output;

    public ShoutTests(ITestOutputHelper output) => _output = output;

    private static MatchConsumable Shout(string id, int tick)
    {
        var definition = Consumables.Find(id) ?? throw new InvalidOperationException(id);
        return new MatchConsumable(definition.Id, definition.Rarity, definition.Effects, ConsumableTrigger.Manual) { ManualTick = tick };
    }

    private static MatchSetup With(ulong seed, string? shout, int tick, IReadOnlyList<OrderChange>? changes = null, Mentality order = Mentality.Neutral)
    {
        var setup = TestMatches.Reference(Catalog, seed);
        return setup with
        {
            Home = setup.Home with
            {
                Order = order,
                OrderChanges = changes ?? Array.Empty<OrderChange>(),
                Consumables = shout is null ? Array.Empty<MatchConsumable>() : new[] { Shout(shout, tick) },
            },
        };
    }

    /// <summary>Orden efectiva y consigna del equipo local en cada tick del partido.</summary>
    private static (List<Mentality> Order, List<Mentality> Base, List<bool> Press) Trace(MatchSetup setup, ulong seed)
    {
        var order = new List<Mentality>();
        var basis = new List<Mentality>();
        var press = new List<bool>();
        var engine = new MatchEngine(setup, seed, Catalog, SimConfig.Default)
        {
            AfterStepForTest = e =>
            {
                order.Add(e.EffectiveOrder(0));
                basis.Add(e.BaseOrder(0));
                press.Add(e.PressActive(0));
            },
        };
        engine.Run();
        return (order, basis, press);
    }

    // AfterStepForTest se invoca tras el tick i+1 (el primer Step sube _tick a 1): el índice i es el tick i+1.
    private static Mentality At(List<Mentality> list, int tick) => list[tick - 1];

    [Fact]
    public void TheThreeShoutsLoadAsShoutsWithoutInvisibleMultipliers()
    {
        var hold = Assert.Single(Consumables.Find("hold_the_line")!.Effects);
        Assert.Equal(EffectType.Shout, hold.Type);
        Assert.Equal(ShoutKind.Defensive, hold.Shout);
        Assert.Equal(10, hold.Value);

        var push = Assert.Single(Consumables.Find("push_forward")!.Effects);
        Assert.Equal(ShoutKind.Offensive, push.Shout);
        Assert.Equal(10, push.Value);

        var afterHim = Assert.Single(Consumables.Find("after_him")!.Effects);
        Assert.Equal(ShoutKind.Press, afterHim.Shout);
        Assert.Equal(6, afterHim.Value);
    }

    [Fact]
    public void TheDescriptionComesFromTheEffectInBothLanguages()
    {
        foreach (string language in new[] { "es", "en" })
        {
            var templates = Catalog.Localization.Get(language);
            foreach (string id in new[] { "hold_the_line", "push_forward", "after_him" })
            {
                string text = DescriptionGenerator.DescribeEffects(Consumables.Find(id)!.Effects, templates);
                int seconds = id == "after_him" ? 6 : 10;
                Assert.Contains($"{seconds} s", text);
                Assert.DoesNotContain("{", text);
            }
        }
    }

    [Theory]
    [InlineData("hold_the_line", Mentality.Defensive)]
    [InlineData("push_forward", Mentality.Offensive)]
    public void DuringTheWindowTheEffectiveOrderIsTheShoutsAndAfterwardsItGoesBack(string id, Mentality shouted)
    {
        var (order, basis, press) = Trace(With(1, id, T), 1);
        int end = T + (10 * Tps);

        Assert.True(order.Count > end + 20, "el partido tiene que durar más que el grito");
        for (int tick = 1; tick < T; tick++)
        {
            Assert.Equal(Mentality.Neutral, At(order, tick));
        }

        for (int tick = T; tick < end; tick++)
        {
            Assert.Equal(shouted, At(order, tick));
        }

        for (int tick = end; tick <= order.Count; tick++)
        {
            Assert.Equal(Mentality.Neutral, At(order, tick));
        }

        // La orden del jugador no se toca nunca, y una orden no enciende la consigna de presión.
        Assert.All(basis, m => Assert.Equal(Mentality.Neutral, m));
        Assert.All(press, p => Assert.False(p));
    }

    [Fact]
    public void ThePressShoutTurnsOnThePressureForSixSecondsAndLeavesTheOrderAlone()
    {
        var (order, _, press) = Trace(With(1, "after_him", T), 1);
        int end = T + (6 * Tps);

        for (int tick = 1; tick <= order.Count; tick++)
        {
            Assert.Equal(tick >= T && tick < end, press[tick - 1]);
            Assert.Equal(Mentality.Neutral, At(order, tick));
        }
    }

    [Fact]
    public void ItGoesBackToTheOrderTheTeamHadNotToNeutral()
    {
        var (order, _, _) = Trace(With(1, "push_forward", T, order: Mentality.Defensive), 1);
        int end = T + (10 * Tps);
        Assert.Equal(Mentality.Defensive, At(order, T - 1));
        Assert.Equal(Mentality.Offensive, At(order, T));
        Assert.Equal(Mentality.Offensive, At(order, end - 1));
        Assert.Equal(Mentality.Defensive, At(order, end));
    }

    /// <summary>
    /// Interacción con la botonera (ADR 0166): la orden que el jugador pone durante el grito no se pierde ni
    /// corta el grito; cambia la orden de BASE, y es a ella a la que se vuelve cuando el grito acaba.
    /// </summary>
    [Fact]
    public void AnOrderChangeDuringTheShoutWaitsForItAndIsWhatTheTeamGoesBackTo()
    {
        int change = T + (4 * Tps);
        int end = T + (10 * Tps);
        var (order, basis, _) = Trace(With(1, "hold_the_line", T, new[] { new OrderChange(change, Mentality.Offensive) }), 1);

        Assert.Equal(Mentality.Defensive, At(order, change - 1));
        Assert.Equal(Mentality.Defensive, At(order, change)); // el grito sigue mandando mientras dura
        Assert.Equal(Mentality.Defensive, At(order, end - 1));
        Assert.Equal(Mentality.Offensive, At(order, end)); // al acabar vuelve a la orden que el jugador puso entretanto
        Assert.Equal(Mentality.Neutral, At(basis, change - 1));
        Assert.Equal(Mentality.Offensive, At(basis, change));
    }

    [Fact]
    public void AnOrderChangeBeforeTheShoutIsWhatTheTeamGoesBackTo()
    {
        int end = T + (10 * Tps);
        var (order, _, _) = Trace(With(1, "hold_the_line", T, new[] { new OrderChange(T - 50, Mentality.Offensive) }), 1);
        Assert.Equal(Mentality.Offensive, At(order, T - 1));
        Assert.Equal(Mentality.Defensive, At(order, T));
        Assert.Equal(Mentality.Offensive, At(order, end));
    }

    /// <summary>
    /// Lo que enseña la pantalla (<c>MatchShoutView</c>, que sólo lee los eventos) coincide tick a tick con
    /// lo que hace el motor: orden efectiva, consigna de presión y cuenta atrás.
    /// </summary>
    [Theory]
    [InlineData("hold_the_line", 10)]
    [InlineData("push_forward", 10)]
    [InlineData("after_him", 6)]
    public void TheViewShowsExactlyWhatTheEngineDoes(string id, int seconds)
    {
        var setup = With(2, id, T, new[] { new OrderChange(T + 20, Mentality.Defensive) });
        var (order, _, press) = Trace(setup, 2);
        var events = Simulator.Run(setup, 2, Catalog, SimConfig.Default).Events;
        var equipped = setup.Home.Consumables;

        for (int tick = 1; tick <= order.Count; tick++)
        {
            var shouts = Underleague.Sim.Run.View.MatchShoutView.ActiveAt(events, equipped, 0, tick);
            var playerOrder = tick >= T + 20 ? Mentality.Defensive : Mentality.Neutral;
            Assert.Equal(At(order, tick), Underleague.Sim.Run.View.MatchShoutView.EffectiveOrder(shouts, playerOrder));
            Assert.Equal(press[tick - 1], shouts.Any(s => s.Kind == ShoutKind.Press));
        }

        var atStart = Underleague.Sim.Run.View.MatchShoutView.ActiveAt(events, equipped, 0, T);
        Assert.Equal(seconds, Assert.Single(atStart).SecondsLeft);
        Assert.Empty(Underleague.Sim.Run.View.MatchShoutView.ActiveAt(events, equipped, 0, T - 1));
        Assert.Empty(Underleague.Sim.Run.View.MatchShoutView.ActiveAt(events, equipped, 0, T + (seconds * Tps)));
        Assert.Empty(Underleague.Sim.Run.View.MatchShoutView.ActiveAt(events, equipped, 1, T));
    }

    [Theory]
    [InlineData("hold_the_line")]
    [InlineData("push_forward")]
    [InlineData("after_him")]
    public void ShoutingAtTickTLeavesEverythingBeforeTUntouchedAndIsDeterministic(string id)
    {
        int diverged = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var plain = Simulator.Run(With(seed, null, T), seed, Catalog, SimConfig.Default);
            var shouted = Simulator.Run(With(seed, id, T), seed, Catalog, SimConfig.Default);
            var again = Simulator.Run(With(seed, id, T), seed, Catalog, SimConfig.Default);

            Assert.Equal(
                plain.Events.Where(e => e.Tick < T).Select(Describe).ToList(),
                shouted.Events.Where(e => e.Tick < T).Select(Describe).ToList());
            Assert.Equal(shouted.Events.Select(Describe).ToList(), again.Events.Select(Describe).ToList());

            var used = shouted.Events.Where(e => e.Type == EventType.ConsumableUsed).ToList();
            Assert.Single(used);
            Assert.Equal(T, used[0].Tick);

            if (!plain.Events.Select(Describe).SequenceEqual(shouted.Events.Select(Describe)))
            {
                diverged++;
            }
        }

        Assert.True(diverged >= 8, $"sólo {diverged} de 12 partidos cambiaron con el grito {id}");
    }

    /// <summary>
    /// Medición del cambio (ADR 0166): los mismos partidos con y sin grito en el mismo tick. Los goles en la
    /// ventana son muy pocos (~0,1 por partido) y su ruido tapa un efecto de este tamaño, así que la puerta
    /// comprueba la CONDUCTA que promete cada grito (entradas con la presión, tiros con la orden) y los goles
    /// se escriben en la salida para la ADR, sin afirmar sobre ellos.
    /// </summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void EachShoutMovesTheBehaviourInTheWindowTheWayItsNamePromises()
    {
        int n = GateScale.Of(1200);
        var baseline = Measure(null, n);
        var hold = Measure("hold_the_line", n);
        var push = Measure("push_forward", n);
        var press = Measure("after_him", n);

        int w10 = 10 * Tps;
        int w6 = 6 * Tps;
        var b10 = Summarize(baseline, w10);
        var b6 = Summarize(baseline, w6);
        var h = Summarize(hold, w10);
        var p = Summarize(push, w10);
        var q = Summarize(press, w6);

        _output.WriteLine($"{n} partidos, mismas semillas; por partido, equipo local; ventana = desde el tick {T}");
        _output.WriteLine($"{"grito",-14} | {"goles +/-",-13} | {"tiros",-6} {"entradas",-8} | después: goles +/-");
        foreach (var (name, m) in new[] { ("ninguno 10 s", b10), ("hold_the_line", h), ("push_forward", p), ("ninguno 6 s", b6), ("after_him", q) })
        {
            _output.WriteLine($"{name,-14} | {m.WindowFor:0.000}/{m.WindowAgainst:0.000} | {m.Shots,-6:0.000} {m.Tackles,-8:0.000} | {m.AfterFor:0.000}/{m.AfterAgainst:0.000}");
        }

        Assert.True(q.Tackles > b6.Tackles, "¡A por él! no aumenta las entradas del equipo en la ventana");
        Assert.True(p.Shots > h.Shots, "¡Arriba! no tira más que ¡Aguantad! en la ventana");
    }

    private sealed record Match(int[] GoalTicks, int[] GoalTeams, int[] ShotTicks, int[] TackleTicks);

    private sealed record Summary(double WindowFor, double WindowAgainst, double AfterFor, double AfterAgainst, double Shots, double Tackles);

    private static Match[] Measure(string? id, int n)
    {
        var rows = new Match[n];
        Parallel.For(0, n, i =>
        {
            var catalog = TestData.LoadCatalog();
            var consumables = ConsumableLoader.FromJson(TestData.LoadAllFiles());
            ulong seed = (ulong)(i + 1);
            var setup = TestMatches.Reference(catalog, seed);
            if (id is not null)
            {
                var definition = consumables.Find(id)!;
                setup = setup with
                {
                    Home = setup.Home with
                    {
                        Consumables = new[] { new MatchConsumable(definition.Id, definition.Rarity, definition.Effects, ConsumableTrigger.Manual) { ManualTick = T } },
                    },
                };
            }

            var events = Simulator.Run(setup, seed, catalog, SimConfig.Default).Events;
            var goals = events.Where(e => e.Type == EventType.Goal).ToList();
            rows[i] = new Match(
                goals.Select(e => e.Tick).ToArray(),
                goals.Select(e => e.Team).ToArray(),
                events.Where(e => e.Type == EventType.Shot && e.Team == 0).Select(e => e.Tick).ToArray(),
                events.Where(e => e.Type == EventType.Tackle && e.Team == 0).Select(e => e.Tick).ToArray());
        });

        return rows;
    }

    private static Summary Summarize(Match[] rows, int window)
    {
        bool InWindow(int tick) => tick >= T && tick < T + window;
        bool After(int tick) => tick >= T + window;
        double PerMatch(Func<Match, int> count) => rows.Average(count);

        return new Summary(
            PerMatch(m => m.GoalTicks.Where((t, k) => m.GoalTeams[k] == 0 && InWindow(t)).Count()),
            PerMatch(m => m.GoalTicks.Where((t, k) => m.GoalTeams[k] != 0 && InWindow(t)).Count()),
            PerMatch(m => m.GoalTicks.Where((t, k) => m.GoalTeams[k] == 0 && After(t)).Count()),
            PerMatch(m => m.GoalTicks.Where((t, k) => m.GoalTeams[k] != 0 && After(t)).Count()),
            PerMatch(m => m.ShotTicks.Count(InWindow)),
            PerMatch(m => m.TackleTicks.Count(InWindow)));
    }

    private static string Describe(MatchEvent e) => $"{e.Tick}:{e.Type}:{e.Detail}:{e.Actor}";
}
