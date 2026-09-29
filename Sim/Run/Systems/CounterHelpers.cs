namespace Underleague.Sim.Run.Systems;

/// <summary>Azúcar sobre <see cref="RunPlayer.Counters"/> para leer o fijar un único contador (W-11).</summary>
internal static class CounterHelpers
{
    public static int Counter(this RunPlayer player, string name) =>
        player.Counters.TryGetValue(name, out int value) ? value : 0;

    public static RunPlayer WithCounter(this RunPlayer player, string name, int value)
    {
        var merged = new Dictionary<string, int>(player.Counters, StringComparer.Ordinal) { [name] = value };
        return player.WithCounters(merged);
    }
}

/// <summary>
/// Comprobaciones de nodo compartidas por los sistemas de decisión (mercado, clínica, recompensas): una
/// decisión solo es válida si el nodo pendiente es del tipo que la resuelve.
/// </summary>
internal static class NodeGuards
{
    public static MapNode RequireOpen(RunState state, NodeKind kind, string action)
    {
        if (state.PendingNodeId < 0)
        {
            throw new InvalidOperationException($"no hay ningún nodo abierto: no se puede {action}");
        }

        var node = state.GetNode(state.PendingNodeId);
        if (node.Kind != kind)
        {
            throw new InvalidOperationException($"el nodo abierto es {node.Kind}, no {kind}: no se puede {action}");
        }

        return node;
    }

    /// <summary>Si el nodo abierto ya tuvo su única elección (<see cref="RunState.NodeResolvedCounter"/>).</summary>
    public static bool IsResolved(RunState state, MapNode node) =>
        state.Counter(RunState.NodeResolvedCounter) == node.Id + 1;

    /// <summary>
    /// Lanza si el nodo (evento o entrenamiento) ya se resolvió: una carta se elige <b>una vez</b>
    /// (ADR 0159, ADR 0160). Elegir una opción es resolver el nodo; solo queda salir con <c>LeaveNode</c>.
    /// </summary>
    public static void RequireUnresolved(RunState state, MapNode node, string action)
    {
        if (IsResolved(state, node))
        {
            throw new InvalidOperationException(
                $"el nodo {node.Id} ({node.Kind}) ya se resolvió: no se puede {action} otra vez, solo salir al mapa");
        }
    }

    /// <summary>Marca el nodo abierto como resuelto; <c>LeaveNode</c> la borra.</summary>
    public static RunState MarkResolved(RunState state, MapNode node) =>
        state.WithCounter(RunState.NodeResolvedCounter, node.Id + 1);

    public static MapNode RequireOpenMatch(RunState state, string action)
    {
        if (state.PendingNodeId < 0)
        {
            throw new InvalidOperationException($"no hay ningún nodo abierto: no se puede {action}");
        }

        var node = state.GetNode(state.PendingNodeId);
        if (!node.IsMatch)
        {
            throw new InvalidOperationException($"el nodo abierto es {node.Kind}, no un partido: no se puede {action}");
        }

        return node;
    }
}
