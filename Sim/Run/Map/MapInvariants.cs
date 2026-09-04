namespace Underleague.Sim.Run;

/// <summary>
/// Comprobación de los invariantes duros de un mapa de acto (RF-003b, RF-010, RF-011b). Es público a
/// propósito: la garantía de RF-011b no se comprueba solo en un test, la comprueba quien quiera
/// (<c>/Balance</c>, el modo de depuración, un mapa construido a mano por el paquete X).
///
/// <para>Devuelve una lista de violaciones en texto en vez de lanzar, para que un fallo diga <b>todo</b>
/// lo que está mal del mapa y no solo lo primero.</para>
/// </summary>
public static class MapInvariants
{
    /// <summary>Lista vacía si el mapa cumple todos los invariantes; una línea por violación si no.</summary>
    public static IReadOnlyList<string> Violations(ActMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var problems = new List<string>();

        CheckShape(map, problems);
        CheckMatchShare(map, problems);
        CheckEdges(map, problems);
        CheckReachability(map, problems);
        CheckMarketGuarantee(map, problems);
        CheckMarketSpacing(map, problems);

        return problems;
    }

    /// <summary>Lanza <see cref="InvalidOperationException"/> con todas las violaciones si el mapa incumple algún invariante.</summary>
    public static void Check(ActMap map)
    {
        var problems = Violations(map);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"el mapa del acto {map.Act} incumple {problems.Count} invariante(s): {string.Join("; ", problems)}");
        }
    }

    /// <summary>Capas del mapa, que es el número de nodos que recorre el jugador en el acto (RF-001).</summary>
    public static int PathLength(ActMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int layers = 0;
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            layers = Math.Max(layers, map.Nodes[i].Layer + 1);
        }

        return layers;
    }

    /// <summary>
    /// Partidos que jugaría el <b>peor camino</b>: capas que contienen algún nodo de partido. Es la
    /// cifra que limita RF-003b, y se calcula por capas y no por nodos porque el jugador solo recorre
    /// una capa cada vez.
    /// </summary>
    public static int WorstCaseMatches(ActMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var matchLayers = new HashSet<int>();
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            if (map.Nodes[i].IsMatch)
            {
                matchLayers.Add(map.Nodes[i].Layer);
            }
        }

        return matchLayers.Count;
    }

    /// <summary>
    /// Saltos mínimos desde <paramref name="fromNodeId"/> hasta un nodo de tipo <paramref name="kind"/>,
    /// siguiendo aristas hacia delante. 0 si el propio nodo es de ese tipo;
    /// <see cref="int.MaxValue"/> si no hay ninguno alcanzable.
    /// </summary>
    public static int HopsTo(ActMap map, int fromNodeId, NodeKind kind)
    {
        ArgumentNullException.ThrowIfNull(map);
        var start = map.Get(fromNodeId);
        if (start.Kind == kind)
        {
            return 0;
        }

        var frontier = new List<MapNode> { start };
        var seen = new HashSet<int> { start.Id };
        int hops = 0;
        while (frontier.Count > 0)
        {
            hops++;
            var next = new List<MapNode>();
            for (int i = 0; i < frontier.Count; i++)
            {
                var edges = frontier[i].Next;
                for (int e = 0; e < edges.Count; e++)
                {
                    var node = map.Get(edges[e]);
                    if (node.Kind == kind)
                    {
                        return hops;
                    }

                    if (seen.Add(node.Id))
                    {
                        next.Add(node);
                    }
                }
            }

            frontier = next;
        }

        return int.MaxValue;
    }

    private static void CheckShape(ActMap map, List<string> problems)
    {
        int pathLength = PathLength(map);
        if (pathLength < MapGenerator.MinPathLength || pathLength > MapGenerator.MaxPathLength)
        {
            problems.Add(
                $"{pathLength} nodos por camino, fuera de {MapGenerator.MinPathLength}..{MapGenerator.MaxPathLength} (RF-001)");
        }

        var ids = new HashSet<int>();
        var populated = new HashSet<int>();
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            populated.Add(node.Layer);
            if (!ids.Add(node.Id))
            {
                problems.Add($"id de nodo repetido: {node.Id}");
            }

            if (node.Act != map.Act)
            {
                problems.Add($"el nodo {node.Id} dice ser del acto {node.Act} y está en el mapa del acto {map.Act}");
            }

            if (i > 0 && map.Nodes[i - 1].Id >= node.Id)
            {
                problems.Add($"los nodos no están ordenados por id ascendente: {map.Nodes[i - 1].Id} antes de {node.Id}");
            }

            if (node.Kind == NodeKind.Workshop)
            {
                problems.Add($"el nodo {node.Id} es un taller: el taller de implantes es de fase 3 (fase2-diseno.md §4)");
            }
        }

        for (int layer = 0; layer < pathLength; layer++)
        {
            if (!populated.Contains(layer))
            {
                problems.Add($"la capa {layer} está vacía: el camino se corta");
            }
        }

        int bosses = 0;
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            if (node.Kind != NodeKind.Boss)
            {
                continue;
            }

            bosses++;
            if (node.Next.Count != 0)
            {
                problems.Add($"el nodo de jefe {node.Id} tiene aristas de salida (RF-010: el acto termina en él)");
            }

            if (node.Id != map.BossNodeId)
            {
                problems.Add($"el nodo de jefe {node.Id} no coincide con ActMap.BossNodeId = {map.BossNodeId}");
            }

            if (node.Layer != pathLength - 1)
            {
                problems.Add($"el jefe está en la capa {node.Layer} y el acto tiene {pathLength} (RF-001: el acto termina en el jefe)");
            }
        }

        if (bosses != 1)
        {
            problems.Add($"{bosses} nodos de jefe; debe haber exactamente 1 (RF-001)");
        }

        if (map.EntryNodeIds.Count == 0)
        {
            problems.Add("el mapa no tiene nodos de entrada");
        }
    }

    private static void CheckMatchShare(ActMap map, List<string> problems)
    {
        int pathLength = PathLength(map);
        int matches = WorstCaseMatches(map);
        int limit = pathLength * MapGenerator.MaxMatchPercent / 100;
        if (matches > limit)
        {
            problems.Add(
                $"el peor camino juega {matches} partidos de {pathLength} nodos; el máximo es {limit} "
                    + $"(RF-003b, {MapGenerator.MaxMatchPercent}%)");
        }
    }

    private static void CheckEdges(ActMap map, List<string> problems)
    {
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            if (node.Kind != NodeKind.Boss && node.Next.Count == 0)
            {
                problems.Add($"el nodo {node.Id} no tiene salida y no es el jefe");
            }

            for (int e = 0; e < node.Next.Count; e++)
            {
                var target = map.Find(node.Next[e]);
                if (target is null)
                {
                    problems.Add($"el nodo {node.Id} apunta a {node.Next[e]}, que no existe");
                    continue;
                }

                if (target.Layer != node.Layer + 1)
                {
                    problems.Add(
                        $"la arista {node.Id} -> {target.Id} va de la capa {node.Layer} a la {target.Layer}; "
                            + "solo se admite capa + 1 (RF-010: dirigido y sin retroceso)");
                }

                if (e > 0 && node.Next[e - 1] >= node.Next[e])
                {
                    problems.Add($"las salidas del nodo {node.Id} no están ordenadas ascendentemente");
                }
            }
        }

        CheckNoCrossings(map, problems);
    }

    /// <summary>
    /// Dos aristas se cruzan cuando la fuente de arriba llega más abajo que la fuente de abajo. Con
    /// intervalos ordenados basta comprobar, capa a capa, que el destino máximo de un nodo no supera al
    /// destino mínimo del siguiente nodo de su capa.
    /// </summary>
    private static void CheckNoCrossings(ActMap map, List<string> problems)
    {
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            if (node.Next.Count == 0)
            {
                continue;
            }

            MapNode? below = null;
            for (int j = 0; j < map.Nodes.Count; j++)
            {
                if (map.Nodes[j].Layer == node.Layer && map.Nodes[j].IndexInLayer == node.IndexInLayer + 1)
                {
                    below = map.Nodes[j];
                    break;
                }
            }

            if (below is null || below.Next.Count == 0)
            {
                continue;
            }

            int maxIndex = map.Get(node.Next[^1]).IndexInLayer;
            int minIndexBelow = map.Get(below.Next[0]).IndexInLayer;
            if (maxIndex > minIndexBelow)
            {
                problems.Add($"las aristas de los nodos {node.Id} y {below.Id} se cruzan");
            }
        }
    }

    private static void CheckReachability(ActMap map, List<string> problems)
    {
        var reachable = new HashSet<int>();
        var frontier = new List<int>(map.EntryNodeIds);
        for (int i = 0; i < frontier.Count; i++)
        {
            reachable.Add(frontier[i]);
        }

        while (frontier.Count > 0)
        {
            var next = new List<int>();
            for (int i = 0; i < frontier.Count; i++)
            {
                var node = map.Find(frontier[i]);
                if (node is null)
                {
                    continue;
                }

                for (int e = 0; e < node.Next.Count; e++)
                {
                    if (reachable.Add(node.Next[e]))
                    {
                        next.Add(node.Next[e]);
                    }
                }
            }

            frontier = next;
        }

        for (int i = 0; i < map.Nodes.Count; i++)
        {
            if (!reachable.Contains(map.Nodes[i].Id))
            {
                problems.Add($"el nodo {map.Nodes[i].Id} no es alcanzable desde la entrada del acto");
            }

            if (HopsTo(map, map.Nodes[i].Id, NodeKind.Boss) == int.MaxValue && map.Nodes[i].Kind != NodeKind.Boss)
            {
                problems.Add($"desde el nodo {map.Nodes[i].Id} no se llega al jefe");
            }
        }
    }

    /// <summary>
    /// RF-011b, garantía dura: desde cualquier nodo hay un mercado a dos saltos como máximo. La única
    /// excepción admisible es el final del acto, donde ya no queda sitio para un mercado: se acepta si
    /// desde ahí el jefe está a dos saltos o menos.
    /// </summary>
    private static void CheckMarketGuarantee(ActMap map, List<string> problems)
    {
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            int toMarket = HopsTo(map, node.Id, NodeKind.Market);
            if (toMarket <= MapGenerator.MaxHopsToMarket)
            {
                continue;
            }

            int toBoss = HopsTo(map, node.Id, NodeKind.Boss);
            if (toBoss > MapGenerator.MaxHopsToMarket)
            {
                problems.Add(
                    $"desde el nodo {node.Id} (capa {node.Layer}) el mercado más cercano está a "
                        + $"{Distance(toMarket)} saltos y el jefe a {Distance(toBoss)} (RF-011b)");
            }
        }
    }

    /// <summary>
    /// RF-011b, densidad: un mercado cada 3-4 nodos a lo largo de cualquier camino. Como todos los
    /// caminos visitan exactamente una capa de cada índice, basta mirar las capas de mercado.
    /// </summary>
    private static void CheckMarketSpacing(ActMap map, List<string> problems)
    {
        int pathLength = PathLength(map);
        var marketLayers = new List<int>();
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            if (map.Nodes[i].Kind == NodeKind.Market && !marketLayers.Contains(map.Nodes[i].Layer))
            {
                marketLayers.Add(map.Nodes[i].Layer);
            }
        }

        marketLayers.Sort();
        if (marketLayers.Count == 0)
        {
            problems.Add("el acto no tiene ningún mercado (RF-011b)");
            return;
        }

        // Una capa con mercado tiene que ser íntegramente de mercado: si no, el nodo que no lo es se
        // queda a tres saltos del siguiente mercado (ver la demostración en MapGenerator).
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            if (map.Nodes[i].Kind != NodeKind.Market && marketLayers.Contains(map.Nodes[i].Layer))
            {
                problems.Add($"la capa {map.Nodes[i].Layer} mezcla mercado con el nodo {map.Nodes[i].Id} ({map.Nodes[i].Kind})");
            }
        }

        if (marketLayers[0] > MapGenerator.MarketLayerSpacing)
        {
            problems.Add($"el primer mercado está en la capa {marketLayers[0]}: demasiado tarde (RF-011b)");
        }

        for (int i = 1; i < marketLayers.Count; i++)
        {
            int gap = marketLayers[i] - marketLayers[i - 1];
            if (gap is < 3 or > 4)
            {
                problems.Add($"entre los mercados de las capas {marketLayers[i - 1]} y {marketLayers[i]} hay {gap} nodos; deben ser 3 o 4 (RF-011b)");
            }
        }

        // Desde la capa siguiente al último mercado hay que llegar al jefe en dos saltos: si el jefe
        // queda a más de 3 capas del último mercado, ese nodo se queda sin mercado y sin jefe cerca.
        int gapToBoss = (pathLength - 1) - marketLayers[^1];
        if (gapToBoss > MapGenerator.MarketLayerSpacing)
        {
            problems.Add(
                $"desde el último mercado (capa {marketLayers[^1]}) hasta el jefe (capa {pathLength - 1}) hay {gapToBoss} nodos (RF-011b)");
        }
    }

    private static string Distance(int hops) => hops == int.MaxValue ? "infinitos" : hops.ToString();
}
