using Underleague.Sim.Random;

namespace Underleague.Sim.Run;

/// <summary>
/// Opciones de generación de un mapa de acto.
/// </summary>
/// <param name="PathLength">
/// Nodos que el jugador <b>recorre</b> en el acto, 10..12 (RF-001), es decir, el número de capas del
/// grafo. Valor de partida 11 (D-2/D-10, <c>fase2-diseno.md</c> §4). Ver la nota sobre la lectura de
/// RF-001 en la documentación de <see cref="MapGenerator"/>.
/// </param>
/// <param name="OpponentIds">
/// Rivales estáticos del acto (RF-015, <c>data/rivals/</c>). Si la lista no está vacía, el generador
/// reparte esos ids entre los nodos de partido barajándolos con el flujo del mapa: <b>lo aleatorio es
/// qué rival cae en qué nodo</b>, no el rival. Si está vacía, los nodos quedan con
/// <see cref="MapNode.OpponentId"/> vacío y el rival lo produce <see cref="IRunSystems.OpponentFor"/>.
/// </param>
public sealed record MapOptions(int PathLength = MapGenerator.DefaultPathLength, IReadOnlyList<string>? OpponentIds = null)
{
    /// <summary>Opciones por defecto: 11 nodos por camino y sin catálogo de rivales.</summary>
    public static MapOptions Default { get; } = new();
}

/// <summary>
/// Generador del grafo de un acto (RF-010..RF-015). Puro y determinista: solo consume el flujo
/// <c>RngStreams.Map(runSeed, act)</c> (RT-022), así que cambiar la semilla de recompensas o de partido
/// no altera el mapa.
///
/// <para><b>Qué cuentan los "10-12 nodos por acto" de RF-001.</b> Cuentan los nodos que el jugador
/// <b>recorre</b>, no los dibujados. Es la única lectura con la que cuadran los demás números del
/// documento: <c>fase2-diseno.md</c> §4 fija "11 nodos, de los cuales 6 partidos como máximo", que por
/// tres actos son 18 partidos, exactamente la métrica "duración de la run en partidos: 18-22" de §10;
/// y RF-003b ("con 30-36 nodos por run, no más del 60% pueden ser partidos") habla de la fatiga del
/// jugador, que depende de lo que juega y no de lo que ve dibujado. Con la lectura contraria -10-12
/// nodos dibujados y un camino que visita solo una parte- una run sale de unos 10 partidos, la mitad de
/// la métrica de §10 y muy por debajo de los 75-100 minutos de RF-003. Queda anotado como lectura
/// aplicada: la palabra "contiene" de RF-001 es la que se dobla.</para>
///
/// <para><b>Esqueleto: una capa por nodo recorrido.</b> El acto tiene <c>PathLength</c> capas y el
/// jugador atraviesa exactamente una por capa. Las capas 2, 5 y 8 son de mercado y la última es el
/// jefe; el resto son libres, de 2 o 3 nodos, que es donde está la decisión.</para>
///
/// <code>
///   capa:   0     1    2       3     4    5       6     7    8       9     10
///          libre libre MERCADO libre libre MERCADO libre libre MERCADO libre JEFE
/// </code>
///
/// <para><b>Las capas de servicio reparten cuatro tipos</b> (clínica, entrenamiento, evento e
/// inscripción, ADR 0046) con dos garantías por acto: una clínica en la primera capa de servicios y un
/// nodo de inscripción en la última. El nodo de inscripción entra quitándole sitio a los otros tres
/// servicios, no al mercado: la garantía de RF-011b sale de las capas de mercado y no la toca nadie.</para>
///
/// <para><b>La garantía de RF-011b sale por construcción, no por reintentos.</b> Demostración corta:
/// en un grafo por capas con aristas <c>i -> i+1</c>, lo alcanzable en dos saltos desde la capa
/// <c>i</c> son las capas <c>i+1</c> e <c>i+2</c>. Si una capa contuviera un mercado <i>y</i> un nodo
/// que no lo es, ese nodo necesitaría otro mercado en <c>i+1</c> o <c>i+2</c>, y el siguiente está a 3
/// o 4 nodos por el propio RF-011b: contradicción. Luego <b>una capa de mercado tiene que ser toda
/// mercado</b>. Y la separación tiene que ser exactamente 3: con 4, un nodo de la capa <c>m+1</c> se
/// queda a tres saltos del siguiente mercado. Con capas de mercado de un nodo cada 3 capas, todo nodo
/// tiene un mercado a uno o dos saltos, y los de las dos últimas capas tienen el jefe a uno o dos.
/// Nunca hay que regenerar un mapa.</para>
///
/// <para><b>Tope de partidos (RF-003b) en el peor camino.</b> Cada capa libre es entera de partidos o
/// entera de nodos de servicio (clínica, entrenamiento, evento). Así el número máximo de partidos que
/// puede jugar <i>cualquier</i> camino es el número de capas de partido, y el tope del 60% se cumple se
/// elija lo que se elija, no en promedio.</para>
///
/// <para><b>Aristas sin cruces.</b> Entre dos capas contiguas de <c>a</c> y <c>b</c> nodos se sortean
/// <c>a-1</c> cortes ordenados en <c>[0, b-1]</c> y la fuente <c>i</c> se conecta al intervalo
/// <c>[corte_i, corte_{i+1}]</c>. Los intervalos son contiguos y ordenados, así que cubren todos los
/// destinos (ningún nodo se queda sin entrada), ninguna fuente se queda sin salida, y dos aristas nunca
/// se cruzan (el máximo destino de la fuente <c>i</c> es el mínimo de la <c>i+1</c>).</para>
/// </summary>
public static class MapGenerator
{
    /// <summary>Nodos recorridos mínimos por acto (RF-001).</summary>
    public const int MinPathLength = 10;

    /// <summary>Nodos recorridos máximos por acto (RF-001).</summary>
    public const int MaxPathLength = 12;

    /// <summary>Nodos recorridos por acto por defecto (D-2/D-10).</summary>
    public const int DefaultPathLength = 11;

    /// <summary>Porcentaje máximo de nodos de partido de un camino (RF-003b).</summary>
    public const int MaxMatchPercent = 60;

    /// <summary>Separación entre capas de mercado, en capas (RF-011b; ver la demostración de la clase).</summary>
    public const int MarketLayerSpacing = 3;

    /// <summary>Primera capa de mercado. Con la separación de 3 salen las capas 2, 5 y 8.</summary>
    public const int FirstMarketLayer = 2;

    /// <summary>Salto máximo permitido hasta un mercado (RF-011b).</summary>
    public const int MaxHopsToMarket = 2;

    /// <summary>Anchura mínima de una capa libre: siempre hay al menos dos opciones donde decidir.</summary>
    public const int MinFreeLayerWidth = 2;

    /// <summary>Anchura máxima de una capa libre.</summary>
    public const int MaxFreeLayerWidth = 3;

    /// <summary>Multiplicador del acto en el id de nodo: <c>Id = act * NodeIdBase + índice</c>.</summary>
    public const int NodeIdBase = 100;

    /// <summary>Capas de mercado de un acto de <paramref name="pathLength"/> nodos: 2, 5 y 8.</summary>
    public static IReadOnlyList<int> MarketLayers(int pathLength)
    {
        var layers = new List<int>();
        for (int layer = FirstMarketLayer; layer < pathLength - 1; layer += MarketLayerSpacing)
        {
            layers.Add(layer);
        }

        // El último mercado no puede quedar a más de 3 capas del jefe: si no, un nodo de la capa
        // siguiente al último mercado se quedaría sin mercado y sin jefe a dos saltos (RF-011b). Con
        // 10, 11 y 12 capas y mercados en 2, 5 y 8 la distancia al jefe es 1, 2 y 3, así que se cumple;
        // la comprobación está aquí para que ampliar el rango de RF-001 no rompa la garantía en
        // silencio.
        if (layers.Count == 0 || pathLength - 1 - layers[^1] > MarketLayerSpacing)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pathLength),
                pathLength,
                "con esa longitud de camino no caben mercados cada 3 capas que dejen el jefe a dos saltos del último (RF-011b)");
        }

        return layers;
    }

    /// <summary>
    /// Genera el mapa del acto <paramref name="act"/> de la run con semilla <paramref name="runSeed"/>.
    /// Determinista: el mismo par (semilla, acto) produce siempre el mismo grafo.
    /// </summary>
    public static ActMap Generate(ulong runSeed, int act, MapOptions? options = null)
    {
        if (act < 1 || act > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(act), act, "el acto debe estar entre 1 y 3 (RF-001)");
        }

        options ??= MapOptions.Default;
        int pathLength = options.PathLength;
        if (pathLength < MinPathLength || pathLength > MaxPathLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                pathLength,
                $"un acto tiene entre {MinPathLength} y {MaxPathLength} nodos por camino (RF-001)");
        }

        var rng = RngStreams.Map(runSeed, act);
        var markets = MarketLayers(pathLength);
        int[] widths = LayerWidths(ref rng, pathLength, markets);
        var layerKinds = LayerClasses(ref rng, pathLength, markets);
        var nodes = BuildNodes(ref rng, act, widths, layerKinds, options.OpponentIds);

        var entries = new List<int>(widths[0]);
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Layer == 0)
            {
                entries.Add(nodes[i].Id);
            }
        }

        return new ActMap(act, nodes, entries, nodes[^1].Id, string.Empty, false);
    }

    /// <summary>Clase de cada capa: mercado, jefe, partidos o servicios.</summary>
    private enum LayerClass
    {
        Market,
        Boss,
        Matches,
        Services,
    }

    /// <summary>
    /// Anchura de cada capa. Mercados y jefe son cuellos de botella de un nodo; las capas libres tienen
    /// 2 nodos, y de cero a dos de ellas suben a 3 para que no todos los actos se dibujen igual.
    /// </summary>
    private static int[] LayerWidths(ref Pcg32 rng, int pathLength, IReadOnlyList<int> markets)
    {
        var widths = new int[pathLength];
        var free = new List<int>();
        for (int layer = 0; layer < pathLength; layer++)
        {
            bool isMarket = markets.Contains(layer);
            bool isBoss = layer == pathLength - 1;
            widths[layer] = isMarket || isBoss ? 1 : MinFreeLayerWidth;
            if (!isMarket && !isBoss)
            {
                free.Add(layer);
            }
        }

        int wide = rng.Range(0, 3);
        rng.Shuffle(free);
        for (int i = 0; i < wide && i < free.Count; i++)
        {
            widths[free[i]] = MaxFreeLayerWidth;
        }

        return widths;
    }

    /// <summary>
    /// Reparte las capas libres entre partidos y servicios. El presupuesto sale de RF-003b:
    /// <c>pathLength * 60 / 100</c> capas de partido <b>incluida la del jefe</b>. Siempre queda al menos
    /// una capa de servicios, que es donde va la clínica.
    /// </summary>
    private static LayerClass[] LayerClasses(ref Pcg32 rng, int pathLength, IReadOnlyList<int> markets)
    {
        var classes = new LayerClass[pathLength];
        var free = new List<int>();
        for (int layer = 0; layer < pathLength; layer++)
        {
            if (layer == pathLength - 1)
            {
                classes[layer] = LayerClass.Boss;
            }
            else if (markets.Contains(layer))
            {
                classes[layer] = LayerClass.Market;
            }
            else
            {
                free.Add(layer);
            }
        }

        int matchBudget = (pathLength * MaxMatchPercent / 100) - 1;
        if (matchBudget > free.Count - 1)
        {
            matchBudget = free.Count - 1;
        }

        if (matchBudget < 0)
        {
            matchBudget = 0;
        }

        var shuffled = new List<int>(free);
        rng.Shuffle(shuffled);
        for (int i = 0; i < shuffled.Count; i++)
        {
            classes[shuffled[i]] = i < matchBudget ? LayerClass.Matches : LayerClass.Services;
        }

        return classes;
    }

    /// <summary>Construye los nodos con sus aristas, tipos, rivales y distintivo de dificultad.</summary>
    private static List<MapNode> BuildNodes(
        ref Pcg32 rng,
        int act,
        int[] widths,
        LayerClass[] layerKinds,
        IReadOnlyList<string>? opponentIds)
    {
        int layerCount = widths.Length;
        var idsByLayer = new int[layerCount][];
        int nextId = act * NodeIdBase;
        for (int layer = 0; layer < layerCount; layer++)
        {
            idsByLayer[layer] = new int[widths[layer]];
            for (int index = 0; index < widths[layer]; index++)
            {
                idsByLayer[layer][index] = nextId++;
            }
        }

        var targets = new List<int>[layerCount][];
        for (int layer = 0; layer < layerCount; layer++)
        {
            targets[layer] = new List<int>[widths[layer]];
            for (int index = 0; index < widths[layer]; index++)
            {
                targets[layer][index] = new List<int>();
            }
        }

        for (int layer = 0; layer < layerCount - 1; layer++)
        {
            Connect(ref rng, widths[layer], widths[layer + 1], targets[layer], idsByLayer[layer + 1]);
        }

        var kinds = NodeKindsByLayer(ref rng, widths, layerKinds, act);
        var opponents = ShuffledOpponents(ref rng, opponentIds);
        int opponentCursor = 0;

        var nodes = new List<MapNode>();
        for (int layer = 0; layer < layerCount; layer++)
        {
            for (int index = 0; index < widths[layer]; index++)
            {
                var kind = kinds[layer][index];
                string opponentId = string.Empty;
                if (NodeKinds.IsMatch(kind) && opponents.Count > 0)
                {
                    opponentId = opponents[opponentCursor % opponents.Count];
                    opponentCursor++;
                }

                nodes.Add(new MapNode(
                    idsByLayer[layer][index],
                    act,
                    layer,
                    index,
                    kind,
                    targets[layer][index],
                    opponentId,
                    Difficulty(act, kind)));
            }
        }

        return nodes;
    }

    /// <summary>
    /// Tipo de cada nodo. En una capa de partidos todos son de partido, y de 1 a 2 de todo el acto se
    /// ascienden a élite (RF-011), nunca en la capa 0. En una capa de servicios los nodos son distintos
    /// entre sí siempre que quepan, para que elegir signifique algo; el acto garantiza una clínica
    /// (RF-094: es lo que hace tratable una lesión grave).
    /// </summary>
    private static NodeKind[][] NodeKindsByLayer(ref Pcg32 rng, int[] widths, LayerClass[] layerKinds, int act)
    {
        var kinds = new NodeKind[widths.Length][];
        var serviceLayers = new List<int>();
        var matchLayers = new List<int>();

        for (int layer = 0; layer < widths.Length; layer++)
        {
            kinds[layer] = new NodeKind[widths[layer]];
            switch (layerKinds[layer])
            {
                case LayerClass.Boss:
                    kinds[layer][0] = NodeKind.Boss;
                    break;

                case LayerClass.Market:
                    kinds[layer][0] = NodeKind.Market;
                    break;

                case LayerClass.Matches:
                    for (int i = 0; i < widths[layer]; i++)
                    {
                        kinds[layer][i] = NodeKind.LeagueMatch;
                    }

                    matchLayers.Add(layer);
                    break;

                default:
                    serviceLayers.Add(layer);
                    break;
            }
        }

        // Servicios. Dos garantías por acto, y las dos son de diseño, no de sorteo: la PRIMERA capa de
        // servicios lleva siempre una clínica (RF-094: es lo que hace tratable una lesión grave) y la
        // ÚLTIMA lleva siempre un nodo de inscripción (ADR 0046: comprar un hueco tiene que ser una
        // opción real en cada acto, no una que el dado puede no ofrecer nunca). El resto de la capa sale
        // del sorteo, así que ir a por el hueco significa no ir al otro servicio de esa capa —y cuando
        // solo hay una capa de servicios en todo el acto, la elección es literalmente «curo al que tengo
        // o me traigo a otro»—.
        bool clinicPlaced = false;
        bool enrollmentPlaced = false;
        for (int i = 0; i < serviceLayers.Count; i++)
        {
            int layer = serviceLayers[i];
            var pool = new List<NodeKind> { NodeKind.Clinic, NodeKind.Training, NodeKind.Event, NodeKind.Enrollment };
            rng.Shuffle(pool);

            var forced = new List<NodeKind>(2);
            if (!clinicPlaced)
            {
                forced.Add(NodeKind.Clinic);
                clinicPlaced = true;
            }

            if (!enrollmentPlaced && i == serviceLayers.Count - 1)
            {
                forced.Add(NodeKind.Enrollment);
                enrollmentPlaced = true;
            }

            var ordered = new List<NodeKind>(pool.Count);
            ordered.AddRange(forced);
            for (int p = 0; p < pool.Count; p++)
            {
                if (!forced.Contains(pool[p]))
                {
                    ordered.Add(pool[p]);
                }
            }

            for (int index = 0; index < widths[layer]; index++)
            {
                kinds[layer][index] = ordered[index % ordered.Count];
                if (kinds[layer][index] == NodeKind.Enrollment)
                {
                    enrollmentPlaced = true;
                }
            }
        }

        // Élites: 1 en el acto 1, 2 en los actos 2 y 3, en capas distintas y nunca en la capa 0.
        var candidates = new List<int>();
        for (int i = 0; i < matchLayers.Count; i++)
        {
            if (matchLayers[i] != 0)
            {
                candidates.Add(matchLayers[i]);
            }
        }

        rng.Shuffle(candidates);
        int elites = act == 1 ? 1 : 2;
        for (int i = 0; i < elites && i < candidates.Count; i++)
        {
            int layer = candidates[i];
            kinds[layer][rng.Range(0, widths[layer])] = NodeKind.EliteMatch;
        }

        return kinds;
    }

    /// <summary>
    /// Distintivo de dificultad de 5 niveles (RF-012), derivado del acto y del tipo de nodo. Es un valor
    /// de partida: cuando el paquete X traiga <c>data/rivals/</c>, la dificultad la fija el rival y este
    /// número pasa a ser el que se use solo cuando el nodo no tiene rival asignado.
    /// </summary>
    private static int Difficulty(int act, NodeKind kind) => kind switch
    {
        NodeKind.LeagueMatch => Math.Clamp(act, 1, 5),
        NodeKind.EliteMatch => Math.Clamp(act + 1, 1, 5),
        NodeKind.Boss => Math.Clamp(act + 2, 1, 5),
        _ => 0,
    };

    private static List<string> ShuffledOpponents(ref Pcg32 rng, IReadOnlyList<string>? opponentIds)
    {
        var opponents = new List<string>();
        if (opponentIds is null || opponentIds.Count == 0)
        {
            return opponents;
        }

        for (int i = 0; i < opponentIds.Count; i++)
        {
            opponents.Add(opponentIds[i]);
        }

        rng.Shuffle(opponents);
        return opponents;
    }

    /// <summary>
    /// Conecta una capa de <paramref name="sources"/> nodos con la siguiente de
    /// <paramref name="destinations"/> nodos. Ver la explicación de los cortes en la documentación de
    /// la clase.
    /// </summary>
    private static void Connect(ref Pcg32 rng, int sources, int destinations, List<int>[] targets, int[] destinationIds)
    {
        var cuts = new int[sources + 1];
        cuts[0] = 0;
        cuts[sources] = destinations - 1;
        for (int i = 1; i < sources; i++)
        {
            cuts[i] = rng.Range(0, destinations);
        }

        if (sources > 1)
        {
            Array.Sort(cuts, 1, sources - 1);
        }

        for (int i = 0; i < sources; i++)
        {
            for (int j = cuts[i]; j <= cuts[i + 1]; j++)
            {
                targets[i].Add(destinationIds[j]);
            }
        }
    }
}
