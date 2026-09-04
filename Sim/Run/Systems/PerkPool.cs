using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Pool de perks del mercado y de las recompensas (RF-071, RF-114). Reutiliza
/// <c>Sim.Generation.PerkAssignment.Eligible</c> (mismo filtro por raza -ADR 0023-, posición y etiquetas
/// que usan los perks iniciales de la plantilla) para no reinventar la regla de elegibilidad.
/// </summary>
public static class PerkPool
{
    /// <summary>
    /// Perks que pueden ofrecerse en esta run: los del pool de la raza del club (ADR 0023, sin la
    /// habilidad racial, que no ocupa slot) que además tienen al menos un portador posible en la
    /// plantilla actual. Orden de id ordinal ascendente.
    /// </summary>
    public static IReadOnlyList<PerkDefinition> Offerable(RunState state, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);

        var offerable = new List<PerkDefinition>();
        foreach (var perk in catalog.Perks.AvailableTo(state.ClubRace))
        {
            if (IsRaceAbility(perk, state, catalog))
            {
                continue;
            }

            if (EligibleCarriers(state, perk, catalog).Count > 0)
            {
                offerable.Add(perk);
            }
        }

        return offerable;
    }

    /// <summary>
    /// Jugadores de la plantilla que pueden recibir <paramref name="perk"/> ahora mismo: no está muerto,
    /// cumple posición y etiquetas requeridas/prohibidas (mismo filtro que
    /// <c>PerkAssignment.Eligible</c>), tiene un slot libre para su rareza (RF-023) y no lo lleva ya.
    /// Orden de id ascendente (RT-041).
    /// </summary>
    public static IReadOnlyList<int> EligibleCarriers(RunState state, PerkDefinition perk, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);

        var carriers = new List<int>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i];
            if (player.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            if (player.Perks.Count >= ProgressionRules.PerkSlots(player.Rarity))
            {
                continue;
            }

            if (player.Perks.Contains(perk.Id))
            {
                continue;
            }

            var definition = player.ToDefinition(catalog, applyMinorInjuryPenalty: false);
            if (!PerkAssignment.Eligible(definition, catalog).Any(p => string.Equals(p.Id, perk.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            carriers.Add(player.Id);
        }

        return carriers;
    }

    /// <summary>Añade el perk indicado al jugador indicado, ordenado ordinalmente (mismo orden que <c>PerkAssignment</c>).</summary>
    public static RunPlayer WithPerk(RunPlayer player, string perkId)
    {
        ArgumentNullException.ThrowIfNull(player);
        var perks = new List<string>(player.Perks) { perkId };
        perks.Sort(StringComparer.Ordinal);
        return player with { Perks = perks };
    }

    private static bool IsRaceAbility(PerkDefinition perk, RunState state, Catalog catalog) =>
        string.Equals(perk.Id, catalog.Race(state.ClubRace).Ability, StringComparison.Ordinal);
}
