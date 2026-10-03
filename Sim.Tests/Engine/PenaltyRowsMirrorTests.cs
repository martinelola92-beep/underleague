using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0185 (gameplay-debug de la asimetría de penaltis entre filas 1 y 5 con el arranque): ¿la asimetría es del motor
/// (un signo o un redondeo que sesga hacia un lado) o de la plantilla (qué jugador ocupa cada lado)? Se mide con la
/// alineación tal cual y con la alineación reflejada (fila r → 6 − r): si la asimetría sigue a la plantilla, se invierte;
/// si es del motor, se queda del mismo lado. Con y sin arranque.
/// </summary>
public sealed class PenaltyRowsMirrorTests
{
    private const ulong BaseSeed = 777UL;
    private const int Matches = 4500;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public PenaltyRowsMirrorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    [Trait("Category", "Diagnostic")]
    public void PenaltyRowsWithAndWithoutMirror()
    {
        foreach (int accel in new[] { 0, 3 })
        {
            var catalog = Catalog with { Tuning = Catalog.Tuning with { Movement = Catalog.Tuning.Movement with { AccelTicks = accel } } };
            foreach (bool mirror in new[] { false, true })
            {
                var setup = TestMatches.Reference(catalog, BaseSeed);
                if (mirror)
                {
                    setup = setup with { Home = Mirror(setup.Home), Away = Mirror(setup.Away) };
                }

                var foulRows = new int[7];
                for (int i = 0; i < Matches; i++)
                {
                    var result = Simulator.Run(setup, RngStreams.MatchSeed(BaseSeed, i), catalog, new SimConfig(CollectLog: false));
                    foreach (var e in result.Events)
                    {
                        if (e.Type == EventType.Foul && e.Detail == "foul")
                        {
                            foulRows[e.Cell.Row]++;
                        }
                    }
                }

                _output.WriteLine($"accel {accel} {(mirror ? "reflejada" : "tal cual ")}: faltas pitadas por fila [{string.Join(", ", foulRows)}]");
            }
        }
    }

    private static TeamSetup Mirror(TeamSetup team) =>
        team with
        {
            Lineup = new Lineup(team.Lineup.Slots.Select(s => s with { HomeCell = new Cell(s.HomeCell.Column, Pitch.Rows - 1 - s.HomeCell.Row) }).ToList()),
        };
}
