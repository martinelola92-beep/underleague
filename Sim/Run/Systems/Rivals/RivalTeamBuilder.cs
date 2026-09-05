using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>Construye el <see cref="TeamSetup"/> de un <see cref="RivalTeam"/> para <c>Simulator.Run</c>.</summary>
public static class RivalTeamBuilder
{
    /// <summary>
    /// Primer id de jugador de un equipo rival. Muy por encima de cualquier id que la plantilla del
    /// jugador pueda alcanzar en una run y separado del rango que usa <c>DefaultRunSystems</c>
    /// (1.000.000): los rivales de datos nunca se guardan en <c>RunState</c>, así que reutilizar el mismo
    /// rango entre nodos es seguro (solo existen mientras dura una llamada a <c>Simulator.Run</c>), pero
    /// mantenerlo separado evita cualquier colisión si algún día conviven.
    /// </summary>
    public const int OpponentFirstPlayerId = 2_000_000;

    /// <summary>Construye el equipo del rival, con la colocación por defecto (mismo 2-3-1 que <c>Lineup.Default</c>).</summary>
    public static TeamSetup Build(RivalTeam team, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(catalog);

        var race = catalog.Race(team.Race);
        var players = new List<PlayerDefinition>(team.Players.Count);
        for (int i = 0; i < team.Players.Count; i++)
        {
            var source = team.Players[i];
            var tags = new List<string> { race.SpeciesTag, source.Position.ToString() };
            for (int t = 0; t < source.Traits.Count; t++)
            {
                tags.Add(source.Traits[t].ToString());
            }

            players.Add(new PlayerDefinition(
                OpponentFirstPlayerId + i,
                source.Name,
                team.Race,
                source.Position,
                source.Rarity,
                source.Level,
                source.Attributes,
                source.Traits,
                tags,
                PhysicalState.Healthy)
            {
                SpeciesTag = race.SpeciesTag,
                StyleTag = StyleTag.Neutral,
                Perks = source.Perks,
            });
        }

        var starters = players.Take(7).ToList();
        var lineup = Lineup.Default(starters);

        // El nombre visible del equipo, no su id de datos (RF-015): antes de este arreglo, el log de
        // eventos (RF-121), el informe post-partido (RF-119) y el marcador del partido mostraban
        // literalmente "act1_elf_swiftwing" en vez de "Ala Veloz", porque TeamSetup.Name se rellenaba con
        // team.Id. El idioma es fijo a "es" (como el resto de textos generados fuera del catálogo de
        // localización) mientras la fase 4 no elija idioma en tiempo de ejecución (RT-073).
        return new TeamSetup(team.Id, team.Name.Es, team.Race, players, lineup);
    }
}
