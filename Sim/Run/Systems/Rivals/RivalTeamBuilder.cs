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
    public static TeamSetup Build(RivalTeam team, Catalog catalog) =>
        Build(team, catalog, RivalMemory.Empty, seed: 0, nemesisLevelBonus: 0);

    /// <summary>
    /// Construye el equipo del rival <b>con la memoria de la run</b> (ADR 0165): un puesto vacante lo cubre un
    /// fichaje con las mismas cifras que el jugador de datos y otro nombre; el némesis que juega ahí lo hace con
    /// su nombre y <paramref name="nemesisLevelBonus"/> niveles más. Los ids siguen siendo
    /// <c>OpponentFirstPlayerId + puesto</c>: el puesto es la identidad estable que leen los créditos de
    /// rival (<c>RivalCredits</c>) y la memoria.
    ///
    /// <para>Un némesis en el banquillo (puestos 7..9: entró de cambio y mató) <b>juega de titular</b>: sustituye
    /// al último titular de su puesto en la alineación, porque «el mapa marca el nodo donde juega» tiene que ser
    /// verdad y un némesis sentado no juega.</para>
    /// </summary>
    public static TeamSetup Build(RivalTeam team, Catalog catalog, RivalMemory memory, ulong seed, int nemesisLevelBonus)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(memory);

        var race = catalog.Race(team.Race);
        var occupants = RivalRoster.Resolve(team, memory, seed, catalog);
        var players = new List<PlayerDefinition>(team.Players.Count);
        for (int i = 0; i < team.Players.Count; i++)
        {
            var source = team.Players[i];
            // Mismo orden que Sim.Generation.PlayerGenerator: [SpeciesTag, StyleTag, Position, ...Traits].
            var tags = new List<string> { race.SpeciesTag, source.StyleTag.ToString(), source.Position.ToString() };
            for (int t = 0; t < source.Traits.Count; t++)
            {
                tags.Add(source.Traits[t].ToString());
            }

            var definition = new PlayerDefinition(
                OpponentFirstPlayerId + i,
                occupants[i].Name,
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
                StyleTag = source.StyleTag,
                Perks = source.Perks,
            };

            if (occupants[i].Nemesis is not null && nemesisLevelBonus > 0)
            {
                definition = Progression.Progression.LevelUp(
                    definition, definition.Level + nemesisLevelBonus, catalog.Tuning.Progression);
            }

            players.Add(definition);
        }

        var starters = players.Take(7).ToList();
        // Quién es némesis en cada puesto de la alineación, actualizado al sustituir: dos némesis de banquillo del
        // mismo puesto no se pisan (el segundo no echa al primero; revisión de la ADR 0165).
        var starterIsNemesis = new bool[starters.Count];
        for (int j = 0; j < starters.Count; j++)
        {
            starterIsNemesis[j] = occupants[j].Nemesis is not null;
        }

        for (int slot = 7; slot < players.Count; slot++)
        {
            if (occupants[slot].Nemesis is null)
            {
                continue;
            }

            for (int j = starters.Count - 1; j >= 0; j--)
            {
                if (starters[j].Position == players[slot].Position && !starterIsNemesis[j])
                {
                    starters[j] = players[slot];
                    starterIsNemesis[j] = true;
                    break;
                }
            }
        }

        var lineup = Lineup.Default(starters);

        // El nombre visible del equipo, no su id de datos (RF-015): antes de este arreglo, el log de
        // eventos (RF-121), el informe post-partido (RF-119) y el marcador del partido mostraban
        // literalmente "act1_elf_swiftwing" en vez de "Ala Veloz", porque TeamSetup.Name se rellenaba con
        // team.Id. El idioma es fijo a "es" (como el resto de textos generados fuera del catálogo de
        // localización) mientras la fase 4 no elija idioma en tiempo de ejecución (RT-073).
        return new TeamSetup(team.Id, team.Name.Es, team.Race, players, lineup);
    }
}
