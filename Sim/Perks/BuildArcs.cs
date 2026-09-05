using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Perks;

/// <summary>
/// Lo que un perk <b>maestro</b> exige para poder cobrarse (ADR 0051): llevar ya <paramref name="Count"/>
/// perks <b>distintos</b> de la línea <paramref name="Family"/> en la plantilla de la run. Se cuentan ids
/// distintos, no portadores: tres copias del mismo perk en tres jugadores siguen siendo una pieza de la
/// línea, no tres.
/// </summary>
public sealed record MasterRequirement(string Family, int Count);

/// <summary>
/// Lo que un perk cierra de forma <b>permanente</b> en esa run al aceptarlo (ADR 0051): líneas enteras o
/// perks concretos. El bloqueo mira <b>hacia adelante</b>: lo que ya se lleva sigue funcionando —un perk
/// no se puede retirar (RF-072), así que apagarlo sería borrar algo ya pagado— y lo que deja de existir
/// es la posibilidad de conseguir más de esa línea en lo que queda de run.
/// </summary>
public sealed record PerkBlock(IReadOnlyList<string> Families, IReadOnlyList<string> Perks)
{
    /// <summary>No cierra nada: el caso normal del catálogo.</summary>
    public static PerkBlock None { get; } = new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>True si el perk cierra algo.</summary>
    public bool Any => Families.Count > 0 || Perks.Count > 0;
}

/// <summary>
/// Curva de <b>profundidad nativa</b> (ADR 0051, tabla de asignación de Angband): peso relativo de un
/// perk o un objeto en el pool según la distancia entre el acto en curso y su acto nativo.
///
/// <para>Dos mitades. Por <b>encima</b> del acto nativo el peso decae despacio
/// (<c>nativePercent</c>): el relleno del acto 1 sigue saliendo en el 3, solo que menos, que es lo que
/// deja sitio a lo hondo sin vaciar el surtido. Por <b>debajo</b> queda un peso pequeño
/// (<c>outOfDepthPercent</c>): la aparición <i>fuera de profundidad</i>, que convierte encontrar algo del
/// acto 3 en el 1 en un momento memorable en vez de en algo imposible.</para>
///
/// <para>La lección del rebalanceo de Angband 3.5 —<b>aplanar</b>— está en los números, no en la forma:
/// la decadencia por encima del acto nativo es suave y el acto nativo de lo bueno es temprano.</para>
/// </summary>
public sealed class DepthCurve
{
    private readonly int[] _native;
    private readonly int[] _below;

    public DepthCurve(IReadOnlyList<int> nativePercent, IReadOnlyList<int> outOfDepthPercent, int masterPreviewPercent = 100)
    {
        ArgumentNullException.ThrowIfNull(nativePercent);
        ArgumentNullException.ThrowIfNull(outOfDepthPercent);
        _native = nativePercent.ToArray();
        _below = outOfDepthPercent.ToArray();
        MasterPreviewPercent = masterPreviewPercent;
    }

    /// <summary>Sin profundidad: todo pesa lo mismo en todos los actos. Es lo que había antes de la ADR 0051.</summary>
    public static DepthCurve Flat { get; } = new(new[] { 100, 100, 100 }, new[] { 100, 100 });

    /// <summary>
    /// Peso de un maestro al que le falta una pieza de su línea, en porcentaje del que tiene cuando ya se
    /// puede comprar (ADR 0055). Es el ajuste que separa **anunciar** el objetivo de **poder cerrarlo**:
    /// medido, un maestro llegaba al mostrador 5,3 veces por run y solo 0,13 de ellas eran comprables,
    /// porque casi todas las apariciones caían cuando la línea aún no estaba hecha.
    /// </summary>
    public int MasterPreviewPercent { get; }

    /// <summary>Peso completo, el del acto nativo.</summary>
    public int FullPercent => _native[0];

    /// <summary>
    /// Peso relativo (en porcentaje del completo) de algo con acto nativo <paramref name="minAct"/> en el
    /// acto <paramref name="act"/>. Cero significa que no puede salir.
    /// </summary>
    public int WeightPercent(int minAct, int act)
    {
        int distance = act - minAct;
        if (distance >= 0)
        {
            return _native[Math.Min(distance, _native.Length - 1)];
        }

        int below = (-distance) - 1;
        return below < _below.Length ? _below[below] : 0;
    }
}

/// <summary>
/// Arcos de build (ADR 0051), cargados de <c>data/build/arcs.json</c>: la lista canónica de <b>líneas</b>
/// del catálogo y la curva de profundidad nativa del pool. Es un dato, no código (RT-031): qué perk
/// pertenece a qué línea lo declara el propio perk y aquí solo está la lista contra la que se valida, de
/// modo que una errata en un <c>family</c> sea un error de carga y no una familia fantasma con un solo
/// miembro.
/// <para>No hace E/S (RT-012): recibe el contenido del fichero ya leído.</para>
/// </summary>
public sealed class BuildArcs
{
    private readonly string[] _families;

    public BuildArcs(IReadOnlyList<string> families, DepthCurve depth)
    {
        ArgumentNullException.ThrowIfNull(families);
        _families = families.ToArray();
        Depth = depth ?? throw new ArgumentNullException(nameof(depth));
    }

    /// <summary>Sin arcos: ninguna línea declarada y ninguna profundidad. El catálogo de antes de la ADR 0051.</summary>
    public static BuildArcs None { get; } = new(Array.Empty<string>(), DepthCurve.Flat);

    /// <summary>Líneas declaradas, en el orden del fichero.</summary>
    public IReadOnlyList<string> Families => _families;

    /// <summary>Curva de profundidad nativa.</summary>
    public DepthCurve Depth { get; }

    /// <summary>True si esa línea está declarada en <c>data/build/arcs.json</c>.</summary>
    public bool HasFamily(string family)
    {
        for (int i = 0; i < _families.Length; i++)
        {
            if (string.Equals(_families[i], family, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lista de líneas para un mensaje de error del cargador.</summary>
    public string FamilyList() => _families.Length == 0 ? "(ninguna)" : string.Join(", ", _families);

    /// <summary>Analiza <c>data/build/arcs.json</c>. Lanza <see cref="DataException"/> si no cumple el esquema.</summary>
    public static BuildArcs Parse(string file, string content)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(file, "$", $"JSON inválido: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new DataException(file, "$", "se esperaba un objeto");
            }

            var families = new List<string>();
            if (!root.TryGetProperty("families", out var familiesNode) || familiesNode.ValueKind != JsonValueKind.Array)
            {
                throw new DataException(file, "$.families", "falta la lista de líneas");
            }

            foreach (var entry in familiesNode.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var idNode)
                    || idNode.ValueKind != JsonValueKind.String)
                {
                    throw new DataException(file, "$.families", "cada línea necesita un 'id' de cadena");
                }

                string id = idNode.GetString()!;
                if (families.Contains(id, StringComparer.Ordinal))
                {
                    throw new DataException(file, "$.families", $"línea repetida '{id}'");
                }

                families.Add(id);
            }

            if (!root.TryGetProperty("depth", out var depthNode) || depthNode.ValueKind != JsonValueKind.Object)
            {
                throw new DataException(file, "$.depth", "falta la curva de profundidad");
            }

            var native = Ints(file, depthNode, "nativePercent", 3);
            var below = Ints(file, depthNode, "outOfDepthPercent", 2);
            int preview = 100;
            if (depthNode.TryGetProperty("masterPreviewPercent", out var previewNode))
            {
                if (previewNode.ValueKind != JsonValueKind.Number || !previewNode.TryGetInt32(out preview)
                    || preview < 1 || preview > 100)
                {
                    throw new DataException(
                        file, "$.depth.masterPreviewPercent", "se esperaba un entero entre 1 y 100");
                }
            }

            for (int i = 1; i < native.Count; i++)
            {
                if (native[i] > native[i - 1])
                {
                    throw new DataException(
                        file,
                        "$.depth.nativePercent",
                        "la curva por encima del acto nativo solo puede decaer: un perk no puede ser más "
                            + "común lejos de su acto nativo que en él");
                }
            }

            for (int i = 0; i < below.Count; i++)
            {
                if (below[i] >= native[0])
                {
                    throw new DataException(
                        file,
                        "$.depth.outOfDepthPercent",
                        "la aparición fuera de profundidad tiene que ser más rara que la de su acto nativo "
                            + "(ADR 0051: es la sorpresa, no la norma)");
                }
            }

            return new BuildArcs(families, new DepthCurve(native, below, preview));
        }
    }

    private static List<int> Ints(string file, JsonElement parent, string name, int minimum)
    {
        if (!parent.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Array)
        {
            throw new DataException(file, "$.depth." + name, "se esperaba un array de enteros");
        }

        var values = new List<int>();
        foreach (var entry in node.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetInt32(out int value) || value < 0)
            {
                throw new DataException(file, "$.depth." + name, "se esperaba un entero no negativo");
            }

            values.Add(value);
        }

        if (values.Count < minimum)
        {
            throw new DataException(file, "$.depth." + name, $"hacen falta al menos {minimum} valores");
        }

        return values;
    }
}
