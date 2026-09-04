using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Bosses;

/// <summary>
/// Catálogo de jefes cargado de <c>data/bosses/*.json</c> (RF-001b, RF-001c). Sin E/S, igual que
/// <see cref="DataLoader"/>: recibe el contenido de los ficheros ya leído y lo valida contra el mismo
/// criterio que el esquema <c>data/schemas/bosses.schema.json</c>; un dato inválido es
/// <see cref="DataException"/> con fichero y ruta JSON, nunca un fallo silencioso (RT-032).
///
/// <para>Vive en <c>Sim/Run/Boss/</c> y no en <see cref="Catalog"/> porque el jefe es una pieza del
/// bucle de run, no del partido: el motor no necesita saber que existe.</para>
/// </summary>
public sealed class BossCatalog
{
    private readonly BossDefinition[] _bosses;
    private readonly Dictionary<string, BossDefinition> _byId;
    private readonly Dictionary<string, BossModifier> _modifiersById;

    /// <summary>Catálogo vacío: una run sin jefes definidos todavía.</summary>
    public static BossCatalog Empty { get; } = new(Array.Empty<BossDefinition>());

    public BossCatalog(IEnumerable<BossDefinition> bosses)
    {
        ArgumentNullException.ThrowIfNull(bosses);
        _bosses = bosses.OrderBy(b => b.Act).ThenBy(b => b.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, BossDefinition>(_bosses.Length, StringComparer.Ordinal);
        _modifiersById = new Dictionary<string, BossModifier>(StringComparer.Ordinal);
        foreach (var boss in _bosses)
        {
            if (!_byId.TryAdd(boss.Id, boss))
            {
                throw new DataException($"bosses/{boss.Id}.json", "$.id", $"id de jefe repetido: '{boss.Id}'");
            }

            foreach (var modifier in boss.Modifiers)
            {
                if (!_modifiersById.TryAdd(modifier.Id, modifier))
                {
                    throw new DataException(
                        $"bosses/{boss.Id}.json", "$.modifiers", $"id de modificador repetido: '{modifier.Id}'");
                }
            }
        }
    }

    /// <summary>Jefes ordenados por acto y por id.</summary>
    public IReadOnlyList<BossDefinition> All => _bosses;

    /// <summary>Jefe con ese id, o null.</summary>
    public BossDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Jefe de ese acto; lanza si el acto no tiene ninguno o tiene más de uno (RF-001: un jefe por acto).</summary>
    public BossDefinition ForAct(int act)
    {
        BossDefinition? found = null;
        for (int i = 0; i < _bosses.Length; i++)
        {
            if (_bosses[i].Act != act)
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidOperationException($"el acto {act} tiene más de un jefe en data/bosses/");
            }

            found = _bosses[i];
        }

        return found ?? throw new InvalidOperationException($"el acto {act} no tiene jefe en data/bosses/");
    }

    /// <summary>Modificador con ese id, venga del jefe que venga. Es lo que necesita el compendio (RF-014b).</summary>
    public BossModifier? FindModifier(string id) => _modifiersById.GetValueOrDefault(id);

    /// <summary>Modificadores de una lista de ids; lanza si alguno no existe.</summary>
    public IReadOnlyList<BossModifier> Modifiers(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var result = new List<BossModifier>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            result.Add(FindModifier(ids[i])
                ?? throw new ArgumentException($"modificador de jefe desconocido: '{ids[i]}'", nameof(ids)));
        }

        return result;
    }

    /// <summary>files: ruta relativa a /data -&gt; contenido. Solo mira <c>bosses/*.json</c>; ignora el resto.</summary>
    public static BossCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var bosses = new List<BossDefinition>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("bosses/", StringComparison.Ordinal)
                || !path.EndsWith(".json", StringComparison.Ordinal)
                || path.AsSpan(7).Contains('/'))
            {
                continue;
            }

            bosses.Add(Parse(path, files[path]));
        }

        return new BossCatalog(bosses);
    }

    /// <summary>Parsea un fichero de jefe.</summary>
    public static BossDefinition Parse(string path, string content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(path, "$", $"JSON inválido ({ex.Message})");
        }

        using (document)
        {
            var root = document.RootElement;
            string id = RequireString(root, path, "$", "id");
            int act = RequireInt(root, path, "$", "act");
            if (act is < 1 or > RunRules.Acts)
            {
                throw new DataException(path, "$.act", $"acto fuera de 1..{RunRules.Acts}: {act}");
            }

            var name = ParseName(root, path, "$");
            var template = ParseTemplate(RequireObject(root, path, "$", "template"), path, "$.template");
            var modifiers = ParseModifiers(root, path);

            // RF-001b: un modificador en los actos 1 y 2. RF-001c: dos en el jefe final.
            int expected = act == RunRules.Acts ? 2 : 1;
            if (modifiers.Count != expected)
            {
                throw new DataException(
                    path, "$.modifiers",
                    $"el jefe del acto {act} tiene que declarar {expected} modificador(es) de regla y declara {modifiers.Count} (RF-001b, RF-001c)");
            }

            BossDefeatCondition? defeat = null;
            if (root.TryGetProperty("defeatCondition", out var defeatElement)
                && defeatElement.ValueKind == JsonValueKind.Object)
            {
                defeat = new BossDefeatCondition(
                    RequireString(defeatElement, path, "$.defeatCondition", "id"),
                    ParseName(defeatElement, path, "$.defeatCondition"),
                    ParseEnum<BossDefeatConditionKind>(
                        RequireString(defeatElement, path, "$.defeatCondition", "kind"), path, "$.defeatCondition.kind"));
            }

            if (act == RunRules.Acts && defeat is null)
            {
                throw new DataException(
                    path, "$.defeatCondition", "el jefe final necesita una condición de derrota propia (RF-001c, D-9)");
            }

            var gate = RequireObject(root, path, "$", "gate");
            int gateLevel = RequireInt(gate, path, "$.gate", "playerLevel");
            var targets = new List<BossGateTarget>();
            var bands = RequireObject(gate, path, "$.gate", "targets");
            foreach (var property in bands.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                double? min = property.Value.TryGetProperty("min", out var minElement) ? minElement.GetDouble() : null;
                double? max = property.Value.TryGetProperty("max", out var maxElement) ? maxElement.GetDouble() : null;
                targets.Add(new BossGateTarget(property.Name, min, max));
            }

            return new BossDefinition(id, act, name, template, modifiers, defeat, gateLevel, targets);
        }
    }

    private static IReadOnlyList<BossModifier> ParseModifiers(JsonElement root, string path)
    {
        var modifiers = new List<BossModifier>();
        if (!root.TryGetProperty("modifiers", out var element) || element.ValueKind != JsonValueKind.Array)
        {
            throw new DataException(path, "$.modifiers", "falta el array 'modifiers'");
        }

        int index = 0;
        foreach (var entry in element.EnumerateArray())
        {
            string where = $"$.modifiers[{index}]";
            var kind = ParseEnum<BossModifierKind>(RequireString(entry, path, where, "kind"), path, $"{where}.kind");
            var probability = ProbabilityKind.ShotOnTarget;
            if (kind == BossModifierKind.BanChannel)
            {
                probability = ParseEnum<ProbabilityKind>(
                    RequireString(entry, path, where, "probability"), path, $"{where}.probability");
            }

            int column = 0;
            if (kind == BossModifierKind.PushBack)
            {
                column = RequireInt(entry, path, where, "column");
            }

            modifiers.Add(new BossModifier(
                RequireString(entry, path, where, "id"), ParseName(entry, path, where), kind, probability, column));
            index++;
        }

        return modifiers;
    }

    private static BossTemplate ParseTemplate(JsonElement element, string path, string where)
    {
        var race = ParseEnum<Race>(RequireString(element, path, where, "race"), path, $"{where}.race");
        int quality = RequireInt(element, path, where, "quality");
        int level = element.TryGetProperty("level", out var levelElement) ? levelElement.GetInt32() : 1;
        if (level is < 1 or > 8)
        {
            throw new DataException(path, $"{where}.level", $"nivel fuera de 1..8: {level}");
        }

        Rarity? uniform = null;
        if (element.TryGetProperty("rarity", out var rarityElement) && rarityElement.ValueKind == JsonValueKind.String)
        {
            uniform = ParseEnum<Rarity>(rarityElement.GetString()!, path, $"{where}.rarity");
        }

        var perks = new List<BossPerkAssignment>();
        if (element.TryGetProperty("perks", out var perksElement) && perksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in perksElement.EnumerateArray())
            {
                int slot = RequireInt(entry, path, $"{where}.perks", "slot");
                if (slot < 0 || slot >= BossTemplate.StarterCount)
                {
                    throw new DataException(path, $"{where}.perks", $"slot fuera de 0..{BossTemplate.StarterCount - 1}: {slot}");
                }

                perks.Add(new BossPerkAssignment(slot, RequireString(entry, path, $"{where}.perks", "perk")));
            }
        }

        var rarities = new SortedDictionary<int, Rarity>();
        if (element.TryGetProperty("rarities", out var raritiesElement) && raritiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in raritiesElement.EnumerateObject())
            {
                rarities[ParseSlot(property.Name, path, $"{where}.rarities")] =
                    ParseEnum<Rarity>(property.Value.GetString() ?? string.Empty, path, $"{where}.rarities.{property.Name}");
            }
        }

        List<Cell>? lineup = null;
        if (element.TryGetProperty("lineup", out var lineupElement) && lineupElement.ValueKind == JsonValueKind.Array)
        {
            lineup = new List<Cell>();
            foreach (var cell in lineupElement.EnumerateArray())
            {
                if (cell.ValueKind != JsonValueKind.Array || cell.GetArrayLength() != 2)
                {
                    throw new DataException(path, $"{where}.lineup", "cada casilla es [columna, fila]");
                }

                lineup.Add(new Cell(cell[0].GetInt32(), cell[1].GetInt32()));
            }

            if (lineup.Count != BossTemplate.StarterCount)
            {
                throw new DataException(
                    path, $"{where}.lineup", $"la alineación necesita {BossTemplate.StarterCount} casillas y tiene {lineup.Count}");
            }
        }

        var styles = new SortedDictionary<int, StyleTag>();
        if (element.TryGetProperty("styles", out var stylesElement) && stylesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in stylesElement.EnumerateObject())
            {
                styles[ParseSlot(property.Name, path, $"{where}.styles")] =
                    ParseEnum<StyleTag>(property.Value.GetString() ?? string.Empty, path, $"{where}.styles.{property.Name}");
            }
        }

        var traits = new SortedDictionary<int, IReadOnlyList<Trait>>();
        if (element.TryGetProperty("traits", out var traitsElement) && traitsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in traitsElement.EnumerateObject())
            {
                var list = new List<Trait>();
                foreach (var item in property.Value.EnumerateArray())
                {
                    list.Add(ParseEnum<Trait>(item.GetString() ?? string.Empty, path, $"{where}.traits.{property.Name}"));
                }

                traits[ParseSlot(property.Name, path, $"{where}.traits")] = list;
            }
        }

        return new BossTemplate(race, quality, level, uniform, perks, rarities, lineup, styles, traits);
    }

    private static IReadOnlyDictionary<string, string> ParseName(JsonElement element, string path, string where)
    {
        if (!element.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.Object)
        {
            throw new DataException(path, $"{where}.name", "falta el objeto 'name' con los idiomas (es/en)");
        }

        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in name.EnumerateObject())
        {
            result[property.Name] = property.Value.GetString()
                ?? throw new DataException(path, $"{where}.name.{property.Name}", "el nombre tiene que ser una cadena");
        }

        foreach (var language in new[] { "es", "en" })
        {
            if (!result.ContainsKey(language))
            {
                throw new DataException(path, $"{where}.name", $"falta el idioma '{language}'");
            }
        }

        return result;
    }

    private static int ParseSlot(string text, string path, string where) =>
        int.TryParse(text, out int slot) && slot >= 0 && slot < BossTemplate.StarterCount
            ? slot
            : throw new DataException(path, where, $"índice de titular inválido: '{text}'");

    private static T ParseEnum<T>(string text, string path, string where)
        where T : struct, Enum =>
        Enum.TryParse<T>(text, ignoreCase: true, out var value)
            ? value
            : throw new DataException(path, where, $"valor desconocido de {typeof(T).Name}: '{text}'");

    private static JsonElement RequireObject(JsonElement element, string path, string where, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw new DataException(path, $"{where}.{property}", "falta el objeto");

    private static string RequireString(JsonElement element, string path, string where, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new DataException(path, $"{where}.{property}", "falta la propiedad de texto");

    private static int RequireInt(JsonElement element, string path, string where, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out int result)
            ? result
            : throw new DataException(path, $"{where}.{property}", "falta la propiedad entera");
}
