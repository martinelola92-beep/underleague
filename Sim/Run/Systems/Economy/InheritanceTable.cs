using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Cuánto traspasa un perk de "Herencia" (paquete BB, <c>docs/analisis/perks-catalogo-unificado.md</c>
/// §3.2) al compañero vinculado cuando su portador muere.
/// </summary>
/// <param name="Percent">
/// Porcentaje del valor <b>del muerto</b> en cada uno de los cinco atributos (<see cref="Model.Attributes"/>)
/// que se suma al mismo atributo del vinculado. 0..100.
/// </param>
/// <param name="MaxPerAttribute">
/// Tope absoluto de puntos que un único traspaso puede sumar a <b>cada</b> atributo, aunque el porcentaje
/// diera más: el freno de un jugador legendario muriendo con un porcentaje alto (RT-023, aritmética
/// entera, los dos límites a la vez).
/// </param>
public sealed record InheritanceRate(int Percent, int MaxPerAttribute);

/// <summary>
/// Tabla de traspaso de atributos al morir (paquete BB, consumidor Herencia). Un perk sin entrada aquí no
/// traspasa nada. Vive junto a <see cref="DeathGoldTable"/> y sigue el mismo patrón de "instantánea sin
/// fichero" (<see cref="Empty"/>): sin <c>data/economy/inheritance.json</c>, ninguna muerte traspasa
/// atributos.
///
/// <para>El destinatario no lo decide esta tabla: lo resuelve <c>Sim.Run.MatchResolution</c> con la MISMA
/// geometría estática que usa el motor en partido (<c>Sim.Perks.LinkGeometry</c>, a través de
/// <see cref="Underleague.Sim.Perks.PerkDefinition.Links"/> del propio perk), sin tocar <c>Sim/Perks</c> ni
/// <c>Sim/Engine</c>. Si el vinculado también ha muerto en el mismo partido, no hay traspaso: no se
/// redirige a un tercero (decisión del paquete BB).</para>
/// </summary>
public sealed class InheritanceTable
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "economy/inheritance.json";

    private readonly IReadOnlyDictionary<string, InheritanceRate> _rates;

    private InheritanceTable(IReadOnlyDictionary<string, InheritanceRate> rates)
    {
        _rates = rates;
    }

    /// <summary>Tabla vacía: ningún perk traspasa nada. Es lo que usa una instantánea sin el fichero.</summary>
    public static InheritanceTable Empty { get; } =
        new(new Dictionary<string, InheritanceRate>(StringComparer.Ordinal));

    /// <summary>Número de perks con traspaso declarado.</summary>
    public int Count => _rates.Count;

    /// <summary>Traspaso de ese perk, o null si no traspasa nada.</summary>
    public InheritanceRate? RateFor(string perkId)
    {
        ArgumentNullException.ThrowIfNull(perkId);
        return _rates.TryGetValue(perkId, out var rate) ? rate : null;
    }

    /// <summary>Carga la tabla de la instantánea de ficheros; devuelve <see cref="Empty"/> si no la trae.</summary>
    public static InheritanceTable FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            return Empty;
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
            var rates = new Dictionary<string, InheritanceRate>(StringComparer.Ordinal);
            foreach (var property in root.Prop("transfers").EnumerateObject())
            {
                int percent = property.Value.Prop("percent").AsInt();
                int maxPerAttribute = property.Value.Prop("maxPerAttribute").AsInt();
                if (percent < 0 || percent > 100)
                {
                    throw new DataException(
                        Path,
                        $"$.transfers.{property.Name}.percent",
                        "el porcentaje de traspaso debe estar entre 0 y 100");
                }

                if (maxPerAttribute < 0)
                {
                    throw new DataException(
                        Path,
                        $"$.transfers.{property.Name}.maxPerAttribute",
                        "el tope por atributo no puede ser negativo");
                }

                rates[property.Name] = new InheritanceRate(percent, maxPerAttribute);
            }

            return new InheritanceTable(rates);
        }
    }
}
