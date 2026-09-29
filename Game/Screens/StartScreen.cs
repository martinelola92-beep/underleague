using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
using Underleague.Game.Ui.Knavall;
using Underleague.Sim.Data;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Clubs;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>inicio</b>: elegir club y semilla, empezar, o continuar la run guardada (RT-061).
/// <para>
/// El club (RF-004, <c>data/clubs/</c>) es la unidad de elección, no la raza suelta: todos los jugadores
/// del club inicial pertenecen a una única raza, pero lo que el jugador elige tiene nombre propio. La
/// "regla especial" de RF-004 (<c>ClubDefinition.SpecialRule</c>) apunta hoy a la habilidad racial de esa
/// raza (ADR 0026) —no se ha inventado una mecánica de club nueva—, y se enseña igual que antes: generada
/// desde el efecto del perk, nunca escrita a mano (RT-035).
/// </para>
/// <para>
/// La <b>semilla</b> se escribe o se sortea. Sortearla es lo único aleatorio de todo el juego que no sale
/// de una semilla, y puede serlo: es la <i>entrada</i> del determinismo, no parte de él. En cuanto entra
/// en <see cref="RunController.NewRun"/>, mapas, rivales y dados salen de ella (RT-021).
/// </para>
/// <para>
/// <b>Lenguaje de Knavall (ADR 0162):</b> el nombre del juego en un cartel (ADR 0123 D1, solo en este
/// texto: el renombrado del código sigue pendiente), los clanes como carteles con el retrato de su raza,
/// la ficha del elegido con su habilidad racial en un cartel de ayuda, y empezar, semilla y continuar como
/// placas. La lógica de elegir, sortear, empezar y continuar no cambia.
/// </para>
/// </summary>
public partial class StartScreen : Control
{

    private Catalog _catalog = null!;
    private ClubCatalog _clubs = null!;
    private ClubDefinition? _club;
    private LineEdit _seed = null!;
    private ClubBoard _board = null!;

    public override void _Ready()
    {
        // El recorrido de capturas de la pantalla de Equipo (docs/ui-equipo.md §13) se lanza con
        // --screenshots y esta pantalla es ahora la principal: se le cede el paso sin más.
        if (Tour.Screenshots && !Tour.Active)
        {
            CallDeferred(MethodName.GoToTeam);
            return;
        }

        Layout.CenterLegacy(this);

        // La única pantalla que pide su propia música: es la escena principal del proyecto, así que Godot
        // la carga sin pasar por Nav.Go, que es donde ScreenAudio decide para todas las demás.
        ScreenAudio.Apply(Nav.Start);

        _catalog = DataLoader.FromJson(GameData.Snapshot);
        _clubs = ClubLoader.FromJson(GameData.Snapshot);

        // Mesa de madera detrás de todo, y los carteles de ayuda dibujados enteros por InkTooltip: el panel
        // nativo que los envuelve se vacía solo en esta pantalla (copia del Theme de pantallas viejas).
        var table = new WoodTable { Position = Vector2.Zero, Size = Layout.LegacySize };
        AddChild(table);
        var theme = (Theme)Widgets.BuildLegacyTheme().Duplicate();
        theme.SetStylebox("panel", "TooltipPanel", new StyleBoxEmpty());
        Theme = theme;

        _board = new ClubBoard { Position = Vector2.Zero, Size = new Vector2(Layout.LegacySize.X, 756f) };
        AddChild(_board);
        _board.Picked += id =>
        {
            foreach (var club in LaunchClubs())
            {
                if (club.Id == id)
                {
                    Choose(club);
                }
            }
        };

        BuildActions();

        Widgets.InputHelp(this, UiText.Get("ui.start.kn.inputMouse"), UiText.Get("ui.input.padPending"));

        var clubs = LaunchClubs();
        if (clubs.Count > 0)
        {
            Choose(clubs[0]);
        }

        if (Tour.Active)
        {
            Tour.Step(this, "inicio", () => Begin());
        }
    }

    /// <summary>
    /// Las acciones, en el panel de la derecha: EMPEZAR es la placa grande; la semilla, secundaria, con su
    /// explicación en el cartel de ayuda; CONTINUAR solo si hay una run guardada.
    /// </summary>
    private void BuildActions()
    {
        var area = ClubBoard.ActionArea;
        float left = area.Position.X + 22f;
        float width = area.Size.X - 52f;

        var begin = PlaqueButton.Create(this, UiText.Get("ui.start.begin"), Glyph.Ball, PlaqueKind.Primary, new Rect2(left, area.Position.Y + 18f, width, 72f), 501);
        begin.FontSize = 28;
        begin.Tip = new Tip(UiText.Get("ui.start.begin"), UiText.Get("ui.start.kn.beginTip"), Glyph.Ball);
        begin.Pressed += Begin;

        var label = new Label { Text = UiText.Get("ui.start.seed").ToUpperInvariant(), Position = new Vector2(left + 4f, area.Position.Y + 110f) };
        label.AddThemeFontOverride("font", Ink.Display);
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", Ink.RedDark);
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);

        _seed = new LineEdit
        {
            Position = new Vector2(left + 100f, area.Position.Y + 104f),
            Size = new Vector2(180f, 40f),
            Text = "20260905",
            TooltipText = UiText.Get("ui.start.seedHint"),
        };
        var box = new StyleBoxFlat
        {
            BgColor = Ink.Paper,
            BorderColor = Ink.Black,
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            ShadowColor = Ink.Shadow,
            ShadowSize = 1,
            ShadowOffset = new Vector2(3f, 4f),
            ContentMarginLeft = 10f,
        };
        var focus = (StyleBoxFlat)box.Duplicate();
        focus.BorderColor = Ink.OchreDark;
        _seed.AddThemeStyleboxOverride("normal", box);
        _seed.AddThemeStyleboxOverride("focus", focus);
        _seed.AddThemeFontOverride("font", Ink.Heavy);
        _seed.AddThemeFontSizeOverride("font_size", 20);
        _seed.AddThemeColorOverride("font_color", Ink.Black);
        AddChild(_seed);

        var random = PlaqueButton.Create(this, UiText.Get("ui.start.kn.random"), Glyph.None, PlaqueKind.Paper, new Rect2(left + 292f, area.Position.Y + 100f, width - 292f, 50f), 502);
        random.FontSize = 18;
        random.Tip = new Tip(UiText.Get("ui.start.seed"), UiText.Get("ui.start.seedHint"), Glyph.Info);
        random.Pressed += RandomSeed;

        if (RunController.SaveExists)
        {
            var resume = PlaqueButton.Create(this, UiText.Get("ui.start.continue"), Glyph.Back, PlaqueKind.Wood, new Rect2(left, area.Position.Y + 172f, width, 60f), 503);
            resume.FontSize = 20;
            resume.Tip = new Tip(UiText.Get("ui.start.continue"), UiText.Get("ui.start.kn.continueTip"), Glyph.Back);
            resume.Pressed += ContinueRun;
        }
        else
        {
            var none = new Label { Text = UiText.Get("ui.start.noSave"), Position = new Vector2(left + 4f, area.Position.Y + 190f) };
            none.AddThemeFontOverride("font", Ink.Plain);
            none.AddThemeFontSizeOverride("font_size", 16);
            none.AddThemeColorOverride("font_color", Ink.Muted);
            none.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(none);
        }
    }

    /// <summary>Clubes jugables al lanzamiento (uno por raza con <c>launch: true</c>), en orden estable de id.</summary>
    private List<ClubDefinition> LaunchClubs() =>
        _clubs.All.Where(c => _catalog.Race(c.Race).Launch).ToList();

    private void Choose(ClubDefinition club)
    {
        _club = club;
        var templates = _catalog.Localization.Get(GameData.Language);
        var cards = new List<ClubCard>();
        foreach (var candidate in LaunchClubs())
        {
            var ability = string.IsNullOrEmpty(candidate.SpecialRule) ? null : _catalog.Perks.Find(candidate.SpecialRule);
            var tip = ability is null
                ? null
                : new Tip(ability.Name.Es, DescriptionGenerator.Describe(ability, templates) + "\n" + UiText.Get("ui.kn.tip.racial"), Glyph.Racial);
            cards.Add(new ClubCard(
                candidate.Id,
                candidate.Race,
                candidate.Name.Es,
                _catalog.Race(candidate.Race).Name.Es,
                candidate.Description.Es,
                candidate.StartingGold,
                tip));
        }

        _board.Bind(cards, club.Id, UiText.Get("ui.start.title"), UiText.Get("ui.start.kn.tagline"));
    }

    /// <summary>
    /// Semilla al azar. Es el único sorteo del juego que no sale de una semilla, y por eso usa una
    /// instancia propia de Godot y no toca nada de <c>/Sim</c> (RT-021): lo que se sortea aquí es la
    /// entrada del determinismo, no un resultado del juego.
    /// </summary>
    private void RandomSeed()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        _seed.Text = (rng.Randi() % 100_000_000U).ToString(CultureInfo.InvariantCulture);
    }

    private void Begin()
    {
        var run = RunController.Instance;
        if (run is null || _club is null)
        {
            return;
        }

        if (!ulong.TryParse(_seed.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
        {
            seed = 1UL;
            _seed.Text = UiText.Get("ui.start.badSeed", seed);
        }

        run.NewRun(_club.Id, _club.Race, seed);
        Nav.Go(this, Nav.Map);
    }

    private void GoToTeam() => Nav.Go(this, Nav.Team);

    private void ContinueRun()
    {
        var run = RunController.Instance;
        if (run is not null && run.Continue())
        {
            Nav.Route(this);
        }
    }
}
