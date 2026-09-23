using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BB-O (docs/pendientes/BB-O.md): <b>el balón no puede pertenecer a quien no está en el campo.</b>
///
/// <para>Por qué es un invariante y no un caso más: si ocurre, el partido se congela entero y en
/// silencio. Nadie puede robarle el balón al fantasma (<c>ResolveTackle</c> sale por
/// <c>!carrier.OnPitch</c>), él no ejecuta ninguna acción (<c>UpdatePlayer</c> sale por lo mismo) y no
/// existe ningún temporizador de inactividad en <c>/Sim</c>. Medido en el árbol del 16 sep 2026
/// (commit 3dd0b6d): semilla 144, <b>740 de 1200 fotogramas</b> —el 62 % del partido— con el dueño fuera
/// del campo y cero eventos. Eso es exactamente lo que el principio rector prohíbe (RF-012d): un partido
/// que se congela no es "malo pero previsible", es un error.</para>
///
/// <para>Se comprueban dos arenas porque una sola no basta: con dos equipos humanos de calidad 50 las
/// salidas del campo son tan raras que el test pasaría sin ejercitar nada. <c>TestMatches.Brutal</c>
/// —frágiles contra brutales— las produce en masa, y es lo que hace que la mitad de "sale del campo con
/// el balón" no sea vacua; la aserción de no vacuidad lo vigila.</para>
///
/// <para><b>Lo que este test NO demuestra, dicho aquí para que nadie lo lea de más.</b> En HEAD el caso
/// no se dispara: cero en 14 000 partidos medidos (4 000 de sonda más un lote de 10 000 de
/// <c>/Balance</c>), así que <b>este test pasaría también sin la guardia</b> y no es lo que demuestra que
/// la guardia sirva. Eso se demostró donde el bug sí se reproduce: en el árbol del 16 sep,
/// <b>2 episodios en 2 000 partidos → 0</b> con la guardia puesta, y la semilla 144 pasando de 740
/// fotogramas congelados a un dueño fantasma que dura 1 tick antes de que otro jugador recoja el balón.
/// Aquí lo que se vigila es que el invariante <i>siga</i> cumpliéndose cuando alguien toque el camino de
/// posesión: es una red, no una reproducción.</para>
/// </summary>
public sealed class BallOwnerOnPitchTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private const int Matches = 400;

    [Fact]
    public void TheBallNeverBelongsToSomebodyOffThePitch()
    {
        var failures = new List<string>();
        int exits = 0;
        int exitsWithBall = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            Check(TestMatches.Reference(Catalog, seed), seed, "reference", failures, ref exits, ref exitsWithBall);
            Check(TestMatches.Brutal(Catalog), seed, "brutal", failures, ref exits, ref exitsWithBall);
        }

        // No vacuidad: si la muestra no produce salidas del campo con el balón en los pies, este test no
        // está midiendo nada y su verde es falso.
        Assert.True(exitsWithBall > 0, $"muestra vacua: {exits} salidas del campo, {exitsWithBall} con el balón");
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(10)));
    }

    private static void Check(MatchSetup setup, ulong seed, string arena, List<string> failures, ref int exits, ref int exitsWithBall)
    {
        var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        foreach (var e in result.Events)
        {
            bool leaves = (e.Type == EventType.Injury && e.Detail != "playOn")
                || e.Type == EventType.Death
                || (e.Type == EventType.Card && e.Detail == "red");
            if (!leaves || e.Actor < 0)
            {
                continue;
            }

            exits++;

            // Trampa medida (BB-M, BA-L): LeavePitch pone la posición en (-1,-1), así que la posesión se
            // lee en el fotograma ANTERIOR al evento, nunca en el del evento.
            int before = trace.FrameOfTick(e.Tick) - 1;
            int victim = IndexOf(trace, e.Actor);
            if (victim >= 0 && before >= 0 && trace.BallOwnerAt(before) == victim)
            {
                exitsWithBall++;
            }
        }

        for (int f = 0; f < trace.FrameCount; f++)
        {
            int owner = trace.BallOwnerAt(f);
            if (owner >= 0 && !trace.OnPitchAt(f, owner))
            {
                failures.Add(
                    $"{arena} semilla {seed}: en el fotograma {f} (tick {trace.TickAt(f)}) el balón pertenece al jugador "
                    + $"{trace.Players[owner].Id}, que no está en el campo (estado {trace.StateAt(f, owner)})");
                return;
            }
        }
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        return -1;
    }
}
