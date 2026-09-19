using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Capturas de la retransmisión (ADR 0119/0120): como <see cref="CaptureRunner"/> pero de la pantalla
/// nueva. Un solo partido casi nunca tiene los siete tipos de momento a la vez (RA-020: roja, muerte y
/// decisión son sucesos raros a propósito, RF-012d), así que primero se <b>sondea</b> una lista fija de
/// semillas —sin Godot, con <see cref="MatchPlaybacks.Of"/> y <see cref="MatchMomentView.Build"/>
/// directamente, igual de deterministas que jugar un partido de verdad (RT-021)— hasta encontrar uno de
/// cada tipo, y solo después se abre <c>Retransmision.tscn</c> una vez por cada semilla que hizo falta,
/// llevando la pantalla a cada momento con <see cref="BroadcastScreen.SeekTo"/> — la API pública que deja
/// al director presentarlo sin que esta clase decida nada del partido (RT-014). Un tipo que ninguna de las
/// semillas de la lista tenga se anota con <see cref="GD.Print(string)"/> y se salta: no es un fallo, es
/// un dato de esas semillas.
/// </summary>
public partial class BroadcastCapture : Control
{
    /// <summary>
    /// Semillas fijas, en orden de intento (RT-021: nada de <c>System.Random</c>, todas deterministas).
    /// La primera es la de <see cref="CaptureRunner"/> — sigue produciendo lesión grave y turba, así que
    /// se prueba primero — el resto son solo enteros fijos sin significado, hasta encontrar roja, muerte
    /// y decisión, que esa semilla no tiene.
    /// </summary>
    private static readonly ulong[] Seeds =
    {
        20260905UL, 20260906UL, 20260907UL, 20260908UL, 1UL, 2UL, 3UL, 7UL, 42UL, 99UL, 12345UL, 777UL,
    };

    private static readonly (string Label, Func<MatchMoment, bool> Matches)[] Kinds =
    {
        // HasGoal y no FullTime: un gol de oro que termina el partido en el mismo momento se presenta
        // como acta, no como estandarte de gol (regla del director) — para la captura del estandarte
        // interesa un gol "normal", no el que coincide con el final.
        ("gol", static m => m.HasGoal && m.Kind != MomentKind.FullTime),
        ("roja", static m => m.Kind == MomentKind.Red && !m.Cancelled),
        ("lesion", static m => m.Kind == MomentKind.SevereInjury && !m.Cancelled),
        ("turba", static m => m.Kind is MomentKind.Mob or MomentKind.RefereeLeaves && !m.Cancelled),
        ("muerte", static m => m.Kind == MomentKind.Death && !m.Cancelled),
        ("final", static m => m.Kind == MomentKind.FullTime),
        ("decision", static m => m.Decision),
    };

    private string _directory = string.Empty;

    public override void _Ready()
    {
        // BA-L2 (ver CaptureRunner): la escena se instancia como hija de este nodo, así que se silencia la
        // navegación mientras dura la captura.
        Nav.Suppressed = true;
        _directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(_directory);
        _ = Capture();
    }

    private async Task Capture()
    {
        var run = RunController.Instance;
        if (run is null)
        {
            GD.PushError("no hay RunController: la escena de capturas necesita el autoload del proyecto");
            GetTree().Quit(1);
            return;
        }

        // 1. Sondeo puro, sin Godot: para cada semilla, el primer nodo de partido del acto 1 y sus
        // momentos ya clasificados. Cuando un tipo ya tiene semilla asignada no se vuelve a buscar: gana
        // siempre la primera semilla de la lista que lo tenga.
        var found = new Dictionary<string, (ulong Seed, int Node, int Frame)>();
        ulong? baseSeed = null;
        int baseNode = -1;

        foreach (var seed in Seeds)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            int node = FirstOfKind(run, n => n.IsMatch);
            if (node < 0)
            {
                continue;
            }

            baseSeed ??= seed;
            if (baseSeed == seed)
            {
                baseNode = node;
            }

            var playback = MatchPlaybacks.Of(run.State!, node, run.Catalog!, run.Engine, trace: true, MatchDecisions.None);
            var moments = MatchMomentView.Build(playback.Setup, playback.Result, run.Catalog!).Moments;
            foreach (var (label, matches) in Kinds)
            {
                if (found.ContainsKey(label))
                {
                    continue;
                }

                for (int i = 0; i < moments.Count; i++)
                {
                    if (matches(moments[i]))
                    {
                        found[label] = (seed, node, moments[i].Frame);
                        break;
                    }
                }
            }
        }

        if (baseSeed is null)
        {
            GD.PushError("ninguna semilla de la lista ofrece un nodo de partido en el arranque del acto 1");
            GetTree().Quit(1);
            return;
        }

        foreach (var (label, _) in Kinds)
        {
            if (found.TryGetValue(label, out var hit))
            {
                GD.Print($"retransmisión: '{label}' en la semilla {hit.Seed}, nodo {hit.Node}, fotograma {hit.Frame}");
            }
            else
            {
                GD.Print($"retransmisión: ninguna de las {Seeds.Length} semillas probadas tiene un momento de tipo '{label}'; se salta la captura");
            }
        }

        // 2. Una pasada de Godot por cada semilla distinta que hizo falta (normalmente 2-4, no las 12): la
        // base siempre entra, aunque no aporte ningún tipo especial.
        var bySeed = new Dictionary<ulong, List<(string Label, int Frame)>>
        {
            [baseSeed.Value] = new(),
        };
        foreach (var (label, hit) in found)
        {
            if (!bySeed.TryGetValue(hit.Seed, out var list))
            {
                list = new List<(string Label, int Frame)>();
                bySeed[hit.Seed] = list;
            }

            list.Add((label, hit.Frame));
        }

        bool savedBase = false;
        foreach (var (seed, labels) in bySeed)
        {
            run.NewRun("orc_ironworks", Race.Orc, seed);
            int node = seed == baseSeed ? baseNode : FirstOfKind(run, n => n.IsMatch);
            run.SelectedNodeId = node;

            var instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
            if (instance is not BroadcastScreen screen)
            {
                GD.PushError("res://Scenes/Retransmision.tscn no instancia BroadcastScreen: no hay capturas");
                GetTree().Quit(1);
                return;
            }

            if (!savedBase)
            {
                await Save("retrans-base");
                savedBase = true;
            }

            foreach (var (label, frame) in labels)
            {
                screen.SeekTo(frame);
                await Settle(10);
                await Save("retrans-" + label);

                if (label == "decision")
                {
                    await CaptureAfterDecision(screen);
                }
            }

            Drop(instance);
        }

        Nav.Suppressed = false;
        GetTree().Quit();
    }

    /// <summary>
    /// Elige el candidato recomendado de la bandeja (como un clic de verdad en "Confirmar") y guarda la
    /// reproducción justo después: la tira del que entra tiene que verse y la bandeja no debe reabrirse
    /// para el mismo jugador.
    /// </summary>
    private async Task CaptureAfterDecision(BroadcastScreen screen)
    {
        if (!screen.ChooseRecommendedForCapture())
        {
            GD.Print("retransmisión: la bandeja no tenía ninguna decisión pendiente que elegir; se salta retrans-tras-decision");
            return;
        }

        await Settle(20);
        await Save("retrans-tras-decision");
    }

    private async Task<Node> Show(string path, int frames)
    {
        var scene = GD.Load<PackedScene>(path);
        var instance = scene.Instantiate();
        AddChild(instance);
        await Settle(frames);
        return instance;
    }

    private void Drop(Node instance)
    {
        RemoveChild(instance);
        instance.QueueFree();
    }

    private async Task Settle(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task Save(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(Path.Combine(_directory, name + ".png"));
        GD.Print($"captura: {name}.png");
    }

    private static int FirstOfKind(RunController run, Func<MapNode, bool> predicate)
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
}
