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
        CheckMarketLayout(map, problems);

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
    /// Cota superior de los partidos que jugaría el <b>peor camino</b>: número de capas que contienen
    /// algún nodo de partido. Es la cifra que limita RF-003b, y se calcula por capas y no por nodos
    /// porque el jugador solo recorre una capa cada vez: ningún camino puede jugar más partidos que
    /// capas con partido haya, se elija lo que se elija.
    ///
    /// <para>Con cuatro carriles (ADR 0053) una capa mezcla tipos, así que la cota puede quedar por
    /// encima del peor camino real —una capa con un solo partido en el carril 0 no se la come quien
    /// venga por el carril 3—. Se deja así a propósito: es conservadora, y es la que compara
    /// <see cref="CheckMatchShare"/>. El extremo exacto lo da <see cref="PathMatches"/>.</para>
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
    /// Partidos del camino que más (<paramref name="worst"/> = true) o que menos juega, exacto: recorre
    /// el grafo hacia atrás quedándose con el extremo. El mínimo es el que dice cuánto se puede
    /// <b>esquivar</b> el juego desviándose a mercados y servicios (RF-002d), y el máximo es el peor
    /// camino de verdad, que nunca supera la cota de <see cref="WorstCaseMatches"/>.
    /// </summary>
    public static int PathMatches(ActMap map, bool worst)
    {
        ArgumentNullException.ThrowIfNull(map);
        var best = new Dictionary<int, int>(map.Nodes.Count);
        for (int i = map.Nodes.Count - 1; i >= 0; i--)
        {
            var node = map.Nodes[i];
            int tail = 0;
            if (node.Next.Count > 0)
            {
                tail = worst ? int.MinValue : int.MaxValue;
                for (int e = 0; e < node.Next.Count; e++)
                {
                    int value = best[node.Next[e]];
                    tail = worst ? Math.Max(tail, value) : Math.Min(tail, value);
                }
            }

            best[node.Id] = (node.IsMatch ? 1 : 0) + tail;
        }

        int result = worst ? int.MinValue : int.MaxValue;
        for (int i = 0; i < map.EntryNodeIds.Count; i++)
        {
            int value = best[map.EntryNodeIds[i]];
            result = worst ? Math.Max(result, value) : Math.Min(result, value);
        }

        return result;
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

        if (map.EntryNodeIds.Count != 1)
        {
            problems.Add(
                $"el mapa tiene {map.EntryNodeIds.Count} nodos de entrada; el acto empieza en uno solo (ADR 0053)");
        }

        CheckLanes(map, problems);
    }

    /// <summary>
    /// Carriles (ADR 0053): cuatro, numerados de arriba abajo. Una capa ocupa un <b>intervalo contiguo</b>
    /// de carriles —con huecos interiores, un carril podría quedarse sin vecino a distancia 1 y el grafo
    /// se partiría— y la apertura del acto es 1 → 2 → 4.
    /// </summary>
    private static void CheckLanes(ActMap map, List<string> problems)
    {
        var lanesByLayer = new Dictionary<int, List<int>>();
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            if (node.IndexInLayer < 0 || node.IndexInLayer >= MapGenerator.Lanes)
            {
                problems.Add($"el nodo {node.Id} está en el carril {node.IndexInLayer}, fuera de 0..{MapGenerator.Lanes - 1}");
            }

            if (!lanesByLayer.TryGetValue(node.Layer, out var lanes))
            {
                lanes = new List<int>();
                lanesByLayer[node.Layer] = lanes;
            }

            lanes.Add(node.IndexInLayer);
        }

        var layers = new List<int>(lanesByLayer.Keys);
        layers.Sort();
        for (int l = 0; l < layers.Count; l++)
        {
            var lanes = lanesByLayer[layers[l]];
            lanes.Sort();
            for (int i = 1; i < lanes.Count; i++)
            {
                if (lanes[i] != lanes[i - 1] + 1)
                {
                    problems.Add(
                        $"la capa {layers[l]} ocupa los carriles [{string.Join(",", lanes)}]: tienen que ser contiguos");
                    break;
                }
            }
        }

        if (lanesByLayer.TryGetValue(0, out var first) && first.Count != 1)
        {
            problems.Add($"la capa 0 tiene {first.Count} nodos; el acto empieza en uno solo (ADR 0053)");
        }

        if (lanesByLayer.TryGetValue(1, out var second) && second.Count != 2)
        {
            problems.Add($"la capa 1 tiene {second.Count} nodos; la apertura del acto es 1 -> 2 -> 4 (ADR 0053)");
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

        CheckLaneContiguity(map, problems);
    }

    /// <summary>
    /// Movimiento solo a carriles contiguos (ADR 0053): desde el carril <c>i</c> se va a <c>i-1</c>,
    /// <c>i</c> o <c>i+1</c>. Es lo que le da memoria a la elección de ruta. Dos excepciones, las dos de
    /// la propia ADR: la <b>apertura</b> del acto (capas 0 y 1, donde 1 → 2 → 4 es total) y el
    /// <b>jefe</b>, en el que convergen todos los caminos.
    ///
    /// <para>Las aristas <b>sí pueden cruzarse</b> entre carriles vecinos, y eso ya no es un defecto:
    /// es lo que permite reconverger. La decisión W-4 (sin cruces) queda revisada por la ADR 0053.</para>
    /// </summary>
    private static void CheckLaneContiguity(ActMap map, List<string> problems)
    {
        for (int i = 0; i < map.Nodes.Count; i++)
        {
            var node = map.Nodes[i];
            if (node.Layer < MapGenerator.OpeningLayers)
            {
                continue;
            }

            for (int e = 0; e < node.Next.Count; e++)
            {
                var target = map.Find(node.Next[e]);
                if (target is null || target.Kind == NodeKind.Boss)
                {
                    continue;
                }

                if (Math.Abs(target.IndexInLayer - node.IndexInLayer) > 1)
                {
                    problems.Add(
                        $"la arista {node.Id} -> {target.Id} salta del carril {node.IndexInLayer} al "
                            + $"{target.IndexInLayer}; solo se admiten carriles contiguos (ADR 0053)");
                }
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
    /// RF-011b, esqueleto: el reparto de capas de mercado que hace cierta la garantía de los dos saltos
    /// (ADR 0053). Con capas de mercado <b>mezcladas</b> —mercado y partido en la misma capa— la
    /// condición es que toda capa sea previa a una capa de mercado, o que lo sea su sucesora, o que
    /// tenga el jefe a dos saltos o menos. De ahí salen las capas pares 2, 4, 6, 8 (…): un mercado cada
    /// 2 capas, no cada 3.
    ///
    /// <para>La densidad "un mercado cada 3-4 nodos" de RF-011b se cumple ahora por exceso en lo que se
    /// <b>ofrece</b> (uno cada 2 capas) y deja de cumplirse en lo que se <b>recorre</b>: un camino puede
    /// no pisar ninguno. Es el desvío que pedía RF-002d y que el mapa de cuellos de botella no tenía;
    /// queda anotado en <c>fase2-diseno.md</c> §24.</para>
    /// </summary>
    private static void CheckMarketLayout(ActMap map, List<string> problems)
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

        int bossLayer = pathLength - 1;
        for (int layer = 0; layer < bossLayer; layer++)
        {
            bool covered = marketLayers.Contains(layer + 1)
                || marketLayers.Contains(layer + 2)
                || layer + MapGenerator.MaxHopsToMarket >= bossLayer;
            if (!covered)
            {
                problems.Add(
                    $"la capa {layer} no tiene capa de mercado en {layer + 1} ni en {layer + 2}, y el jefe "
                        + $"está a más de {MapGenerator.MaxHopsToMarket} saltos (RF-011b)");
            }
        }
    }

    private static string Distance(int hops) => hops == int.MaxValue ? "infinitos" : hops.ToString();
}
