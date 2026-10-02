using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas del flujo «salir a mitad de partido y volver» (BR-A, RT-061, ADR 0183): la retransmisión recién
/// empezada, la pantalla de inicio con «Continuar» tras salir, y la retransmisión al volver, que vuelve a
/// empezar el mismo partido en vez de saltar al informe o al mapa. Además comprueba y escribe en el log lo que
/// no se ve: que el guardado de mitad de partido lleva <c>pendingMatch</c>, que es el estado de ANTES (fase en
/// el mapa), y que al continuar queda un partido por retomar.
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/CapturasReanudar.tscn
/// </code>
/// </summary>
public partial class ResumeCaptureRunner : Control
{
    private string _directory = string.Empty;

    public override void _Ready()
    {
        Nav.Suppressed = true;
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

        bool ok = true;
        run.NewRun("orc_ironworks", Race.Orc, 7UL);
        var node = FirstMatch(run);
        run.SelectedNodeId = node.Id;

        // 1. La retransmisión: BroadcastScreen._Ready llama a PlayMatch, que resuelve el partido y abre el «partido a medias».
        var first = await Show("res://Scenes/Retransmision.tscn");
        await Frames(90);
        await Save("reanudar-1-partido");
        ok &= Check("durante el partido hay un partido abierto y la run no se da por terminada", !run.IsOverNow && run.Playback is not null);

        // 2. Salir al menú principal a mitad de partido (lo que hace el menú de pausa).
        run.LeaveToMenu();
        Drop(first);
        string json = Godot.FileAccess.GetFileAsString(RunController.SavePath);
        ok &= Check("el guardado lleva pendingMatch", json.Contains("\"pendingMatch\":{", System.StringComparison.Ordinal));
        ok &= Check($"el pendingMatch apunta al nodo {node.Id}", json.Contains($"\"node\":{node.Id},", System.StringComparison.Ordinal));
        ok &= Check("el guardado es el de ANTES (fase en el mapa)", json.Contains("\"phase\":\"onMap\"", System.StringComparison.Ordinal));

        var start = await Show("res://Scenes/Inicio.tscn");
        await Save("reanudar-2-inicio");
        Drop(start);

        // 3. Continuar: queda un partido por retomar y la retransmisión lo reproduce otra vez.
        ok &= Check("Continue() devuelve true", run.Continue());
        ok &= Check("tras continuar hay un partido por retomar", run.HasMatchToResume);
        ok &= Check("el nodo del partido sigue seleccionado", run.SelectedNodeId == node.Id);
        ok &= Check("Nav.For lleva al partido, no al mapa", Nav.For(run) == Nav.MatchView);
        var second = await Show("res://Scenes/Retransmision.tscn");
        await Frames(90);
        await Save("reanudar-3-partido-reanudado");
        ok &= Check("la retransmisión retomó el partido", !run.HasMatchToResume && run.Playback is not null);
        Drop(second);

        // 4. Llegar al informe cierra el partido: el guardado pasa a ser el de después.
        run.CommitMatch();
        string after = Godot.FileAccess.GetFileAsString(RunController.SavePath);
        ok &= Check("al cerrar el partido el guardado ya no lleva pendingMatch", after.Contains("\"pendingMatch\":null", System.StringComparison.Ordinal));

        Nav.Suppressed = false;
        GetTree().Quit(ok ? 0 : 1);
    }

    private static bool Check(string what, bool value)
    {
        GD.Print($"{(value ? "OK   " : "FALLO")} {what}");
        return value;
    }

    private static MapNode FirstMatch(RunController run)
    {
        foreach (var node in run.Available())
        {
            if (node.IsMatch)
            {
                return node;
            }
        }

        throw new System.InvalidOperationException("la run no ofrece ningún nodo de partido al empezar");
    }

    private async System.Threading.Tasks.Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async System.Threading.Tasks.Task<Node> Show(string path)
    {
        var instance = GD.Load<PackedScene>(path).Instantiate();
        AddChild(instance);
        await Frames(4);
        return instance;
    }

    private void Drop(Node instance)
    {
        RemoveChild(instance);
        instance.QueueFree();
    }

    private async System.Threading.Tasks.Task Save(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_directory, name + ".png"));
        GD.Print($"captura: {name}.png");
    }
}
