using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0154: la orden táctica se puede cambiar durante el partido, y se nota en el marcador —defensivo
/// encaja menos, ofensivo marca más— porque además de cambiar qué acción elige cada uno (ADR 0140) mueve
/// la altura de las líneas (<c>ai.mentalityShift</c>).
/// </summary>
public sealed class MatchOrderTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public MatchOrderTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Un cambio de orden en el tick T no altera nada anterior a T: es lo que permite a la pantalla
    /// reanudar el partido desde donde el jugador pulsó el botón, como con las sustituciones (ADR 0094).
    /// </summary>
    [Fact]
    public void AnOrderChangeAtTickTLeavesEverythingBeforeTUntouched()
    {
        const int T = 600;
        int diverged = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var plain = Simulator.Run(setup, seed, Catalog, SimConfig.Default);
            var changed = Simulator.Run(
                setup with { Home = setup.Home with { OrderChanges = new[] { new OrderChange(T, Mentality.Offensive) } } },
                seed, Catalog, SimConfig.Default);

            var before = plain.Events.Where(e => e.Tick < T).Select(Describe).ToList();
            var beforeChanged = changed.Events.Where(e => e.Tick < T).Select(Describe).ToList();
            Assert.Equal(before, beforeChanged);

            if (!plain.Events.Select(Describe).SequenceEqual(changed.Events.Select(Describe)))
            {
                diverged++;
            }
        }

        // Y el cambio llega al campo: con la orden ofensiva desde el minuto ~45 el partido es otro.
        Assert.True(diverged >= 15, $"sólo {diverged} de 20 partidos cambiaron tras el cambio de orden");
    }

    /// <summary>
    /// La promesa de la orden, medida con las mismas semillas contra un rival neutro. MEDIDO al calibrar
    /// (1.200 partidos, 27 sep 2026, ADR 0156 reajustada con la 0155): neutro 0,96 a favor y 0,91 en contra;
    /// defensivo 0,68 / 0,73; ofensivo 1,12 / 0,94. Defensivo encaja menos y ofensivo marca más, y cada una paga algo por ello.
    /// </summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void DefensiveConcedesLessAndOffensiveScoresMore()
    {
        int N = GateScale.Of(1200);
        var neutral = Measure(Mentality.Neutral, N);
        var defensive = Measure(Mentality.Defensive, N);
        var offensive = Measure(Mentality.Offensive, N);

        _output.WriteLine($"neutro {neutral.For:0.000}/{neutral.Against:0.000} · defensivo {defensive.For:0.000}/{defensive.Against:0.000} · ofensivo {offensive.For:0.000}/{offensive.Against:0.000}");
        Assert.True(defensive.Against < neutral.Against - 0.04, "defensivo no encaja claramente menos que neutro");
        Assert.True(offensive.For > neutral.For + 0.06, "ofensivo no marca claramente más que neutro");
        Assert.True(defensive.For < neutral.For, "defensivo tiene que costar ataque");
        Assert.True(offensive.Against > neutral.Against, "ofensivo tiene que costar defensa");
    }

    private static (double For, double Against) Measure(Mentality order, int n)
    {
        var goals = new (int For, int Against)[n];
        Parallel.For(0, n, i =>
        {
            var catalog = TestData.LoadCatalog();
            ulong seed = (ulong)(i + 1);
            var setup = TestMatches.Reference(catalog, seed);
            var result = Simulator.Run(setup with { Home = setup.Home with { Order = order } }, seed, catalog, SimConfig.Default);
            goals[i] = (result.Report.Goals[0], result.Report.Goals[1]);
        });

        return (goals.Average(g => g.For), goals.Average(g => g.Against));
    }

    private static string Describe(MatchEvent e) => $"{e.Tick}:{e.Type}:{e.Detail}:{e.Actor}";
}
