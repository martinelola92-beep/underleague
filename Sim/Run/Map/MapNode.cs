namespace Underleague.Sim.Run;

/// <summary>
/// Un nodo del grafo de un acto (RF-010, RF-011). Inmutable y serializable: forma parte del estado de
/// la run (RT-030) y viaja dentro del guardado.
/// </summary>
/// <param name="Id">
/// Identificador único <b>en toda la run</b>: <c>act * 100 + índice dentro del acto</c>
/// (<see cref="MapGenerator.NodeIdBase"/>). Es la clave con la que se deriva la semilla del partido
/// (<c>RngStreams.MatchSeed(runSeed, node.Id)</c>), así que no puede depender del camino recorrido:
/// entrar en el mismo nodo produce siempre el mismo partido, sea el tercero o el quinto de la run.
/// </param>
/// <param name="Act">Acto al que pertenece, 1..3.</param>
/// <param name="Layer">Capa del grafo, 0 = entrada del acto. Las aristas siempre van de <c>Layer</c> a <c>Layer + 1</c> (RF-010: sin retroceso).</param>
/// <param name="IndexInLayer">
/// <b>Carril</b> del nodo, 0 arriba y <c>MapGenerator.Lanes - 1</c> abajo (ADR 0053). No es un índice
/// correlativo dentro de la capa: es una posición fija en el mapa, la misma en todas las capas, y por eso
/// vale a la vez para dibujar (la altura del nodo) y para la regla de movimiento (solo se va a un carril
/// contiguo). Una capa ocupa siempre un intervalo contiguo de carriles.
/// </param>
/// <param name="Kind">Tipo de nodo (RF-011).</param>
/// <param name="Next">Ids de los nodos alcanzables en un salto, en orden ascendente. Vacío solo en el nodo de jefe.</param>
/// <param name="OpponentId">
/// Id del rival estático asignado a este nodo (RF-015, <c>data/rivals/</c>). Cadena vacía si el nodo no
/// es de partido o si el llamador no pasó catálogo de rivales: en ese caso el rival lo produce
/// <see cref="IRunSystems.OpponentFor"/> (el paquete X lo sustituye por los rivales de datos).
/// </param>
/// <param name="Difficulty">Distintivo de dificultad de 5 niveles (RF-012). 0 si el nodo no es de partido.</param>
public sealed record MapNode(
    int Id,
    int Act,
    int Layer,
    int IndexInLayer,
    NodeKind Kind,
    IReadOnlyList<int> Next,
    string OpponentId,
    int Difficulty)
{
    /// <summary>True si el nodo se resuelve jugando un partido (liga, élite o jefe).</summary>
    public bool IsMatch => NodeKinds.IsMatch(Kind);
}

/// <summary>
/// Mapa de un acto: grafo dirigido por capas, sin retroceso (RF-010), con un único nodo de jefe en la
/// última capa (RF-001). Lo genera <see cref="MapGenerator"/> con el flujo <c>RngStreams.Map</c> (RT-022).
/// </summary>
/// <param name="Act">Acto, 1..3.</param>
/// <param name="Nodes">Nodos ordenados por <see cref="MapNode.Id"/> ascendente (= capa, luego índice en capa).</param>
/// <param name="EntryNodeIds">Nodos de la capa 0. Desde la ADR 0053 es <b>uno solo</b>: el acto empieza siempre en el mismo nodo y bifurca a partir de ahí (1 -> 2 -> 4).</param>
/// <param name="BossNodeId">Nodo de jefe. Visible desde el principio del acto (RF-014).</param>
/// <param name="BossModifierId">
/// Modificador de regla del jefe (RF-001b, <c>data/bosses/</c>). Cadena vacía mientras el paquete Y no lo
/// asigne. Permanece oculto hasta llegar al nodo (RF-014): quien decide qué enseñar es la interfaz, y
/// <see cref="BossModifierRevealed"/> es el dato que consulta.
/// </param>
/// <param name="BossModifierRevealed">True una vez que el jugador ha llegado al nodo de jefe (RF-014, RF-014b).</param>
public sealed record ActMap(
    int Act,
    IReadOnlyList<MapNode> Nodes,
    IReadOnlyList<int> EntryNodeIds,
    int BossNodeId,
    string BossModifierId,
    bool BossModifierRevealed)
{
    /// <summary>Nodo con ese id; null si no está en este acto.</summary>
    public MapNode? Find(int nodeId)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Id == nodeId)
            {
                return Nodes[i];
            }
        }

        return null;
    }

    /// <summary>Nodo con ese id; lanza si no está en este acto.</summary>
    public MapNode Get(int nodeId) =>
        Find(nodeId) ?? throw new ArgumentOutOfRangeException(nameof(nodeId), $"el acto {Act} no tiene ningún nodo con id {nodeId}");

    /// <summary>Copia del mapa con el modificador de jefe asignado (paquete Y).</summary>
    public ActMap WithBossModifier(string modifierId) => this with { BossModifierId = modifierId };

    /// <summary>Copia del mapa con el modificador de jefe revelado (RF-014b).</summary>
    public ActMap WithBossModifierRevealed(bool revealed) => this with { BossModifierRevealed = revealed };

    /// <summary>Copia del mapa sustituyendo un nodo por otro con el mismo id (asignación de rival, por ejemplo).</summary>
    public ActMap WithNode(MapNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var nodes = new List<MapNode>(Nodes.Count);
        bool found = false;
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (Nodes[i].Id == node.Id)
            {
                nodes.Add(node);
                found = true;
            }
            else
            {
                nodes.Add(Nodes[i]);
            }
        }

        if (!found)
        {
            throw new ArgumentOutOfRangeException(nameof(node), $"el acto {Act} no tiene ningún nodo con id {node.Id}");
        }

        return this with { Nodes = nodes };
    }
}
