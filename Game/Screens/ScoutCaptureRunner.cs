using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas del mapa y del ojeo en los puntos que una run recién generada no alcanza sola. Juega con semillas
/// fijas y coloca el estado como lo dejaría el jugador: delante del nodo de jefe (BH-B: el jefe se presentaba con
/// el nombre de un clan de liga, en el mapa y en el ojeo) y con un titular que no puede jugar (BC-H: el aviso de
/// «entra X de oficio» sólo sale con alguien de baja y la secuencia normal empieza con la plantilla sana).
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/CapturasOjeo.tscn
/// </code>
/// </summary>
public partial class ScoutCaptureRunner : Control
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

        // BH-B: el mapa y el ojeo delante del jefe. Actos 1 y 3: el primero es el Taller, el último el jefe final.
        foreach (int act in new[] { 1, 3 })
        {
            run.NewRun("orc_ironworks", Race.Orc, 7UL);
            int bossId = BeforeTheBoss(run, act);
            GD.Print($"jefe del acto {act}: nodo {bossId}, opponentId guardado = '{run.State!.GetNode(bossId).OpponentId}'");

            var map = await Show("res://Scenes/Mapa.tscn");
            await Save($"mapa-jefe-acto{act}");
            Drop(map);

            run.SelectedNodeId = bossId;
            var scout = await Show("res://Scenes/Ojeo.tscn");
            await Save($"ojeo-jefe-acto{act}");
            Drop(scout);
        }

        // BC-H: el ojeo con un titular de baja. Sin este escenario el aviso de relleno no se regresiona solo.
        run.NewRun("orc_ironworks", Race.Orc, 7UL);
        var first = FirstMatch(run);
        string absent = MakeAStarterUnavailable(run);
        GD.Print($"titular de baja: {absent}");
        run.SelectedNodeId = first.Id;
        var filled = await Show("res://Scenes/Ojeo.tscn");
        await Save("ojeo-relleno");

        // RF-002d: el jugador decide dejar el hueco. Se pulsa el botón de verdad (la señal que lanza el clic), no se
        // llama al manejador; con la navegación silenciada la pantalla se queda, así que se monta otra para enseñar
        // el once que sale de la decisión.
        var leave = FindButton(filled, UiText.Get("ui.scout.leaveGap"));
        if (leave is null)
        {
            GD.PushError("el ojeo con un hueco de oficio no ofrece «Dejar el hueco vacío»");
        }
        else
        {
            leave.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print($"tras pulsar: juega sin relleno = {RunLineup.PlaysShort(run.State!)}");
        }

        Drop(filled);
        var gap = await Show("res://Scenes/Ojeo.tscn");
        await Save("ojeo-hueco");

        // Y se deshace con el botón contrario.
        var refill = FindButton(gap, UiText.Get("ui.scout.fillGap"));
        if (refill is null)
        {
            GD.PushError("el ojeo con la decisión tomada no ofrece «Que el banquillo tape el hueco»");
        }
        else
        {
            refill.EmitSignal(BaseButton.SignalName.Pressed);
            GD.Print($"tras deshacer: juega sin relleno = {RunLineup.PlaysShort(run.State!)}");
        }

        Drop(gap);

        Nav.Suppressed = false;
        GetTree().Quit(0);
    }

    /// <summary>El botón con ese texto, buscado en todo el árbol de la pantalla; null si no hay ninguno.</summary>
    private static Button? FindButton(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            if (FindButton(child, text) is { } found)
            {
                return found;
            }
        }

        return null;
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

    /// <summary>
    /// Lesión grave para un centrocampista titular, y fuera de la alineación guardada: es como lo deja un partido
    /// (<c>MatchResolution.PruneLineup</c>). Devuelve su nombre.
    /// </summary>
    private static string MakeAStarterUnavailable(RunController run)
    {
        string name = string.Empty;
        run.SeedForCapture(state =>
        {
            RunPlayer? victim = null;
            foreach (var slot in state.Lineup.Slots)
            {
                var player = state.FindPlayer(slot.PlayerId);
                if (player is { Position: Underleague.Sim.Model.Position.Midfielder })
                {
                    victim = player;
                    break;
                }
            }

            if (victim is null)
            {
                throw new System.InvalidOperationException("la alineación inicial no tiene ningún centrocampista");
            }

            name = victim.Name;
            var remaining = new System.Collections.Generic.List<LineupSlot>();
            foreach (var slot in state.Lineup.Slots)
            {
                if (slot.PlayerId != victim.Id)
                {
                    remaining.Add(slot);
                }
            }

            return state.WithPlayer(victim with { PhysicalState = PhysicalState.SevereInjury }).WithLineup(new Lineup(remaining));
        });

        return name;
    }

    /// <summary>
    /// Coloca la run en el nodo anterior al jefe del acto <paramref name="act"/>, de modo que el jefe sea
    /// accesible desde el mapa. Devuelve el id del nodo de jefe.
    /// </summary>
    private static int BeforeTheBoss(RunController run, int act)
    {
        int bossId = -1;
        run.SeedForCapture(state =>
        {
            state = state.WithAct(act);
            var map = state.CurrentMap;
            bossId = map.BossNodeId;
            foreach (var node in map.Nodes)
            {
                if (node.Next.Contains(bossId))
                {
                    return state.WithCurrentNode(node.Id);
                }
            }

            throw new System.InvalidOperationException($"el jefe del acto {act} no tiene ningún nodo anterior");
        });

        return bossId;
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
