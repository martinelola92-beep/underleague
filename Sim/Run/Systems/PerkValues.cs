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

    /// <summary>
    /// Curva de valor contra el <b>horizonte</b> —los partidos que al perk le quedan por jugar— para los
    /// perks cuyo valor depende de él (AV-B). El índice 0 es el horizonte 1. Los perks que no están aquí
    /// valen <see cref="_values"/> a cualquier horizonte, y eso no es una simplificación: la ADR 0070
    /// midió que arrastrar el contador mueve <b>0,0</b> unidades en los 36 perks sin
    /// <c>accumulatesAcrossMatches</c> —salen bit a bit idénticos— y 61,4 de media en los quince que sí.
    /// </summary>
    private readonly Dictionary<string, int[]> _horizonValues;

    /// <summary>Valores de la tabla en orden ascendente; el índice i corresponde a <see cref="_cumulativeWeight"/>[i].</summary>
    private readonly int[] _sortedValues;

    /// <summary>Peso acumulado hasta ese valor incluido, en el mismo orden que <see cref="_sortedValues"/>.</summary>
    private readonly long[] _cumulativeWeight;

    /// <summary>La misma distribución de oferta, una por horizonte (índice 0 = horizonte 1).</summary>
    private readonly int[][] _sortedByHorizon;

    private readonly long[][] _cumulativeByHorizon;

    private readonly long[] _totalWeightByHorizon;

    private readonly int[] _meanByHorizon;

    private readonly int[] _observedByHorizon;

    private readonly int[] _rowDeviationByHorizon;

    private PerkValueTable(
        Dictionary<string, int> values,
        int baseWeight,
        int referenceValue,
        int valueShift,
        int valueFloor,
        int minWeight,
        int maxWeight,
        int rowDeviation,
        Dictionary<string, int[]>? horizonValues = null,
        int[]? rowDeviationByHorizon = null,
        int referenceHorizon = 0)
    {
        _values = values;
        _horizonValues = horizonValues ?? new Dictionary<string, int[]>(StringComparer.Ordinal);
        _rowDeviationByHorizon = rowDeviationByHorizon ?? Array.Empty<int>();
        Horizons = _rowDeviationByHorizon.Length;
        ReferenceHorizon = referenceHorizon;
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

        // La misma distribución de oferta, evaluada a cada horizonte (AV-B). El PESO no cambia con el
        // horizonte —es la palanca de frecuencia de la ADR 0038, una propiedad del pool y no del momento
        // en que la run mira— y lo que cambia es el VALOR de cada perk. Así el listón del slot compara
        // el perk que se juzga con la oferta que llenaría el slot **en el mismo momento**, que es
        // exactamente lo que la tabla de un solo número no podía hacer.
        _sortedByHorizon = new int[Horizons][];
        _cumulativeByHorizon = new long[Horizons][];
        _totalWeightByHorizon = new long[Horizons];
        _meanByHorizon = new int[Horizons];
        _observedByHorizon = new int[Horizons];
        var ordered = new List<KeyValuePair<string, int>>(values.Count);
        for (int h = 0; h < Horizons; h++)
        {
            ordered.Clear();
            foreach (var (id, _) in values)
            {
                ordered.Add(new KeyValuePair<string, int>(id, ValueAt(id, h + 1)));
            }

            ordered.Sort((a, b) =>
            {
                int byValue = a.Value.CompareTo(b.Value);
                return byValue != 0 ? byValue : string.CompareOrdinal(a.Key, b.Key);
            });

            var hv = new int[ordered.Count];
            var hc = new long[ordered.Count];
            long running = 0;
            long total = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                hv[i] = ordered[i].Value;
                running += WeightFor(_values[ordered[i].Key]);
                hc[i] = running;
                total += hv[i];
            }

            _sortedByHorizon[h] = hv;
            _cumulativeByHorizon[h] = hc;
            _totalWeightByHorizon[h] = running;
            int mean = hv.Length > 0 ? (int)(total / hv.Length) : 0;
            _meanByHorizon[h] = mean;
            long sq = 0;
            for (int i = 0; i < hv.Length; i++)
            {
                long d = hv[i] - mean;
                sq += d * d;
            }

            _observedByHorizon[h] = hv.Length > 0 ? (int)Math.Sqrt((double)sq / hv.Length) : 0;
        }
    }

    /// <summary>Tabla vacía: todos los perks pesan lo mismo. Es lo que usa una instantánea sin fichero de valores.</summary>
    public static PerkValueTable Uniform { get; } =
        new(new Dictionary<string, int>(StringComparer.Ordinal), 100, 500, 500, 100, 100, 100, 0);

    /// <summary>Horizontes medidos, o 0 si la tabla no trae curva (AV-B).</summary>
    public int Horizons { get; }

    /// <summary>
    /// Horizonte al que está medido <see cref="ValueOf(string)"/>, es decir la campaña de la ADR 0070:
    /// <b>ocho</b> partidos. Es el valor que sigue alimentando el peso del pool, porque la palanca de
    /// frecuencia es una propiedad del perk sobre una run entera y no del momento en que se ofrece.
    /// </summary>
    public int ReferenceHorizon { get; }

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

    /// <summary>
    /// El mismo cuantil, pero sobre la distribución de oferta <b>valorada al horizonte</b>
    /// <paramref name="horizon"/> (AV-B). Es la mitad que faltaba: la ADR 0072 comparaba el valor del
    /// perk que se juzga con el de la oferta que llenará el slot, y los dos salían de la misma columna
    /// medida en campaña de ocho partidos. Con la curva, los dos se leen en el mismo momento de la run.
    /// </summary>
    public int ValueAtQuantile(long numerator, long denominator, int horizon)
    {
        if (Horizons <= 0)
        {
            return ValueAtQuantile(numerator, denominator);
        }

        int h = Math.Clamp(horizon, 1, Horizons) - 1;
        var values = _sortedByHorizon[h];
        var cumulative = _cumulativeByHorizon[h];
        if (values.Length == 0 || denominator <= 0)
        {
            return 0;
        }

        if (numerator <= 0)
        {
            return values[0];
        }

        if (numerator >= denominator)
        {
            return values[^1];
        }

        long target = _totalWeightByHorizon[h] * numerator / denominator;
        for (int i = 0; i < cumulative.Length; i++)
        {
            if (cumulative[i] >= target)
            {
                return values[i];
            }
        }

        return values[^1];
    }

    /// <summary>Valor medido del perk, en milésimas de punto de tasa de victoria; null si no está medido.</summary>
    public int? ValueOf(string perkId) => _values.TryGetValue(perkId, out int value) ? value : null;

    /// <summary>
    /// Valor medido del perk <b>al horizonte que tiene</b> (AV-B): lo que gana un equipo por llevarlo si
    /// sólo le quedan <paramref name="horizon"/> partidos por jugar. El horizonte se recorta al rango
    /// medido, así que por debajo de 1 vale lo del primer partido y por encima del último medido vale lo
    /// del último — la curva es plana ahí porque los contadores ya han tocado su <c>maxValue</c>.
    ///
    /// <para>Sin curva declarada, o para un perk que no la tiene, devuelve el valor de referencia: es la
    /// misma cifra que <see cref="ValueOf(string)"/> y el comportamiento de antes de este cambio.</para>
    /// </summary>
    public int ValueAt(string perkId, int horizon)
    {
        if (Horizons > 0 && _horizonValues.TryGetValue(perkId, out var curve))
        {
            return curve[Math.Clamp(horizon, 1, curve.Length) - 1];
        }

        return _values.TryGetValue(perkId, out int value) ? value : 0;
    }

    /// <summary>Media de los valores al horizonte dado; <see cref="MeanValue"/> si la tabla no trae curva.</summary>
    public int MeanValueAt(int horizon) =>
        Horizons > 0 ? _meanByHorizon[Math.Clamp(horizon, 1, Horizons) - 1] : MeanValue;

    /// <summary>Dispersión observada entre perks al horizonte dado.</summary>
    public int ObservedDeviationAt(int horizon) =>
        Horizons > 0 ? _observedByHorizon[Math.Clamp(horizon, 1, Horizons) - 1] : ObservedDeviation;

    /// <summary>
    /// Desviación <b>por fila</b> de la medida a ese horizonte. No es la misma a todos: el valor a
    /// horizonte 1 sale de un dieciseisavo de los partidos que sostienen el de horizonte 16, así que su
    /// ruido es mayor y el listón tiene que corregir por él y no por el de la campaña entera.
    /// </summary>
    public int RowDeviationAt(int horizon) =>
        Horizons > 0 ? _rowDeviationByHorizon[Math.Clamp(horizon, 1, Horizons) - 1] : RowDeviation;

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

            // Curva por horizonte (AV-B). Opcional: sin ella la tabla se comporta exactamente como antes.
            var horizonValues = new Dictionary<string, int[]>(StringComparer.Ordinal);
            int[] rowDeviationByHorizon = Array.Empty<int>();
            int referenceHorizon = 0;
            if (root.TryProp("valuesByHorizon") is { } byHorizon)
            {
                var deviations = new List<int>();
                foreach (var element in root.Prop("rowDeviationByHorizon").EnumerateArray())
                {
                    deviations.Add(element.AsInt());
                }

                rowDeviationByHorizon = deviations.ToArray();
                referenceHorizon = root.Int("referenceHorizon");
                foreach (var property in byHorizon.EnumerateObject())
                {
                    var curve = new List<int>(rowDeviationByHorizon.Length);
                    foreach (var element in property.Value.EnumerateArray())
                    {
                        curve.Add(element.AsInt());
                    }

                    if (curve.Count != rowDeviationByHorizon.Length)
                    {
                        throw new DataException(
                            Path,
                            $"$.valuesByHorizon.{property.Name}",
                            $"la curva tiene {curve.Count} horizontes y rowDeviationByHorizon declara {rowDeviationByHorizon.Length}");
                    }

                    if (referenceHorizon >= 1
                        && referenceHorizon <= curve.Count
                        && values.TryGetValue(property.Name, out int reference)
                        && curve[referenceHorizon - 1] != reference)
                    {
                        // La columna del horizonte de referencia y `values` son la MISMA medición: si se
                        // separan, una de las dos se ha regenerado sin la otra y el listón compararía
                        // dos tablas distintas. Es un error explícito, no un ajuste silencioso (RT-032).
                        throw new DataException(
                            Path,
                            $"$.valuesByHorizon.{property.Name}[{referenceHorizon - 1}]",
                            $"vale {curve[referenceHorizon - 1]} y values.{property.Name} vale {reference}: son la misma medida y tienen que coincidir");
                    }

                    horizonValues[property.Name] = curve.ToArray();
                }
            }

            return new PerkValueTable(
                values,
                root.Int("baseWeight"),
                root.Int("referenceValue"),
                root.Int("valueShift"),
                root.Int("valueFloor"),
                root.Int("minWeight"),
                root.Int("maxWeight"),
                root.Int("rowDeviation"),
                horizonValues,
                rowDeviationByHorizon,
                referenceHorizon);
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
