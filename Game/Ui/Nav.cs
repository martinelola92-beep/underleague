using Godot;
using Underleague.Game.Autoload;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// Navegación entre pantallas. Un solo sitio decide <b>qué pantalla toca</b> a partir del estado de la
/// run, para que ninguna pantalla tenga que saber quién viene después de ella: cada una termina llamando
/// a <see cref="Route"/> y el estado manda.
/// <para>
/// Las pantallas se citan <b>por nombre de escena</b>. Las que todavía no existen —las de partido,
/// informe, recompensa y mercado, que se escriben en paralelo— no rompen la navegación: se sustituyen por
/// <see cref="Pending"/>, que dice qué falta y deja continuar. Cuando el fichero aparezca, la ruta lo
/// encuentra sin tocar nada.
/// </para>
/// </summary>
public static class Nav
{
    public const string Start = "res://Scenes/Inicio.tscn";
    public const string Map = "res://Scenes/Mapa.tscn";
    public const string Scout = "res://Scenes/Ojeo.tscn";
    public const string Team = "res://Scenes/Equipo.tscn";
    public const string Node = "res://Scenes/Nodo.tscn";
    public const string End = "res://Scenes/FinDeRun.tscn";
    public const string Pending = "res://Scenes/Pendiente.tscn";

    /// <summary>Pantalla de partido (marcador, resultado y log). La escribe el paquete de partido.</summary>
    public const string Match = "res://Scenes/Partido.tscn";

    /// <summary>Informe post-partido (RF-119). La escribe el paquete de partido.</summary>
    public const string Report = "res://Scenes/Informe.tscn";

    /// <summary>Elección de recompensa (RF-071). La escribe el paquete de partido.</summary>
    public const string Reward = "res://Scenes/Recompensa.tscn";

    /// <summary>Mercado (RF-114). La escribe el paquete de partido.</summary>
    public const string Market = "res://Scenes/Mercado.tscn";

    /// <summary>Escena que <b>falta</b> y que <see cref="Pending"/> anuncia; la lee la pantalla provisional.</summary>
    public static string Missing { get; private set; } = string.Empty;

    /// <summary>
    /// Cambia a esa escena. Si el fichero no existe todavía, va a la pantalla provisional, que dice cuál
    /// falta y ofrece seguir: una escena que aún no está escrita no puede dejar la run bloqueada.
    /// </summary>
    public static void Go(Godot.Node from, string scene)
    {
        if (!ResourceLoader.Exists(scene))
        {
            GD.Print($"pantalla pendiente: {scene}");
            Missing = scene;
            from.GetTree().ChangeSceneToFile(Pending);
            return;
        }

        Missing = string.Empty;
        from.GetTree().ChangeSceneToFile(scene);
    }

    /// <summary>
    /// La pantalla que le corresponde al estado actual de la run. Es la única regla de navegación del
    /// juego, y se lee de arriba abajo: run terminada, nodo abierto, y si no, el mapa.
    /// </summary>
    public static string For(RunController run)
    {
        if (!run.HasRun)
        {
            return Start;
        }

        if (run.Outcome().IsOver)
        {
            return End;
        }

        var state = run.State!;
        if (state.Phase != RunPhase.NodeOpen || state.PendingNodeId < 0)
        {
            return Map;
        }

        var node = state.GetNode(state.PendingNodeId);

        // Un nodo de partido que sigue abierto después de jugarse es la recompensa de RF-071.
        if (node.IsMatch)
        {
            return Reward;
        }

        return node.Kind switch
        {
            NodeKind.Market => Market,
            NodeKind.Clinic or NodeKind.Enrollment => Node,
            _ => Node,
        };
    }

    /// <summary>Va a la pantalla que le corresponde al estado (<see cref="For"/>).</summary>
    public static void Route(Godot.Node from)
    {
        var run = RunController.Instance;
        Go(from, run is null ? Start : For(run));
    }
}
