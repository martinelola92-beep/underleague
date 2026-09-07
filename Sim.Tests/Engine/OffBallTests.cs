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
    /// AW-E (docs/pendientes.md, cambio 2 de 2): hasta ahora <c>FindSpace</c> solo miraba al rival más
    /// cercano, así que dos compañeros podían converger en el mismo hueco sin que nada lo evitara. Mismo
    /// escenario que <see cref="TheChosenSpaceIsClearerThanTheStartingPoint"/>: si un compañero ya ocupa
    /// el punto que se habría elegido sin él, la elección deja de ser ese punto.
    /// </summary>
    [Fact]
    public void FindSpaceAvoidsACandidateAlreadyOccupiedByATeammate()
    {
        var (baselinePlayer, baselineContext) = SpaceScenario(mirrored: false);
        Utility.Choose(baselineContext, baselinePlayer, null);
        var previousBest = baselinePlayer.TargetPoint;

        var (crowdedPlayer, crowdedContext) = SpaceScenario(mirrored: false, crowdedAt: previousBest);
        Utility.Choose(crowdedContext, crowdedPlayer, null);

        Assert.NotEqual(previousBest, crowdedPlayer.TargetPoint);
    }

    /// <summary>
    /// AW-Q (docs/pendientes.md): el desmarque ya no puede elegir una casilla más allá de la línea
    /// defensiva rival más <c>findSpaceLineMarginCells</c>. El escenario pone el balón exactamente en la
    /// columna del rival más retrasado, así que la línea es esa columna sin ambigüedad. A/B con el mismo
    /// escenario: con el margen abierto de par en par el hueco elegido queda por delante del techo, y con
    /// el margen real de una casilla el objetivo se recorta al techo.
    /// </summary>
    [Fact]
    public void FindSpaceDoesNotPickACellBeyondTheOpponentDefensiveLine()
    {
        var (uncapped, uncappedPlayer) = LineScenario(lineMargin: 20f);
        Utility.Choose(uncapped, uncappedPlayer, null);

        float line = Utility.OffsideLineColumn(uncapped.Players, uncapped.Ball.Position, 0);
        Assert.Equal(12f, line);

        Assert.True(
            uncappedPlayer.TargetPoint.X > line + 1f,
            $"sin techo el desmarque debía irse más allá de la línea+1 ({line + 1f}), se fue a X={uncappedPlayer.TargetPoint.X}");

        var (capped, cappedPlayer) = LineScenario(lineMargin: 1f);
        Utility.Choose(capped, cappedPlayer, null);

        Assert.True(
            cappedPlayer.TargetPoint.X <= line + 1f + 0.001f,
            $"con techo el desmarque no podía pasar de X={line + 1f}, se fue a X={cappedPlayer.TargetPoint.X}");
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

    /// <summary>
    /// AW-S (docs/pendientes.md): ChaseBall pasa de penalizar a descalificar. Con dos defensas del mismo
    /// equipo, solo el designado por <c>ctx.NearestToBall[team]</c> puede perseguir el balón; el resto la
    /// tiene descartada del todo, no solo penalizada.
    /// </summary>
    [Fact]
    public void ChaseBallIsDiscardedForADefenderWhoIsNotTheDesignatedChaserNorAPassReceiver()
    {
        var rows = ChaseBallScenario(isNearest: false, isIncomingPassReceiver: false);
        Assert.True(Row(rows, PlayerAction.ChaseBall).Rejected);
    }

    /// <summary>
    /// El mismo jugador, ahora designado como el más cercano al balón de su equipo, recupera ChaseBall
    /// como acción legal con el mismo cálculo de siempre: sin ninguna resta de penalización, porque
    /// <c>chaseBallNotNearestPenalty</c> ya no existe ni en el motor ni en los datos.
    /// </summary>
    [Fact]
    public void ChaseBallIsLegalForTheDesignatedChaserWithTheSameScoreAsBefore()
    {
        var rows = ChaseBallScenario(isNearest: true, isIncomingPassReceiver: false);
        var row = Row(rows, PlayerAction.ChaseBall);
        Assert.False(row.Rejected);

        // La distancia (2 casillas, dentro de la zona blanda de 3 del defensa, así que sin
        // OutsidePenalty de por medio) es la misma con la que se construye el escenario.
        int distanceCenti = Utility.Centi(2.0f);
        int expected = -(Catalog.Ai.Context.ChaseBallDistancePenaltyPerCell * distanceCenti / 100)
            + Catalog.Ai.Context.ChaseBallLooseBonus;
        Assert.Equal(expected, row.Context);
    }

    /// <summary>
    /// AW-S conserva la excepción del paquete E: el receptor previsto de un pase en vuelo sigue pudiendo
    /// perseguir el balón aunque no sea el compañero más cercano de su equipo.
    /// </summary>
    [Fact]
    public void ChaseBallStaysLegalForTheIncomingPassReceiverEvenWhenNotTheNearest()
    {
        var rows = ChaseBallScenario(isNearest: false, isIncomingPassReceiver: true);
        Assert.False(Row(rows, PlayerAction.ChaseBall).Rejected);
    }

    /// <summary>
    /// AW-R (docs/pendientes.md): con el balón muerto (aparcado para una reanudación) ChaseBall sigue
    /// siendo legal para el designado, pero deja de llevar el bono de "balón suelto"
    /// (<c>chaseBallLooseBonus</c>) — nadie debe converger sobre un balón que no se va a mover. El resto
    /// de la fórmula (candidato designado, penalización por distancia) no cambia.
    /// </summary>
    [Fact]
    public void ChaseBallDropsTheLooseBonusWhenTheBallIsDead()
    {
        var alive = ChaseBallScenario(isNearest: true, isIncomingPassReceiver: false, ballDead: false);
        var dead = ChaseBallScenario(isNearest: true, isIncomingPassReceiver: false, ballDead: true);

        var aliveRow = Row(alive, PlayerAction.ChaseBall);
        var deadRow = Row(dead, PlayerAction.ChaseBall);

        Assert.False(aliveRow.Rejected);
        Assert.False(deadRow.Rejected);
        Assert.Equal(aliveRow.Context - Catalog.Ai.Context.ChaseBallLooseBonus, deadRow.Context);
    }

    /// <summary>
    /// Escenario común a los tests de AW-S y AW-R: dos defensas del equipo 0, el jugador 0 (siempre el
    /// evaluado) en su casilla-hogar, con el balón suelto 2 casillas por delante (dentro de las 3 de la
    /// zona blanda del defensa, así que la penalización de salir de zona no interfiere en el cálculo).
    /// <paramref name="isNearest"/> designa quién es <c>ctx.NearestToBall[0]</c>; <paramref
    /// name="isIncomingPassReceiver"/> convierte el balón en un pase en vuelo con el jugador 0 como
    /// receptor previsto (y deja al compañero como designado). <paramref name="ballDead"/> (AW-R) marca
    /// el balón como aparcado para una reanudación.
    /// </summary>
    private static List<UtilityRow> ChaseBallScenario(
        bool isNearest, bool isIncomingPassReceiver, bool ballDead = false)
    {
        var player = Player(0, Position.Defender, new Cell(4, 2));
        var teammate = Player(1, Position.Defender, new Cell(9, 2));
        var players = new[] { player, teammate };

        var context = Context(Catalog.Ai, players);
        context.Ball.Position = player.HomeCenter + new Vec2(2.0f, 0f);
        context.NearestToBall[0] = isNearest ? player : teammate;
        context.BallDead = ballDead;

        if (isIncomingPassReceiver)
        {
            context.Ball.InFlight = true;
            context.Ball.IsShot = false;
            context.Ball.FlightTarget = context.Ball.Position;
            context.Ball.PassReceiver = player;
        }

        var rows = new List<UtilityRow>();
        Utility.Choose(context, player, rows);
        return rows;
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
    /// <paramref name="crowdedAt"/> añade un cuarto compañero ahí, para AW-E (cambio 2 de 2): confirmar
    /// que un candidato ya ocupado deja de ser el elegido.
    /// </summary>
    private static (MatchPlayer Player, UtilityContext Context) SpaceScenario(bool mirrored, Vec2? crowdedAt = null)
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

        var players = new List<MatchPlayer> { player, carrier, opponents[0], opponents[1], opponents[2] };
        player.Position = new Vec2(8.0f, 2.5f);
        carrier.Position = new Vec2(5.0f, 2.5f);
        opponents[0].Position = new Vec2(9.0f, 2.5f + (0.5f * side));
        opponents[1].Position = new Vec2(9.5f, 2.5f + (0.7f * side));
        opponents[2].Position = new Vec2(8.5f, 2.5f + (0.9f * side));

        if (crowdedAt is { } point)
        {
            var teammate = Player(5, Position.Midfielder, new Cell(8, 2));
            teammate.Position = point;
            players.Add(teammate);
        }

        var context = Context(Weights(PlayerAction.FindSpace, 100), players.ToArray());
        context.Ball.Owner = carrier;
        context.Ball.Position = carrier.Position;
        context.HoldingTeam = 0;
        return (player, context);
    }

    /// <summary>
    /// Escenario de AW-Q: poseedor propio y balón en la columna 12, el rival más retrasado también en la
    /// 12 —así la línea defensiva rival es 12 con cualquier lectura— y el jugador que se desmarca justo
    /// delante, con todo el campo libre por delante para que sin techo se vaya más allá de la línea.
    /// </summary>
    private static (UtilityContext Context, MatchPlayer Player) LineScenario(float lineMargin)
    {
        var player = Player(0, Position.Forward, new Cell(12, 2));
        var carrier = Player(1, Position.Midfielder, new Cell(12, 2));
        var deepest = Player(2, Position.Defender, new Cell(12, 2), team: 1);
        var other = Player(3, Position.Defender, new Cell(10, 2), team: 1);

        player.Position = new Vec2(12.0f, 2.5f);
        carrier.Position = new Vec2(12.0f, 2.5f);
        deepest.Position = new Vec2(12.0f, 0.5f);
        other.Position = new Vec2(10.0f, 4.5f);

        var context = Context(
            Weights(PlayerAction.FindSpace, 100, lineMargin),
            new[] { player, carrier, deepest, other });
        context.Ball.Owner = carrier;
        context.Ball.Position = carrier.Position;
        context.HoldingTeam = 0;
        return (context, player);
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
    private static AiWeights Weights(PlayerAction action, int weight, float lineMargin = 1.0f)
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

        // Contexto sintético: todo a cero salvo los términos de las dos acciones que este fichero prueba.
        // Desde el paquete V viven en data/ai/weights.json (§4, decisión 20, saldada), así que el test los
        // declara aquí con los mismos valores en vez de heredarlos de una constante de Utility.
        var context = new AiContext(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1.2f, 0, 0, 0, 0,
            FindSpaceOpponentDistanceBonusPerCell: 70,
            FindSpaceAdvanceBonusPerCell: 60,
            FindSpaceOpenLaneBonus: 200,
            PressCarrierBonus: 120,
            PressDistancePenaltyPerCell: 60,
            PressGoalkeeperExitBonus: 200,
            FindSpaceCrowdedPenalty: 90, // AW-E (docs/pendientes.md, cambio 2 de 2)
            FindSpaceLineMarginCells: lineMargin); // AW-Q (docs/pendientes.md)
        return new AiWeights(baseTable, tacticalTable, context, new BlockShift[Enum.GetValues<TacticalState>().Length]);
    }
}
