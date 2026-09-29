using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas de la apuesta del vestuario (ADR 0157, paso 4): el ojeo con la apuesta sin tomar y tomada, el
/// mapa con la marca, y el informe con la apuesta cobrada y con la perdida. Jugando runs de verdad con
/// semillas fijas y pulsando el botón «Apostar» del propio ojeo (no llamando a nada por dentro).
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/CapturasApuesta.tscn
/// </code>
/// </summary>
public partial class BetCaptureRunner : Control
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

        // 1. Ojeo con la apuesta sin tomar y tomada, en una semilla cuya apuesta nombra a un rival.
        ulong pick = FindSeed(run, hunt: true) ?? FindSeed(run, hunt: false) ?? 0UL;
        if (pick == 0UL)
        {
            GD.PushError("ninguna semilla de 1..200 ofrece apuesta en el primer nodo de partido");
            GetTree().Quit(1);
            return;
        }

        run.NewRun("orc_ironworks", Race.Orc, pick);
        var node = FirstMatch(run);
        run.SelectedNodeId = node.Id;
        var scout = await Show("res://Scenes/Ojeo.tscn");
        await Save("ojeo-apuesta-sin-tomar");

        Press(scout, "Apostar");
        Drop(scout);
        scout = await Show("res://Scenes/Ojeo.tscn");
        await Save("ojeo-apuesta-tomada");
        Drop(scout);

        var map = await Show("res://Scenes/Mapa.tscn");
        await Save("mapa-apuesta");
        Drop(map);

        // 2. Informes: se juega cada semilla con la apuesta tomada hasta tener una cobrada y una perdida.
        bool won = false;
        bool lost = false;
        for (ulong seed = 1; seed <= 200 && !(won && lost); seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            var match = FirstMatch(run);
            if (BetSystem.OfferFor(run.State!, match, run.Engine, run.Catalog!) is null)
            {
                continue;
            }

            run.Apply(new TakeBet(match.Id));
            run.SelectedNodeId = match.Id;
            run.PlayMatch(match.Id);
            var result = run.LastMatch?.Summary.Bet;
            if (result is null || (result.Met ? won : lost))
            {
                continue;
            }

            var report = await Show("res://Scenes/Informe.tscn");
            await Save(result.Met ? "informe-apuesta-ganada" : "informe-apuesta-perdida");
            GD.Print($"informe: semilla {seed}, {result.BetId}, cumplida={result.Met}, cobro={result.GoldPaid}");
            Drop(report);
            won |= result.Met;
            lost |= !result.Met;
        }

        Nav.Suppressed = false;
        GetTree().Quit(won && lost ? 0 : 1);
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

    private static ulong? FindSeed(RunController run, bool hunt)
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            var offer = BetSystem.OfferFor(run.State!, FirstMatch(run), run.Engine, run.Catalog!);
            if (offer is not null && (!hunt || offer.Kind == BetKind.HuntTheStar))
            {
                return seed;
            }
        }

        return null;
    }

    /// <summary>Pulsa el botón con ese texto, emitiendo su señal: el mismo camino que un clic.</summary>
    private static void Press(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button && button.Text == text && !button.Disabled)
            {
                button.EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }

            Press(child, text);
        }
    }

    private async System.Threading.Tasks.Task<Node> Show(string path)
    {
        var instance = GD.Load<PackedScene>(path).Instantiate();
        AddChild(instance);
        for (int i = 0; i < 4; i++)
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

    private async System.Threading.Tasks.Task Save(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_directory, name + ".png"));
        GD.Print($"captura: {name}.png");
    }
}
