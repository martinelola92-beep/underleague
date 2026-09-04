using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>Generación procedural de un equipo completo (10 jugadores) a partir de raza y calidad.</summary>
public static class TeamGenerator
{
    private static readonly Position[] StarterPositions =
    {
        Position.Goalkeeper, Position.Defender, Position.Defender,
        Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward,
    };

    private static readonly Position[] SubstitutePositions =
    {
        Position.Defender, Position.Midfielder, Position.Forward,
    };

    /// <summary>
    /// 10 jugadores: titulares GK, DEF, DEF, MID, MID, MID, FWD (ids firstId..firstId+6) y suplentes
    /// DEF, MID, FWD (firstId+7..firstId+9). Uno de los 10 es Rare (RF-005), elegido con rng.
    /// Decisión fuera de la especificación: Name del equipo se fija igual a teamId (Generate no recibe
    /// un nombre de equipo separado).
    ///
    /// <paramref name="quality"/> es el parámetro histórico de <c>/Balance</c> (equipos de referencia,
    /// builds, tests estadísticos) para variar la fuerza de un equipo sin rediseñar esos consumidores.
    /// El modelo de presupuesto de fase1b-diseno.md §1.3 (ADR 0025, ADR 0027) ya no tiene un dial de
    /// "calidad": el presupuesto de atributos depende de rareza y nivel. Aquí se traduce quality a nivel
    /// con <c>Clamp(quality / 10, 1, 8)</c> (nivel 1..8, RF-023/Progression.MaxLevel) y ese nivel es el
    /// de todos los jugadores generados: decisión fuera de la especificación, tomada para no tocar
    /// Balance/*.cs ni data/balance/*.json (fuera del ámbito de este paquete); el paquete de reajuste
    /// debería sustituirla por un dial explícito de nivel en Balance en vez de reciclar "quality".
    /// </summary>
    public static TeamSetup Generate(ref Pcg32 rng, Catalog catalog, string teamId, Race race, int quality, int firstPlayerId)
    {
        var raceDefinition = catalog.Race(race);
        var nameGenerator = new NameGenerator(raceDefinition);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        int level = Math.Clamp(quality / 10, 1, 8);

        int totalPlayers = StarterPositions.Length + SubstitutePositions.Length;
        int rareIndex = rng.Range(0, totalPlayers);

        var players = new List<PlayerDefinition>(totalPlayers);
        int index = 0;
        foreach (var position in StarterPositions)
        {
            players.Add(GeneratePlayer(ref rng, catalog, raceDefinition, nameGenerator, usedNames, position, index == rareIndex ? Rarity.Rare : Rarity.Common, level, firstPlayerId + index));
            index++;
        }

        foreach (var position in SubstitutePositions)
        {
            players.Add(GeneratePlayer(ref rng, catalog, raceDefinition, nameGenerator, usedNames, position, index == rareIndex ? Rarity.Rare : Rarity.Common, level, firstPlayerId + index));
            index++;
        }

        var starters = players.Take(StarterPositions.Length).ToList();
        var lineup = Lineup.Default(starters);

        return new TeamSetup(teamId, teamId, race, players, lineup);
    }

    private static PlayerDefinition GeneratePlayer(ref Pcg32 rng, Catalog catalog, RaceDefinition race, NameGenerator nameGenerator, HashSet<string> usedNames, Position position, Rarity rarity, int level, int id)
    {
        string name;
        do
        {
            name = nameGenerator.Next(ref rng);
        }
        while (!usedNames.Add(name));

        return PlayerGenerator.Generate(ref rng, catalog, race, position, rarity, level, id, name);
    }
}
