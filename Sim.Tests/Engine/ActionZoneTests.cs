using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Zona de acción con forma (ADR 0028, RF-042, RT-095, fase1b-diseno.md §2.2): forma asimétrica por
/// posición, escala por el atributo de correa, penalización creciente al salir y límite duro exterior.
/// </summary>
public sealed class ActionZoneTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// La forma la da la posición (RF-042): desde la misma casilla-hogar, un delantero —sin límite hacia
    /// delante— llega a la portería rival y un defensa —tres columnas— no. Es la asimetría por la que la
    /// correa dejó de ser un radio.
    /// </summary>
    [Fact]
    public void TheZoneIsAsymmetricByPositionSoAForwardReachesTheOpposingGoalAndADefenderDoesNot()
    {
        var home = new Cell(7, 2);
        var forward = Player(0, Position.Forward, home);
        var defender = Player(1, Position.Defender, home);
        var goal = Pitch.GoalCenter(0);

        Assert.Equal(ActionZone.Unlimited, forward.Zone.ForwardMilli);
        Assert.Equal(0f, Utility.DistanceOutsideZone(forward, goal));
        Assert.Equal(goal, Utility.ClampToZone(forward, goal));

        Assert.NotEqual(ActionZone.Unlimited, defender.Zone.ForwardMilli);
        Assert.True(Utility.DistanceOutsideZone(defender, goal) > 0f);
        Assert.True(Utility.ClampToZone(defender, goal).X < goal.X - 1f);
    }

    /// <summary>
    /// Y sigue siendo asimétrica hacia atrás: el defensa no tiene límite hacia su propia portería y el
    /// delantero solo puede bajar una casilla escalada.
    /// </summary>
    [Fact]
    public void TheZoneIsAlsoAsymmetricBackwards()
    {
        var home = new Cell(7, 2);
        var forward = Player(0, Position.Forward, home);
        var defender = Player(1, Position.Defender, home);
        var ownGoal = Pitch.GoalCenter(1);

        Assert.Equal(ActionZone.Unlimited, defender.Zone.BackMilli);
        Assert.Equal(0f, Utility.DistanceOutsideZone(defender, ownGoal));
        Assert.True(Utility.DistanceOutsideZone(forward, ownGoal) > 0f);
    }

    /// <summary>El atributo de correa escala la zona sin cambiar su forma (ADR 0028, decisión 2).</summary>
    [Fact]
    public void TheLeashAttributeScalesTheZoneWithoutChangingItsShape()
    {
        var low = Player(0, Position.Midfielder, new Cell(7, 2), leash: 1);
        var high = Player(1, Position.Midfielder, new Cell(7, 2), leash: 99);

        Assert.True(high.Zone.ForwardMilli > low.Zone.ForwardMilli);
        Assert.True(high.Zone.BackMilli > low.Zone.BackMilli);
        Assert.True(high.Zone.SidesMilli > low.Zone.SidesMilli);

        // Misma forma: las tres direcciones crecen en la misma proporción que la escala del dato.
        var scale = Catalog.Tuning.ActionZone.ScaleFromLeashPercent;
        Assert.Equal(scale.At1, low.Zone.SidesMilli * 100 / (3 * 1000));
        Assert.Equal(scale.At99, high.Zone.SidesMilli * 100 / (3 * 1000));
    }

    /// <summary>
    /// La zona es blanda (RT-095): salir no descarta, penaliza, y la penalización crece con la distancia
    /// fuera. Se mide sobre el volcado de utilidad, que es donde hay que poder verlo (RT-098).
    /// </summary>
    [Fact]
    public void LeavingTheZoneCostsMoreTheFurtherOut()
    {
        var weights = Weights(PlayerAction.ChaseBall, 1000);
        int previousContext = int.MaxValue;
        int previousOutside = -1;

        for (int cells = 4; cells <= 6; cells++)
        {
            var player = Player(0, Position.Defender, new Cell(4, 2));
            var context = Context(weights, player);
            context.Ball.Position = new Vec2(player.HomeCenter.X + cells, player.HomeCenter.Y);

            var rows = new List<UtilityRow>();
            Utility.Choose(context, player, rows);
            var row = Row(rows, PlayerAction.ChaseBall);

            Assert.False(row.Rejected);
            Assert.True(row.OutsideZone, $"a {cells} casillas el objetivo ya está fuera de la zona del defensa");
            Assert.True(row.OutsideCentiCells > previousOutside);
            Assert.True(row.Context < previousContext, $"a {cells} casillas el contexto debía bajar ({row.Context} >= {previousContext})");
            previousContext = row.Context;
            previousOutside = row.OutsideCentiCells;
        }
    }

    /// <summary>
    /// La disciplina modula esa penalización (ADR 0028, decisión 3): un enano (disciplina 80) paga más
    /// por la misma salida que un elfo (disciplina 35).
    /// </summary>
    [Fact]
    public void DisciplineScalesTheCostOfLeavingTheZone()
    {
        var weights = Weights(PlayerAction.ChaseBall, 1000);

        int dwarf = OutsideContext(weights, Race.Dwarf);
        int elf = OutsideContext(weights, Race.Elf);

        Assert.True(dwarf < elf, $"el enano debía pagar más por salirse: enano {dwarf}, elfo {elf}");
    }

    /// <summary>
    /// Y replegar gana peso conforme el jugador se aleja de su zona (RT-095): el bono se mide sobre la
    /// posición actual, no sobre el objetivo, porque el objetivo de replegar está siempre dentro.
    /// </summary>
    [Fact]
    public void RetreatGainsWeightWhileOutsideTheZone()
    {
        var weights = Weights(PlayerAction.Retreat, 100);

        var inside = Player(0, Position.Defender, new Cell(4, 2));
        inside.Position = new Vec2(inside.HomeCenter.X + 1f, inside.HomeCenter.Y);
        int insideContext = RetreatContext(weights, inside);

        var outside = Player(0, Position.Defender, new Cell(4, 2));
        outside.Position = new Vec2(outside.HomeCenter.X + 6f, outside.HomeCenter.Y);
        int outsideContext = RetreatContext(weights, outside);

        Assert.Equal(0f, Utility.DistanceOutsideZone(inside, inside.Position));
        Assert.True(Utility.DistanceOutsideZone(outside, outside.Position) > 0f);
        Assert.True(outsideContext > insideContext);
    }

    /// <summary>
    /// El límite duro exterior sí es un muro (§2.2): el movimiento se acota ahí, y es el doble de la zona
    /// según <c>outerLimitMultiplier</c>.
    /// </summary>
    [Fact]
    public void TheOuterLimitClampsTheMovement()
    {
        var player = Player(0, Position.Defender, new Cell(4, 2));
        int multiplier = Catalog.Tuning.ActionZone.OuterLimitMultiplier;
        Assert.Equal(player.Zone.ForwardMilli * multiplier / 100, player.OuterZone.ForwardMilli);

        var far = new Vec2(player.HomeCenter.X + 12f, player.HomeCenter.Y);
        var clamped = Utility.ClampToZone(player, far);

        Assert.Equal(player.HomeCenter.X + ActionZone.Cells(player.OuterZone.ForwardMilli), clamped.X, 4);
        Assert.Equal(player.HomeCenter.Y, clamped.Y, 5);
    }

    /// <summary>La zona del visitante se refleja sola: la forma se lee en el marco de ataque del equipo.</summary>
    [Fact]
    public void TheAwayTeamZoneIsMirroredWithoutItsOwnData()
    {
        var home = Player(0, Position.Forward, new Cell(7, 2));
        var away = Player(1, Position.Forward, new Cell(7, 2), team: 1);

        // El delantero local no tiene límite hacia la portería del equipo 1 (X alta) y el visitante no lo
        // tiene hacia la del equipo 0 (X baja); cada uno sí lo tiene hacia la contraria.
        Assert.Equal(0f, Utility.DistanceOutsideZone(home, Pitch.GoalCenter(0)));
        Assert.True(Utility.DistanceOutsideZone(home, Pitch.GoalCenter(1)) > 0f);
        Assert.Equal(0f, Utility.DistanceOutsideZone(away, Pitch.GoalCenter(1)));
        Assert.True(Utility.DistanceOutsideZone(away, Pitch.GoalCenter(0)) > 0f);
    }

    private static int OutsideContext(AiWeights weights, Race race)
    {
        var player = Player(0, Position.Defender, new Cell(4, 2), race: race);
        var context = Context(weights, player);
        context.Ball.Position = new Vec2(player.HomeCenter.X + 5f, player.HomeCenter.Y);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, player, rows);
        return Row(rows, PlayerAction.ChaseBall).Context;
    }

    private static int RetreatContext(AiWeights weights, MatchPlayer player)
    {
        var context = Context(weights, player);
        var rows = new List<UtilityRow>();
        Utility.Choose(context, player, rows);
        return Row(rows, PlayerAction.Retreat).Context;
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

    private static MatchPlayer Player(int id, Position position, Cell home, int team = 0, int leash = 50, Race race = Race.Human)
    {
        var definition = new PlayerDefinition(
            id, "p" + id, race, position, Rarity.Common, 1,
            new Attributes(50, 50, 50, 50, leash),
            Array.Empty<Trait>(),
            new[] { position.ToString() },
            PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
    }

    private static UtilityContext Context(AiWeights weights, params MatchPlayer[] players)
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
        context.TacticalStates[1] = TacticalState.InPossession;
        context.NearestToBall[0] = players[0];
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
