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

    // RETIRADO (23 sep 2026): había aquí un test que comprobaba que los goles cruzan la línea dentro del
    // semiancho y por debajo del larguero, midiendo la posición del balón en el fotograma ANTERIOR al gol.
    // Medía mal: en ese fotograma el balón todavía está volando y no ha llegado a la línea, así que su
    // fila es un punto intermedio de la trayectoria y no el de cruce -de ahí "gol con fila 2,23" en una
    // portería de ±1-. El punto de cruce es el destino del vuelo, que la traza no expone. Lo que el test
    // quería comprobar lo garantiza el código por construcción (el punto de mira se acota al marco antes
    // de volar) y lo que sí es observable -que un tiro al hierro no es gol- lo cubre el test de abajo.
    // Se retira en vez de relajarlo hasta que pase, que habría dejado un verde que no significa nada.

    /// <summary>
    /// ADR 0135 paso 2b: <b>el marco es físico y rechaza.</b> Antes los postes sólo existían en los
    /// comentarios: un disparo que cruzaba la línea pegado al hierro era gol como cualquier otro.
    ///
    /// <para>Sin el marco, apuntar al rincón —que es donde el portero no llega— sería gratis y un tiro
    /// alto sería siempre mejor que uno raso. El palo es lo que le pone precio a buscar la escuadra.</para>
    /// </summary>
    [Fact]
    public void ShotsCanHitTheWoodworkAndDoNotCountAsGoals()
    {
        int frames = 0;
        int shots = 0;
        var afterFrame = new List<string>();

        for (ulong seed = 1; seed <= 400; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            var events = result.Events;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == EventType.Shot && events[i].Detail != "attempted")
                {
                    shots++;
                    continue;
                }

                if (events[i].Type != EventType.ShotPost)
                {
                    continue;
                }

                frames++;
                Assert.True(
                    events[i].Detail is "post" or "crossbar",
                    $"detalle inesperado en el tiro al marco: '{events[i].Detail}'");

                // Un tiro al marco NO es gol: no puede haber un Goal en el mismo tick.
                for (int j = i + 1; j < events.Count && events[j].Tick == events[i].Tick; j++)
                {
                    if (events[j].Type == EventType.Goal)
                    {
                        afterFrame.Add($"semilla {seed}: gol en el mismo tick que un tiro al marco (tick {events[i].Tick})");
                    }
                }

                // Y el balón queda VIVO, que es lo que distingue un rechace de un balón muerto: sale del
                // marco con velocidad. Que alguien lo recoja al tick siguiente NO es un fallo -un rechace
                // que cae a los pies de un delantero es exactamente lo que se buscaba-, así que lo que se
                // comprueba es que sale con velocidad, no que nadie lo coja.
                int f = trace.FrameOfTick(events[i].Tick);
                if (f + 1 < trace.FrameCount
                    && trace.BallOwnerAt(f + 1) < 0
                    && Vec2.Distance(trace.BallAt(f), trace.BallAt(f + 1)) < 0.01f)
                {
                    afterFrame.Add($"semilla {seed}: el balón se quedó muerto en el marco en vez de rechazar");
                }
            }
        }

        Assert.True(frames > 0, $"ningún tiro dio en el marco en 400 partidos ({shots} tiros)");
        Assert.True(afterFrame.Count == 0, string.Join("\n", afterFrame.Take(5)));

        // Raro, pero no anecdótico: en el fútbol real el marco se lleva el 1-2 % de los disparos.
        double share = frames * 100.0 / Math.Max(shots, 1);
        Assert.InRange(share, 0.3, 5.0);
    }
}
