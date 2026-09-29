using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Map;

/// <summary>
/// Estructura del mapa de la run, cargada de <c>data/map/map.json</c> (D-2, D-10). Solo lleva lo que es
/// un <b>valor</b> y no una regla: cuántos nodos recorre el jugador en cada acto. Todo lo demás —dónde
/// caen los mercados, cuántas capas son de partido, dónde está el jefe— se deriva de ahí por
/// construcción en <see cref="MapGenerator"/>, y moverlo exigiría un ADR (RT-057).
/// </summary>
/// <param name="NodesPerAct">Nodos recorridos en cada acto (índice 0 = acto 1), 8..10 en los datos actuales (RF-001, lectura W-1, enmendado por la ADR 0170); 8..12 en la instantánea de una run guardada (<see cref="MapGenerator.SnapshotMaxPathLength"/>).</param>
/// <param name="EliteRivalLevelBonus">
/// ADR 0043: niveles que el rival de un nodo de élite juega por encima del de liga del mismo acto. Es el
/// <b>más riesgo</b> del élite —su más premio está en <c>economy.nodeRewards</c>—, y no inventa una
/// dificultad nueva: sube al rival del acto con la progresión de RF-027, que es la misma escalera por la
/// que sube el jugador.
/// </param>
public sealed record MapConfig(IReadOnlyList<int> NodesPerAct, int EliteRivalLevelBonus = 0)
{
    /// <summary>Nodos recorridos en el acto indicado, 1..3.</summary>
    public int Of(int act) => act >= 1 && act <= NodesPerAct.Count
        ? NodesPerAct[act - 1]
        : throw new ArgumentOutOfRangeException(nameof(act), act, "el acto debe estar entre 1 y 3 (RF-001)");

    /// <summary>Nodos que recorre una run completa (RF-003b, ADR 0170: entre 24 y 30 con los datos actuales; 30-36 en las runs guardadas antes).</summary>
    public int TotalNodes
    {
        get
        {
            int total = 0;
            for (int i = 0; i < NodesPerAct.Count; i++)
            {
                total += NodesPerAct[i];
            }

            return total;
        }
    }
}

/// <summary>Carga <c>data/map/map.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class MapLoader
{
    private const string Path = "map/map.json";

    /// <summary>
    /// Configuración del mapa de la instantánea de ficheros indicada. Con <paramref name="fromRunSnapshot"/> a
    /// <c>false</c> (el <c>/data</c> vigente) exige el rango de diseño de RF-001 tras la ADR 0170: 8-10 nodos por
    /// acto, y al menos <see cref="MapGenerator.MinPathLengthForTwoElites"/> en los actos 2 y 3. Con <c>true</c> (la
    /// instantánea de <c>/data</c> que lleva una run guardada, RT-061b) acepta el rango histórico
    /// 8-<see cref="MapGenerator.SnapshotMaxPathLength"/> sin la exigencia de élites: una run empezada con 11/12/12
    /// nodos por acto se retoma con las reglas con las que empezó, y sus mapas ya generados viven en el guardado.
    /// </summary>
    public static MapConfig FromJson(IReadOnlyDictionary<string, string> files, bool fromRunSnapshot = false)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            throw new DataException(Path, "$", "fichero requerido ausente");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(Path, "$", $"JSON inválido: {ex.Message}");
        }

        using (document)
        {
            var root = Json.Root(Path, document);
            var nodes = new List<int>(RunRules.Acts);
            foreach (var item in root.Prop("nodesPerAct").EnumerateArray())
            {
                nodes.Add(item.AsInt());
            }

            if (nodes.Count != RunRules.Acts)
            {
                throw new DataException(Path, "$.nodesPerAct", $"debe tener exactamente {RunRules.Acts} valores, uno por acto (RF-001)");
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                int max = fromRunSnapshot ? MapGenerator.SnapshotMaxPathLength : MapGenerator.MaxPathLength;
                if (nodes[i] < MapGenerator.MinPathLength || nodes[i] > max)
                {
                    throw new DataException(
                        Path,
                        $"$.nodesPerAct[{i}]",
                        $"un acto recorre entre {MapGenerator.MinPathLength} y {max} nodos (RF-001, ADR 0170)");
                }

                if (!fromRunSnapshot && i >= 1 && nodes[i] < MapGenerator.MinPathLengthForTwoElites)
                {
                    throw new DataException(
                        Path,
                        $"$.nodesPerAct[{i}]",
                        $"los actos 2 y 3 recorren al menos {MapGenerator.MinPathLengthForTwoElites} nodos: con menos, AssignElites puede dejar un solo élite (ADR 0170)");
                }
            }

            return new MapConfig(nodes, root.Prop("eliteRivalLevelBonus").AsInt());
        }
    }
}
