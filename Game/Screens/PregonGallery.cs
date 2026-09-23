using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;

namespace Underleague.Game.Screens;

/// <summary>
/// Galería de la retransmisión de partido, «voz de pregón» (ADR 0119, ADR 0120): compone en un lienzo
/// lógico de 1920×1200 (docs/ui/README.md §7) el tablero, el campo (un rectángulo verde plano —esta
/// escena NO se conecta a ningún partido real todavía) y la fila de tiras, y sobre esa base muestra cada
/// momento de la retransmisión con datos inventados fijos, para poder juzgar la composición contra las
/// capturas de referencia sin depender de una run jugada.
/// <para>
/// Argumento de línea de órdenes tras <c>--</c>: uno de <c>base gol roja lesion turba muerte final
/// bandeja</c>. Sin argumento, recorre los ocho y guarda una captura de cada uno en
/// <c>Game/screenshots/pregon-&lt;estado&gt;.png</c>.
/// </para>
/// </summary>
public partial class PregonGallery : Control
{
    private const float CanvasWidth = 1920f;
    private const float CanvasHeight = 1200f;
    private const float StripY = 1105f;

    private static readonly string[] States = { "base", "gol", "roja", "lesion", "turba", "muerte", "final", "bandeja" };
    private static readonly string[] Names = { "Hrakka", "Torka", "Lurtz", "Thrak", "Rukh", "Ruk", "Mazka" };
    private static readonly string[] Roles =
    {
        "ui.pos.Goalkeeper", "ui.pos.Defender", "ui.pos.Defender", "ui.pos.Midfielder",
        "ui.pos.Midfielder", "ui.pos.Midfielder", "ui.pos.Forward",
    };

    private BroadcastBoard _board = null!;
    private readonly List<PlayerStrip> _strips = new();
    private BenchPlaque _bench = null!;
    private Stamp _foulStamp = null!;
    private Stamp _perkStamp = null!;
    private HeraldBanner _banner = null!;
    private ProclamationBand _band = null!;
    private Edict _edict = null!;
    private MatchRecord _record = null!;
    private DecisionTray _tray = null!;

    private string _directory = string.Empty;

    public override void _Ready()
    {
        Size = new Vector2(CanvasWidth, CanvasHeight);
        Position = Vector2.Zero;
        Theme = Pregon.BuildTheme();
        MouseFilter = MouseFilterEnum.Stop;

        var viewport = GetViewport().GetVisibleRect().Size;
        float scale = viewport.X > 0f ? viewport.X / CanvasWidth : 1f;
        Scale = new Vector2(scale, scale);

        _directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(_directory);

        Build();

        var args = OS.GetCmdlineUserArgs();
        _ = Run(args.Length > 0 ? args[0] : null);
    }

    private void Build()
    {
        Widgets.Panel(this, new Rect2(Vector2.Zero, new Vector2(CanvasWidth, CanvasHeight)), new Color("41502f"));
        Widgets.Panel(this, new Rect2(new Vector2(0f, 300f), new Vector2(CanvasWidth, 690f)), Style.Grass);

        _board = new BroadcastBoard();
        AddChild(_board);
        _board.Position = new Vector2(0f, 8f);
        _board.Size = new Vector2(CanvasWidth, BroadcastBoard.DesignHeight);
        _board.SetTeams("Nuestro F. C.", "Rival Utd.");
        _board.SetScore(2, 1);
        _board.SetProgress(0.62f);
        _board.SetRivalResidue(UiText.Get("ui.pregon.board.residue", 1, 1));
        _board.SetSpeedIndex(0);

        float x0 = (CanvasWidth - ((7 * 232f) + (6 * 12f) + 24f + 150f)) / 2f;
        var states = new[] { PhysicalState.Healthy, PhysicalState.Healthy, PhysicalState.MinorInjury, PhysicalState.Healthy, PhysicalState.SevereInjury, PhysicalState.Healthy, PhysicalState.Healthy };
        for (int i = 0; i < Names.Length; i++)
        {
            var strip = new PlayerStrip();
            AddChild(strip);
            strip.Position = new Vector2(x0 + (i * 244f), StripY);
            strip.Size = new Vector2(PlayerStrip.DesignWidth, PlayerStrip.DesignHeight);
            string subtitle = UiText.Get("ui.pregon.strip.subtitle", UiText.Get(Roles[i]), "orco");
            strip.SetModel(new StripModel(i + 1, Names[i].ToUpperInvariant(), subtitle, 2, states[i], Off: false));
            _strips.Add(strip);
        }

        _bench = new BenchPlaque();
        AddChild(_bench);
        _bench.Position = new Vector2(x0 + (7 * 244f) + 12f, StripY);
        _bench.Size = new Vector2(BenchPlaque.DesignWidth, BenchPlaque.DesignHeight);
        _bench.SetCount(2);

        _foulStamp = new Stamp();
        AddChild(_foulStamp);
        _foulStamp.Position = new Vector2(980f, 165f);

        _perkStamp = new Stamp();
        AddChild(_perkStamp);
        _perkStamp.Position = new Vector2(720f, 470f);

        _banner = new HeraldBanner();
        AddChild(_banner);
        _banner.Position = new Vector2(0f, 150f);
        _banner.Size = new Vector2(HeraldBanner.DesignWidth, HeraldBanner.DesignHeight);

        _band = new ProclamationBand();
        AddChild(_band);
        _band.Position = new Vector2(0f, 150f);
        _band.Size = new Vector2(CanvasWidth, ProclamationBand.DesignHeight);

        _edict = new Edict();
        AddChild(_edict);
        _edict.Position = new Vector2(0f, 150f);
        _edict.Size = new Vector2(Edict.DesignWidth, Edict.DesignHeight);

        _record = new MatchRecord();
        AddChild(_record);
        _record.Position = Vector2.Zero;
        _record.Size = new Vector2(CanvasWidth, CanvasHeight);

        _tray = new DecisionTray();
        AddChild(_tray);

        // La bandeja sustituye a la fila de tiras y usa el hueco que deja el campo hasta el borde del
        // lienzo — es más alta que una tira (DesignHeight), no la misma franja estrecha.
        _tray.Position = new Vector2(0f, CanvasHeight - 20f - DecisionTray.DesignHeight);
        _tray.Size = new Vector2(CanvasWidth, DecisionTray.DesignHeight);
    }

    private void Apply(string state)
    {
        Reset();
        switch (state)
        {
            case "gol":
                _banner.Show(ours: true, UiText.Get("ui.pregon.banner.said"), "Gol", "de Mazka Comecráneos,\nal minuto sesenta y tres", "¡Viva Nuestro F. C.!");
                break;
            case "roja":
                _banner.Show(ours: false, UiText.Get("ui.pregon.banner.said"), "Expulsado", "Grunk Rompecostillas, del Rival,\npor entrar como entra la peste", "Juegan con seis");
                break;
            case "lesion":
                _banner.Show(ours: true, UiText.Get("ui.pregon.banner.said"), "Herido", "Thrak Mascadientes, medio,\ncon la pierna donde no debe", "Lesión grave · sale del campo");
                break;
            case "turba":
                _band.Show(UiText.Get("ui.pregon.turba.header"), UiText.Get("ui.pregon.turba.body"));
                break;
            case "muerte":
                _edict.Show("Mazka Comecráneos", "delantero, n.º 7, orco; rematado por «Sed de tuétano»\nde Grunk, del Rival, al minuto sesenta y tres");
                ShowTray(outState: "muerto", outSquare: "D4", recommendedName: "Brakk");
                break;
            case "final":
                _record.Show("Nuestro F. C.", "Rival Utd.", 2, 1, "Goles de Mazka (2) y Grunk · un expulsado · un muerto\nLa crónica completa, en la gaceta de mañana");
                break;
            case "bandeja":
                ShowTray(outState: UiText.Get("ui.state.MinorInjury"), outSquare: "D4", recommendedName: "Brakk");
                break;
            default:
                _foulStamp.Show(UiText.Get("ui.pregon.stamp.foul"), StampTone.Foul, large: true);
                _perkStamp.Show("Sangre caliente", StampTone.Perk, large: false);
                break;
        }
    }

    private void ShowTray(string outState, string outSquare, string recommendedName)
    {
        foreach (var strip in _strips)
        {
            strip.Visible = false;
        }

        _bench.Visible = false;
        _tray.SetOutgoing(new OutgoingModel(7, "Mazka", UiText.Get("ui.pos.Forward"), outSquare, outState));
        // La galería enseña la bandeja en su caso más cargado (ADR 0134): dos candidatos con riesgo y las
        // dos respuestas que no son un candidato, que es lo que tiene que caber sin comerse el Confirmar.
        _tray.SetCandidates(
            new[]
            {
                new CandidateModel(
                    8, recommendedName, UiText.Get("ui.pos.Forward"), UiText.Get("ui.state.Healthy"),
                    Recommended: true, UiText.Get("ui.pregon.tray.candidateRisk", "1,4 %")),
                new CandidateModel(
                    9, "Narg", UiText.Get("ui.pos.Midfielder"), UiText.Get("ui.state.MinorInjury"),
                    Recommended: false, UiText.Get("ui.pregon.tray.candidateRisk", "6,2 %")),
            },
            new[]
            {
                new TrayOption("decline", UiText.Get("ui.pregon.tray.decline"), UiText.Get("ui.pregon.tray.declineSub")),
                // Con su riesgo: quedarse tocado multiplica la probabilidad de morir (ADR 0134 E), y la
                // galería tiene que enseñar el caso peor, que es el que puede no caber.
                new TrayOption(
                    "playOn", UiText.Get("ui.pregon.tray.playOn"), UiText.Get("ui.pregon.tray.playOnSub"),
                    UiText.Get("ui.pregon.tray.candidateRisk", "23,7 %")),
            });
    }

    private void Reset()
    {
        _foulStamp.Visible = false;
        _perkStamp.Visible = false;
        _banner.Visible = false;
        _band.Visible = false;
        _edict.Visible = false;
        _record.Visible = false;
        _tray.Visible = false;
        foreach (var strip in _strips)
        {
            strip.Visible = true;
        }

        _bench.Visible = true;
    }

    private async Task Run(string? state)
    {
        if (string.IsNullOrEmpty(state))
        {
            foreach (var s in States)
            {
                Apply(s);
                await Settle();
                await Save(s);
            }
        }
        else
        {
            Apply(state);
            await Settle();
            await Save(state);
        }

        GetTree().Quit();
    }

    private async Task Settle()
    {
        for (int i = 0; i < 6; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task Save(string state)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, "frame_post_draw");
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(Path.Combine(_directory, "pregon-" + state + ".png"));
        GD.Print($"captura: pregon-{state}.png");
    }
}
