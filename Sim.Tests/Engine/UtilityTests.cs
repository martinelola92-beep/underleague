using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// IA de utilidad (§3.5, RT-095, RT-097): desempate por el orden del enum, filtro de correa y
/// acumulación de multiplicadores de rasgo (RT-094).
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

    [Fact]
    public void LeashFiltersAnActionThatWouldRequireLeavingTheRadius()
    {
        var weights = Weights(builder =>
        {
            builder[(int)Position.Defender, (int)PlayerAction.ChaseBall] = 1000;
            builder[(int)Position.Defender, (int)PlayerAction.Retreat] = 100;
        });

        // Correa de 1 casilla y jugador ya en el borde de la correa, en la dirección del balón.
        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 1, 1));
        Assert.Equal(1f, player.LeashCells);
        player.Position = new Vec2(player.HomeCenter.X + 1f, player.HomeCenter.Y);

        var context = Context(weights, player);
        context.Ball.Position = new Vec2(player.HomeCenter.X + 9f, player.HomeCenter.Y);

        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, player, rows);

        Assert.True(Row(rows, PlayerAction.ChaseBall).LeashFiltered);
        Assert.NotEqual(PlayerAction.ChaseBall, chosen);
    }

    [Fact]
    public void LeashDoesNotFilterAnActionThatStillAdvances()
    {
        var weights = Weights(builder =>
        {
            builder[(int)Position.Defender, (int)PlayerAction.ChaseBall] = 1000;
            builder[(int)Position.Defender, (int)PlayerAction.Retreat] = 100;
        });

        var player = Player(0, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 1, 1));
        var context = Context(weights, player);
        context.Ball.Position = new Vec2(player.HomeCenter.X + 9f, player.HomeCenter.Y);

        var rows = new List<UtilityRow>();
        var chosen = Utility.Choose(context, player, rows);

        Assert.False(Row(rows, PlayerAction.ChaseBall).LeashFiltered);
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
        return new MatchPlayer(definition, team, 0, home, Catalog);
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

        var context = new UtilityContext(players, ball, weights);
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

        var context = new AiContext(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1.2f, 0, 0, 0, 0);
        var shifts = new BlockShift[Enum.GetValues<TacticalState>().Length];
        return new AiWeights(baseTable, tacticalTable, context, shifts);
    }
}
