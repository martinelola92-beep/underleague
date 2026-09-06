using System.Collections.Generic;
using System.Globalization;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
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

    /// <summary>Líneas de log que se rehacen al retroceder. Más arriba no se mira; el resto vive en la barra.</summary>
    private const int LogWindow = 160;

    private static readonly int[] Speeds = { 1, 4, 16 };

    private readonly List<MatchLogLine> _lines = new();

    private RunController _run = null!;
    private MatchTrace? _trace;
    private RichTextLabel _log = null!;
    private Label _scoreboard = null!;
    private Label _state = null!;
    private Label _clock = null!;
    private Label _highlightList = null!;
    private Label _selected = null!;
    private Label _progress = null!;
    private Button _play = null!;
    private Button _speed = null!;
    private Button _zone = null!;
    private MatchPitchView _pitch = null!;
    private MatchTimelineView _timeline = null!;

    private int _frame;
    private double _carry;
    private int _revealed;

    /// <summary>Último fotograma con el que se refrescó todo lo que no es el campo; -1 fuerza el refresco.</summary>
    private int _synced = -1;

    private int _speedIndex;
    private bool _playing = true;
    private int _selectedId = -1;

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
        };
        AddChild(legend);

        // El campo: 16x5 casillas cuadradas de 70 px. Ocupa la mitad de la pantalla porque es lo que hay
        // que mirar; el resto de la pantalla es contexto de lo que se está viendo en él.
        Widgets.Panel(this, new Rect2(12f, 152f, 1256f, 358f));
        _pitch = new MatchPitchView
        {
            Trace = _trace,
            Position = new Vector2(80f, 156f),
            Size = new Vector2(1120f, 350f),
        };
        _pitch.PlayerPicked += OnPlayerPicked;
        AddChild(_pitch);

        Widgets.Panel(this, new Rect2(12f, 516f, 888f, 234f));
        Widgets.Section(this, UiText.Get("ui.match.log"), new Vector2(24f, 520f), 400f);
        _progress = Widgets.Body(this, string.Empty, new Vector2(600f, 520f), 288f, Style.TextDim);
        _progress.HorizontalAlignment = HorizontalAlignment.Right;

        _log = new RichTextLabel
        {
            Position = new Vector2(24f, 540f),
            Size = new Vector2(864f, 198f),
            BbcodeEnabled = true,
            ScrollActive = true,
            ScrollFollowing = true,
            FitContent = false,
        };
        _log.AddThemeFontSizeOverride("normal_font_size", Style.TextSmall);
        _log.AddThemeColorOverride("default_color", Style.Text);
        AddChild(_log);

        Widgets.Panel(this, new Rect2(908f, 516f, 360f, 234f));
        _clock = Widgets.Body(this, string.Empty, new Vector2(918f, 520f), 340f, Style.Accent);

        _timeline = new MatchTimelineView
        {
            Position = new Vector2(918f, 540f),
            Size = new Vector2(340f, 22f),
            FrameCount = _trace?.FrameCount ?? 0,
            Marks = BuildMarks(),
            RegulationFrame = RegulationFrame(),
        };
        _timeline.Seeked += OnSeeked;
        AddChild(_timeline);

        Widgets.Button(this, UiText.Get("ui.match.stepBack"), new Rect2(918f, 570f, 60f, 26f)).Pressed += () => Step(-1);

        _play = Widgets.Button(this, UiText.Get("ui.match.pause"), new Rect2(982f, 570f, 70f, 26f));
        _play.Pressed += TogglePlay;

        Widgets.Button(this, UiText.Get("ui.match.stepForward"), new Rect2(1056f, 570f, 60f, 26f)).Pressed += () => Step(1);

        _speed = Widgets.Button(this, "x" + Speeds[0].ToString(CultureInfo.InvariantCulture), new Rect2(1120f, 570f, 48f, 26f));
        _speed.Pressed += () =>
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            _speed.Text = "x" + Speeds[_speedIndex].ToString(CultureInfo.InvariantCulture);
        };

        _zone = Widgets.Button(this, UiText.Get("ui.match.zoneOff"), new Rect2(1172f, 570f, 86f, 26f));
        _zone.Pressed += () =>
        {
            _pitch.ShowZone = !_pitch.ShowZone;
            _zone.Text = UiText.Get(_pitch.ShowZone ? "ui.match.zoneOn" : "ui.match.zoneOff");
            _pitch.QueueRedraw();
        };

        Widgets.Button(this, UiText.Get("ui.match.end"), new Rect2(918f, 602f, 100f, 26f)).Pressed += GoToEnd;
        Widgets.Button(this, UiText.Get("ui.match.report"), new Rect2(1026f, 602f, 232f, 26f)).Pressed += GoToReport;

        Widgets.Section(this, UiText.Get("ui.match.highlights"), new Vector2(918f, 636f), 340f);
        _highlightList = Widgets.Body(this, UiText.Get("ui.match.highlightsNone"), new Vector2(918f, 654f), 340f);
        _selected = Widgets.Body(this, UiText.Get("ui.match.selectHint"), new Vector2(918f, 722f), 340f, Style.TextDim);

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseMatch"), UiText.Get("ui.input.padMatch"));

        if (_trace is null)
        {
            _state.Text = UiText.Get("ui.match.noTrace");
            _playing = false;
        }

        Sync();
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
        _pitch.Frame = _frame;
        _pitch.Alpha = _playing ? (float)_carry : 0f;
        _pitch.QueueRedraw();

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

        SyncHighlights();
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
            _log.Clear();
            for (int i = Mathf.Max(0, target - LogWindow); i < target; i++)
            {
                Append(_lines[i]);
            }
        }

        _revealed = target;
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

    /// <summary>Los últimos sucesos que no se pueden perder de vista; la barra dice además dónde están.</summary>
    private void SyncHighlights()
    {
        var recent = new List<string>();
        for (int i = _revealed - 1; i >= 0 && recent.Count < 4; i--)
        {
            if (_lines[i].Highlight)
            {
                recent.Insert(0, UiText.Get("ui.match.minute", _lines[i].Minute) + "  " + Sentence(_lines[i]));
            }
        }

        _highlightList.Text = recent.Count == 0
            ? UiText.Get("ui.match.highlightsNone")
            : string.Join("\n", recent);
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
                UiText.Get("ui.pstate." + trace.StateAt(_frame, i)));
            return;
        }

        _selected.Text = UiText.Get("ui.match.selectHint");
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
        EventType.Death => Style.Hole,
        EventType.Injury => Style.Of(Sim.Model.PhysicalState.SevereInjury),
        EventType.Card => Style.Of(Sim.Model.PhysicalState.MinorInjury),
        EventType.MobStart or EventType.RefereeLeaves or EventType.MatchStart or EventType.MatchEnd => Style.Accent,
        _ => line.Side == MatchSide.Own ? Style.Text : Style.TextDim,
    };

    /// <summary>Los corchetes son marcas de BBCode: un nombre no puede abrir una etiqueta por accidente.</summary>
    private static string Escape(string text) => text.Replace("[", "[lb]");
}
