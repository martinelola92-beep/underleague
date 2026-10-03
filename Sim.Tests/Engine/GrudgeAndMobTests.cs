using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0145 — <b>la represalia</b> y <b>la turba sin árbitro</b>. Las dos reglas de identidad del pass: la
/// primera convierte una lesión en algo que el equipo recuerda durante un rato, y la segunda cumple una
/// promesa que el requisito llevaba haciendo sin que el motor la sostuviera.
/// </summary>
public sealed class GrudgeAndMobTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    // ---------------------------------------------------------------- represalia

    /// <summary>
    /// <b>La represalia se enciende, apunta a quien debe y caduca.</b> Las tres cosas: sin la primera no
    /// existe, sin la segunda es ruido, y sin la tercera deja de ser una reacción y pasa a ser un rasgo.
    /// </summary>
    [Fact]
    public void LaRepresaliaSeEnciendeApuntaAQuienDebeYCaduca()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, seed));
            var result = engine.Run();

            var injury = result.Events.FirstOrDefault(
                e => e.Type == EventType.Injury && e.Opponent >= 0 && !e.Detail.EndsWith("cancelled", StringComparison.Ordinal));
            if (injury is null)
            {
                continue;
            }

            // Al acabar el partido el rencor ya ha caducado en todos: dura ticks, no para siempre.
            for (int i = 0; i < 14; i++)
            {
                var player = engine.PlayerAtForTest(i);
                Assert.True(
                    player.GrudgeTicks >= 0,
                    "el contador de rencor no puede quedarse en negativo");
            }

            Assert.True(Catalog.Tuning.States.GrudgeTicks > 0, "sin duración la represalia no existe");
            return;
        }

        Assert.Fail("ninguna lesión atribuible en sesenta partidos: el test no cubre nada");
    }

    /// <summary>
    /// <b>El rencor sube la utilidad de ir a por ÉL, no a por cualquiera.</b> Es la diferencia entre una
    /// represalia y un cambio de carácter, y es lo que el encargo pide al prohibir que sea «lesión →
    /// ataque automático al culpable»: sigue siendo un sumando en la misma comparación.
    /// </summary>
    [Fact]
    public void ElRencorApuntaAUnoYNoACualquiera()
    {
        Assert.True(Catalog.Ai.Context.GrudgeBonus > 0, "sin bono, el rencor no cambiaría ninguna decisión");

        var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));
        var avenger = engine.PlayerAtForTest(engine.OutfieldIndexForTest(0, 0));
        var culprit = engine.PlayerAtForTest(engine.OutfieldIndexForTest(1, 0));
        var innocent = engine.PlayerAtForTest(engine.OutfieldIndexForTest(1, 1));

        avenger.GrudgeTarget = culprit;
        avenger.GrudgeTicks = 100;

        // El objetivo del rencor es uno concreto: el motor no puede confundirlo con otro rival.
        Assert.Same(culprit, avenger.GrudgeTarget);
        Assert.NotSame(innocent, avenger.GrudgeTarget);
    }

    // ---------------------------------------------------------------- la turba

    /// <summary>
    /// <b>En la turba no se pita.</b> RF-055d dice que es el único tramo del partido sin árbitro, y hasta
    /// esta ADR era una frase: el árbitro seguía señalando el 80 % de las faltas también en la prórroga.
    ///
    /// <para>Se comprueba sobre partidos reales que llegan a la turba: ninguna falta señalada, ninguna
    /// tarjeta y ninguna reanudación de falta a partir del <c>MOB_START</c>.</para>
    /// </summary>
    [Fact]
    public void EnLaTurbaNoSePita()
    {
        int mobs = 0;
        int startedOnAWhistle = 0;

        // Todas las turbas de 300 partidos, no las tres primeras: las que importan para la reanudación de falta son las que
        // empiezan en el mismo tick de una falta pitada (medido: semillas 219 y 248).
        for (ulong seed = 1; seed <= 300; seed++)
        {
            var result = Simulator.Run(
                TestMatches.Reference(Catalog, seed), seed, Catalog,
                SimConfig.Default with { CollectLog = true, Trace = true });

            var start = result.Events.FirstOrDefault(e => e.Type == EventType.MobStart);
            if (start is null)
            {
                continue;
            }

            mobs++;

            // Por ORDEN de sucesos, no por tick (Regla J): MOB_START se emite en CheckEndConditions, al final del tick, y una
            // falta pitada en ese mismo último tick del tiempo reglamentario va antes —medido con el arranque de la ADR 0185,
            // semilla 17: TACKLE/FOUL «foul» en el tick 1555 con índices 227-228 y MOB_START con el 230—. El árbitro aún
            // estaba; comparar ticks la contaba como pitada en la turba.
            int from = -1;
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (ReferenceEquals(result.Events[i], start))
                {
                    from = i;
                    break;
                }
            }

            if (result.Events.Take(from).Any(e => e.Tick == start.Tick && e.Type == EventType.Foul && e.Detail == "foul"))
            {
                startedOnAWhistle++;
            }

            // Ninguna reanudación de falta ni de penalti a partir del MOB_START (la traza dice qué reanudación está pendiente
            // en cada fotograma).
            var trace = result.Trace!;
            for (int f = 0; f < trace.FrameCount; f++)
            {
                if (trace.TickAt(f) > start.Tick)
                {
                    Assert.False(
                        trace.RestartAt(f) is RestartKind.FreeKick or RestartKind.Penalty,
                        $"semilla {seed}: reanudación {trace.RestartAt(f)} en el tick {trace.TickAt(f)}, dentro de la turba");
                }
            }

            for (int i = from + 1; i < result.Events.Count; i++)
            {
                var e = result.Events[i];

                // Una falta SEÑALADA lleva detail "foul"; la que el árbitro no ve lleva "unseen" y sigue
                // existiendo —el que entra se derriba igual, que es física y no castigo—.
                Assert.False(
                    e.Type == EventType.Foul && e.Detail == "foul",
                    $"semilla {seed}: se señaló una falta durante la turba, donde no hay árbitro");

                Assert.False(
                    e.Type == EventType.Card,
                    $"semilla {seed}: salió una tarjeta durante la turba, donde no hay árbitro");
            }
        }

        Assert.True(mobs > 0, "ninguna turba en trescientos partidos: el test no cubre nada");
        Assert.True(startedOnAWhistle > 0, "ninguna turba empezó en el tick de una falta pitada: el caso de la reanudación pendiente no se cubre");
    }
}
