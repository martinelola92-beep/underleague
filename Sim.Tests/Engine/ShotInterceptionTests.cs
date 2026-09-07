using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// AW-A, paso 1 (docs/plan-intercepcion-disparo.md §3, RF-057c): la parada exige llegar al balón. Durante
/// el vuelo de un tiro a puerta, el portero solo disputa el duelo si en algún tick estuvo a menos de
/// <c>save.reachCells</c> de la posición del balón, y lo disputa <b>una sola vez</b>; si lo pierde, el
/// balón sigue su camino y entra sin segundo duelo.
/// <para>
/// El motor no expone su balón ni sus jugadores, así que el escenario "portero inalcanzable" se controla
/// por el <b>radio</b>, que es el mismo predicado visto desde el otro lado: con <c>reachCells</c> a 0
/// ningún portero llega nunca, y con un radio enorme llega siempre desde el primer tick. Las dos
/// configuraciones acotan el comportamiento por arriba y por abajo sin tocar ni un término de la fórmula
/// de parada.
/// </para>
/// </summary>
public sealed class ShotInterceptionTests
{
    private const int Matches = 50;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Con el radio real (0,9 casillas, el mismo que <c>pass.interceptRadiusCells</c>) siguen conviviendo
    /// paradas y goles: la regla nueva no apaga al portero. Y los contadores del informe siguen cuadrando
    /// con la secuencia de eventos ahora que la parada se resuelve a mitad de vuelo.
    /// </summary>
    [Fact]
    public void SavesAndGoalsBothSurviveTheReachRule()
    {
        var totals = Play(Catalog);

        Assert.True(totals.Saves > 0, "con el radio real tenía que haber paradas");
        Assert.True(totals.Goals > 0, "con el radio real tenía que haber goles");
        Assert.Equal(totals.Saves, totals.SaveEvents);
        Assert.True(
            totals.Saves <= totals.ShotsOnTarget,
            $"no puede haber más paradas ({totals.Saves}) que tiros a puerta ({totals.ShotsOnTarget})");
    }

    /// <summary>
    /// Portero que nunca alcanza el balón: con <c>reachCells</c> a 0 la distancia nunca es menor que el
    /// radio, así que no hay un solo duelo en 50 partidos y **todo** tiro a puerta acaba en gol. Es el
    /// escenario del portero inmovilizado lejos de la trayectoria del plan, expresado sobre el mismo
    /// predicado. El contraste con el radio real mide lo que la regla decide: 400 goles frente a 181.
    /// <para>
    /// AW-A paso 2: <c>diveReachCells</c> también va a 0 aquí. Si se dejara en su valor real (1,5) con
    /// <c>reachCells</c> a 0, la estirada del tick de llegada SÍ se dispararía —el portero asentado casi
    /// siempre está dentro de 1,5 casillas del balón cuando este llega a la línea— y el escenario dejaría
    /// de ser "portero nunca al alcance, cero paradas". Con los dos radios a 0 sigue siéndolo, con o sin
    /// estirada.
    /// </para>
    /// </summary>
    [Fact]
    public void AGoalkeeperThatNeverReachesTheBallNeverSaves()
    {
        var unreachable = Play(WithReach(0f, diveReach: 0f));
        var real = Play(Catalog);

        Assert.True(unreachable.ShotsOnTarget > 0, "el escenario tenía que producir tiros a puerta");
        Assert.Equal(0, unreachable.Saves);
        Assert.Equal(0, unreachable.SaveEvents);
        Assert.True(
            unreachable.Goals > real.Goals,
            $"sin portero al alcance tenían que entrar más goles: {unreachable.Goals} frente a {real.Goals}");
    }

    /// <summary>
    /// AW-A paso 2: con <c>reachCells</c> a 0 el radio normal nunca se dispara durante el vuelo, pero con
    /// <c>diveReachCells</c> real (o mayor) la estirada del tick de llegada sí lo hace, así que ahora
    /// **sí** hay paradas donde el test anterior (con los dos radios a 0) no tenía ninguna. Se fuerza
    /// <c>basePercent</c> alto para que la penalización de la estirada no la deje en cero por ruido.
    /// </summary>
    [Fact]
    public void ADivingGoalkeeperSavesShotsOutsideTheNormalReach()
    {
        var withDive = Catalog with
        {
            Tuning = Catalog.Tuning with
            {
                Save = Catalog.Tuning.Save with
                {
                    ReachCells = 0f,
                    DiveReachCells = Catalog.Tuning.Save.DiveReachCells,
                    BasePercent = 90
                }
            }
        };
        var withoutDive = withDive with
        {
            Tuning = withDive.Tuning with { Save = withDive.Tuning.Save with { DiveReachCells = 0f } }
        };

        var dive = Play(withDive);
        var noDive = Play(withoutDive);

        Assert.True(dive.Saves > 0, "con diveReachCells activo tenía que haber paradas por estirada");
        Assert.Equal(0, noDive.Saves);
    }

    /// <summary>
    /// Un solo duelo por disparo. Con un radio enorme el portero está dentro de él desde el primer tick de
    /// vuelo, así que <b>todas</b> las paradas tienen que caer en el tick del propio disparo; si el flag
    /// <c>SaveAttempted</c> no cortara, un tiro perdido en el tick 1 volvería a disputarse en los ticks
    /// siguientes y aparecerían paradas retrasadas. El desfase de 1 tick es el penalti: se lanza al
    /// resolver la reanudación, en un tick en el que el motor no llega a mover el balón.
    /// </summary>
    [Fact]
    public void TheGoalkeeperOnlyDisputesEachShotOnce()
    {
        var catalog = WithReach(50f);
        int saves = 0, goals = 0;

        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, new SimConfig(CollectLog: false));
            int shotTick = -1;
            foreach (var e in result.Events)
            {
                if (e.Type == EventType.Shot && e.Detail == "onTarget")
                {
                    shotTick = e.Tick;
                }
                else if (e.Type == EventType.Save)
                {
                    saves++;
                    Assert.True(
                        shotTick >= 0 && e.Tick - shotTick <= 1,
                        $"semilla {seed}: parada en el tick {e.Tick} de un disparo del tick {shotTick}: hubo un segundo duelo");
                }
                else if (e.Type == EventType.Goal)
                {
                    goals++;
                }
            }
        }

        Assert.True(saves > 0, "el escenario tenía que producir paradas");
        Assert.True(goals > 0, "el escenario tenía que producir goles: sin duelos perdidos no prueba nada");
    }

    /// <summary>
    /// Aritmética exacta del borde (RT-023): el alcance es estrictamente menor que el radio, igual que el
    /// radio de intercepción del pase. Se comprueba sobre la geometría real de un tiro desde el centro del
    /// campo al centro de la portería: 8 casillas a 0,700 casillas/tick son 12 ticks de vuelo, y en el
    /// tick 6 el balón está en (12; 2,5).
    /// </summary>
    [Fact]
    public void TheReachBorderIsExact()
    {
        float reach = Catalog.Tuning.Save.ReachCells;
        var origin = new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow);
        var target = Pitch.GoalCenter(0);
        int ticks = FlightTicks(Vec2.Distance(origin, target), Catalog.Tuning.Ball.ShotSpeedCellsPerTickMilli);
        Assert.Equal(12, ticks);

        var ball = Vec2.Lerp(origin, target, 6 / (float)ticks);
        Assert.Equal(new Vec2(12f, 2.5f), ball);

        Assert.True(MatchEngine.WithinSaveReach(new Vec2(ball.X, ball.Y + reach - 0.01f), ball, reach));
        Assert.False(MatchEngine.WithinSaveReach(new Vec2(ball.X, ball.Y + reach + 0.01f), ball, reach));
    }

    /// <summary>
    /// AW-A paso 2: la estirada usa el mismo predicado <c>WithinSaveReach</c> que el radio normal, solo con
    /// un radio distinto (<c>diveReachCells</c>), así que el borde es igual de estricto (<c>&lt;</c>, no
    /// <c>&lt;=</c>). No hace falta un método nuevo; basta reutilizar el mismo con el radio de la estirada.
    /// </summary>
    [Fact]
    public void TheDiveReachBorderIsExact()
    {
        float diveReach = Catalog.Tuning.Save.DiveReachCells;
        var ball = new Vec2(12f, 2.5f);

        Assert.True(MatchEngine.WithinSaveReach(new Vec2(ball.X, ball.Y + diveReach - 0.01f), ball, diveReach));
        Assert.False(MatchEngine.WithinSaveReach(new Vec2(ball.X, ball.Y + diveReach + 0.01f), ball, diveReach));
    }

    /// <summary>
    /// Portero inmóvil fuera de la trayectoria: en ningún tick del vuelo llega al balón, así que no hay
    /// duelo y el tiro es gol. Se le coloca sobre su propia línea de gol pero en el borde del campo, a 2,5
    /// casillas de la recta tirador→portería (3 no caben: el campo tiene 5 filas).
    /// </summary>
    [Fact]
    public void AGoalkeeperOffTheTrajectoryIsOutOfReachEveryTick()
    {
        float reach = Catalog.Tuning.Save.ReachCells;
        var origin = new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow);
        var target = Pitch.GoalCenter(0);
        int ticks = FlightTicks(Vec2.Distance(origin, target), Catalog.Tuning.Ball.ShotSpeedCellsPerTickMilli);
        var goalkeeper = new Vec2(Pitch.Columns, 0f);

        for (int tick = 1; tick <= ticks; tick++)
        {
            var ball = Vec2.Lerp(origin, target, tick / (float)ticks);
            Assert.False(
                MatchEngine.WithinSaveReach(goalkeeper, ball, reach),
                $"tick {tick}: el portero desplazado no podía alcanzar el balón en {ball}");
        }
    }

    /// <summary>Misma división entera que <c>MatchEngine.FlightTicks</c>, que es privado.</summary>
    private static int FlightTicks(float distance, int speedMilli)
    {
        int distanceMilli = (int)(distance * 1000f);
        int ticks = (distanceMilli + speedMilli - 1) / speedMilli;
        return ticks < 1 ? 1 : ticks;
    }

    private static Catalog WithReach(float reach, float diveReach = 1.5f) =>
        Catalog with
        {
            Tuning = Catalog.Tuning with
            {
                Save = Catalog.Tuning.Save with { ReachCells = reach, DiveReachCells = diveReach }
            }
        };

    private static Totals Play(Catalog catalog)
    {
        var totals = new Totals();
        for (ulong seed = 1; seed <= Matches; seed++)
        {
            var result = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, new SimConfig(CollectLog: false));
            totals.Saves += result.Report.Saves[0] + result.Report.Saves[1];
            totals.Goals += result.Report.Goals[0] + result.Report.Goals[1];
            totals.ShotsOnTarget += result.Report.ShotsOnTarget[0] + result.Report.ShotsOnTarget[1];
            foreach (var e in result.Events)
            {
                if (e.Type == EventType.Save)
                {
                    totals.SaveEvents++;
                }
            }
        }

        return totals;
    }

    private sealed class Totals
    {
        public int Saves;
        public int Goals;
        public int ShotsOnTarget;
        public int SaveEvents;
    }
}
