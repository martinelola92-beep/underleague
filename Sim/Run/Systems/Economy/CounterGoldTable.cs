using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Tarifa en oro de los contadores de partido (paquete Z, primitiva D: "un contador de partido tiene que
/// poder pagar oro"). Cada entrada declara cuántas monedas paga <b>cada unidad</b> de un contador
/// concreto al resolver el partido (<see cref="GoldCalculator"/>); un contador sin entrada aquí no paga
/// nada, que es el comportamiento de hoy.
///
/// <para>Solo llegan aquí los contadores de perks con <c>accumulatesAcrossMatches: true</c> (RF-070 §6):
/// son los únicos que <c>EffectEngine.CounterDeltas()</c> expone por partido
/// (<see cref="Underleague.Sim.Engine.PlayerCounterDelta"/>, vía <c>MatchResult.CounterDeltas</c> →
/// <see cref="RunMatchSummary.CounterDeltas"/>). Un contador sin ese flag vive y muere dentro del mismo
/// partido y nunca llega a la capa de campaña, así que declarar una tarifa para él no tendría ningún
/// efecto: no es una restricción nueva, es la que ya existía para que un contador sobreviva al pitido
/// final.</para>
///
/// <para>Es dato (regla 5 de CLAUDE.md), no código: la tabla vive en
/// <c>data/economy/counter-gold.json</c> (mismo formato de mapa id→entero que
/// <c>data/economy/perk-values.json</c>) y se carga con el mismo patrón de "instantánea sin fichero"
/// que <see cref="PerkValueTable"/> y <see cref="Items.ItemValueTable"/>: si el fichero no
/// está, <see cref="Empty"/> hace que ningún contador pague nada, sin lanzar.</para>
/// </summary>
public sealed class CounterGoldTable
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "economy/counter-gold.json";

    private readonly IReadOnlyDictionary<string, int> _rates;

    private CounterGoldTable(IReadOnlyDictionary<string, int> rates)
    {
        _rates = rates;
    }

    /// <summary>Tabla vacía: ningún contador paga oro. Es lo que usa una instantánea sin el fichero.</summary>
    public static CounterGoldTable Empty { get; } = new(new Dictionary<string, int>(StringComparer.Ordinal));

    /// <summary>Número de contadores con tarifa declarada.</summary>
    public int Count => _rates.Count;

    /// <summary>Oro que paga cada unidad de ese contador; 0 si no tiene tarifa.</summary>
    public int RateFor(string counter)
    {
        ArgumentNullException.ThrowIfNull(counter);
        return _rates.TryGetValue(counter, out int rate) ? rate : 0;
    }

    /// <summary>Carga la tarifa de la instantánea de ficheros; devuelve <see cref="Empty"/> si no la trae.</summary>
    public static CounterGoldTable FromJson(IReadOnlyDictionary<string, string> files)
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
                        "la tarifa no puede ser negativa: un contador paga, nunca cobra");
                }

                rates[property.Name] = rate;
            }

            return new CounterGoldTable(rates);
        }
    }
}
