using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Catálogo de objetos de equipamiento, cargado de <c>data/items/*.json</c> (RF-075..078, ADR 0036).
/// Ordenado por id ordinal ascendente, igual que <c>Sim.Perks.PerkCatalog</c> (RT-041).
/// </summary>
public sealed class ItemCatalog
{
    private readonly ItemDefinition[] _items;
    private readonly Dictionary<string, ItemDefinition> _byId;

    public ItemCatalog(IEnumerable<ItemDefinition> items, ItemScale scale)
    {
        ArgumentNullException.ThrowIfNull(items);
        Scale = scale ?? throw new ArgumentNullException(nameof(scale));
        _items = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, ItemDefinition>(_items.Length, StringComparer.Ordinal);
        foreach (var item in _items)
        {
            _byId.Add(item.Id, item);
        }
    }

    /// <summary>Escala del equipamiento y tabla de valor marginal (ADR 0036, ADR 0038).</summary>
    public ItemScale Scale { get; }

    /// <summary>Objetos ordenados por id ordinal ascendente.</summary>
    public IReadOnlyList<ItemDefinition> All => _items;

    /// <summary>
    /// Objetos que pueden aparecer en una run de esa raza (ADR 0036): los universales, más los
    /// restringidos de <b>su</b> raza y ninguno más. Orden de id ordinal ascendente (RT-041).
    /// </summary>
    public IReadOnlyList<ItemDefinition> OfferableTo(Race clubRace)
    {
        var result = new List<ItemDefinition>(_items.Length);
        foreach (var item in _items)
        {
            if (item.Race is null || item.Race == clubRace)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>Busca un objeto por id; null si no existe.</summary>
    public ItemDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Busca un objeto por id; lanza si no existe.</summary>
    public ItemDefinition Get(string id) =>
        Find(id) ?? throw new InvalidOperationException($"objeto no encontrado en el catálogo: {id}");
}

/// <summary>
/// Carga <c>data/items/*.json</c> (RT-012: sin E/S, recibe el contenido ya leído).
///
/// <para><b>La validación es trivial, y eso es la ventaja</b> (ADR 0036): un objeto declara
/// <c>attributeBonus</c> y nada más, el número de atributos que sube tiene que coincidir con el que su
/// rareza permite, y la magnitud tiene que ser la de la escala (doble en el maldito). Cualquier
/// <c>effects</c> —el formato anterior, que permitía a un objeto hacer lo mismo que un perk— es un error
/// explícito.</para>
/// </summary>
public static class ItemLoader
{
    public static ItemCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var scale = ItemScale.FromJson(files);
        var items = new List<ItemDefinition>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("items/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            items.Add(Parse(path, files[path], scale));
        }

        if (items.Count == 0)
        {
            throw new DataException("items/", "$", "no se ha encontrado ningún objeto en data/items/");
        }

        return new ItemCatalog(items, scale);
    }

    private static ItemDefinition Parse(string path, string content, ItemScale scale)
    {
        using var document = ParseJson(path, content);
        var root = Json.Root(path, document);

        if (root.TryProp("effects") is not null || root.TryProp("drawbackEffects") is not null)
        {
            throw new DataException(path, "$.effects",
                "un objeto sube atributos y nada más (ADR 0036): usa 'attributeBonus'. Los efectos con "
                    + "disparador, condición o canal de probabilidad son lo que hace un perk, no un objeto");
        }

        string id = root.Str("id");
        var name = LocalizedNameJson.Read(root.Prop("name"));
        var archetype = ParseArchetype(root, root.Str("archetype"));
        var modifier = ReadModifier(root.Prop("attributeBonus"));

        bool restricted = archetype == ItemArchetype.Restricted;
        var rarityNode = root.TryProp("rarity");
        if (restricted && rarityNode is not null)
        {
            throw new DataException(path, "$.rarity", "un objeto restringido no tiene rareza (ADR 0036): la fija su arquetipo");
        }

        if (!restricted && rarityNode is null)
        {
            throw new DataException(path, "$.rarity", "un objeto necesita rareza: es lo que fija cuántos atributos sube (ADR 0036)");
        }

        // El restringido no declara rareza; vale exactamente lo que un raro (tres atributos con magnitud
        // normal) y se le asigna esa banda para precio y venta.
        var rarity = restricted ? Rarity.Rare : ParseRarity(root, root.Str("rarity"));

        Race? race = null;
        string requiredTag = string.Empty;
        var raceNode = root.TryProp("race");
        if (restricted)
        {
            if (raceNode is null)
            {
                throw new DataException(path, "$.race", "un objeto restringido es exclusivo de una raza (ADR 0036)");
            }

            race = ParseRace(root, root.Str("race"));
            requiredTag = race.Value.ToString();
        }
        else if (raceNode is not null)
        {
            throw new DataException(path, "$.race", "solo un objeto restringido declara raza (ADR 0036)");
        }

        int breakChance = root.OptionalInt("breakChancePercent", 0);
        if (archetype == ItemArchetype.Fragile && breakChance is <= 0 or >= 100)
        {
            throw new DataException(path, "$.breakChancePercent",
                "un objeto frágil necesita una probabilidad de rotura entre 1 y 99 (RF-077, ADR 0036: se resuelve al terminar el partido y se anuncia antes de equipar)");
        }

        if (archetype != ItemArchetype.Fragile && breakChance != 0)
        {
            throw new DataException(path, "$.breakChancePercent", "solo un objeto frágil se rompe (RF-077)");
        }

        var item = new ItemDefinition(id, name, rarity, archetype, modifier, breakChance, race, requiredTag);
        Validate(path, item, scale);
        return item;
    }

    /// <summary>
    /// Comprueba el contrato de la ADR 0036: cuántos atributos sube (rareza), con qué magnitud
    /// (arquetipo) y qué contrapartida lleva.
    /// </summary>
    private static void Validate(string path, ItemDefinition item, ItemScale scale)
    {
        int expectedCount = item.Archetype == ItemArchetype.Restricted
            ? scale.RestrictedAttributes
            : scale.AttributesFor(item.Rarity);

        int magnitude = item.Archetype == ItemArchetype.Cursed
            ? scale.AttributeBonus * scale.CursedMultiplier
            : scale.AttributeBonus;

        var raised = item.Raised;
        if (raised.Count != expectedCount)
        {
            throw new DataException(path, "$.attributeBonus",
                $"un objeto {Describe(item)} sube exactamente {expectedCount} atributo(s) y este sube {raised.Count} (ADR 0036)");
        }

        foreach (var kind in raised)
        {
            if (item.Modifier.Get(kind) != magnitude)
            {
                throw new DataException(path, "$.attributeBonus",
                    $"la magnitud de un objeto {Describe(item)} es +{magnitude} por atributo y '{kind}' vale {item.Modifier.Get(kind)} (ADR 0036)");
            }
        }

        var lowered = item.Lowered;
        if (item.Archetype == ItemArchetype.Cursed)
        {
            if (lowered.Count != 1)
            {
                throw new DataException(path, "$.attributeBonus",
                    $"un objeto maldito baja exactamente un atributo, y este baja {lowered.Count} (ADR 0036)");
            }

            if (item.Modifier.Get(lowered[0]) != -magnitude)
            {
                throw new DataException(path, "$.attributeBonus",
                    $"un objeto maldito baja el doble: '{lowered[0]}' debería valer {-magnitude} (ADR 0036)");
            }
        }
        else if (lowered.Count > 0)
        {
            throw new DataException(path, "$.attributeBonus", "solo un objeto maldito baja un atributo (ADR 0036)");
        }
    }

    private static string Describe(ItemDefinition item) => item.Archetype switch
    {
        ItemArchetype.Cursed => "maldito",
        ItemArchetype.Fragile => "frágil " + RarityName(item.Rarity),
        ItemArchetype.Restricted => "restringido",
        _ => RarityName(item.Rarity),
    };

    private static string RarityName(Rarity rarity) => rarity switch
    {
        Rarity.Common => "común",
        Rarity.Uncommon => "poco común",
        Rarity.Rare => "raro",
        _ => "legendario",
    };

    private static Attributes ReadModifier(Json node)
    {
        var attributes = default(Attributes);
        foreach (var kind in ItemScale.AttributeOrder)
        {
            string name = kind.ToString();
            string key = char.ToLowerInvariant(name[0]) + name[1..];
            if (node.TryProp(key) is { } value)
            {
                attributes = attributes.With(kind, value.AsInt());
            }
        }

        return attributes;
    }

    private static Rarity ParseRarity(Json node, string rarity) => rarity switch
    {
        "common" => Rarity.Common,
        "uncommon" => Rarity.Uncommon,
        "rare" => Rarity.Rare,
        "legendary" => Rarity.Legendary,
        _ => throw new DataException(node.File, node.Path + ".rarity", $"rareza desconocida: '{rarity}'"),
    };

    private static Race ParseRace(Json node, string race) =>
        Enum.TryParse<Race>(race, ignoreCase: false, out var parsed)
            ? parsed
            : throw new DataException(node.File, node.Path + ".race", $"raza desconocida: '{race}'");

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
