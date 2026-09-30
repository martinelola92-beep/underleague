using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BC-D (ADR 0181): <c>teammatesGoalside(who)</c>, cuántos compañeros de campo hay entre el balón y la portería
/// propia de <c>who</c> (proyección sobre X). Primitiva genérica de condición: «Último hombre» la usa con
/// <c>== 0</c>, pero no es una excepción para ese perk.
/// </summary>
public sealed class TeammatesGoalsideTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private readonly ITestOutputHelper _output;

    public TeammatesGoalsideTests(ITestOutputHelper output) => _output = output;

    private static int Count(MatchEngine engine, MatchPlayer who) => ((IPerkWorld)engine).TeammatesGoalside(who);

    /// <summary>
    /// Equipo visitante (ataca hacia X = 0, su portería está en X = 16). Balón en X = 6: los visitantes entre el
    /// balón y su portería son los que tienen X entre 6 y 16; el portero y los caídos de más allá no cuentan.
    /// </summary>
    [Fact]
    public void CountsOutfieldTeammatesBetweenTheBallAndTheOwnGoalOnly()
    {
        var catalog = TestPerks.CatalogWith(("noop", TestPerks.Json("noop", "MATCH_START", """[ { "type": "modifyBias", "value": 10 } ]""")));
        var setup = TestPerks.Match(catalog, 1, (101, new[] { "noop" }));
        var engine = new MatchEngine(setup, 1, catalog, new SimConfig(CollectLog: false));
        var owner = engine.PlayerById(101)!;

        // Todos los demás, lejos del tramo 6..16 (a la izquierda del balón), y el balón en X = 6.
        for (int i = 0; i < 14; i++)
        {
            engine.PlaceForTest(i, new Vec2(2f, 0.5f));
        }

        engine.PlaceForTest(owner.Index, new Vec2(12f, 3f));
        engine.GiveBallForTest(engine.PlayerById(6)!.Index, new Vec2(6f, 3f));
        Assert.Equal(0, Count(engine, owner));

        // Un compañero de campo de su equipo entre el balón y su portería.
        var mate = Enumerable.Range(0, 14).Select(engine.PlayerAtForTest)
            .First(p => p.Team == owner.Team && p.Id != owner.Id && p.IsOutfield);
        engine.PlaceForTest(mate.Index, new Vec2(9f, 1f));
        Assert.Equal(1, Count(engine, owner));

        // El portero de su equipo no cuenta aunque esté en el tramo.
        var keeper = Enumerable.Range(0, 14).Select(engine.PlayerAtForTest)
            .First(p => p.Team == owner.Team && !p.IsOutfield);
        engine.PlaceForTest(keeper.Index, new Vec2(15f, 2.5f));
        Assert.Equal(1, Count(engine, owner));

        // Un rival en el tramo tampoco cuenta.
        var rival = Enumerable.Range(0, 14).Select(engine.PlayerAtForTest).First(p => p.Team != owner.Team && p.IsOutfield);
        engine.PlaceForTest(rival.Index, new Vec2(10f, 2f));
        Assert.Equal(1, Count(engine, owner));

        // Un compañero por detrás del balón (en X = 3, hacia el rival) deja de estar entre el balón y su portería.
        engine.PlaceForTest(mate.Index, new Vec2(3f, 1f));
        Assert.Equal(0, Count(engine, owner));
    }
}
