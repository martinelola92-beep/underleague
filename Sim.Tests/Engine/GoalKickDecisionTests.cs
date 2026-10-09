using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BX-8 (partida del revisor del 3 oct): el saque de puerta es la decisión que escribe la ADR 0141 §2 —«corto si hay
/// alguien seguro, largo si no» y el despeje si no hay opción segura—, no un despeje por defecto. Medido antes del
/// arreglo con 200 partidos de referencia: el portero despejaba el 89 % de los saques (el peligro, que mide dónde está
/// el balón, le daba +620 en un balón muerto) y su «pase corto» iba a 9,7 casillas de mediana porque la condición del
/// alcance le eximía también en el pase corto. Después: despeje 5 %, pase largo 93 %, corto 2 %.
/// </summary>
public sealed class GoalKickDecisionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static IEnumerable<(MatchTrace Trace, IReadOnlyList<MatchEvent> Events, int Frame, int Taker)> GoalKicks(int matches)
    {
        for (ulong seed = 1; seed <= (ulong)matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, new SimConfig(CollectLog: false, Trace: true));
            var trace = result.Trace!;
            for (int f = 0; f + 1 < trace.FrameCount; f++)
            {
                if (trace.RestartAt(f) == RestartKind.GoalKick && trace.RestartAt(f + 1) != RestartKind.GoalKick && trace.RestartTakerAt(f) >= 0)
                {
                    yield return (trace, result.Events, f + 1, trace.RestartTakerAt(f));
                }
            }
        }
    }

    [Fact]
    public void TheGoalKickIsNotAClearanceByDefault()
    {
        int kicks = 0;
        int clears = 0;
        foreach (var (trace, _, frame, taker) in GoalKicks(60))
        {
            kicks++;
            if (trace.ActionAt(frame, taker) == PlayerAction.Clear)
            {
                clears++;
            }
        }

        Assert.True(kicks >= 60, $"la muestra debía tener saques de puerta: {kicks}");
        Assert.True(clears * 100 < kicks * 20, $"el portero despejó {clears} de {kicks} saques de puerta (antes de BX-8, el 89 %)");
    }

    [Fact]
    public void AKeeperShortPassStaysWithinShortRange()
    {
        float shortRange = Catalog.Ai.Context.ShortPassMaxCells;
        int checkedPasses = 0;
        foreach (var (trace, events, frame, taker) in GoalKicks(60))
        {
            if (trace.ActionAt(frame, taker) != PlayerAction.ShortPass)
            {
                continue;
            }

            int takerId = trace.Players[taker].Id;
            int tick = trace.TickAt(frame);
            var attempt = events.FirstOrDefault(e => e.Tick >= tick && e.Tick < tick + 60 && e.Type == EventType.PassAttempted && e.Actor == takerId);
            if (attempt is null)
            {
                continue;
            }

            // Distancia en el fotograma de la DECISIÓN, que es donde se filtra el alcance: al golpear, unos ticks
            // después (el armado del pase), el receptor ya se ha movido.
            int receiver = Enumerable.Range(0, trace.Players.Count).First(p => trace.Players[p].Id == attempt.Target);
            float distance = Vec2.Distance(trace.PositionAt(frame, taker), trace.PositionAt(frame, receiver));
            Assert.True(distance <= shortRange + 0.01f, $"pase corto del portero a {distance:0.0} casillas (alcance {shortRange})");
            checkedPasses++;
        }

        Assert.True(checkedPasses > 0, "la muestra debía tener algún pase corto de saque de puerta");
    }
}
