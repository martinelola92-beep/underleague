using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests;

/// <summary>Emparejamientos de referencia para los tests del motor: dos equipos humanos de calidad 50.</summary>
internal static class TestMatches
{
    /// <summary>Partido de referencia: local y visitante humanos de calidad 50, árbitro neutro y criterio 0.</summary>
    public static MatchSetup Reference(Catalog catalog, ulong seed) => Build(catalog, seed, 50, 50);

    /// <summary>
    /// Emparejamiento artificial para el test de incomparecencia: cinco titulares frágiles (aguante 1,
    /// fuerza 1, técnica 99, correa 99) contra siete titulares brutales (fuerza y velocidad 99, rasgos
    /// Aggressive y Dirty). Se construye a mano porque TeamGenerator no puede producir estos extremos.
    /// </summary>
    public static MatchSetup Brutal(Catalog catalog)
    {
        var fragile = HandmadeTeam("fragile", 0, 5, new Attributes(1, 50, 99, 1, 99), Array.Empty<Trait>());
        var brutal = HandmadeTeam("brutal", 100, 7, new Attributes(99, 99, 1, 99, 99), new[] { Trait.Aggressive, Trait.Dirty });
        return new MatchSetup(fragile, brutal, new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));
    }

    private static TeamSetup HandmadeTeam(string id, int firstId, int count, Attributes attributes, IReadOnlyList<Trait> traits)
    {
        var positions = new[] { Position.Goalkeeper, Position.Defender, Position.Defender, Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward };
        var players = new List<PlayerDefinition>();
        for (int i = 0; i < count; i++)
        {
            var tags = new List<string> { "Neutral", positions[i].ToString() };
            foreach (var trait in traits)
            {
                tags.Add(trait.ToString());
            }

            players.Add(new PlayerDefinition(firstId + i, id + i, Race.Human, positions[i], Rarity.Common, 1, attributes, traits, tags, PhysicalState.Healthy));
        }

        return new TeamSetup(id, id, Race.Human, players, Lineup.Default(players));
    }

    /// <summary>Partido con calidades arbitrarias; los equipos se generan con RngStreams.Generation.</summary>
    public static MatchSetup Build(Catalog catalog, ulong seed, int homeQuality, int awayQuality)
    {
        var homeRng = RngStreams.Generation(seed, 0);
        var awayRng = RngStreams.Generation(seed, 1);
        var home = TeamGenerator.Generate(ref homeRng, catalog, "home", Race.Human, homeQuality, 0);
        var away = TeamGenerator.Generate(ref awayRng, catalog, "away", Race.Human, awayQuality, 100);
        return new MatchSetup(home, away, new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));
    }
}
