using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// <b>Medición</b> para la violencia que dejó la ADR 0147 y para el hallazgo sin dueño que dejó
/// [BI-D](../../docs/pendientes/BI-D.md): <b>hoy se decide conducir un 44 % menos</b> que en el árbol de la
/// ADR 0137 —4,1 contra 7,3 conducciones por partido, las dos con <c>driveTicks</c> a 0—, y nadie sabe qué
/// se decide en su lugar.
///
/// <para>El sospechoso con nombre es <see cref="PlayerState.Shielding"/>: gemelo exacto de
/// <see cref="PlayerState.Dribbling"/> desde la ADR 0137 —mismo <c>EnterState(estado, ticks)</c>, mismo
/// corte por pérdida de balón, y hasta la <b>misma dosis</b> (<c>ShieldingTicks</c> 12 contra
/// <c>driveTicks</c> 12)— que <b>no está instrumentado en absoluto</b> y compite por las mismas decisiones
/// del portador.</para>
///
/// <para>No es una puerta y no afirma ninguna banda: <b>censa y escribe</b>. Qué hace el que lleva el
/// balón, cuánto dura cada cosa y cómo termina. Mismo patrón que
/// <see cref="DribbleMeasurementTests"/>, incluido el que importa: una racha que acaba <b>con</b> el balón
/// agotó su compromiso y una que acaba <b>sin</b> él la cortaron, y las dos piden arreglos opuestos.</para>
/// </summary>
public sealed class CarrierCensusTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Matches = 200;
    private const ulong BaseSeed = 1;

    private readonly ITestOutputHelper _output;
    public CarrierCensusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void WhatDoesTheCarrierDo_AndHowDoesShieldingEnd()
    {
        var setup = TestMatches.Reference(Catalog, BaseSeed);
        var config = SimConfig.Default with { CollectLog = false, Trace = true };

        int states = Enum.GetValues<PlayerState>().Length;
        int actions = Enum.GetValues<PlayerAction>().Length;
        var stateTicks = new long[Matches][];
        var actionTicks = new long[Matches][];
        var shield = new (int Ticks, int Runs, int EndedOwner, int EndedGone, int Frames, int Fouls, int Tackles)[Matches];

        Parallel.For(0, Matches, i =>
        {
            ulong matchSeed = RngStreams.MatchSeed(BaseSeed, i);
            var result = Simulator.Run(setup, matchSeed, ThreadCatalogs.Current, config);
            var byState = new long[states];
            var byAction = new long[actions];
            int shieldTicks = 0, runs = 0, endedOwner = 0, endedGone = 0, frames = 0;

            if (result.Trace is { FrameCount: > 0 } trace)
            {
                frames = trace.FrameCount;
                var run = new int[trace.Players.Count];
                for (int f = 0; f < trace.FrameCount; f++)
                {
                    int owner = trace.BallOwnerAt(f);
                    if (owner >= 0)
                    {
                        byState[(int)trace.StateAt(f, owner)]++;
                        if (trace.ActionAt(f, owner) is { } decided)
                        {
                            byAction[(int)decided]++;
                        }
                    }

                    for (int p = 0; p < run.Length; p++)
                    {
                        if (trace.OnPitchAt(f, p) && trace.StateAt(f, p) == PlayerState.Shielding)
                        {
                            shieldTicks++;
                            run[p]++;
                            if (run[p] == 1)
                            {
                                runs++;
                            }
                        }
                        else
                        {
                            if (run[p] > 0)
                            {
                                if (trace.BallOwnerAt(f) == p)
                                {
                                    endedOwner++;
                                }
                                else
                                {
                                    endedGone++;
                                }
                            }

                            run[p] = 0;
                        }
                    }
                }
            }

            int fouls = 0, tackles = 0;
            for (int e = 0; e < result.Events.Count; e++)
            {
                if (result.Events[e].Type == EventType.Foul)
                {
                    fouls++;
                }
                else if (result.Events[e].Type == EventType.Tackle)
                {
                    tackles++;
                }
            }

            stateTicks[i] = byState;
            actionTicks[i] = byAction;
            shield[i] = (shieldTicks, runs, endedOwner, endedGone, frames, fouls, tackles);
        });

        // Reducción después, en orden de índice (RT-041).
        var totalState = new long[states];
        var totalAction = new long[actions];
        long shieldTotal = 0, runsTotal = 0, ownerTotal = 0, goneTotal = 0, framesTotal = 0, foulsTotal = 0, tacklesTotal = 0;
        for (int i = 0; i < Matches; i++)
        {
            for (int s = 0; s < states; s++)
            {
                totalState[s] += stateTicks[i][s];
            }

            for (int a = 0; a < actions; a++)
            {
                totalAction[a] += actionTicks[i][a];
            }

            shieldTotal += shield[i].Ticks;
            runsTotal += shield[i].Runs;
            ownerTotal += shield[i].EndedOwner;
            goneTotal += shield[i].EndedGone;
            framesTotal += shield[i].Frames;
            foulsTotal += shield[i].Fouls;
            tacklesTotal += shield[i].Tackles;
        }

        long ownedTicks = 0;
        foreach (long t in totalState)
        {
            ownedTicks += t;
        }

        _output.WriteLine($"{Matches} partidos, emparejamiento de referencia, semilla {BaseSeed}");
        _output.WriteLine($"  fotogramas por partido         : {(double)framesTotal / Matches:0.#}");
        _output.WriteLine($"  faltas / entradas por partido  : {(double)foulsTotal / Matches:0.##} / {(double)tacklesTotal / Matches:0.##}");
        _output.WriteLine($"  ticks con dueño por partido    : {(double)ownedTicks / Matches:0.#}");
        _output.WriteLine("  QUÉ HACE EL QUE LLEVA EL BALÓN (% de los ticks con dueño):");
        for (int s = 0; s < states; s++)
        {
            if (totalState[s] > 0)
            {
                _output.WriteLine($"    {(PlayerState)s,-14} {100.0 * totalState[s] / ownedTicks:0.##} %");
            }
        }

        _output.WriteLine("  QUÉ HA DECIDIDO (% de los ticks con dueño):");
        for (int a = 0; a < actions; a++)
        {
            if (totalAction[a] > 0)
            {
                _output.WriteLine($"    {(PlayerAction)a,-16} {100.0 * totalAction[a] / ownedTicks:0.##} %");
            }
        }

        double endedTotal = ownerTotal + goneTotal;
        _output.WriteLine($"  PROTECCIONES por partido       : {(double)runsTotal / Matches:0.##}");
        _output.WriteLine($"  ticks protegiendo por partido  : {(double)shieldTotal / Matches:0.#} ({100.0 * shieldTotal / framesTotal:0.##} % de la traza)");
        _output.WriteLine($"  duración media de proteger     : {(runsTotal == 0 ? 0 : (double)shieldTotal / runsTotal):0.##} ticks");
        _output.WriteLine($"  terminadas CON balón (agotan)  : {(double)ownerTotal / Matches:0.##} ({(endedTotal == 0 ? 0 : 100.0 * ownerTotal / endedTotal):0.#} %)");
        _output.WriteLine($"  terminadas SIN balón (cortadas): {(double)goneTotal / Matches:0.##} ({(endedTotal == 0 ? 0 : 100.0 * goneTotal / endedTotal):0.#} %)");

        // ¿Explica proteger la violencia? Correlación de Pearson por PARTIDO entre los ticks protegiendo
        // y las faltas, y entre los ticks protegiendo y las entradas. El nivel absoluto de faltas depende
        // del conjunto —este arnés genera dos equipos de calidad 50 y /Balance usa reference.json, que da
        // 7,96 faltas contra las 3,91 de aquí— pero la CORRELACIÓN dentro del mismo conjunto no: si
        // proteger fuese el canal de la violencia, los partidos con más protección tendrían más faltas.
        _output.WriteLine($"  correlación protección↔faltas  : {Correlation(shield, x => x.Ticks, x => x.Fouls):0.###}");
        _output.WriteLine($"  correlación protección↔entradas: {Correlation(shield, x => x.Ticks, x => x.Tackles):0.###}");
        _output.WriteLine($"  correlación conducción↔faltas  : n/d (este censo no separa conducción por partido)");

        Assert.True(ownedTicks > 0, "ningún partido registró posesión: la medición no vale");
    }

    /// <summary>Pearson sobre las 200 muestras por partido. Sin librería: son cuatro sumas.</summary>
    private static double Correlation(
        (int Ticks, int Runs, int EndedOwner, int EndedGone, int Frames, int Fouls, int Tackles)[] samples,
        Func<(int Ticks, int Runs, int EndedOwner, int EndedGone, int Frames, int Fouls, int Tackles), int> left,
        Func<(int Ticks, int Runs, int EndedOwner, int EndedGone, int Frames, int Fouls, int Tackles), int> right)
    {
        int n = samples.Length;
        double mx = samples.Average(s => (double)left(s));
        double my = samples.Average(s => (double)right(s));
        double sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = left(samples[i]) - mx;
            double dy = right(samples[i]) - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }

        return sxx <= 0 || syy <= 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
    }
}
