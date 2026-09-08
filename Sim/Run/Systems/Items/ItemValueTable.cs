using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Tabla de <b>valor medido</b> de cada objeto, cargada de <c>data/economy/item-values.json</c> (AT-A).
/// Es la gemela de <see cref="PerkValueTable"/> y, sobre todo, está en <b>su misma unidad</b>: milésimas
/// de punto de tasa de victoria de <b>un objeto sobre un portador</b> frente al espejo sin él, medidas
/// con el mismo instrumento (<c>/Balance --item-values</c>).
///
/// <para><b>Por qué hacía falta.</b> El valor de un objeto ya se <b>calculaba</b> —<c>ItemScale.ValueOf</c>,
/// suma de modificador × valor marginal del atributo— pero esa tabla de valor marginal está medida en
/// fase 1b sobre otro experimento: un +20 <b>repartido entre los diez jugadores</b> del equipo. Las dos
/// cifras se expresan en milésimas y aun así no se pueden restar, porque un +20 concentrado en un
/// portador no responde como el mismo +20 repartido entre diez. Por eso el listón del mercado no podía
/// ser "el del slot MÁS el del oro" (AT-A). Con esta tabla las dos columnas quedan en la misma escala.
/// </para>
///
/// <para><b>Lo que esta tabla NO hace.</b> No pesa nada. El peso de un objeto en el pool de recompensas
/// y en el surtido del mercado ya sale de su precio calculado (ADR 0038, <c>ItemPricing</c>) modulado
/// por la profundidad nativa (ADR 0051, <c>ItemCatalog.DepthWeight</c>) y por su <c>frequency</c>, y
/// este paquete no lo toca: por eso aquí no hay ni <c>baseWeight</c> ni la fórmula de frecuencia de la
/// tabla de perks. Tampoco hay curva por horizonte (AV-B): la de los perks existe porque su contador de
/// carrera crece partido a partido, y <see cref="ItemDefinition"/> no tiene ningún contador —un objeto
/// vale lo mismo en el partido 1 que en el 8—. Y no hay cuantil de oferta
/// (<c>PerkValueTable.ValueAtQuantile</c>) porque ese cuantil se pondera con el peso del pool, que esta
/// tabla no posee.</para>
///
/// <para><b>Hoy no la consulta nadie</b>: es el paso 1 de AT-A, puro instrumento. Cargarla es inerte.</para>
/// </summary>
public sealed class ItemValueTable
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "economy/item-values.json";

    private readonly Dictionary<string, int> _values;

    private ItemValueTable(Dictionary<string, int> values, int rowDeviation)
    {
        _values = values;
        RowDeviation = rowDeviation;

        // Media y dispersión en aritmética entera (RT-023), sobre los valores ordenados para que el
        // resultado no dependa del orden de recorrido del diccionario (RT-041).
        var sorted = new List<int>(values.Count);
        foreach (var (_, value) in values)
        {
            sorted.Add(value);
        }

        sorted.Sort();

        long sum = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            sum += sorted[i];
        }

        MeanValue = sorted.Count > 0 ? (int)(sum / sorted.Count) : 0;

        long squares = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            long d = sorted[i] - MeanValue;
            squares += d * d;
        }

        ObservedDeviation = sorted.Count > 0 ? (int)Math.Sqrt((double)squares / sorted.Count) : 0;
    }

    /// <summary>Tabla vacía: ningún objeto tiene valor medido. Es lo que usa una instantánea sin fichero.</summary>
    public static ItemValueTable Empty { get; } = new(new Dictionary<string, int>(StringComparer.Ordinal), 0);

    /// <summary>Número de objetos con valor medido.</summary>
    public int Count => _values.Count;

    /// <summary>
    /// <b>Desviación por fila</b> de la medición, en las mismas milésimas: lo que se mueve el valor de un
    /// objeto entre dos lotes independientes del mismo instrumento. Es el ruido de la medida, no la
    /// dispersión del catálogo, y es lo que impedirá que un umbral en el cero exacto sea un umbral,
    /// exactamente igual que en la tabla de perks (ADR 0072).
    /// </summary>
    public int RowDeviation { get; }

    /// <summary>Media de los valores medidos, en milésimas.</summary>
    public int MeanValue { get; }

    /// <summary>
    /// Dispersión <b>observada</b> entre objetos, en milésimas. Incluye el ruido: la dispersión real del
    /// catálogo es <c>sqrt(observada² − desviaciónDeFila²)</c>.
    /// </summary>
    public int ObservedDeviation { get; }

    /// <summary>Valor medido del objeto, en milésimas de punto de tasa de victoria; null si no está medido.</summary>
    public int? ValueOf(string itemId) => _values.TryGetValue(itemId, out int value) ? value : null;

    /// <summary>Carga la tabla; devuelve <see cref="Empty"/> si la instantánea no la trae.</summary>
    public static ItemValueTable FromJson(IReadOnlyDictionary<string, string> files)
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
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var property in root.Prop("values").EnumerateObject())
            {
                values[property.Name] = property.Value.AsInt();
            }

            if (values.Count == 0)
            {
                // Una tabla presente y vacía es un fichero a medio regenerar, no "no hay tabla": el caso
                // de no tener tabla es no tener fichero. Error explícito, nunca silencioso (RT-032).
                throw new DataException(
                    Path, "$.values", "la tabla de valor de objetos está vacía: mídela con /Balance --item-values o borra el fichero");
            }

            int rowDeviation = root.Int("rowDeviation");
            if (rowDeviation < 0)
            {
                throw new DataException(Path, "$.rowDeviation", "la desviación por fila no puede ser negativa");
            }

            return new ItemValueTable(values, rowDeviation);
        }
    }
}
