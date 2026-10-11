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

    /// <summary>
    /// BX-10c: el bloqueo del tiro mide el alcance en esfera (ADR 0135, decisión 3), como el corte del pase. Ningún defensa
    /// bloquea un balón que le pasa por encima de su radio. La cuota de bloqueo se sube a 100 (<c>shot.blockChancePercent</c>,
    /// el doble de la publicada) para tener muestra: cambia cuántos, no dónde.
    /// <para>El instrumento (Regla J, revisión independiente): la traza enseña el balón bloqueado con altura 0, porque
    /// <c>Ball.SetLoose</c> la pone a cero. La altura del tick del bloqueo sale de la parábola de los tres fotogramas
    /// anteriores, que es exacta porque la comba es cuadrática en el tick. Sólo cuentan los bloqueos cuyos fotogramas son
    /// todos del mismo vuelo; no lo son, por ejemplo, los remates de primera. Del que bloquea se toma la posición más
    /// favorable de los dos fotogramas, porque se mueve dentro del tick. Casi todos los bloqueos caen en los primeros
    /// ticks del vuelo y no son medibles, de ahí los 3200 partidos (~45 s). Medido en ellos: con el alcance en el plano,
    /// 18 de 65 bloqueos medibles iban por encima; con la esfera, 0 de 49.</para>
    /// </summary>
    [Fact]
    public void NoShotIsBlockedAboveTheDefendersReach()
    {
        var catalog = Catalog with { Tuning = Catalog.Tuning with { Shot = Catalog.Tuning.Shot with { BlockChancePercent = 100 } } };
        float radius = catalog.Tuning.Pass.InterceptRadiusCells;
        int measured = 0;
        var above = new List<string>();
        for (ulong seed = 1; seed <= 3200; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;
            foreach (var e in result.Events)
            {
                if (e.Type != EventType.ShotBlocked)
                {
                    continue;
                }

                int cut = trace.FrameOfTick(e.Tick);
                if (cut < 3 || trace.TickAt(cut - 3) != e.Tick - 3
                    || !trace.BallInFlightAt(cut - 1) || !trace.BallInFlightAt(cut - 2) || !trace.BallInFlightAt(cut - 3))
                {
                    continue;
                }

                var b1 = trace.BallAt(cut - 1);
                var b2 = trace.BallAt(cut - 2);
                var step = b1 - b2;
                if (Vec2.Distance(trace.BallAt(cut), b1 + step) > 0.05f || Vec2.Distance(b2 - step, trace.BallAt(cut - 3)) > 0.05f)
                {
                    continue;
                }

                float z = (3f * trace.BallHeightAt(cut - 1)) - (3f * trace.BallHeightAt(cut - 2)) + trace.BallHeightAt(cut - 3);
                int blocker = Enumerable.Range(0, trace.Players.Count).First(p => trace.Players[p].Id == e.Actor);
                float reach = Math.Min(
                    MatchEngine.ReachDistance(trace.PositionAt(cut - 1, blocker), trace.BallAt(cut), z),
                    MatchEngine.ReachDistance(trace.PositionAt(cut, blocker), trace.BallAt(cut), z));
                measured++;
                if (reach >= radius + 0.01f)
                {
                    above.Add($"semilla {seed}, tick {e.Tick}: altura {z:F2}, a {reach:F2}");
                }
            }
        }

        Assert.True(measured >= 35, $"muestra escasa: {measured} bloqueos medibles");
        Assert.True(above.Count == 0, $"{above.Count} de {measured} bloqueos por encima del alcance: {string.Join("; ", above.Take(5))}");
    }
}
