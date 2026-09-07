using Underleague.Sim.Model;

namespace Underleague.Sim.Tests;

/// <summary>
/// Media simple de <see cref="Attributes.Average"/> (AW-M): la "valoración" de la ficha de jugador, sin
/// ponderar por posición.
/// <para>
/// Dividir una suma entera entre cinco nunca da un decimal exacto de ,5 (los únicos restos posibles son
/// ,2/,4/,6/,8), así que no hay un borde de redondeo bancario que probar: los dos casos que sí hacen
/// falta son el redondeo hacia abajo (,4) y hacia arriba (,6) alrededor del mismo entero.
/// </para>
/// </summary>
public sealed class AttributesTests
{
    [Fact]
    public void AverageIsTheSimpleMeanOfTheFiveAttributes()
    {
        var attributes = new Attributes(Strength: 60, Speed: 50, Technique: 40, Stamina: 30, Leash: 20);

        Assert.Equal(40, attributes.Average); // suma 200 / 5 = 40,0
    }

    [Fact]
    public void AverageRoundsDownBelowTheMidpoint()
    {
        var attributes = new Attributes(Strength: 10, Speed: 10, Technique: 10, Stamina: 10, Leash: 12);

        Assert.Equal(10, attributes.Average); // suma 52 / 5 = 10,4 -> 10
    }

    [Fact]
    public void AverageRoundsUpAboveTheMidpoint()
    {
        var attributes = new Attributes(Strength: 10, Speed: 10, Technique: 10, Stamina: 10, Leash: 13);

        Assert.Equal(11, attributes.Average); // suma 53 / 5 = 10,6 -> 11
    }
}
