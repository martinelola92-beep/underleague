using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas de los némesis (ADR 0165): el ojeo con un némesis en el clan rival, el mapa con la marca del
/// nodo donde juega, el informe con un némesis recién nacido y el informe con una venganza. Juega runs de
/// verdad con semillas fijas; sólo el ojeo y el mapa siembran la memoria (<c>SeedForCapture</c>), porque un mapa
/// recién generado no ha matado a nadie todavía. Los informes salen de partidos jugados por el motor.
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/CapturasNemesis.tscn
/// </code>
/// </summary>
public partial class NemesisCaptureRunner : Control
{
    private const int MaxSeeds = 250;

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

        // 1. Ojeo y mapa con un némesis sembrado en el clan del primer partido.
        run.NewRun("orc_ironworks", Race.Orc, 7UL);
        var first = FirstMatch(run);
        SeedNemesis(run, first, slot: 3, title: "brother_slayer");
        run.SelectedNodeId = first.Id;
        var scout = await Show("res://Scenes/Ojeo.tscn");
        await Save("ojeo-nemesis");
        Drop(scout);

        var map = await Show("res://Scenes/Mapa.tscn");
        await Save("mapa-nemesis");
        Drop(map);

        // 2. Informe con un némesis recién nacido: el primer partido de una semilla cuyo rival mata a alguien. Sin
        //    memoria sembrada: el némesis tiene que nacer del propio partido.
        bool made = false;
        for (ulong seed = 1; seed <= MaxSeeds && !made; seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            var match = FirstMatch(run);
            run.SelectedNodeId = match.Id;
            run.PlayMatch(match.Id);
            if (run.LastMatch?.Summary is { } summary && summary.NemesesMade.Count > 0)
            {
                var report = await Show("res://Scenes/Informe.tscn");
                await Save("informe-nemesis");
                GD.Print($"informe némesis: semilla {seed}, {summary.NemesesMade[0].Name}, {summary.NemesesMade[0].TitleId}");
                Drop(report);
                made = true;
            }
        }

        // 3. Informe con una venganza: dos némesis sembrados en puestos titulares, para que alguno caiga.
        bool revenge = false;
        for (ulong seed = 1; seed <= MaxSeeds && !revenge; seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            var match = FirstMatch(run);
            SeedNemesis(run, match, slot: 3, title: "quiet_one");
            SeedNemesis(run, match, slot: 4, title: "bill_collector", id: 2);
            run.SelectedNodeId = match.Id;
            run.PlayMatch(match.Id);
            if (run.LastMatch?.Summary is { } summary && summary.Revenges.Count > 0)
            {
                var report = await Show("res://Scenes/Informe.tscn");
                await Save("informe-venganza");
                GD.Print($"informe venganza: semilla {seed}, {summary.Revenges[0].AvengerName}, muerto={summary.Revenges[0].Slain}");
                Drop(report);
                revenge = true;
            }
        }

        if (!made || !revenge)
        {
            GD.PushError($"capturas incompletas en {MaxSeeds} semillas: némesis={made}, venganza={revenge}");
        }

        Nav.Suppressed = false;
        GetTree().Quit(made && revenge ? 0 : 1);
    }

    /// <summary>Siembra un némesis vivo en el clan del rival de ese nodo, en un puesto concreto.</summary>
    private static void SeedNemesis(RunController run, MapNode node, int slot, string title, int id = 1)
    {
        var team = run.Systems!.Rivals.Find(node.OpponentId)!;
        run.SeedForCapture(state =>
        {
            var victim = state.Roster[0];
            var nemesis = new RivalNemesis(
                id, title, team.Players[slot].Name, team.Players[slot].Position, team.ClanId, slot, team.ClanId, slot,
                victim.Name, victim.Id, 1, 1, NemesisStatus.Active);
            return state.WithRivalMemory(state.RivalMemory.WithNemesis(nemesis));
        });
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
