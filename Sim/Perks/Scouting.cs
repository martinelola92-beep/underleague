using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Un perk rival capaz de matar (RF-013): quién lo lleva, cuál es y cómo se llama en el idioma del
/// jugador. Es lo que el informe de ojeo tiene que destacar antes de jugar (RF-012b, RF-012d).
/// </summary>
public sealed record LethalThreat(int PlayerId, string PlayerName, string PerkId, LocalizedName PerkName);

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
}
