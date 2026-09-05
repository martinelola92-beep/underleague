using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>
/// Catálogo de rivales estáticos (RF-015), cargado de <c>data/rivals/*.json</c>. Agrupados por acto:
/// <see cref="OfAct"/> es la lista que <c>RunSetup.OpponentIdsByAct</c> necesita para que
/// <c>MapGenerator</c> los reparta entre los nodos de partido (fase2-diseno.md §13, tabla de huecos de W).
/// </summary>
public sealed class RivalCatalog
{
    private readonly RivalTeam[] _teams;
    private readonly Dictionary<string, RivalTeam> _byId;
    private readonly Dictionary<int, List<string>> _idsByAct;

    public RivalCatalog(IEnumerable<RivalTeam> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        _teams = teams.OrderBy(t => t.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, RivalTeam>(_teams.Length, StringComparer.Ordinal);
        _idsByAct = new Dictionary<int, List<string>>();
        foreach (var team in _teams)
        {
            _byId.Add(team.Id, team);
            if (!_idsByAct.TryGetValue(team.Act, out var list))
            {
                list = new List<string>();
                _idsByAct[team.Act] = list;
            }

            list.Add(team.Id);
        }
    }

    /// <summary>Todos los rivales, ordenados por id ordinal ascendente.</summary>
    public IReadOnlyList<RivalTeam> All => _teams;

    /// <summary>Busca un rival por id; null si no existe.</summary>
    public RivalTeam? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Ids de los rivales de ese acto, en orden ordinal ascendente. Vacío si el acto no tiene ninguno.</summary>
    public IReadOnlyList<string> OfAct(int act) =>
        _idsByAct.TryGetValue(act, out var list) ? list : Array.Empty<string>();
}

/// <summary>Carga <c>data/rivals/*.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class RivalLoader
{
    /// <summary>Orden de posiciones de <c>Sim.Generation.TeamGenerator</c>: 7 titulares y 3 suplentes.</summary>
    private static readonly Position[] SlotPositions =
    {
        Position.Goalkeeper, Position.Defender, Position.Defender,
        Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward,
        Position.Defender, Position.Midfielder, Position.Forward,
    };

    public static RivalCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var teams = new List<RivalTeam>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("rivals/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            teams.Add(Parse(path, files[path]));
        }

        if (teams.Count == 0)
        {
            throw new DataException("rivals/", "$", "no se ha encontrado ningún rival en data/rivals/");
        }

        return new RivalCatalog(teams);
    }

    private static RivalTeam Parse(string path, string content)
    {
        using var document = ParseJson(path, content);
        var root = Json.Root(path, document);

        string id = root.Str("id");
        var name = LocalizedNameJson.Read(root.Prop("name"));
        var description = LocalizedNameJson.Read(root.Prop("description"));
        var race = ParseRace(root, root.Str("race"));
        int act = root.Int("act");
        if (act < 1 || act > 3)
        {
            throw new DataException(path, "$.act", "el acto debe estar entre 1 y 3");
        }

        int difficulty = root.Int("difficulty");
        if (difficulty < 1 || difficulty > 5)
        {
            throw new DataException(path, "$.difficulty", "la dificultad debe estar entre 1 y 5 (RF-012)");
        }

        var players = new List<RivalPlayer>(10);
        int index = 0;
        foreach (var playerNode in root.Prop("players").EnumerateArray())
        {
            players.Add(ParsePlayer(playerNode, index));
            index++;
        }

        if (players.Count != SlotPositions.Length)
        {
            throw new DataException(path, "$.players", $"un rival necesita exactamente {SlotPositions.Length} jugadores (7 titulares y 3 suplentes)");
        }

        return new RivalTeam(id, name, description, race, act, difficulty, players);
    }

    private static RivalPlayer ParsePlayer(Json node, int index)
    {
        string name = node.Str("name");
        var position = ParsePosition(node, node.Str("position"));
        var expected = SlotPositions[index];
        if (position != expected)
        {
            throw new DataException(node.File, node.Path + ".position", $"el jugador {index} debe ser {expected} (mismo orden que TeamGenerator: 7 titulares GK/DEF/DEF/MID/MID/MID/FWD y 3 suplentes DEF/MID/FWD)");
        }

        var rarity = node.Str("rarity") switch
        {
            "common" => Rarity.Common,
            "uncommon" => Rarity.Uncommon,
            "rare" => Rarity.Rare,
            "legendary" => Rarity.Legendary,
            var other => throw new DataException(node.File, node.Path + ".rarity", $"rareza desconocida: '{other}'"),
        };

        int level = node.Int("level");
        var attributesNode = node.Prop("attributes");
        var attributes = new Attributes(
            attributesNode.Int("strength"),
            attributesNode.Int("speed"),
            attributesNode.Int("technique"),
            attributesNode.Int("stamina"),
            attributesNode.Int("leash"));

        var traits = new List<Trait>();
        var traitsNode = node.TryProp("traits");
        if (traitsNode is { } traitsValue)
        {
            foreach (var traitNode in traitsValue.EnumerateArray())
            {
                traits.Add(ParseTrait(traitNode, traitNode.AsString()));
            }
        }

        var perks = new List<string>();
        var perksNode = node.TryProp("perks");
        if (perksNode is { } perksValue)
        {
            foreach (var perkNode in perksValue.EnumerateArray())
            {
                perks.Add(perkNode.AsString());
            }
        }

        return new RivalPlayer(name, position, rarity, level, attributes, traits, perks);
    }

    private static Position ParsePosition(Json node, string position) => position switch
    {
        "Goalkeeper" => Position.Goalkeeper,
        "Defender" => Position.Defender,
        "Midfielder" => Position.Midfielder,
        "Forward" => Position.Forward,
        _ => throw new DataException(node.File, node.Path + ".position", $"posición desconocida: '{position}'"),
    };

    private static Race ParseRace(Json node, string race) => race switch
    {
        "Human" => Race.Human,
        "Orc" => Race.Orc,
        "Elf" => Race.Elf,
        "Dwarf" => Race.Dwarf,
        "Undead" => Race.Undead,
        "DarkElf" => Race.DarkElf,
        "Demon" => Race.Demon,
        "Vampire" => Race.Vampire,
        "Lizard" => Race.Lizard,
        _ => throw new DataException(node.File, node.Path + ".race", $"raza desconocida: '{race}'"),
    };

    private static Trait ParseTrait(Json node, string trait) => trait switch
    {
        "Aggressive" => Trait.Aggressive,
        "Fast" => Trait.Fast,
        "Scorer" => Trait.Scorer,
        "LongShot" => Trait.LongShot,
        "Cerebral" => Trait.Cerebral,
        "Dirty" => Trait.Dirty,
        "Resilient" => Trait.Resilient,
        "Coward" => Trait.Coward,
        "Leader" => Trait.Leader,
        "Lazy" => Trait.Lazy,
        "Cat" => Trait.Cat,
        "Wall" => Trait.Wall,
        "Rusher" => Trait.Rusher,
        _ => throw new DataException(node.File, node.Path, $"rasgo desconocido: '{trait}'"),
    };

    private static JsonDocument ParseJson(string path, string content)
    {
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(path, "$", $"JSON inválido: {ex.Message}");
        }
    }
}
