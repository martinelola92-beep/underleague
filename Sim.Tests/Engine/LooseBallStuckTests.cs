using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BC-G (docs/pendientes/BC-G.md, ADR 0177): el balón se queda suelto en el córner y nadie lo coge.
///
/// <para><b>La causa, CONFIRMED con el volcado de utilidad (RT-098).</b> AW-S deja a un solo jugador por equipo
/// con derecho a perseguir un balón suelto —el más cercano—, y la utilidad lo hacía <i>competir</i> contra las
/// acciones de colocación. Con el balón parado en el córner, el designado (un defensa a 0,5 casillas)
/// puntuaba <c>CoverSpace</c> en 790 y <c>ChaseBall</c> en 631: como los demás no pueden perseguir, nadie
/// iba. Ahora el designado de un balón suelto VA (<c>Utility.HasLooseBallDuty</c>).</para>
///
/// <para><b>El instrumento</b> (Regla J): un episodio es una racha de al menos <see cref="MinTicks"/> ticks
/// con el balón en juego, sin dueño, sin vuelo y sin moverse. Medido antes del arreglo con 300 partidos de
/// referencia (semillas 1..300): <b>58 episodios, el más largo de 656 ticks (44 s)</b>, 13 de ellos de más
/// de 40 ticks (árbol previo a la rama). Sobre `main` rebasado, los bloqueos largos están en las semillas 32 (180 ticks), 173 (276), 355 (471) y 398 (374); la 141 (285) apareció con el primer arreglo.</para>
/// </summary>
public sealed class LooseBallStuckTests
{
    private const int MinTicks = 15;
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private readonly ITestOutputHelper _output;

    public LooseBallStuckTests(ITestOutputHelper output) => _output = output;

    internal readonly record struct Episode(ulong Seed, int StartTick, int Length, Vec2 Ball);

    internal static List<Episode> Measure(Catalog catalog, IEnumerable<ulong> seeds)
    {
        var episodes = new List<Episode>();
        foreach (ulong seed in seeds)
        {
            var trace = Simulator.Run(TestMatches.Reference(catalog, seed), seed, catalog, SimConfig.Default with { Trace = true }).Trace!;
            int run = 0;
            int start = 0;
            for (int f = 1; f <= trace.FrameCount; f++)
            {
                bool still = f < trace.FrameCount
                    && trace.PhaseAt(f) == MatchPhase.OpenPlay
                    && trace.RestartAt(f) == RestartKind.None
                    && trace.BallOwnerAt(f) < 0
                    && !trace.BallInFlightAt(f)
                    && Vec2.Distance(trace.BallAt(f), trace.BallAt(f - 1)) < 0.02f;
                if (still)
                {
                    if (run == 0)
                    {
                        start = f;
                    }

                    run++;
                    continue;
                }

                if (run >= MinTicks)
                {
                    episodes.Add(new Episode(seed, trace.TickAt(start), run, trace.BallAt(start)));
                }

                run = 0;
            }
        }

        return episodes;
    }

    private static IEnumerable<ulong> Range(int count) => Enumerable.Range(1, count).Select(i => (ulong)i);

    /// <summary>
    /// Las semillas del bloqueo largo medido antes del arreglo (main: 180, 276, 471 y 374 ticks con el balón quieto
    /// en el córner; la 141, 285 ticks con el portero designado sin alcance, BB-G2). Ninguna puede volver a tener más de 60 ticks: es la causa reproducida.
    /// </summary>
    [Fact]
    public void TheSeedsThatFrozeTheBallForHalfAMinuteNoLongerDo()
    {
        var episodes = Measure(Catalog, new ulong[] { 32, 173, 355, 398, 141 });
        foreach (var e in episodes)
        {
            _output.WriteLine($"seed {e.Seed} tick {e.StartTick} len {e.Length} ball {e.Ball}");
        }

        int longest = episodes.Count == 0 ? 0 : episodes.Max(e => e.Length);
        Assert.True(longest <= 60, $"el balón volvió a quedar quieto {longest} ticks (main: 180/276/471/374)");
    }

    /// <summary>
    /// En bloque: 150 partidos. Medido antes del arreglo (300 partidos: 58 episodios, 13 de más de 40 ticks);
    /// después (con la designación que sólo cuenta a quien puede llegar), 4-11 episodios en 150 partidos según el árbol (sobre `main` rebasado y separación 0,8: 11) y el más largo de ≤ 40 ticks. Los topes dejan holgura de muestra y siguen siendo
    /// una fracción de lo de antes.
    /// </summary>
    [Fact]
    public void TheBallDoesNotStayLooseInTheCorner()
    {
        int matches = 150;
        var episodes = Measure(Catalog, Range(matches));
        int longest = episodes.Count == 0 ? 0 : episodes.Max(e => e.Length);
        _output.WriteLine($"partidos {matches} · episodios ≥{MinTicks} ticks {episodes.Count} · más largo {longest}");

        Assert.True(episodes.Count <= 22, $"{episodes.Count} episodios de balón quieto ≥ {MinTicks} ticks en {matches} partidos (main ≈ 29; con el arreglo 10-18 según el árbol, todos cortos)");
        Assert.True(longest <= 40, $"el más largo duró {longest} ticks (antes 656)");
    }

    // ------------------------------------------------------------------ el deber, con los pesos reales

    /// <summary>
    /// Defensa del equipo 0 a 0,5 casillas de un balón parado en su propio córner (el caso de la semilla 79,
    /// tick 900) con los pesos REALES de <c>data/ai/weights.json</c>, y un compañero lejano que no es el más
    /// cercano.
    /// </summary>
    private static (MatchPlayer Nearest, MatchPlayer Other, UtilityContext Context) CornerScenario()
    {
        var nearest = Player(2, Position.Defender, new Cell(1, 4), team: 0);
        var other = Player(3, Position.Defender, new Cell(1, 2), team: 0);
        var opponent = Player(101, Position.Forward, new Cell(8, 3), team: 1);

        nearest.Position = new Vec2(0.5f, 6.5f);
        other.Position = new Vec2(1.5f, 2.5f);
        opponent.Position = new Vec2(8.5f, 3.5f);

        var players = new[] { nearest, other, opponent };
        for (int i = 0; i < players.Length; i++)
        {
            players[i].Index = i;
        }

        var ball = new Ball
        {
            InterceptAttempted = new bool[players.Length],
            Position = new Vec2(0.5f, 7f),
        };

        var context = new UtilityContext(players, ball, Catalog.Ai, Catalog.Tuning.ActionZone, Catalog.Tuning.Pass.InterceptRadiusCells);
        context.TacticalStates[0] = TacticalState.OutOfPossession;
        context.TacticalStates[1] = TacticalState.InPossession;
        context.NearestToBall[0] = nearest;
        context.NearestToBall[1] = opponent;
        context.HoldingTeam = -1;
        return (nearest, other, context);
    }

    private static MatchPlayer Player(int id, Position position, Cell home, int team)
    {
        var definition = new PlayerDefinition(
            id, "p" + id, Race.Human, position, Rarity.Common, 1,
            new Attributes(50, 50, 50, 50, 50),
            Array.Empty<Trait>(),
            new[] { position.ToString() },
            PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
    }

    private static UtilityRow Row(List<UtilityRow> rows, PlayerAction action) => rows.Single(r => r.Action == action);

    /// <summary>
    /// El escenario es un bloqueo REAL con los pesos de los datos: sin el deber, <c>CoverSpace</c> puntúa más
    /// que <c>ChaseBall</c> (control: la fila de la tabla lo enseña), y aun así el designado va a por el balón.
    /// </summary>
    [Fact]
    public void TheDesignatedChaserGoesForALooseBallEvenIfCoveringScoresMore()
    {
        var (nearest, _, context) = CornerScenario();
        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, nearest, rows);

        var chase = Row(rows, PlayerAction.ChaseBall);
        var cover = Row(rows, PlayerAction.CoverSpace);
        Assert.False(chase.Rejected, "ChaseBall debía ser viable en el escenario: si no, la prueba no mide el deber");
        Assert.True(
            cover.Score > chase.Score,
            $"control: el escenario debía ser el bloqueo real (CoverSpace {cover.Score} > ChaseBall {chase.Score}); con los pesos solos no se rompe");
        Assert.True(cover.Rejected, "el deber debía descartar la colocación");
        Assert.Equal(PlayerAction.ChaseBall, chosen);
        Assert.Equal(context.Ball.Position, nearest.TargetPoint);
    }

    /// <summary>El deber es del designado: los demás compañeros siguen decidiendo como siempre.</summary>
    [Fact]
    public void OnlyTheDesignatedChaserHasTheDuty()
    {
        var (_, other, context) = CornerScenario();
        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, other, rows);

        Assert.NotEqual(PlayerAction.ChaseBall, chosen);
        Assert.True(Row(rows, PlayerAction.ChaseBall).Rejected, "AW-S: el que no es el designado no puede perseguir");
        Assert.False(Row(rows, PlayerAction.CoverSpace).Rejected, "la colocación del que no es el designado no debía ceder");
    }

    /// <summary>Sin balón suelto no hay deber: con un rival llevándolo, el designado compara como siempre.</summary>
    [Fact]
    public void ThereIsNoDutyWhileSomeoneCarriesTheBall()
    {
        var (nearest, _, context) = CornerScenario();
        var carrier = context.Players[2];
        carrier.Position = new Vec2(2.5f, 5.5f);
        context.Ball.Owner = carrier;
        context.Ball.Position = carrier.Position;
        context.HoldingTeam = 1;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, nearest, rows);
        Assert.False(Row(rows, PlayerAction.CoverSpace).Rejected, "con un portador rival la colocación no cede");
        Assert.False(Row(rows, PlayerAction.Retreat).Rejected);
    }

    /// <summary>Un balón en vuelo tampoco es un balón suelto.</summary>
    [Fact]
    public void ThereIsNoDutyWhileTheBallIsInFlight()
    {
        var (nearest, _, context) = CornerScenario();
        context.Ball.InFlight = true;
        context.Ball.FlightTarget = new Vec2(0.5f, 7f);
        context.HoldingTeam = 1;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, nearest, rows);
        Assert.False(Row(rows, PlayerAction.CoverSpace).Rejected);
    }

    /// <summary>Un balón suelto que todavía rueda no obliga a nadie: el deber es del balón quieto (ADR 0177).</summary>
    [Fact]
    public void ThereIsNoDutyForARollingLooseBall()
    {
        var (nearest, _, context) = CornerScenario();
        context.Ball.Velocity = new Vec2(0.15f, 0f);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, nearest, rows);
        Assert.False(Row(rows, PlayerAction.CoverSpace).Rejected);
    }

    /// <summary>Un balón aparcado para una reanudación no se persigue (AW-R): el deber tampoco existe.</summary>
    [Fact]
    public void ThereIsNoDutyForAParkedRestartBall()
    {
        var (nearest, _, context) = CornerScenario();
        context.BallDead = true;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, nearest, rows);
        Assert.False(Row(rows, PlayerAction.CoverSpace).Rejected);
    }

    /// <summary>
    /// Un deber que no se puede cumplir no deja al jugador sin nada que hacer: si ChaseBall no es viable
    /// (el balón queda más allá de su límite exterior de zona) la colocación sigue compitiendo.
    /// </summary>
    [Fact]
    public void ADutyThatCannotBeMetLeavesThePositioningInPlace()
    {
        var (nearest, _, context) = CornerScenario();
        // El defensa ya está en el borde de su límite exterior (el doble de su zona hacia delante y hacia el
        // lado) y el balón queda más allá: perseguirlo no lo movería ni un paso.
        nearest.Position = new Vec2(7.5f, 0.5f);
        context.Ball.Position = new Vec2(15.5f, 0.5f);
        context.NearestToBall[0] = nearest;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, nearest, rows);
        Assert.True(Row(rows, PlayerAction.ChaseBall).Rejected, "ChaseBall debía estar descartada por el límite exterior");
        Assert.False(Row(rows, PlayerAction.CoverSpace).Rejected, "si el deber no se puede cumplir, la colocación no cede");
    }
}
