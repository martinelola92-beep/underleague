using System.Text.Json;
using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Map;

/// <summary>
/// Estructura del mapa de la run, cargada de <c>data/map/map.json</c> (D-2, D-10). Solo lleva lo que es
/// un <b>valor</b> y no una regla: cuántos nodos recorre el jugador en cada acto. Todo lo demás —dónde
/// caen los mercados, cuántas capas son de partido, dónde está el jefe— se deriva de ahí por
/// construcción en <see cref="MapGenerator"/>, y moverlo exigiría un ADR (RT-057).
/// </summary>
/// <param name="NodesPerAct">Nodos recorridos en cada acto (índice 0 = acto 1), 10..12 (RF-001, lectura W-1).</param>
public sealed record MapConfig(IReadOnlyList<int> NodesPerAct)
{
    /// <summary>Nodos recorridos en el acto indicado, 1..3.</summary>
    public int Of(int act) => act >= 1 && act <= NodesPerAct.Count
        ? NodesPerAct[act - 1]
        : throw new ArgumentOutOfRangeException(nameof(act), act, "el acto debe estar entre 1 y 3 (RF-001)");

    /// <summary>Nodos que recorre una run completa (RF-003b: entre 30 y 36).</summary>
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

    /// <summary>Configuración del mapa de la instantánea de ficheros indicada.</summary>
    public static MapConfig FromJson(IReadOnlyDictionary<string, string> files)
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
                if (nodes[i] < MapGenerator.MinPathLength || nodes[i] > MapGenerator.MaxPathLength)
                {
                    throw new DataException(
                        Path,
                        $"$.nodesPerAct[{i}]",
                        $"un acto recorre entre {MapGenerator.MinPathLength} y {MapGenerator.MaxPathLength} nodos (RF-001)");
                }
            }

            return new MapConfig(nodes);
        }
    }
}
