using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// IA de utilidad (§3.5, RT-095, RT-097): desempate por el orden del enum, límite duro exterior de la
/// zona de acción y acumulación de multiplicadores de rasgo (RT-094). La zona blanda tiene sus propios
/// tests en <see cref="ActionZoneTests"/>.
/// </summary>
public sealed class UtilityTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void TieGoesToTheFirstActionOfTheEnum()
    {
        // MarkOpponent (índice 1) y CoverSpace (índice 3) empatan en 100; gana la primera del enum.
        var weights = Weights(builder =>
        {
            builder[(int)Position.Defender, (int)PlayerAction.MarkOpponent] = 100;
            builder[(int)Position.Defender, (int)PlayerAction.CoverSpace] = 100;
        });

        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        var opponent = Player(100, Position.Forward, new Cell(13, 2), new Attributes(50, 50, 50, 50, 50), team: 1);
        opponent.Position = new Vec2(3.5f, 2.5f);

        var context = Context(weights, player, opponent);
        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, player, rows);

        Assert.Equal(PlayerAction.MarkOpponent, chosen);
        Assert.Equal(100, Row(rows, PlayerAction.MarkOpponent).Score);
        Assert.Equal(100, Row(rows, PlayerAction.CoverSpace).Score);
    }

    /// <summary>
    /// El límite duro exterior (§2.2) es la única forma de descarte espacial que queda: un jugador ya
    /// pegado a él, con el objetivo todavía más lejos, no gana nada yendo y la acción se descarta.
    /// </summary>
    [Fact]
    public void TheOuterLimitDiscardsAnActionThatCannotAdvanceAnyFurther()
    {
        var weights = Weights(builder =>
        {
            builder[(int)Position.Defender, (int)PlayerAction.ChaseBall] = 1000;
            builder[(int)Position.Defender, (int)PlayerAction.Retreat] = 100;
        });

        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 1));
        float limit = ActionZone.Cells(player.OuterZone.ForwardMilli);
        player.Position = new Vec2(player.HomeCenter.X + limit, player.HomeCenter.Y);

        var context = Context(weights, player);
        context.Ball.Position = new Vec2(player.HomeCenter.X + limit + 4f, player.HomeCenter.Y);

        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, player, rows);

        Assert.True(Row(rows, PlayerAction.ChaseBall).Rejected);
        Assert.NotEqual(PlayerAction.ChaseBall, chosen);
    }

    /// <summary>Dentro del límite duro la acción no se descarta aunque salga de la zona blanda.</summary>
    [Fact]
    public void AnActionThatStillAdvancesIsNotDiscarded()
    {
        var weights = Weights(builder =>
        {
            builder[(int)Position.Defender, (int)PlayerAction.ChaseBall] = 1000;
            builder[(int)Position.Defender, (int)PlayerAction.Retreat] = 100;
        });

        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 1));
        var context = Context(weights, player);
        context.Ball.Position = new Vec2(player.HomeCenter.X + 9f, player.HomeCenter.Y);

        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, player, rows);

        Assert.False(Row(rows, PlayerAction.ChaseBall).Rejected);
        Assert.Equal(PlayerAction.ChaseBall, chosen);
    }

    [Fact]
    public void TraitMultipliersAccumulateInSequence()
    {
        // Aggressive: Tackle 160. Dirty: Tackle 125. Acumulado entero: 100*160/100*125/100 = 200.
        var aggressive = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50), traits: new[] { Trait.Aggressive });
        var both = Player(1, Position.Defender, new Cell(2, 3), new Attributes(50, 50, 50, 50, 50), traits: new[] { Trait.Aggressive, Trait.Dirty });

        Assert.Equal(100, aggressive.ActionMultiplier(PlayerAction.Retreat));
        Assert.Equal(160, aggressive.ActionMultiplier(PlayerAction.Tackle));
        Assert.Equal(200, both.ActionMultiplier(PlayerAction.Tackle));
        Assert.Equal(120, both.ActionMultiplier(PlayerAction.MarkOpponent));
    }

    [Fact]
    public void TraitMultiplierIsAppliedToTheScore()
    {
        var weights = Weights(builder => builder[(int)Position.Defender, (int)PlayerAction.Tackle] = 300);
        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50), traits: new[] { Trait.Aggressive, Trait.Dirty });
        var context = Context(weights, player);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, player, rows);

        var row = Row(rows, PlayerAction.Tackle);
        Assert.Equal(300, row.Base);
        Assert.Equal(100, row.TacticalMultiplier);
        Assert.Equal(200, row.TraitMultiplier);
        Assert.Equal(600, row.Score);
    }

    /// <summary>
    /// Leader (RT-094): el bono del compañero con casilla-hogar contigua multiplica la puntuación base,
    /// dentro del multiplicador de rasgos de la fórmula de §3.5.
    /// </summary>
    [Fact]
    public void LeaderBonusMultipliesTheBaseScore()
    {
        var weights = Weights(builder => builder[(int)Position.Defender, (int)PlayerAction.Retreat] = 300);
        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        player.LeaderBonusPercent = 8;
        var context = Context(weights, player);

        var rows = new List<UtilityRow>();
        Utility.Choose(context, player, rows);

        var row = Row(rows, PlayerAction.Retreat);
        Assert.Equal(108, row.TraitMultiplier);
        Assert.Equal(324, row.Base * row.TacticalMultiplier / 100 * row.TraitMultiplier / 100);
    }

    /// <summary>Casillas-hogar contiguas incluidas las diagonales; una casilla no es contigua a sí misma.</summary>
    [Fact]
    public void AdjacentHomeCellsIncludeDiagonalsButNotTheSameCell()
    {
        Assert.True(Pitch.AreAdjacent(new Cell(2, 2), new Cell(3, 3)));
        Assert.True(Pitch.AreAdjacent(new Cell(2, 2), new Cell(2, 1)));
        Assert.False(Pitch.AreAdjacent(new Cell(2, 2), new Cell(2, 2)));
        Assert.False(Pitch.AreAdjacent(new Cell(2, 2), new Cell(4, 2)));
    }

    /// <summary>El enfriamiento de entrada descarta Tackle mientras dura (§3.5, paquete E).</summary>
    [Fact]
    public void TackleIsDiscardedWhileTheCooldownLasts()
    {
        var weights = Weights(builder =>
        {
            builder[(int)Position.Defender, (int)PlayerAction.Tackle] = 1000;
            builder[(int)Position.Defender, (int)PlayerAction.Retreat] = 100;
        });

        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        var carrier = Player(100, Position.Forward, new Cell(13, 2), new Attributes(50, 50, 50, 50, 50), team: 1);
        carrier.Position = player.Position;

        var context = Context(weights, player, carrier);
        context.Ball.Owner = carrier;

        var rows = new List<UtilityRow>();
        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, player, rows));

        player.TackleCooldown = 1;
        rows.Clear();
        Assert.NotEqual(PlayerAction.Tackle, Utility.Choose(context, player, rows));
        Assert.True(Row(rows, PlayerAction.Tackle).Rejected);
    }

    /// <summary>
    /// AW-Q (docs/pendientes.md): aritmética exacta de la línea defensiva, con los dos equipos y con el
    /// balón por delante y por detrás del defensa más retrasado. La línea es la columna del defensa de
    /// campo más retrasado del equipo, o la del balón si el balón está más adelantado que él hacia la
    /// portería rival, lo que esté más avanzado de los dos (docs/referencia-motores-futbol.md §6.1).
    /// </summary>
    [Fact]
    public void TheDefensiveLineIsTheDeepestOutfielderOrTheBallWhicheverIsFurtherUp()
    {
        // Equipo 0: portería propia en la columna 0, ataca hacia columnas crecientes.
        var home = new[]
        {
            Outfield(0, new Vec2(3f, 2.5f), team: 0),
            Outfield(1, new Vec2(5f, 2.5f), team: 0),
        };

        // Balón por detrás del defensa más retrasado: manda el defensa (columna 3).
        Assert.Equal(3f, Utility.DefensiveLineColumn(home, new Vec2(1f, 2.5f), 0));

        // Balón por delante de los dos: manda el balón (columna 7), la línea sube con él.
        Assert.Equal(7f, Utility.DefensiveLineColumn(home, new Vec2(7f, 2.5f), 0));

        // Equipo 1: portería propia en la columna 16, ataca hacia columnas decrecientes; el defensa más
        // retrasado es el de mayor columna. Avance del defensa = (12-16)·-1 = 4, del balón = (14-16)·-1 = 2,
        // así que manda el defensa: 16 + 4·-1 = 12.
        var away = new[]
        {
            Outfield(2, new Vec2(12f, 2.5f), team: 1),
            Outfield(3, new Vec2(9f, 2.5f), team: 1),
        };
        Assert.Equal(12f, Utility.DefensiveLineColumn(away, new Vec2(14f, 2.5f), 1));
        Assert.Equal(8f, Utility.DefensiveLineColumn(away, new Vec2(8f, 2.5f), 1));
    }

    /// <summary>
    /// AW-Q: el portero no cuenta para la línea (lo excluye <c>IsOutfield</c>) y un jugador fuera del campo
    /// tampoco. Sin jugadores de campo sobre el césped el caso degenera en la columna del balón, nunca por
    /// detrás de la propia línea de gol: es el comportamiento que este test fija.
    /// </summary>
    [Fact]
    public void TheDefensiveLineIgnoresTheGoalkeeperAndPlayersOffThePitch()
    {
        var keeper = Player(10, Position.Goalkeeper, new Cell(1, 2), new Attributes(50, 50, 50, 50, 50));
        keeper.Position = new Vec2(0.5f, 2.5f);
        var sentOff = Outfield(11, new Vec2(1f, 2.5f), team: 0);
        sentOff.OnPitch = false;
        var defender = Outfield(12, new Vec2(6f, 2.5f), team: 0);

        // Con el portero en la 0,5 y el expulsado en la 1, la línea sigue siendo la del defensa (6).
        Assert.Equal(6f, Utility.DefensiveLineColumn(new[] { keeper, sentOff, defender }, new Vec2(4f, 2.5f), 0));

        // Sin ningún jugador de campo sobre el césped, la línea es la del balón.
        Assert.Equal(4f, Utility.DefensiveLineColumn(new[] { keeper, sentOff }, new Vec2(4f, 2.5f), 0));
        Assert.Equal(12f, Utility.DefensiveLineColumn(Array.Empty<MatchPlayer>(), new Vec2(12f, 2.5f), 1));
    }

    /// <summary>
    /// AW-Q, techo del bloque: el recorte solo frena. Una casilla-hogar por delante de la línea propia más
    /// el margen se recorta al techo; una por detrás no se toca (el techo nunca empuja hacia adelante), y
    /// una justo en el techo tampoco. Es la aritmética exacta que aplica <c>MatchEngine.UpdateBlockShift</c>
    /// a los defensas de campo, extraída a función pura para poder probarla sin montar un partido (mismo
    /// criterio que <c>MatchEngine.WithinSaveReach</c> en AW-A).
    /// </summary>
    [Fact]
    public void TheBlockCeilingOnlyPullsBack()
    {
        // Equipo 0: línea en la columna 4, margen 1,5 -> techo en la 5,5.
        Assert.Equal(5.5f, Utility.CapToDefensiveLine(8f, 4f, 1.5f, 1));
        Assert.Equal(5.5f, Utility.CapToDefensiveLine(5.5f, 4f, 1.5f, 1));
        Assert.Equal(2f, Utility.CapToDefensiveLine(2f, 4f, 1.5f, 1));

        // Equipo 1: ataca hacia columnas decrecientes, así que el techo está en 12 - 1,5 = 10,5 y lo que
        // se recorta es lo que queda por DEBAJO de esa columna.
        Assert.Equal(10.5f, Utility.CapToDefensiveLine(8f, 12f, 1.5f, -1));
        Assert.Equal(14f, Utility.CapToDefensiveLine(14f, 12f, 1.5f, -1));
    }

    /// <summary>
    /// AW-Q: <c>OffsideLineColumn</c> es la línea que afronta el ATACANTE, y por eso no es
    /// <c>DefensiveLineColumn</c> del rival. Las dos eligen al mismo jugador —el rival más retrasado— pero
    /// toman el máximo con el balón en marcos opuestos, y el caso normal de una jugada de ataque (balón
    /// por detrás de la defensa rival) es justo donde se separan: la línea de fuera de juego se queda en
    /// la defensa, mientras que la lectura en el marco del defensor caería sobre el balón y dejaría al
    /// atacante clavado a su altura. Es el paso 3 de <c>AI_GetOffsideLine</c>
    /// (docs/referencia-motores-futbol.md §6.1): <c>max(segundo_más_adelantado, balón)</c>.
    /// </summary>
    [Fact]
    public void TheOffsideLineIsMeasuredFromTheAttackersOwnGoal()
    {
        // Equipo 0 ataca hacia columnas crecientes; los rivales del equipo 1 están en 13 (el más
        // retrasado de los suyos) y 9, y el balón viene por detrás, en la columna 10.
        var players = new[]
        {
            Outfield(0, new Vec2(11f, 2.5f), team: 0),
            Outfield(1, new Vec2(13f, 2.5f), team: 1),
            Outfield(2, new Vec2(9f, 2.5f), team: 1),
        };
        var ball = new Vec2(10f, 2.5f);

        Assert.Equal(13f, Utility.OffsideLineColumn(players, ball, 0));

        // La misma escena leída desde el equipo 1 da la columna del balón: correcto como techo del bloque
        // del PROPIO equipo 1 (no subir más de un margen por delante de su hombre más retrasado o del
        // balón), y equivocado como línea a la que recortar al atacante del equipo 0.
        Assert.Equal(10f, Utility.DefensiveLineColumn(players, ball, 1));

        // Balón por delante de la defensa rival: no hay fuera de juego por delante del balón, la línea
        // sube con él.
        Assert.Equal(14f, Utility.OffsideLineColumn(players, new Vec2(14f, 2.5f), 0));

        // Simétrico para el equipo 1, que ataca hacia columnas decrecientes.
        Assert.Equal(11f, Utility.OffsideLineColumn(players, new Vec2(13f, 2.5f), 1));
        Assert.Equal(3f, Utility.OffsideLineColumn(players, new Vec2(3f, 2.5f), 1));
    }

    /// <summary>Jugador de campo sintético colocado en una posición exacta, para las pruebas de línea.</summary>
    private static MatchPlayer Outfield(int id, Vec2 position, int team)
    {
        var player = Player(id, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50), team);
        player.Position = position;
        return player;
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

    private static MatchPlayer Player(int id, Position position, Cell home, Attributes attributes, int team = 0, IReadOnlyList<Trait>? traits = null)
    {
        traits ??= Array.Empty<Trait>();
        var tags = new List<string> { "Neutral", position.ToString() };
        foreach (var trait in traits)
        {
            tags.Add(trait.ToString());
        }

        var definition = new PlayerDefinition(id, "p" + id, Race.Human, position, Rarity.Common, 1, attributes, traits, tags, PhysicalState.Healthy);
        return new MatchPlayer(definition, team, home, Catalog);
    }

    private static UtilityContext Context(AiWeights weights, params MatchPlayer[] players)
    {
        var ball = new Ball
        {
            InterceptAttempted = new bool[players.Length],
            Position = players[0].Position,
        };

        for (int i = 0; i < players.Length; i++)
        {
            players[i].Index = i;
        }

        var context = new UtilityContext(players, ball, weights, Catalog.Tuning.ActionZone);
        context.TacticalStates[0] = TacticalState.InPossession;
        context.TacticalStates[1] = TacticalState.InPossession;
        context.NearestToBall[0] = players[0];
        return context;
    }

    /// <summary>Pesos sintéticos: todo a 0, multiplicador táctico neutro y contexto sin términos.</summary>
    private static AiWeights Weights(Action<int[,]> configure)
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

        configure(baseTable);

        var context = new AiContext(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1.2f, 0, 0, 0, 0);
        var shifts = new BlockShift[Enum.GetValues<TacticalState>().Length];
        return new AiWeights(baseTable, tacticalTable, context, shifts);
    }
}
