using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Porcentaje MÍNIMO de reventa que garantiza un perk (paquete BB, consumidor Préstamo: "se revende sin
/// perder la mitad", <c>docs/analisis/perks-catalogo-unificado.md</c> §3.2), por encima del que le
/// tocaría por su estado físico (<see cref="MarketConfig.PlayerSaleStatePercent"/>).
///
/// <para><c>Sim.Run.Systems.Market.MarketSystem.SalePrice</c> ya suma
/// <see cref="MarketConfig.PlayerSalePerPerk"/> por cada perk que lleve el jugador (RF-114f): esta tabla
/// NO es un sistema nuevo, es el modificador que le faltaba a esa misma función para un perk concreto,
/// tal y como pide el encargo. Toma el MAYOR entre el porcentaje de estado físico y el que declare aquí
/// cualquiera de los perks del jugador: el perk solo puede ayudar, nunca bajar el precio por debajo de lo
/// que ya le tocaba. Un perk sin entrada aquí no cambia nada (<see cref="Empty"/>, mismo patrón de
/// "instantánea sin fichero" que <see cref="DeathGoldTable"/> e <see cref="InheritanceTable"/>).</para>
/// </summary>
public sealed class PerkResaleTable
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "economy/perk-resale.json";

    private readonly IReadOnlyDictionary<string, int> _overrides;

    private PerkResaleTable(IReadOnlyDictionary<string, int> overrides)
    {
        _overrides = overrides;
    }

    /// <summary>Tabla vacía: ningún perk cambia el porcentaje de reventa.</summary>
    public static PerkResaleTable Empty { get; } = new(new Dictionary<string, int>(StringComparer.Ordinal));

    /// <summary>Número de perks con porcentaje de reventa declarado.</summary>
    public int Count => _overrides.Count;

    /// <summary>Porcentaje mínimo de reventa que garantiza ese perk; 0 si no declara ninguno.</summary>
    public int PercentFor(string perkId)
    {
        ArgumentNullException.ThrowIfNull(perkId);
        return _overrides.TryGetValue(perkId, out int percent) ? percent : 0;
    }

    /// <summary>Carga la tabla de la instantánea de ficheros; devuelve <see cref="Empty"/> si no la trae.</summary>
    public static PerkResaleTable FromJson(IReadOnlyDictionary<string, string> files)
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
            var overrides = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in root.Prop("overrides").EnumerateObject())
            {
                int percent = property.Value.AsInt();
                if (percent < 0)
                {
                    throw new DataException(
                        Path,
                        $"$.overrides.{property.Name}",
                        "el porcentaje de reventa no puede ser negativo");
                }

                overrides[property.Name] = percent;
            }

            return new PerkResaleTable(overrides);
        }
    }
}
