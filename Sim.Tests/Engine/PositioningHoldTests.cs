using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BV-A H8 (docs/pendientes/BV-A.md, ADR 0184): <b>una colocación se sostiene</b>. Sin ella, el 46,5 % de las
/// inversiones de rumbo a velocidad de carrera se deshacían en ≤ 4 ticks: jugadores que van y vienen.
///
/// <para>Las tres causas CONFIRMED por la sonda (<see cref="OscillationProbeTests"/>): <c>CoverSpace</c> y
/// <c>Retreat</c> alternándose por un empate que el propio movimiento invierte; <c>CoverSpace</c> descartada al
/// llegar al borde exterior de la zona y recuperada en cuanto el jugador se aleja 0,25 casillas; y
/// <c>FindSpace</c> cambiando de hueco porque el anterior no estaba entre sus candidatos.</para>
/// </summary>
public sealed class PositioningHoldTests
{
    private const int Matches = 40;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private readonly ITestOutputHelper _output;

    public PositioningHoldTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Regla J, el instrumento con respuesta conocida: una carrera recta no tiene inversiones, un zigzag de ida
    /// y vuelta las tiene todas y se deshacen, un giro de 90° no es una inversión y un paso de andar no cuenta.
    /// </summary>
    [Fact]
    public void TheReversalInstrumentAnswersTheKnownCases()
    {
        var run = new Vec2(0.14f, 0f);
        Assert.False(OscillationProbeTests.IsReversal(run, run), "carrera recta");
        Assert.True(OscillationProbeTests.IsReversal(run, new Vec2(-0.14f, 0f)), "media vuelta a carrera");
        Assert.True(OscillationProbeTests.Undoes(run, new Vec2(-0.14f, 0f)), "volver a la dirección de antes deshace la inversión");
        Assert.False(OscillationProbeTests.IsReversal(run, new Vec2(0f, 0.14f)), "un giro de 90° no es una inversión");
        Assert.False(OscillationProbeTests.IsReversal(run, new Vec2(-0.05f, 0f)), "un paso de andar no es carrera");
        Assert.False(OscillationProbeTests.Undoes(new Vec2(0.01f, 0f), new Vec2(-0.14f, 0f)), "un empujón de 0,01 no deshace nada");
    }

    /// <summary>
    /// El arreglo, contra su control. Medido en 40 partidos de referencia (semillas 1-40): con la sostenida
    /// apagada el 46,5 % de las inversiones se deshacen en ≤ 4 ticks (0,30 inversiones por jugador y segundo);
    /// con <c>positioningHoldBonus</c> = 40, el 13,7 % (0,16/s). Los topes dejan margen de ruido a los dos lados.
    /// </summary>
    [Fact]
    public void ReversalsAreRarelyUndoneOnceAPositioningHolds()
    {
        Assert.True(Catalog.Ai.Context.PositioningHoldBonus > 0, "los datos reales debían traer la sostenida encendida");

        var off = OscillationProbeTests.Measure(OscillationProbeTests.WithHold(Catalog, 0), 1, Matches);
        var on = OscillationProbeTests.Measure(Catalog, 1, Matches);
        OscillationProbeTests.Report(_output, "sin sostener", off);
        OscillationProbeTests.Report(_output, "datos", on);

        Assert.True(off.UndonePercent >= 35.0, $"control: sin sostener el instrumento debía ver la oscilación (≥ 35 %), vio {off.UndonePercent:F1} %");
        Assert.True(on.UndonePercent < 15.0, $"inversiones deshechas en ≤ 4 ticks: {on.UndonePercent:F1} % (tope 15 %)");
        Assert.True(on.PerPlayerSecond < 0.75 * off.PerPlayerSecond, $"y menos inversiones en total: {on.PerPlayerSecond:F3}/s contra {off.PerPlayerSecond:F3}/s");
    }

    /// <summary>
    /// La regla, en escenarios montados: un defensa que ya cubre (<c>CoverSpace</c>) se coloca en una rejilla de
    /// puntos con el balón en tres sitios. (a) Existe al menos un punto en el que sin sostenida se repliega y con
    /// ella sigue cubriendo; (b) en NINGÚN punto la sostenida cambia una decisión que no sea entre dos
    /// colocaciones: si sin ella gana perseguir, entrar, presionar o bloquear, con ella gana lo mismo.
    /// </summary>
    [Fact]
    public void TheHoldOnlyEverSwapsOnePositioningForAnother()
    {
        int kept = 0, changed = 0;
        foreach (var ball in new[] { new Vec2(12f, 3.5f), new Vec2(9f, 1.5f), new Vec2(6f, 5.5f) })
        {
            for (int ix = 0; ix <= 14; ix++)
            {
                for (int iy = 0; iy <= 6; iy++)
                {
                    var at = new Vec2(0.75f + (ix * 0.5f), 0.5f + iy);
                    var (offChoice, onChoice) = ChooseBoth(at, ball, PlayerAction.CoverSpace);
                    if (offChoice == onChoice)
                    {
                        continue;
                    }

                    changed++;
                    Assert.True(
                        IsPositioning(offChoice) && IsPositioning(onChoice),
                        $"en {at} con el balón en {ball} la sostenida cambió {offChoice} por {onChoice}: sólo puede cambiar una colocación por otra");
                    Assert.Equal(PlayerAction.CoverSpace, onChoice);
                    if (offChoice == PlayerAction.Retreat)
                    {
                        kept++;
                    }
                }
            }
        }

        _output.WriteLine($"puntos donde la sostenida cambia la decisión: {changed}, de ellos cubrir en vez de replegar: {kept}");
        Assert.True(kept > 0, "la rejilla debía encontrar un punto en el que la sostenida mantiene la cobertura frente al repliegue");
    }

    private static bool IsPositioning(PlayerAction action) =>
        action is PlayerAction.CoverSpace or PlayerAction.Retreat or PlayerAction.MarkOpponent
            or PlayerAction.FindSpace or PlayerAction.OfferSupport;

    /// <summary>
    /// Lo que la sostenida NO toca: si gana una acción que no es de colocación —aquí perseguir un balón suelto
    /// al lado—, gana igual aunque el jugador estuviera cubriendo.
    /// </summary>
    [Fact]
    public void TheHoldNeverStopsAPlayerFromGoingForTheBall()
    {
        var (offChoice, onChoice) = ChooseBoth(new Vec2(3f, 2.5f), new Vec2(3.4f, 2.5f), PlayerAction.CoverSpace);
        Assert.Equal(PlayerAction.ChaseBall, offChoice);
        Assert.Equal(PlayerAction.ChaseBall, onChoice);
    }

    /// <summary>Lo que elige el primer defensa del local en <paramref name="at"/>, sin y con sostenida, viniendo de <paramref name="current"/>.</summary>
    private static (PlayerAction Off, PlayerAction On) ChooseBoth(Vec2 at, Vec2 ball, PlayerAction current)
    {
        var off = Choose(OscillationProbeTests.WithHold(Catalog, 0), at, ball, current);
        var on = Choose(Catalog, at, ball, current);
        return (off, on);
    }

    private static PlayerAction Choose(Catalog catalog, Vec2 at, Vec2 ball, PlayerAction current)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        engine.ParkBallForTest(ball);
        int index = engine.OutfieldIndexForTest(0, 0);
        engine.PlaceForTest(index, at);
        var player = engine.PlayerAtForTest(index);
        Assert.Equal(PlayerState.Positioning, player.State);
        player.CurrentAction = current;
        return engine.ChooseForTest(index);
    }
}
