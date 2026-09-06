using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Tabla de <b>valor medido</b> de cada perk y peso que ese valor le da en el pool (ADR 0038), cargada
/// de <c>data/economy/perk-values.json</c>.
///
/// <para>Los objetos se <b>calculan</b> (son un paquete de atributos y hay tabla de valor marginal); los
/// perks hay que <b>medirlos</b>, y esa medición es parte del lote de balance
/// (<c>/Balance --perk-values</c>). El valor está en <b>milésimas de punto de tasa de victoria</b>: lo
/// que gana un equipo por llevar ese perk frente a su espejo sin él.</para>
///
/// <para><b>La palanca es la frecuencia, no el número.</b> Donde hay precio se encarece (mercado); donde
/// se obtiene gratis —una de tres tras ganar, RF-071— el peso en el pool es inversamente proporcional al
/// valor. Un perk excelente sigue siendo excelente; simplemente sale menos.</para>
///
/// <code>
/// peso(perk) = clamp(pesoBase × valorReferencia / max(valor + desplazamiento, suelo), pesoMín, pesoMáx)
/// </code>
///
/// <para>El <b>desplazamiento</b> existe porque el valor medido es una diferencia sobre un espejo y sale
/// negativo en la mitad del catálogo: sin él, "inversamente proporcional" no está definido. Lo que la
/// tabla ordena es el <b>orden</b> de los perks, no su magnitud exacta: la medida tiene una desviación
/// de unos 3 puntos por fila y por eso el peso está acotado por arriba y por abajo.</para>
///
/// <para>Un perk sin entrada en la tabla pesa <c>pesoBase</c>: no se le castiga por no estar medido.</para>
/// </summary>
public sealed class PerkValueTable
{
    /// <summary>Ruta del fichero dentro de la instantánea de <c>/data</c>.</summary>
    public const string Path = "economy/perk-values.json";

    private readonly Dictionary<string, int> _values;

    /// <summary>Valores de la tabla en orden ascendente; el índice i corresponde a <see cref="_cumulativeWeight"/>[i].</summary>
    private readonly int[] _sortedValues;

    /// <summary>Peso acumulado hasta ese valor incluido, en el mismo orden que <see cref="_sortedValues"/>.</summary>
    private readonly long[] _cumulativeWeight;

    private PerkValueTable(
        Dictionary<string, int> values,
        int baseWeight,
        int referenceValue,
        int valueShift,
        int valueFloor,
        int minWeight,
        int maxWeight,
        int rowDeviation)
    {
        _values = values;
        BaseWeight = baseWeight;
        ReferenceValue = referenceValue;
        ValueShift = valueShift;
        ValueFloor = valueFloor;
        MinWeight = minWeight;
        MaxWeight = maxWeight;
        RowDeviation = rowDeviation;

        // La distribución de lo que el pool OFRECE, precalculada una vez: pares (valor, peso) ordenados
        // por valor ascendente y con el peso acumulado. Es lo que convierte "me quedan S slots y voy a
        // ver N ofertas" en un número (ADR 0072); sin ella habría que recorrer la tabla en cada decisión.
        var sorted = new List<KeyValuePair<string, int>>(values);
        sorted.Sort((a, b) =>
        {
            int byValue = a.Value.CompareTo(b.Value);
            return byValue != 0 ? byValue : string.CompareOrdinal(a.Key, b.Key);
        });

        _sortedValues = new int[sorted.Count];
        _cumulativeWeight = new long[sorted.Count];
        long cumulative = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            _sortedValues[i] = sorted[i].Value;
            cumulative += WeightFor(sorted[i].Value);
            _cumulativeWeight[i] = cumulative;
        }

        TotalOfferWeight = cumulative;

        long sum = 0;
        for (int i = 0; i < _sortedValues.Length; i++)
        {
            sum += _sortedValues[i];
        }

        MeanValue = _sortedValues.Length > 0 ? (int)(sum / _sortedValues.Length) : 0;

        long squares = 0;
        for (int i = 0; i < _sortedValues.Length; i++)
        {
            long d = _sortedValues[i] - MeanValue;
            squares += d * d;
        }

        ObservedDeviation = _sortedValues.Length > 0
            ? (int)Math.Sqrt((double)squares / _sortedValues.Length)
            : 0;
    }

    /// <summary>Tabla vacía: todos los perks pesan lo mismo. Es lo que usa una instantánea sin fichero de valores.</summary>
    public static PerkValueTable Uniform { get; } =
        new(new Dictionary<string, int>(StringComparer.Ordinal), 100, 500, 500, 100, 100, 100, 0);

    /// <summary>Peso base, el de un perk cuyo valor es exactamente el de referencia.</summary>
    public int BaseWeight { get; }

    /// <summary>Valor de referencia, en milésimas de punto de tasa de victoria.</summary>
    public int ReferenceValue { get; }

    /// <summary>Desplazamiento que lleva el valor medido a terreno positivo antes de invertirlo.</summary>
    public int ValueShift { get; }

    /// <summary>Suelo del divisor: por debajo, el peso dejaría de estar acotado.</summary>
    public int ValueFloor { get; }

    /// <summary>Peso mínimo: ningún perk desaparece del pool por caro que sea.</summary>
    public int MinWeight { get; }

    /// <summary>Peso máximo: ningún perk inunda el pool por flojo que sea.</summary>
    public int MaxWeight { get; }

    /// <summary>Número de perks con valor medido.</summary>
    public int Count => _values.Count;

    /// <summary>
    /// <b>Desviación por fila</b> de la medición, en las mismas milésimas (ADR 0070 §2): lo que se mueve
    /// el valor de un perk entre dos lotes independientes del mismo instrumento. Es el ruido de la
    /// medida, no la dispersión del catálogo, y es lo que hace que un umbral en el cero exacto no sea un
    /// umbral (ADR 0072). Cero en una tabla que no lo declara.
    /// </summary>
    public int RowDeviation { get; }

    /// <summary>Media de los valores medidos, en milésimas: el centro al que encoge la corrección de ruido.</summary>
    public int MeanValue { get; }

    /// <summary>
    /// Dispersión <b>observada</b> entre perks, en milésimas. Incluye el ruido: la dispersión real del
    /// catálogo es <c>sqrt(observada² − desviaciónDeFila²)</c>.
    /// </summary>
    public int ObservedDeviation { get; }

    /// <summary>Peso total de la distribución de oferta; denominador de <see cref="ValueAtQuantile"/>.</summary>
    public long TotalOfferWeight { get; }

    /// <summary>
    /// Valor por debajo del cual queda la fracción <paramref name="numerator"/>/<paramref name="denominator"/>
    /// de lo que el pool <b>ofrece</b> (ADR 0072). Aritmética entera y determinista (RT-023): el peso
    /// acumulado está precalculado y se recorre hasta pasar el objetivo.
    ///
    /// <para>Es la mitad medible del coste de oportunidad de un slot: con <c>S</c> slots libres y
    /// <c>N</c> ofertas por delante, el slot marginal se llena con la mejor <c>S</c>-ésima de esas
    /// <c>N</c>, o sea con el cuantil <c>1 − S/N</c> de esta distribución.</para>
    /// </summary>
    public int ValueAtQuantile(long numerator, long denominator)
    {
        if (_sortedValues.Length == 0 || denominator <= 0)
        {
            return 0;
        }

        if (numerator <= 0)
        {
            return _sortedValues[0];
        }

        if (numerator >= denominator)
        {
            return _sortedValues[^1];
        }

        long target = TotalOfferWeight * numerator / denominator;
        for (int i = 0; i < _cumulativeWeight.Length; i++)
        {
            if (_cumulativeWeight[i] >= target)
            {
                return _sortedValues[i];
            }
        }

        return _sortedValues[^1];
    }

    /// <summary>Valor medido del perk, en milésimas de punto de tasa de victoria; null si no está medido.</summary>
    public int? ValueOf(string perkId) => _values.TryGetValue(perkId, out int value) ? value : null;

    /// <summary>Peso del perk en el pool de recompensas y en el surtido del mercado (ADR 0038).</summary>
    public int WeightOf(string perkId) =>
        _values.TryGetValue(perkId, out int value) ? WeightFor(value) : BaseWeight;

    private int WeightFor(int value)
    {
        int divisor = Math.Max(value + ValueShift, ValueFloor);
        int weight = (int)((long)BaseWeight * ReferenceValue / divisor);
        return Math.Clamp(weight, MinWeight, MaxWeight);
    }

    /// <summary>Carga la tabla; devuelve <see cref="Uniform"/> si la instantánea no la trae.</summary>
    public static PerkValueTable FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            return Uniform;
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
            var node = root.Prop("values");
            foreach (var property in node.EnumerateObject())
            {
                values[property.Name] = property.Value.AsInt();
            }

            return new PerkValueTable(
                values,
                root.Int("baseWeight"),
                root.Int("referenceValue"),
                root.Int("valueShift"),
                root.Int("valueFloor"),
                root.Int("minWeight"),
                root.Int("maxWeight"),
                root.Int("rowDeviation"));
        }
    }
}

/// <summary>Sorteo ponderado determinista, con el orden fijo de la lista que recibe (RT-041).</summary>
public static class WeightedPick
{
    /// <summary>Índice sorteado según los pesos; -1 si la lista está vacía o todos los pesos son cero.</summary>
    public static int Index(ref Pcg32 rng, IReadOnlyList<int> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        int total = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            total += weights[i];
        }

        if (total <= 0)
        {
            return weights.Count > 0 ? rng.Range(0, weights.Count) : -1;
        }

        int roll = rng.Range(0, total);
        int cumulative = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }
}
