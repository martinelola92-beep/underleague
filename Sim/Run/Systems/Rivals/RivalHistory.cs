namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>
/// Un encuentro contra un rival, leído de <see cref="RunState.NodeHistory"/> (RF-122, ADR 0124):
/// contra quién, en qué nodo, de qué tipo, y con qué resultado se cerró.
/// </summary>
public sealed record RivalEncounter(string RivalId, int NodeId, NodeKind Kind, NodeResult Result);

/// <summary>
/// Historial de <b>enfrentamientos</b> contra rivales, derivado de <see cref="RunState.NodeHistory"/>
/// (ADR 0124, la tabla "Dónde vive cada memoria"): contra qué rival, en qué nodo, de qué tipo y con qué
/// resultado -nada por debajo del nivel de partido. <b>No es</b> una atribución "quién knaveó a quién":
/// <see cref="RivalEncounter"/> no lleva ningún id de jugador, así que esta clase no puede decir qué
/// jugador propio hizo qué contra qué jugador rival -esa memoria vive, cuando existe, en
/// <see cref="RunPlayer.Career"/> (vocabulario cerrado, por jugador PROPIO) o en el detalle de partido de
/// <see cref="MatchResolution"/>. Un rival es vocabulario <b>abierto</b> (pares rival×nodo), así que este
/// historial no vive en un registro tipado como <c>RunPlayer.Career</c> sino que se <b>deriva</b> del
/// historial de nodos que ya existe -sin tocar el esquema de guardado ni subir su versión.
///
/// <para>Clase pura de solo lectura: sin estado, sin RNG, sin E/S. <see cref="RunState.NodeHistory"/> ya
/// está en orden cronológico (se añade una entrada por <c>RunState.WithNodeCompleted</c> a medida que se
/// completan los nodos), así que estos métodos se limitan a recorrerlo y filtrar.</para>
///
/// <para><b>Por qué se excluye <see cref="NodeKind.Boss"/></b>: el nodo de jefe guarda un
/// <c>opponentId</c> fantasma que ningún sistema usa hoy (el jefe no es un <see cref="RivalTeam"/> del
/// catálogo de rivales). Incluirlo mentiría sobre "cuántas veces se ha visto a este rival".</para>
/// </summary>
public static class RivalHistory
{
    /// <summary>
    /// Todos los encuentros contra rivales de la run, en el mismo orden cronológico que
    /// <see cref="RunState.NodeHistory"/>.
    /// </summary>
    public static IReadOnlyList<RivalEncounter> Encounters(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var history = state.NodeHistory;
        var encounters = new List<RivalEncounter>();
        for (int i = 0; i < history.Count; i++)
        {
            var entry = history[i];
            if (!NodeKinds.IsMatch(entry.Kind) || entry.Kind == NodeKind.Boss)
            {
                continue;
            }

            var node = state.FindNode(entry.NodeId);
            if (node is null || node.OpponentId.Length == 0)
            {
                continue;
            }

            encounters.Add(new RivalEncounter(node.OpponentId, entry.NodeId, entry.Kind, entry.Result));
        }

        return encounters;
    }

    /// <summary>Encuentros contra ese rival concreto, en orden cronológico.</summary>
    public static IReadOnlyList<RivalEncounter> Against(RunState state, string rivalId)
    {
        ArgumentException.ThrowIfNullOrEmpty(rivalId);

        var all = Encounters(state);
        var matches = new List<RivalEncounter>();
        for (int i = 0; i < all.Count; i++)
        {
            if (string.Equals(all[i].RivalId, rivalId, StringComparison.Ordinal))
            {
                matches.Add(all[i]);
            }
        }

        return matches;
    }

    /// <summary>True si la run se ha enfrentado a ese rival al menos una vez.</summary>
    public static bool HasFaced(RunState state, string rivalId) => Against(state, rivalId).Count > 0;
}
