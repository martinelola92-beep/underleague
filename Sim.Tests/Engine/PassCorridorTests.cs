using Underleague.Sim.Engine;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// AZ-B pasos 2 y 3 (docs/plan-pases-trayectoria.md): el factor de proximidad de la intercepción y el
/// peligro del pasillo, en aritmética entera exacta sobre las funciones puras.
/// </summary>
public sealed class PassCorridorTests
{
    private const int Radius = 90;
    private const int Contact = 600;

    [Fact]
    public void TheEdgeOfTheRadiusBehavesExactlyAsBefore()
    {
        // En el borde exacto del radio el factor es 100; un centímetro dentro ya sube linealmente (108).
        Assert.Equal(100, Utility.ProximityFactorPercent(90, 30, Radius, Contact));
        Assert.Equal(108, Utility.ProximityFactorPercent(89, 30, Radius, Contact));
    }

    [Fact]
    public void InsideTheBodyTheContactFactorApplies()
    {
        Assert.Equal(Contact, Utility.ProximityFactorPercent(30, 30, Radius, Contact));
        Assert.Equal(Contact, Utility.ProximityFactorPercent(0, 30, Radius, Contact));
    }

    [Fact]
    public void TheFactorIsLinearBetweenBodyAndRadius()
    {
        // A mitad de camino entre el cuerpo (30) y el radio (90): 100 + 500 × 30 / 60 = 350.
        Assert.Equal(350, Utility.ProximityFactorPercent(60, 30, Radius, Contact));
    }

    [Fact]
    public void TheContactFactorUsesTheRaceBodyRadius()
    {
        // A 0,33 casillas el orco (bodyRadius 38) ya está en contacto y el no-muerto (28) todavía no.
        Assert.Equal(Contact, Utility.ProximityFactorPercent(33, 38, Radius, Contact));
        Assert.True(Utility.ProximityFactorPercent(33, 28, Radius, Contact) < Contact);
    }

    [Fact]
    public void LaneDangerIsExactAtTheBoundaries()
    {
        // Radio 0,6 (60 centi), cuerpo 30: 0 en el radio, 100 dentro del cuerpo, 50 a mitad de camino.
        Assert.Equal(0, Utility.LaneDangerPercent(60, 30, 60));
        Assert.Equal(100, Utility.LaneDangerPercent(30, 30, 60));
        Assert.Equal(100, Utility.LaneDangerPercent(0, 30, 60));
        Assert.Equal(50, Utility.LaneDangerPercent(45, 30, 60));
    }
}
