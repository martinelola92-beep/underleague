using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-J: primitiva genérica para "cuántos compañeros como yo", resuelta en tiempo de partido a partir
/// del propio <c>StyleTag</c> del jugador — sin fijar una etiqueta literal de antemano, a diferencia de
/// <c>teammatesWithTag</c>. Existe porque «Mentalidad de manada» pedía brutos concretos y era casi
/// imposible reunir tres en un equipo de enanos (75 % Bulwark, 8 % Brute); la condición genérica hace
/// que un perk de este tipo funcione con cualquier composición, para cualquier raza.
/// </summary>
public sealed class TeammatesWithSameStyleTests
{
    private const string Condition = """[ { "type": "modifyBias", "value": 10 } ]""";

    [Fact]
    public void CountsOnlyOnPitchTeammatesWithTheSameStyleTagExcludingSelf()
    {
        var catalog = TestPerks.CatalogWith(("test_pack", TestPerks.Json(
            "test_pack", "MATCH_START", Condition, condition: "teammatesWithSameStyle(owner) >= 2")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "test_pack" }));

        // Tres Bulwark (1, 2, 3, uno de ellos el portador) y dos Brute (4, 5) en el mismo equipo.
        var home = setup.Home.Players.Select(p => p.Id switch
        {
            1 or 2 or 3 => p with { StyleTag = StyleTag.Bulwark },
            4 or 5 => p with { StyleTag = StyleTag.Brute },
            _ => p,
        }).ToList();
        setup = setup with { Home = setup.Home with { Players = home } };

        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;

        // Dos MÁS con su mismo estilo (2 y 3), excluido él mismo: cuenta 2, la condición ">=2" se cumple.
        Assert.Equal(2, CountSameStyle(engine, owner));
    }

    [Fact]
    public void WorksForAnyStyleNotJustBrute()
    {
        // El caso que motivó BB-J: un equipo de enanos, mayoritariamente Bulwark, sin brutos suficientes
        // para "teammatesWithTag(owner,'Brute')", pero con Bulwark de sobra para la version generica.
        var catalog = TestPerks.CatalogWith(("test_pack", TestPerks.Json(
            "test_pack", "MATCH_START", Condition, condition: "teammatesWithSameStyle(owner) >= 2")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "test_pack" }));

        var home = setup.Home.Players.Select(p => p with { StyleTag = StyleTag.Bulwark }).ToList();
        setup = setup with { Home = setup.Home with { Players = home } };

        var engine = TestPerks.Engine(catalog, setup);
        var owner = engine.PlayerById(1)!;

        Assert.True(CountSameStyle(engine, owner) >= 2, "un equipo entero del mismo estilo debe contar sobrado");
    }

    private static int CountSameStyle(Underleague.Sim.Engine.MatchEngine engine, Underleague.Sim.Engine.MatchPlayer owner) =>
        ((Underleague.Sim.Perks.IPerkWorld)engine).TeammatesWithSameStyle(owner);
}
