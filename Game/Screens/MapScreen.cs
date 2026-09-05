using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Run;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>mapa</b>: adónde ir (RF-010), con qué te vas a encontrar (RF-012) y con cuánta gente
/// cuentas (RF-002e).
/// <para>
/// Las tres cosas que <c>docs/ui-run-minima.md</c> pide de esta pantalla son las tres que convierten el
/// mapa en una decisión y no en un pasillo:
/// <list type="number">
/// <item>el <b>distintivo de dificultad</b> de cada partido, con color y forma (RF-012, UI-002);</item>
/// <item>los <b>mercados destacados</b> y a cuántos saltos queda el más cercano (RF-011b), que es lo que
/// hace de jugar en inferioridad una decisión en vez de una trampa;</item>
/// <item>el <b>contador de disponibles frente al mínimo</b>, arriba y siempre visible (RF-002e).</item>
/// </list>
/// </para>
/// <para>La pantalla no calcula nada de eso: los nodos los da <c>RunController.Available</c>, la
/// distancia al mercado <c>Sim.Run.RunSummary.HopsToMarket</c> y el distintivo viene en el propio
/// <see cref="MapNode"/> (RT-014).</para>
/// </summary>
public partial class MapScreen : Control
{
    private RunController _run = null!;
    private MapView _view = null!;
    private Label _subtitle = null!;
    private Label _available = null!;
    private VBoxContainer _choices = null!;
    private Label _state = null!;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Go(this, Nav.Start);
            return;
        }

        _run = run;
        Widgets.Background(this);
        _subtitle = Widgets.Header(this, UiText.Get("ui.map.title"), string.Empty);

        BuildAvailableCounter();
        BuildChoicesPanel();
        BuildGraphPanel();
        BuildButtons();

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        Refresh();

        if (Tour.Active)
        {
            Tour.Step(this, "mapa", TourPickMatch);
        }
    }

    /// <summary>
    /// El contador de RF-002e: arriba a la derecha, permanente, y en el color del estado físico que le
    /// corresponde —verde con holgura, ámbar con lo justo, rojo al borde— porque el número solo asusta
    /// cuando se compara, y aquí se compara siempre con el mínimo.
    /// </summary>
    private void BuildAvailableCounter()
    {
        Widgets.Panel(this, new Rect2(966f, 8f, 302f, 38f), Style.PanelSoft);
        _available = Widgets.Body(this, string.Empty, new Vector2(978f, 14f), 280f, Style.Text);
        Widgets.Body(this, UiText.Get("ui.map.availableHint"), new Vector2(978f, 30f), 280f, Style.TextDim);
    }

    private void BuildChoicesPanel()
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 690f));
        Widgets.Section(this, UiText.Get("ui.map.choose"), new Vector2(24f, 60f), 340f);

        _choices = new VBoxContainer
        {
            Position = new Vector2(22f, 82f),
            Size = new Vector2(356f, 650f),
        };
        _choices.AddThemeConstantOverride("separation", 6);
        AddChild(_choices);
    }

    private void BuildGraphPanel()
    {
        Widgets.Panel(this, new Rect2(396f, 52f, 872f, 400f));
        _view = new MapView
        {
            Position = new Vector2(410f, 96f),
            Size = new Vector2(844f, 320f),
        };
        _view.NodePressed += OnNodePressed;
        AddChild(_view);

        Widgets.Body(this, UiText.Get("ui.map.legend"), new Vector2(410f, 424f), 844f, Style.TextDim);

        // Debajo del grafo, cómo va la run: es la misma cuenta que enseñará la pantalla de fin de run
        // (Sim.Run.RunSummary), y verla por el camino es lo que convierte el mapa en un sitio donde se
        // decide y no solo se avanza.
        Widgets.Panel(this, new Rect2(396f, 464f, 872f, 278f));
        Widgets.Section(this, UiText.Get("ui.map.state"), new Vector2(412f, 474f), 830f);
        _state = Widgets.Body(this, string.Empty, new Vector2(412f, 494f), 830f);
    }

    private void BuildButtons()
    {
        var team = Widgets.Button(this, UiText.Get("ui.nav.team"), new Rect2(410f, 60f, 120f, 26f));
        team.Pressed += () => Nav.Go(this, Nav.Team);

        var quit = Widgets.Button(this, UiText.Get("ui.map.quit"), new Rect2(1138f, 60f, 116f, 26f));
        quit.Pressed += () =>
        {
            _run.Save();
            Nav.Go(this, Nav.Start);
        };

        // Abandonar la run (RF-007), con confirmación en el propio botón: es irreversible —borra el
        // guardado— y no merece un diálogo, pero tampoco una sola pulsación.
        var abandon = Widgets.Button(this, UiText.Get("ui.map.abandon"), new Rect2(1002f, 60f, 128f, 26f));
        bool armed = false;
        abandon.Pressed += () =>
        {
            if (!armed)
            {
                armed = true;
                abandon.Text = UiText.Get("ui.map.abandonSure");
                return;
            }

            _run.Abandon();
            Nav.Go(this, Nav.Start);
        };
    }

    private void Refresh()
    {
        var state = _run.State!;
        var catalog = _run.Catalog!;

        _subtitle.Text = UiText.Get(
            "ui.map.subtitle",
            state.Act,
            RunRules.Acts,
            catalog.Race(state.ClubRace).Name.Es,
            state.Gold);

        int available = state.AvailablePlayerCount;
        _available.Text = UiText.Get("ui.map.available", available, RunRules.MinimumAvailablePlayers);
        _available.AddThemeColorOverride("font_color", AvailableColor(available));

        var map = state.CurrentMap;
        var nodes = _run.Available();
        var ids = new List<int>(nodes.Count);
        foreach (var node in nodes)
        {
            ids.Add(node.Id);
        }

        _view.Map = map;
        _view.CurrentNodeId = state.CurrentNodeId;
        _view.AvailableIds = ids;
        _view.VisitedIds = VisitedIds(state);
        _view.QueueRedraw();

        _state.Text = string.Join("\n", new[]
        {
            UiText.Get("ui.map.progress", state.NodeHistory.Count + 1, MapInvariants.PathLength(map)),
            UiText.Get(
                "ui.end.stats",
                RunSummary.ActsCleared(state),
                RunRules.Acts,
                RunSummary.NodesVisited(state),
                RunSummary.MatchesWon(state),
                RunSummary.MatchesPlayed(state),
                state.Gold),
            UiText.Get("ui.map.roster", state.RosterSize, state.RosterCapacity, RunSummary.Fallen(state).Count),
        });

        BuildChoices(nodes);
    }

    /// <summary>Verde con holgura, ámbar con un jugador de margen, rojo con lo justo (UI-002 en cifra).</summary>
    private static Color AvailableColor(int available)
    {
        int margin = available - RunRules.MinimumAvailablePlayers;
        return margin >= 2 ? Style.Of(Underleague.Sim.Model.PhysicalState.Healthy)
            : margin == 1 ? Style.Of(Underleague.Sim.Model.PhysicalState.MinorInjury)
            : Style.Of(Underleague.Sim.Model.PhysicalState.SevereInjury);
    }

    private static List<int> VisitedIds(RunState state)
    {
        var visited = new List<int>(state.NodeHistory.Count);
        for (int i = 0; i < state.NodeHistory.Count; i++)
        {
            visited.Add(state.NodeHistory[i].NodeId);
        }

        return visited;
    }

    /// <summary>Un botón por nodo accesible, con lo que hay que saber antes de pulsarlo y nada más.</summary>
    private void BuildChoices(IReadOnlyList<MapNode> nodes)
    {
        foreach (var child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
            child.QueueFree();
        }

        if (nodes.Count == 0)
        {
            Widgets.Body(_choices, UiText.Get("ui.map.noNodes"), Vector2.Zero, 340f, Style.TextDim);
            return;
        }

        foreach (var node in nodes)
        {
            var row = new Control();
            _choices.AddChild(row);

            var badge = new NodeBadge
            {
                Node = node,
                Position = new Vector2(2f, 2f),
                Size = new Vector2(28f, 28f),
            };
            row.AddChild(badge);

            var button = Widgets.Button(row, UiText.Get("ui.kind." + node.Kind), new Rect2(36f, 2f, 316f, 28f));
            int id = node.Id;
            button.Pressed += () => OnNodePressed(id);
            button.MouseEntered += () =>
            {
                _view.HighlightedId = id;
                _view.QueueRedraw();
            };

            // El alto de la fila lo manda el detalle: un rival con nombre largo ocupa tres líneas y la
            // fila siguiente no puede montársele encima.
            var detail = Widgets.Body(row, DetailOf(node), new Vector2(36f, 32f), 316f, Style.TextDim);
            row.CustomMinimumSize = new Vector2(356f, 36f + detail.Size.Y);
        }
    }

    /// <summary>
    /// La línea de detalle: dificultad para un partido y distancia al mercado para todo lo demás. Es
    /// deliberadamente corta: el informe completo está a un clic, en el ojeo (RF-012b).
    /// </summary>
    private string DetailOf(MapNode node)
    {
        var lines = new List<string>();
        if (node.IsMatch && node.Difficulty > 0)
        {
            lines.Add(UiText.Get("ui.map.difficulty", node.Difficulty, UiText.Get("ui.difficulty." + node.Difficulty)));
        }

        // El rival tiene nombre (RF-015: son personajes que el jugador aprende, no bloques de estadísticas).
        if (node.OpponentId.Length > 0 && _run.Systems!.Rivals.Find(node.OpponentId) is { } rival)
        {
            lines.Add(rival.Name.Es);
        }

        if (node.Kind == NodeKind.Boss)
        {
            lines.Add(UiText.Get("ui.map.boss", node.Act));
        }

        if (node.Kind == NodeKind.Market)
        {
            lines.Add(UiText.Get("ui.map.marketHere"));
        }
        else
        {
            int hops = RunSummary.HopsToMarket(_run.State!, node.Id);
            lines.Add(hops < 0 ? UiText.Get("ui.map.marketNone") : UiText.Get("ui.map.marketHops", hops));
        }

        return string.Join(" · ", lines);
    }

    /// <summary>
    /// Elegir nodo. Un partido no se entra: primero se ojea (RF-012b), que es gratis y opcional pero
    /// tiene que estar antes de la decisión irreversible. Lo demás se entra y la ruta decide adónde
    /// lleva.
    /// </summary>
    private void OnNodePressed(int nodeId)
    {
        var node = _run.State!.GetNode(nodeId);
        _run.SelectedNodeId = nodeId;

        if (node.IsMatch)
        {
            Nav.Go(this, Nav.Scout);
            return;
        }

        // El entrenamiento y el evento se resuelven solos al entrar (no piden decisiones), así que se
        // enseñan antes de entrar: si no, el jugador vería el nodo pasar sin enterarse de qué le ha dado.
        if (node.Kind is NodeKind.Training or NodeKind.Event)
        {
            Nav.Go(this, Nav.Node);
            return;
        }

        _run.Enter(nodeId);
        Nav.Route(this);
    }

    /// <summary>Paso del recorrido de capturas: el primer nodo de partido accesible, que es lo que un jugador miraría.</summary>
    private void TourPickMatch()
    {
        var nodes = _run.Available();
        foreach (var node in nodes)
        {
            if (node.IsMatch)
            {
                OnNodePressed(node.Id);
                return;
            }
        }

        if (nodes.Count > 0)
        {
            OnNodePressed(nodes[0].Id);
        }
    }
}
