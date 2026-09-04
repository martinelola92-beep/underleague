using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Ayudante de los tests de perks: construye catálogos con perks escritos a mano (pasando el JSON por el
/// cargador real, no por el constructor de <c>PerkDefinition</c>, para que los tests ejerciten también la
/// validación de carga) y equipos generados con <see cref="TeamGenerator"/> a los que asigna perks.
/// </summary>
internal static class TestPerks
{
    /// <summary>
    /// Ids de <c>data/perks/</c> que estos tests conservan del catálogo real: las cinco habilidades
    /// raciales (RF-031b, ADR 0026), que el motor asigna solo por la raza del jugador y sin las que
    /// ningún partido se puede construir. El resto del catálogo se descarta a propósito: lo está
    /// reescribiendo el paquete T al formato de fase1b-diseno.md §1.4, y los tests de perks no deben
    /// depender de en qué punto de esa reescritura esté. Todo lo que estos tests necesitan lo declaran
    /// ellos mismos con <see cref="Json"/>.
    /// </summary>
    private static readonly string[] RacialAbilities =
    {
        "elf_touch", "hot_blooded", "numb", "quick_learner", "roots",
    };

    /// <summary>Catálogo real de /data (sin el catálogo de perks, ver arriba) más los perks indicados.</summary>
    public static Catalog CatalogWith(params (string Id, string Json)[] perks)
    {
        var files = TestData.LoadAllFiles();
        foreach (var path in files.Keys.Where(IsDiscardedPerk).ToList())
        {
            files.Remove(path);
        }

        foreach (var (id, json) in perks)
        {
            files["perks/" + id + ".json"] = json;
        }

        return DataLoader.FromJson(files);
    }

    private static bool IsDiscardedPerk(string path) =>
        path.StartsWith("perks/", StringComparison.Ordinal)
        && !RacialAbilities.Any(id => path == "perks/" + id + ".json");

    /// <summary>Carga un único perk sobre el catálogo real y lo devuelve ya compilado.</summary>
    public static Underleague.Sim.Perks.PerkDefinition Load(string id, string json) =>
        CatalogWith((id, json)).Perks.Get(id);

    /// <summary>
    /// JSON de un perk con los campos que el test necesita. Todo lo que no se indique queda en su valor
    /// por defecto, igual que en un fichero de /data.
    /// </summary>
    public static string Json(
        string id,
        string trigger,
        string effects,
        string rarity = "common",
        string kind = "filler",
        string axis = "identity",
        string? race = null,
        string links = "[]",
        string scope = "actor",
        string condition = "",
        string? limit = null,
        string? elseEffects = null,
        bool accumulates = false,
        string? positionOnly = null,
        string tagsRequired = "[]",
        string tagsForbidden = "[]")
    {
        string limitText = limit is null ? string.Empty : $"\"limit\": {limit},";
        string elseText = elseEffects is null ? string.Empty : $"\"elseEffects\": {elseEffects},";
        string positionText = positionOnly is null ? "null" : $"\"{positionOnly}\"";
        string raceText = race is null ? "null" : $"\"{race}\"";
        return $$"""
        {
          "id": "{{id}}",
          "name": { "es": "{{id}}", "en": "{{id}}" },
          "rarity": "{{rarity}}",
          "kind": "{{kind}}",
          "axis": "{{axis}}",
          "race": {{raceText}},
          "links": {{links}},
          "trigger": "{{trigger}}",
          "scope": "{{scope}}",
          "condition": "{{condition}}",
          "effects": {{effects}},
          {{elseText}}
          {{limitText}}
          "accumulatesAcrossMatches": {{(accumulates ? "true" : "false")}},
          "lethal": false,
          "positionOnly": {{positionText}},
          "tagsRequired": {{tagsRequired}},
          "tagsForbidden": {{tagsForbidden}}
        }
        """;
    }

    /// <summary>Partido de referencia (dos equipos humanos de calidad 50) con perks asignados por id.</summary>
    public static MatchSetup Match(Catalog catalog, ulong seed, params (int PlayerId, string[] Perks)[] assignments)
    {
        var setup = TestMatches.Reference(catalog, seed);
        return setup with
        {
            Home = WithPerks(setup.Home, assignments),
            Away = WithPerks(setup.Away, assignments),
        };
    }

    /// <summary>Motor construido (sin ejecutar) sobre ese partido, para publicar eventos a mano.</summary>
    public static MatchEngine Engine(Catalog catalog, MatchSetup setup, int maxDepth = 4) =>
        new(setup, 1, catalog, new SimConfig(CollectLog: false, MaxDepth: maxDepth));

    private static TeamSetup WithPerks(TeamSetup team, (int PlayerId, string[] Perks)[] assignments)
    {
        var players = new List<PlayerDefinition>(team.Players.Count);
        foreach (var player in team.Players)
        {
            var assignment = assignments.FirstOrDefault(a => a.PlayerId == player.Id);
            players.Add(assignment.Perks is null ? player : player with { Perks = assignment.Perks });
        }

        return team with { Players = players };
    }

    /// <summary>Equipo humano de calidad 50 generado con la semilla indicada (ids firstId..firstId+9).</summary>
    public static TeamSetup Team(Catalog catalog, ulong seed, string id, int firstId)
    {
        var rng = RngStreams.Generation(seed, firstId);
        return TeamGenerator.Generate(ref rng, catalog, id, Race.Human, 50, firstId);
    }
}
