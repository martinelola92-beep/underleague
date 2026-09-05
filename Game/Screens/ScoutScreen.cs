using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>ojeo</b> (RF-012b): el informe completo del rival antes de decidir. Es opcional y
/// gratuito, y es el sitio donde se cumple o se rompe el principio rector del juego —todo lo malo que
/// pase en el partido tenía que ser previsible (RF-012d)—, así que enseña las cuatro cosas que hacen
/// previsible una muerte:
/// <list type="number">
/// <item>la <b>plantilla íntegra</b> del rival, con la misma ficha que la propia (UI-010) y su build;</item>
/// <item>el <b>árbitro</b> con su rasgo (RF-061);</item>
/// <item>los <b>perks letales destacados</b> (RF-013): desde la ADR 0048 un jugador sano también puede
/// morir, y lo único que separa eso del azar injusto es que se sepa antes;</item>
/// <item>el <b>riesgo por titular</b>, con su número (RF-012c), que es el mismo que tirará el motor y que
/// se mueve con a quién alineas, en qué estado y en qué casilla: por eso el botón de alinear está aquí,
/// al lado del número.</item>
/// </list>
/// <para>Nada de esto lo calcula la pantalla (RT-014): el partido lo arma <c>RunEngine.BuildMatch</c> —el
/// mismo que se va a jugar—, los perks letales los lista <c>Sim.Perks.Scouting</c> y los riesgos,
/// <c>RunEngine.LethalRisks</c>.</para>
/// </summary>
public partial class ScoutScreen : Control
{
    private readonly List<PlayerCard> _cards = new();

    private RunController _run = null!;
    private int _nodeId = -1;
    private int _expanded = -1;
    private TeamState _rival = null!;
    private VBoxContainer _list = null!;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun || run.SelectedNodeId < 0)
        {
            Nav.Route(this);
            return;
        }

        _run = run;
        _nodeId = run.SelectedNodeId;

        var state = run.State!;
        var catalog = run.Catalog!;
        var node = state.GetNode(_nodeId);
        var (setup, _, _) = RunEngine.BuildMatch(state, _nodeId, catalog, run.Engine);

        Widgets.Background(this);
        Widgets.Header(this, UiText.Get("ui.scout.title"), UiText.Get(
            "ui.scout.subtitle",
            UiText.Get("ui.kind." + node.Kind),
            node.Difficulty,
            UiText.Get("ui.difficulty." + (node.Difficulty <= 0 ? 5 : node.Difficulty))));

        BuildRivalList(catalog, setup.Away);
        BuildReport(state, catalog, node, setup);
        BuildButtons();

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        if (Tour.Active)
        {
            Tour.Step(this, "ojeo", () => Nav.Go(this, Nav.Team));
        }
    }

    /// <summary>La plantilla rival, con la misma ficha que la propia y el mismo patrón de inspección (UI-001, UI-010).</summary>
    private void BuildRivalList(Sim.Data.Catalog catalog, TeamSetup away)
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 690f));
        Widgets.Section(this, UiText.Get("ui.scout.rival"), new Vector2(24f, 60f), 340f);

        _rival = TeamState.Of(catalog, away);
        _list = new VBoxContainer
        {
            Position = new Vector2(22f, 82f),
            Size = new Vector2(356f, 650f),
        };
        _list.AddThemeConstantOverride("separation", 3);
        AddChild(_list);

        var scene = GD.Load<PackedScene>("res://Scenes/PlayerCard.tscn");
        var players = new List<PlayerDefinition>(away.Players);
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        foreach (var player in players)
        {
            var card = scene.Instantiate<PlayerCard>();
            _list.AddChild(card);
            card.Bind(_rival, player, System.Array.Empty<string>());
            card.Activated += OnCardActivated;
            _cards.Add(card);
        }
    }

    /// <summary>Activar expande una ficha y solo una (UI-012), igual que en Equipo.</summary>
    private void OnCardActivated(int playerId)
    {
        _expanded = _expanded == playerId ? -1 : playerId;
        foreach (var card in _cards)
        {
            bool current = card.PlayerId == _expanded;
            card.Expanded = current;
            card.Selected = current;
        }
    }

    private void BuildReport(RunState state, Sim.Data.Catalog catalog, MapNode node, MatchSetup setup)
    {
        Widgets.Panel(this, new Rect2(396f, 52f, 872f, 690f));

        float y = 62f;
        var profile = Scouting.Profile(setup.Away, catalog);

        y = Block(UiText.Get("ui.scout.build"), new List<string>
        {
            UiText.Get(
                "ui.scout.buildLine",
                catalog.Race(profile.Race).Name.Es,
                profile.AverageLevel,
                Top(catalog, profile)),
        }, y);

        y = Block(UiText.Get("ui.scout.referee"), new List<string>
        {
            UiText.Get("ui.scout.refereeLine", setup.Referee.Name, setup.Referee.Trait),
            UiText.Get("ui.scout.refereeGap"),
        }, y);

        // RF-013: los perks letales, destacados. Si no hay ninguno, se dice: la ausencia de amenaza es
        // información igual de accionable que la amenaza.
        var threats = Scouting.LethalPerks(setup.Away, catalog);
        var lethalLines = new List<string>();
        var templates = catalog.Localization.Get(GameData.Language);
        foreach (var threat in threats)
        {
            var perk = catalog.Perks.Get(threat.PerkId);
            lethalLines.Add(UiText.Get(
                "ui.scout.lethalLine",
                threat.PlayerName,
                threat.PerkName.Es,
                DescriptionGenerator.Describe(perk, templates)));
        }

        if (lethalLines.Count == 0)
        {
            lethalLines.Add(UiText.Get("ui.scout.lethalNone"));
        }

        y = Block(UiText.Get("ui.scout.lethal"), lethalLines, y, threats.Count > 0 ? Style.Hole : Style.Text);

        // El número de RF-012c, por titular. Es el mismo que el motor tira, no una estimación.
        var risks = RunEngine.LethalRisks(state, node.Id, catalog, _run.Engine);
        var riskLines = new List<string>();
        foreach (var risk in risks)
        {
            if (risk.Risk <= 0)
            {
                continue;
            }

            var player = state.FindPlayer(risk.PlayerId);
            riskLines.Add(UiText.Get("ui.scout.riskLine", player?.Name ?? "?", Percent(risk.Risk)));
        }

        if (riskLines.Count == 0)
        {
            riskLines.Add(UiText.Get("ui.scout.riskNone"));
        }

        y = Block(UiText.Get("ui.scout.risk"), riskLines, y, riskLines.Count > 0 && risks.Count > 0 ? Style.Text : Style.TextDim);

        // Lo que hay que advertir antes de confirmar (RF-012d, RF-002d, RF-093).
        var warnings = RunEngine.LineupWarnings(state, node.Id, catalog, _run.Engine);
        var warningLines = new List<string>();
        foreach (var warning in warnings)
        {
            var player = state.FindPlayer(warning.PlayerId);
            warningLines.Add(warning.Kind switch
            {
                LineupWarningKind.Shorthanded => UiText.Get("ui.scout.warnShorthanded"),
                LineupWarningKind.SevereInjuryDeathRisk => UiText.Get("ui.scout.warnSevere", player?.Name ?? "?"),
                _ => UiText.Get("ui.scout.warnLethal", player?.Name ?? "?", Percent(warning.Risk)),
            });
        }

        if (warningLines.Count > 0)
        {
            y = Block(UiText.Get("ui.scout.warnings"), warningLines, y, Style.Accent);
        }

        // El once con el que se juega, que es lo que el jugador cambia si el número no le gusta.
        var starters = new List<string>();
        foreach (var slot in state.Lineup.Slots)
        {
            var player = state.FindPlayer(slot.PlayerId);
            if (player is not null)
            {
                starters.Add($"{player.Name} · {UiText.Get("ui.pos." + player.Position)} · "
                    + $"{UiText.Get("ui.state." + player.PhysicalState)} · ({slot.HomeCell.Column},{slot.HomeCell.Row})");
            }
        }

        Block(UiText.Get("ui.scout.starters"), starters, y);
    }

    /// <summary>Las etiquetas que más se repiten: es lo que hace reconocible a un rival (RF-015).</summary>
    private static string Top(Sim.Data.Catalog catalog, TeamProfile profile)
    {
        var parts = new List<string>();
        for (int i = 0; i < profile.Styles.Count && i < 2; i++)
        {
            parts.Add(catalog.Style(profile.Styles[i].Style).Name.Es + " x" + profile.Styles[i].Count);
        }

        for (int i = 0; i < profile.Traits.Count && i < 2; i++)
        {
            parts.Add(catalog.Trait(profile.Traits[i].Trait).Name.Es + " x" + profile.Traits[i].Count);
        }

        return string.Join(", ", parts);
    }

    /// <summary>Probabilidad en base 10.000 escrita como porcentaje con un decimal (RF-012c).</summary>
    private static string Percent(int risk) => UiText.Get("ui.risk.percent", risk / 100, (risk % 100) / 10);

    /// <summary>Un bloque de informe: título en acento y sus líneas debajo. Devuelve la y siguiente.</summary>
    private float Block(string title, IReadOnlyList<string> lines, float y, Color? color = null)
    {
        Widgets.Section(this, title, new Vector2(412f, y), 830f);
        y += 18f;
        foreach (string line in lines)
        {
            var label = Widgets.Body(this, line, new Vector2(412f, y), 830f, color);
            y += label.Size.Y + 2f;
        }

        return y + 12f;
    }

    private void BuildButtons()
    {
        var lineup = Widgets.Button(this, UiText.Get("ui.scout.lineup"), new Rect2(1002f, 58f, 120f, 28f));
        lineup.Pressed += () => Nav.Go(this, Nav.Team);

        var start = Widgets.Button(this, UiText.Get("ui.scout.start"), new Rect2(1130f, 58f, 124f, 28f));
        start.Pressed += StartMatch;

        var back = Widgets.Button(this, UiText.Get("ui.nav.back"), new Rect2(886f, 58f, 100f, 28f));
        back.Pressed += () =>
        {
            _run.SelectedNodeId = -1;
            Nav.Go(this, Nav.Map);
        };
    }

    /// <summary>
    /// Empezar. La pantalla de partido es la que entra en el nodo: entrar lo resuelve entero y es ella
    /// quien tiene que enseñar lo que pasó (RF-119).
    /// </summary>
    private void StartMatch()
    {
        _run.SelectedNodeId = _nodeId;
        Nav.Go(this, Nav.Match);
    }
}
