using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Tarifa en oro que paga la run cuando <b>muere</b> un jugador que lleva un perk de esta tabla (paquete
/// BB, primitiva "run-level: oro y atributos al salir de la plantilla",
/// <c>docs/analisis/perks-catalogo-unificado.md</c> §3.2, consumidor Seguro de vida). Cada entrada declara
/// cuánto paga <b>una</b> muerte con ese perk (<see cref="GoldCalculator.DeathGold"/>); un perk sin entrada
/// aquí no paga nada.
///
/// <para>Es el mismo patrón que <see cref="CounterGoldTable"/> (ADR 0113), pero la clave es el <b>id del
/// perk</b> y no el nombre de un contador: la muerte es un suceso único de la plantilla, no algo que se
/// acumule partido a partido, así que no hace falta que el perk declare ningún <c>addCounter</c> ni
/// <c>accumulatesAcrossMatches</c> para cobrar. Sim/Run lee directamente
/// <see cref="Model.RunPlayer.Perks"/> del jugador que ha muerto en <c>MatchResolution</c> — no toca
/// <c>Sim/Perks</c> ni <c>Sim/Engine</c> (fuera de sus fronteras, fase2-diseno.md §12).</para>
///
/// <para>Es dato (regla 5 de CLAUDE.md), no código: la tabla vive en
/// <c>data/economy/death-gold.json</c> y se carga con el mismo patrón de "instantánea sin fichero" que
/// <see cref="CounterGoldTable"/>, <see cref="PerkValueTable"/> y <see cref="Items.ItemValueTable"/>: si
/// el fichero no está, <see cref="Empty"/> hace que ninguna muerte pague nada, sin lanzar.</para>
/// </summary>
public sealed class DeathGoldTable
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "economy/death-gold.json";

    private readonly IReadOnlyDictionary<string, int> _rates;

    private DeathGoldTable(IReadOnlyDictionary<string, int> rates)
    {
        _rates = rates;
    }

    /// <summary>Tabla vacía: ninguna muerte paga oro. Es lo que usa una instantánea sin el fichero.</summary>
    public static DeathGoldTable Empty { get; } = new(new Dictionary<string, int>(StringComparer.Ordinal));

    /// <summary>Número de perks con tarifa de muerte declarada.</summary>
    public int Count => _rates.Count;

    /// <summary>Oro que paga la muerte de un portador de ese perk; 0 si no tiene tarifa.</summary>
    public int RateFor(string perkId)
    {
        ArgumentNullException.ThrowIfNull(perkId);
        return _rates.TryGetValue(perkId, out int rate) ? rate : 0;
    }

    /// <summary>Carga la tarifa de la instantánea de ficheros; devuelve <see cref="Empty"/> si no la trae.</summary>
    public static DeathGoldTable FromJson(IReadOnlyDictionary<string, string> files)
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
            var rates = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in root.Prop("rates").EnumerateObject())
            {
                int rate = property.Value.AsInt();
                if (rate < 0)
                {
                    throw new DataException(
                        Path,
                        $"$.rates.{property.Name}",
                        "la tarifa no puede ser negativa: una muerte paga, nunca cobra");
                }

                rates[property.Name] = rate;
            }

            return new DeathGoldTable(rates);
        }
    }
}
