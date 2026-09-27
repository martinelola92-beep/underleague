using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Referees;

/// <summary>
/// Ficha de datos de un árbitro (ADR 0158, RF-061, RF-061b): nombre y muletilla localizados, rasgo y, si
/// es tuerto, el lado que no ve. <see cref="BlindSide"/> es <see cref="RefereeSide.None"/> salvo con
/// <see cref="RefereeTrait.OneEyed"/>, donde es obligatorio (lo exige el esquema y este cargador).
/// </summary>
public sealed record RefereeDefinition(
    string Id,
    LocalizedName Name,
    RefereeTrait Trait,
    LocalizedName Catchphrase,
    RefereeSide BlindSide);

/// <summary>
/// Catálogo de árbitros de <c>data/referees/referees.json</c> (RT-041: ordenado por id, no un Dictionary
/// iterado), fuera del <see cref="Catalog"/> compartido porque solo lo consume el bucle de run
/// (<c>StandardRunSystems</c>), como <c>EventCatalog</c> o <c>RivalCatalog</c>.
/// </summary>
public sealed class RefereeCatalog
{
    private readonly IReadOnlyList<RefereeDefinition> _referees;

    public RefereeCatalog(IReadOnlyList<RefereeDefinition> referees)
    {
        ArgumentNullException.ThrowIfNull(referees);
        _referees = referees.OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Todos los árbitros del plantel de datos, por id ascendente.</summary>
    public IReadOnlyList<RefereeDefinition> All => _referees;

    /// <summary>Ficha con ese id, o null.</summary>
    public RefereeDefinition? Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < _referees.Count; i++)
        {
            if (string.Equals(_referees[i].Id, id, StringComparison.Ordinal))
            {
                return _referees[i];
            }
        }

        return null;
    }
}

/// <summary>Carga <c>data/referees/referees.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class RefereeLoader
{
    private const string Path = "referees/referees.json";

    /// <summary>Catálogo de árbitros de la instantánea de ficheros indicada.</summary>
    public static RefereeCatalog FromJson(IReadOnlyDictionary<string, string> files)
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
            var referees = new List<RefereeDefinition>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Prop("referees").EnumerateArray())
            {
                var referee = Parse(node);
                if (!seenIds.Add(referee.Id))
                {
                    throw new DataException(Path, node.Path + ".id", $"id de árbitro repetido: '{referee.Id}'");
                }

                referees.Add(referee);
            }

            if (referees.Count == 0)
            {
                throw new DataException(Path, "$.referees", "no hay ningún árbitro en data/referees/referees.json");
            }

            return new RefereeCatalog(referees);
        }
    }

    private static RefereeDefinition Parse(Json node)
    {
        var trait = ParseTrait(node);
        return new RefereeDefinition(
            node.Str("id"),
            LocalizedNameJson.Read(node.Prop("name")),
            trait,
            LocalizedNameJson.Read(node.Prop("catchphrase")),
            ParseBlindSide(node, trait));
    }

    private static RefereeTrait ParseTrait(Json node)
    {
        var element = node.Prop("trait");
        string text = element.AsString();
        return text switch
        {
            "Neutral" => RefereeTrait.Neutral,
            "Strict" => RefereeTrait.Strict,
            "Lenient" => RefereeTrait.Lenient,
            "Homer" => RefereeTrait.Homer,
            "OneEyed" => RefereeTrait.OneEyed,
            "Cowardly" => RefereeTrait.Cowardly,
            "Corrupt" => RefereeTrait.Corrupt,
            "Incorruptible" => RefereeTrait.Incorruptible,
            var other => throw new DataException(element.File, element.Path, $"rasgo de árbitro desconocido: '{other}'"),
        };
    }

    /// <summary>
    /// ADR 0158: <c>blindSide</c> es obligatorio SOLO en <see cref="RefereeTrait.OneEyed"/> y es un error
    /// de datos en cualquier otro rasgo (el esquema ya lo comprueba; el cargador lo repite porque
    /// DataValidator no llama a este cargador semánticamente, solo valida el esquema).
    /// </summary>
    private static RefereeSide ParseBlindSide(Json node, RefereeTrait trait)
    {
        var value = node.TryProp("blindSide");
        if (trait != RefereeTrait.OneEyed)
        {
            if (value is { } present)
            {
                throw new DataException(present.File, present.Path, "solo un árbitro tuerto (OneEyed) declara blindSide (ADR 0158)");
            }

            return RefereeSide.None;
        }

        if (value is not { } blindSide)
        {
            throw new DataException(node.File, node.Path + ".blindSide", "un árbitro tuerto necesita blindSide: 'top' o 'bottom' (ADR 0158)");
        }

        return blindSide.AsString() switch
        {
            "top" => RefereeSide.Top,
            "bottom" => RefereeSide.Bottom,
            var other => throw new DataException(blindSide.File, blindSide.Path, $"lado ciego desconocido: '{other}'"),
        };
    }
}
