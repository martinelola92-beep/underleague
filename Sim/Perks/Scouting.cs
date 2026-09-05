using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Un perk rival capaz de matar (RF-013): quién lo lleva, cuál es y cómo se llama en el idioma del
/// jugador. Es lo que el informe de ojeo tiene que destacar antes de jugar (RF-012b, RF-012d).
/// </summary>
public sealed record LethalThreat(int PlayerId, string PlayerName, string PerkId, LocalizedName PerkName);


/// <summary>
/// La <b>build</b> de un equipo tal y como la lee el informe de ojeo (RF-012b, RF-015): no hay un campo
/// de etiqueta de build en ningún sitio, la build es el propio roster, así que esto es lo que hay que
/// mirar de él para reconocerla. Todo son cuentas enteras sobre el equipo ya construido.
/// </summary>
/// <param name="Race">Raza del equipo (RF-004: un club, una raza).</param>
/// <param name="Players">Jugadores del equipo.</param>
/// <param name="MinLevel">Nivel más bajo de la plantilla.</param>
/// <param name="MaxLevel">Nivel más alto.</param>
/// <param name="AverageLevel">Nivel medio, entero (RT-023: aritmética entera).</param>
/// <param name="Styles">Etiquetas de estilo por frecuencia descendente y, a igualdad, por nombre ascendente.</param>
/// <param name="Traits">Rasgos por frecuencia descendente y, a igualdad, por nombre ascendente.</param>
/// <param name="Perks">Ids de perk por frecuencia descendente y, a igualdad, por id ascendente (RT-041).</param>
public sealed record TeamProfile(
    Race Race,
    int Players,
    int MinLevel,
    int MaxLevel,
    int AverageLevel,
    IReadOnlyList<(StyleTag Style, int Count)> Styles,
    IReadOnlyList<(Trait Trait, int Count)> Traits,
    IReadOnlyList<(string PerkId, int Count)> Perks);

/// <summary>
/// Consultas puras sobre un equipo ya construido, para las pantallas de información previas al partido.
/// No simulan nada: leen el <see cref="TeamSetup"/> y el catálogo.
/// </summary>
public static class Scouting
{
    /// <summary>
    /// Perks letales del equipo (RF-013, RF-093 vía 2), ordenados por id de jugador ascendente y, dentro
    /// de un jugador, por id de perk ordinal ascendente (RT-041).
    ///
    /// <para>Cuenta tanto los perks asignados como la habilidad racial (ADR 0026), porque desde el otro
    /// lado del campo la distinción no existe: lo que importa es si ese rival puede matar. Un perk
    /// exclusivo de raza que el portador no puede activar (le falta la etiqueta de especie, ADR 0023 §4)
    /// <b>no</b> se lista: no va a dispararse, y anunciar un peligro que no existe es tan malo como
    /// callarse uno que sí (RF-012d).</para>
    /// </summary>
    public static IReadOnlyList<LethalThreat> LethalPerks(TeamSetup team, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(catalog);

        var threats = new List<LethalThreat>();
        var players = new List<PlayerDefinition>(team.Players);
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        foreach (var player in players)
        {
            var ability = EffectEngine.RacialAbility(catalog, player);
            var ids = new List<string>(player.Perks.Count + 1);
            if (ability is not null && ability.Lethal)
            {
                ids.Add(ability.Id);
            }

            for (int i = 0; i < player.Perks.Count; i++)
            {
                var perk = catalog.Perks.Find(player.Perks[i]);
                if (perk is null || !perk.Lethal)
                {
                    continue;
                }

                if (perk.Race is { } required && !player.HasTag(required.ToString()))
                {
                    continue;
                }

                ids.Add(perk.Id);
            }

            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                threats.Add(new LethalThreat(player.Id, player.Name, id, catalog.Perks.Get(id).Name));
            }
        }

        return threats;
    }

    /// <summary>
    /// Perfil del equipo para el informe de ojeo (RF-012b): con qué te vas a encontrar, en cifras y sin
    /// abrir las diez fichas. Determinista y ordenado (RT-041): a igualdad de frecuencia manda el nombre,
    /// no el orden en que aparecieron.
    /// </summary>
    public static TeamProfile Profile(TeamSetup team, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(catalog);

        int min = int.MaxValue;
        int max = 0;
        int total = 0;
        var styles = new Dictionary<StyleTag, int>();
        var traits = new Dictionary<Trait, int>();
        var perks = new Dictionary<string, int>();

        foreach (var player in team.Players)
        {
            min = Math.Min(min, player.Level);
            max = Math.Max(max, player.Level);
            total += player.Level;
            styles[player.StyleTag] = styles.TryGetValue(player.StyleTag, out int s) ? s + 1 : 1;

            for (int i = 0; i < player.Traits.Count; i++)
            {
                var trait = player.Traits[i];
                traits[trait] = traits.TryGetValue(trait, out int t) ? t + 1 : 1;
            }

            for (int i = 0; i < player.Perks.Count; i++)
            {
                string id = player.Perks[i];
                perks[id] = perks.TryGetValue(id, out int p) ? p + 1 : 1;
            }
        }

        int count = team.Players.Count;
        return new TeamProfile(
            team.Race,
            count,
            count == 0 ? 0 : min,
            max,
            count == 0 ? 0 : total / count,
            Ranked(styles, static (a, b) => a.CompareTo(b)),
            Ranked(traits, static (a, b) => a.CompareTo(b)),
            Ranked(perks, static (a, b) => string.CompareOrdinal(a, b)));
    }

    /// <summary>Cuentas ordenadas por frecuencia descendente y, a igualdad, por el criterio del tipo (RT-041).</summary>
    private static IReadOnlyList<(T Key, int Count)> Ranked<T>(Dictionary<T, int> counts, Comparison<T> tie)
        where T : notnull
    {
        var ranked = new List<(T Key, int Count)>(counts.Count);
        foreach (var (key, value) in counts)
        {
            ranked.Add((key, value));
        }

        ranked.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : tie(a.Key, b.Key));
        return ranked;
    }
}
