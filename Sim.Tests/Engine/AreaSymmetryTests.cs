using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// El área de portería está centrada en el eje Rows/2, no pegada a la banda de arriba (ADR 0121):
/// <see cref="Pitch.IsInArea"/> y <see cref="Utility.ClampToArea"/> deben tratar igual un punto y su
/// espejo respecto al centro del campo. Antes de la ADR, una fila quedaba libre arriba y dos abajo, así
/// que estos mismos puntos habrían dado resultados distintos.
/// </summary>
public sealed class AreaSymmetryTests
{
    private const float Center = Pitch.Rows / 2f;

    private static float Mirror(float y) => (2f * Center) - y;

    public static IEnumerable<object[]> MirroredYPairs()
    {
        // Incluye los bordes de la banda (1,5 y 5,5) y puntos interiores y exteriores.
        yield return new object[] { 1.5f, 5.5f };
        yield return new object[] { 2f, 5f };
        yield return new object[] { 3f, 4f };
        yield return new object[] { 0f, 7f };
        yield return new object[] { 1f, 6f };
    }

    [Theory]
    [MemberData(nameof(MirroredYPairs))]
    public void IsInAreaIsSymmetricAroundTheCenterAxis(float y, float mirroredY)
    {
        Assert.Equal(mirroredY, Mirror(y));

        // Columna fija dentro del área del equipo 0 (X < AreaColumns): solo varía la fila.
        var point = new Vec2(0.5f, y);
        var mirroredPoint = new Vec2(0.5f, mirroredY);

        Assert.Equal(Pitch.IsInArea(point, 0), Pitch.IsInArea(mirroredPoint, 0));
    }

    [Fact]
    public void IsInAreaAcceptsBothBandEdgesInclusive()
    {
        Assert.True(Pitch.IsInArea(new Vec2(0.5f, Pitch.AreaTop), 0));
        Assert.True(Pitch.IsInArea(new Vec2(0.5f, Pitch.AreaBottom), 0));
    }

    [Theory]
    [MemberData(nameof(MirroredYPairs))]
    public void ClampToAreaReturnsMirroredPointsForMirroredInputs(float y, float mirroredY)
    {
        var clamped = Utility.ClampToArea(new Vec2(0.5f, y), 0);
        var mirroredClamped = Utility.ClampToArea(new Vec2(0.5f, mirroredY), 0);

        Assert.Equal(Mirror(clamped.Y), mirroredClamped.Y, precision: 5);
    }

    [Fact]
    public void AreaBandHeightStaysAreaRows()
    {
        Assert.Equal(Pitch.AreaRows, Pitch.AreaBottom - Pitch.AreaTop, precision: 5);
    }

    [Fact]
    public void AreaBandIsCenteredOnTheMiddleRow()
    {
        // El punto medio de la banda cae exactamente en Rows/2 (antes de la ADR 0121 caía en 3, no en 3,5).
        Assert.Equal(Center, (Pitch.AreaTop + Pitch.AreaBottom) / 2f, precision: 5);
    }
}
