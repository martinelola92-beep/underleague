using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0185, enmienda del acotado (revisión independiente): valores conocidos de lo que el acotado promete.
/// <list type="bullet">
/// <item>la rampa actúa en juego abierto, se apaga con una reanudación pendiente y sigue en la turba;</item>
/// <item>la barrera de reanudación, con la rampa, sólo quita la velocidad hacia el balón;</item>
/// <item>el sacador del saque de centro tiene que haber llegado andando (<c>TakerInPlaceCells</c>), con y sin rampa;</item>
/// <item>la rampa baja de verdad las inversiones de rumbo (la cifra de cabecera, como aserción).</item>
/// </list>
/// </summary>
public sealed class OpenPlayRampTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly Catalog On = AccelerationTests.WithAccel(Catalog, 3);
    private static readonly Catalog Off = AccelerationTests.WithAccel(Catalog, 0);

    private readonly ITestOutputHelper _output;

    public OpenPlayRampTests(ITestOutputHelper output) => _output = output;

    /// <summary>Primer paso desde parado hacia un punto lejano, en milésimas de casilla.</summary>
    private static (int Step, int Ceiling) FirstStep(Catalog catalog, Action<MatchEngine>? setUp)
    {
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        int index = engine.OutfieldIndexForTest(0, 3);
        engine.ParkBallForTest(new Vec2(14f, 6f));
        setUp?.Invoke(engine);
        var player = engine.PlayerAtForTest(index);
        engine.PlaceForTest(index, new Vec2(3f, 3.5f));
        player.Velocity = new Vec2(0f, 0f);
        player.TargetPoint = new Vec2(12f, 3.5f);
        int ceiling = engine.SpeedPerTickMilliForTest(index);
        var before = player.Position;
        engine.MoveForTest(index);
        return ((int)MathF.Round(Vec2.Distance(before, player.Position) * 1000f), ceiling);
    }

    [Fact]
    public void TheRampWorksInOpenPlayStopsDuringARestartAndKeepsGoingInTheMob()
    {
        int accelTicks = On.Tuning.Movement.AccelTicks;

        var (open, ceiling) = FirstStep(On, null);
        Assert.Equal(ceiling / accelTicks, open);

        // Con un saque de falta pendiente (lejos del jugador), la colocación va a paso constante: el techo desde parado.
        var (restart, restartCeiling) = FirstStep(On, e => e.BeginRestartForTest(RestartKind.FreeKick, 1, new Vec2(13f, 1f), 20));
        Assert.Equal(restartCeiling, restart);

        // La turba es juego abierto: la rampa sigue, sobre el techo con el bono de velocidad de la turba.
        var (mob, mobCeiling) = FirstStep(On, e => e.EnterMobPhaseForTest());
        Assert.True(mobCeiling > ceiling, "la turba debía subir el techo");
        Assert.Equal(mobCeiling / accelTicks, mob);

        // Control: sin rampa, el techo en los tres casos.
        Assert.Equal(FirstStep(Off, null).Ceiling, FirstStep(Off, null).Step);
    }

    /// <summary>
    /// La barrera aparta a un rival que estaba dentro de su radio. Con la rampa, su velocidad pierde sólo la parte que iba
    /// hacia el balón (la que la barrera le ha quitado) y conserva la de lado; sin rampa, se pone a cero como siempre.
    /// </summary>
    [Fact]
    public void TheBarrierRemovesOnlyTheVelocityTowardsTheBall()
    {
        foreach (var (catalog, ramped) in new[] { (On, true), (Off, false) })
        {
            var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
            var point = new Vec2(8f, 3.5f);
            engine.BeginRestartForTest(RestartKind.FreeKick, 0, point, 20);
            int rival = engine.OutfieldIndexForTest(1, 2);
            float clearance = catalog.Tuning.Restart.RestartClearanceCells;
            engine.PlaceForTest(rival, new Vec2(point.X + (clearance / 2f), point.Y));
            var player = engine.PlayerAtForTest(rival);
            player.Velocity = new Vec2(-0.10f, 0.05f);

            engine.EnforceRestartClearanceForTest();

            Assert.True(Vec2.Distance(player.Position, point) >= clearance - 0.001f, "la barrera debía apartarle");
            if (ramped)
            {
                Assert.Equal(0f, player.Velocity.X, 3);
                Assert.Equal(0.05f, player.Velocity.Y, 3);
            }
            else
            {
                Assert.Equal(0f, player.Velocity.Length, 3);
            }
        }
    }

    /// <summary>
    /// El saque de centro no se da por colocado mientras el sacador esté a más de <c>TakerInPlaceCells</c> (0,25) de su
    /// punto, con y sin rampa (el sacador anda a paso constante: el teletransporte no depende del arranque).
    /// </summary>
    [Fact]
    public void TheKickoffWaitsForTheTakerWithAndWithoutTheRamp()
    {
        foreach (var catalog in new[] { On, Off })
        {
            var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
            var center = new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow);
            engine.BeginRestartForTest(RestartKind.Kickoff, 0, center, 20);
            int taker = engine.RestartTakerIndexForTest;
            Assert.True(taker >= 0);

            // Todos en su campo, lejos del círculo, en filas distintas; los porteros donde están.
            for (int team = 0; team < 2; team++)
            {
                for (int k = 0; k < 6; k++)
                {
                    int i = engine.OutfieldIndexForTest(team, k);
                    if (i != taker)
                    {
                        engine.PlaceForTest(i, new Vec2(team == 0 ? 3f : 13f, 1f + k));
                    }
                }
            }

            engine.PlaceForTest(taker, new Vec2(center.X - 3f, center.Y));
            Assert.False(engine.EveryoneInPlaceForTest(), "a 3 casillas, el sacador aún no ha llegado");
            engine.PlaceForTest(taker, new Vec2(center.X - 0.3f, center.Y));
            Assert.False(engine.EveryoneInPlaceForTest(), "a 0,3 casillas (> 0,25), tampoco");
            engine.PlaceForTest(taker, new Vec2(center.X - 0.2f, center.Y));
            Assert.True(engine.EveryoneInPlaceForTest(), "a 0,2 casillas está colocado");
        }
    }

    /// <summary>
    /// La cifra de cabecera de la ADR 0185 como aserción: con la sonda limpia de la ADR 0184 en 12 partidos, la rampa deja las
    /// inversiones de rumbo a velocidad de carrera en menos de un tercio (medido en 40: 0,092 → 0,010 por jugador y segundo).
    /// </summary>
    [Fact]
    public void TheRampCutsTheReversals()
    {
        var off = OscillationProbeTests.Measure(Off, 1, 12);
        var on = OscillationProbeTests.Measure(On, 1, 12);
        OscillationProbeTests.Report(_output, "accelTicks 0", off);
        OscillationProbeTests.Report(_output, "accelTicks 3", on);
        Assert.True(off.Reversals > 500, $"control: sin rampa la sonda debía ver inversiones, vio {off.Reversals}");
        Assert.True(on.PerPlayerSecond * 3 < off.PerPlayerSecond, $"inversiones {on.PerPlayerSecond:F3}/s con rampa contra {off.PerPlayerSecond:F3}/s sin ella");
    }
}
