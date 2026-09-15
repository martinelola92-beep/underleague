using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Aplica el traspaso de atributos de "Herencia" (paquete BB, consumidor de
/// <see cref="InheritanceTable"/>): lo que era del muerto pasa, en parte, al compañero vinculado que
/// <c>MatchResolution</c> ya resolvió y dejó en <see cref="PlayerDeathDetail.LinkedPlayerId"/>.
///
/// <para>Vive fuera de <c>MatchResolution</c> a propósito: esa clase no conoce <see cref="EconomyConfig"/>
/// (la economía es del paquete X, la resolución del partido es del W), así que el traspaso se aplica
/// aquí, en <c>StandardRunSystems.AfterMatch</c>, sobre el <see cref="RunState"/> ya actualizado por el
/// partido. Esto también resuelve solo, sin lógica aparte, el caso "el vinculado también ha muerto en
/// el mismo partido": si ha muerto antes o después de él dentro del mismo evento, para cuando esto se
/// ejecuta ya está en <see cref="PhysicalState.Dead"/> en el <c>state</c> final, y se salta sin
/// redirigir a un tercero (decisión del paquete BB).</para>
/// </summary>
public static class InheritanceSystem
{
    /// <summary>Aplica los traspasos de todas las muertes de este partido; no hace nada si no hay ninguno.</summary>
    public static RunState Apply(RunState state, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);

        if (summary.DeathDetails.Count == 0 || economy.Inheritance.Count == 0)
        {
            return state;
        }

        var roster = new List<RunPlayer>(state.Roster);
        bool changed = false;
        for (int i = 0; i < summary.DeathDetails.Count; i++)
        {
            var detail = summary.DeathDetails[i];
            if (detail.LinkedPlayerId < 0)
            {
                continue;
            }

            var dead = state.FindPlayer(detail.PlayerId);
            if (dead is null)
            {
                continue;
            }

            int recipientIndex = IndexOf(roster, detail.LinkedPlayerId);
            if (recipientIndex < 0 || roster[recipientIndex].PhysicalState == PhysicalState.Dead)
            {
                // El vinculado también ha muerto (en este partido o antes): sin heredero vivo no hay
                // traspaso. No se redirige a un tercero.
                continue;
            }

            var perks = detail.Perks;
            for (int p = 0; p < perks.Count; p++)
            {
                var rate = economy.Inheritance.RateFor(perks[p]);
                if (rate is null)
                {
                    continue;
                }

                var recipient = roster[recipientIndex];
                var a = recipient.Attributes;
                var d = dead.Attributes;
                var boosted = Attributes.Clamp(new Attributes(
                    a.Strength + Transfer(d.Strength, rate),
                    a.Speed + Transfer(d.Speed, rate),
                    a.Technique + Transfer(d.Technique, rate),
                    a.Stamina + Transfer(d.Stamina, rate),
                    a.Leash + Transfer(d.Leash, rate)));
                roster[recipientIndex] = recipient with { Attributes = boosted };
                changed = true;
            }
        }

        return changed ? state.WithRoster(roster) : state;
    }

    /// <summary>
    /// Puntos que aporta un único atributo del muerto: el porcentaje declarado de SU valor, con el tope
    /// absoluto por atributo (RT-023, los dos límites de <see cref="InheritanceRate"/> a la vez).
    /// </summary>
    private static int Transfer(int deadValue, InheritanceRate rate) =>
        Math.Min(deadValue * rate.Percent / 100, rate.MaxPerAttribute);

    private static int IndexOf(List<RunPlayer> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
