using System.Collections.Generic;
using System.Globalization;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Events;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Partido</b> (RF-121, <c>ui-run-minima.md</c>): marcador, resultado y el log de eventos
/// con scroll. <b>Sin campo animado</b>: el partido se resuelve entero y aquí se enseña lo que pasó.
/// <para>
/// La decisión de diseño que la sostiene: el partido ya está jugado, así que <b>leer el log es
/// opcional</b>. El jugador puede verlo caer a la velocidad que quiera, mostrarlo entero de golpe o
/// saltar directo al informe; lo que no puede es perderse un gol o una muerte, y por eso los sucesos
/// clave viven aparte, en la columna izquierda, y no hay que rescatarlos del scroll.
/// </para>
/// <para>
/// La pantalla <b>no calcula nada del juego</b> (RT-014): el partido lo resuelve <c>/Sim</c> y el log lo
/// compone <c>Sim.Run.View.MatchLogView</c> como dato estructurado; aquí solo se le pone la frase en
/// español de <see cref="UiText"/> (RT-073) y se decide a qué ritmo aparece.
/// </para>
/// </summary>
public partial class MatchScreen : Control
{
    /// <summary>Sucesos que caen por segundo a velocidad x1: un partido largo se lee en medio minuto.</summary>
    private const float BaseRate = 22f;

    private static readonly int[] Speeds = { 1, 4, 16 };

    private readonly List<MatchLogLine> _lines = new();
    private readonly List<string> _highlights = new();

    private RunController _run = null!;
    private RichTextLabel _log = null!;
    private Label _scoreboard = null!;
    private Label _state = null!;
    private Label _highlightList = null!;
    private Label _progress = null!;
    private Button _play = null!;
    private Button _speed = null!;

    private int _revealed;
    private int _speedIndex;
    private bool _playing = true;
    private float _pending;
    private int _goalsFor;
    private int _goalsAgainst;

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
        Build();
    }

    public override void _Process(double delta)
    {
        if (!_playing || _revealed >= _lines.Count)
        {
            return;
        }

        _pending += (float)delta * BaseRate * Speeds[_speedIndex];
        while (_pending >= 1f && _revealed < _lines.Count)
        {
            _pending -= 1f;
            Reveal(_lines[_revealed]);
            _revealed++;
        }

        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept"))
        {
            RevealAll();
        }
        else if (@event.IsActionPressed("ui_cancel"))
        {
            GoToReport();
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

        // Marcador: ocupa la banda de arriba entera porque es lo único que el jugador mira de lejos.
        Widgets.Panel(this, new Rect2(12f, 56f, 1256f, 84f), Style.PanelSoft);
        var own = Widgets.Body(this, playback.OwnName, new Vector2(28f, 74f), 460f, Style.Text);
        own.HorizontalAlignment = HorizontalAlignment.Right;
        own.AddThemeFontSizeOverride("font_size", Style.TextLarge);

        _scoreboard = Widgets.Title(this, string.Empty, new Vector2(508f, 70f), 264f);
        _scoreboard.HorizontalAlignment = HorizontalAlignment.Center;

        var rival = Widgets.Body(this, playback.RivalName, new Vector2(792f, 74f), 460f, Style.TextDim);
        rival.AddThemeFontSizeOverride("font_size", Style.TextLarge);

        _state = Widgets.Body(this, string.Empty, new Vector2(28f, 112f), 1224f, Style.TextDim);
        _state.HorizontalAlignment = HorizontalAlignment.Center;

        // Columna izquierda de 376 px, la misma que Equipo: aquí no son fichas, son los sucesos que no se
        // pueden perder de vista (goles, tarjetas, lesiones, muertes y turba).
        Widgets.Panel(this, new Rect2(12f, 152f, Widgets.CardColumnWidth, 588f));
        Widgets.Section(this, UiText.Get("ui.match.highlights"), new Vector2(24f, 158f), 350f);
        _highlightList = Widgets.Body(this, UiText.Get("ui.match.highlightsNone"), new Vector2(24f, 180f), 352f);

        Widgets.Panel(this, new Rect2(400f, 152f, 868f, 588f));
        Widgets.Section(this, UiText.Get("ui.match.log"), new Vector2(412f, 158f), 400f);
        _progress = Widgets.Body(this, string.Empty, new Vector2(900f, 158f), 356f, Style.TextDim);
        _progress.HorizontalAlignment = HorizontalAlignment.Right;

        _log = new RichTextLabel
        {
            Position = new Vector2(412f, 180f),
            Size = new Vector2(844f, 516f),
            BbcodeEnabled = true,
            ScrollActive = true,
            ScrollFollowing = true,
            FitContent = false,
        };
        _log.AddThemeFontSizeOverride("normal_font_size", Style.TextSmall);
        _log.AddThemeColorOverride("default_color", Style.Text);
        AddChild(_log);

        _play = Widgets.Button(this, UiText.Get("ui.match.pause"), new Rect2(412f, 706f, 120f, 26f));
        _play.Pressed += () =>
        {
            _playing = !_playing;
            _play.Text = UiText.Get(_playing ? "ui.match.pause" : "ui.match.resume");
        };

        _speed = Widgets.Button(this, UiText.Get("ui.match.speed", Speeds[0]), new Rect2(542f, 706f, 140f, 26f));
        _speed.Pressed += () =>
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            _speed.Text = UiText.Get("ui.match.speed", Speeds[_speedIndex]);
        };

        Widgets.Button(this, UiText.Get("ui.match.all"), new Rect2(692f, 706f, 140f, 26f)).Pressed += RevealAll;
        Widgets.Button(this, UiText.Get("ui.match.report"), new Rect2(1076f, 706f, 180f, 26f)).Pressed += GoToReport;
        Widgets.Body(this, UiText.Get("ui.match.hint"), new Vector2(12f, 736f), 1256f, Style.TextDim);

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseMatch"), UiText.Get("ui.input.padMatch"));
        Refresh();
    }

    private void Reveal(MatchLogLine line)
    {
        _goalsFor = line.GoalsFor;
        _goalsAgainst = line.GoalsAgainst;

        var color = ColorOf(line);
        string minute = line.Type == EventType.MatchStart
            ? UiText.Get("ui.match.kickoff")
            : UiText.Get("ui.match.minute", line.Minute);

        _log.AppendText($"[color=#{Style.TextDim.ToHtml(false)}]{Escape(minute)}[/color]  ");
        _log.AppendText($"[color=#{color.ToHtml(false)}]{Escape(Sentence(line))}[/color]\n");

        if (line.Highlight)
        {
            _highlights.Add(minute + "  " + Sentence(line));
            if (_highlights.Count > 34)
            {
                _highlights.RemoveAt(0);
            }
        }
    }

    private void RevealAll()
    {
        while (_revealed < _lines.Count)
        {
            Reveal(_lines[_revealed]);
            _revealed++;
        }

        _playing = false;
        _play.Text = UiText.Get("ui.match.resume");
        Refresh();
    }

    private void GoToReport() => Nav.Go(this, Nav.Report);

    private void Refresh()
    {
        _scoreboard.Text = _goalsFor.ToString(CultureInfo.InvariantCulture) + " - " + _goalsAgainst.ToString(CultureInfo.InvariantCulture);
        _progress.Text = UiText.Get("ui.match.progress", _revealed, _lines.Count);
        _highlightList.Text = _highlights.Count == 0
            ? UiText.Get("ui.match.highlightsNone")
            : string.Join("\n", _highlights);

        var playback = _run.Playback!;
        var lines = new List<string>();
        if (_revealed >= _lines.Count)
        {
            lines.Add(UiText.Get("ui.match.final", UiText.Get(playback.Won ? "ui.match.won" : "ui.match.lost")));
            if (playback.Result.Report.WentToGoldenGoal)
            {
                lines.Add(UiText.Get("ui.match.golden"));
            }

            if (playback.Result.Report.Forfeit)
            {
                lines.Add(UiText.Get("ui.match.forfeit"));
            }
        }
        else if (_revealed > 0)
        {
            lines.Add(UiText.Get("ui.match.minute", _lines[_revealed - 1].Minute));
        }

        _state.Text = string.Join(" · ", lines);
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
