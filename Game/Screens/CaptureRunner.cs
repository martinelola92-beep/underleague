using System.Collections.Generic;
using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Regenera las capturas de las cuatro pantallas del partido —Partido, Informe, Recompensa y Mercado—
/// jugando una run de verdad con una semilla fija. No es una pantalla del juego: es el equivalente de la
/// secuencia de capturas de <c>TeamScreen</c> para las pantallas que no son la de Equipo, y existe porque
/// sin editor gráfico (<c>docs/entorno.md</c>) la única forma de juzgar la composición es mirarlas.
/// <para>
/// Las pantallas no se falsean: se instancian sus escenas de verdad, con el <see cref="RunController"/>
/// del proyecto y el estado que deja una run jugada hasta ese punto. Los estados que hacen falta un clic
/// —una opción de recompensa elegida, un objeto del mercado abierto— se alcanzan <b>empujando eventos de
/// ratón sintéticos</b> por el mismo camino que la entrada real, no llamando a los métodos por dentro.
/// </para>
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/Capturas.tscn
/// </code>
/// </summary>
public partial class CaptureRunner : Control
{
    /// <summary>Semilla fija: las capturas tienen que enseñar siempre la misma run (RT-021).</summary>
    private const ulong Seed = 20260905UL;

    private string _directory = string.Empty;

    public override void _Ready()
    {
        _directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(_directory);
        _ = Capture();
    }

    private async System.Threading.Tasks.Task Capture()
    {
        var run = RunController.Instance;
        if (run is null)
        {
            GD.PushError("no hay RunController: la escena de capturas necesita el autoload del proyecto");
            GetTree().Quit(1);
            return;
        }

        run.NewRun(Race.Orc, Seed);

        int matchNode = FirstOfKind(run, node => node.IsMatch);
        if (matchNode < 0)
        {
            GD.PushError("la run no ofrece ningún nodo de partido en el arranque del acto 1");
            GetTree().Quit(1);
            return;
        }

        run.SelectedNodeId = matchNode;

        // 1. Partido: el log cayendo, con el marcador al día del último suceso revelado.
        var match = await Show("res://Scenes/Partido.tscn", frames: 150);
        await Save("partido");
        Drop(match);

        // 2. Informe post-partido.
        var report = await Show("res://Scenes/Informe.tscn");
        await Save("informe");
        Drop(report);

        // 3. Recompensa, con la primera opción elegida para que se vea la asignación a un jugador.
        var reward = await Show("res://Scenes/Recompensa.tscn");
        await Click(new Vector2(60f, 96f));
        await Save("recompensa");

        Drop(reward);

        // Prueba de humo de la asignación: se cobra la recompensa de verdad con el portador que la vista
        // declara elegible. No se hace pulsando el botón porque cobrar la última elección del nodo lo
        // cierra y navega al mapa, y eso se llevaría por delante a esta escena a mitad del recorrido.
        int perksBefore = PerkCount(run);
        var view = run.Reward();
        if (view is { Options.Count: > 0 })
        {
            for (int i = 0; i < view.Options.Count; i++)
            {
                var option = view.Options[i];
                if (option.Block != Sim.Run.View.RewardBlock.None)
                {
                    continue;
                }

                run.Apply(new ChooseReward(i, option.Carriers.Count > 0 ? option.Carriers[0].PlayerId : -1));
                break;
            }
        }

        GD.Print($"recompensa cobrada: perks {perksBefore} -> {PerkCount(run)}");

        // 4. Mercado: se rechazan las recompensas pendientes, se cierra el nodo y se camina hasta la
        //    tienda, que RF-011b garantiza a dos saltos como máximo.
        ResolveRewards(run);
        int marketNode = WalkToMarket(run);
        if (marketNode < 0)
        {
            GD.PushError("no se alcanzó ningún nodo de mercado en el acto 1");
        }
        else
        {
            var market = await Show("res://Scenes/Mercado.tscn");
            await Click(new Vector2(700f, 92f));
            await Save("mercado");

            // Prueba de humo de la compra: el botón de comprar del objeto elegido.
            int goldBefore = run.State!.Gold;
            await Click(new Vector2(549f, 679f));
            GD.Print($"compra: oro {goldBefore} -> {run.State!.Gold}");
            Drop(market);
        }

        GetTree().Quit();
    }

    /// <summary>Instancia la escena de una pantalla y espera a que se estabilice.</summary>
    private async System.Threading.Tasks.Task<Node> Show(string path, int frames = 4)
    {
        var scene = GD.Load<PackedScene>(path);
        var instance = scene.Instantiate();
        AddChild(instance);
        for (int i = 0; i < frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        return instance;
    }

    private void Drop(Node instance)
    {
        RemoveChild(instance);
        instance.QueueFree();
    }

    /// <summary>Un clic de ratón por el mismo camino que la entrada real, para no llamar a nada por dentro.</summary>
    private async System.Threading.Tasks.Task Click(Vector2 at)
    {
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = at,
            GlobalPosition = at,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        });
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = at,
            GlobalPosition = at,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        });

        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async System.Threading.Tasks.Task Save(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(Path.Combine(_directory, name + ".png"));
        GD.Print($"captura: {name}.png");
    }

    /// <summary>Perks asignados en toda la plantilla: la forma más simple de ver si una recompensa entró.</summary>
    private static int PerkCount(RunController run)
    {
        int count = 0;
        foreach (var player in run.State!.Roster)
        {
            count += player.Perks.Count;
        }

        return count;
    }

    private static int FirstOfKind(RunController run, System.Func<MapNode, bool> predicate)
    {
        foreach (var node in run.Available())
        {
            if (predicate(node))
            {
                return node.Id;
            }
        }

        return -1;
    }

    /// <summary>Rechaza las recompensas que el nodo de partido haya dejado abiertas y cierra el nodo.</summary>
    private static void ResolveRewards(RunController run)
    {
        for (int guard = 0; guard < 4 && run.Reward() is not null; guard++)
        {
            run.Apply(new DeclineReward());
        }

        if (run.State is { Phase: RunPhase.NodeOpen, PendingNodeId: >= 0 })
        {
            run.Apply(new LeaveNode());
        }
    }

    /// <summary>
    /// Camina por el mapa hasta el primer nodo de mercado. RF-011b garantiza uno a dos saltos, así que el
    /// paseo es corto; los nodos intermedios se resuelven y se cierran sin tocar nada.
    /// </summary>
    private static int WalkToMarket(RunController run)
    {
        for (int hop = 0; hop < 6; hop++)
        {
            var available = new List<MapNode>(run.Available());
            if (available.Count == 0)
            {
                return -1;
            }

            foreach (var node in available)
            {
                if (node.Kind == NodeKind.Market)
                {
                    run.Enter(node.Id);
                    return node.Id;
                }
            }

            run.Enter(available[0].Id);
            ResolveRewards(run);
            if (run.Outcome().IsOver)
            {
                return -1;
            }
        }

        return -1;
    }
}
