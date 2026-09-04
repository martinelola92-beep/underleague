using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Comportamiento sin balón (ADR 0022, RT-089, fase1b-diseno.md §2.3): búsqueda de espacio, presión al
/// poseedor, marcaje con asignación estable y contraste por estado táctico.
/// </summary>
public sealed class OffBallTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// <c>FindSpace</c> elige el candidato más despejado: con los rivales apiñados por arriba se va hacia
    /// abajo, y con los mismos rivales reflejados se va hacia arriba. El punto elegido está siempre más
    /// lejos del rival más cercano que la posición de partida.
    /// </summary>
    [Fact]
    public void FindSpacePicksTheClearestCandidate()
    {
        var down = ChooseSpace(mirrored: false);
        var up = ChooseSpace(mirrored: true);

        Assert.True(down < 2.5f, $"con los rivales arriba debía buscar hueco abajo, se fue a Y={down}");
        Assert.True(up > 2.5f, $"con los rivales abajo debía buscar hueco arriba, se fue a Y={up}");
    }

    /// <summary>El hueco elegido está más despejado que la posición desde la que se decide.</summary>
    [Fact]
    public void TheChosenSpaceIsClearerThanTheStartingPoint()
    {
        var (player, context) = SpaceScenario(mirrored: false);
        Utility.Choose(context, player, null);

        float before = NearestOpponent(context, player, player.Position);
        float after = NearestOpponent(context, player, player.TargetPoint);
        Assert.True(after > before, $"el hueco elegido debía estar más despejado: antes {before}, después {after}");
    }

    /// <summary>
    /// <c>PressCarrier</c> apunta al poseedor rival, y vale más cuando el que lleva el balón es el portero
    /// rival dentro de su área: es la presión a la salida (§2.3).
    /// </summary>
    [Fact]
    public void PressCarrierTargetsTheOpposingCarrierAndPaysMoreAgainstTheGoalkeeper()
    {
        var presser = Player(0, Position.Forward, new Cell(13, 2));
        var keeper = Player(1, Position.Goalkeeper, new Cell(15, 2), team: 1);
        var outfield = Player(2, Position.Defender, new Cell(13, 2), team: 1);
        var players = new[] { presser, keeper, outfield };

        // El portero visitante sale con el balón desde dentro de su propia área (X > 14).
        keeper.Position = new Vec2(15.4f, 2.5f);
        outfield.Position = new Vec2(15.4f, 2.5f);
        presser.Position = new Vec2(14.0f, 2.5f);
        Assert.True(Pitch.IsInArea(keeper.Position, keeper.Team));

        var weights = Weights(PlayerAction.PressCarrier, 100);

        var againstKeeper = Context(weights, players);
        againstKeeper.Ball.Owner = keeper;
        var keeperRows = new List<UtilityRow>();
        var chosen = Utility.Choose(againstKeeper, presser, keeperRows);
        Assert.Equal(PlayerAction.PressCarrier, chosen);
        Assert.Equal(keeper.Position, presser.TargetPoint);

        var againstOutfield = Context(weights, players);
        againstOutfield.Ball.Owner = outfield;
        var outfieldRows = new List<UtilityRow>();
        Utility.Choose(againstOutfield, presser, outfieldRows);

        Assert.True(
            Row(keeperRows, PlayerAction.PressCarrier).Context > Row(outfieldRows, PlayerAction.PressCarrier).Context,
            "presionar al portero en su salida debe valer más que presionar a un jugador de campo en el mismo sitio");
    }

    /// <summary>
    /// El marcaje se asigna una vez y se mantiene (§2.3): aunque los rivales se muevan y otro pase a estar
    /// más cerca, el defensor sigue con el suyo. Sin esto, <c>MarkOpponent</c> cambiaba de objetivo en
    /// cada decisión y no marcaba a nadie.
    /// </summary>
    [Fact]
    public void MarkingKeepsItsTargetWhileTheAssignmentIsValid()
    {
        var players = MarkingScenario();
        var scratch = new bool[players.Length];

        Marking.Assign(players, scratch, force: true);
        var defender = players[0];
        var assigned = defender.MarkTarget;
        Assert.NotNull(assigned);

        // Los rivales se mueven: el delantero al que marcaba se va al otro extremo del campo y el defensa
        // rival se le pone al lado. Sin asignación estable, el defensor cambiaría de objetivo aquí mismo.
        players[5].Position = new Vec2(14.0f, 4.5f);
        players[3].Position = new Vec2(3.6f, 1.5f);

        Marking.Assign(players, scratch, force: false);
        Assert.Same(assigned, defender.MarkTarget);

        Marking.Assign(players, scratch, force: true);
        Assert.NotSame(assigned, defender.MarkTarget);
    }

    /// <summary>Preferencia por rol (§2.3): el defensa se empareja con el delantero rival.</summary>
    [Fact]
    public void MarkingPrefersTheOpposingForwardForADefender()
    {
        var players = MarkingScenario();
        Marking.Assign(players, new bool[players.Length], force: true);

        Assert.Equal(Position.Forward, players[0].MarkTarget!.Role);
        Assert.Equal(Position.Midfielder, players[1].MarkTarget!.Role);
    }

    /// <summary>Un objetivo que sale del campo deja de ser válido y se reasigna sin rehacer el resto.</summary>
    [Fact]
    public void MarkingReassignsOnlyTheDefenderWhoseTargetLeftThePitch()
    {
        var players = MarkingScenario();
        var scratch = new bool[players.Length];
        Marking.Assign(players, scratch, force: true);

        var untouched = players[1].MarkTarget;
        players[0].MarkTarget!.LeavePitch(PlayerState.SentOff);

        Marking.Assign(players, scratch, force: false);

        Assert.NotNull(players[0].MarkTarget);
        Assert.True(players[0].MarkTarget!.OnPitch);
        Assert.Same(untouched, players[1].MarkTarget);
    }

    /// <summary>
    /// Contraste por estado táctico (ADR 0022, decisión 1) con los pesos reales de <c>data/ai/weights.json</c>:
    /// un mismo centrocampista busca espacio cuando su equipo tiene el balón y marca o presiona cuando no
    /// lo tiene, y la diferencia se lee en el volcado de utilidad (RT-098).
    /// </summary>
    [Fact]
    public void TacticalContrastIsVisibleInTheUtilityDump()
    {
        var attacking = TacticalRows(TacticalState.InPossession);
        var defending = TacticalRows(TacticalState.OutOfPossession);

        int attackFind = Row(attacking, PlayerAction.FindSpace).Score;
        int attackMark = Row(attacking, PlayerAction.MarkOpponent).Score;
        int defendFind = Row(defending, PlayerAction.FindSpace).Score;
        int defendMark = Row(defending, PlayerAction.MarkOpponent).Score;

        Assert.True(attackFind > attackMark, $"atacando debía pesar más buscar espacio ({attackFind}) que marcar ({attackMark})");
        Assert.True(defendMark > defendFind, $"defendiendo debía pesar más marcar ({defendMark}) que buscar espacio ({defendFind})");

        // Y el contraste es grande, no un matiz: la acción de ataque cae a menos de la mitad al defender.
        Assert.True(defendFind * 2 < attackFind);
        Assert.True(attackMark * 2 < defendMark);
    }

    private static List<UtilityRow> TacticalRows(TacticalState state)
    {
        var midfielder = Player(0, Position.Midfielder, new Cell(7, 2));
        var mate = Player(1, Position.Forward, new Cell(9, 2));
        var opponent = Player(2, Position.Defender, new Cell(4, 2), team: 1);
        var players = new[] { midfielder, mate, opponent };
        opponent.Position = new Vec2(9.0f, 2.5f);

        var context = Context(Catalog.Ai, players);
        context.TacticalStates[0] = state;
        context.TacticalStates[1] = state == TacticalState.InPossession
            ? TacticalState.OutOfPossession
            : TacticalState.InPossession;
        context.Ball.Owner = state == TacticalState.InPossession ? mate : opponent;
        context.Ball.Position = context.Ball.Owner.Position;
        context.HoldingTeam = context.Ball.Owner.Team;

        var rows = new List<UtilityRow>();
        Utility.Choose(context, midfielder, rows);
        return rows;
    }

    /// <summary>Tres jugadores del equipo 0 y tres del 1, en el orden por id que usa el motor.</summary>
    private static MatchPlayer[] MarkingScenario()
    {
        var players = new[]
        {
            Player(0, Position.Defender, new Cell(3, 1)),
            Player(1, Position.Midfielder, new Cell(6, 2)),
            Player(2, Position.Forward, new Cell(10, 3)),
            Player(3, Position.Defender, new Cell(12, 1), team: 1),
            Player(4, Position.Midfielder, new Cell(9, 2), team: 1),
            Player(5, Position.Forward, new Cell(5, 3), team: 1),
        };

        players[0].Position = new Vec2(3.5f, 1.5f);
        players[1].Position = new Vec2(6.5f, 2.5f);
        players[2].Position = new Vec2(10.5f, 3.5f);
        players[3].Position = new Vec2(12.5f, 1.5f);
        players[4].Position = new Vec2(9.5f, 2.5f);
        players[5].Position = new Vec2(4.0f, 2.5f);

        for (int i = 0; i < players.Length; i++)
        {
            players[i].Index = i;
        }

        return players;
    }

    private static float ChooseSpace(bool mirrored)
    {
        var (player, context) = SpaceScenario(mirrored);
        Assert.Equal(PlayerAction.FindSpace, Utility.Choose(context, player, null));
        return player.TargetPoint.Y;
    }

    /// <summary>
    /// Centrocampista del equipo 0 con el balón en poder de un compañero retrasado y tres rivales
    /// apiñados por delante y a un lado; <paramref name="mirrored"/> los pasa al otro lado.
    /// </summary>
    private static (MatchPlayer Player, UtilityContext Context) SpaceScenario(bool mirrored)
    {
        float side = mirrored ? -1f : 1f;
        var player = Player(0, Position.Midfielder, new Cell(8, 2));
        var carrier = Player(1, Position.Defender, new Cell(5, 2));
        var opponents = new[]
        {
            Player(2, Position.Defender, new Cell(9, 2), team: 1),
            Player(3, Position.Defender, new Cell(9, 3), team: 1),
            Player(4, Position.Midfielder, new Cell(8, 3), team: 1),
        };

        var players = new[] { player, carrier, opponents[0], opponents[1], opponents[2] };
        player.Position = new Vec2(8.0f, 2.5f);
        carrier.Position = new Vec2(5.0f, 2.5f);
        opponents[0].Position = new Vec2(9.0f, 2.5f + (0.5f * side));
        opponents[1].Position = new Vec2(9.5f, 2.5f + (0.7f * side));
        opponents[2].Position = new Vec2(8.5f, 2.5f + (0.9f * side));

        var context = Context(Weights(PlayerAction.FindSpace, 100), players);
        context.Ball.Owner = carrier;
        context.Ball.Position = carrier.Position;
        context.HoldingTeam = 0;
        return (player, context);
    }

    private static float NearestOpponent(UtilityContext context, MatchPlayer player, Vec2 point)
    {
        float best = float.MaxValue;
        foreach (var other in context.Players)
        {
            if (other.Team == player.Team)
            {
                continue;
            }

            float distance = Vec2.Distance(other.Position, point);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    private static UtilityRow Row(List<UtilityRow> rows, PlayerAction action)
    {
        foreach (var row in rows)
        {
            if (row.Action == action)
            {
                return row;
            }
        }

        throw new InvalidOperationException($"la tabla de utilidad no contiene la acción {action}");
    }

    private static MatchPlayer Player(int id, Position position, Cell home, int team = 0)
    {
        var definition = new PlayerDefinition(
            id, "p" + id, Race.Human, position, Rarity.Common, 1,
            new Attributes(50, 50, 50, 50, 50),
            Array.Empty<Trait>(),
            new[] { position.ToString() },
            PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
    }

    private static UtilityContext Context(AiWeights weights, MatchPlayer[] players)
    {
        for (int i = 0; i < players.Length; i++)
        {
            players[i].Index = i;
        }

        var ball = new Ball
        {
            InterceptAttempted = new bool[players.Length],
            Position = players[0].Position,
        };

        var context = new UtilityContext(players, ball, weights, Catalog.Tuning.ActionZone);
        context.TacticalStates[0] = TacticalState.InPossession;
        context.TacticalStates[1] = TacticalState.OutOfPossession;
        context.NearestToBall[0] = players[0];
        context.NearestToBall[1] = players[players.Length - 1];
        return context;
    }

    /// <summary>Pesos sintéticos: todo a 0 salvo la acción indicada, táctico neutro y contexto sin términos.</summary>
    private static AiWeights Weights(PlayerAction action, int weight)
    {
        int positions = Enum.GetValues<Position>().Length;
        int actions = Enum.GetValues<PlayerAction>().Length;
        var baseTable = new int[positions, actions];
        var tacticalTable = new int[Enum.GetValues<TacticalState>().Length, actions];
        for (int s = 0; s < tacticalTable.GetLength(0); s++)
        {
            for (int a = 0; a < actions; a++)
            {
                tacticalTable[s, a] = 100;
            }
        }

        for (int p = 0; p < positions; p++)
        {
            baseTable[p, (int)action] = weight;
        }

        var context = new AiContext(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1.2f, 0, 0, 0, 0);
        return new AiWeights(baseTable, tacticalTable, context, new BlockShift[Enum.GetValues<TacticalState>().Length]);
    }
}
