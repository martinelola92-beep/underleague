using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// C7 (docs/analisis/perks-catalogo-unificado.md §3.2): el criterio de a quién entrar sin balón (ADR
/// 0105), cuando un perk lo sesga. Dos variantes: el rival ya derribado (<c>MatchPlayer.
/// PreferKnockedDownTackleTarget</c>, "Olfato de sangre") y el que cometió la última falta
/// (<c>MatchPlayer.TackleNemesis</c>, "Rabia"). Se ejercita <see cref="Utility.Choose"/> directamente,
/// con el mismo arnés ligero que <see cref="UtilityTests"/> (jugadores y contexto a mano, sin motor
/// completo), porque los dos campos son estado plano de <c>MatchPlayer</c> y no hace falta un perk real
/// de <c>data/perks/</c> -que todavía no existe, es el paquete siguiente- para demostrar que el motor los
/// respeta.
/// </summary>
public sealed class TackleBiasTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Sin sesgo, la entrada sin balón va al marcado (ADR 0105, comportamiento de siempre). Con
    /// <c>PreferKnockedDownTackleTarget</c>, el MISMO jugador, en la MISMA posición, cambia de objetivo:
    /// entra al rival ya derribado y no al marcado, aunque el marcado siga siendo una opción legal y más
    /// cercana. Es la demostración de "la entrada va al tocado", no solo que el campo se puede escribir.
    /// </summary>
    [Fact]
    public void PreferKnockedDownSendsTheTackleToTheDownedRivalInsteadOfTheMarked()
    {
        var weights = TackleWeights();
        var tackler = Player(1, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        var marked = Player(101, Position.Forward, new Cell(13, 2), new Attributes(50, 50, 50, 50, 50), team: 1);
        var downed = Player(102, Position.Forward, new Cell(13, 3), new Attributes(50, 50, 50, 50, 50), team: 1);

        tackler.Position = new Vec2(5f, 2f);
        marked.Position = new Vec2(5.5f, 2f); // más cerca que el derribado: sin sesgo, este es el elegido.
        downed.Position = new Vec2(6f, 2f);
        downed.EnterState(PlayerState.KnockedDown, 100);

        tackler.MarkTarget = marked;

        var context = Context(weights, tackler, marked, downed);
        var rows = new List<UtilityRow>();

        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, tackler, rows));
        Assert.Same(marked, tackler.TackleTarget);
        Assert.True(tackler.TackleOffBall);

        tackler.PreferKnockedDownTackleTarget = true;
        rows.Clear();

        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, tackler, rows));
        Assert.Same(downed, tackler.TackleTarget);
        Assert.True(tackler.TackleOffBall);
    }

    /// <summary>
    /// Sin <c>PreferKnockedDownTackleTarget</c> ni un rival derribado al alcance, un rival en pie que no
    /// es el marcado NUNCA es objetivo (el sesgo no es "el más cercano", es "el ya derribado").
    /// </summary>
    [Fact]
    public void PreferKnockedDownIgnoresAStandingRivalThatIsNotDown()
    {
        var weights = TackleWeights();
        var tackler = Player(1, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        var marked = Player(101, Position.Forward, new Cell(13, 2), new Attributes(50, 50, 50, 50, 50), team: 1);
        var standing = Player(102, Position.Forward, new Cell(13, 3), new Attributes(50, 50, 50, 50, 50), team: 1);

        tackler.Position = new Vec2(5f, 2f);
        marked.Position = new Vec2(5.5f, 2f);
        standing.Position = new Vec2(6f, 2f); // más cerca, pero NO está derribado.
        tackler.MarkTarget = marked;
        tackler.PreferKnockedDownTackleTarget = true;

        var context = Context(weights, tackler, marked, standing);
        var rows = new List<UtilityRow>();

        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, tackler, rows));
        Assert.Same(marked, tackler.TackleTarget);
    }

    /// <summary>
    /// Sin sesgo, la entrada sin balón va al marcado. Con <c>TackleNemesis</c> apuntando a otro rival en
    /// pie -distinto del marcado-, el mismo jugador entra a por él ("Rabia": va a por quien le hizo la
    /// última falta).
    /// </summary>
    [Fact]
    public void FouledNemesisSendsTheTackleToWhoeverFouledInsteadOfTheMarked()
    {
        var weights = TackleWeights();
        var tackler = Player(1, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        var marked = Player(101, Position.Forward, new Cell(13, 2), new Attributes(50, 50, 50, 50, 50), team: 1);
        var fouler = Player(102, Position.Midfielder, new Cell(13, 3), new Attributes(50, 50, 50, 50, 50), team: 1);

        tackler.Position = new Vec2(5f, 2f);
        marked.Position = new Vec2(5.5f, 2f);
        fouler.Position = new Vec2(6f, 2f);
        tackler.MarkTarget = marked;

        var context = Context(weights, tackler, marked, fouler);
        var rows = new List<UtilityRow>();

        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, tackler, rows));
        Assert.Same(marked, tackler.TackleTarget);

        tackler.TackleNemesis = fouler;
        rows.Clear();

        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, tackler, rows));
        Assert.Same(fouler, tackler.TackleTarget);
        Assert.True(tackler.TackleOffBall);
    }

    /// <summary>
    /// El poseedor rival sigue ganando siempre a cualquier sesgo (ADR 0105 intacta): "Olfato de sangre" y
    /// "Rabia" solo sustituyen al marcado, nunca a quien lleva el balón.
    /// </summary>
    [Fact]
    public void TheBallCarrierStillBeatsAnyBiasedOffBallTarget()
    {
        var weights = TackleWeights();
        var tackler = Player(1, Position.Defender, new Cell(2, 2), new Attributes(50, 50, 50, 50, 50));
        var carrier = Player(100, Position.Forward, new Cell(13, 2), new Attributes(50, 50, 50, 50, 50), team: 1);
        var nemesis = Player(102, Position.Forward, new Cell(13, 3), new Attributes(50, 50, 50, 50, 50), team: 1);

        tackler.Position = new Vec2(5f, 2f);
        carrier.Position = new Vec2(5.5f, 2f);
        nemesis.Position = new Vec2(5.6f, 2f); // incluso más cerca que el poseedor.
        tackler.TackleNemesis = nemesis;

        var context = Context(weights, tackler, carrier, nemesis);
        context.Ball.Owner = carrier;
        var rows = new List<UtilityRow>();

        Assert.Equal(PlayerAction.Tackle, Utility.Choose(context, tackler, rows));
        Assert.Same(carrier, tackler.TackleTarget);
        Assert.False(tackler.TackleOffBall);
    }

    // ---------------------------------------------------------------- ayudantes (mismo arnés que UtilityTests)

    private static MatchPlayer Player(int id, Position position, Cell home, Attributes attributes, int team = 0)
    {
        var tags = new List<string> { "Neutral", position.ToString() };
        var definition = new PlayerDefinition(
            id, "p" + id, Race.Human, position, Rarity.Common, 1, attributes, Array.Empty<Trait>(), tags, PhysicalState.Healthy);
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

        var context = new UtilityContext(players, ball, weights, Catalog.Tuning.ActionZone, Catalog.Tuning.Pass.InterceptRadiusCells);
        context.TacticalStates[0] = TacticalState.OutOfPossession;
        context.TacticalStates[1] = TacticalState.OutOfPossession;
        context.NearestToBall[0] = players[0];
        return context;
    }

    /// <summary>
    /// Tackle domina claramente sobre Retreat (única alternativa relevante en Positioning), y el radio de
    /// alcance/jugada activa es generoso a propósito: lo que estos tests miden es A QUIÉN entra, no SI
    /// entra.
    /// </summary>
    private static AiWeights TackleWeights()
    {
        int positions = Enum.GetValues<Position>().Length;
        int actions = Enum.GetValues<PlayerAction>().Length;
        var baseTable = new int[positions, actions];
        baseTable[(int)Position.Defender, (int)PlayerAction.Tackle] = 1000;
        baseTable[(int)Position.Defender, (int)PlayerAction.Retreat] = 100;

        var tacticalTable = new int[Enum.GetValues<TacticalState>().Length, actions];
        for (int s = 0; s < tacticalTable.GetLength(0); s++)
        {
            for (int a = 0; a < actions; a++)
            {
                tacticalTable[s, a] = 100;
            }
        }

        var context = new AiContext(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            TackleDistanceMaxCells: 20f,
            TackleOutOfReachPenalty: 0,
            TackleBallCarrierBonus: 200,
            RetreatDistanceBonusPerCell: 0,
            RetreatAtHomePenalty: 0,
            BlockActiveRadiusCells: 50f);

        var shifts = new BlockShift[Enum.GetValues<TacticalState>().Length];
        return new AiWeights(baseTable, tacticalTable, TestData.NeutralMentality(), TestData.OffBallTackleAdjust(defender: 100), context, shifts);
    }
}
