using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Instrumento de BV-A («los modelos 3D no se mueven con naturalidad, parpadean por ticks»): graba un
/// partido REAL de la retransmisión, fotograma a fotograma, con lo que se está DIBUJANDO de cada jugador —
/// posición del cuerpo, orientación del modelo, clip que suena, su tiempo y su ritmo—, más fotogramas
/// completos en unos pocos tramos típicos para hacer hojas de contacto.
///
/// <para><b>Cómo se lanza</b>: <c>godot --path Game --fixed-fps 30 --scene res://Scenes/CapturasRetransmision.tscn
/// -- movimiento &lt;carpeta&gt;</c>. <c>--fixed-fps</c> es lo que lo hace determinista: el motor pasa
/// siempre el mismo <c>delta</c> a todo —pantalla, director, <see cref="AnimationPlayer"/>— por lento que
/// vaya el render por software, así que el registro es el de una máquina que va exactamente a 30 fps.</para>
///
/// <para><b>Por qué se registra en <c>frame_post_draw</c> y no tras un <c>await</c></b>: la continuación de
/// un <c>await</c> la ejecuta el planificador de Godot cuando le toca, y no hay garantía de que el estado
/// leído y la imagen sean del mismo fotograma. El callback de <c>frame_post_draw</c> corre síncrono justo
/// después de dibujar, antes del siguiente <c>_Process</c>: lo registrado y la imagen son el mismo instante.</para>
///
/// <para>Solo mide y fotografía. No decide nada del partido (RT-014): la pantalla juega su partido como
/// siempre, el instrumento solo elige a qué fotograma saltar y, en dos tramos, pulsa pausa o x4 como lo
/// haría el jugador.</para>
/// </summary>
public partial class BroadcastCapture
{
    private sealed class MovementWindow
    {
        public required string Label { get; init; }
        public required int Start { get; init; }
        public required int End { get; init; }
        public int[] Focus { get; init; } = Array.Empty<int>();
        public bool Images { get; init; }
        public int MaxEngineFrames { get; init; } = 600;
        public Action<BroadcastScreen, int>? OnFrame { get; init; }
    }

    private MovementWindow? _window;
    private BroadcastScreen? _windowScreen;
    private int _windowFrame;
    private TaskCompletionSource? _windowDone;
    private StringBuilder? _movementLog;
    private string _movementDirectory = string.Empty;

    private bool _movementImages = true;

    private async Task CaptureMovement(
        RunController run, string directory, ulong seed, bool images,
        IReadOnlyList<(string Label, int StartTick, int EndTick, string Focus)>? custom = null)
    {
        _movementImages = images;

        // BV-A, B3: `sinik` mide el antes del IK del pie con el mismo binario.
        Ui.PlayerModel.DebugFootLockOff = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "sinik") >= 0;
        Ui.PlayerModel.DebugCaptureFinalPose = true;
        _movementDirectory = directory;
        Directory.CreateDirectory(directory);

        // Un equipo HUMANO: la maqueta solo pone modelo a los humanos (PlayerModel), y el síntoma es de los modelos.
        run.NewRun("human_abattoir", Race.Human, seed);
        int node = FirstOfKind(run, n => n.IsMatch);
        if (node < 0)
        {
            GD.PushError("movimiento: la semilla no tiene partido en el acto 1");
            return;
        }

        run.SelectedNodeId = node;
        var instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
        if (instance is not BroadcastScreen screen || screen.Trace is not { FrameCount: > 0 } trace)
        {
            GD.PushError("movimiento: no se abrió la retransmisión");
            Drop(instance);
            return;
        }

        var pitch = screen.Pitch3D;
        var hasModel = new bool[trace.Players.Count];
        for (int i = 0; i < hasModel.Length && i < pitch.BodyCount; i++)
        {
            hasModel[i] = pitch.ProbeBody(i).HasModel;
        }

        WriteTraceCsv(trace, hasModel, Path.Combine(directory, "traza.csv"));
        File.WriteAllText(Path.Combine(directory, "clips.csv"), Ui.PlayerModel.DebugDescribeClips());
        File.WriteAllText(
            Path.Combine(directory, "pies.csv"),
            "clip,t,lx,ly,lz,rx,ry,rz,hipsY,headY\n" + Ui.PlayerModel.DebugFootProfile(this, "kick") + Ui.PlayerModel.DebugFootProfile(this, "receive")
            + Ui.PlayerModel.DebugFootProfile(this, "tackle") + Ui.PlayerModel.DebugFootProfile(this, "trip")
            + Ui.PlayerModel.DebugFootProfile(this, "fallen") + Ui.PlayerModel.DebugFootProfile(this, "standup")
            + Ui.PlayerModel.DebugFootProfile(this, "idle"));
        WriteEventsCsv(trace, screen.Pitch3D.DebugEvents, Path.Combine(directory, "eventos.csv"));

        var windows = custom is { Count: > 0 } ? CustomWindows(trace, hasModel, custom) : PlanMovementWindows(screen, trace, hasModel);
        var plan = new StringBuilder("label,start,end,focus,images\n");
        foreach (var w in windows)
        {
            plan.Append(CultureInfo.InvariantCulture, $"{w.Label},{w.Start},{w.End},{string.Join(' ', w.Focus)},{(w.Images ? 1 : 0)}\n");
            GD.Print($"movimiento: tramo '{w.Label}' {w.Start}-{w.End} foco [{string.Join(' ', w.Focus)}]");
        }

        File.WriteAllText(Path.Combine(directory, "tramos.csv"), plan.ToString());

        _movementLog = new StringBuilder(
            "window,n,frame,alpha,frozen,timescale,player,team,hasModel,visible,state,x,z,yaw,clip,clipTime,speedScale,inputSpeed,naturalSpeed,sx,sy,lx,ly,lz,rx,ry,rz,bx,by,bz,owner,hipsY,kneeL,kneeR,frontL,frontR,lockL,lockR,procMs,ikOff\n");
        RenderingServer.FramePostDraw += OnMovementPostDraw;
        foreach (var w in windows)
        {
            screen.ChooseSpeedForCapture(0);
            screen.SeekTo(w.Start);
            _windowScreen = screen;
            _windowFrame = 0;
            _windowDone = new TaskCompletionSource();
            _window = w;
            await _windowDone.Task;
            await Settle(2);
        }

        RenderingServer.FramePostDraw -= OnMovementPostDraw;
        File.WriteAllText(Path.Combine(directory, "fotogramas.csv"), _movementLog.ToString());
        GD.Print($"movimiento: registro escrito en {directory}");
        Drop(instance);
    }

    /// <summary>
    /// Tramos pedidos por la línea de órdenes (ticks de la traza, no fotogramas): la batería de detectores de
    /// síntomas da semilla y tick de sus peores casos, y aquí se convierten en tramos con imágenes. Foco: los
    /// ids de jugador pedidos que tengan modelo, o, si no hay, los tres con modelo más cercanos al balón.
    /// </summary>
    private static List<MovementWindow> CustomWindows(
        MatchTrace trace, bool[] hasModel, IReadOnlyList<(string Label, int StartTick, int EndTick, string Focus)> custom)
    {
        var windows = new List<MovementWindow>();
        foreach (var (label, t0, t1, focusArg) in custom)
        {
            int start = trace.FrameOfTick(t0);
            int end = Math.Max(start + 1, trace.FrameOfTick(t1));
            var focus = new List<int>();
            foreach (string token in focusArg.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    for (int i = 0; i < trace.Players.Count; i++)
                    {
                        if (trace.Players[i].Id == id && hasModel[i] && !focus.Contains(i))
                        {
                            focus.Add(i);
                        }
                    }
                }
            }

            if (focus.Count == 0)
            {
                var ball = trace.BallAt(start);
                var order = new List<(float D, int I)>();
                for (int i = 0; i < trace.Players.Count; i++)
                {
                    if (hasModel[i] && trace.OnPitchAt(start, i))
                    {
                        order.Add((Vec2.Distance(ball, trace.PositionAt(start, i)), i));
                    }
                }

                order.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.I.CompareTo(b.I));
                focus.AddRange(order.Take(3).Select(o => o.I));
            }

            windows.Add(new MovementWindow
            {
                Label = label, Start = start, End = end, Focus = focus.ToArray(), Images = true, MaxEngineFrames = ((end - start) * 3) + 60,
            });
        }

        return windows;
    }

    private void OnMovementPostDraw()
    {
        if (_window is not { } w || _windowScreen is not { } screen || _movementLog is null)
        {
            return;
        }

        var pitch = screen.Pitch3D;
        var trace = screen.Trace!;
        int frame = pitch.Frame;
        var log = _movementLog;
        var (ballAt, _, owner) = pitch.DebugBall();
        for (int i = 0; i < pitch.BodyCount && i < trace.Players.Count; i++)
        {
            var probe = pitch.ProbeBody(i);
            int f = Math.Clamp(frame, 0, trace.FrameCount - 1);
            log.Append(CultureInfo.InvariantCulture,
                $"{w.Label},{_windowFrame},{frame},{pitch.Alpha:0.####},{(screen.Frozen ? 1 : 0)},{screen.TimeScaleForCapture:0.###},{i},{trace.Players[i].Team},{(probe.HasModel ? 1 : 0)},{(probe.Visible ? 1 : 0)},{trace.StateAt(f, i)},");
            log.Append(CultureInfo.InvariantCulture,
                $"{probe.Position.X:0.#####},{probe.Position.Z:0.#####},{probe.Yaw:0.#####},{probe.Clip},{probe.ClipTime:0.####},{probe.SpeedScale:0.####},{probe.InputSpeed:0.####},{probe.NaturalSpeed:0.####},{probe.Screen.X:0.#},{probe.Screen.Y:0.#},");
            log.Append(CultureInfo.InvariantCulture,
                $"{probe.LeftToe.X:0.####},{probe.LeftToe.Y:0.####},{probe.LeftToe.Z:0.####},{probe.RightToe.X:0.####},{probe.RightToe.Y:0.####},{probe.RightToe.Z:0.####},{ballAt.X:0.####},{ballAt.Y:0.####},{ballAt.Z:0.####},{owner},{probe.HipsY:0.####},{probe.Knees.AngleL:0.#},{probe.Knees.AngleR:0.#},{probe.Knees.FrontL:0.####},{probe.Knees.FrontR:0.####},{probe.Knees.LockL:0.##},{probe.Knees.LockR:0.##},{Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:0.###},{(Ui.PlayerModel.DebugFootLockOff ? 1 : 0)}\n");
        }

        if (w.Images && _movementImages)
        {
            var image = GetViewport().GetTexture().GetImage();
            image.SaveJpg(Path.Combine(_movementDirectory, $"{w.Label}_{_windowFrame:0000}.jpg"), 0.9f);
        }

        w.OnFrame?.Invoke(screen, _windowFrame);
        _windowFrame++;
        if (frame >= w.End || _windowFrame >= w.MaxEngineFrames)
        {
            _window = null;
            _windowDone?.TrySetResult();
        }
    }

    /// <summary>
    /// Los tramos, buscados en la traza (pura, sin dibujar nada): una carrera recta larga —el caso de
    /// respuesta conocida con el que se valida el instrumento—, un giro brusco, una recepción seguida de
    /// pase, una entrada, una falta que para el juego y su reanudación, una pausa manual, un tramo a x4 y
    /// un tramo largo sin imágenes para la estadística.
    /// </summary>
    private static List<MovementWindow> PlanMovementWindows(BroadcastScreen screen, MatchTrace trace, bool[] hasModel)
    {
        var windows = new List<MovementWindow>();
        int frames = trace.FrameCount;

        bool Free(int f, int i) => trace.OnPitchAt(f, i)
            && trace.StateAt(f, i) is PlayerState.Positioning or PlayerState.Chasing or PlayerState.Dribbling;

        Vector2 Step(int f, int i)
        {
            var a = trace.PositionAt(f, i);
            var b = trace.PositionAt(f + 1, i);
            return new Vector2(b.X - a.X, b.Y - a.Y);
        }

        int[] Near(int f, int who, int count)
        {
            var here = trace.PositionAt(f, who);
            var list = new List<(float D, int I)>();
            for (int j = 0; j < trace.Players.Count; j++)
            {
                if (j != who && hasModel[j] && trace.OnPitchAt(f, j))
                {
                    list.Add((Vec2.Distance(here, trace.PositionAt(f, j)), j));
                }
            }

            list.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.I.CompareTo(b.I));
            var result = new List<int> { who };
            for (int k = 0; k < list.Count && result.Count < count; k++)
            {
                result.Add(list[k].I);
            }

            return result.ToArray();
        }

        // 1. Carrera recta: la racha más larga de pasos > 0,1 casillas con giro < 8° entre pasos consecutivos.
        (int Player, int From, int Length) best = (-1, 0, 0);
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (!hasModel[i])
            {
                continue;
            }

            int runFrom = -1;
            for (int f = 120; f + 2 < frames; f++)
            {
                var s0 = Step(f, i);
                var s1 = Step(f + 1, i);
                bool straight = Free(f, i) && Free(f + 1, i) && s0.Length() > 0.1f && s1.Length() > 0.1f
                    && s0.Length() < 0.6f && s1.Length() < 0.6f && Mathf.Abs(s0.AngleTo(s1)) < Mathf.DegToRad(8f);
                if (straight && runFrom < 0)
                {
                    runFrom = f;
                }
                else if (!straight && runFrom >= 0)
                {
                    if (f - runFrom > best.Length)
                    {
                        best = (i, runFrom, f - runFrom);
                    }

                    runFrom = -1;
                }
            }
        }

        if (best.Player >= 0)
        {
            int start = best.From;
            windows.Add(new MovementWindow
            {
                Label = "carrera", Start = start, End = start + Math.Min(best.Length, 45), Focus = Near(start, best.Player, 3), Images = true,
            });
        }

        // 2. Giro brusco: más de 120° entre dos pasos de carrera.
        bool turned = false;
        for (int f = 150; f + 2 < frames && !turned; f++)
        {
            for (int i = 0; i < trace.Players.Count; i++)
            {
                if (!hasModel[i] || !Free(f, i) || !Free(f + 1, i))
                {
                    continue;
                }

                var s0 = Step(f, i);
                var s1 = Step(f + 1, i);
                if (s0.Length() > 0.1f && s1.Length() > 0.1f && s0.Length() < 0.6f && s1.Length() < 0.6f
                    && Mathf.Abs(s0.AngleTo(s1)) > Mathf.DegToRad(120f))
                {
                    windows.Add(new MovementWindow { Label = "giro", Start = f - 15, End = f + 30, Focus = Near(f, i, 3), Images = true });
                    turned = true;
                    break;
                }
            }
        }

        // 3. Recepción y pase: el balón pasa a un jugador con modelo viniendo en vuelo y ese jugador pasa en 30 ticks.
        for (int f = 150; f + 31 < frames; f++)
        {
            int owner = trace.BallOwnerAt(f);
            if (owner < 0 || !hasModel[owner] || trace.BallOwnerAt(f - 1) == owner || !trace.BallInFlightAt(f - 1))
            {
                continue;
            }

            int pass = -1;
            for (int g = f + 1; g < f + 30; g++)
            {
                if (trace.StateAt(g, owner) == PlayerState.Passing)
                {
                    pass = g;
                    break;
                }
            }

            if (pass > 0)
            {
                windows.Add(new MovementWindow { Label = "pase", Start = f - 12, End = pass + 20, Focus = Near(f, owner, 3), Images = true });
                break;
            }
        }

        // 4. Entrada: el primer jugador con modelo que entra a por el balón.
        for (int f = 150; f < frames; f++)
        {
            int tackler = -1;
            for (int i = 0; i < trace.Players.Count; i++)
            {
                if (hasModel[i] && trace.OnPitchAt(f, i) && trace.StateAt(f, i) == PlayerState.Tackling
                    && trace.StateAt(f - 1, i) != PlayerState.Tackling)
                {
                    tackler = i;
                    break;
                }
            }

            if (tackler >= 0)
            {
                windows.Add(new MovementWindow { Label = "entrada", Start = f - 15, End = f + 30, Focus = Near(f, tackler, 3), Images = true });
                break;
            }
        }

        // 4b. Derribo: el primer jugador con modelo que cae (derribado o lesionado), con su levantada.
        for (int f = 150; f < frames; f++)
        {
            int victim = -1;
            for (int i = 0; i < trace.Players.Count; i++)
            {
                if (hasModel[i] && trace.OnPitchAt(f, i) && trace.StateAt(f, i) is PlayerState.KnockedDown or PlayerState.Injured
                    && trace.StateAt(f - 1, i) is not (PlayerState.KnockedDown or PlayerState.Injured))
                {
                    victim = i;
                    break;
                }
            }

            if (victim >= 0)
            {
                windows.Add(new MovementWindow { Label = "derribo", Start = f - 10, End = f + 40, Focus = Near(f, victim, 3), Images = true });
                break;
            }
        }

        // 5. Falta que para el juego (pausa breve, ADR 0173) y la reanudación.
        foreach (var m in screen.Moments.Moments)
        {
            if (m.Frame > 200 && Game.Match.PlayStops.Holds(m, trace))
            {
                int focus = 0;
                float bestDistance = float.MaxValue;
                var ball = trace.BallAt(m.Frame);
                for (int i = 0; i < trace.Players.Count; i++)
                {
                    float d = Vec2.Distance(ball, trace.PositionAt(m.Frame, i));
                    if (hasModel[i] && trace.OnPitchAt(m.Frame, i) && d < bestDistance)
                    {
                        bestDistance = d;
                        focus = i;
                    }
                }

                windows.Add(new MovementWindow
                {
                    Label = "reanudacion", Start = m.Frame - 20, End = m.LastFrame + 60, Focus = Near(m.Frame, focus, 3), Images = true, MaxEngineFrames = 900,
                });
                break;
            }
        }

        // 6. Pausa manual a mitad de una carrera: 0,5 s jugando, 1 s en pausa, y sigue.
        if (best.Player >= 0)
        {
            int start = best.From;
            windows.Add(new MovementWindow
            {
                Label = "pausa", Start = start, End = start + 40, Focus = Near(start, best.Player, 3), Images = true,
                OnFrame = static (s, n) =>
                {
                    if (n == 15 || n == 45)
                    {
                        s.TogglePauseForCapture();
                    }
                },
            });
        }

        // 6b. Vídeo para el revisor: unos diez segundos a x1 alrededor de la recepción y el pase (imágenes a 30 fps).
        foreach (var w in windows)
        {
            if (w.Label == "pase")
            {
                int from = Math.Max(150, w.Start - 45);
                windows.Add(new MovementWindow { Label = "video", Start = from, End = Math.Min(frames - 1, from + 150), Focus = w.Focus, Images = true, MaxEngineFrames = 420 });
                break;
            }
        }

        // 6c. Contactos: un tramo corto (sin imágenes) alrededor de cada entrada o falta en que interviene un jugador con
        // modelo, para medir la entrada, la caída y la levantada sobre todos los casos del partido y no sólo los del tramo
        // largo (BV-A, tercera pasada). Sin solaparse, y como mucho 16.
        if (screen.Pitch3D.DebugEvents is { } events)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < trace.Players.Count; i++)
            {
                index[trace.Players[i].Id] = i;
            }

            int lastEnd = -1;
            int made = 0;
            for (int f = 0; f < frames && made < 16; f++)
            {
                int from = trace.EventFromAt(f);
                int count = trace.EventCountAt(f);
                for (int e = from; e < from + count && e < events.Count; e++)
                {
                    var ev = events[e];
                    if (ev.Type is not (EventType.Tackle or EventType.Foul) || f - 10 <= lastEnd)
                    {
                        continue;
                    }

                    bool actor = index.TryGetValue(ev.Actor, out int a) && hasModel[a];
                    bool opponent = index.TryGetValue(ev.Opponent, out int o) && hasModel[o];
                    if (!actor && !opponent)
                    {
                        continue;
                    }

                    lastEnd = Math.Min(frames - 1, f + 40);
                    windows.Add(new MovementWindow { Label = $"caida{made:00}", Start = Math.Max(1, f - 10), End = lastEnd, Images = false, MaxEngineFrames = 300 });
                    made++;
                    break;
                }
            }
        }

        // 7. x4: tres segundos reales, sin imágenes (la cifra que importa es pies frente a cuerpo).
        windows.Add(new MovementWindow
        {
            Label = "x4", Start = 300, End = 300 + 180, Images = false, MaxEngineFrames = 90,
            OnFrame = static (s, n) =>
            {
                if (n == 0)
                {
                    s.ChooseSpeedForCapture(1);
                }
            },
        });

        // 8. Tramo largo a x1 sin imágenes: la estadística del parpadeo, con el partido tal cual (pausas incluidas).
        windows.Add(new MovementWindow { Label = "tramo", Start = 200, End = Math.Min(frames - 1, 200 + 600), Images = false, MaxEngineFrames = 1500 });
        // 9. (el último: deja el IK como lo encuentre) Coste del IK del pie (BV-A, B3): 12 s de partido en x1 alternando IK apagado/encendido cada segundo, en el MISMO
        // proceso, para comparar el tiempo de proceso por fotograma sin el ruido de otra ejecución (la máquina es compartida).
        windows.Add(new MovementWindow
        {
            Label = "coste", Start = 300, End = Math.Min(frames - 1, 300 + 180), Images = false, MaxEngineFrames = 400,
            OnFrame = static (s, n) => Ui.PlayerModel.DebugFootLockOff = (n / 30) % 2 == 0,
        });

        return windows;
    }

    /// <summary>
    /// Los sucesos de contacto de la traza (entrada, falta, lesión), con el fotograma en que caen y los dos implicados
    /// como índices de la traza (BV-A, tercera pasada: entradas, faltas y caídas). Sólo lee.
    /// </summary>
    private static void WriteEventsCsv(MatchTrace trace, IReadOnlyList<MatchEvent>? events, string path)
    {
        var sb = new StringBuilder("frame,tick,type,detail,actor,opponent\n");
        if (events is not null)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < trace.Players.Count; i++)
            {
                index[trace.Players[i].Id] = i;
            }

            for (int f = 0; f < trace.FrameCount; f++)
            {
                int from = trace.EventFromAt(f);
                int count = trace.EventCountAt(f);
                for (int e = from; e < from + count && e < events.Count; e++)
                {
                    var ev = events[e];
                    if (ev.Type is not (EventType.Tackle or EventType.Foul or EventType.Injury or EventType.Death or EventType.DribbleWon))
                    {
                        continue;
                    }

                    int actor = index.TryGetValue(ev.Actor, out int a) ? a : -1;
                    int opponent = index.TryGetValue(ev.Opponent, out int o) ? o : -1;
                    sb.Append(CultureInfo.InvariantCulture, $"{f},{ev.Tick},{ev.Type},{ev.Detail},{actor},{opponent}\n");
                }
            }
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteTraceCsv(MatchTrace trace, bool[] hasModel, string path)
    {
        var sb = new StringBuilder("frame,player,team,hasModel,onPitch,state,x,y,owner,inFlight\n");
        for (int f = 0; f < trace.FrameCount; f++)
        {
            int owner = trace.BallOwnerAt(f);
            bool flight = trace.BallInFlightAt(f);
            for (int i = 0; i < trace.Players.Count; i++)
            {
                var at = trace.PositionAt(f, i);
                sb.Append(CultureInfo.InvariantCulture,
                    $"{f},{i},{trace.Players[i].Team},{(hasModel[i] ? 1 : 0)},{(trace.OnPitchAt(f, i) ? 1 : 0)},{trace.StateAt(f, i)},{at.X:0.#####},{at.Y:0.#####},{owner},{(flight ? 1 : 0)}\n");
            }
        }

        File.WriteAllText(path, sb.ToString());
    }
}
