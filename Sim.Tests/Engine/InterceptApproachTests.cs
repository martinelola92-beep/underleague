using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BX-10 (partida del revisor del 3 oct: «el balón pasa al lado de un jugador solo y no lo coge»): la tirada de
/// intercepción de cada rival se hace en la máxima aproximación del pase, no al entrar en su radio. Tirando al entrar,
/// tres de cada cuatro tiradas se hacían entre 0,6 y 0,9 casillas, con el factor de cercanía del paso 2 de AZ-B casi en
/// ×1, y el balón atravesaba después el cuerpo del rival sin otra tirada (`docs/pendientes/BX-10.md`).
/// </summary>
public sealed class InterceptApproachTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// En el tick del corte el balón está en su punto más cercano al que corta —no más lejos que un tick antes (no se
    /// ha pasado) ni que un tick después (no se adelanta)— o ya dentro de su cuerpo (radio de su raza), que es la otra
    /// razón para tirar. La traza enseña ya el balón en el pie en ese tick, así que su posición se extrapola de los dos
    /// fotogramas anteriores, exacta en el plano porque el vuelo es una recta a velocidad constante. Se mide desde donde
    /// está el que corta en ese tick. Medido en estos 40 partidos: 97 de 117 (83 %); con la tirada al entrar, 14 de 58
    /// (24 %, y con tan pocos cortes medibles porque casi todos caían en los primeros ticks del vuelo). El resto es el
    /// paso que da el que corta dentro del tick y los pases bombeados, que aquí se miden sólo en el plano.
    /// </summary>
    [Fact]
    public void TheInterceptionHappensAtTheClosestPointOfThePass()
    {
        const float Eps = 0.02f;
        int measured = 0;
        int atClosest = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;
            var setup = TestMatches.Reference(Catalog, seed);
            var races = setup.Home.Players.Concat(setup.Away.Players).ToDictionary(p => p.Id, p => p.Race);
            foreach (var e in result.Events)
            {
                if (e.Type != EventType.PassFailed || e.Detail != "intercepted")
                {
                    continue;
                }

                int cut = trace.FrameOfTick(e.Tick);
                if (cut < 2 || !trace.BallInFlightAt(cut - 1) || !trace.BallInFlightAt(cut - 2)
                    || trace.TickAt(cut - 1) != e.Tick - 1 || trace.TickAt(cut - 2) != e.Tick - 2)
                {
                    continue;
                }

                int cutter = Enumerable.Range(0, trace.Players.Count).First(p => trace.Players[p].Id == e.Opponent);
                var from = trace.PositionAt(cut, cutter);
                float body = Catalog.Race(races[e.Opponent]).BodyRadius / 100f;
                var b1 = trace.BallAt(cut - 1);
                var step = b1 - trace.BallAt(cut - 2);
                float before = Vec2.Distance(from, b1);
                float at = Vec2.Distance(from, b1 + step);
                float after = Vec2.Distance(from, b1 + step + step);
                measured++;
                if (at <= body + Eps || (at <= before + Eps && at <= after + Eps))
                {
                    atClosest++;
                }

            }
        }

        Assert.True(measured >= 100, $"muestra escasa: {measured} intercepciones medibles");
        Assert.True(atClosest * 100 > measured * 70, $"sólo {atClosest} de {measured} cortes en el punto más cercano del pase");
    }
}
