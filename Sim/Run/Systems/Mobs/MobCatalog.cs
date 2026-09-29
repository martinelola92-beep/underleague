using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Mobs;

/// <summary>Un tipo de turba de <c>data/mobs/mobs.json</c> (ADR 0167).</summary>
public sealed record MobType(string Id, LocalizedName Name, int Weight, IReadOnlyList<MobEffectKind> Effects)
{
    /// <summary>Lo que recibe el motor: el tipo ya resuelto, sin nombre ni peso.</summary>
    public MobSetup ToSetup() => new(Id, Effects);
}

/// <summary>
/// Tipos de turba (ADR 0167). Fuera del <see cref="Catalog"/> compartido porque sólo lo usa la run: el motor recibe
/// el tipo ya resuelto en <see cref="MatchSetup.Mob"/>. Sin tipos (<see cref="Empty"/>) no hay sorteo y la turba es
/// la de siempre.
/// </summary>
public sealed class MobCatalog
{
    /// <summary>
    /// Desplazamiento del sorteo del tipo en <see cref="OfferStream"/> (tabla en <c>GeneratedPlayers.cs</c>): propio
    /// de la run y del nodo, nunca el flujo de partido (RT-022). Mismo nodo y misma semilla, mismo tipo.
    /// </summary>
    public const int StreamOffset = 9600;

    public MobCatalog(IReadOnlyList<MobType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        Types = types;
    }

    public static MobCatalog Empty { get; } = new(Array.Empty<MobType>());

    /// <summary>Los tipos, en el orden del fichero (el sorteo recorre este orden).</summary>
    public IReadOnlyList<MobType> Types { get; }

    public MobType? Find(string id)
    {
        for (int i = 0; i < Types.Count; i++)
        {
            if (string.Equals(Types[i].Id, id, StringComparison.Ordinal))
            {
                return Types[i];
            }
        }

        return null;
    }

    /// <summary>
    /// El tipo de turba del partido de ese nodo, sorteado por peso con el flujo propio de la run. Null si no hay
    /// catálogo o el nodo no es un partido.
    /// </summary>
    public MobType? For(ulong seed, MapNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Types.Count == 0 || !node.IsMatch)
        {
            return null;
        }

        int total = 0;
        for (int i = 0; i < Types.Count; i++)
        {
            total += Types[i].Weight;
        }

        int roll = OfferStream.For(seed, node.Id, StreamOffset).Range(0, total);
        for (int i = 0; i < Types.Count; i++)
        {
            if (roll < Types[i].Weight)
            {
                return Types[i];
            }

            roll -= Types[i].Weight;
        }

        return Types[^1];
    }
}

/// <summary>Carga <c>data/mobs/mobs.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class MobLoader
{
    private const string Path = "mobs/mobs.json";

    public static MobCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            throw new DataException(Path, "$", "fichero requerido ausente");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(Path, "$", $"JSON inválido: {ex.Message}");
        }

        using (document)
        {
            var root = Json.Root(Path, document);
            root.EnsureKnownKeys("types");
            var types = new List<MobType>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Prop("types").EnumerateArray())
            {
                node.EnsureKnownKeys("id", "name", "weight", "effects");
                string id = node.Str("id");
                if (!seen.Add(id))
                {
                    throw new DataException(Path, node.Path + ".id", $"tipo de turba repetido: '{id}'");
                }

                int weight = node.Int("weight");
                if (weight < 1)
                {
                    throw new DataException(Path, node.Path + ".weight", "el peso de un tipo de turba es al menos 1");
                }

                var effects = new List<MobEffectKind>();
                foreach (var effect in node.Prop("effects").EnumerateArray())
                {
                    string name = effect.AsString();
                    var kind = name switch
                    {
                        "injure" => MobEffectKind.Injure,
                        "pressBoth" => MobEffectKind.PressBoth,
                        "theirOffensive" => MobEffectKind.TheirOffensive,
                        _ => throw new DataException(Path, effect.Path, $"efecto de turba desconocido: '{name}'"),
                    };
                    if (effects.Contains(kind))
                    {
                        throw new DataException(Path, effect.Path, $"efecto de turba repetido: '{name}'");
                    }

                    effects.Add(kind);
                }

                types.Add(new MobType(id, LocalizedNameJson.Read(node.Prop("name")), weight, effects));
            }

            if (types.Count == 0)
            {
                throw new DataException(Path, "$.types", "no hay ningún tipo de turba");
            }

            return new MobCatalog(types);
        }
    }
}
