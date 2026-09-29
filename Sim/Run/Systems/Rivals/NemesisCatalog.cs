using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>Un título de némesis de <c>data/nemesis/titles.json</c> (ADR 0165), bilingüe.</summary>
public sealed record NemesisTitle(string Id, LocalizedName Name)
{
    /// <summary>Título en el idioma pedido ("en" o, por defecto, "es").</summary>
    public string NameIn(string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? Name.En : Name.Es;
}

/// <summary>
/// Lo que necesita la memoria de rivales para funcionar (ADR 0165): los clanes (<see cref="Rivals"/>), los
/// títulos de némesis y las dos cifras de la regla (tope de vivos y niveles que suma un némesis, ambas
/// provisionales en <c>data/nemesis/titles.json</c>). Fuera del <see cref="Catalog"/> compartido porque sólo
/// lo consume el bucle de run, como <c>NicknameCatalog</c>. <see cref="Empty"/> es el mundo sin clanes ni
/// némesis: los tests antiguos y los sistemas sin datos juegan exactamente como antes.
/// </summary>
public sealed class NemesisCatalog
{
    private readonly IReadOnlyList<NemesisTitle> _titles;

    public NemesisCatalog(RivalCatalog rivals, IReadOnlyList<NemesisTitle> titles, int maxAlive, int levelBonus)
    {
        Rivals = rivals ?? throw new ArgumentNullException(nameof(rivals));
        ArgumentNullException.ThrowIfNull(titles);
        _titles = titles.OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        MaxAlive = maxAlive;
        LevelBonus = levelBonus;
    }

    /// <summary>Sin clanes ni némesis.</summary>
    public static NemesisCatalog Empty { get; } = new(
        new RivalCatalog(Array.Empty<RivalTeam>()), Array.Empty<NemesisTitle>(), maxAlive: 0, levelBonus: 0);

    /// <summary>Catálogo de rivales (clanes) con el que se resuelve «¿de qué clan es este rival?».</summary>
    public RivalCatalog Rivals { get; }

    /// <summary>Títulos, en orden de id ordinal (RT-041).</summary>
    public IReadOnlyList<NemesisTitle> Titles => _titles;

    /// <summary>Tope de némesis vivos a la vez. Provisional, sin medir (Regla H).</summary>
    public int MaxAlive { get; }

    /// <summary>Niveles que suma un némesis vivo a su puesto. Provisional, sin medir (Regla H).</summary>
    public int LevelBonus { get; }

    /// <summary>True si hay clanes y títulos con los que jugar la regla.</summary>
    public bool IsActive => MaxAlive > 0 && _titles.Count > 0 && Rivals.All.Count > 0;

    /// <summary>Título con ese id, o null.</summary>
    public NemesisTitle? Find(string id)
    {
        for (int i = 0; i < _titles.Count; i++)
        {
            if (string.Equals(_titles[i].Id, id, StringComparison.Ordinal))
            {
                return _titles[i];
            }
        }

        return null;
    }
}

/// <summary>Carga <c>data/nemesis/titles.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class NemesisLoader
{
    private const string Path = "nemesis/titles.json";

    /// <summary>Catálogo de némesis de la instantánea de ficheros indicada, sobre los rivales ya cargados.</summary>
    public static NemesisCatalog FromJson(IReadOnlyDictionary<string, string> files, RivalCatalog rivals)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(rivals);
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
            int maxAlive = root.Int("maxAlive");
            if (maxAlive < 1)
            {
                throw new DataException(Path, "$.maxAlive", "el tope de némesis vivos es al menos 1");
            }

            int levelBonus = root.Int("levelBonus");
            if (levelBonus < 0)
            {
                throw new DataException(Path, "$.levelBonus", "los niveles de un némesis no pueden ser negativos");
            }

            var titles = new List<NemesisTitle>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenEs = new HashSet<string>(StringComparer.Ordinal);
            var seenEn = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Prop("titles").EnumerateArray())
            {
                var title = new NemesisTitle(node.Str("id"), LocalizedNameJson.Read(node.Prop("name")));
                if (!seenIds.Add(title.Id))
                {
                    throw new DataException(Path, node.Path + ".id", $"id de título repetido: '{title.Id}'");
                }

                if (!seenEs.Add(title.Name.Es) || !seenEn.Add(title.Name.En))
                {
                    throw new DataException(Path, node.Path + ".name", $"título repetido: '{title.Name.Es}'");
                }

                titles.Add(title);
            }

            if (titles.Count == 0)
            {
                throw new DataException(Path, "$.titles", "no hay ningún título de némesis");
            }

            return new NemesisCatalog(rivals, titles, maxAlive, levelBonus);
        }
    }
}
