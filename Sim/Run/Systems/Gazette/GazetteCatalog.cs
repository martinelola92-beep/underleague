using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Gazette;

/// <summary>
/// Plantillas de la Gaceta de fin de run (ADR 0163, RF-122): para cada clave, una lista de variantes por
/// idioma. La vista elige la variante con una función pura de la semilla de la run, no con un RNG: el
/// mismo desenlace se cuenta siempre igual (RT-021, RT-035).
/// </summary>
public sealed class GazetteCatalog
{
    /// <summary>
    /// Claves que la vista de la Gaceta necesita. Un fichero al que le falte alguna es un error al cargar
    /// (RT-032), no un texto vacío en la portada de fin de run.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredKeys = new[]
    {
        "masthead",
        "headline.victoryClean", "headline.victory", "headline.defeatBoss", "headline.defeatPlayers",
        "lede.victory", "lede.defeatBoss", "lede.defeatPlayers",
        "mvp.title", "mvp.line", "mvp.none",
        "highlight.matches.one", "highlight.matches.many",
        "highlight.goals.one", "highlight.goals.many",
        "highlight.assists.one", "highlight.assists.many",
        "highlight.tacklesWon.one", "highlight.tacklesWon.many",
        "highlight.injuriesCaused.one", "highlight.injuriesCaused.many",
        "highlight.deathsCaused.one", "highlight.deathsCaused.many",
        "highlight.and",
        "villain.title", "villain.deaths", "villain.injuries",
        "obituaries.title", "obituaries.none", "obituaries.entry",
        "epitaph.byRival", "epitaph.byOpponent", "epitaph.noAuthor", "epitaph.sacrifice", "epitaph.quack",
        "epitaph.unknown", "epitaph.career", "epitaph.noCareer",
    };

    private static readonly string[] RunFacts = { "deaths", "wins", "matches", "acts", "act" };
    private static readonly string[] None = Array.Empty<string>();

    /// <summary>
    /// Los marcadores <c>{...}</c> que <b>puede</b> usar una plantilla de esa clave: los que la vista le
    /// da al pintarla (<c>GazetteView</c>). El cargador rechaza cualquier otro (RT-032) en vez de dejar que
    /// la vista lo borre en silencio. Null si la clave no es de las que conoce la vista (una plantilla
    /// extra no se valida).
    /// </summary>
    public static IReadOnlyList<string>? MarkersFor(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key switch
        {
            "masthead" or "mvp.title" or "mvp.none" or "villain.title" or "obituaries.title" or "obituaries.none" => RunFacts,
            "mvp.line" => new[] { "name", "nick", "highlights" },
            "villain.deaths" or "villain.injuries" => new[] { "villain", "clan", "deaths", "injuries" },
            "obituaries.entry" or "epitaph.noCareer" => new[] { "name", "nick", "race", "level" },
            "epitaph.byRival" => new[] { "killer", "clan" },
            "epitaph.career" => new[] { "career" },
            "highlight.and" => None,
            "epitaph.byOpponent" or "epitaph.noAuthor" or "epitaph.sacrifice" or "epitaph.quack" or "epitaph.unknown" => None,
            _ when key.StartsWith("headline.", StringComparison.Ordinal) || key.StartsWith("lede.", StringComparison.Ordinal) => RunFacts,
            _ when key.StartsWith("highlight.", StringComparison.Ordinal) => new[] { "n" },
            _ => null,
        };
    }

    private readonly Dictionary<string, IReadOnlyList<string>> _es;
    private readonly Dictionary<string, IReadOnlyList<string>> _en;

    public GazetteCatalog(
        IReadOnlyDictionary<string, IReadOnlyList<string>> es,
        IReadOnlyDictionary<string, IReadOnlyList<string>> en)
    {
        ArgumentNullException.ThrowIfNull(es);
        ArgumentNullException.ThrowIfNull(en);
        _es = new Dictionary<string, IReadOnlyList<string>>(es, StringComparer.Ordinal);
        _en = new Dictionary<string, IReadOnlyList<string>>(en, StringComparer.Ordinal);
    }

    /// <summary>Catálogo vacío: sin Gaceta (tests antiguos y sistemas sin datos). <see cref="Variants"/> devuelve vacío.</summary>
    public static GazetteCatalog Empty { get; } = new(
        new Dictionary<string, IReadOnlyList<string>>(),
        new Dictionary<string, IReadOnlyList<string>>());

    /// <summary>True si no tiene ninguna plantilla.</summary>
    public bool IsEmpty => _es.Count == 0;

    /// <summary>Variantes de una clave en un idioma ("en" o, por defecto, "es"); vacío si no existe.</summary>
    public IReadOnlyList<string> Variants(string key, string language)
    {
        var table = string.Equals(language, "en", StringComparison.Ordinal) ? _en : _es;
        return table.TryGetValue(key, out var variants) ? variants : Array.Empty<string>();
    }
}

/// <summary>Carga <c>data/gazette/gazette.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class GazetteLoader
{
    private const string Path = "gazette/gazette.json";

    /// <summary>Plantillas de la instantánea de ficheros indicada.</summary>
    public static GazetteCatalog FromJson(IReadOnlyDictionary<string, string> files)
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
            var es = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var en = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var node in root.Prop("texts").EnumerateArray())
            {
                string key = node.Str("key");
                if (es.ContainsKey(key))
                {
                    throw new DataException(Path, node.Path + ".key", $"clave de texto repetida: '{key}'");
                }

                es[key] = Variants(node.Prop("es"));
                en[key] = Variants(node.Prop("en"));
                CheckMarkers(key, node.Prop("es"), es[key]);
                CheckMarkers(key, node.Prop("en"), en[key]);
            }

            foreach (string required in GazetteCatalog.RequiredKeys)
            {
                if (!es.ContainsKey(required))
                {
                    throw new DataException(Path, "$.texts", $"falta la plantilla '{required}' que necesita la Gaceta");
                }
            }

            return new GazetteCatalog(es, en);
        }
    }

    /// <summary>Cada <c>{marcador}</c> de cada variante tiene que estar en <see cref="GazetteCatalog.MarkersFor"/>.</summary>
    private static void CheckMarkers(string key, Json node, IReadOnlyList<string> variants)
    {
        var allowed = GazetteCatalog.MarkersFor(key);
        if (allowed is null)
        {
            return;
        }

        for (int v = 0; v < variants.Count; v++)
        {
            string text = variants[v];
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                int close = open < 0 ? -1 : text.IndexOf('}', open + 1);
                if (close < 0)
                {
                    break;
                }

                string marker = text.Substring(open + 1, close - open - 1);
                if (!allowed.Contains(marker))
                {
                    throw new DataException(
                        node.File,
                        node.Path + "[" + v + "]",
                        $"la plantilla '{key}' usa el marcador {{{marker}}}, que la Gaceta no le da (admite: {(allowed.Count == 0 ? "ninguno" : string.Join(", ", allowed))})");
                }

                i = close + 1;
            }
        }
    }

    private static IReadOnlyList<string> Variants(Json node)
    {
        var list = new List<string>();
        foreach (var item in node.EnumerateArray())
        {
            string text = item.AsString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new DataException(item.File, item.Path, "una variante de la Gaceta no puede estar vacía");
            }

            list.Add(text);
        }

        if (list.Count == 0)
        {
            throw new DataException(node.File, node.Path, "una plantilla de la Gaceta necesita al menos una variante");
        }

        return list;
    }
}
