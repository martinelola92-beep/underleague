using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>Ayudantes de los tests del paquete X: catálogos reales de /data y una RunSetup lista para jugar.</summary>
internal static class SystemsTestSupport
{
    /// <summary>Catálogo compartido (perks, razas, tuning...), cargado una vez.</summary>
    public static Catalog Catalog { get; } = TestData.LoadCatalog();

    /// <summary>Los cuatro catálogos del paquete X (economía, objetos, consumibles, rivales), cargados una vez.</summary>
    public static StandardRunSystems Systems { get; } = StandardRunSystems.FromJson(TestData.LoadAllFiles());

    /// <summary>
    /// RunSetup con los rivales estáticos del paquete X (RF-015) y oro de partida suficiente para probar
    /// mercado y clínica sin depender de las recompensas.
    /// </summary>
    public static RunSetup Setup(Race clubRace = Race.Human, int startingGold = 400, int quality = 50) => new(
        "test_club",
        clubRace,
        TestData.LoadAllFiles())
    {
        StartingGold = startingGold,
        GeneratedQuality = quality,
        OpponentIdsByAct = Systems.OpponentIdsByAct(),
    };

    /// <summary>
    /// Busca un nodo real de ese tipo en cualquiera de los tres actos y devuelve el estado con él como
    /// pendiente, sin jugar nada: instrumento de test para probar mercado, clínica y recompensas de forma
    /// aislada, sin depender de ganar un partido de verdad. El nodo tiene que existir en <c>state.Maps</c>
    /// porque <c>NodeGuards</c> resuelve <c>state.GetNode(state.PendingNodeId)</c>.
    /// </summary>
    public static RunState WithFakePendingNode(RunState state, NodeKind kind)
    {
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var map = state.MapOf(act);
            var node = map.Nodes.FirstOrDefault(n => n.Kind == kind);
            if (node is not null)
            {
                return state.WithPendingNode(node.Id);
            }
        }

        throw new InvalidOperationException($"no se ha encontrado ningún nodo {kind} en los mapas de esta run");
    }

    /// <summary>Avanza hasta que haya un nodo del tipo pedido disponible y entra en él.</summary>
    public static (RunState State, MapNode Node) WalkToNode(RunState state, NodeKind kind, int maxSteps = 12)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            var nodes = RunEngine.AvailableNodes(state);
            var target = nodes.FirstOrDefault(n => n.Kind == kind);
            if (target is not null)
            {
                return (RunEngine.Enter(state, target.Id, Catalog, Systems), target);
            }

            if (nodes.Count == 0)
            {
                break;
            }

            // Evita partidos mientras se busca un nodo de servicio: entra en el primero que no sea de partido si lo hay.
            var nonMatch = nodes.FirstOrDefault(n => !n.IsMatch);
            var chosen = nonMatch ?? nodes[0];
            state = RunEngine.Enter(state, chosen.Id, Catalog, Systems);
        }

        throw new InvalidOperationException($"no se ha encontrado ningún nodo {kind} accesible en {maxSteps} pasos");
    }

    /// <summary>Avanza hasta el siguiente partido accesible y lo juega, devolviendo el estado resultante.</summary>
    public static RunState PlayNextMatch(RunState state, int maxSteps = 12)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            var nodes = RunEngine.AvailableNodes(state);
            var match = nodes.FirstOrDefault(n => n.IsMatch);
            if (match is not null)
            {
                return RunEngine.Enter(state, match.Id, Catalog, Systems);
            }

            if (nodes.Count == 0)
            {
                throw new InvalidOperationException("no quedan nodos accesibles");
            }

            state = RunEngine.Enter(state, nodes[0].Id, Catalog, Systems);
        }

        throw new InvalidOperationException($"no se ha encontrado ningún partido accesible en {maxSteps} pasos");
    }

    /// <summary>Juega la run entera con la política más simple: siempre el primer nodo accesible, cerrando cualquier nodo abierto.</summary>
    public static RunState PlayToTheEnd(RunState state, int maxNodes = 80)
    {
        for (int i = 0; i < maxNodes && !RunEngine.Outcome(state).IsOver; i++)
        {
            if (state.Phase == RunPhase.NodeOpen)
            {
                state = RunEngine.Apply(state, new LeaveNode(), Catalog, Systems);
                continue;
            }

            var nodes = RunEngine.AvailableNodes(state);
            if (nodes.Count == 0)
            {
                break;
            }

            state = RunEngine.Enter(state, nodes[0].Id, Catalog, Systems);
        }

        return state;
    }
}
