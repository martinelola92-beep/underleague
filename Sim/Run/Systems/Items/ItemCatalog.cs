using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Catálogo de objetos de equipamiento, cargado de <c>data/items/*.json</c> (RF-075..078, 12 objetos en
/// esta fase). Ordenado por id ordinal ascendente, igual que <c>Sim.Perks.PerkCatalog</c> (RT-041).
/// </summary>
public sealed class ItemCatalog
{
    private readonly ItemDefinition[] _items;
    private readonly Dictionary<string, ItemDefinition> _byId;

    public ItemCatalog(IEnumerable<ItemDefinition> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, ItemDefinition>(_items.Length, StringComparer.Ordinal);
        foreach (var item in _items)
        {
            _byId.Add(item.Id, item);
        }
    }

    /// <summary>Objetos ordenados por id ordinal ascendente.</summary>
    public IReadOnlyList<ItemDefinition> All => _items;

    /// <summary>Busca un objeto por id; null si no existe.</summary>
    public ItemDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Busca un objeto por id; lanza si no existe.</summary>
    public ItemDefinition Get(string id) =>
        Find(id) ?? throw new InvalidOperationException($"objeto no encontrado en el catálogo: {id}");
}

/// <summary>Carga <c>data/items/*.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class ItemLoader
{
    public static ItemCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var items = new List<ItemDefinition>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("items/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            items.Add(Parse(path, files[path]));
        }

        if (items.Count == 0)
        {
            throw new DataException("items/", "$", "no se ha encontrado ningún objeto en data/items/");
        }

        return new ItemCatalog(items);
    }

    private static ItemDefinition Parse(string path, string content)
    {
        using var document = ParseJson(path, content);
        var root = Json.Root(path, document);

        string id = root.Str("id");
        var name = LocalizedNameJson.Read(root.Prop("name"));
        var rarity = ParseRarity(root, root.Str("rarity"));
        var archetype = ParseArchetype(root, root.Str("archetype"));
        var effects = EffectJson.ReadList(root.Prop("effects"));
        if (effects.Count == 0)
        {
            throw new DataException(path, "$.effects", "un objeto necesita al menos un efecto");
        }

        var drawback = EffectJson.ReadList(root.TryProp("drawbackEffects"));
        int usesLimit = root.OptionalInt("usesLimit", 0);
        string requiredTag = root.OptionalStr("requiredTag", string.Empty);

        if (archetype == ItemArchetype.Cursed && drawback.Count == 0)
        {
            throw new DataException(path, "$.drawbackEffects", "un objeto maldito necesita al menos una contrapartida (RF-077)");
        }

        if (archetype != ItemArchetype.Cursed && drawback.Count > 0)
        {
            throw new DataException(path, "$.drawbackEffects", "solo un objeto maldito lleva contrapartida (RF-077)");
        }

        if (archetype == ItemArchetype.Fragile && usesLimit <= 0)
        {
            throw new DataException(path, "$.usesLimit", "un objeto frágil necesita un número positivo de usos (RF-077)");
        }

        if (archetype != ItemArchetype.Fragile && usesLimit != 0)
        {
            throw new DataException(path, "$.usesLimit", "solo un objeto frágil lleva usesLimit (RF-077)");
        }

        if (archetype == ItemArchetype.Restricted && requiredTag.Length == 0)
        {
            throw new DataException(path, "$.requiredTag", "un objeto restringido necesita una etiqueta (RF-077)");
        }

        if (archetype != ItemArchetype.Restricted && requiredTag.Length > 0)
        {
            throw new DataException(path, "$.requiredTag", "solo un objeto restringido lleva requiredTag (RF-077)");
        }

        return new ItemDefinition(id, name, rarity, archetype, effects, drawback, usesLimit, requiredTag);
    }

    private static Rarity ParseRarity(Json node, string rarity) => rarity switch
    {
        "common" => Rarity.Common,
        "rare" => Rarity.Rare,
        "legendary" => Rarity.Legendary,
        _ => throw new DataException(node.File, node.Path + ".rarity", $"rareza desconocida: '{rarity}'"),
    };

    private static ItemArchetype ParseArchetype(Json node, string archetype) => archetype switch
    {
        "normal" => ItemArchetype.Normal,
        "cursed" => ItemArchetype.Cursed,
        "fragile" => ItemArchetype.Fragile,
        "restricted" => ItemArchetype.Restricted,
        _ => throw new DataException(node.File, node.Path + ".archetype", $"arquetipo desconocido: '{archetype}'"),
    };

    private static JsonDocument ParseJson(string path, string content)
    {
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(path, "$", $"JSON inválido: {ex.Message}");
        }
    }
}
