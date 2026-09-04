using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>
/// Reparto de los perks <b>iniciales</b> de una plantilla (RF-023: la rareza es el punto de partida y el
/// techo de perks). Puro y determinista: recibe jugadores, catálogo y un flujo de RNG explícito y
/// devuelve jugadores nuevos; sin E/S, sin reloj, sin generador estático (RT-012, RT-021).
/// <para>
/// <b>Provisional, y a propósito.</b> La vía normal de conseguir un perk es la recompensa por partido
/// ganado (RF-071) y el mercado (RF-114e), las dos de fase 2. Hasta que exista el bucle de run, las
/// pantallas de fase 1 necesitan plantillas con perks de verdad —con su descripción generada (RT-035)—
/// para poder enseñarlas, y esto es lo que se lo da. La elección es <b>uniforme</b> entre los perks
/// elegibles: no hay ninguna regla de reparto por rareza en <c>requisitos.md</c> y no se inventa una
/// aquí. <see cref="Generation.TeamGenerator"/> no lo llama: quien quiera perks iniciales, los pide.
/// </para>
/// </summary>
public static class PerkAssignment
{
    /// <summary>
    /// Copia de <paramref name="players"/> con los perks iniciales de su rareza asignados
    /// (<c>Progression.InitialPerks</c>). Recorre a los jugadores por id ascendente (RT-041) para que el
    /// resultado dependa solo de la semilla, no del orden de la lista de entrada.
    /// </summary>
    public static IReadOnlyList<PlayerDefinition> AssignInitial(
        ref Pcg32 rng,
        IReadOnlyList<PlayerDefinition> players,
        Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(catalog);

        var order = new List<PlayerDefinition>(players);
        order.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        var assigned = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var player in order)
        {
            int wanted = Progression.Progression.InitialPerks(player.Rarity);
            if (wanted <= 0)
            {
                continue;
            }

            var candidates = Eligible(player, catalog);
            var picked = new List<string>();
            for (int i = 0; i < wanted && candidates.Count > 0; i++)
            {
                int index = rng.Range(0, candidates.Count);
                picked.Add(candidates[index].Id);
                candidates.RemoveAt(index);
            }

            picked.Sort(StringComparer.Ordinal);
            assigned[player.Id] = picked;
        }

        var result = new List<PlayerDefinition>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            result.Add(assigned.TryGetValue(players[i].Id, out var perks)
                ? players[i] with { Perks = perks }
                : players[i]);
        }

        return result;
    }

    /// <summary>
    /// Perks que ese jugador puede llevar: los del pool de su raza (ADR 0023) que además cumplen su
    /// posición y sus etiquetas, quitando su habilidad racial (RF-031b: la lleva de oficio y no ocupa
    /// slot). Orden de id ordinal ascendente, el de <see cref="PerkCatalog.All"/>.
    /// </summary>
    public static List<PerkDefinition> Eligible(PlayerDefinition player, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);

        string ability = catalog.Race(player.Race).Ability;
        var eligible = new List<PerkDefinition>();
        foreach (var perk in catalog.Perks.AvailableTo(player.Race))
        {
            if (string.Equals(perk.Id, ability, StringComparison.Ordinal))
            {
                continue;
            }

            if (perk.PositionOnly is { } position && position != player.Position)
            {
                continue;
            }

            if (!HasAll(player, perk.TagsRequired) || HasAny(player, perk.TagsForbidden))
            {
                continue;
            }

            eligible.Add(perk);
        }

        return eligible;
    }

    private static bool HasAll(PlayerDefinition player, IReadOnlyList<string> tags)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (!player.HasTag(tags[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAny(PlayerDefinition player, IReadOnlyList<string> tags)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (player.HasTag(tags[i]))
            {
                return true;
            }
        }

        return false;
    }
}
