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
        int LongestRun);

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
        int longestEver = 0;
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

        // La posesión se lee de la traza, que es donde vive tick a tick quién lleva el balón.
        int possessions = 0, owned = 0, longest = 0, current = 0, previous = -1, total = 0;
        if (result.Trace is { FrameCount: > 0 } trace)
        {
            total = trace.FrameCount;
            for (int f = 0; f < trace.FrameCount; f++)
            {
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

        return new Sample(attempted, won, lost, possessions, owned, total, longest);
    }
}
