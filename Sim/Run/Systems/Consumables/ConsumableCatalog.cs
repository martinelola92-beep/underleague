using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems.Consumables;

/// <summary>Familia de un consumible (RF-084).</summary>
public enum ConsumableFamily
{
    Medical,
    Tactical,
    Dirty,
    Supernatural,
}

/// <summary>
/// Un consumible vendido en el mercado, cargado de <c>data/consumables/&lt;id&gt;.json</c>.
///
/// <para><b>Por qué existe este catálogo, fuera de la lista de ficheros del encargo.</b> RF-114 exige
/// "cuatro categorías simultáneas: jugadores, perks, equipamiento y consumibles", y el propio encargo del
/// paquete X (fase2-diseno.md §7) las repite las cuatro. Sin un catálogo mínimo de consumibles el mercado
/// se queda en tres categorías y no cumple RF-114 tal cual está escrito, así que el paquete X añade
/// <c>data/consumables/</c> con el mismo criterio de alcance que <c>data/items/</c>: catálogo pequeño (4,
/// uno por familia de RF-084), formato de efecto igual al de los perks, descripción generada.</para>
///
/// <para><b>Límite declarado.</b> RF-080..085 (equipar hasta 3, condicionales con disparador, familia
/// manual obligatoria) ya los resuelve <c>Sim.Run.RunEngine.Apply</c> a través de <c>SetConsumables</c>
/// (paquete W): cualquier id de consumible se puede equipar sin comprobar que se posea, porque
/// <c>RunState</c> no tiene un inventario de consumibles (el paquete W dejó anotado el mismo límite: "los
/// consumibles equipados... no surten efecto en el partido", <c>fase2-diseno.md</c> §13). Comprar aquí
/// registra la propiedad en <c>RunState.Counters["consumable_owned:&lt;id&gt;"]</c> (RT-030, mecanismo
/// genérico de W-11) para que un paquete futuro pueda exigirla sin subir la versión del esquema; hoy no
/// se aplica ninguna comprobación al equipar, y queda anotado como límite de esta fase.</para>
/// </summary>
public sealed record ConsumableDefinition(
    string Id,
    LocalizedName Name,
    Rarity Rarity,
    ConsumableFamily Family,
    IReadOnlyList<EffectDefinition> Effects);

/// <summary>Catálogo de consumibles, ordenado por id ordinal ascendente.</summary>
public sealed class ConsumableCatalog
{
    private readonly ConsumableDefinition[] _consumables;
    private readonly Dictionary<string, ConsumableDefinition> _byId;

    public ConsumableCatalog(IEnumerable<ConsumableDefinition> consumables)
    {
        ArgumentNullException.ThrowIfNull(consumables);
        _consumables = consumables.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, ConsumableDefinition>(_consumables.Length, StringComparer.Ordinal);
        foreach (var consumable in _consumables)
        {
            _byId.Add(consumable.Id, consumable);
        }
    }

    public IReadOnlyList<ConsumableDefinition> All => _consumables;

    public ConsumableDefinition? Find(string id) => _byId.GetValueOrDefault(id);
}

/// <summary>Carga <c>data/consumables/*.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class ConsumableLoader
{
    public static ConsumableCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var consumables = new List<ConsumableDefinition>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("consumables/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            consumables.Add(Parse(path, files[path]));
        }

        if (consumables.Count == 0)
        {
            throw new DataException("consumables/", "$", "no se ha encontrado ningún consumible en data/consumables/");
        }

        return new ConsumableCatalog(consumables);
    }

    private static ConsumableDefinition Parse(string path, string content)
    {
        using var document = ParseJson(path, content);
        var root = Json.Root(path, document);

        string id = root.Str("id");
        var name = LocalizedNameJson.Read(root.Prop("name"));
        var rarity = root.Str("rarity") switch
        {
            "common" => Rarity.Common,
            "uncommon" => Rarity.Uncommon,
            "rare" => Rarity.Rare,
            "legendary" => Rarity.Legendary,
            var other => throw new DataException(path, "$.rarity", $"rareza desconocida: '{other}'"),
        };
        var family = root.Str("family") switch
        {
            "medical" => ConsumableFamily.Medical,
            "tactical" => ConsumableFamily.Tactical,
            "dirty" => ConsumableFamily.Dirty,
            "supernatural" => ConsumableFamily.Supernatural,
            var other => throw new DataException(path, "$.family", $"familia desconocida: '{other}'"),
        };
        var effects = EffectJson.ReadList(root.Prop("effects"), rarity);
        if (effects.Count == 0)
        {
            throw new DataException(path, "$.effects", "un consumible necesita al menos un efecto");
        }

        return new ConsumableDefinition(id, name, rarity, family, effects);
    }

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
