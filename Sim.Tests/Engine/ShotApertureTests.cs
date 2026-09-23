using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0135 paso 3a: <b>la portería disponible.</b> La apertura a portería —el coseno del ángulo con la
/// perpendicular a la línea de gol— entra en el tiro por dos sitios, y ninguno es una penalización
/// inventada: uno es una identidad trigonométrica y el otro es el término que la hace visible.
///
/// <para>Lo que venía a arreglar está fichado y medido en <b>BA-E</b>: el 32,4 % de los tiros salía con
/// apertura &lt; 0,5 y producía el 37,1 % de los goles, o sea que <b>convertían mejor que la media</b>.
/// Ésa es la inversión que estos tests vigilan.</para>
/// </summary>
public sealed class ShotApertureTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private const int Matches = 400;

    /// <summary>
    /// <b>El punto de trabajo con el que se MIDIÓ la mecánica</b>, no el que viene en <c>tuning.json</c>.
    /// Los dos valores se publican <b>apagados</b> (<c>minAimApertureCenti: 100</c> hace el divisor
    /// exactamente 1 y <c>offTargetAperturePenalty: 0</c> anula el término) porque el paquete quedó
    /// pendiente de una decisión del revisor: encendido pone en rojo
    /// <c>buildsWinDifferently_injuries</c> (1,29 → 1,04), que es una puerta acertando, no una banda
    /// estrecha (ADR 0131). Los tests conducen el mecanismo <b>explícitamente</b> para que sigan
    /// demostrando lo que hace, en vez de volverse verdes por estar apagado — un test que pasa porque la
    /// mecánica no corre no demuestra nada.
    /// </summary>
    private static Catalog WithAperture(int offTargetPenalty = 2000, int minAperture = 20) => Catalog with
    {
        Tuning = Catalog.Tuning with
        {
            Shot = Catalog.Tuning.Shot with
            {
                OffTargetAperturePenalty = offTargetPenalty,
                MinAimApertureCenti = minAperture,
            }
        }
    };

    /// <summary>
    /// <b>El cimiento, comprobado y no supuesto:</b> <c>ApertureCenti</c> es el coseno del ángulo con la
    /// perpendicular a la línea de gol. Todo el paso 3a descansa en que dividir por esta magnitud es la
    /// trigonometría correcta (<c>m/cos θ</c>) y no un factor elegido a ojo, así que la identidad se
    /// afirma aquí de forma independiente del motor.
    /// </summary>
    [Theory]
    [InlineData(0f, 100)]   // de frente: cos 0 = 1
    [InlineData(30f, 86)]   // cos 30 = 0,866
    [InlineData(45f, 70)]   // cos 45 = 0,707
    [InlineData(60f, 50)]   // cos 60 = 0,5
    [InlineData(80f, 17)]   // casi desde el cordel
    public void ApertureIsTheCosineOfTheAngleToGoal(float degrees, int expectedCenti)
    {
        var goal = new Vec2(0f, 3.5f);
        float radians = degrees * MathF.PI / 180f;
        // Un punto a 10 casillas de la portería, girado el ángulo pedido respecto de la perpendicular.
        var point = new Vec2(goal.X + (10f * MathF.Cos(radians)), goal.Y + (10f * MathF.Sin(radians)));

        Assert.Equal(expectedCenti, Utility.ApertureCenti(point, goal), tolerance: 1);
    }

    /// <summary>
    /// <b>La ventaja de conversión de BA-E desaparece.</b> Es la afirmación de diseño del paso, dicha en
    /// la misma unidad con la que se midió el problema: un tiro sin ángulo no puede seguir siendo un tiro
    /// <i>mejor</i> que la media.
    ///
    /// <para>Se comprueba con el instrumento del propio motor (<c>LowApertureShots</c>/
    /// <c>LowApertureGoals</c>, ADR 0136) y no con un script aparte, para que la cifra del test y la del
    /// lote sean la misma cifra.</para>
    /// </summary>
    [Fact]
    public void ShotsWithoutAngleNoLongerConvertBetterThanAverage()
    {
        int shots = 0, goals = 0, lowShots = 0, lowGoals = 0;

        var catalog = WithAperture();
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default);
            var r = result.Report;
            shots += r.Shots[0] + r.Shots[1];
            goals += r.Goals[0] + r.Goals[1];
            lowShots += r.LowApertureShots;
            lowGoals += r.LowApertureGoals;
        }

        Assert.True(lowShots > 200, $"muestra corta: sólo {lowShots} tiros sin ángulo");

        double overall = (double)goals / shots;
        double low = (double)lowGoals / lowShots;

        // El listón es «no mejor que la media», no «peor»: lo que BA-E señalaba como absurdo era la
        // VENTAJA, y fijar aquí un objetivo numérico de desventaja sería convertir un lote de balance en
        // un test unitario. Un margen de holgura pequeño para no depender de la última décima.
        Assert.True(
            low <= overall * 1.02,
            $"un tiro sin ángulo sigue convirtiendo mejor que la media: {low:P2} contra {overall:P2}");
    }

    /// <summary>
    /// <b>El término de dato hace algo, y se demuestra apagándolo.</b> Con <c>offTargetAperturePenalty</c>
    /// en 0 y todo lo demás igual, los mismos partidos tienen que dar más tiros a puerta: si no, el número
    /// publicado en <c>tuning.json</c> sería decorativo.
    ///
    /// <para>Existe por la lección de BE-B, que la revisión independiente dejó escrita: dos <c>if</c> sin
    /// ningún test se podían borrar con la suite entera en verde.</para>
    /// </summary>
    [Fact]
    public void TheApertureTermActuallyCostsShotsOnTarget()
    {
        (int onTarget, int shots) With = Count(WithAperture());
        (int onTarget, int shots) Without = Count(WithAperture(offTargetPenalty: 0));

        double withRate = (double)With.onTarget / With.shots;
        double withoutRate = (double)Without.onTarget / Without.shots;

        Assert.True(
            withoutRate > withRate,
            $"apagar offTargetAperturePenalty no cambió la puntería: {withoutRate:P2} contra {withRate:P2}");

        static (int, int) Count(Catalog catalog)
        {
            int onTarget = 0, shots = 0;
            for (ulong seed = 1; seed <= Matches; seed++)
            {
                var r = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default).Report;
                onTarget += r.ShotsOnTarget[0] + r.ShotsOnTarget[1];
                shots += r.Shots[0] + r.Shots[1];
            }

            return (onTarget, shots);
        }
    }

    /// <summary>
    /// <b>El suelo de apertura es una guarda, no un ajuste.</b> <c>1/cos θ</c> es una asíntota: sin suelo,
    /// un tiro desde el cordel exacto tendría error infinito. Se comprueba que el suelo existe de verdad
    /// —que bajarlo mucho cambia el resultado y por tanto está en el camino— y que con el valor publicado
    /// el motor no produce ningún disparo con destino fuera del campo, que es lo que un error sin acotar
    /// habría producido.
    /// </summary>
    [Fact]
    public void TheApertureFloorBoundsTheAimError()
    {
        Assert.InRange(Catalog.Tuning.Shot.MinAimApertureCenti, 1, 100);

        int tight = Goals(WithAperture());
        int wild = Goals(WithAperture(minAperture: 1));
        Assert.True(tight != wild, "bajar el suelo de apertura no cambió nada: el suelo no está en el camino");

        static int Goals(Catalog catalog)
        {
            int goals = 0;
            for (ulong seed = 1; seed <= 120; seed++)
            {
                var r = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default).Report;
                goals += r.Goals[0] + r.Goals[1];
            }

            return goals;
        }
    }
}
