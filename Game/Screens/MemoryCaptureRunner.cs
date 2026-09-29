using System.Collections.Generic;
using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Knavall;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using FileAccess = Godot.FileAccess;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas de «la memoria se ve» (ADR 0163, RF-119, RF-122): el informe con un apodo ganado y las
/// estadísticas por jugador, la ficha de Equipo con el apodo y las cifras de la run, y la Gaceta de una
/// victoria y de una derrota con caídos. Jugando un partido de verdad sobre una run con carreras
/// preparadas (se escribe el guardado y se retoma con <c>Continue</c>, el mismo camino que un jugador).
/// <code>
/// xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
///   --rendering-driver opengl3 --audio-driver Dummy res://Scenes/CapturasMemoria.tscn
/// </code>
/// </summary>
public partial class MemoryCaptureRunner : Control
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

        int failures = 0;

        // 1. Informe con un apodo ganado: todos los titulares llegan a un paso de un apodo.
        bool reported = false;
        for (ulong seed = 1; seed <= 40 && !reported; seed++)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            Forge(run, state => PrimeCareers(state, run));
            var match = FirstMatch(run);
            run.SelectedNodeId = match.Id;
            run.PlayMatch(match.Id);
            var post = run.PostMatch();
            if (post is null || post.NicknamesEarned.Count == 0)
            {
                continue;
            }

            var report = await Show("res://Scenes/Informe.tscn");
            await Save("memoria-informe-apodo");
            GD.Print($"informe: semilla {seed}, apodos ganados {post.NicknamesEarned.Count}");
            Drop(report);
            reported = true;

            // 2. La ficha de Equipo con esa carrera: el primer jugador del banquillo o titular.
            var team = await Show("res://Scenes/Equipo.tscn");
            var row = FindRow(team, skip: 1);
            if (row is not null)
            {
                await Click(row.GetGlobalRect().GetCenter());
            }

            await Save("memoria-ficha");
            Drop(team);
        }

        if (!reported)
        {
            GD.PushError("ninguna semilla de 1..40 dio un apodo ganado en el primer partido");
            failures++;
        }

        // 3. La Gaceta de una victoria y de una derrota con caídos.
        foreach (var (name, outcome) in new[]
        {
            ("memoria-gaceta-victoria", new RunOutcome(RunOutcomeKind.Victory)),
            ("memoria-gaceta-derrota", new RunOutcome(RunOutcomeKind.Defeat, DefeatCause.BossMatchLost, 5)),
        })
        {
            run.NewRun("orc_ironworks", Race.Orc, 7UL);
            Forge(run, state => Ended(state, run, outcome));
            var end = await Show("res://Scenes/FinDeRun.tscn");
            await Save(name);
            Drop(end);
        }

        Nav.Suppressed = false;
        GetTree().Quit(failures);
    }

    /// <summary>Carreras a un paso de un apodo: el primer gol, entrada ganada o falta cruza un umbral.</summary>
    private static RunState PrimeCareers(RunState state, RunController run)
    {
        // Los umbrales salen del catálogo (el censo de la ADR 0163 los mueve): un paso por debajo de cada uno.
        int Below(string id) => System.Math.Max(0, run.Systems!.Nicknames.Find(id)!.Threshold - 1);
        for (int i = 0; i < state.Roster.Count; i++)
        {
            var player = state.Roster[i] with
            {
                Career = RunCareer.None with { Matches = 3, Goals = Below("golden_boots"), Assists = Below("delivery_boy"), Cards = Below("collector"), Fouls = Below("grubby"), InjuriesCaused = Below("butcher"), TacklesWon = Below("wall") },
            };
            state = state.WithPlayer(player);
        }

        return state;
    }

    /// <summary>Una run terminada con un MVP claro, dos caídos y un rival que les hizo daño.</summary>
    private static RunState Ended(RunState state, RunController run, RunOutcome outcome)
    {
        state = state.WithPlayer(state.Roster[0] with { Career = RunCareer.None with { Matches = 9, Goals = 6, Assists = 2, TacklesWon = 16, InjuriesCaused = 3 } });
        state = state.WithPlayer(state.Roster[1] with { PhysicalState = PhysicalState.Dead, Career = RunCareer.None with { Matches = 4, Goals = 1, Fouls = 7 } });
        state = state.WithPlayer(state.Roster[2] with { PhysicalState = PhysicalState.Dead, Career = RunCareer.None with { Matches = 6, Assists = 3, InjuriesSuffered = 2 } });
        string clan = run.Systems!.Rivals.All[0].Id;
        var counters = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var (key, value) in state.Counters)
        {
            counters[key] = value;
        }

        counters[$"{RunState.RivalCreditPrefix}{clan}:1:{state.Roster[1].Id}:sufferedDeath"] = 1;
        counters[$"{RunState.RivalCreditPrefix}{clan}:1:{state.Roster[3].Id}:sufferedInjury"] = 2;
        return state with { Counters = counters, Result = outcome };
    }

    /// <summary>Escribe el estado editado como guardado y lo retoma: el camino de «Continuar».</summary>
    private static void Forge(RunController run, System.Func<RunState, RunState> edit)
    {
        using (var file = FileAccess.Open(RunController.SavePath, FileAccess.ModeFlags.Write))
        {
            file.StoreString(RunSave.Save(edit(run.State!)));
        }

        if (!run.Continue())
        {
            throw new System.InvalidOperationException("no se pudo retomar el guardado forjado");
        }
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

    private static RosterRow? FindRow(Node root, int skip)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is RosterRow row && row.PlayerId >= 0 && skip-- <= 0)
            {
                return row;
            }

            if (FindRow(child, skip) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private async System.Threading.Tasks.Task Click(Vector2 at)
    {
        var visible = GetViewport().GetVisibleRect().Size;
        var window = (Vector2)DisplayServer.WindowGetSize();
        var physical = visible.X <= 0f || visible.Y <= 0f ? at : new Vector2(at.X * window.X / visible.X, at.Y * window.Y / visible.Y);
        foreach (bool pressed in new[] { true, false })
        {
            GetViewport().PushInput(new InputEventMouseButton { Position = physical, GlobalPosition = physical, ButtonIndex = MouseButton.Left, Pressed = pressed });
        }

        for (int i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
