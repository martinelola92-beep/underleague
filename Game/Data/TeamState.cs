using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Placement;
using Underleague.Sim.Random;

namespace Underleague.Game.Data;

/// <summary>
/// Estado de la pantalla de Equipo: el catálogo real de <c>/data</c> y una plantilla generada con
/// <c>Sim.Generation.TeamGenerator</c>. No hay datos falsos incrustados en ninguna parte de la pantalla.
/// <para>
/// La E/S vive aquí, en <c>/Game</c>, que es donde puede vivir: <c>/Sim</c> no lee ficheros (RT-012) y
/// recibe el contenido ya leído. Y ninguna regla de juego vive aquí (RT-014): mover a un jugador lo
/// resuelve <see cref="PlacementView.WithPlayerAt"/>.
/// </para>
/// </summary>
public sealed class TeamState
{
    /// <summary>Idioma de la interfaz. En fase 4 lo elige el jugador (RT-073); hasta entonces, español.</summary>
    public const string Language = "es";

    private TeamState(Catalog catalog, TeamSetup team)
    {
        Catalog = catalog;
        Team = team;
        Templates = catalog.Localization.Get(Language);
    }

    public Catalog Catalog { get; }

    public TeamSetup Team { get; private set; }

    public DescriptionTemplates Templates { get; }

    public IReadOnlyList<PlayerDefinition> Players => Team.Players;

    public Lineup Lineup => Team.Lineup;

    /// <summary>
    /// Carga <c>/data</c> y genera la plantilla con la semilla dada. Flujos de RNG separados (RT-022):
    /// uno para generar el equipo y otro para los perks iniciales, que son recompensa.
    /// </summary>
    public static TeamState Load(ulong seed)
    {
        var catalog = DataLoader.FromJson(ReadDataFiles(FindDataDirectory()));

        var generation = RngStreams.Generation(seed, 0);
        var team = TeamGenerator.Generate(ref generation, catalog, "underleague_fc", Race.Orc, quality: 55, firstPlayerId: 1, level: 3);

        var rewards = RngStreams.Rewards(seed, 0);
        var withPerks = PerkAssignment.AssignInitial(ref rewards, team.Players, catalog);
        return new TeamState(catalog, team with { Players = withPerks });
    }

    /// <summary>Jugador por id, o null si no está en la plantilla.</summary>
    public PlayerDefinition? Find(int id)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].Id == id)
            {
                return Players[i];
            }
        }

        return null;
    }

    /// <summary>Jugador alineado en esa casilla, o null.</summary>
    public PlayerDefinition? At(Cell cell)
    {
        foreach (var slot in Lineup.Slots)
        {
            if (slot.HomeCell == cell)
            {
                return Find(slot.PlayerId);
            }
        }

        return null;
    }

    /// <summary>Casilla-hogar del jugador, o null si está en el banquillo.</summary>
    public Cell? CellOf(int playerId)
    {
        foreach (var slot in Lineup.Slots)
        {
            if (slot.PlayerId == playerId)
            {
                return slot.HomeCell;
            }
        }

        return null;
    }

    /// <summary>True si el jugador está en la alineación.</summary>
    public bool IsStarter(int playerId) => CellOf(playerId) is not null;

    /// <summary>Alineación resultante de dejar al jugador en esa casilla, <b>sin</b> aplicarla (RF-045: previsualización).</summary>
    public Lineup Preview(int playerId, Cell target) => PlacementView.WithPlayerAt(Lineup, Players, playerId, target);

    /// <summary>Aplica el movimiento. La regla es de <c>/Sim</c>; aquí solo se guarda el resultado.</summary>
    public bool Move(int playerId, Cell target)
    {
        var next = Preview(playerId, target);
        if (ReferenceEquals(next, Lineup))
        {
            return false;
        }

        Team = Team with { Lineup = next };
        return true;
    }

    /// <summary>Todos los JSON de <c>/data</c> salvo los esquemas, con la ruta relativa que espera el cargador.</summary>
    private static Dictionary<string, string> ReadDataFiles(string dataDirectory)
    {
        var files = new Dictionary<string, string>();
        string schemas = Path.Combine(dataDirectory, "schemas") + Path.DirectorySeparatorChar;
        var paths = new List<string>(Directory.EnumerateFiles(dataDirectory, "*.json", SearchOption.AllDirectories));
        paths.Sort(StringComparer.Ordinal);

        foreach (string path in paths)
        {
            if (path.StartsWith(schemas, StringComparison.Ordinal))
            {
                continue;
            }

            files[Path.GetRelativePath(dataDirectory, path).Replace(Path.DirectorySeparatorChar, '/')] = File.ReadAllText(path);
        }

        return files;
    }

    /// <summary>Sube directorios desde el proyecto de Godot hasta encontrar <c>data/sim/tuning.json</c>.</summary>
    private static string FindDataDirectory()
    {
        var candidates = new List<string>
        {
            ProjectSettings.GlobalizePath("res://"),
            AppContext.BaseDirectory,
        };

        foreach (string start in candidates)
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                string data = Path.Combine(dir.FullName, "data");
                if (File.Exists(Path.Combine(data, "sim", "tuning.json")))
                {
                    return data;
                }

                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException("no se encontró data/sim/tuning.json subiendo desde res:// ni desde el ensamblado");
    }
}
