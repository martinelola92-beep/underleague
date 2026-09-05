using System.Collections.Generic;
using System.Globalization;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>inicio</b>: elegir club y semilla, empezar, o continuar la run guardada (RT-061).
/// <para>
/// El club es hoy una raza (RF-004: todos los jugadores del club inicial son de una única raza) porque
/// <c>data/clubs/</c> no existe todavía; el hueco se enseña como hueco. Cada raza se elige leyendo lo que
/// la hace distinta —su descripción y su <b>habilidad racial</b>, que RF-031b pide que sea visible aquí—
/// generada desde el efecto del perk, nunca escrita a mano (RT-035).
/// </para>
/// <para>
/// La <b>semilla</b> se escribe o se sortea. Sortearla es lo único aleatorio de todo el juego que no sale
/// de una semilla, y puede serlo: es la <i>entrada</i> del determinismo, no parte de él. En cuanto entra
/// en <see cref="RunController.NewRun"/>, mapas, rivales y dados salen de ella (RT-021).
/// </para>
/// </summary>
public partial class StartScreen : Control
{
    private readonly List<(Race Race, Button Button)> _raceButtons = new();

    private Catalog _catalog = null!;
    private Race _race;
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

        Widgets.Background(this);
        Widgets.Header(this, UiText.Get("ui.start.title"), UiText.Get("ui.start.subtitle"));

        BuildClubPanel();
        BuildSeedPanel();
        BuildSavePanel();

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        var races = LaunchRaces();
        Choose(races.Count > 0 ? races[0] : Race.Human);

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
        foreach (var race in LaunchRaces())
        {
            var definition = _catalog.Race(race);
            var button = Widgets.Button(this, definition.Name.Es, new Rect2(24f, y, 340f, 26f));
            var chosen = race;
            button.Pressed += () => Choose(chosen);
            _raceButtons.Add((race, button));
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

    /// <summary>Razas jugables al lanzamiento, en orden estable (el mismo criterio que <c>/Balance</c>).</summary>
    private List<Race> LaunchRaces()
    {
        var races = new List<Race>();
        for (int i = 0; i < _catalog.Races.Count; i++)
        {
            if (_catalog.Races[i].Launch)
            {
                races.Add(_catalog.Races[i].Id);
            }
        }

        races.Sort();
        return races;
    }

    private void Choose(Race race)
    {
        _race = race;
        var definition = _catalog.Race(race);
        _chosen.Text = UiText.Get("ui.start.chosen", definition.Name.Es);

        var templates = _catalog.Localization.Get(GameData.Language);
        var ability = _catalog.Perks.Find(definition.Ability);
        string abilityLine = ability is null
            ? string.Empty
            : "\n\n" + UiText.Get("ui.start.ability").ToUpperInvariant() + "\n"
                + ability.Name.Es + ": " + DescriptionGenerator.Describe(ability, templates);

        _description.Text = definition.Description.Es + abilityLine;

        foreach (var (candidate, button) in _raceButtons)
        {
            button.AddThemeColorOverride("font_color", candidate == race ? Style.Accent : Style.Text);
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
        if (run is null)
        {
            return;
        }

        if (!ulong.TryParse(_seed.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
        {
            seed = 1UL;
            _seed.Text = UiText.Get("ui.start.badSeed", seed);
        }

        run.NewRun(_race, seed);
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
