using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Run;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// Al expulsado no se le sustituye, aunque se haya lesionado en el mismo tick (destapado al mover las semillas con BX-10b, 11 oct
/// 2026). Una entrada sin balón puede lesionar al propio infractor y costarle la roja en el mismo tick. <c>SubstitutionPoints.Pending</c>
/// veía la lesión y ofrecía la sustitución; el motor la rechazaba, porque el jugador estaba expulsado (ADR 0094), y la run
/// entera se caía con una <see cref="ArgumentException"/> (RunPolicy, orcos, semilla 4: lesión grave, perk <c>dirty_play</c> y
/// roja, todo en el tick 373).
/// <para>El caso es demasiado raro para encontrarlo barriendo partidos sueltos (ninguno en 3000), así que se construye: un
/// partido real con una lesión, y la roja al lesionado en ese mismo tick. El mismo partido sin la roja es el control.</para>
/// </summary>
public sealed class SubstitutionSentOffTests
{
    [Fact]
    public void NoSubstitutionIsOfferedForAPlayerSentOffInTheSameTick()
    {
        var catalog = TestData.LoadCatalog();
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var setup = TestMatches.Build(catalog, seed, homeQuality: 15, awayQuality: 95);
            var result = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false));
            var point = SubstitutionPoints.Pending(setup, result, team: 0, catalog);
            if (point is null)
            {
                continue;
            }

            var injury = result.Events.First(e => e.Type is EventType.Injury or EventType.Death && e.Tick == point.Tick && e.Actor == point.OutPlayerId);
            var events = result.Events.ToList();
            events.Insert(events.IndexOf(injury) + 1, injury with { Type = EventType.Card, Detail = "red" });
            var sentOff = result with { Events = events };

            var after = SubstitutionPoints.Pending(setup, sentOff, team: 0, catalog);
            Assert.True(after is null || after.OutPlayerId != point.OutPlayerId || after.Tick != point.Tick, $"semilla {seed}: se ofrece sustituir al expulsado {point.OutPlayerId} en el tick {point.Tick}");
            return;
        }

        Assert.Fail("ningún punto de sustitución en 200 partidos: la prueba no cubre nada");
    }
}
