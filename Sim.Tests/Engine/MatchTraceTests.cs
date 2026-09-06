using Underleague.Sim.Engine;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// La traza de partido (<see cref="MatchTrace"/>): que esté apagada por defecto, que encendida cubra
/// todos los ticks sin submuestrear y que no cambie ni un evento del partido (RT-024).
/// </summary>
public sealed class MatchTraceTests
{
    private const ulong Seed = 20260906UL;

    [Fact]
    public void OffByDefault()
    {
        var catalog = TestData.LoadCatalog();
        var result = Simulator.Run(TestMatches.Reference(catalog, Seed), Seed, catalog, SimConfig.Default);

        Assert.Null(result.Trace);
    }

    /// <summary>
    /// Lo importante de todo el paquete: grabar la traza no puede mover el partido. Si esto falla, la
    /// pantalla estaría enseñando un partido distinto del que la run resolvió.
    /// </summary>
    [Fact]
    public void DoesNotChangeTheMatch()
    {
        var catalog = TestData.LoadCatalog();
        var setup = TestMatches.Reference(catalog, Seed);

        var plain = Simulator.Run(setup, Seed, catalog, SimConfig.Default);
        var traced = Simulator.Run(setup, Seed, catalog, SimConfig.Default with { Trace = true });

        Assert.Equal(plain.Events.Count, traced.Events.Count);
        for (int i = 0; i < plain.Events.Count; i++)
        {
            Assert.Equal(plain.Events[i], traced.Events[i]);
        }

        Assert.Equal(plain.Report.Goals, traced.Report.Goals);
        Assert.Equal(plain.Report.Ticks, traced.Report.Ticks);
        Assert.Equal(plain.Report.Winner, traced.Report.Winner);
    }

    /// <summary>Un fotograma por tick jugado, del 1 al último, sin huecos: el revisor ve el partido entero.</summary>
    [Fact]
    public void CoversEveryTick()
    {
        var catalog = TestData.LoadCatalog();
        var setup = TestMatches.Reference(catalog, Seed);
        var result = Simulator.Run(setup, Seed, catalog, SimConfig.Default with { Trace = true });

        var trace = Assert.IsType<MatchTrace>(result.Trace);
        Assert.Equal(result.Report.Ticks, trace.FrameCount);
        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            Assert.Equal(frame + 1, trace.TickAt(frame));
        }

        Assert.Equal(setup.Home.Lineup.Slots.Count + setup.Away.Lineup.Slots.Count, trace.Players.Count);
    }

    /// <summary>
    /// El contenido de un fotograma: los 20 jugadores dentro del campo, el balón dentro del campo y el
    /// poseedor —cuando lo hay— encima del balón, que es como el motor lo mantiene (§3.7).
    /// </summary>
    [Fact]
    public void FramesAreConsistent()
    {
        var catalog = TestData.LoadCatalog();
        var result = Simulator.Run(
            TestMatches.Reference(catalog, Seed), Seed, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        int owned = 0;
        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            var ball = trace.BallAt(frame);
            Assert.InRange(ball.X, -1f, Sim.Model.Pitch.Columns + 1f);
            Assert.InRange(ball.Y, -1f, Sim.Model.Pitch.Rows + 1f);

            int owner = trace.BallOwnerAt(frame);
            if (owner >= 0)
            {
                owned++;
                Assert.True(Vec2.Distance(trace.PositionAt(frame, owner), ball) < 0.001f);
            }

            for (int player = 0; player < trace.Players.Count; player++)
            {
                var position = trace.PositionAt(frame, player);
                Assert.InRange(position.X, -1f, Sim.Model.Pitch.Columns + 1f);
                Assert.InRange(position.Y, -1f, Sim.Model.Pitch.Rows + 1f);
            }
        }

        Assert.True(owned > trace.FrameCount / 10, "el balón debería tener dueño en buena parte del partido");
    }

    /// <summary>Los tramos de eventos de los fotogramas cubren la secuencia entera, en orden y sin solaparse.</summary>
    [Fact]
    public void EventRangesPartitionTheSequence()
    {
        var catalog = TestData.LoadCatalog();
        var result = Simulator.Run(
            TestMatches.Reference(catalog, Seed), Seed, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        int next = 0;
        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            Assert.Equal(next, trace.EventFromAt(frame));
            int count = trace.EventCountAt(frame);
            Assert.True(count >= 0);
            for (int i = 0; i < count; i++)
            {
                // El primer fotograma se queda además con lo emitido antes del primer tick (el saque).
                Assert.True(frame == 0 || result.Events[next + i].Tick == trace.TickAt(frame));
            }

            next += count;
        }

        Assert.Equal(result.Events.Count, next);
    }

    /// <summary>
    /// Las dos capas que explican el porqué: el objetivo de marcaje y la acción elegida. Lo que se
    /// comprueba es que sean <b>coherentes</b> —un marcado es siempre un rival de campo que está en el
    /// campo— y que la acción esté dentro del enum; que estén ahí lo cubre el hecho de que el partido de
    /// referencia marque en algún tick.
    /// </summary>
    [Fact]
    public void MarkingAndActionsAreRecorded()
    {
        var catalog = TestData.LoadCatalog();
        var result = Simulator.Run(
            TestMatches.Reference(catalog, Seed), Seed, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        int assigned = 0;
        int marking = 0;
        for (int frame = 0; frame < trace.FrameCount; frame++)
        {
            for (int player = 0; player < trace.Players.Count; player++)
            {
                int target = trace.MarkTargetAt(frame, player);
                if (target >= 0)
                {
                    assigned++;
                    Assert.NotEqual(trace.Players[player].Team, trace.Players[target].Team);
                    Assert.NotEqual(Sim.Model.Position.Goalkeeper, trace.Players[target].Role);
                    Assert.True(trace.OnPitchAt(frame, target), "un marcado tiene que estar en el campo");
                }

                var action = trace.ActionAt(frame, player);
                if (action is null)
                {
                    continue;
                }

                Assert.True(Enum.IsDefined(action.Value), "acción fuera del enum: " + action);
                if (action == PlayerAction.MarkOpponent && trace.OnPitchAt(frame, player))
                {
                    marking++;
                }
            }
        }

        Assert.True(assigned > 0, "nadie tiene asignado a quién marcar en todo el partido");
        Assert.True(marking > 0, "nadie llega a elegir MarkOpponent en todo el partido");
    }

    /// <summary>Dorsales 1..N por equipo, sin repetir, y el 1 siempre para el portero.</summary>
    [Fact]
    public void NumbersAreStableAndStartAtTheGoalkeeper()
    {
        var catalog = TestData.LoadCatalog();
        var result = Simulator.Run(
            TestMatches.Reference(catalog, Seed), Seed, catalog, SimConfig.Default with { Trace = true });
        var trace = result.Trace!;

        for (int team = 0; team < 2; team++)
        {
            var seen = new SortedSet<int>();
            foreach (var player in trace.Players)
            {
                if (player.Team != team)
                {
                    continue;
                }

                Assert.True(seen.Add(player.Number), "dorsal repetido en el equipo " + team);
                if (player.Role == Sim.Model.Position.Goalkeeper)
                {
                    Assert.Equal(1, player.Number);
                }
            }

            Assert.Equal(1, seen.Min);
            Assert.Equal(seen.Count, seen.Max);
        }
    }
}
