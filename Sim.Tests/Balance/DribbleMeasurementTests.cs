using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// <b>Medición</b> para [BI-D]: ¿existe el regate en la práctica, y cuánto dura una posesión?
///
/// <para>Nace de una observación del revisor jugando —«el balón no va en sus pies, no se nota como si lo
/// controlara»— y de su conclusión de diseño: <i>«debería existir conducción con duración porque si no el
/// regate tampoco entra en juego»</i>. Antes de tocar una regla hay que saber si la premisa es cierta, y
/// <b>el regate no está instrumentado en ninguna parte</b>: no hay métrica en <c>/Balance</c>, ni campo en
/// el informe de partido, y <c>MatchLogView</c> omite <c>DRIBBLE_ATTEMPTED</c> a propósito. Sin esto, «el
/// regate no entra en juego» es una impresión, no un dato.</para>
///
/// <para>No es una puerta y no afirma ninguna banda: <b>mide y escribe</b>. Lo que salga decide si la
/// mecánica de conducción hace falta y contra qué se compara después (<c>balance-measure</c>).</para>
/// </summary>
public sealed class DribbleMeasurementTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Matches = 200;
    private const ulong BaseSeed = 1;

    private readonly ITestOutputHelper _output;
    public DribbleMeasurementTests(ITestOutputHelper output) => _output = output;

    private readonly record struct Sample(
        int Attempted,
        int Won,
        int Lost,
        int Possessions,
        int OwnedTicks,
        int TotalTicks,
        int LongestRun,
        int DribblingTicks,
        int DribbleRuns,
        int LongestDribbleRun,
        int RunsEndedStillOwner,
        int RunsEndedBallGone,
        int RunsStillOpenAtEnd);

    [Fact]
    public void DribbleAndPossession_HowMuchIsThereToday()
    {
        var setup = TestMatches.Reference(Catalog, BaseSeed);
        var config = SimConfig.Default with { CollectLog = false, Trace = true };

        var samples = new Sample[Matches];
        Parallel.For(0, Matches, i =>
        {
            ulong matchSeed = RngStreams.MatchSeed(BaseSeed, i);
            var result = Simulator.Run(setup, matchSeed, ThreadCatalogs.Current, config);
            samples[i] = Measure(result);
        });

        // Reducción después, en orden de índice (RT-041): el resultado no depende de qué hilo acabó antes.
        double attempted = 0, won = 0, lost = 0, possessions = 0, ownedShare = 0, longest = 0, meanRun = 0;
        double dribbling = 0, dribbleRuns = 0, dribbleRunLength = 0, endedOwner = 0, endedGone = 0, stillOpen = 0, frames = 0;
        int longestEver = 0, longestDribbleEver = 0;
        for (int i = 0; i < Matches; i++)
        {
            var s = samples[i];
            attempted += s.Attempted;
            won += s.Won;
            lost += s.Lost;
            possessions += s.Possessions;
            ownedShare += s.TotalTicks == 0 ? 0 : 100.0 * s.OwnedTicks / s.TotalTicks;
            longest += s.LongestRun;
            meanRun += s.Possessions == 0 ? 0 : (double)s.OwnedTicks / s.Possessions;
            longestEver = Math.Max(longestEver, s.LongestRun);
            dribbling += s.DribblingTicks;
            dribbleRuns += s.DribbleRuns;
            dribbleRunLength += s.DribbleRuns == 0 ? 0 : (double)s.DribblingTicks / s.DribbleRuns;
            longestDribbleEver = Math.Max(longestDribbleEver, s.LongestDribbleRun);
            endedOwner += s.RunsEndedStillOwner;
            endedGone += s.RunsEndedBallGone;
            stillOpen += s.RunsStillOpenAtEnd;
            frames += s.TotalTicks;
        }

        _output.WriteLine($"{Matches} partidos, emparejamiento de referencia, semilla {BaseSeed}");
        _output.WriteLine($"  regates intentados por partido : {attempted / Matches:0.###}");
        _output.WriteLine($"  regates ganados por partido    : {won / Matches:0.###}");
        _output.WriteLine($"  regates perdidos por partido   : {lost / Matches:0.###}");
        _output.WriteLine($"  posesiones por partido         : {possessions / Matches:0.#}");
        _output.WriteLine($"  ticks con dueño                : {ownedShare / Matches:0.#} %");
        _output.WriteLine($"  duración media de una posesión : {meanRun / Matches:0.##} ticks ({meanRun / Matches / 15.0:0.###} s)");
        _output.WriteLine($"  posesión más larga (media)     : {longest / Matches:0.##} ticks ({longest / Matches / 15.0:0.###} s)");
        _output.WriteLine($"  posesión más larga (máximo)    : {longestEver} ticks ({longestEver / 15.0:0.###} s)");
        // El denominador es la traza REAL, no el literal 1200. Desde la ADR 0147 el reloj del partido
        // (_clockTick) y el del motor (_tick) divergen —las reanudaciones consumen ticks de motor sin
        // consumir partido—, así que dividir por 1200 mide otra cosa desde entonces y siempre al alza.
        _output.WriteLine($"  fotogramas por partido         : {frames / Matches:0.#} (el literal de antes era 1200)");
        _output.WriteLine($"  ticks CONDUCIENDO por partido  : {dribbling / Matches:0.#} ({(frames == 0 ? 0 : 100.0 * dribbling / frames):0.##} % de la traza)");
        _output.WriteLine($"  conducciones por partido       : {dribbleRuns / Matches:0.#}");
        _output.WriteLine($"  duración media de conducir     : {dribbleRunLength / Matches:0.##} ticks ({dribbleRunLength / Matches / 15.0:0.###} s)");
        _output.WriteLine($"  conducción más larga (máximo)  : {longestDribbleEver} ticks ({longestDribbleEver / 15.0:0.###} s)");

        // BI-D: POR QUÉ termina una conducción. Con el contador en driveTicks, una conducción que acaba
        // con el balón todavía en los pies es una que AGOTÓ su compromiso; una que acaba sin balón es una
        // que CORTARON. Distinguirlas es lo que separa «la dosis es corta» de «el partido no la deja
        // durar», y las dos piden arreglos opuestos.
        double endedTotal = endedOwner + endedGone;
        _output.WriteLine($"  conducciones terminadas CON balón (agotan el contador) : {endedOwner / Matches:0.##} por partido ({(endedTotal == 0 ? 0 : 100.0 * endedOwner / endedTotal):0.#} %)");
        _output.WriteLine($"  conducciones terminadas SIN balón (las cortan)         : {endedGone / Matches:0.##} por partido ({(endedTotal == 0 ? 0 : 100.0 * endedGone / endedTotal):0.#} %)");
        _output.WriteLine($"  conducciones aún vivas al acabar el partido            : {stillOpen / Matches:0.##} por partido");

        // Las tres salidas tienen que sumar exactamente las rachas contadas: si un cambio futuro en las
        // transiciones de estado rompe la clasificación, este Assert lo dice en vez de dejar que el
        // porcentaje mienta.
        Assert.True(
            Math.Abs(endedOwner + endedGone + stillOpen - dribbleRuns) < 0.5,
            $"las conducciones clasificadas ({endedOwner + endedGone + stillOpen}) no cuadran con las contadas ({dribbleRuns})");

        // Lo único que se afirma: que la medición ha corrido de verdad sobre partidos con traza.
        Assert.True(possessions > 0, "ningún partido registró posesión: la medición no vale");
    }

    private static Sample Measure(MatchResult result)
    {
        int attempted = 0, won = 0, lost = 0;
        var events = result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            switch (events[i].Type)
            {
                case EventType.DribbleAttempted:
                    attempted++;
                    break;
                case EventType.DribbleWon:
                    won++;
                    break;
                case EventType.DribbleLost:
                    lost++;
                    break;
            }
        }

        // La posesión y el estado se leen de la traza, que es donde viven tick a tick quién lleva el balón
        // y qué está haciendo cada jugador.
        int possessions = 0, owned = 0, longest = 0, current = 0, previous = -1, total = 0;
        int dribblingTicks = 0, dribbleRuns = 0, longestDribble = 0;
        int endedStillOwner = 0, endedBallGone = 0, stillOpen = 0;
        var dribbleRun = new int[result.Trace?.Players.Count ?? 0];
        if (result.Trace is { FrameCount: > 0 } trace)
        {
            total = trace.FrameCount;
            for (int f = 0; f < trace.FrameCount; f++)
            {
                // Cuántos ticks pasa CUALQUIERA conduciendo, y en rachas de cuántos ticks seguidos. Es la
                // pregunta que los eventos de duelo no responden: un duelo solo salta si hay un rival a
                // 0,8 casillas, así que 0,85 duelos por partido no dice cuánto se conduce.
                for (int p = 0; p < dribbleRun.Length; p++)
                {
                    if (trace.OnPitchAt(f, p) && trace.StateAt(f, p) == PlayerState.Dribbling)
                    {
                        dribblingTicks++;
                        dribbleRun[p]++;
                        if (dribbleRun[p] == 1)
                        {
                            dribbleRuns++;
                        }

                        longestDribble = Math.Max(longestDribble, dribbleRun[p]);
                    }
                    else
                    {
                        // La racha que acaba en este fotograma: si el balón sigue siendo suyo, la
                        // conducción terminó por su cuenta; si no, se la quitaron.
                        if (dribbleRun[p] > 0)
                        {
                            if (trace.BallOwnerAt(f) == p)
                            {
                                endedStillOwner++;
                            }
                            else
                            {
                                endedBallGone++;
                            }
                        }

                        dribbleRun[p] = 0;
                    }
                }

                int owner = trace.BallOwnerAt(f);
                if (owner >= 0)
                {
                    owned++;
                    current = owner == previous ? current + 1 : 1;
                    if (current == 1)
                    {
                        possessions++;
                    }

                    longest = Math.Max(longest, current);
                }
                else
                {
                    current = 0;
                }

                previous = owner;
            }
        }

        // Una conducción todavía viva en el último fotograma no pasa por la rama que clasifica, así que
        // se cuenta aparte: sin esto desaparecería en silencio y endedOwner+endedGone no cuadraría con
        // dribbleRuns (la revisión independiente del 25 sep lo pidió, y el Assert de abajo lo fija).
        for (int p = 0; p < dribbleRun.Length; p++)
        {
            if (dribbleRun[p] > 0)
            {
                stillOpen++;
            }
        }

        return new Sample(attempted, won, lost, possessions, owned, total, longest, dribblingTicks, dribbleRuns, longestDribble, endedStillOwner, endedBallGone, stillOpen);
    }
}
