using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Medical;

/// <summary>Clase de una prótesis del herrero (ADR 0164): ventaja o desventaja.</summary>
public enum ProsthesisKind
{
    /// <summary>Ventaja: el efecto suma a un atributo (delta &gt; 0).</summary>
    Improve,

    /// <summary>Desventaja: el efecto resta a un atributo (delta &lt; 0). Nunca mata: es la identidad lo que se arriesga.</summary>
    Worsen,
}

/// <summary>
/// Ficha de una prótesis (<c>data/prostheses/prostheses.json</c>, ADR 0164, RF-095). El efecto es un delta a un
/// atributo del jugador, permanente al instalarse (<see cref="ApplyTo"/>).
/// </summary>
/// <param name="Id">Id en <c>snake_case</c>; es lo que guarda <see cref="RunProsthesis.Effect"/>.</param>
/// <param name="Slot">Ranura del cuerpo; una ranura ocupada no admite otra prótesis.</param>
public sealed record ProsthesisDefinition(
    string Id,
    string Slot,
    ProsthesisKind Kind,
    LocalizedName Name,
    AttributeKind Attribute,
    int Delta)
{
    /// <summary>Atributos del jugador con el efecto aplicado, acotados a 1..99 (<see cref="Attributes.Clamp"/>).</summary>
    public Attributes ApplyTo(Attributes attributes) =>
        Attributes.Clamp(attributes.With(Attribute, attributes.Get(Attribute) + Delta));
}

/// <summary>
/// Catálogo de prótesis de <c>data/prostheses/prostheses.json</c> (RT-041: ordenado por id, nunca un
/// <c>Dictionary</c> iterado), fuera del <see cref="Catalog"/> compartido porque solo lo consume la clínica
/// (<c>MedicalSystem.Forge</c>).
/// </summary>
public sealed class ProsthesisCatalog
{
    /// <summary>Catálogo vacío: el herrero no puede instalar nada. Para tests y llamadores sin <c>data/prostheses/</c>.</summary>
    public static ProsthesisCatalog Empty { get; } = new(Array.Empty<ProsthesisDefinition>());

    private readonly IReadOnlyList<ProsthesisDefinition> _all;

    public ProsthesisCatalog(IReadOnlyList<ProsthesisDefinition> prostheses)
    {
        ArgumentNullException.ThrowIfNull(prostheses);
        _all = prostheses.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>Todas las prótesis, por id ascendente.</summary>
    public IReadOnlyList<ProsthesisDefinition> All => _all;

    /// <summary>Ficha con ese id, o null.</summary>
    public ProsthesisDefinition? Find(string id)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            if (string.Equals(_all[i].Id, id, StringComparison.Ordinal))
            {
                return _all[i];
            }
        }

        return null;
    }

    /// <summary>Ranuras distintas del catálogo, ordenadas.</summary>
    public IReadOnlyList<string> Slots => _all.Select(p => p.Slot).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Prótesis de la clase indicada cuya ranura no está en <paramref name="occupiedSlots"/>, por id ascendente
    /// (orden determinista para el sorteo, RT-041).
    /// </summary>
    public IReadOnlyList<ProsthesisDefinition> Candidates(ProsthesisKind kind, IReadOnlyCollection<string> occupiedSlots)
    {
        ArgumentNullException.ThrowIfNull(occupiedSlots);
        var result = new List<ProsthesisDefinition>();
        for (int i = 0; i < _all.Count; i++)
        {
            if (_all[i].Kind == kind && !occupiedSlots.Contains(_all[i].Slot))
            {
                result.Add(_all[i]);
            }
        }

        return result;
    }
}

/// <summary>Carga <c>data/prostheses/prostheses.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class ProsthesisLoader
{
    private const string Path = "prostheses/prostheses.json";

    /// <summary>Catálogo de prótesis de la instantánea de ficheros indicada.</summary>
    public static ProsthesisCatalog FromJson(IReadOnlyDictionary<string, string> files)
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
            var list = new List<ProsthesisDefinition>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Prop("prostheses").EnumerateArray())
            {
                var definition = Parse(node);
                if (!seenIds.Add(definition.Id))
                {
                    throw new DataException(Path, node.Path + ".id", $"id de prótesis repetido: '{definition.Id}'");
                }

                list.Add(definition);
            }

            // Cada ranura ofrece las dos clases: así un jugador con una ranura libre puede recibir la mejora y
            // el empeoramiento, y la tabla que enseña el herrero nunca promete un resultado sin prótesis.
            foreach (string slot in list.Select(p => p.Slot).Distinct(StringComparer.Ordinal))
            {
                foreach (var kind in Enum.GetValues<ProsthesisKind>())
                {
                    if (!list.Any(p => p.Slot == slot && p.Kind == kind))
                    {
                        throw new DataException(Path, "$.prostheses", $"la ranura '{slot}' no tiene ninguna prótesis de clase {kind}");
                    }
                }
            }

            return new ProsthesisCatalog(list);
        }
    }

    private static ProsthesisDefinition Parse(Json node)
    {
        var kindNode = node.Prop("kind");
        ProsthesisKind kind = kindNode.AsString() switch
        {
            "improve" => ProsthesisKind.Improve,
            "worsen" => ProsthesisKind.Worsen,
            var other => throw new DataException(kindNode.File, kindNode.Path, $"clase de prótesis desconocida: '{other}'"),
        };

        var effect = node.Prop("effect");
        var attributeNode = effect.Prop("attribute");
        if (!Enum.TryParse<AttributeKind>(attributeNode.AsString(), ignoreCase: false, out var attribute) || !Enum.IsDefined(attribute))
        {
            throw new DataException(attributeNode.File, attributeNode.Path, $"atributo desconocido: '{attributeNode.AsString()}'");
        }

        var deltaNode = effect.Prop("delta");
        int delta = deltaNode.AsInt();
        if ((kind == ProsthesisKind.Improve && delta <= 0) || (kind == ProsthesisKind.Worsen && delta >= 0))
        {
            throw new DataException(
                deltaNode.File,
                deltaNode.Path,
                $"una prótesis de clase {kind} debe {(kind == ProsthesisKind.Improve ? "sumar (delta > 0)" : "restar (delta < 0)")}");
        }

        return new ProsthesisDefinition(
            node.Str("id"),
            node.Str("slot"),
            kind,
            LocalizedNameJson.Read(node.Prop("name")),
            attribute,
            delta);
    }
}
