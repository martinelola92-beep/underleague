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

    /// <summary>
    /// Pantalla de partido: la retransmisión con voz de pregón (ADR 0119/0120) — campo 3D, tablero, tiras
    /// y presentaciones de momento. <see cref="MatchDebug"/> es <see cref="MatchScreen"/> (vista 2D, tick
    /// a tick, log), accesible desde aquí con F3 sobre la misma <see cref="RunController.Playback"/>.
    /// </summary>
    public const string Match = "res://Scenes/Retransmision.tscn";

    /// <summary>Modo depuración de la pantalla de Partido: la vista 2D de siempre, tick a tick, con log y leyenda.</summary>
    public const string MatchDebug = "res://Scenes/Partido.tscn";

    /// <summary>Informe post-partido (RF-119). La escribe el paquete de partido.</summary>
    public const string Report = "res://Scenes/Informe.tscn";

    /// <summary>Elección de recompensa (RF-071). La escribe el paquete de partido.</summary>
    public const string Reward = "res://Scenes/Recompensa.tscn";

    /// <summary>Mercado (RF-114). La escribe el paquete de partido.</summary>
    public const string Market = "res://Scenes/Mercado.tscn";

    /// <summary>Escena que <b>falta</b> y que <see cref="Pending"/> anuncia; la lee la pantalla provisional.</summary>
    public static string Missing { get; private set; } = string.Empty;

    /// <summary>
    /// Adónde volver al salir de Equipo (AW-N): cualquier pantalla que ofrezca un botón "Ver equipo" deja
    /// aquí su propio nombre de escena antes de navegar, y el botón de vuelta de Equipo lo consume una
    /// sola vez. Vacío significa "sin desvío": Equipo decide con su lógica de siempre (Ojeo si hay un
    /// nodo elegido, si no el Mapa).
    /// </summary>
    public static string ReturnTo { get; set; } = string.Empty;

    /// <summary>
    /// Silencia la navegación (BA-L2). <c>CaptureRunner</c> instancia las pantallas del juego como HIJAS
    /// suyas, no como escena principal, así que un <see cref="Go"/> desde dentro de una de ellas cambia la
    /// escena raíz y se lleva por delante al propio arnés de capturas: a partir de ahí
    /// <c>GetTree()</c> devuelve null y el recorrido muere a mitad, sin llegar a <c>recompensa</c> ni a
    /// <c>mercado</c>.
    ///
    /// <para>Con esto puesto, una pantalla que decida navegar —por ejemplo <c>ReportScreen._Ready</c> o
    /// <c>RewardScreen._Ready</c> cuando no hay run— deja constancia en el log y se queda donde está, que
    /// es justo lo que una captura necesita: enseñar la pantalla, no irse de ella. No afecta al juego: solo
    /// lo enciende el arnés de capturas.</para>
    /// </summary>
    public static bool Suppressed { get; set; }

    /// <summary>
    /// Cambia a esa escena. Si el fichero no existe todavía, va a la pantalla provisional, que dice cuál
    /// falta y ofrece seguir: una escena que aún no está escrita no puede dejar la run bloqueada.
    /// </summary>
    public static void Go(Godot.Node from, string scene)
    {
        if (Suppressed)
        {
            GD.Print($"navegación silenciada (captura): se pedía ir a {scene}");
            return;
        }

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
            NodeKind.Clinic => Node,
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
