using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Acciones de ataque diferenciadas y bloqueo sin balón (ADR 0030 §1 y §2, RF-051, RF-057, RT-090..098).
/// Se puntúa con los pesos reales de <c>data/ai/weights.json</c>, no con pesos sintéticos: lo que estas
/// pruebas defienden es que la <b>decisión</b> depende del jugador, y eso solo se ve con la tabla entera
/// compitiendo.
/// </summary>
public sealed class AttackActionsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// El pase largo lo elige quien tiene con qué (ADR 0030 §1): mismo escenario, mismos pesos, misma
    /// geometría; lo único que cambia es la técnica del pasador. El técnico abre el juego; el torpe ni lo
    /// considera, y su puntuación de pase largo queda por debajo de la del corto.
    /// </summary>
    [Fact]
    public void LongPassIsForTheTechnicalPasserAndNotForTheClumsyOne()
    {
        var (skilled, skilledRows) = PassScenario(technique: 95);
        var (clumsy, clumsyRows) = PassScenario(technique: 10);

        Assert.Equal(PlayerAction.LongPass, skilled);
        Assert.NotEqual(PlayerAction.LongPass, clumsy);

        Assert.True(
            Row(skilledRows, PlayerAction.LongPass).Score > Row(skilledRows, PlayerAction.ShortPass).Score,
            "un pasador técnico debía preferir el pase largo al corto");
        Assert.True(
            Row(clumsyRows, PlayerAction.LongPass).Score < Row(clumsyRows, PlayerAction.ShortPass).Score,
            "un pasador torpe debía preferir el pase corto al largo");
    }

    /// <summary>
    /// Las dos bandas son disjuntas: el receptor de cada acción es el que le corresponde por distancia y
    /// ninguno puntúa en las dos. El pase corto no puede elegir al compañero a seis casillas ni el largo
    /// al de dos.
    /// </summary>
    [Fact]
    public void EachPassBandPicksItsOwnReceiver()
    {
        var (carrier, near, far, context) = PassPlayers(technique: 95);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, carrier, rows);
        Assert.Same(far, carrier.PassReceiver);

        // Con el pase largo apagado en la tabla (y con él las otras dos acciones con balón, para que la
        // elección sea entre las dos bandas de pase y nada más), el mismo escenario elige al cercano.
        var shortOnly = WeightsWithout(PlayerAction.LongPass, PlayerAction.Dribble, PlayerAction.Shoot);
        var shortContext = Context(shortOnly, new[] { carrier, near, far, context.Players[3] });
        shortContext.Ball.Owner = carrier;
        shortContext.HoldingTeam = 0;
        Utility.Choose(shortContext, carrier, null);
        Assert.Same(near, carrier.PassReceiver);
    }

    /// <summary>
    /// El tiro pierde su corte binario (ADR 0030 §1): pasado el alcance la utilidad cae en rampa, no en
    /// escalón. Se comprueba en el propio borde, que es donde estaba el escalón: dos décimas de casilla
    /// alrededor del alcance no pueden costar más que dos décimas de rampa.
    /// </summary>
    [Fact]
    public void ShootDistancePenaltyIsContinuousAcrossTheRangeEdge()
    {
        var context = Catalog.Ai.Context;
        int range = context.ShootBaseRangeCells;

        int justInside = ShootContext(range - 0.1f);
        int justOutside = ShootContext(range + 0.1f);
        int wellOutside = ShootContext(range + 2.0f);

        int edgeDrop = justInside - justOutside;
        int rampDrop = justOutside - wellOutside;

        Assert.True(edgeDrop > 0, "un tiro más lejano nunca puede puntuar más");
        Assert.True(
            edgeDrop < rampDrop,
            $"el borde del alcance no puede ser un escalón: cayó {edgeDrop} en 0,2 casillas y {rampDrop} en 1,9");

        // Y la caída sigue siendo suave más allá: sin cortes, la rampa es proporcional a la distancia.
        Assert.True(ShootContext(range + 4.0f) < wellOutside);
    }

    /// <summary>
    /// <c>LongShot</c> modula la rampa moviendo dónde empieza (RT-094): a la misma distancia larga, el
    /// tirador lejano puntúa por encima del que no lo es, sin ningún <c>if</c> por rasgo en el motor.
    /// </summary>
    [Fact]
    public void LongShotMovesWhereTheRampStarts()
    {
        int range = Catalog.Ai.Context.ShootBaseRangeCells;
        Assert.True(ShootContext(range + 1.0f, Trait.LongShot) > ShootContext(range + 1.0f));
    }

    /// <summary>
    /// Encarar y rematar escalan con el jugador (ADR 0030 §1) y lo hacen con más pendiente que el pase
    /// corto, que es la acción por defecto: subir un atributo tiene que mover más la decisión de regatear
    /// o tirar que la de dar el pase de siempre.
    /// </summary>
    [Fact]
    public void DribbleAndShootHaveASteeperSlopeThanTheDefaultPass()
    {
        var context = Catalog.Ai.Context;
        Assert.True(context.DribbleTechniqueSlope > context.ShortPassTechniqueSlope);
        Assert.True(context.ShootTechniqueSlope > context.ShortPassTechniqueSlope);

        var (skilled, skilledRows) = PassScenario(technique: 95);
        var (clumsy, clumsyRows) = PassScenario(technique: 10);
        Assert.NotEqual(PlayerAction.Dribble, clumsy);
        Assert.NotEqual(default, skilled);

        int dribbleGap = Row(skilledRows, PlayerAction.Dribble).Score - Row(clumsyRows, PlayerAction.Dribble).Score;
        int shortGap = Row(skilledRows, PlayerAction.ShortPass).Score - Row(clumsyRows, PlayerAction.ShortPass).Score;
        Assert.True(dribbleGap > shortGap, $"la técnica debía mover más el regate ({dribbleGap}) que el pase corto ({shortGap})");
    }

    /// <summary>
    /// Límite de RF-057 (ADR 0030 §2): el bloqueo solo alcanza a un rival dentro de la <b>jugada
    /// activa</b>. Mismo rival, misma distancia de carga, misma agresividad; lo único que cambia es dónde
    /// está el balón. Lejos del balón y fuera del corredor hacia la portería atacada, la acción se
    /// descarta: no hay peleas paralelas sin relación con el juego.
    /// </summary>
    [Fact]
    public void BlockCannotReachAnOpponentOutsideTheActivePlay()
    {
        var far = BlockScenario(ballAtCarrier: false);
        Assert.True(Row(far.Rows, PlayerAction.Block).Rejected);
        Assert.NotEqual(PlayerAction.Block, far.Chosen);

        var near = BlockScenario(ballAtCarrier: true);
        Assert.False(Row(near.Rows, PlayerAction.Block).Rejected);
        Assert.True(Row(near.Rows, PlayerAction.Block).Context > 0);

        // Y es una acción que se puede elegir de verdad, no una fila condenada a perder siempre: con su
        // competidora directa apagada, el delantero agresivo se va a por el rival.
        var forced = BlockScenario(ballAtCarrier: true, weights: WeightsWithout(PlayerAction.FindSpace));
        Assert.Equal(PlayerAction.Block, forced.Chosen);
        Assert.NotNull(forced.Blocker.BlockTarget);
    }

    /// <summary>
    /// El corredor entre el balón y la portería atacada también es jugada activa (ADR 0030 §2): un rival
    /// lejos del balón pero en la trayectoria de la jugada sigue siendo un objetivo legal.
    /// </summary>
    [Fact]
    public void TheCorridorTowardsTheAttackedGoalIsAlsoActivePlay()
    {
        var blocker = Player(0, Position.Forward, new Cell(11, 2), traits: new[] { Trait.Aggressive });
        var mate = Player(1, Position.Midfielder, new Cell(4, 2));
        var target = Player(2, Position.Defender, new Cell(11, 2), team: 1);
        var keeper = Player(3, Position.Goalkeeper, new Cell(15, 2), team: 1);
        var players = new[] { blocker, mate, target, keeper };

        // El balón lo lleva un compañero muy retrasado, en la fila central: el rival está a más de
        // blockActiveRadiusCells del balón, pero justo sobre la recta balón -> portería visitante.
        mate.Position = new Vec2(4.0f, PitchConstants.CenterRow);
        blocker.Position = new Vec2(11.0f, PitchConstants.CenterRow);
        target.Position = new Vec2(11.4f, PitchConstants.CenterRow);
        keeper.Position = new Vec2(15.5f, PitchConstants.CenterRow);

        var context = Context(Catalog.Ai, players);
        context.Ball.Owner = mate;
        context.Ball.Position = mate.Position;
        context.HoldingTeam = 0;
        Assert.True(Vec2.Distance(mate.Position, target.Position) > Catalog.Ai.Context.BlockActiveRadiusCells);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, blocker, rows);
        Assert.False(Row(rows, PlayerAction.Block).Rejected);
    }

    /// <summary>El que lleva el balón no se bloquea: para eso está la entrada (RF-057, ADR 0030 §2).</summary>
    [Fact]
    public void TheBallCarrierIsNeverABlockTarget()
    {
        var scenario = BlockScenario(ballAtCarrier: true, targetCarriesBall: true);
        Assert.True(Row(scenario.Rows, PlayerAction.Block).Rejected);
    }

    // ------------------------------------------------------------------ escenarios

    private static (PlayerAction Chosen, List<UtilityRow> Rows) PassScenario(int technique)
    {
        var (carrier, _, _, context) = PassPlayers(technique);
        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, carrier, rows);
        return (chosen, rows);
    }

    /// <summary>
    /// Centrocampista con el balón, un compañero a dos casillas (banda corta) y otro a seis en línea
    /// despejada (banda larga), más un rival lejano para que el poseedor no esté presionado.
    /// </summary>
    private static (MatchPlayer Carrier, MatchPlayer Near, MatchPlayer Far, UtilityContext Context) PassPlayers(int technique)
    {
        var carrier = Player(0, Position.Midfielder, new Cell(7, 2), new Attributes(50, 50, technique, 50, 60));
        var near = Player(1, Position.Midfielder, new Cell(9, 2));
        var far = Player(2, Position.Forward, new Cell(13, 2));
        var opponent = Player(3, Position.Defender, new Cell(4, 4), team: 1);

        carrier.Position = new Vec2(7.5f, PitchConstants.CenterRow);
        near.Position = new Vec2(9.5f, PitchConstants.CenterRow);
        far.Position = new Vec2(13.5f, PitchConstants.CenterRow);
        opponent.Position = new Vec2(2.5f, 0.5f);

        var players = new[] { carrier, near, far, opponent };
        var context = Context(Catalog.Ai, players);
        context.Ball.Owner = carrier;
        context.Ball.Position = carrier.Position;
        context.HoldingTeam = 0;
        carrier.EnterState(PlayerState.Dribbling, 0);
        return (carrier, near, far, context);
    }

    private static int ShootContext(float distanceFromGoal, Trait? trait = null)
    {
        var traits = trait is null ? Array.Empty<Trait>() : new[] { trait.Value };
        var shooter = Player(0, Position.Forward, new Cell(11, 2), traits: traits);
        var keeper = Player(1, Position.Goalkeeper, new Cell(15, 2), team: 1);

        Vec2 goal = Pitch.GoalCenter(0);
        shooter.Position = new Vec2(goal.X - distanceFromGoal, PitchConstants.CenterRow);
        keeper.Position = new Vec2(15.5f, PitchConstants.CenterRow);

        var context = Context(Catalog.Ai, new[] { shooter, keeper });
        context.Ball.Owner = shooter;
        context.Ball.Position = shooter.Position;
        context.HoldingTeam = 0;
        shooter.EnterState(PlayerState.Dribbling, 0);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, shooter, rows);
        return Row(rows, PlayerAction.Shoot).Context;
    }

    /// <summary>
    /// Delantero agresivo con un rival pegado. Con <paramref name="ballAtCarrier"/> el balón lo lleva un
    /// compañero al lado, así que los dos están en la jugada; sin él, el balón está en la otra punta del
    /// campo y fuera del corredor, y ese mismo rival deja de ser un objetivo legal.
    /// </summary>
    private static (PlayerAction Chosen, List<UtilityRow> Rows, MatchPlayer Blocker) BlockScenario(
        bool ballAtCarrier, bool targetCarriesBall = false, AiWeights? weights = null)
    {
        var blocker = Player(0, Position.Forward, new Cell(11, 2), traits: new[] { Trait.Aggressive });
        var mate = Player(1, Position.Midfielder, new Cell(10, 2));
        var target = Player(2, Position.Defender, new Cell(11, 2), team: 1);
        var keeper = Player(3, Position.Goalkeeper, new Cell(15, 2), team: 1);
        var players = new[] { blocker, mate, target, keeper };

        blocker.Position = new Vec2(11.5f, 0.6f);
        target.Position = new Vec2(11.9f, 0.6f);
        keeper.Position = new Vec2(15.5f, PitchConstants.CenterRow);
        mate.Position = ballAtCarrier ? new Vec2(11.0f, 0.6f) : new Vec2(1.0f, 4.4f);

        var context = Context(weights ?? Catalog.Ai, players);
        var owner = targetCarriesBall ? target : mate;
        context.Ball.Owner = owner;
        context.Ball.Position = owner.Position;
        context.HoldingTeam = owner.Team;

        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, blocker, rows);
        return (chosen, rows, blocker);
    }

    // ------------------------------------------------------------------ ayudantes

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

    private static MatchPlayer Player(
        int id,
        Position position,
        Cell home,
        Attributes? attributes = null,
        int team = 0,
        IReadOnlyList<Trait>? traits = null)
    {
        traits ??= Array.Empty<Trait>();
        var tags = new List<string> { "Neutral", position.ToString() };
        foreach (var trait in traits)
        {
            tags.Add(trait.ToString());
        }

        var definition = new PlayerDefinition(
            id, "p" + id, Race.Human, position, Rarity.Common, 1,
            attributes ?? new Attributes(50, 50, 50, 50, 60), traits, tags, PhysicalState.Healthy);
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

    /// <summary>Los pesos reales con algunas acciones apagadas, para aislar a las que se comparan.</summary>
    private static AiWeights WeightsWithout(params PlayerAction[] disabled)
    {
        int positions = Enum.GetValues<Position>().Length;
        int actions = Enum.GetValues<PlayerAction>().Length;
        var baseTable = new int[positions, actions];
        var tacticalTable = new int[Enum.GetValues<TacticalState>().Length, actions];
        var shifts = new BlockShift[Enum.GetValues<TacticalState>().Length];

        for (int p = 0; p < positions; p++)
        {
            for (int a = 0; a < actions; a++)
            {
                baseTable[p, a] = Array.IndexOf(disabled, (PlayerAction)a) >= 0
                    ? 0
                    : Catalog.Ai.Base((Position)p, (PlayerAction)a);
            }
        }

        for (int s = 0; s < tacticalTable.GetLength(0); s++)
        {
            for (int a = 0; a < actions; a++)
            {
                tacticalTable[s, a] = Catalog.Ai.Tactical((TacticalState)s, (PlayerAction)a);
            }

            shifts[s] = Catalog.Ai.Shift((TacticalState)s);
        }

        return new AiWeights(baseTable, tacticalTable, Catalog.Ai.Context, shifts);
    }
}
