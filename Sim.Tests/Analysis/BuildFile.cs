using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Lectura mínima de <c>data/balance/builds/*.json</c>. /Balance tiene la suya (BuildConfig); aquí no
/// se comparte proyecto, igual que con reference.json en la puerta de fase 0.
/// </summary>
internal sealed record BuildFile(
    string Id,
    Race Race,
    int Quality,
    IReadOnlyList<(int Slot, string Perk)> Perks,
    IReadOnlyDictionary<int, Rarity> Rarities,
    IReadOnlyList<Cell>? Lineup,
    int Level,
    Rarity? UniformRarity,
    IReadOnlyDictionary<int, StyleTag> Styles,
    IReadOnlyDictionary<int, IReadOnlyList<Trait>> Traits)
{
    private const int StarterCount = 7;

    public static IReadOnlyDictionary<string, BuildFile> LoadAll(string dataDirectory)
    {
        var result = new Dictionary<string, BuildFile>(StringComparer.Ordinal);
        string dir = Path.Combine(dataDirectory, "balance", "builds");
        foreach (var path in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var build = Parse(File.ReadAllText(path));
            result[build.Id] = build;
        }

        return result;
    }

    private static BuildFile Parse(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var perks = new List<(int, string)>();
        if (root.TryGetProperty("perks", out var perksElement) && perksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in perksElement.EnumerateArray())
            {
                perks.Add((entry.GetProperty("slot").GetInt32(), entry.GetProperty("perk").GetString()!));
            }
        }

        var rarities = new Dictionary<int, Rarity>();
        if (root.TryGetProperty("rarities", out var raritiesElement) && raritiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in raritiesElement.EnumerateObject())
            {
                rarities[int.Parse(property.Name)] = Enum.Parse<Rarity>(property.Value.GetString()!, ignoreCase: true);
            }
        }

        List<Cell>? lineup = null;
        if (root.TryGetProperty("lineup", out var lineupElement) && lineupElement.ValueKind == JsonValueKind.Array)
        {
            lineup = new List<Cell>();
            foreach (var cell in lineupElement.EnumerateArray())
            {
                lineup.Add(new Cell(cell[0].GetInt32(), cell[1].GetInt32()));
            }
        }

        int level = root.TryGetProperty("level", out var levelElement) ? levelElement.GetInt32() : 1;
        Rarity? uniformRarity = root.TryGetProperty("rarity", out var uniformElement)
            ? Enum.Parse<Rarity>(uniformElement.GetString()!, ignoreCase: true)
            : null;

        var styles = new Dictionary<int, StyleTag>();
        if (root.TryGetProperty("styles", out var stylesElement) && stylesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in stylesElement.EnumerateObject())
            {
                styles[int.Parse(property.Name)] = Enum.Parse<StyleTag>(property.Value.GetString()!);
            }
        }

        var extraTraits = new Dictionary<int, IReadOnlyList<Trait>>();
        if (root.TryGetProperty("traits", out var traitsElement) && traitsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in traitsElement.EnumerateObject())
            {
                extraTraits[int.Parse(property.Name)] =
                    property.Value.EnumerateArray().Select(t => Enum.Parse<Trait>(t.GetString()!)).ToList();
            }
        }

        return new BuildFile(
            root.GetProperty("id").GetString()!,
            Enum.Parse<Race>(root.GetProperty("race").GetString()!),
            root.GetProperty("quality").GetInt32(),
            perks,
            rarities,
            lineup,
            level,
            uniformRarity,
            styles,
            extraTraits);
    }

    /// <summary>
    /// Mismo esquema de generación que /Balance: RngStreams.Generation(semilla, índice).
    /// <paramref name="levelOverride"/> y <paramref name="rarityOverride"/> sirven a las métricas de
    /// rareza y de jefe final (ADR 0027): la misma build jugada con una plantilla de otro nivel o de otra
    /// rareza, sin duplicar el fichero de build.
    /// </summary>
    public TeamSetup ToTeamSetup(Catalog catalog, ulong seed, int generationIndex, int firstPlayerId, int? levelOverride = null, Rarity? rarityOverride = null)
    {
        var rng = RngStreams.Generation(seed, generationIndex);
        var generated = TeamGenerator.Generate(
            ref rng, catalog, Id, Race, Quality, firstPlayerId,
            levelOverride ?? Level, rarityOverride ?? UniformRarity, Styles, Traits);
        var players = generated.Players.ToList();

        if (rarityOverride is null)
        {
            foreach (var (slot, rarity) in Rarities.OrderBy(r => r.Key))
            {
                players[slot] = players[slot] with { Rarity = rarity };
            }
        }

        var bySlot = new Dictionary<int, List<string>>();
        foreach (var (slot, perk) in Perks)
        {
            if (!bySlot.TryGetValue(slot, out var list))
            {
                list = new List<string>();
                bySlot[slot] = list;
            }

            list.Add(perk);
        }

        foreach (var (slot, list) in bySlot.OrderBy(e => e.Key))
        {
            players[slot] = players[slot] with { Perks = list };
        }

        Lineup lineup;
        if (Lineup is { } cells)
        {
            var slots = new List<LineupSlot>(StarterCount);
            for (int i = 0; i < StarterCount; i++)
            {
                slots.Add(new LineupSlot(players[i].Id, cells[i]));
            }

            lineup = new Lineup(slots);
        }
        else
        {
            lineup = Model.Lineup.Default(players.Take(StarterCount).ToList());
        }

        return new TeamSetup(Id, Id, Race, players, lineup);
    }
}

/// <summary>Lectura mínima de <c>data/balance/groups.json</c> (§8, paquete H).</summary>
internal sealed record BuildGroupsFile(
    IReadOnlyList<string> Coherent,
    IReadOnlyList<string> Bad,
    IReadOnlyList<string> Random,
    IReadOnlyDictionary<string, string> BaselineByRace)
{
    public static BuildGroupsFile Load(string dataDirectory)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(dataDirectory, "balance", "groups.json")));
        var root = document.RootElement;

        static List<string> Array(JsonElement root, string name) =>
            root.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToList();

        var baselines = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in root.GetProperty("baselineByRace").EnumerateObject())
        {
            baselines[property.Name] = property.Value.GetString()!;
        }

        return new BuildGroupsFile(
            Array(root, "coherent"), Array(root, "bad"), Array(root, "random"), baselines);
    }
}
