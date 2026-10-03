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
