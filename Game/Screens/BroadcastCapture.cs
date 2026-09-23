using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Events;
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

        // "muerte" NO va aquí (revisión del orquestador, gestos de cámara): tiene su propio sondeo más
        // ancho (hasta ~60 semillas, ver Capture) y su propia captura en dos tiempos
        // (retrans-muerte-1/2.png), no la genérica de este array.
        ("final", static m => m.Kind == MomentKind.FullTime),
        ("decision", static m => m.Decision),
    };

    /// <summary>1 s a 15 ticks/s (RT-020): tope de espera al sondear la resolución de un tiro, solo para elegir cuál capturar — el gesto en juego (BroadcastScreen) ya no depende de esto, dispara con cualquier tiro.</summary>
    private const int ShotGestureMaxTicks = 15;

    /// <summary>Semillas deterministas adicionales para el sondeo ancho de "muerte" (hasta ~60 en total con <see cref="Seeds"/>), solo si ninguna de las doce de siempre la tiene.</summary>
    private const int ExtraDeathSeeds = 48;

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

        // Tiro que NO acaba en gol (para retrans-tiro: el gesto no puede delatar el resultado) y sangre
        // (cualquier partido con al menos una lesión/muerte no anulada, capturado en su propio fotograma
        // de "final" para que las manchas ya estén todas puestas): no son MatchMoment (N0, sin agrupador),
        // así que se leen de Result.Events directamente, no de Kinds.
        (ulong Seed, int Node, int StartFrame, int ReleaseFrame, Cell Cell)? foundShot = null;
        (ulong Seed, int Node, int Frame)? foundBlood = null;
        (ulong Seed, int Node, int Frame)? foundDeath = null;

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

            foundShot ??= FindShotWithoutGoal(playback, seed, node);
            foundBlood ??= FindBlood(playback, moments, seed, node);
            foundDeath ??= FindDeath(moments, seed, node);
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

        if (foundShot is { } shotHit)
        {
            GD.Print($"retransmisión: 'tiro' (sin gol) en la semilla {shotHit.Seed}, nodo {shotHit.Node}, fotograma {shotHit.StartFrame}");
        }
        else
        {
            GD.Print($"retransmisión: ninguna de las {Seeds.Length} semillas probadas tiene un tiro que no acabe en gol; se salta retrans-tiro");
        }

        if (foundBlood is { } bloodHit)
        {
            GD.Print($"retransmisión: 'sangre' en la semilla {bloodHit.Seed}, nodo {bloodHit.Node}, fotograma {bloodHit.Frame}");
        }
        else
        {
            GD.Print($"retransmisión: ninguna de las {Seeds.Length} semillas probadas tiene una lesión o muerte no anulada; se salta retrans-sangre");
        }

        // Muerte: sondeo ancho (hasta ~ExtraDeathSeeds más, RA-020 la hace rara a propósito) solo si las
        // doce de siempre no dieron ninguna.
        int extraDeathSeedsTried = 0;
        if (foundDeath is null)
        {
            for (ulong seed = 500000UL; extraDeathSeedsTried < ExtraDeathSeeds; seed++, extraDeathSeedsTried++)
            {
                run.NewRun("orc_ironworks", Race.Orc, seed);
                int node = FirstOfKind(run, n => n.IsMatch);
                if (node < 0)
                {
                    continue;
                }

                var playback = MatchPlaybacks.Of(run.State!, node, run.Catalog!, run.Engine, trace: true, MatchDecisions.None);
                var moments = MatchMomentView.Build(playback.Setup, playback.Result, run.Catalog!).Moments;
                foundDeath = FindDeath(moments, seed, node);
                if (foundDeath is not null)
                {
                    break;
                }
            }
        }

        int deathSeedsTried = Seeds.Length + extraDeathSeedsTried;
        if (foundDeath is { } deathHit)
        {
            GD.Print($"retransmisión: 'muerte' en la semilla {deathHit.Seed}, nodo {deathHit.Node}, fotograma {deathHit.Frame} (probadas {deathSeedsTried} semillas)");
        }
        else
        {
            GD.Print($"retransmisión: ninguna de las {deathSeedsTried} semillas probadas tiene un momento de muerte; se salta retrans-muerte-1/retrans-muerte-2");
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
                // Referencia para comparar con retrans-tiro (revisión del orquestador): centro/distancia
                // y esquinas del césped SIN ningún gesto activo.
                var basePitch3d = screen.Pitch3D;
                GD.Print(
                    $"retrans-base: centro efectivo={basePitch3d.DebugCenter}, "
                    + $"distancia efectiva={basePitch3d.DebugDistance:0.###}, zoom={basePitch3d.DebugZoomFactor:0.###}");
                foreach (var corner in basePitch3d.DebugPitchCorners())
                {
                    GD.Print($"  esquina del césped en pantalla: {corner}");
                }

                // Medir la portería, no suponerla (revisión del revisor, 20 sep 2026): las dos esquinas de
                // la boca de cada portería tienen que caer EXACTAMENTE sobre la recta entre las dos
                // esquinas del césped a esa misma X (0 o 16) — es geometría de proyectiva pura, una recta
                // en el mundo siempre proyecta a una recta en pantalla.
                for (int team = 0; team < 2; team++)
                {
                    foreach (var corner in basePitch3d.DebugGoalMouth(team))
                    {
                        GD.Print($"  boca de la portería {team} en pantalla: {corner}");
                    }
                }

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

        // 2b. Gestos de cámara (docs/ui/README §4): tiro (el gesto no puede delatar el resultado, así que
        // se captura A MITAD del acercamiento de un tiro que no acaba en gol) y muerte en dos tiempos (el
        // campo la cuenta primero, el bando 1,2 s después). Avanzan el reloj real a mano —_Process con
        // deltas fijos, nunca Settle/dormir— para que la captura sea siempre la misma, sin depender de
        // cuánto tarde el motor en pintar un fotograma de verdad.
        if (foundShot is { } shot)
        {
            run.NewRun("orc_ironworks", Race.Orc, shot.Seed);
            run.SelectedNodeId = shot.Node;

            var instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
            if (instance is BroadcastScreen screen)
            {
                GoManual(screen);
                screen.SeekTo(shot.StartFrame);

                // ~0,417 s reales: pasado el "inSeconds" (0,25 s) del acercamiento del tiro
                // (BroadcastScreen.ShotPunchInSeconds) y dentro del mantenimiento mínimo (0,25-0,60 s,
                // ShotPunchHoldSeconds=0,35 s) — a fondo, no a mitad de entrada.
                StepManual(screen, 1.0 / 60.0, 25);

                // Medir, no suponer (revisión del orquestador, 19 sep 2026): centro/distancia efectivos
                // contra los que habría sin gesto, las cuatro esquinas del césped proyectadas, y el
                // píxel del propio objetivo del acercamiento —tiene que quedarse prácticamente fijo, eso
                // es lo que demuestra que amplía ALREDEDOR del punto y no recentra la escena en él.
                var pitch3d = screen.Pitch3D;
                var (unpunchedCenter, unpunchedDistance) = pitch3d.DebugUnpunchedRig();
                GD.Print(
                    $"retrans-tiro: centro efectivo={pitch3d.DebugCenter} (sin gesto {unpunchedCenter}), "
                    + $"distancia efectiva={pitch3d.DebugDistance:0.###} (sin gesto {unpunchedDistance:0.###}), "
                    + $"zoom={pitch3d.DebugZoomFactor:0.###}");
                foreach (var corner in pitch3d.DebugPitchCorners())
                {
                    GD.Print($"  esquina del césped en pantalla: {corner}");
                }

                var targetCenter = Pitch.CellCenter(shot.Cell);
                var targetWorld = new Vector3(targetCenter.X, 0f, targetCenter.Y);
                var targetPunched = pitch3d.DebugProject(targetWorld);
                var targetUnpunched = pitch3d.DebugProjectUnpunched(targetWorld);

                // DebugProject trabaja en el lienzo lógico de 1920x1200 del SubViewport (docs/ui/README
                // §7): a pantalla real (1280x800, ×0,667) para que el "~10 px" del encargo sea en los
                // píxeles que de verdad se ven, no en los del lienzo.
                const float CanvasToScreen = 1280f / 1920f;
                float targetShiftScreenPx = targetPunched.DistanceTo(targetUnpunched) * CanvasToScreen;
                GD.Print(
                    $"  objetivo del acercamiento ({shot.Cell}): sin gesto {targetUnpunched} con gesto {targetPunched} "
                    + $"(lienzo) -> desplazamiento {targetShiftScreenPx:0.##}px en pantalla real");

                await Save("retrans-tiro");
            }

            Drop(instance);
        }

        if (foundBlood is { } blood)
        {
            run.NewRun("orc_ironworks", Race.Orc, blood.Seed);
            run.SelectedNodeId = blood.Node;

            var instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
            if (instance is BroadcastScreen screen)
            {
                screen.SeekTo(blood.Frame);
                await Settle(10);
                await Save("retrans-sangre");
            }

            Drop(instance);
        }

        if (foundDeath is { } death)
        {
            run.NewRun("orc_ironworks", Race.Orc, death.Seed);
            run.SelectedNodeId = death.Node;

            var instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
            if (instance is BroadcastScreen screen)
            {
                GoManual(screen);
                screen.SeekTo(death.Frame);

                // Primer tiempo: el acercamiento ya se nota, el bando todavía no (el retardo son 1,2 s
                // reales, BroadcastScreen.DeathEdictDelaySeconds) — unos pocos pasos pequeños, lejos de
                // ese retardo.
                StepManual(screen, 1.0 / 60.0, 6);
                await Save("retrans-muerte-1");

                // Segundo tiempo: se pasa el retardo del bando con margen (1,2 s) sin acercarse al hueco
                // de espera del acercamiento (HoldSeconds = 20 s en BroadcastScreen).
                StepManual(screen, 1.0 / 15.0, 20);
                await Save("retrans-muerte-2");
            }

            Drop(instance);
        }

        // 3. Variantes de profundidad (revisión del orquestador: «el campo debe tener más 3D, más
        // profundidad»), solo si se pidieron por línea de comandos (`-- variantes`). Reutiliza el MISMO
        // fotograma que 'retrans-base' (estado inicial de la semilla base) y el MISMO que 'retrans-gol'
        // (found["gol"], que puede venir de otra semilla) para que lo único que cambie entre imágenes sea
        // la cámara, nunca el partido.
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "variantes") >= 0)
        {
            await CaptureDepthVariants(run, baseSeed.Value, baseNode, found);
        }

        // 4. MAQUETA de modelos humanoides + ARCO del balón (23 sep 2026). Necesita un club HUMANO —el
        // resto del recorrido juega con orcos y la maqueta solo viste a los humanos— y busca el fotograma
        // de mayor altura del balón, que es el que enseña las dos cosas a la vez: los modelos en el campo
        // y una pelota por el aire con su sombra. Las dos son provisionales (regla 10: nada de arte hasta
        // cerrar el diseño de la fase 2) y esta captura existe para poder DECIDIR mirándolas.
        run.NewRun("human_abattoir", Race.Human, baseSeed.Value);
        int humanNode = FirstOfKind(run, n => n.IsMatch);
        if (humanNode >= 0)
        {
            run.SelectedNodeId = humanNode;
            var humanInstance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
            if (humanInstance is BroadcastScreen humanScreen && run.Playback?.Trace is { } humanTrace)
            {
                int peakFrame = 0;
                float peak = 0f;
                for (int f = 0; f < humanTrace.FrameCount; f++)
                {
                    float h = humanTrace.BallHeightAt(f);
                    if (h > peak)
                    {
                        peak = h;
                        peakFrame = f;
                    }
                }

                GoManual(humanScreen);
                humanScreen.SeekTo(peakFrame);
                StepManual(humanScreen, 1.0 / 60.0, 6);
                await Save("retrans-modelos");
                GD.Print($"retrans-modelos: club humano; balón a {peak:0.###} casillas de alto en el fotograma {peakFrame} de {humanTrace.FrameCount}");
            }

            Drop(humanInstance);
        }
        else
        {
            GD.PushWarning("el club humano no tiene ningún nodo de partido: se salta retrans-modelos");
        }

        Nav.Suppressed = false;
        GetTree().Quit();
    }

    /// <summary>
    /// Las cinco variantes de cámara que se comparan (revisión del orquestador, 19 sep 2026): A es el
    /// control (la ortográfica de siempre, sin estadio), B aísla el efecto del entorno sobre la misma
    /// cámara, y C/D/E prueban perspectiva con distinto FOV y elevación, todas con estadio.
    /// </summary>
    private static readonly BroadcastScreen.PitchVariant[] DepthVariants =
    {
        new("A", Perspective: false, Elevation: 60f, Fov: 35f, Stadium: false),
        new("B", Perspective: false, Elevation: 60f, Fov: 35f, Stadium: true),
        new("C", Perspective: true, Elevation: 55f, Fov: 35f, Stadium: true),
        new("D", Perspective: true, Elevation: 45f, Fov: 30f, Stadium: true),
        new("E", Perspective: true, Elevation: 40f, Fov: 40f, Stadium: true),
    };

    /// <summary>
    /// Una pasada de <c>Retransmision.tscn</c> por variante, con <see cref="BroadcastScreen.CaptureVariant"/>
    /// puesto justo antes de instanciar (se consume solo, <see cref="BroadcastScreen.Build"/> lo vuelve a
    /// <c>null</c>). <paramref name="found"/> es el mismo sondeo sin Godot que ya hizo <see cref="Capture"/>
    /// para el resto de las capturas: si "gol" salió de una semilla distinta de la base, esta función abre
    /// una segunda pasada solo para esa captura, en vez de forzar el gol a la semilla base.
    /// </summary>
    private async Task CaptureDepthVariants(
        RunController run,
        ulong baseSeed,
        int baseNode,
        Dictionary<string, (ulong Seed, int Node, int Frame)> found)
    {
        bool hasGoal = found.TryGetValue("gol", out var goal);
        if (!hasGoal)
        {
            GD.Print("retransmisión (variantes): ninguna semilla sondeada tiene un momento de gol; se salta depth-*-gol");
        }

        foreach (var variant in DepthVariants)
        {
            BroadcastScreen.CaptureVariant = variant;
            run.NewRun("orc_ironworks", Race.Orc, baseSeed);
            run.SelectedNodeId = baseNode;

            var instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
            if (instance is not BroadcastScreen screen)
            {
                GD.PushError("res://Scenes/Retransmision.tscn no instancia BroadcastScreen: no hay capturas de variantes");
                GetTree().Quit(1);
                return;
            }

            await Save($"depth-{variant.Label}-base");

            if (hasGoal)
            {
                if (goal.Seed == baseSeed && goal.Node == baseNode)
                {
                    screen.SeekTo(goal.Frame);
                    await Settle(10);
                    await Save($"depth-{variant.Label}-gol");
                }
                else
                {
                    Drop(instance);
                    BroadcastScreen.CaptureVariant = variant;
                    run.NewRun("orc_ironworks", Race.Orc, goal.Seed);
                    run.SelectedNodeId = goal.Node;

                    instance = await Show("res://Scenes/Retransmision.tscn", frames: 10);
                    if (instance is not BroadcastScreen goalScreen)
                    {
                        GD.PushError("res://Scenes/Retransmision.tscn no instancia BroadcastScreen: no hay captura depth-gol");
                        GetTree().Quit(1);
                        return;
                    }

                    goalScreen.SeekTo(goal.Frame);
                    await Settle(10);
                    await Save($"depth-{variant.Label}-gol");
                }
            }

            Drop(instance);
        }

        BroadcastScreen.CaptureVariant = null;
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

    /// <summary>
    /// A partir de aquí el reloj real de <paramref name="screen"/> —y el de su campo 3D, un nodo hijo con
    /// su propio <c>_Process</c>— lo controla esta clase a mano con <see cref="StepManual"/>: sin esto, el
    /// motor seguiría llamando a los dos por su cuenta con un delta real sin control mientras
    /// <see cref="Save"/> espera a que se dibuje el fotograma, y las capturas de gesto (tiro, muerte) no
    /// serían siempre las mismas.
    /// </summary>
    private static void GoManual(BroadcastScreen screen)
    {
        screen.SetProcess(false);
        screen.Pitch3D.SetProcess(false);
    }

    /// <summary>Avanza <paramref name="screen"/> (y su campo 3D) <paramref name="times"/> veces con el mismo <paramref name="delta"/> fijo: determinista, nada de dormir ni esperar fotogramas del motor.</summary>
    private static void StepManual(BroadcastScreen screen, double delta, int times)
    {
        for (int i = 0; i < times; i++)
        {
            screen._Process(delta);
            screen.Pitch3D._Process(delta);
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

    /// <summary>
    /// Un tiro de <paramref name="playback"/> cuya resolución —el próximo
    /// <see cref="EventType.Goal"/>/<see cref="EventType.Save"/>/<see cref="EventType.ShotBlocked"/> de la
    /// lista, un único balón en vuelo a la vez— NO es un gol: para retrans-tiro, que tiene que demostrar
    /// que el gesto no delata el resultado. Descarta los de ventana casi nula (un tiro a bocajarro puede
    /// resolverse en el mismo tick que se lanza: RA-005, un balón que ya está prácticamente en la
    /// portería) — revisión del orquestador, 19 sep 2026, ese fue el primero que salió y no dio tiempo a
    /// que se notara ningún acercamiento — y se queda con el de ventana MÁS LARGA de todo el partido, no
    /// el primero que encuentra. Null si este partido no tiene ninguno así.
    /// </summary>
    private static (ulong Seed, int Node, int StartFrame, int ReleaseFrame, Cell Cell)? FindShotWithoutGoal(MatchPlayback playback, ulong seed, int node)
    {
        const int MinWindowTicks = 6; // deja tiempo de sobra para pasar el "inSeconds" (0,25 s ~ 4 ticks) del acercamiento antes de la resolución.

        var trace = playback.Trace;
        if (trace is null)
        {
            return null;
        }

        var events = playback.Result.Events;
        int bestStartTick = -1;
        int bestReleaseTick = -1;
        int bestWindow = -1;
        Cell bestCell = default;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type != EventType.Shot)
            {
                continue;
            }

            int startTick = events[i].Tick;
            int releaseTick = startTick + ShotGestureMaxTicks;
            EventType? resolution = null;
            for (int j = i + 1; j < events.Count; j++)
            {
                if (events[j].Type is EventType.Goal or EventType.Save or EventType.ShotBlocked)
                {
                    resolution = events[j].Type;
                    releaseTick = Math.Min(releaseTick, events[j].Tick);
                    break;
                }
            }

            if (resolution == EventType.Goal)
            {
                continue;
            }

            int window = releaseTick - startTick;
            if (window >= MinWindowTicks && window > bestWindow)
            {
                bestWindow = window;
                bestStartTick = startTick;
                bestReleaseTick = releaseTick;
                bestCell = events[i].Cell;
            }
        }

        return bestStartTick < 0 ? null : (seed, node, trace.FrameOfTick(bestStartTick), trace.FrameOfTick(bestReleaseTick), bestCell);
    }

    /// <summary>
    /// Un fotograma de JUEGO CORRIENTE de este partido, posterior a la última lesión/muerte no anulada
    /// (para que ya estén todas las manchas puestas) y anterior al final — sin caer dentro de la ventana
    /// de presentación de ningún momento (revisión del orquestador: no bajo el acta ni ningún otro
    /// estandarte/sello, que atenúan el campo). Null si este partido no tiene sangre que enseñar, o si no
    /// se encuentra ningún hueco así.
    /// </summary>
    private static (ulong Seed, int Node, int Frame)? FindBlood(MatchPlayback playback, IReadOnlyList<MatchMoment> moments, ulong seed, int node)
    {
        var trace = playback.Trace;
        if (trace is null)
        {
            return null;
        }

        var events = playback.Result.Events;
        int lastBloodTick = -1;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (IsCancelledEvent(e) || e.Type is not (EventType.Death or EventType.Injury))
            {
                continue;
            }

            lastBloodTick = Math.Max(lastBloodTick, e.Tick);
        }

        if (lastBloodTick < 0)
        {
            return null;
        }

        int fullTimeFrame = -1;
        for (int i = 0; i < moments.Count; i++)
        {
            if (moments[i].Kind == MomentKind.FullTime)
            {
                fullTimeFrame = moments[i].Frame;
                break;
            }
        }

        if (fullTimeFrame < 0)
        {
            return null;
        }

        int afterFrame = trace.FrameOfTick(lastBloodTick);
        int? quiet = FindQuietFrame(moments, afterFrame, fullTimeFrame);
        return quiet is int frame ? (seed, node, frame) : null;
    }

    /// <summary>
    /// Un fotograma en <c>(afterFrame, beforeFrame)</c> que no cae dentro de la ventana de presentación de
    /// NINGÚN momento (un margen de <see cref="QuietMargin"/> fotogramas alrededor de su
    /// <c>MatchMoment.Frame</c> — generoso a propósito, cubre de sobra un sello N1 corto o una voz N3/N4
    /// larga): empieza cerca del final y retrocede, para quedar lo más lejos posible de la última sangre
    /// y lo más cerca posible del final sin tocar su propia presentación.
    /// </summary>
    private static int? FindQuietFrame(IReadOnlyList<MatchMoment> moments, int afterFrame, int beforeFrame)
    {
        const int QuietMargin = 60; // 4 s a 15 ticks/s: más que cualquier duración de sello/voz de docs/ui/README §4.
        for (int candidate = beforeFrame - QuietMargin; candidate > afterFrame; candidate -= 15)
        {
            bool clear = true;
            for (int i = 0; i < moments.Count; i++)
            {
                if (Math.Abs(moments[i].Frame - candidate) < QuietMargin)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>La primera muerte no anulada (propia o rival, da igual el equipo) de estos momentos.</summary>
    private static (ulong Seed, int Node, int Frame)? FindDeath(IReadOnlyList<MatchMoment> moments, ulong seed, int node)
    {
        for (int i = 0; i < moments.Count; i++)
        {
            if (moments[i].Kind == MomentKind.Death && !moments[i].Cancelled)
            {
                return (seed, node, moments[i].Frame);
            }
        }

        return null;
    }

    private static bool IsCancelledEvent(MatchEvent e) => e.Detail.EndsWith(":cancelled", StringComparison.Ordinal);
}
