using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BM-A: un perk puede derribar al que le entra a su portador (<c>scope: opponent</c> + <c>target: actor</c>),
/// y un derribado no disputa. El cargador decide la rivalidad por el alcance, no por el nombre del objetivo;
/// el motor no tira la entrada ni el regate de un actor que un perk tumbó en la publicación previa.
/// </summary>
public sealed class DownedActorTests
{
    private const string KnockDownActor = """[{ "type": "setState", "target": "actor", "state": "KnockedDown", "ticks": 12 }]""";

    [Theory]
    [InlineData("opponent", "actor", true)]
    [InlineData("opposingTeam", "actor", true)]
    [InlineData("actor", "opponent", true)]
    [InlineData("team", "opponent", true)]
    [InlineData("any", "opposingTeam", true)]
    [InlineData("opponent", "adjacentOpponents", true)]
    [InlineData("actor", "actor", false)]      // el propio portador
    [InlineData("opponent", "opponent", false)] // el propio portador, visto desde el otro lado
    [InlineData("actor", "target", false)]     // en un PASS es un compañero
    [InlineData("any", "actor", false)]        // no se sabe de qué bando es
    [InlineData("target", "opponent", false)]  // rival del actor, no necesariamente del portador
    public void SetStateAcceptsOnlyTargetsThatAreAlwaysRivalsOfTheOwner(string scope, string target, bool valid) =>
        AssertSetState("TACKLE", scope, target, valid);

    /// <summary>
    /// En INJURY y DEATH el causante puede ser un compañero o la propia víctima (fuego amigo, BE-B), así que
    /// ni el actor ni el opponent son rivales garantizados: sólo valen los objetivos de población rival.
    /// </summary>
    [Theory]
    [InlineData("INJURY", "opponent", "actor", false)]
    [InlineData("INJURY", "actor", "opponent", false)]
    [InlineData("DEATH", "team", "opponent", false)]
    [InlineData("INJURY", "actor", "opposingTeam", true)]
    [InlineData("DEATH", "any", "adjacentOpponents", true)]
    public void OnInjuryAndDeathOnlyPopulationTargetsAreRivals(string trigger, string scope, string target, bool valid) =>
        AssertSetState(trigger, scope, target, valid);

    private static void AssertSetState(string trigger, string scope, string target, bool valid)
    {
        string effects = $$"""[{ "type": "setState", "target": "{{target}}", "state": "KnockedDown", "ticks": 10 }]""";
        string json = TestPerks.Json("p", trigger, effects, scope: scope);
        if (valid)
        {
            Assert.NotNull(TestPerks.Load("p", json));
        }
        else
        {
            var ex = Assert.Throws<DataException>(() => TestPerks.Load("p", json));
            Assert.Contains("rival del portador", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ATacklerKnockedDownByTheCarriersPerkDoesNotWinTheBall()
    {
        var catalog = TestPerks.CatalogWith(
            ("wall", TestPerks.Json("wall", "TACKLE", KnockDownActor, scope: "opponent")));
        var (engine, carrier, rival) = Duel(catalog);

        engine.RepeatTackle(rival);

        Assert.Equal(PlayerState.KnockedDown, rival.State);
        Assert.Same(carrier, engine.Ball.Owner);
        Assert.Equal(0, rival.Tackles);
        Assert.NotEqual(PlayerState.KnockedDown, carrier.State);

        // No hubo entrada: ni evento TACKLE registrado, ni falta, ni recuperación.
        Assert.DoesNotContain(engine.EventsForTest, e => e.Type is EventType.Tackle or EventType.Foul or EventType.Recovery);
    }

    [Fact]
    public void ABlockerKnockedDownByTheTargetsPerkStaysDown()
    {
        var catalog = TestPerks.CatalogWith(
            ("wall", TestPerks.Json("wall", "TACKLE", KnockDownActor, scope: "opponent")));
        var (engine, owner, rival) = Duel(catalog);
        engine.ParkBallForTest(new Vec2(1f, 1f)); // el bloqueo es a quien NO lleva el balón

        engine.ResolveBlockForTest(rival.Index, owner.Index);

        // Sin el corte, la rama «el árbitro no lo ve» devolvía a Positioning a un jugador recién tumbado.
        Assert.Equal(PlayerState.KnockedDown, rival.State);
        Assert.NotEqual(PlayerState.KnockedDown, owner.State);
        Assert.DoesNotContain(engine.EventsForTest, e => e.Type is EventType.Tackle or EventType.Foul);
    }

    [Fact]
    public void ADribblerKnockedDownByTheDefendersPerkLeavesTheBallLoose()
    {
        var catalog = TestPerks.CatalogWith(
            ("wall", TestPerks.Json("wall", "DRIBBLE_ATTEMPTED", KnockDownActor, scope: "opponent")));
        var (engine, defender, dribbler) = Duel(catalog);
        engine.GiveBallForTest(dribbler.Index, dribbler.Position);

        engine.TryDribbleDuelForTest(dribbler.Index);

        Assert.Equal(PlayerState.KnockedDown, dribbler.State);
        Assert.Null(engine.Ball.Owner);
        Assert.NotEqual(PlayerState.KnockedDown, defender.State);

        // El intento queda registrado; el regate no se resuelve, así que no hay ganador ni perdedor.
        Assert.Contains(engine.EventsForTest, e => e.Type == EventType.DribbleAttempted);
        Assert.DoesNotContain(engine.EventsForTest, e => e.Type is EventType.DribbleWon or EventType.DribbleLost);
    }

    /// <summary>
    /// El jugador 1 (con el perk) y el primer rival de campo, pegados en el centro; todos los demás lejos
    /// para que el duelo no pueda elegir a otro. El balón, en los pies del jugador 1.
    /// </summary>
    private static (MatchEngine Engine, MatchPlayer Owner, MatchPlayer Rival) Duel(Catalog catalog)
    {
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "wall" }));
        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;
        var rival = engine.PlayerAtForTest(engine.OutfieldIndexForTest(1 - owner.Team, 0));

        for (int i = 0; i < 14; i++)
        {
            var player = engine.PlayerAtForTest(i);
            if (!ReferenceEquals(player, owner) && !ReferenceEquals(player, rival))
            {
                engine.PlaceForTest(i, new Vec2(player.Team == 0 ? 0.5f : 15.5f, 0.5f));
            }
        }

        engine.PlaceForTest(owner.Index, new Vec2(8f, 3.5f));
        engine.PlaceForTest(rival.Index, new Vec2(8.4f, 3.5f));
        engine.GiveBallForTest(owner.Index, new Vec2(8f, 3.5f));
        return (engine, owner, rival);
    }
}
