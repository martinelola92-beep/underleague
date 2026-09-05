using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
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
/// </summary>
public partial class StartScreen : Control
{
    private readonly List<(ClubDefinition Club, Button Button)> _clubButtons = new();

    private Catalog _catalog = null!;
    private ClubCatalog _clubs = null!;
    private ClubDefinition? _club;
    private LineEdit _seed = null!;
    private Label _chosen = null!;
    private Label _description = null!;

    public override void _Ready()
    {
        // El recorrido de capturas de la pantalla de Equipo (docs/ui-equipo.md §13) se lanza con
        // --screenshots y esta pantalla es ahora la principal: se le cede el paso sin más.
        if (Tour.Screenshots && !Tour.Active)
        {
            CallDeferred(MethodName.GoToTeam);
            return;
        }

        _catalog = DataLoader.FromJson(GameData.Snapshot);
        _clubs = ClubLoader.FromJson(GameData.Snapshot);

        Widgets.Background(this);
        Widgets.Header(this, UiText.Get("ui.start.title"), UiText.Get("ui.start.subtitle"));

        BuildClubPanel();
        BuildSeedPanel();
        BuildSavePanel();

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

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

    private void BuildClubPanel()
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 620f));
        Widgets.Section(this, UiText.Get("ui.start.club"), new Vector2(24f, 60f), 340f);
        Widgets.Body(this, UiText.Get("ui.start.clubHint"), new Vector2(24f, 78f), 340f, Style.TextDim);

        float y = 112f;
        foreach (var club in LaunchClubs())
        {
            var button = Widgets.Button(this, club.Name.Es, new Rect2(24f, y, 340f, 26f));
            var chosen = club;
            button.Pressed += () => Choose(chosen);
            _clubButtons.Add((club, button));
            y += 32f;
        }

        _chosen = Widgets.Body(this, string.Empty, new Vector2(24f, y + 8f), 340f, Style.Accent);
        _description = Widgets.Body(this, string.Empty, new Vector2(24f, y + 30f), 340f);
    }

    private void BuildSeedPanel()
    {
        Widgets.Panel(this, new Rect2(396f, 52f, 872f, 200f));
        Widgets.Section(this, UiText.Get("ui.start.seed"), new Vector2(412f, 60f), 500f);
        Widgets.Body(this, UiText.Get("ui.start.seedHint"), new Vector2(412f, 78f), 830f, Style.TextDim);

        _seed = new LineEdit
        {
            Position = new Vector2(412f, 106f),
            Size = new Vector2(240f, 28f),
            Text = "20260905",
        };
        _seed.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        AddChild(_seed);

        var random = Widgets.Button(this, UiText.Get("ui.start.random"), new Rect2(664f, 106f, 120f, 28f));
        random.Pressed += RandomSeed;

        var begin = Widgets.Button(this, UiText.Get("ui.start.begin"), new Rect2(412f, 152f, 240f, 32f));
        begin.Pressed += Begin;
    }

    private void BuildSavePanel()
    {
        Widgets.Panel(this, new Rect2(396f, 268f, 872f, 120f));
        bool exists = RunController.SaveExists;
        Widgets.Body(
            this,
            exists ? UiText.Get("ui.nav.continue") : UiText.Get("ui.start.noSave"),
            new Vector2(412f, 278f),
            830f,
            exists ? Style.Accent : Style.TextDim);

        if (!exists)
        {
            return;
        }

        var button = Widgets.Button(this, UiText.Get("ui.start.continue"), new Rect2(412f, 306f, 300f, 32f));
        button.Pressed += ContinueRun;
    }

    /// <summary>Clubes jugables al lanzamiento (uno por raza con <c>launch: true</c>), en orden estable de id.</summary>
    private List<ClubDefinition> LaunchClubs() =>
        _clubs.All.Where(c => _catalog.Race(c.Race).Launch).ToList();

    private void Choose(ClubDefinition club)
    {
        _club = club;
        _chosen.Text = UiText.Get("ui.start.chosen", club.Name.Es);

        var templates = _catalog.Localization.Get(GameData.Language);
        var ability = string.IsNullOrEmpty(club.SpecialRule) ? null : _catalog.Perks.Find(club.SpecialRule);
        string abilityLine = ability is null
            ? string.Empty
            : "\n\n" + UiText.Get("ui.start.ability").ToUpperInvariant() + "\n"
                + ability.Name.Es + ": " + DescriptionGenerator.Describe(ability, templates);

        _description.Text = club.Description.Es + abilityLine;

        foreach (var (candidate, button) in _clubButtons)
        {
            button.AddThemeColorOverride("font_color", candidate.Id == club.Id ? Style.Accent : Style.Text);
        }
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
