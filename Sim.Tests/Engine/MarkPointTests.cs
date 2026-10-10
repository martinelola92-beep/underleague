using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BX-13 (partida del revisor del 3 oct, «stutter»): el que marca se pone del lado de su portería y en contacto con el
/// marcado, no encima. Con el centro del rival por destino, los cuerpos se solapaban, la separación (ADR 0020) los
/// apartaba y el marcador volvía a entrar: los dos temblaban ±0,08 casillas por tick.
/// </summary>
public sealed class MarkPointTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void TheMarkerStandsGoalSideTouchingTheMarkedPlayer()
    {
        var engine = TestPerks.Engine(Catalog, TestMatches.Reference(Catalog, 1));
        for (int team = 0; team < 2; team++)
        {
            var marker = engine.PlayerAtForTest(engine.OutfieldIndexForTest(team, 0));
            var marked = engine.PlayerAtForTest(engine.OutfieldIndexForTest(1 - team, 0));
            marked.Position = new Vec2(8f, 1.5f);

            var point = Utility.MarkPoint(marker, marked);
            var ownGoal = Pitch.GoalCenter(1 - team);

            Assert.Equal(marker.BodyRadius + marked.BodyRadius, Vec2.Distance(point, marked.Position), 3);
            Assert.True(
                Vec2.Distance(point, ownGoal) < Vec2.Distance(marked.Position, ownGoal),
                $"equipo {team}: el punto de marcaje {point} no queda entre el marcado {marked.Position} y la portería propia {ownGoal}");
        }
    }

    /// <summary>
    /// En partido: un marcador que ya ha llegado no se mete dentro del cuerpo del marcado. Antes del arreglo, los pares
    /// marcador–marcado solapados eran lo normal (el destino estaba dentro del rival).
    /// </summary>
    [Fact]
    public void AnArrivedMarkerDoesNotOverlapTheMarkedPlayer()
    {
        int arrived = 0;
        int overlapping = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true }).Trace!;
            for (int f = 1; f < trace.FrameCount; f++)
            {
                for (int p = 0; p < trace.Players.Count; p++)
                {
                    int m = trace.MarkTargetAt(f, p);
                    if (m < 0 || trace.ActionAt(f, p) != PlayerAction.MarkOpponent || trace.StateAt(f, p) != PlayerState.Positioning
                        || !trace.OnPitchAt(f, p) || !trace.OnPitchAt(f, m) || Vec2.Distance(trace.PositionAt(f, p), trace.TargetAt(f, p)) > 0.1f)
                    {
                        continue;
                    }

                    arrived++;
                    if (Vec2.Distance(trace.PositionAt(f, p), trace.PositionAt(f, m)) < 0.45f)
                    {
                        overlapping++;
                    }
                }
            }
        }

        Assert.True(arrived >= 200, $"muestra corta: {arrived} fotogramas de marcador llegado");
        Assert.True(overlapping * 100 < arrived * 10, $"{overlapping} de {arrived} fotogramas con el marcador metido en el marcado");
    }
}
