using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Marcaje con asignación estable (ADR 0022, fase1b-diseno.md §2.3). Hasta la fase 1 <c>MarkOpponent</c>
/// tomaba al rival más cercano en cada decisión, así que un defensa podía cambiar de objetivo cada dos
/// ticks y no marcaba a nadie. Aquí el emparejamiento defensor → atacante se calcula <b>una vez por
/// posesión</b> y se mantiene mientras siga siendo válido; solo se rehace el emparejamiento de los
/// defensores cuyo objetivo ha dejado de estar en el campo.
///
/// <para>Orden de resolución fijo (RT-041, RT-097): los defensores se recorren por id ascendente —el
/// array del motor ya está ordenado así— y cada uno se queda con el rival libre de menor coste, con
/// empate resuelto por id de rival ascendente. Nada depende del orden de un diccionario ni del tick.</para>
/// </summary>
internal static class Marking
{
    /// <summary>
    /// Preferencia por rol, en casillas de descuento sobre el coste: un defensa prefiere emparejarse con
    /// un delantero aunque tenga un centrocampista algo más cerca. Es la "preferencia por rol" de §2.3.
    /// </summary>
    private const float RolePreferenceCells = 2.0f;

    /// <summary>
    /// Actualiza las asignaciones de los dos equipos. Con <paramref name="force"/> se descartan las
    /// asignaciones vigentes y se rehacen todas (cambio de posesión); sin él solo se rellenan los huecos:
    /// defensores sin objetivo o con un objetivo que ya no está en el campo.
    /// </summary>
    public static void Assign(MatchPlayer[] players, bool[] taken, bool force)
    {
        AssignTeam(players, taken, 0, force);
        AssignTeam(players, taken, 1, force);
    }

    /// <summary>True si el objetivo sigue siendo marcable por un jugador del equipo indicado.</summary>
    public static bool IsValidTarget(MatchPlayer? target, int markerTeam) =>
        target is not null && target.OnPitch && target.IsOutfield && target.Team != markerTeam;

    private static void AssignTeam(MatchPlayer[] players, bool[] taken, int team, bool force)
    {
        for (int i = 0; i < taken.Length; i++)
        {
            taken[i] = false;
        }

        // Primera pasada: conservar lo que sigue valiendo y reservar esos rivales.
        for (int i = 0; i < players.Length; i++)
        {
            var marker = players[i];
            if (marker.Team != team || !marker.IsOutfield)
            {
                continue;
            }

            if (force || !IsValidTarget(marker.MarkTarget, team))
            {
                marker.MarkTarget = null;
                continue;
            }

            taken[marker.MarkTarget!.Index] = true;
        }

        // Segunda pasada: repartir los huecos. Primero solo entre rivales libres; si no queda ninguno
        // (más marcadores que rivales en campo), se permite doblar el marcaje sobre el mejor candidato.
        for (int i = 0; i < players.Length; i++)
        {
            var marker = players[i];
            if (marker.Team != team || !marker.IsOutfield || !marker.OnPitch || marker.MarkTarget is not null)
            {
                continue;
            }

            var chosen = Best(players, taken, marker, freeOnly: true) ?? Best(players, taken, marker, freeOnly: false);
            if (chosen is null)
            {
                continue;
            }

            marker.MarkTarget = chosen;
            taken[chosen.Index] = true;
        }
    }

    private static MatchPlayer? Best(MatchPlayer[] players, bool[] taken, MatchPlayer marker, bool freeOnly)
    {
        MatchPlayer? best = null;
        float bestCost = 0f;

        for (int i = 0; i < players.Length; i++)
        {
            var candidate = players[i];
            if (candidate.Team == marker.Team || !candidate.OnPitch || !candidate.IsOutfield)
            {
                continue;
            }

            if (freeOnly && taken[candidate.Index])
            {
                continue;
            }

            float cost = Vec2.Distance(marker.Position, candidate.Position);
            if (Prefers(marker.Role, candidate.Role))
            {
                cost -= RolePreferenceCells;
            }

            if (best is null || cost < bestCost)
            {
                best = candidate;
                bestCost = cost;
            }
        }

        return best;
    }

    /// <summary>Emparejamiento preferente por rol: defensa↔delantero, centrocampista↔centrocampista.</summary>
    private static bool Prefers(Position marker, Position target) => marker switch
    {
        Position.Defender => target == Position.Forward,
        Position.Midfielder => target == Position.Midfielder,
        Position.Forward => target == Position.Defender,
        _ => false,
    };
}
