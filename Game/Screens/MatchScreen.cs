using System.Collections.Generic;
using System.Globalization;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Partido</b> (RF-121, <c>ui-run-minima.md</c>): el campo con las fichas moviéndose, el
/// marcador y el log de eventos, los tres sincronizados en el mismo tick.
/// <para>
/// El partido lo resuelve <c>/Sim</c> entero antes de pintar nada; lo que se ve aquí es su
/// <see cref="MatchTrace"/> reproducida — un fotograma por tick lógico, sin submuestrear— y por eso se
/// puede pausar, acelerar, ir tick a tick y sobre todo <b>retroceder</b>: un gol raro se vuelve a ver
/// tantas veces como haga falta.
/// </para>
/// <para>
/// La pantalla <b>no calcula nada del juego</b> (RT-014): las posiciones y los estados salen de la traza,
/// el log lo compone <c>Sim.Run.View.MatchLogView</c> como dato estructurado y aquí solo se le pone la
/// frase en español de <see cref="UiText"/> (RT-073). Lo único que se decide aquí es el ritmo al que
/// avanza el reloj de reproducción y el suavizado entre dos ticks, que es la interpolación de render que
/// RT-020 permite.
/// </para>
/// </summary>
public partial class MatchScreen : Control
{
    /// <summary>Ticks lógicos por segundo (RT-020). A x1 el partido dura lo que duraría de verdad.</summary>
    private const float TicksPerSecond = 15f;

    /// <summary>Alto disponible para el log; el real se recorta a un número entero de líneas en <see cref="FitLog"/>.</summary>
    private const float LogHeight = 132f;

    private static readonly int[] Speeds = { 1, 4, 16 };

    private readonly List<MatchLogLine> _lines = new();

    private RunController _run = null!;
    private MatchTrace? _trace;
    private RichTextLabel _log = null!;
    private Label _scoreboard = null!;
    private Label _state = null!;
    private Label _clock = null!;
    private Label _selected = null!;
    private Label _progress = null!;
    private Button _play = null!;
    private Button _speed = null!;
    private Button _zone = null!;
    private Button _marking = null!;
    private Button _view3d = null!;
    private Button _silhouette = null!;
    private MatchPitchView _pitch = null!;
    private MatchPitchView3D _pitch3d = null!;
    private MatchTimelineView _timeline = null!;

    private int _frame;
    private double _carry;
    private int _revealed;

    /// <summary>Último fotograma con el que se refrescó todo lo que no es el campo; -1 fuerza el refresco.</summary>
    private int _synced = -1;

    /// <summary>Paso vertical de una línea del log, en píxeles; lo dicta la fuente, no una constante.</summary>
    private float _logStep;

    private int _speedIndex;
    private bool _playing = true;
    private int _selectedId = -1;

    // ADR 0094: la ventana de sustitución forzada; mientras está abierta la reproducción no avanza.
    private Control? _window;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Route(this);
            return;
        }

        _run = run;

        // El partido se juega al entrar en la pantalla: el mapa eligió el nodo y el ojeo lo confirmó.
        // Si ya se había jugado (se vuelve del informe), no se juega otra vez.
        if (_run.SelectedNodeId >= 0 && _run.State!.GetNode(_run.SelectedNodeId).IsMatch)
        {
            _run.PlayMatch(_run.SelectedNodeId);
        }

        if (_run.Playback is null)
        {
            Nav.Route(this);
            return;
        }

        _lines.AddRange(_run.MatchLog());
        _trace = _run.Playback.Trace;
        Build();
    }

    public override void _Process(double delta)
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        if (!_playing)
        {
            return;
        }

        _carry += delta * TicksPerSecond * Speeds[_speedIndex];
        int advance = (int)_carry;
        if (advance > 0)
        {
            _carry -= advance;
            _frame += advance;
        }

        if (_frame >= trace.FrameCount - 1)
        {
            _frame = trace.FrameCount - 1;
            _carry = 0d;
            _playing = false;
            _play.Text = UiText.Get("ui.match.resume");
        }

        Sync();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // ADR 0119/0120: esta pantalla es ahora el modo depuración de Partido; F3 vuelve a la
        // retransmisión sobre la misma RunController.Playback (nada se vuelve a jugar).
        if (@event is InputEventKey { Pressed: true, Keycode: Key.F3 })
        {
            Nav.Go(this, Nav.Match);
            return;
        }

        if (_window is not null)
        {
            return;
        }

        if (@event.IsActionPressed("ui_accept"))
        {
            TogglePlay();
        }
        else if (@event.IsActionPressed("ui_cancel"))
        {
            GoToReport();
        }
        else if (@event.IsActionPressed("ui_left"))
        {
            Step(-1);
        }
        else if (@event.IsActionPressed("ui_right"))
        {
            Step(1);
        }
    }

    private void Build()
    {
        var playback = _run.Playback!;
        var node = playback.Node;

        Layout.CenterLegacy(this);
        Widgets.Background(this);
        Widgets.Header(
            this,
            UiText.Get("ui.match.title"),
            UiText.Get(
                "ui.match.subtitle",
                UiText.Get("ui.kind." + node.Kind),
                node.Difficulty,
                node.Act,
                playback.Setup.Referee.Name));

        // Marcador: la banda de arriba entera, porque es lo único que se mira de lejos. Cada nombre va en
        // el color con el que su equipo se pinta en el campo, para no tener que recordar quién es quién.
        Widgets.Panel(this, new Rect2(12f, 54f, 1256f, 48f), Style.PanelSoft);
        var own = Widgets.Body(this, playback.OwnName, new Vector2(28f, 60f), 460f, Style.TeamOwn);
        own.HorizontalAlignment = HorizontalAlignment.Right;
        own.AddThemeFontSizeOverride("font_size", Style.TextLarge);

        _scoreboard = Widgets.Title(this, string.Empty, new Vector2(508f, 56f), 264f);
        _scoreboard.HorizontalAlignment = HorizontalAlignment.Center;

        var rival = Widgets.Body(this, playback.RivalName, new Vector2(792f, 60f), 460f, Style.TeamRival);
        rival.AddThemeFontSizeOverride("font_size", Style.TextLarge);

        _state = Widgets.Body(this, string.Empty, new Vector2(28f, 82f), 1224f, Style.TextDim);
        _state.HorizontalAlignment = HorizontalAlignment.Center;

        var legend = new LegendView
        {
            MatchMode = true,
            Position = new Vector2(16f, 106f),
            Size = new Vector2(1248f, 44f),

            // Vive en el hueco entre el marcador y el panel del campo, sobre la madera del fondo sin
            // ningún pergamino debajo: Style.TextDim (tinta oscura desde el retinte) se perdería ahí.
            TextColor = Style.OnWood,
        };
        AddChild(legend);

        // El campo: 16x6 casillas (ADR 0103) en 1120 px de ancho salen a 70 px por casilla, así que las seis
        // filas piden 420 px de alto para que la casilla sea cuadrada (si no, el jugador no puede estimar
        // distancias a ojo). El panel le añade el mismo margen de 4 px que tenía antes arriba y abajo. Ese
        // alto extra (antes 350, ahora 420: +70 px) sale del panel del log y del de controles, más abajo.
        Widgets.Panel(this, new Rect2(12f, 152f, 1256f, 508f));
        _pitch = new MatchPitchView
        {
            Trace = _trace,
            Position = new Vector2(80f, 156f),
            Size = new Vector2(1120f, 500f),
        };
        _pitch.PlayerPicked += OnPlayerPicked;
        AddChild(_pitch);

        // ADR 0102: el mismo rectángulo, en 3D con cámara ortográfica fija en tres cuartos. Arranca
        // apagado y la vista 2D se conserva: el 3D no la sustituye hasta que la iguale en legibilidad.
        _pitch3d = new MatchPitchView3D
        {
            Position = _pitch.Position,
            Size = _pitch.Size,
            Visible = false,
        };
        AddChild(_pitch3d);
        _pitch3d.Bind(_trace, _run.Playback?.Setup, _run.Catalog);
        BindFlashes();

        // Los dos interruptores de la vista viven en el canalón del panel del campo, que es el único hueco
        // que queda en la pantalla y además el sitio donde se busca lo que afecta al campo.
        _view3d = Widgets.Button(this, UiText.Get("ui.match.view2d"), new Rect2(1202f, 158f, 62f, 24f));
        _view3d.Pressed += TogglePitch3D;

        _silhouette = Widgets.Button(this, UiText.Get("ui.match.bwOff"), new Rect2(1202f, 186f, 62f, 24f), enabled: false);
        _silhouette.Pressed += ToggleSilhouette;

        // El log y el panel de controles bajan y encogen exactamente el alto que ganó el campo (70 px):
        // empezaban en y=516 y medían 234, ahora empiezan en y=586 y miden 164. Dentro, todo se reescala en
        // la misma proporción (164/234 ≈ 0,70) para que ningún botón se quede sin sitio.
        Widgets.Panel(this, new Rect2(12f, 666f, 888f, 84f));
        Widgets.Section(this, UiText.Get("ui.match.log"), new Vector2(24f, 590f), 400f);
        _progress = Widgets.Body(this, string.Empty, new Vector2(600f, 590f), 288f, Style.TextDim);
        _progress.HorizontalAlignment = HorizontalAlignment.Right;

        _log = new RichTextLabel
        {
            Position = new Vector2(24f, 610f),
            Size = new Vector2(864f, LogHeight),
            BbcodeEnabled = true,
            ScrollActive = true,
            ScrollFollowing = true,
            FitContent = false,
        };
        _log.AddThemeFontSizeOverride("normal_font_size", Style.TextSmall);
        _log.AddThemeColorOverride("default_color", Style.Text);
        AddChild(_log);
        FitLog();

        Widgets.Panel(this, new Rect2(908f, 666f, 360f, 84f));
        _clock = Widgets.Body(this, string.Empty, new Vector2(918f, 670f), 340f, Style.Accent);

        _timeline = new MatchTimelineView
        {
            Position = new Vector2(918f, 688f),
            Size = new Vector2(340f, 16f),
            FrameCount = _trace?.FrameCount ?? 0,
            Marks = BuildMarks(),
            RegulationFrame = RegulationFrame(),
        };
        _timeline.Seeked += OnSeeked;
        AddChild(_timeline);

        Widgets.Button(this, UiText.Get("ui.match.stepBack"), new Rect2(918f, 706f, 60f, 22f)).Pressed += () => Step(-1);

        _play = Widgets.Button(this, UiText.Get("ui.match.pause"), new Rect2(982f, 706f, 70f, 22f));
        _play.Pressed += TogglePlay;

        Widgets.Button(this, UiText.Get("ui.match.stepForward"), new Rect2(1056f, 706f, 60f, 22f)).Pressed += () => Step(1);

        _speed = Widgets.Button(this, "x" + Speeds[0].ToString(CultureInfo.InvariantCulture), new Rect2(1120f, 706f, 48f, 22f));
        _speed.Pressed += () =>
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            _speed.Text = "x" + Speeds[_speedIndex].ToString(CultureInfo.InvariantCulture);
        };

        _zone = Widgets.Button(this, UiText.Get("ui.match.zoneOff"), new Rect2(1172f, 706f, 86f, 22f));
        _zone.Pressed += () =>
        {
            _pitch.ShowZone = !_pitch.ShowZone;
            _zone.Text = UiText.Get(_pitch.ShowZone ? "ui.match.zoneOn" : "ui.match.zoneOff");
            _pitch.QueueRedraw();
        };

        Widgets.Button(this, UiText.Get("ui.match.end"), new Rect2(918f, 728f, 84f, 22f)).Pressed += GoToEnd;

        _marking = Widgets.Button(this, UiText.Get("ui.match.markOn"), new Rect2(1008f, 728f, 106f, 22f));
        _marking.Pressed += () =>
        {
            _pitch.ShowMarking = !_pitch.ShowMarking;
            _marking.Text = UiText.Get(_pitch.ShowMarking ? "ui.match.markOn" : "ui.match.markOff");
            _pitch.QueueRedraw();
        };

        Widgets.Button(this, UiText.Get("ui.match.report"), new Rect2(1120f, 728f, 138f, 22f)).Pressed += GoToReport;

        // UN suceso clave. Eran cuatro, luego tres, y baja a uno con el campo de seis filas (ADR 0103):
        // el panel de controles perdió 70 px de alto para dárselos al campo, y un suceso clave largo se
        // el panel de controles perdió 70 px de alto para dárselos al campo. La cuenta del panel entero, de
        // arriba abajo, que hay que rehacer si algo se mueve: borde 586 · reloj 590 (17) · barra 608 (16) ·
        // botones 626 y 650 (20) · sección 674 (16) · lista 690 (una línea, 17) · jugador seguido 712 (dos
        // líneas, 34) = 746, contra un borde inferior de 750. Las dos líneas del jugador seguido son las que cambian con cada clic, así que son las que
        // no pueden perderse; el resto de sucesos está a un vistazo en el log de al lado y en las marcas
        // de la barra.
        // ADR 0109: con siete filas el campo pide 500 px de alto y el panel de controles baja a 84.
        // El bloque de SUCESOS CLAVE sale de aquí: ya estaba reducido a uno (ADR 0103) y el log, que
        // ocupa los 888 px de al lado, enseña lo mismo y más. La línea del jugador seguido —que es la
        // que cambia con cada clic y por tanto la que no se puede perder— se muda a la cabecera del log.
        _selected = Widgets.Body(this, UiText.Get("ui.match.selectHint"), new Vector2(300f, 640f), 596f, Style.TextDim);

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseMatch"), UiText.Get("ui.input.padMatch"));

        if (_trace is null)
        {
            _state.Text = UiText.Get("ui.match.noTrace");
            _playing = false;
        }

        Sync();
    }

    // ------------------------------------------------------------------ vista del campo (ADR 0102)

    /// <summary>
    /// Cambia entre la vista 2D de siempre y la de cápsulas en 3D. Las dos leen la misma traza y el mismo
    /// fotograma, así que el cambio es instantáneo y no mueve el reloj: es exactamente lo que hace falta
    /// para comparar las dos en el mismo instante.
    /// </summary>
    private void TogglePitch3D()
    {
        bool on = !_pitch3d.Visible;
        _pitch3d.Visible = on;
        _pitch.Visible = !on;
        _view3d.Text = UiText.Get(on ? "ui.match.view3d" : "ui.match.view2d");
        _silhouette.Disabled = !on;
        Sync();
    }

    /// <summary>Modo silueta del 3D (RA-002): cápsulas negras planas sobre suelo blanco, sin color de equipo.</summary>
    private void ToggleSilhouette()
    {
        _pitch3d.SilhouetteMode = !_pitch3d.SilhouetteMode;
        _silhouette.Text = UiText.Get(_pitch3d.SilhouetteMode ? "ui.match.bwOn" : "ui.match.bwOff");
    }

    // ------------------------------------------------------------------ controles de reproducción

    private void TogglePlay()
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        // Darle a seguir con el partido terminado vuelve a empezar: es lo que se espera de un botón de
        // reproducción al final de la cinta, y evita tener que arrastrar la barra hasta el origen.
        if (!_playing && _frame >= trace.FrameCount - 1)
        {
            _frame = 0;
        }

        _playing = !_playing;
        _carry = 0d;
        _play.Text = UiText.Get(_playing ? "ui.match.pause" : "ui.match.resume");
        Sync();
    }

    private void Step(int delta)
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        _playing = false;
        _carry = 0d;
        _play.Text = UiText.Get("ui.match.resume");
        _frame = Mathf.Clamp(_frame + delta, 0, trace.FrameCount - 1);
        Sync();
    }

    private void GoToEnd()
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        _playing = false;
        _carry = 0d;
        _play.Text = UiText.Get("ui.match.resume");
        _frame = trace.FrameCount - 1;
        Sync();
    }

    private void OnSeeked(int frame)
    {
        _playing = false;
        _carry = 0d;
        _play.Text = UiText.Get("ui.match.resume");
        _frame = frame;
        Sync();
    }

    private void OnPlayerPicked(int playerId)
    {
        _selectedId = playerId;
        _pitch.SelectedId = playerId;
        _synced = -1;
        Sync();
    }

    private void GoToReport() => Nav.Go(this, Nav.Report);

    /// <summary>
    /// ADR 0094 (AZ-F): si la reproducción ha llegado al tick en el que un jugador propio salió por lesión o
    /// muerte y hay banquillo, se detiene ahí y abre la ventana. La decisión vuelve a pedir el partido con
    /// la sustitución incluida (RF-082: la decisión es estado inicial) y se sigue desde ese tick.
    /// </summary>
    private void CheckSubstitution(MatchTrace trace)
    {
        if (_window is not null)
        {
            return;
        }

        var point = _run.PendingSubstitution();
        if (point is null || trace.TickAt(_frame) < point.Tick)
        {
            return;
        }

        _frame = trace.FrameOfTick(point.Tick);
        _carry = 0d;
        _playing = false;
        _play.Text = UiText.Get("ui.match.resume");
        ShowSubstitutionWindow(point);
    }

    private void ShowSubstitutionWindow(SubstitutionPoint point)
    {
        var playback = _run.Playback!;
        string outName = point.OutPlayerId.ToString(CultureInfo.InvariantCulture);
        foreach (var player in playback.Setup.Home.Players)
        {
            if (player.Id == point.OutPlayerId)
            {
                outName = player.Name;
            }
        }

        var window = new Control { Position = Vector2.Zero, Size = new Vector2(1280f, 800f) };
        AddChild(window);
        // Cortina modal, no pergamino: sin parchment:false el rasgado de ParchmentPanel se dibujaría
        // sobre un rectángulo semitransparente que cubre la pantalla entera, donde no pinta nada.
        Widgets.Panel(window, new Rect2(0f, 0f, 1280f, 800f), new Color(0f, 0f, 0f, 0.62f), parchment: false);
        float height = 150f + (point.Candidates.Count * 36f);
        var area = new Rect2(340f, 400f - (height / 2f), 600f, height);
        Widgets.Panel(window, area, Style.Panel);
        Widgets.Title(window, UiText.Get("ui.match.subTitle"), new Vector2(area.Position.X + 20f, area.Position.Y + 14f), 560f);
        // El puesto y la casilla del que sale (BB-F). Esta es la vista de DEPURACIÓN (Nav.MatchDebug); la
        // que juega el revisor es la bandeja de pregón de BroadcastScreen, que además ofrece las otras dos
        // respuestas de la ADR 0134. Aquí basta con que el dato no falte, no con reproducir la bandeja.
        Widgets.Body(
            window,
            UiText.Get(point.Detail == "death" ? "ui.match.subDeath" : "ui.match.subInjury", outName)
                + $" · {UiText.Get("ui.pos." + point.OutPosition)} · ({point.OutCell.Column},{point.OutCell.Row})",
            new Vector2(area.Position.X + 20f, area.Position.Y + 52f),
            560f);
        for (int i = 0; i < point.Candidates.Count; i++)
        {
            var candidate = point.Candidates[i];
            string text = UiText.Get(
                "ui.match.subCandidate",
                candidate.Name,
                UiText.Get("ui.pos." + candidate.Position),
                UiText.Get("ui.state." + candidate.PhysicalState));
            int chosenId = candidate.Id;
            Widgets.Button(window, text, new Rect2(area.Position.X + 20f, area.Position.Y + 84f + (i * 36f), 560f, 30f)).Pressed +=
                () => ChooseSubstitute(point, chosenId);
        }

        Widgets.Body(window, UiText.Get("ui.match.subHint"), new Vector2(area.Position.X + 20f, area.End.Y - 40f), 560f, Style.TextDim);
        _window = window;
    }

    private void ChooseSubstitute(SubstitutionPoint point, int playerId)
    {
        _run.Substitute(new Substitution(point.Tick, point.OutPlayerId, playerId));
        _window?.QueueFree();
        _window = null;
        ReloadPlayback(point.Tick);
    }

    /// <summary>
    /// Pasa al campo los avisos de perk activado del partido (C9). La lista la compone <c>/Sim</c>
    /// (<see cref="MatchFlashView"/>) a partir de los eventos: aquí no se decide ni se calcula nada
    /// (RT-014), solo se entrega.
    /// </summary>
    private void BindFlashes()
    {
        var playback = _run.Playback;
        _pitch.Flashes = _trace is null || playback is null || _run.Catalog is null
            ? System.Array.Empty<MatchFlash>()
            : MatchFlashView.Build(playback.Result.Events, _trace, _run.Catalog);
    }

    /// <summary>Vuelve a cargar la reproducción tras una decisión y sigue desde <paramref name="tick"/>: hasta ahí el partido es el mismo.</summary>
    private void ReloadPlayback(int tick)
    {
        _trace = _run.Playback!.Trace;
        _pitch.Trace = _trace;
        _pitch3d.Bind(_trace, _run.Playback!.Setup, _run.Catalog);
        BindFlashes();
        _lines.Clear();
        _lines.AddRange(_run.MatchLog());
        _log.Clear();
        _revealed = 0;
        _synced = -1;
        _timeline.FrameCount = _trace?.FrameCount ?? 0;
        _timeline.Marks = BuildMarks();
        _timeline.RegulationFrame = RegulationFrame();
        _timeline.QueueRedraw();
        if (_trace is { FrameCount: > 0 } trace)
        {
            _frame = trace.FrameOfTick(tick);
        }

        _carry = 0d;
        _playing = true;
        _play.Text = UiText.Get("ui.match.pause");
        Sync();
    }

    // ------------------------------------------------------------------ sincronización con el tick

    /// <summary>
    /// Pone campo, marcador, reloj, barra y log en el mismo tick. Es el único sitio donde se decide qué
    /// se está enseñando: todo lo demás cambia <c>_frame</c> y llama aquí.
    /// </summary>
    private void Sync()
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            _progress.Text = UiText.Get("ui.match.progress", _lines.Count, _lines.Count);
            return;
        }

        _frame = Mathf.Clamp(_frame, 0, trace.FrameCount - 1);
        CheckSubstitution(trace);
        _pitch.Frame = _frame;
        _pitch.Alpha = _playing ? (float)_carry : 0f;
        _pitch.QueueRedraw();
        _pitch3d.Frame = _frame;
        _pitch3d.Alpha = _pitch.Alpha;

        // El campo se redibuja en todos los fotogramas de pantalla —es lo que suaviza el movimiento— pero
        // el resto solo cambia cuando cambia el tick. A x1 hay 15 ticks por segundo y 60 fotogramas: sin
        // esta puerta se estarían recomponiendo cuatro etiquetas de texto por cada tick de partido.
        if (_frame == _synced)
        {
            return;
        }

        _synced = _frame;
        _timeline.Frame = _frame;
        _timeline.QueueRedraw();

        int tick = trace.TickAt(_frame);
        SyncLog(tick);

        _clock.Text = UiText.Get(
            "ui.match.clock",
            trace.MinuteAt(_frame),
            tick,
            trace.TickAt(trace.FrameCount - 1),
            UiText.Get("ui.phase." + trace.PhaseAt(_frame)));

        int goalsFor = _revealed > 0 ? _lines[_revealed - 1].GoalsFor : 0;
        int goalsAgainst = _revealed > 0 ? _lines[_revealed - 1].GoalsAgainst : 0;
        _scoreboard.Text = goalsFor.ToString(CultureInfo.InvariantCulture) + " - " + goalsAgainst.ToString(CultureInfo.InvariantCulture);
        _progress.Text = UiText.Get("ui.match.progress", _revealed, _lines.Count);
        SyncSelected(trace);
        SyncState();
    }

    /// <summary>
    /// El log revelado hasta el tick que se pinta. Hacia delante solo añade; al retroceder rehace la
    /// ventana entera, que es la operación cara y por eso está acotada a <see cref="LogWindow"/> líneas.
    /// </summary>
    private void SyncLog(int tick)
    {
        int target = _revealed;
        if (target > 0 && _lines[target - 1].Tick > tick)
        {
            target = 0;
        }

        while (target < _lines.Count && _lines[target].Tick <= tick)
        {
            target++;
        }

        if (target == _revealed)
        {
            return;
        }

        if (target > _revealed)
        {
            for (int i = _revealed; i < target; i++)
            {
                Append(_lines[i]);
            }
        }
        else
        {
            // Se rehace el log ENTERO, no una ventana de las últimas líneas. La ventana ahorraba unas
            // decenas de AppendText al arrastrar hacia atrás y a cambio dejaba el minuto 10 inalcanzable:
            // el scroll se quedaba sin nada que enseñar más arriba. Un partido completo son del orden de
            // 150 líneas, así que rehacerlas todas cuesta menos que el redibujado que las acompaña.
            _log.Clear();
            for (int i = 0; i < target; i++)
            {
                Append(_lines[i]);
            }
        }

        _revealed = target;
        SnapLog();
    }

    private void Append(MatchLogLine line)
    {
        var color = ColorOf(line);
        string minute = line.Type == EventType.MatchStart
            ? UiText.Get("ui.match.kickoff")
            : UiText.Get("ui.match.minute", line.Minute);

        _log.AppendText($"[color=#{Style.TextDim.ToHtml(false)}]{Escape(minute)}[/color]  ");
        _log.AppendText($"[color=#{color.ToHtml(false)}]{Escape(Sentence(line))}[/color]\n");
    }



    private void SyncSelected(MatchTrace trace)
    {
        if (_selectedId < 0)
        {
            _selected.Text = UiText.Get("ui.match.selectHint");
            return;
        }

        for (int i = 0; i < trace.Players.Count; i++)
        {
            var player = trace.Players[i];
            if (player.Id != _selectedId)
            {
                continue;
            }

            _selected.Text = UiText.Get(
                "ui.match.selected",
                player.Name,
                UiText.Get("ui.pos." + player.Role),
                player.Number,
                UiText.Get("ui.pstate." + trace.StateAt(_frame, i)))
                + "\n" + Intent(trace, i);
            return;
        }

        _selected.Text = UiText.Get("ui.match.selectHint");
    }

    /// <summary>
    /// Qué está intentando hacer el jugador seguido: la acción que ganó su última tabla de utilidad
    /// (RT-098) y, si es marcar, a quién. Es la línea que convierte "coloca" —el estado de nueve de cada
    /// diez fichas— en una frase que dice algo.
    /// </summary>
    private string Intent(MatchTrace trace, int index)
    {
        var action = trace.ActionAt(_frame, index);
        if (action is null)
        {
            return UiText.Get("ui.match.noAction");
        }

        int mark = trace.MarkTargetAt(_frame, index);
        if (action == PlayerAction.MarkOpponent && mark >= 0)
        {
            return UiText.Get("ui.match.marking", trace.Players[mark].Name, trace.Players[mark].Number);
        }

        string text = UiText.Get("ui.match.doing", UiText.Get("ui.paction." + action));
        return mark >= 0
            ? text + " · " + UiText.Get("ui.match.markAssigned", trace.Players[mark].Number)
            : text;
    }

    private void SyncState()
    {
        var playback = _run.Playback!;
        var lines = new List<string>();
        if (_revealed >= _lines.Count)
        {
            lines.Add(UiText.Get("ui.match.final", UiText.Get(playback.Won ? "ui.match.won" : "ui.match.lost")));
        }

        if (playback.Result.Report.WentToGoldenGoal)
        {
            lines.Add(UiText.Get("ui.match.golden"));
        }

        if (playback.Result.Report.Forfeit)
        {
            lines.Add(UiText.Get("ui.match.forfeit"));
        }

        _state.Text = lines.Count == 0 ? UiText.Get("ui.match.hint") : string.Join(" · ", lines);
    }

    /// <summary>Marcas de la barra: un trazo por suceso clave, en el color con el que sale en el log.</summary>
    private TimelineMark[] BuildMarks()
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return System.Array.Empty<TimelineMark>();
        }

        var marks = new List<TimelineMark>();
        foreach (var line in _lines)
        {
            if (line.Highlight)
            {
                marks.Add(new TimelineMark(trace.FrameOfTick(line.Tick), ColorOf(line)));
            }
        }

        return marks.ToArray();
    }

    /// <summary>Fotograma del final del reglamentario, o -1 si el partido no pasó de ahí.</summary>
    private int RegulationFrame()
    {
        if (_trace is not { FrameCount: > 0 } trace || trace.TickAt(trace.FrameCount - 1) <= trace.RegulationTicks)
        {
            return -1;
        }

        return trace.FrameOfTick(trace.RegulationTicks);
    }

    /// <summary>
    /// La frase de una línea del log. El dato viene estructurado de <c>/Sim</c> y el idioma lo pone aquí
    /// (RT-073): el detalle del evento sale del vocabulario ya localizado de <c>data/l10n</c>, que es el
    /// mismo con el que se generan las descripciones de los perks.
    /// </summary>
    private string Sentence(MatchLogLine line)
    {
        string text = UiText.Get("ui.ev." + line.Type, line.ActorName, line.OtherName);
        string detail = Detail(line.Detail);
        if (detail.Length > 0)
        {
            text += " (" + detail + ")";
        }

        if (line.Cancelled)
        {
            text += " · " + UiText.Get("ui.match.cancelled");
        }

        return text;
    }

    /// <summary>Nombre localizado del detalle del evento; vacío si el catálogo no lo nombra.</summary>
    private string Detail(string detail)
    {
        if (detail.Length == 0)
        {
            return string.Empty;
        }

        int separator = detail.IndexOf(':');
        string key = separator < 0 ? detail : detail[..separator];

        // "GOL de Fulano (gol)" no informa de nada: el detalle solo se enseña cuando dice algo que la
        // frase no dice ya.
        if (key == "goal")
        {
            return string.Empty;
        }

        // El motor marca la muerte con el perk que la causó (RF-013): ninguna muerte sin culpable.
        if (key == "perk" && separator >= 0)
        {
            string id = detail[(separator + 1)..];
            return _run.Catalog!.Perks.Find(id)?.Name.Es ?? id;
        }

        var templates = _run.Catalog!.Localization.Get(Data.GameData.Language);
        return templates.Find("details", key) ?? string.Empty;
    }

    private static Color ColorOf(MatchLogLine line) => line.Type switch
    {
        EventType.Goal => Style.Accent,
        EventType.Substitution => Style.Accent,
        EventType.Death => Style.Hole,
        EventType.Injury => Style.Of(Sim.Model.PhysicalState.SevereInjury),
        EventType.Card => Style.Of(Sim.Model.PhysicalState.MinorInjury),
        EventType.MobStart or EventType.RefereeLeaves or EventType.MatchStart or EventType.MatchEnd => Style.Accent,
        _ => line.Side == MatchSide.Own ? Style.Text : Style.TextDim,
    };

    /// <summary>
    /// Recorta el alto del log a un número <b>entero</b> de líneas. La primera línea salía partida por
    /// arriba porque el área visible no medía un múltiplo del paso: con <c>ScrollFollowing</c> el texto
    /// queda pegado abajo y lo que sobra se lo come el borde de arriba, siempre los mismos píxeles. El
    /// paso no es una constante —lo dictan la fuente y la separación del tema— así que se mide aquí.
    /// </summary>
    private void FitLog()
    {
        var font = _log.GetThemeFont("normal_font");
        _logStep = font.GetHeight(Style.TextSmall) + _log.GetThemeConstant("line_separation");
        if (_logStep <= 0f)
        {
            return;
        }

        var box = _log.GetThemeStylebox("normal");
        float chrome = box.GetMargin(Side.Top) + box.GetMargin(Side.Bottom);
        int lines = Mathf.Max(1, Mathf.FloorToInt((LogHeight - chrome) / _logStep));
        _log.Size = new Vector2(_log.Size.X, (lines * _logStep) + chrome);
        _log.GetVScrollBar().ValueChanged += _ => SnapLog();
    }

    /// <summary>
    /// Deja el scroll del log en un múltiplo del paso de línea. Con el área visible ya ajustada esto solo
    /// hace falta cuando el propio jugador arrastra la barra, pero es la misma cuenta y cuesta una resta.
    /// </summary>
    private void SnapLog()
    {
        if (_logStep <= 0f)
        {
            return;
        }

        var bar = _log.GetVScrollBar();
        double snapped = Mathf.Floor(bar.Value / _logStep) * _logStep;
        if (Mathf.Abs(bar.Value - snapped) > 0.01d)
        {
            bar.Value = snapped;
        }
    }

    /// <summary>Los corchetes son marcas de BBCode: un nombre no puede abrir una etiqueta por accidente.</summary>
    private static string Escape(string text) => text.Replace("[", "[lb]");
}
