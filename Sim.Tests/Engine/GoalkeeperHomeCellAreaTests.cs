using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// La casilla-hogar del portero debe caer dentro del área centrada (ADR 0121): filas cuyo centro
/// (fila + 0,5) está en la banda [<see cref="Pitch.AreaTop"/>, <see cref="Pitch.AreaBottom"/>], es decir
/// 1..5 con los valores actuales de <see cref="Pitch.AreaRows"/>. Antes de la ADR la banda estaba pegada
/// a la fila de arriba y la fila 5 era ilegal mientras que la 0 seguía siendo ilegal por el otro lado.
/// </summary>
public sealed class GoalkeeperHomeCellAreaTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const ulong Seed = 12345UL;

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void AcceptsTheSymmetricEdgeRows(int row)
    {
        var setup = WithGoalkeeperRow(row);

        // No debe lanzar: el centro de la fila cae dentro de la banda en ambos bordes.
        Simulator.Run(setup, Seed, Catalog, new SimConfig(CollectLog: false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void RejectsTheRowsJustOutsideTheBand(int row)
    {
        var setup = WithGoalkeeperRow(row);

        var ex = Assert.Throws<ArgumentException>(
            () => Simulator.Run(setup, Seed, Catalog, new SimConfig(CollectLog: false)));
        Assert.Contains("alinea al portero", ex.Message);
    }

    /// <summary>Reconstruye el partido de referencia con la casilla-hogar del portero local en la fila dada.</summary>
    private static MatchSetup WithGoalkeeperRow(int row)
    {
        var reference = TestMatches.Reference(Catalog, Seed);
        var home = reference.Home;
        var goalkeeperId = home.Players.First(p => p.Position == Position.Goalkeeper).Id;
        var newSlots = home.Lineup.Slots
            .Select(slot => slot.PlayerId == goalkeeperId ? slot with { HomeCell = new Cell(slot.HomeCell.Column, row) } : slot)
            .ToList();
        var newHome = home with { Lineup = new Lineup(newSlots) };
        return reference with { Home = newHome };
    }
}
