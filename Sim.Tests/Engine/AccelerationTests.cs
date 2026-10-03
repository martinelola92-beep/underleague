using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0185 (BV-A H4): arranque, giro y frenada. Valores conocidos sobre <c>Move</c> con un jugador solo y un destino
/// fijo, y control con <c>tuning.movement.accelTicks</c> = 0 (el motor de antes: el techo desde el primer tick).
/// </summary>
public sealed class AccelerationTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// El arranque de la ADR 0185 con <c>accelTicks</c> = 3. Los datos lo traen APAGADO (0) hasta resolver las regresiones
    /// medidas (ver la ADR), así que estos tests encienden la regla en su propio catálogo.
    /// </summary>
    private static readonly Catalog On = WithAccel(3);

    private readonly ITestOutputHelper _output;

    public AccelerationTests(ITestOutputHelper output) => _output = output;

    /// <summary>Desde parado tarda exactamente <c>accelTicks</c> ticks en llegar al techo; con el dato a 0, el primero.</summary>
    [Fact]
    public void FromStandstillTheTopSpeedArrivesAfterAccelTicks()
    {
        int accelTicks = On.Tuning.Movement.AccelTicks;

        var steps = Steps(On, start: new Vec2(2f, 3.5f), target: new Vec2(14f, 3.5f), ticks: accelTicks + 2, out int ceiling);
        _output.WriteLine($"techo {ceiling} · pasos {string.Join(" ", steps)}");
        for (int t = 0; t < accelTicks - 1; t++)
        {
            Assert.True(steps[t] < ceiling, $"tick {t + 1}: {steps[t]} ya es el techo {ceiling}");
            Assert.True(steps[t + 1] > steps[t], "el arranque sube cada tick");
        }

        Assert.InRange(steps[accelTicks - 1], ceiling - 2, ceiling);
        Assert.InRange(steps[accelTicks + 1], ceiling - 2, ceiling);

        var off = Steps(WithAccel(0), new Vec2(2f, 3.5f), new Vec2(14f, 3.5f), 2, out int offCeiling);
        Assert.InRange(off[0], offCeiling - 1, offCeiling);
    }

    /// <summary>
    /// Frenada: lanzado a la punta hacia un destino a 3 casillas, llega sin rebasarlo y los últimos pasos bajan de la punta;
    /// con el dato a 0, el penúltimo paso todavía es la punta.
    /// </summary>
    [Fact]
    public void AtTopSpeedItBrakesBeforeTheTarget()
    {
        var steps = Steps(On, new Vec2(2f, 3.5f), new Vec2(5f, 3.5f), 40, out int ceiling, preRunTicks: 6);
        _output.WriteLine($"techo {ceiling} · pasos {string.Join(" ", steps)}");
        Assert.InRange(steps.Sum(), 2990, 3010);
        int last = steps.FindLastIndex(s => s > 0);
        Assert.True(last >= 2, "debía llegar en varios pasos");
        Assert.True(steps[last] < ceiling / 2 && steps[last - 1] < ceiling, $"debía frenar al llegar: {string.Join(" ", steps)}");

        var off = Steps(WithAccel(0), new Vec2(2f, 3.5f), new Vec2(5f, 3.5f), 40, out int offCeiling, preRunTicks: 6);
        int offLast = off.FindLastIndex(s => s > 0);
        Assert.InRange(off[offLast - 1], offCeiling - 1, offCeiling);
    }

    /// <summary>Media vuelta: la rapidez se pierde entera y vuelve a arrancar; un giro de 90° conserva la mitad.</summary>
    [Fact]
    public void AHalfTurnStartsFromZeroAndARightAngleKeepsHalf()
    {
        var engine = Engine(On, out int index, out int ceiling);
        var player = engine.PlayerAtForTest(index);
        engine.PlaceForTest(index, new Vec2(7f, 3.5f));
        player.Velocity = new Vec2(ceiling / 1000f, 0f);
        player.TargetPoint = new Vec2(2f, 3.5f);
        engine.MoveForTest(index);
        int back = Milli(player.Velocity.Length);
        int accel = ceiling / On.Tuning.Movement.AccelTicks;
        Assert.InRange(back, accel - 2, accel + 2);

        engine.PlaceForTest(index, new Vec2(7f, 3.5f));
        player.Velocity = new Vec2(ceiling / 1000f, 0f);
        player.TargetPoint = new Vec2(7f, 0.5f);
        engine.MoveForTest(index);
        int side = Milli(player.Velocity.Length);
        Assert.InRange(side, (ceiling / 2) + accel - 3, Math.Min(ceiling, (ceiling / 2) + accel + 3));
    }

    /// <summary>ADR 0185 (gameplay-debug): quién sigue dentro del área al sacar de puerta, y qué estaba haciendo.</summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void WhoIsStillInTheAreaAtTheGoalKick()
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true });
            var trace = result.Trace!;
            foreach (var e in result.Events.Where(e => e.Type == Underleague.Sim.Events.EventType.Recovery && e.Detail == "goalKick"))
            {
                int f = trace.FrameOfTick(e.Tick);
                int keeper = trace.BallOwnerAt(f);
                if (keeper < 0 || trace.Players[keeper].Role != Position.Goalkeeper)
                {
                    continue;
                }

                int team = trace.Players[keeper].Team;
                for (int i = 0; i < trace.Players.Count; i++)
                {
                    if (i == keeper || !trace.OnPitchAt(f, i) || trace.Players[i].Role == Position.Goalkeeper || !Pitch.IsInArea(trace.PositionAt(f, i), team))
                    {
                        continue;
                    }

                    var path = string.Join(" ", Enumerable.Range(Math.Max(0, f - 4), 5).Select(g => $"({trace.PositionAt(g, i).X:F2},{trace.PositionAt(g, i).Y:F2})->({trace.TargetAt(g, i).X:F2},{trace.TargetAt(g, i).Y:F2}) {trace.ActionAt(g, i)} balón ({trace.BallAt(g).X:F1},{trace.BallAt(g).Y:F1})"));
                    _output.WriteLine($"semilla {seed} tick {e.Tick} jugador {trace.Players[i].Id} (equipo {trace.Players[i].Team}, portero del {team}): {path}");
                }
            }
        }
    }

    /// <summary>ADR 0185 (gameplay-debug): la racha de baile de cobertura más larga con el arranque, y qué la produce.</summary>
    [Fact]
    [Trait("Category", "Diagnostic")]
    public void LongestCoverDance()
    {
        int best = 0;
        string where = "";
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var trace = Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { Trace = true }).Trace!;
            for (int p = 0; p < trace.Players.Count; p++)
            {
                int run = 0;
                for (int f = 2; f < trace.FrameCount; f++)
                {
                    var a = trace.PositionAt(f - 1, p) - trace.PositionAt(f - 2, p);
                    var b = trace.PositionAt(f, p) - trace.PositionAt(f - 1, p);
                    bool rev = trace.OnPitchAt(f, p) && a.Length > 0.02f && b.Length > 0.02f && ((a.X * b.X) + (a.Y * b.Y)) < 0f;
                    run = rev ? run + 1 : 0;
                    if (rev && run > best && trace.ActionAt(f, p) == PlayerAction.CoverSpace)
                    {
                        best = run;
                        var steps = string.Join(" ", Enumerable.Range(f - 5, 6).Select(g =>
                        {
                            var s = trace.PositionAt(g, p) - trace.PositionAt(g - 1, p);
                            int near = Enumerable.Range(0, trace.Players.Count).Where(q => q != p && trace.OnPitchAt(g, q)).OrderBy(q => Vec2.Distance(trace.PositionAt(g, q), trace.PositionAt(g, p))).First();
                            return $"({s.X:F3},{s.Y:F3}) {trace.StateAt(g, p)}/{trace.ActionAt(g, p)} obj ({trace.TargetAt(g, p).X:F2},{trace.TargetAt(g, p).Y:F2}) pos ({trace.PositionAt(g, p).X:F2},{trace.PositionAt(g, p).Y:F2}) R{trace.RestartAt(g)} cerca {trace.Players[near].Id} a {Vec2.Distance(trace.PositionAt(g, near), trace.PositionAt(g, p)):F2} {trace.ActionAt(g, near)}";
                        }));
                        where = $"semilla {seed} jugador {trace.Players[p].Id} fotograma {f} racha {run}: {steps}";
                    }
                }
            }
        }

        _output.WriteLine(where);
    }

    private static List<int> Steps(Catalog catalog, Vec2 start, Vec2 target, int ticks, out int ceiling, int preRunTicks = 0)
    {
        var engine = Engine(catalog, out int index, out ceiling);
        var player = engine.PlayerAtForTest(index);
        engine.PlaceForTest(index, start);
        if (preRunTicks > 0)
        {
            player.Velocity = new Vec2(ceiling / 1000f, 0f);
        }

        var steps = new List<int>();
        for (int t = 0; t < ticks; t++)
        {
            player.TargetPoint = target;
            var before = player.Position;
            engine.MoveForTest(index);
            steps.Add(Milli(Vec2.Distance(before, player.Position)));
        }

        return steps;
    }

    private static MatchEngine Engine(Catalog catalog, out int index, out int ceiling)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        index = engine.OutfieldIndexForTest(0, 3);
        engine.ParkBallForTest(new Vec2(14f, 6f));
        ceiling = engine.SpeedPerTickMilliForTest(index);
        return engine;
    }

    private static int Milli(float cells) => (int)MathF.Round(cells * 1000f);

    private static Catalog WithAccel(int ticks) =>
        Catalog with { Tuning = Catalog.Tuning with { Movement = Catalog.Tuning.Movement with { AccelTicks = ticks } } };
}
