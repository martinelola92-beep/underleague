using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Consultas puras sobre un <see cref="RunState"/> para las pantallas de resumen: el mapa, la de fin de
/// run (victoria o derrota con su causa, actos superados, plantilla final y caídos) y el memorial de
/// RF-122 cuando llegue.
///
/// <para>Existe por RT-014: nada de lo que se pinta lo calcula la escena. Recorrer el historial de nodos
/// para contar cuántos jefes se han batido es derivar estado de la run, no dibujar, así que se deriva
/// aquí —en <c>/Sim</c>, puro, entero y sin E/S— y la pantalla solo lo lee. Es el mismo criterio con el
/// que <c>Sim.Placement.PlacementView</c> resolvió la pantalla de Equipo.</para>
/// </summary>
public static class RunSummary
{
    /// <summary>Nodos que el jugador ha completado en toda la run, de cualquier tipo.</summary>
    public static int NodesVisited(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.NodeHistory.Count;
    }

    /// <summary>Partidos jugados (liga, élite y jefe), ganados o perdidos.</summary>
    public static int MatchesPlayed(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int count = 0;
        for (int i = 0; i < state.NodeHistory.Count; i++)
        {
            if (NodeKinds.IsMatch(state.NodeHistory[i].Kind))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Partidos ganados.</summary>
    public static int MatchesWon(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int count = 0;
        for (int i = 0; i < state.NodeHistory.Count; i++)
        {
            if (state.NodeHistory[i].Result == NodeResult.Won)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Jefes batidos (RF-001): cada uno cierra un acto, así que es también <b>cuántos actos se han
    /// superado</b>. Se cuenta del historial y no del acto actual porque perder contra el jefe del acto 3
    /// deja la run en el acto 3 con dos actos superados, y el número que el jugador quiere leer al final
    /// es ese.
    /// </summary>
    public static int ActsCleared(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int count = 0;
        for (int i = 0; i < state.NodeHistory.Count; i++)
        {
            var entry = state.NodeHistory[i];
            if (entry.Kind == NodeKind.Boss && entry.Result == NodeResult.Won)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Los caídos (RF-093, RF-122), por id ascendente. Siguen en <see cref="RunState.Roster"/> —morir
    /// cuesta el jugador, no su hueco— y son la mitad del resumen que el jugador se lleva de la run.
    /// </summary>
    public static IReadOnlyList<RunPlayer> Fallen(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var fallen = new List<RunPlayer>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState == PhysicalState.Dead)
            {
                fallen.Add(state.Roster[i]);
            }
        }

        return fallen;
    }

    /// <summary>
    /// La plantilla que sobrevive, por id ascendente: todos menos los muertos. Es la cifra de
    /// <see cref="RunState.RosterSize"/>, con los jugadores dentro.
    /// </summary>
    public static IReadOnlyList<RunPlayer> Survivors(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var alive = new List<RunPlayer>();
        for (int i = 0; i < state.Roster.Count; i++)
        {
            if (state.Roster[i].PhysicalState != PhysicalState.Dead)
            {
                alive.Add(state.Roster[i]);
            }
        }

        return alive;
    }

    /// <summary>
    /// Saltos hasta el mercado más cercano <b>siguiendo aristas hacia delante</b> desde ese nodo, o -1 si
    /// no hay ninguno alcanzable (RF-011b). 0 si el propio nodo es un mercado.
    ///
    /// <para>Es lo que el mapa necesita para destacar los mercados: RF-011b garantiza uno a dos saltos
    /// desde cualquier punto, y esa garantía solo es una decisión si el jugador la ve. Envuelve
    /// <see cref="MapInvariants.HopsTo"/> para que la pantalla no tenga que traducir
    /// <see cref="int.MaxValue"/>.</para>
    /// </summary>
    public static int HopsToMarket(RunState state, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var node = state.GetNode(nodeId);
        int hops = MapInvariants.HopsTo(state.MapOf(node.Act), nodeId, NodeKind.Market);
        return hops == int.MaxValue ? -1 : hops;
    }
}
