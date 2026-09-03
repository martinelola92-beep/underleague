using System.Text.Json;
using Underleague.Sim.Model;

namespace Underleague.Balance;

/// <summary>Un equipo del conjunto de referencia: id, raza y calidad media de atributos.</summary>
public sealed record ReferenceTeam(string Id, Race Race, int Quality);

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
            teams.Add(new ReferenceTeam(id, race, quality));
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

    private static int RequireInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || !value.TryGetInt32(out int result))
        {
            throw new FormatException($"reference.json: falta o es inválida la propiedad entera '{property}'");
        }

        return result;
    }
}
