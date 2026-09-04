using System.Text.Json;
using Underleague.Sim.Model;

namespace Underleague.Balance;

/// <summary>
/// Un equipo del conjunto de referencia: id, raza, calidad media de atributos, nivel de la plantilla y
/// rareza uniforme opcional. <c>Quality</c> y <c>Level</c> son diales independientes (paquete U):
/// la calidad desplaza los atributos punto por punto y el nivel es la progresión dentro de la run.
/// <c>UniformRarity</c> null deja la composición por defecto de RF-005 (un raro entre diez).
/// </summary>
public sealed record ReferenceTeam(string Id, Race Race, int Quality, int Level = 1, Rarity? UniformRarity = null, Attributes? AttributeBonus = null);

/// <summary>Un emparejamiento local-visitante del conjunto de referencia.</summary>
public sealed record ReferencePairing(string HomeId, string AwayId);

/// <summary>Conjunto de referencia de /Balance (data/balance/reference.json, RT-052).</summary>
public sealed record ReferenceConfig(IReadOnlyList<ReferenceTeam> Teams, IReadOnlyList<ReferencePairing> Pairings)
{
    /// <summary>Lee y valida el fichero de referencia. Sin esquema formal aquí: tools/DataValidator lo cubre por separado.</summary>
    public static ReferenceConfig Load(string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        if (!root.TryGetProperty("teams", out var teamsElement) || teamsElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("reference.json: falta el array 'teams'");
        }

        var teams = new List<ReferenceTeam>();
        foreach (var teamElement in teamsElement.EnumerateArray())
        {
            string id = RequireString(teamElement, "id");
            string raceText = RequireString(teamElement, "race");
            if (!Enum.TryParse<Race>(raceText, out var race))
            {
                throw new FormatException($"reference.json: raza desconocida '{raceText}' en equipo '{id}'");
            }

            int quality = RequireInt(teamElement, "quality");
            int level = OptionalInt(teamElement, "level", 1);
            if (level is < 1 or > 8)
            {
                throw new FormatException($"reference.json: 'level' fuera de 1..8 en equipo '{id}'");
            }

            Rarity? uniformRarity = null;
            if (teamElement.TryGetProperty("rarity", out var rarityElement) && rarityElement.ValueKind == JsonValueKind.String)
            {
                if (!Enum.TryParse<Rarity>(rarityElement.GetString(), ignoreCase: true, out var parsed))
                {
                    throw new FormatException($"reference.json: rareza desconocida '{rarityElement.GetString()}' en equipo '{id}'");
                }

                uniformRarity = parsed;
            }

            Attributes? attributeBonus = null;
            if (teamElement.TryGetProperty("attributeBonus", out var bonusElement) && bonusElement.ValueKind == JsonValueKind.Object)
            {
                attributeBonus = new Attributes(
                    OptionalIntIn(bonusElement, "strength"),
                    OptionalIntIn(bonusElement, "speed"),
                    OptionalIntIn(bonusElement, "technique"),
                    OptionalIntIn(bonusElement, "stamina"),
                    OptionalIntIn(bonusElement, "leash"));
            }

            teams.Add(new ReferenceTeam(id, race, quality, level, uniformRarity, attributeBonus));
        }

        if (!root.TryGetProperty("pairings", out var pairingsElement) || pairingsElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("reference.json: falta el array 'pairings'");
        }

        var pairings = new List<ReferencePairing>();
        foreach (var pairingElement in pairingsElement.EnumerateArray())
        {
            if (pairingElement.ValueKind != JsonValueKind.Array || pairingElement.GetArrayLength() != 2)
            {
                throw new FormatException("reference.json: cada emparejamiento debe ser un array [homeId, awayId]");
            }

            string homeId = pairingElement[0].GetString() ?? throw new FormatException("reference.json: homeId nulo en emparejamiento");
            string awayId = pairingElement[1].GetString() ?? throw new FormatException("reference.json: awayId nulo en emparejamiento");
            pairings.Add(new ReferencePairing(homeId, awayId));
        }

        var knownIds = new HashSet<string>(teams.Select(t => t.Id), StringComparer.Ordinal);
        foreach (var pairing in pairings)
        {
            if (!knownIds.Contains(pairing.HomeId))
            {
                throw new FormatException($"reference.json: emparejamiento referencia un equipo desconocido '{pairing.HomeId}'");
            }

            if (!knownIds.Contains(pairing.AwayId))
            {
                throw new FormatException($"reference.json: emparejamiento referencia un equipo desconocido '{pairing.AwayId}'");
            }
        }

        if (pairings.Count == 0)
        {
            throw new FormatException("reference.json: 'pairings' no puede estar vacío");
        }

        return new ReferenceConfig(teams, pairings);
    }

    private static string RequireString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"reference.json: falta o es inválida la propiedad de cadena '{property}'");
        }

        return value.GetString()!;
    }

    private static int OptionalIntIn(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out int result) ? result : 0;

    private static int OptionalInt(JsonElement element, string property, int fallback)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return fallback;
        }

        if (!value.TryGetInt32(out int result))
        {
            throw new FormatException($"reference.json: la propiedad '{property}' debe ser entera");
        }

        return result;
    }

    private static int RequireInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || !value.TryGetInt32(out int result))
        {
            throw new FormatException($"reference.json: falta o es inválida la propiedad entera '{property}'");
        }

        return result;
    }
}
