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
/// <b>recorre</b>, no los dibujados (decisión W-1). Es la única lectura con la que cuadran los demás
/// números del documento: <c>fase2-diseno.md</c> §4 fija "11 nodos, de los cuales 6 partidos como
/// máximo", que por tres actos son 18 partidos, dentro de la métrica "duración de la run en partidos:
/// 18-22" de §10; y RF-003b habla de la fatiga del jugador, que depende de lo que juega y no de lo que
/// ve dibujado.</para>
///
/// <para><b>Cuatro carriles (ADR 0053).</b> El acto tiene <c>PathLength</c> capas y el jugador atraviesa
/// exactamente una por capa, pero cada capa reparte sus nodos entre <see cref="Lanes"/> carriles fijos.
/// La forma es la de Slay the Spire:</para>
/// <list type="number">
/// <item><b>Entrada única y apertura progresiva</b>: 1 → 2 → 4. Todo el mundo juega el mismo primer
/// nodo, que es siempre un partido de liga: arranque consistente, punto de comparación entre runs y
/// sitio de la primera run guiada (RF-123).</item>
/// <item><b>Carriles contiguos a partir de la capa 2</b>: desde el carril <c>i</c> solo se va a
/// <c>i-1</c>, <c>i</c> o <c>i+1</c> de la capa siguiente. Es lo que le da <b>memoria</b> a la elección:
/// subir de carril aleja de la parte baja y volver cuesta varias capas. La apertura (capas 0 y 1) es
/// completa, y la capa del jefe está exenta: todos los caminos convergen en él.</item>
/// <item><b>Divergencia y reconvergencia</b>: las capas se cruzan y se vuelven a juntar, así que elegir
/// una rama no cierra el mapa.</item>
/// </list>
///
/// <code>
///   capa:    0    1    2      3    4      5    6      7    8      9    10
///   ancho:   1    2    4      3-4  4      3-4  4      3-4  4      3-4   1
///                      MERC.       MERC.       MERC.       MERC.       JEFE
/// </code>
///
/// <para><b>La garantía de RF-011b sigue saliendo por construcción, y es lo que fija el esqueleto.</b>
/// En un grafo por capas, lo alcanzable en dos saltos desde la capa <c>i</c> son las capas <c>i+1</c> e
/// <c>i+2</c>. Si una capa de mercado <b>mezcla</b> mercado con otros nodos —que es justo lo que pide la
/// ADR 0053 para que desviarse cueste posición—, el nodo que no es mercado necesita otro mercado en
/// <c>i+1</c> o <c>i+2</c>. Luego <b>las capas de mercado van cada 2</b>, no cada 3: son las capas pares
/// de la 2 a la <c>PathLength-2</c>. La demostración se cierra en tres pasos:</para>
/// <list type="number">
/// <item><b>Dominación.</b> Los mercados de la capa <c>m</c> cubren todos los carriles de la capa
/// <c>m-1</c>: para cada carril <c>x</c> de <c>m-1</c> hay un mercado en <c>[x-1, x+1]</c>. Se consigue
/// eligiendo los carriles de mercado en función del ancho de la capa anterior: si mide 3 carriles basta
/// con <b>uno</b> (el central los cubre los tres); si mide los 4, hacen falta <b>dos</b>, uno en
/// <c>{0,1}</c> y otro en <c>{2,3}</c>. De ahí sale el "uno o dos carriles" de la ADR.</item>
/// <item><b>Arista forzada.</b> Todo nodo de la capa <c>m-1</c> recibe explícitamente la arista al
/// mercado que lo domina, así que tiene un mercado <b>a un salto</b>.</item>
/// <item><b>Dos saltos para el resto.</b> Cualquier otro nodo tiene sus sucesores en una capa
/// <c>m-1</c> (porque las capas de mercado van cada 2), y por el paso anterior ese sucesor tiene un
/// mercado a un salto: dos en total. Las últimas capas del acto no necesitan mercado porque tienen el
/// <b>jefe</b> a uno o dos saltos, que es la excepción que RF-011b ya admitía.</item>
/// </list>
/// <para>Nunca hay que regenerar un mapa, y el precio son <b>4-5 capas de mercado por acto</b> en vez de
/// 3 capas-cuello de botella. Ojo: son capas <i>ofrecidas</i>. Un camino puede no pisar ninguna, que es
/// exactamente el desvío que RF-002d describía y que el mapa anterior no tenía.</para>
///
/// <para><b>Tope de partidos (RF-003b) en el peor camino.</b> Ya no vale la regla anterior ("una capa es
/// entera de partidos o entera de servicios"), porque con cuatro carriles interesa mezclar. La regla
/// nueva es más débil pero basta: una capa <b>lleva partidos o no lleva ninguno</b>. Como un camino
/// visita una capa de cada índice, el número de partidos de cualquier camino está acotado por el número
/// de capas con partido, y ese número es el presupuesto de RF-003b. Una de esas capas por acto es
/// <b>porosa</b>: lleva un servicio en uno de sus carriles, y ahí la elección es "juego o me curo".
/// Mercado y partido no comparten capa, y la razón no es de diseño sino del instrumento de medida:
/// ver <see cref="PorousMatchLayers"/> y <c>fase2-diseno.md</c> §24 (AH-8b).</para>
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

    /// <summary>Carriles del mapa (ADR 0053). Un nodo vive en un carril y solo se mueve a los contiguos.</summary>
    public const int Lanes = 4;

    /// <summary>
    /// Capas de apertura, sin restricción de carril: la 0 (entrada única) y la 1 (dos nodos). De la capa
    /// <see cref="OpeningLayers"/> en adelante, las aristas solo van a carriles contiguos.
    /// </summary>
    public const int OpeningLayers = 2;

    /// <summary>Separación entre capas de mercado, en capas (RF-011b + ADR 0053; ver la demostración de la clase).</summary>
    public const int MarketLayerSpacing = 2;

    /// <summary>Primera capa de mercado; es también la primera capa de ancho completo.</summary>
    public const int FirstMarketLayer = 2;

    /// <summary>Salto máximo permitido hasta un mercado (RF-011b).</summary>
    public const int MaxHopsToMarket = 2;

    /// <summary>Anchura mínima de una capa libre.</summary>
    public const int MinFreeLayerWidth = 3;

    /// <summary>Anchura máxima de una capa libre: el ancho completo del mapa.</summary>
    public const int MaxFreeLayerWidth = Lanes;

    /// <summary>Carril donde se dibujan las capas de un solo nodo (entrada y jefe).</summary>
    public const int CenterLane = 1;

    /// <summary>
    /// Capas de partido <b>porosas</b> por acto: las que ofrecen un servicio en uno de sus carriles, de
    /// modo que se puede pasar por esa capa sin jugar. Las demás capas de partido son de partido en
    /// todos sus carriles, así que ningún camino puede esquivarlas.
    ///
    /// <para>Es el mando que fija el <b>suelo</b> de partidos de un acto, y hay que tocarlo sabiendo lo
    /// que se hace: cada capa porosa de más es un partido menos en el camino que los esquiva todos. Con
    /// 1, el peor camino juega el máximo de RF-003b y el más evasivo uno menos (medido: 5-6 partidos por
    /// acto, 17-20 por run, contra los 18-22 de <c>fase2-diseno.md</c> §10).</para>
    /// </summary>
    public const int PorousMatchLayers = 1;

    /// <summary>Multiplicador del acto en el id de nodo: <c>Id = act * NodeIdBase + índice</c>.</summary>
    public const int NodeIdBase = 100;

    /// <summary>
    /// Capas de mercado de un acto de <paramref name="pathLength"/> nodos: las pares de la 2 a la
    /// <c>pathLength-2</c>. Comprueba además la condición que hace cierta la garantía de RF-011b, para
    /// que ampliar el rango de RF-001 no la rompa en silencio.
    /// </summary>
    public static IReadOnlyList<int> MarketLayers(int pathLength)
    {
        var layers = new List<int>();
        for (int layer = FirstMarketLayer; layer <= pathLength - 2; layer += MarketLayerSpacing)
        {
            layers.Add(layer);
        }

        // Cobertura: cada capa tiene que ser capa previa a un mercado (mercado a un salto), o tener a su
        // sucesora como capa previa a un mercado (dos saltos), o tener el jefe a dos saltos o menos.
        int bossLayer = pathLength - 1;
        for (int layer = 0; layer < bossLayer; layer++)
        {
            bool covered = layers.Contains(layer + 1)
                || layers.Contains(layer + 2)
                || layer + MaxHopsToMarket >= bossLayer;
            if (!covered)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pathLength),
                    pathLength,
                    $"con esa longitud de camino la capa {layer} se queda sin mercado ni jefe a dos saltos (RF-011b)");
            }
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
        var shapes = LayerShapes(ref rng, pathLength, markets);
        var marketLanes = MarketLanes(ref rng, shapes, markets);
        var kinds = BuildKinds(ref rng, shapes, markets, marketLanes, act);
        var nodes = BuildNodes(ref rng, act, shapes, markets, marketLanes, kinds, options.OpponentIds);

        var entries = new List<int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Layer == 0)
            {
                entries.Add(nodes[i].Id);
            }
        }

        return new ActMap(act, nodes, entries, nodes[^1].Id, string.Empty, false);
    }

    /// <summary>
    /// Forma de una capa: un intervalo de carriles contiguos <c>[Start, Start + Width)</c>. Contiguo a
    /// propósito: con huecos interiores un carril podría quedarse sin vecino a distancia 1 en la capa de
    /// al lado y el grafo se rompería en trozos.
    /// </summary>
    private readonly record struct LayerShape(int Start, int Width)
    {
        public int Lane(int index) => Start + index;

        public bool Has(int lane) => lane >= Start && lane < Start + Width;
    }

    /// <summary>
    /// Ancho y posición de cada capa. La 0 tiene un nodo, la 1 dos, las de mercado el ancho completo, el
    /// jefe uno, y las libres 3 o 4 carriles. Las capas <b>previas a un mercado</b> tiran a 3 (tres de
    /// cada cuatro veces): con 3 carriles un solo mercado los domina, y ese es el caso en que el mercado
    /// es un desvío nítido en vez de dos puertas.
    /// </summary>
    private static LayerShape[] LayerShapes(ref Pcg32 rng, int pathLength, IReadOnlyList<int> markets)
    {
        var shapes = new LayerShape[pathLength];
        int bossLayer = pathLength - 1;
        shapes[0] = new LayerShape(CenterLane, 1);
        shapes[1] = new LayerShape(1, 2);
        shapes[bossLayer] = new LayerShape(CenterLane, 1);

        for (int layer = OpeningLayers; layer < bossLayer; layer++)
        {
            if (markets.Contains(layer))
            {
                shapes[layer] = new LayerShape(0, Lanes);
                continue;
            }

            bool beforeMarket = markets.Contains(layer + 1);
            int width = beforeMarket
                ? (rng.Range(0, 4) == 0 ? MaxFreeLayerWidth : MinFreeLayerWidth)
                : (rng.Range(0, 2) == 0 ? MinFreeLayerWidth : MaxFreeLayerWidth);
            int start = width >= Lanes ? 0 : rng.Range(0, Lanes - width + 1);
            shapes[layer] = new LayerShape(start, width);
        }

        return shapes;
    }

    /// <summary>
    /// Carriles de mercado de cada capa de mercado: el conjunto dominante mínimo de los carriles de la
    /// capa anterior (paso 1 de la demostración de RF-011b). La capa 2 es la excepción: la apertura
    /// 2 → 4 es completa, así que cualquier carril vale y basta con uno.
    /// </summary>
    private static Dictionary<int, List<int>> MarketLanes(ref Pcg32 rng, LayerShape[] shapes, IReadOnlyList<int> markets)
    {
        var lanes = new Dictionary<int, List<int>>();
        for (int i = 0; i < markets.Count; i++)
        {
            int layer = markets[i];
            var chosen = new List<int>();
            if (layer == FirstMarketLayer)
            {
                int first = rng.Range(0, Lanes);
                chosen.Add(first);

                // Un segundo mercado de vez en cuando, en la otra mitad del mapa: dos desvíos distintos
                // en la misma capa, no dos puertas pegadas.
                if (rng.Range(0, 4) == 0)
                {
                    chosen.Add(first < Lanes / 2 ? rng.Range(Lanes / 2, Lanes) : rng.Range(0, Lanes / 2));
                }
            }
            else
            {
                var previous = shapes[layer - 1];
                if (previous.Width >= Lanes)
                {
                    chosen.Add(rng.Range(0, Lanes / 2));
                    chosen.Add(rng.Range(Lanes / 2, Lanes));
                }
                else
                {
                    chosen.Add(previous.Start + (previous.Width / 2));
                }
            }

            chosen.Sort();
            lanes[layer] = chosen;
        }

        return lanes;
    }

    /// <summary>
    /// Tipo de cada nodo, por capa y carril. El reparto obedece tres reglas duras: la capa 0 es siempre
    /// un partido de liga (arranque común de la ADR 0053), las capas con partido no pasan del
    /// presupuesto de RF-003b, y el acto garantiza una clínica (RF-094) y un nodo de inscripción
    /// (ADR 0046).
    /// </summary>
    private static Dictionary<int, NodeKind[]> BuildKinds(
        ref Pcg32 rng,
        LayerShape[] shapes,
        IReadOnlyList<int> markets,
        Dictionary<int, List<int>> marketLanes,
        int act)
    {
        int pathLength = shapes.Length;
        int bossLayer = pathLength - 1;
        var kinds = new Dictionary<int, NodeKind[]>();
        for (int layer = 0; layer < pathLength; layer++)
        {
            kinds[layer] = new NodeKind[shapes[layer].Width];
        }

        kinds[bossLayer][0] = NodeKind.Boss;
        kinds[0][0] = NodeKind.LeagueMatch;

        foreach (var (layer, lanes) in Ordered(marketLanes))
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                kinds[layer][lanes[i] - shapes[layer].Start] = NodeKind.Market;
            }
        }

        // Capas con partido (RF-003b sobre el peor camino). El presupuesto cuenta el jefe y la capa 0,
        // que ya están puestos; el resto se reparte entre las capas intermedias, y no da igual cuáles:
        //
        //   - ninguna capa con partido lleva mercado. No es una decisión de diseño sino del instrumento
        //     de medida: la política automática de /Balance puntúa el mercado con 90 y un partido con
        //     50 menos la dificultad (`RunPolicy.ChooseNode`), pesos calibrados cuando el mercado era un
        //     cuello de botella y no competía con nada; con mercado y partido en la misma capa la
        //     política se va SIEMPRE a la tienda y la run medida baja de los 18 partidos de §10. Ver §24;
        //   - la capa POROSA es una capa de partido con un carril de servicio: ahí la elección es
        //     "juego o me curo", que la política solo toma cuando de verdad necesita la clínica o el
        //     hueco de plantilla, que es justo cuando debe tomarse;
        //   - las demás capas con partido son de partido en todos sus carriles, que es lo que impide que
        //     un camino esquive el acto entero.
        int budget = (pathLength * MaxMatchPercent / 100) - 2;
        var plain = new List<int>();
        var shops = new List<int>();
        for (int layer = 1; layer < bossLayer; layer++)
        {
            if (markets.Contains(layer))
            {
                shops.Add(layer);
            }
            else
            {
                plain.Add(layer);
            }
        }

        rng.Shuffle(plain);
        rng.Shuffle(shops);
        var matchLayers = new List<int>();
        for (int i = 0; i < budget && i < plain.Count; i++)
        {
            matchLayers.Add(plain[i]);
        }

        for (int i = 0; matchLayers.Count < budget && i < shops.Count; i++)
        {
            matchLayers.Add(shops[i]);
        }

        // Las capas porosas se eligen entre las que tienen sitio: un carril para el servicio y al menos
        // dos para partidos, de modo que desviarse siga costando el partido que había en ese carril.
        var mixed = new List<int>();
        for (int i = 0; i < matchLayers.Count && mixed.Count < PorousMatchLayers; i++)
        {
            if (shapes[matchLayers[i]].Width - MarketLaneCount(marketLanes, matchLayers[i]) >= 3)
            {
                mixed.Add(matchLayers[i]);
            }
        }

        matchLayers.Sort();

        // Huecos de servicio: todo lo que no es mercado en una capa sin partido, más un carril suelto en
        // cada capa mezclada.
        var serviceSlots = new List<(int Layer, int Index)>();
        for (int layer = 1; layer < bossLayer; layer++)
        {
            var free = new List<int>();
            for (int index = 0; index < shapes[layer].Width; index++)
            {
                if (kinds[layer][index] != NodeKind.Market)
                {
                    free.Add(index);
                }
            }

            if (!matchLayers.Contains(layer))
            {
                for (int i = 0; i < free.Count; i++)
                {
                    serviceSlots.Add((layer, free[i]));
                }

                continue;
            }

            for (int i = 0; i < free.Count; i++)
            {
                kinds[layer][free[i]] = NodeKind.LeagueMatch;
            }

            if (mixed.Contains(layer) && free.Count >= 2)
            {
                serviceSlots.Add((layer, free[rng.Range(0, free.Count)]));
            }
        }

        AssignServices(ref rng, kinds, serviceSlots);
        AssignElites(ref rng, kinds, shapes, matchLayers, act);
        return kinds;
    }

    /// <summary>
    /// Reparte clínica, entrenamiento, evento e inscripción entre los huecos de servicio. Dos garantías
    /// por acto, y las dos son de diseño y no de sorteo: el <b>primer</b> hueco de servicio del acto es
    /// una clínica (RF-094: es lo que hace tratable una lesión grave) y el <b>último</b> es un nodo de
    /// inscripción (ADR 0046: comprar un hueco tiene que ser una opción real en cada acto). Dentro de
    /// una capa los servicios son distintos entre sí mientras quepan, para que elegir signifique algo.
    /// </summary>
    private static void AssignServices(ref Pcg32 rng, Dictionary<int, NodeKind[]> kinds, List<(int Layer, int Index)> slots)
    {
        if (slots.Count == 0)
        {
            return;
        }

        var byLayer = new List<int>();
        for (int i = 0; i < slots.Count; i++)
        {
            if (!byLayer.Contains(slots[i].Layer))
            {
                byLayer.Add(slots[i].Layer);
            }
        }

        byLayer.Sort();
        bool clinicPlaced = false;
        for (int l = 0; l < byLayer.Count; l++)
        {
            int layer = byLayer[l];
            var indices = new List<int>();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Layer == layer)
                {
                    indices.Add(slots[i].Index);
                }
            }

            indices.Sort();

            var pool = new List<NodeKind> { NodeKind.Clinic, NodeKind.Training, NodeKind.Event, NodeKind.Enrollment };
            rng.Shuffle(pool);

            var forced = new List<NodeKind>(2);
            if (!clinicPlaced)
            {
                forced.Add(NodeKind.Clinic);
                clinicPlaced = true;
            }

            if (l == byLayer.Count - 1)
            {
                forced.Add(NodeKind.Enrollment);
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

            for (int i = 0; i < indices.Count; i++)
            {
                kinds[layer][indices[i]] = ordered[i % ordered.Count];
            }
        }
    }

    /// <summary>
    /// Élites: 1 en el acto 1, 2 en los actos 2 y 3, en capas distintas y nunca en la apertura (capas 0 y
    /// 1), donde no habría forma de esquivarlos. Ascienden un partido de liga ya colocado, así que no
    /// cambian el número de capas con partido y no tocan RF-003b.
    /// </summary>
    private static void AssignElites(
        ref Pcg32 rng,
        Dictionary<int, NodeKind[]> kinds,
        LayerShape[] shapes,
        List<int> matchLayers,
        int act)
    {
        var candidates = new List<int>();
        for (int i = 0; i < matchLayers.Count; i++)
        {
            if (matchLayers[i] >= OpeningLayers)
            {
                candidates.Add(matchLayers[i]);
            }
        }

        rng.Shuffle(candidates);
        int elites = act == 1 ? 1 : 2;
        int placed = 0;
        for (int i = 0; i < candidates.Count && placed < elites; i++)
        {
            int layer = candidates[i];
            var league = new List<int>();
            for (int index = 0; index < shapes[layer].Width; index++)
            {
                if (kinds[layer][index] == NodeKind.LeagueMatch)
                {
                    league.Add(index);
                }
            }

            if (league.Count == 0)
            {
                continue;
            }

            kinds[layer][league[rng.Range(0, league.Count)]] = NodeKind.EliteMatch;
            placed++;
        }
    }

    /// <summary>Construye los nodos con sus aristas, tipos, rivales y distintivo de dificultad.</summary>
    private static List<MapNode> BuildNodes(
        ref Pcg32 rng,
        int act,
        LayerShape[] shapes,
        IReadOnlyList<int> markets,
        Dictionary<int, List<int>> marketLanes,
        Dictionary<int, NodeKind[]> kinds,
        IReadOnlyList<string>? opponentIds)
    {
        int layerCount = shapes.Length;
        var idsByLayer = new int[layerCount][];
        int nextId = act * NodeIdBase;
        for (int layer = 0; layer < layerCount; layer++)
        {
            idsByLayer[layer] = new int[shapes[layer].Width];
            for (int index = 0; index < shapes[layer].Width; index++)
            {
                idsByLayer[layer][index] = nextId++;
            }
        }

        var targets = new List<int>[layerCount][];
        for (int layer = 0; layer < layerCount; layer++)
        {
            targets[layer] = new List<int>[shapes[layer].Width];
            for (int index = 0; index < shapes[layer].Width; index++)
            {
                targets[layer][index] = new List<int>();
            }
        }

        for (int layer = 0; layer < layerCount - 1; layer++)
        {
            Connect(ref rng, layer, layerCount, shapes, markets, marketLanes, targets[layer], idsByLayer[layer + 1]);
        }

        var opponents = ShuffledOpponents(ref rng, opponentIds);
        int opponentCursor = 0;

        var nodes = new List<MapNode>();
        for (int layer = 0; layer < layerCount; layer++)
        {
            for (int index = 0; index < shapes[layer].Width; index++)
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
                    shapes[layer].Lane(index),
                    kind,
                    targets[layer][index],
                    opponentId,
                    Difficulty(act, kind)));
            }
        }

        return nodes;
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
    /// Conecta la capa <paramref name="layer"/> con la siguiente. Tres regímenes:
    /// <list type="bullet">
    /// <item>hacia el <b>jefe</b>, todos los nodos convergen (sin restricción de carril);</item>
    /// <item>en la <b>apertura</b> (capas 0 y 1), completo: 1 → 2 y 2 → 4;</item>
    /// <item>a partir de la capa 2, <b>solo carriles contiguos</b>: cada nodo recibe una arista
    /// obligatoria a un vecino sorteado y las demás a cara o cruz, después se repesca todo
    /// destino que se hubiera quedado sin entrada, y por último se fuerza la arista al mercado que
    /// domina el carril (paso 2 de la demostración de RF-011b).</item>
    /// </list>
    /// </summary>
    private static void Connect(
        ref Pcg32 rng,
        int layer,
        int layerCount,
        LayerShape[] shapes,
        IReadOnlyList<int> markets,
        Dictionary<int, List<int>> marketLanes,
        List<int>[] targets,
        int[] destinationIds)
    {
        var from = shapes[layer];
        var to = shapes[layer + 1];
        bool open = layer + 1 == layerCount - 1 || layer < OpeningLayers;
        if (open)
        {
            for (int i = 0; i < from.Width; i++)
            {
                for (int j = 0; j < to.Width; j++)
                {
                    targets[i].Add(destinationIds[j]);
                }
            }

            return;
        }

        for (int i = 0; i < from.Width; i++)
        {
            var candidates = Neighbours(from.Lane(i), to);
            int pick = candidates[rng.Range(0, candidates.Count)];
            targets[i].Add(destinationIds[pick - to.Start]);
            for (int c = 0; c < candidates.Count; c++)
            {
                if (candidates[c] != pick && rng.Range(0, 2) == 0)
                {
                    targets[i].Add(destinationIds[candidates[c] - to.Start]);
                }
            }
        }

        for (int j = 0; j < to.Width; j++)
        {
            bool hasEntry = false;
            for (int i = 0; i < from.Width && !hasEntry; i++)
            {
                hasEntry = targets[i].Contains(destinationIds[j]);
            }

            if (hasEntry)
            {
                continue;
            }

            var sources = Neighbours(to.Lane(j), from);
            int source = sources[rng.Range(0, sources.Count)];
            targets[source - from.Start].Add(destinationIds[j]);
        }

        if (marketLanes.TryGetValue(layer + 1, out var shops))
        {
            for (int i = 0; i < from.Width; i++)
            {
                int lane = from.Lane(i);
                bool reachesMarket = false;
                int forced = -1;
                for (int s = 0; s < shops.Count; s++)
                {
                    if (Math.Abs(shops[s] - lane) > 1)
                    {
                        continue;
                    }

                    forced = shops[s];
                    if (targets[i].Contains(destinationIds[shops[s] - to.Start]))
                    {
                        reachesMarket = true;
                        break;
                    }
                }

                if (!reachesMarket && forced >= 0)
                {
                    targets[i].Add(destinationIds[forced - to.Start]);
                }
            }
        }

        for (int i = 0; i < from.Width; i++)
        {
            targets[i].Sort();
        }
    }

    /// <summary>Carriles de <paramref name="shape"/> a distancia 1 o menos de <paramref name="lane"/>.</summary>
    private static List<int> Neighbours(int lane, LayerShape shape)
    {
        var found = new List<int>(3);
        for (int candidate = lane - 1; candidate <= lane + 1; candidate++)
        {
            if (shape.Has(candidate))
            {
                found.Add(candidate);
            }
        }

        return found;
    }

    private static int MarketLaneCount(Dictionary<int, List<int>> marketLanes, int layer) =>
        marketLanes.TryGetValue(layer, out var lanes) ? lanes.Count : 0;

    /// <summary>Pares del diccionario por capa ascendente: nunca se itera un diccionario sin ordenar (RT-041).</summary>
    private static List<KeyValuePair<int, List<int>>> Ordered(Dictionary<int, List<int>> marketLanes)
    {
        var pairs = new List<KeyValuePair<int, List<int>>>(marketLanes);
        pairs.Sort((a, b) => a.Key.CompareTo(b.Key));
        return pairs;
    }
}
