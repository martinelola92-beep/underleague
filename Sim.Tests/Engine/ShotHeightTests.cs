using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0135 paso 2: <b>el tiro sale con altura y la portería tiene geometría.</b>
///
/// <para>Hasta esta ADR todo tiro a puerta apuntaba al <b>centro exacto</b> de la portería y el balón se
/// dibujaba siempre a ras de suelo, así que en un render 3D todos los disparos eran rasos e idénticos. Sin
/// dispersión del punto de mira, dar altura al balón no habría cambiado nada: todos los tiros irían al
/// mismo sitio. Las dos cosas son la misma decisión y entran juntas.</para>
///
/// <para><b>Lo que este paso NO cambia, a propósito:</b> el portero sigue midiendo su alcance en el plano,
/// así que ningún desenlace se mueve todavía. La altura empieza a decidir en el paso 3. Aquí se comprueba
/// que existe, que es coherente y que no rompe nada.</para>
/// </summary>
public sealed class ShotHeightTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private const int Matches = 300;

    /// <summary>El balón se despega del suelo en algún momento: antes era imposible por construcción.</summary>
    [Fact]
    public void TheBallLeavesTheGroundDuringShots()
    {
        float highest = 0f;
        int framesAboveGround = 0;

        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            for (int f = 0; f < trace.FrameCount; f++)
            {
                float h = trace.BallHeightAt(f);
                if (h > 0f)
                {
                    framesAboveGround++;
                    highest = MathF.Max(highest, h);
                }
            }
        }

        Assert.True(framesAboveGround > 0, "el balón nunca se levantó del suelo en 40 partidos");
        Assert.True(highest > 0.1f, $"el balón apenas se despegó: máximo {highest:F3} casillas");
    }

    /// <summary>
    /// Un disparo describe una <b>parábola</b>, no una recta: sube y vuelve a bajar. Es lo que distingue un
    /// tiro de un rayo láser, y lo que el paso 3 usará para saber si a un defensa le pasa por encima.
    /// </summary>
    [Fact]
    public void AShotArcsInsteadOfTravellingFlat()
    {
        bool sawAnArc = false;

        for (ulong seed = 1; seed <= 60 && !sawAnArc; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Shot || e.Detail == "attempted")
                {
                    continue;
                }

                int from = trace.FrameOfTick(e.Tick);
                float peak = 0f;
                int peakFrame = -1;
                int last = Math.Min(from + 30, trace.FrameCount - 1);
                for (int f = from; f <= last; f++)
                {
                    if (trace.BallHeightAt(f) > peak)
                    {
                        peak = trace.BallHeightAt(f);
                        peakFrame = f;
                    }
                }

                // Sube y baja: el punto más alto no está ni al principio ni al final del vuelo.
                if (peak > 0.05f && peakFrame > from && peakFrame < last && trace.BallHeightAt(last) < peak)
                {
                    sawAnArc = true;
                    break;
                }
            }
        }

        Assert.True(sawAnArc, "ningún disparo describió una parábola: el balón viaja plano");
    }

    /// <summary>
    /// <b>Los tiros a puerta dejan de ir todos al mismo punto.</b> Antes cruzaban la línea exactamente por
    /// el centro, siempre; ahora se reparten por el ancho de la portería según la calidad del disparo.
    /// </summary>
    [Fact]
    public void ShotsOnTargetSpreadAcrossTheGoalInsteadOfAllHittingTheCentre()
    {
        var rows = new List<float>();

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Goal)
                {
                    continue;
                }

                int frame = trace.FrameOfTick(e.Tick);
                if (frame > 0 && frame < trace.FrameCount)
                {
                    rows.Add(trace.BallAt(frame - 1).Y);
                }
            }
        }

        Assert.True(rows.Count > 50, $"muestra corta: sólo {rows.Count} goles");
        int offCentre = rows.Count(r => MathF.Abs(r - 3.5f) > 0.05f);
        Assert.True(
            offCentre > rows.Count / 4,
            $"los goles siguen concentrados en el centro de la portería: sólo {offCentre} de {rows.Count} fuera del centro exacto");
    }

    /// <summary>
    /// La geometría de la portería es coherente con la de fuera: un tiro a puerta cruza la línea
    /// <b>dentro</b> del semiancho declarado, y por debajo del larguero.
    /// </summary>
    [Fact]
    public void ShotsOnTargetStayInsideTheDeclaredGoalMouth()
    {
        float halfWidth = Catalog.Tuning.Shot.GoalHalfWidthCellsMilli / 1000f;
        float height = Catalog.Tuning.Shot.GoalHeightCellsMilli / 1000f;
        var offences = new List<string>();

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;

            foreach (var e in result.Events)
            {
                if (e.Type != EventType.Goal)
                {
                    continue;
                }

                int frame = trace.FrameOfTick(e.Tick);
                if (frame <= 0 || frame >= trace.FrameCount)
                {
                    continue;
                }

                float row = trace.BallAt(frame - 1).Y;
                float z = trace.BallHeightAt(frame - 1);
                if (MathF.Abs(row - 3.5f) > halfWidth + 0.01f || z > height + 0.01f)
                {
                    offences.Add($"semilla {seed}: gol con fila {row:F2} y altura {z:F2} (portería ±{halfWidth} y {height} de alto)");
                }
            }
        }

        Assert.True(offences.Count == 0, string.Join("\n", offences.Take(5)));
    }
}
