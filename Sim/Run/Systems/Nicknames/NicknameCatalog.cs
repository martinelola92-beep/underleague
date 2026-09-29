using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Nicknames;

/// <summary>Campo de <see cref="RunCareer"/> sobre el que un apodo pone su umbral (ADR 0163).</summary>
public enum NicknameStat
{
    Matches,
    Goals,
    Assists,
    Tackles,
    TacklesWon,
    Fouls,
    Cards,
    InjuriesCaused,
    DeathsCaused,
    InjuriesSuffered,
}

/// <summary>
/// Un apodo de <c>data/nicknames/nicknames.json</c> (ADR 0163): se gana cuando <see cref="Stat"/> de la
/// carrera llega a <see cref="Threshold"/>. Con varios cumplidos gana la <see cref="Priority"/> mayor.
/// </summary>
public sealed record NicknameDefinition(string Id, LocalizedName Name, NicknameStat Stat, int Threshold, int Priority)
{
    /// <summary>Nombre en el idioma pedido ("en" o, por defecto, "es").</summary>
    public string NameIn(string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? Name.En : Name.Es;
}

/// <summary>
/// Catálogo de apodos, ordenado por prioridad descendente y, a igual prioridad, por id ascendente (RT-041,
/// RT-097: nunca un Dictionary iterado), fuera del <see cref="Catalog"/> compartido porque solo lo consume
/// el bucle de run, como <c>RefereeCatalog</c>.
/// </summary>
public sealed class NicknameCatalog
{
    private readonly IReadOnlyList<NicknameDefinition> _nicknames;

    public NicknameCatalog(IReadOnlyList<NicknameDefinition> nicknames)
    {
        ArgumentNullException.ThrowIfNull(nicknames);
        _nicknames = nicknames
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Catálogo vacío: sin apodos (tests antiguos y sistemas sin datos).</summary>
    public static NicknameCatalog Empty { get; } = new(Array.Empty<NicknameDefinition>());

    /// <summary>Todos los apodos, por prioridad descendente y luego id ascendente.</summary>
    public IReadOnlyList<NicknameDefinition> All => _nicknames;

    /// <summary>Apodo con ese id, o null.</summary>
    public NicknameDefinition? Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < _nicknames.Count; i++)
        {
            if (string.Equals(_nicknames[i].Id, id, StringComparison.Ordinal))
            {
                return _nicknames[i];
            }
        }

        return null;
    }
}

/// <summary>Carga <c>data/nicknames/nicknames.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class NicknameLoader
{
    private const string Path = "nicknames/nicknames.json";

    /// <summary>Catálogo de apodos de la instantánea de ficheros indicada.</summary>
    public static NicknameCatalog FromJson(IReadOnlyDictionary<string, string> files)
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
            var nicknames = new List<NicknameDefinition>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var seenNamesEs = new HashSet<string>(StringComparer.Ordinal);
            var seenNamesEn = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Prop("nicknames").EnumerateArray())
            {
                var nickname = Parse(node);
                if (!seenIds.Add(nickname.Id))
                {
                    throw new DataException(Path, node.Path + ".id", $"id de apodo repetido: '{nickname.Id}'");
                }

                // Dos apodos con el mismo nombre serían indistinguibles en el informe y en la Gaceta.
                if (!seenNamesEs.Add(nickname.Name.Es))
                {
                    throw new DataException(Path, node.Path + ".name.es", $"apodo repetido en español: '{nickname.Name.Es}'");
                }

                if (!seenNamesEn.Add(nickname.Name.En))
                {
                    throw new DataException(Path, node.Path + ".name.en", $"apodo repetido en inglés: '{nickname.Name.En}'");
                }

                nicknames.Add(nickname);
            }

            if (nicknames.Count == 0)
            {
                throw new DataException(Path, "$.nicknames", "no hay ningún apodo en data/nicknames/nicknames.json");
            }

            return new NicknameCatalog(nicknames);
        }
    }

    private static NicknameDefinition Parse(Json node)
    {
        int threshold = node.Int("threshold");
        if (threshold < 1)
        {
            throw new DataException(node.File, node.Path + ".threshold", "el umbral de un apodo es al menos 1");
        }

        int priority = node.Int("priority");
        if (priority < 1)
        {
            throw new DataException(node.File, node.Path + ".priority", "la prioridad de un apodo es al menos 1");
        }

        return new NicknameDefinition(
            node.Str("id"),
            LocalizedNameJson.Read(node.Prop("name")),
            ParseStat(node.Prop("stat")),
            threshold,
            priority);
    }

    private static NicknameStat ParseStat(Json element)
    {
        string text = element.AsString();
        return text switch
        {
            "matches" => NicknameStat.Matches,
            "goals" => NicknameStat.Goals,
            "assists" => NicknameStat.Assists,
            "tackles" => NicknameStat.Tackles,
            "tacklesWon" => NicknameStat.TacklesWon,
            "fouls" => NicknameStat.Fouls,
            "cards" => NicknameStat.Cards,
            "injuriesCaused" => NicknameStat.InjuriesCaused,
            "deathsCaused" => NicknameStat.DeathsCaused,
            "injuriesSuffered" => NicknameStat.InjuriesSuffered,
            var other => throw new DataException(element.File, element.Path, $"campo de carrera desconocido: '{other}'"),
        };
    }
}
