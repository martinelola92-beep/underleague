using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Clubs;

/// <summary>
/// Un jugador de la plantilla inicial de un club (RF-005), tal y como se enseña en la pantalla de
/// selección de club: nombre, posición y rareza. No lleva atributos ni perks porque hoy es
/// <b>descriptivo</b>, no la fuente real de la plantilla con la que arranca la run: esa la sigue
/// generando <c>Sim.Generation.TeamGenerator</c> a través de <c>RunSetup.GeneratedQuality</c> (ver
/// <see cref="ClubDefinition"/>), para no mover ninguna métrica de balance al cerrar este arreglo
/// cosmético. Enchufar esta plantilla como la real de la run queda anotado en
/// <c>docs/pendientes.md</c>.
/// </summary>
public sealed record ClubPlayer(string Name, Position Position, Rarity Rarity);

/// <summary>
/// Definición de un club inicial (RF-004): un club por raza jugable al lanzamiento. <c>Name</c> y
/// <c>Description</c> se escriben a mano (no son un efecto, RT-035 no aplica), con las reglas de
/// <c>docs/estilo-descripciones.md</c>.
///
/// <para>
/// <b>Alcance de este dato, anotado explícitamente</b>: <c>StartingGold</c> y <c>Roster</c> son
/// informativos, para la pantalla de selección de club. El oro real con el que arranca la run lo sigue
/// calculando <c>StandardRunSystems.NewRunSetup</c> desde <c>data/economy/economy.json</c> (por división,
/// RF-128), y la plantilla real la sigue generando <c>TeamGenerator</c>; ninguno de los dos lee este
/// fichero todavía. <c>StartingGold</c> aquí coincide con el oro real de la división tercera (la única
/// jugable hoy) precisamente para que la pantalla de selección no prometa un número que la run no da.
/// </para>
///
/// <para>
/// <c>SpecialRule</c> es la "regla especial" de RF-004. No se inventa una mecánica nueva para ella: aquí
/// siempre es el id del perk de habilidad racial que ya existe (ADR 0026, el mismo que
/// <c>RaceDefinition.Ability</c>), o cadena vacía si algún día un club no tuviera una regla propia que
/// mostrar. Queda anotado como pendiente en <c>docs/pendientes.md</c> que RF-004 pida una regla
/// <i>de club</i> y hoy sea, en la práctica, la regla <i>de su raza</i>.
/// </para>
/// </summary>
public sealed record ClubDefinition(
    string Id,
    Race Race,
    LocalizedName Name,
    LocalizedName Description,
    int StartingGold,
    string SpecialRule,
    IReadOnlyList<ClubPlayer> Roster);

/// <summary>Catálogo de clubes iniciales (RF-004), cargado de <c>data/clubs/*.json</c>.</summary>
public sealed class ClubCatalog
{
    private readonly ClubDefinition[] _clubs;
    private readonly Dictionary<string, ClubDefinition> _byId;

    public ClubCatalog(IEnumerable<ClubDefinition> clubs)
    {
        ArgumentNullException.ThrowIfNull(clubs);
        _clubs = clubs.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, ClubDefinition>(_clubs.Length, StringComparer.Ordinal);
        foreach (var club in _clubs)
        {
            _byId.Add(club.Id, club);
        }
    }

    /// <summary>Todos los clubes, ordenados por id ordinal ascendente.</summary>
    public IReadOnlyList<ClubDefinition> All => _clubs;

    /// <summary>Busca un club por id; null si no existe.</summary>
    public ClubDefinition? Find(string id) => _byId.GetValueOrDefault(id);
}

/// <summary>Carga <c>data/clubs/*.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class ClubLoader
{
    /// <summary>Mismo orden de posiciones que <c>Sim.Generation.TeamGenerator</c> y <c>RivalLoader</c>: 7 titulares y 3 suplentes.</summary>
    private static readonly Position[] SlotPositions =
    {
        Position.Goalkeeper, Position.Defender, Position.Defender,
        Position.Midfielder, Position.Midfielder, Position.Midfielder, Position.Forward,
        Position.Defender, Position.Midfielder, Position.Forward,
    };

    public static ClubCatalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var clubs = new List<ClubDefinition>();
        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (!path.StartsWith("clubs/", StringComparison.Ordinal) || !path.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }

            clubs.Add(Parse(path, files[path]));
        }

        if (clubs.Count == 0)
        {
            throw new DataException("clubs/", "$", "no se ha encontrado ningún club en data/clubs/");
        }

        return new ClubCatalog(clubs);
    }

    private static ClubDefinition Parse(string path, string content)
    {
        using var document = ParseJson(path, content);
        var root = Json.Root(path, document);

        string id = root.Str("id");
        var race = ParseRace(root, root.Str("race"));
        var name = LocalizedNameJson.Read(root.Prop("name"));
        var description = LocalizedNameJson.Read(root.Prop("description"));
        int startingGold = root.Int("startingGold");
        string specialRule = root.Str("specialRule");

        var roster = new List<ClubPlayer>(10);
        int index = 0;
        foreach (var playerNode in root.Prop("roster").EnumerateArray())
        {
            roster.Add(ParsePlayer(playerNode, index));
            index++;
        }

        if (roster.Count != SlotPositions.Length)
        {
            throw new DataException(path, "$.roster", $"un club necesita exactamente {SlotPositions.Length} jugadores (7 titulares y 3 suplentes, RF-005)");
        }

        return new ClubDefinition(id, race, name, description, startingGold, specialRule, roster);
    }

    private static ClubPlayer ParsePlayer(Json node, int index)
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

        return new ClubPlayer(name, position, rarity);
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
