using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using SimLineup = Underleague.Sim.Model.Lineup;

namespace Underleague.Balance;

/// <summary>Un perk asignado a un titular de una build por su slot (docs/fase1-diseno.md §8).</summary>
public sealed record BuildPerkAssignment(int Slot, string Perk);

/// <summary>
/// Una build de <c>/Balance</c> (<c>data/balance/builds/&lt;id&gt;.json</c>, docs/fase1-diseno.md §8):
/// raza, calidad y perks asignados a titulares por <c>slot</c> (0 GK, 1-2 DEF, 3-5 MID, 6 FWD, el mismo
/// orden que <see cref="TeamGenerator"/> usa para generar los titulares), con rarezas y alineación
/// opcionales. Es un dato puro: <see cref="ToTeamSetup"/> es lo único que hace E/S-libre trabajo real
/// (generar el equipo), <see cref="Load"/>/<see cref="Parse"/> son la única E/S (lectura de fichero).
/// </summary>
public sealed record BuildConfig(
    string Id,
    string Name,
    Race Race,
    int Quality,
    IReadOnlyList<BuildPerkAssignment> Perks,
    IReadOnlyDictionary<int, Rarity> Rarities,
    IReadOnlyList<Cell>? Lineup,
    int Level = 1,
    Rarity? UniformRarity = null,
    IReadOnlyDictionary<int, StyleTag>? Styles = null,
    IReadOnlyDictionary<int, IReadOnlyList<Trait>>? Traits = null,
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>>? Counters = null)
{
    /// <summary>Número de titulares de un equipo (GK, DEF, DEF, MID, MID, MID, FWD).</summary>
    public const int StarterCount = 7;

    public static BuildConfig Load(string path) => Parse(path, File.ReadAllText(path));

    public static BuildConfig Parse(string path, string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        string id = RequireString(root, path, "id");
        string name = RequireString(root, path, "name");
        string raceText = RequireString(root, path, "race");
        if (!Enum.TryParse<Race>(raceText, out var race))
        {
            throw new FormatException($"{path}: raza desconocida '{raceText}'");
        }

        int quality = RequireInt(root, path, "quality");

        int level = 1;
        if (root.TryGetProperty("level", out var levelElement) && levelElement.ValueKind == JsonValueKind.Number)
        {
            level = levelElement.GetInt32();
            if (level is < 1 or > 8)
            {
                throw new FormatException($"{path}: 'level' fuera de 1..8");
            }
        }

        Rarity? uniformRarity = null;
        if (root.TryGetProperty("rarity", out var uniformElement) && uniformElement.ValueKind == JsonValueKind.String)
        {
            if (!Enum.TryParse<Rarity>(uniformElement.GetString(), ignoreCase: true, out var parsedUniform))
            {
                throw new FormatException($"{path}: rareza desconocida '{uniformElement.GetString()}' en 'rarity'");
            }

            uniformRarity = parsedUniform;
        }

        var perks = new List<BuildPerkAssignment>();
        if (root.TryGetProperty("perks", out var perksElement) && perksElement.ValueKind != JsonValueKind.Null)
        {
            if (perksElement.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException($"{path}: 'perks' debe ser un array");
            }

            foreach (var entry in perksElement.EnumerateArray())
            {
                int slot = RequireInt(entry, path, "slot");
                string perk = RequireString(entry, path, "perk");
                if (slot < 0 || slot >= StarterCount)
                {
                    throw new FormatException($"{path}: slot {slot} fuera de rango (0..{StarterCount - 1})");
                }

                perks.Add(new BuildPerkAssignment(slot, perk));
            }
        }

        var rarities = new Dictionary<int, Rarity>();
        if (root.TryGetProperty("rarities", out var raritiesElement) && raritiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in raritiesElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out int slot) || slot < 0 || slot >= StarterCount)
                {
                    throw new FormatException($"{path}: clave de 'rarities' inválida '{property.Name}'");
                }

                string rarityText = property.Value.GetString()
                    ?? throw new FormatException($"{path}: 'rarities.{property.Name}' debe ser una cadena");
                if (!Enum.TryParse<Rarity>(rarityText, ignoreCase: true, out var rarity))
                {
                    throw new FormatException($"{path}: rareza desconocida '{rarityText}' en 'rarities.{property.Name}'");
                }

                rarities[slot] = rarity;
            }
        }

        IReadOnlyList<Cell>? lineup = null;
        if (root.TryGetProperty("lineup", out var lineupElement) && lineupElement.ValueKind == JsonValueKind.Array)
        {
            var cells = new List<Cell>();
            foreach (var cellElement in lineupElement.EnumerateArray())
            {
                if (cellElement.ValueKind != JsonValueKind.Array || cellElement.GetArrayLength() != 2)
                {
                    throw new FormatException($"{path}: cada entrada de 'lineup' debe ser [columna, fila]");
                }

                cells.Add(new Cell(cellElement[0].GetInt32(), cellElement[1].GetInt32()));
            }

            if (cells.Count != StarterCount)
            {
                throw new FormatException($"{path}: 'lineup' debe tener {StarterCount} casillas, tiene {cells.Count}");
            }

            lineup = cells;
        }

        var styles = new Dictionary<int, StyleTag>();
        if (root.TryGetProperty("styles", out var stylesElement) && stylesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in stylesElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out int slot) || slot < 0 || slot >= StarterCount)
                {
                    throw new FormatException($"{path}: clave de 'styles' inválida '{property.Name}'");
                }

                if (!Enum.TryParse<StyleTag>(property.Value.GetString(), ignoreCase: false, out var style))
                {
                    throw new FormatException($"{path}: etiqueta de estilo desconocida '{property.Value.GetString()}'");
                }

                styles[slot] = style;
            }
        }

        var traits = new Dictionary<int, IReadOnlyList<Trait>>();
        if (root.TryGetProperty("traits", out var traitsElement) && traitsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in traitsElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out int slot) || slot < 0 || slot >= StarterCount)
                {
                    throw new FormatException($"{path}: clave de 'traits' inválida '{property.Name}'");
                }

                var list = new List<Trait>();
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (!Enum.TryParse<Trait>(item.GetString(), ignoreCase: false, out var trait))
                    {
                        throw new FormatException($"{path}: rasgo desconocido '{item.GetString()}'");
                    }

                    list.Add(trait);
                }

                traits[slot] = list;
            }
        }

        var counters = new Dictionary<int, IReadOnlyDictionary<string, int>>();
        if (root.TryGetProperty("counters", out var countersElement) && countersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in countersElement.EnumerateObject())
            {
                if (!int.TryParse(property.Name, out int slot) || slot < 0 || slot >= StarterCount)
                {
                    throw new FormatException($"{path}: clave de 'counters' inválida '{property.Name}'");
                }

                var values = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var counter in property.Value.EnumerateObject())
                {
                    values[counter.Name] = counter.Value.GetInt32();
                }

                counters[slot] = values;
            }
        }

        return new BuildConfig(id, name, race, quality, perks, rarities, lineup, level, uniformRarity, styles, traits, counters);
    }

    /// <summary>Carga todas las builds de un directorio: todo *.json (los grupos viven fuera, en data/balance/groups.json).</summary>
    public static IReadOnlyDictionary<string, BuildConfig> LoadAll(string buildsDir)
    {
        var result = new Dictionary<string, BuildConfig>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(buildsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            string fileName = Path.GetFileName(file);
            if (fileName.StartsWith('_'))
            {
                continue;
            }

            var build = Load(file);
            if (!result.TryAdd(build.Id, build))
            {
                throw new FormatException($"{file}: id de build repetido '{build.Id}'");
            }
        }

        return result;
    }

    /// <summary>
    /// Construye el <see cref="TeamSetup"/> de la build (docs/fase1-diseno.md §8): genera el equipo con
    /// <see cref="TeamGenerator.Generate"/> (raza y calidad de la build, o <paramref name="qualityOverride"/>
    /// si se da, para la campaña de calidad creciente del rival), sube la rareza de los titulares
    /// indicados por <c>rarities</c> y les asigna los perks de <c>perks</c> por slot; si hay <c>lineup</c>,
    /// sustituye las 7 casillas-hogar de los titulares por las indicadas (mismo orden que los slots).
    /// La validación de la build (slots por rareza, positionOnly, tagsRequired/tagsForbidden) la hace
    /// <c>Simulator.Run</c> al recibir el <see cref="TeamSetup"/>; un <c>ArgumentException</c> desde ahí ya
    /// nombra el equipo (Id de la build), el jugador y el perk (Sim/Engine/Simulator.cs, ValidatePerks).
    /// </summary>
    public TeamSetup ToTeamSetup(ref Pcg32 rng, Catalog catalog, int firstPlayerId, int? qualityOverride = null)
    {
        var generated = TeamGenerator.Generate(ref rng, catalog, Id, Race, qualityOverride ?? Quality, firstPlayerId, Level, UniformRarity, Styles, Traits);
        var players = generated.Players.ToList();

        foreach (var (slot, rarity) in Rarities)
        {
            int index = IndexOfSlot(players, firstPlayerId, slot);
            players[index] = players[index] with { Rarity = rarity };
        }

        var perksBySlot = new Dictionary<int, List<string>>();
        foreach (var assignment in Perks)
        {
            if (!perksBySlot.TryGetValue(assignment.Slot, out var list))
            {
                list = new List<string>();
                perksBySlot[assignment.Slot] = list;
            }

            list.Add(assignment.Perk);
        }

        foreach (var (slot, perkIds) in perksBySlot)
        {
            int index = IndexOfSlot(players, firstPlayerId, slot);
            players[index] = players[index] with { Perks = perkIds };
        }

        // Contadores de carrera con los que entra el titular (RF-070): es lo que separa "buena" de "muy
        // buena" en la escala de la ADR 0033, porque los perks de acumulación valen 0 con el contador a
        // cero. EffectEngine los siembra desde PlayerDefinition.Counters.
        if (Counters is not null)
        {
            foreach (var (slot, values) in Counters.OrderBy(c => c.Key))
            {
                int index = IndexOfSlot(players, firstPlayerId, slot);
                players[index] = players[index].WithCounters(values);
            }
        }

        SimLineup lineup;
        if (Lineup is { } cells)
        {
            var slots = new List<LineupSlot>(StarterCount);
            for (int i = 0; i < StarterCount; i++)
            {
                slots.Add(new LineupSlot(players[i].Id, cells[i]));
            }

            lineup = new SimLineup(slots);
        }
        else
        {
            lineup = SimLineup.Default(players.Take(StarterCount).ToList());
        }

        return new TeamSetup(Id, Name, Race, players, lineup);
    }

    private static int IndexOfSlot(List<PlayerDefinition> players, int firstPlayerId, int slot)
    {
        int playerId = firstPlayerId + slot;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == playerId)
            {
                return i;
            }
        }

        throw new InvalidOperationException(
            $"slot {slot} no corresponde a ningún titular generado (playerId esperado {playerId})");
    }

    private static string RequireString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"{path}: falta o es inválida la propiedad de cadena '{property}'");
        }

        return value.GetString()!;
    }

    private static int RequireInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || !value.TryGetInt32(out int result))
        {
            throw new FormatException($"{path}: falta o es inválida la propiedad entera '{property}'");
        }

        return result;
    }
}

/// <summary>
/// Grupos de builds para las métricas de fase 1 (<see cref="Underleague.Sim.Analysis.BuildMetrics"/>):
/// coherentes, malas a propósito, aleatoria y la build de referencia sin perks de cada raza.
/// Vive en <c>data/balance/groups.json</c>; no es una build (el nombre empieza por '_' y
/// <see cref="BuildConfig.LoadAll"/> lo ignora al enumerar el directorio).
/// </summary>
public sealed record BuildGroups(
    IReadOnlyList<string> Coherent,
    IReadOnlyList<string> Bad,
    IReadOnlyList<string> Random,
    IReadOnlyDictionary<string, string> BaselineByRace,
    IReadOnlyDictionary<string, IReadOnlyList<string>> QualityLevels)
{
    public static BuildGroups Load(string path) => Parse(path, File.ReadAllText(path));

    public static BuildGroups Parse(string path, string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var coherent = RequireStringArray(root, path, "coherent");
        var bad = RequireStringArray(root, path, "bad");
        var random = RequireStringArray(root, path, "random");

        var baselineByRace = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("baselineByRace", out var baselineElement) && baselineElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in baselineElement.EnumerateObject())
            {
                baselineByRace[property.Name] = property.Value.GetString()
                    ?? throw new FormatException($"{path}: 'baselineByRace.{property.Name}' debe ser una cadena");
            }
        }

        // Escala de calidad de build de la ADR 0033 (paquete Y): los cuatro escalones por raza.
        var qualityLevels = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (root.TryGetProperty("qualityLevels", out var levelsElement) && levelsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in levelsElement.EnumerateObject())
            {
                var ids = new List<string>();
                foreach (var item in property.Value.EnumerateArray())
                {
                    ids.Add(item.GetString() ?? throw new FormatException($"{path}: entrada nula en 'qualityLevels.{property.Name}'"));
                }

                qualityLevels[property.Name] = ids;
            }
        }

        return new BuildGroups(coherent, bad, random, baselineByRace, qualityLevels);
    }

    /// <summary>
    /// Resuelve, para cada build de <paramref name="builds"/>, el id de su build de referencia según su
    /// raza (<c>baselineByRace</c>). Ayudante para llamar a
    /// <see cref="Underleague.Sim.Analysis.BuildMetrics.CoherentBuildsBeatNone"/> sin que ese código puro
    /// necesite conocer <see cref="Race"/> ni <see cref="BuildConfig"/>.
    /// </summary>
    public Dictionary<string, string> ResolveBaselines(IReadOnlyDictionary<string, BuildConfig> builds, IEnumerable<string> buildIds)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in buildIds)
        {
            if (!builds.TryGetValue(id, out var build))
            {
                continue;
            }

            if (BaselineByRace.TryGetValue(build.Race.ToString(), out var baseline))
            {
                result[id] = baseline;
            }
        }

        return result;
    }

    private static IReadOnlyList<string> RequireStringArray(JsonElement root, string path, string property)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"{path}: falta el array '{property}'");
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(item.GetString() ?? throw new FormatException($"{path}: entrada nula en '{property}'"));
        }

        return list;
    }
}
