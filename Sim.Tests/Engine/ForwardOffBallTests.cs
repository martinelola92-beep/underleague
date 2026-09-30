using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BF-C (docs/pendientes/BF-C.md, ADR 0179): el delantero sin balón no tenía nada mejor que pegar. Sin balón,
/// <c>Tackle</c> (128 × 165 % = 211) ganaba a su <c>MarkOpponent</c> (120 × 150 % = 180) por 31 puntos, al revés
/// que el centrocampista, cuya marca gana por 104 (ADR 0133). Por eso el delantero no se podía abrir: cualquier
/// ajuste positivo lo saturaba. Ahora su marca (210 × 150 % = 315) gana a su entrada por los mismos 104.
/// </summary>
public sealed class ForwardOffBallTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;

    public ForwardOffBallTests(ITestOutputHelper output) => _output = output;

    private static int OutOfPossession(Position role, PlayerAction action) =>
        Catalog.Ai.Base(role, action) * Catalog.Ai.Tactical(TacticalState.OutOfPossession, action) / 100;

    /// <summary>La marca del delantero le gana a su entrada por el mismo margen que la del centrocampista (ADR 0133).</summary>
    [Fact]
    public void TheForwardMarkBeatsHisTackleByTheMidfieldersMargin()
    {
        int midfielderMargin = OutOfPossession(Position.Midfielder, PlayerAction.MarkOpponent) - OutOfPossession(Position.Midfielder, PlayerAction.Tackle);
        int forwardMargin = OutOfPossession(Position.Forward, PlayerAction.MarkOpponent) - OutOfPossession(Position.Forward, PlayerAction.Tackle);
        _output.WriteLine($"margen marca - entrada: centrocampista {midfielderMargin}, delantero {forwardMargin}");

        Assert.True(midfielderMargin >= 100, "referencia: la marca del centrocampista gana a su entrada por ≥ 100 (ADR 0133)");
        Assert.True(forwardMargin >= midfielderMargin - 2, $"el delantero debía tener el margen del centrocampista ({midfielderMargin}), tiene {forwardMargin}");
    }

    /// <summary>
    /// Abierto el puesto con el ajuste positivo más pequeño, el delantero ya no satura: medido en 600 partidos
    /// de referencia, 0,57 entradas sin balón de delantero por partido antes y 0,15 después.
    /// </summary>
    [Fact]
    public void AnOpenedForwardNoLongerSaturatesTheOffBallTackle()
    {
        int closed = OffBallByForwards(Catalog, 150);
        var table = new int[Enum.GetValues<Position>().Length];
        table[(int)Position.Defender] = Catalog.Ai.OffBallTackleAdjust(Position.Defender);
        table[(int)Position.Midfielder] = Catalog.Ai.OffBallTackleAdjust(Position.Midfielder);
        table[(int)Position.Forward] = 1;
        var opened = Catalog with { Ai = Catalog.Ai.WithOffBallTackle(table) };
        int hits = OffBallByForwards(opened, 150);
        _output.WriteLine($"entradas sin balón de delanteros en 150 partidos: cerrado {closed}, abierto a +1 {hits} ({hits / 150.0:F2} por partido)");

        Assert.Equal(0, closed);
        Assert.True(hits / 150.0 <= 0.30, $"el delantero abierto pegaba {hits / 150.0:F2} veces por partido (antes ≈ 0,57)");
    }

    private static int OffBallByForwards(Catalog catalog, int matches)
    {
        int total = 0;
        for (int m = 1; m <= matches; m++)
        {
            ulong seed = (ulong)m;
            var setup = TestMatches.Reference(catalog, seed);
            var forwards = new HashSet<int>(setup.Home.Players.Concat(setup.Away.Players).Where(p => p.Position == Position.Forward).Select(p => p.Id));
            var result = Simulator.Run(setup, seed, catalog, SimConfig.Default);
            total += result.Events.Count(e => e.Type == EventType.Tackle && e.Detail.StartsWith("offBall", StringComparison.Ordinal) && forwards.Contains(e.Actor));
        }

        return total;
    }
}
