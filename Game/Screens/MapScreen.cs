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
    private Newspaper _newspaper = null!;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Go(this, Nav.Start);
            return;
        }

        _run = run;
        Layout.CenterLegacy(this);

        // Mesa de madera con vetas y clavos (encargo mapa-pregon), en vez del color plano de
        // Widgets.Background: es la estructura persistente sobre la que se clava el pergamino del mapa.
        var table = new WoodTable { Size = Layout.LegacySize };
        AddChild(table);

        _subtitle = Widgets.Header(this, UiText.Get("ui.map.title"), string.Empty);

        BuildAvailableCounter();
        BuildChoicesPanel();
        BuildGraphPanel();
        BuildLegendPanel();
        BuildStatePanel();
        BuildNewspaperPanel();
        BuildButtons();

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseOnly"), UiText.Get("ui.input.padPending"));

        Refresh();

        if (Tour.Maps)
        {
            // Recorrido corto: el mapa de los tres actos y fuera. Con cuatro carriles (ADR 0053) el
            // dibujo cambia bastante de un acto a otro -11 capas en el primero y 12 en los otros dos-,
            // así que juzgar la legibilidad con uno solo no vale.
            var state = _run.State!;
            if (state.CurrentNodeId >= 0)
            {
                // Cuarta captura: a media travesía, que es donde se ve lo que el mapa de cuatro carriles
                // añade -lo que ya no se puede alcanzar desde el carril en el que estás se apaga-.
                Tour.Step(this, "mapa-mitad", null);
                return;
            }

            int act = state.Act;
            Tour.Step(this, "mapa-acto" + act, act < RunRules.Acts ? () => NextAct(act + 1) : MidAct);
            return;
        }

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

    /// <summary>
    /// La lista de destinos, arriba de la columna izquierda: se estrecha en alto respecto a la primera
    /// versión de este encargo -690 a 326 px- porque casi nunca hay más de un puñado de nodos accesibles
    /// a la vez, y el resto de esa columna es donde vive ahora <see cref="BuildLegendPanel"/> (revisión de
    /// reparto del revisor, 20 sep 2026: la columna se veía casi vacía con dos líneas de texto en 690 px).
    /// </summary>
    private void BuildChoicesPanel()
    {
        Widgets.Panel(this, new Rect2(12f, 52f, Widgets.CardColumnWidth, 326f));
        Widgets.Section(this, UiText.Get("ui.map.choose"), new Vector2(24f, 60f), 340f);

        _choices = new VBoxContainer
        {
            Position = new Vector2(22f, 82f),
            Size = new Vector2(356f, 286f),
        };
        _choices.AddThemeConstantOverride("separation", 6);
        AddChild(_choices);
    }

    /// <summary>
    /// El grafo del acto, en su pergamino con la esquina doblada (encargo mapa-pregon: "el mapa vive
    /// sobre un pergamino con las esquinas dobladas"). Con la leyenda ya no a su lado sino en la columna
    /// izquierda (<see cref="BuildLegendPanel"/>), gana todo el ancho de la parte derecha -872 px- y algo
    /// de alto -330 a 360-, así que <see cref="MapView.NodeRadius"/> puede crecer y el grafo respira.
    /// </summary>
    private void BuildGraphPanel()
    {
        var area = new Rect2(396f, 96f, 872f, 360f);
        Widgets.Panel(this, area);

        var fold = new FoldedCorner
        {
            Position = new Vector2(area.Position.X + area.Size.X - 26f, area.Position.Y),
            Size = new Vector2(26f, 26f),
        };
        AddChild(fold);

        _view = new MapView
        {
            Position = new Vector2(area.Position.X + 14f, area.Position.Y + 14f),
            Size = new Vector2(area.Size.X - 28f, area.Size.Y - 28f),
        };
        _view.NodePressed += OnNodePressed;
        AddChild(_view);
    }

    /// <summary>
    /// La leyenda, debajo de la lista de destinos y con su mismo ancho completo (revisión de reparto del
    /// revisor, 20 sep 2026: antes vivía en una columna de 140 px junto al grafo, que partía cada
    /// descripción en tres líneas y se desbordaba sobre la madera). Un <see cref="KindGlyph"/> y una línea
    /// -"nombre — qué es"- por cada uno de los ocho <see cref="Underleague.Sim.Run.NodeKind"/>.
    /// </summary>
    private void BuildLegendPanel()
    {
        var area = new Rect2(12f, 386f, Widgets.CardColumnWidth, 356f);
        Widgets.Panel(this, area);

        var legend = new MapLegend
        {
            Position = area.Position + new Vector2(10f, 8f),
            Size = area.Size - new Vector2(20f, 16f),
        };
        AddChild(legend);
    }

    /// <summary>
    /// Cómo va la run: la misma cuenta que enseñará la pantalla de fin de run (<c>Sim.Run.RunSummary</c>).
    /// Verla por el camino es lo que convierte el mapa en un sitio donde se decide y no solo se avanza.
    /// Se estrecha respecto a la primera versión -300 a 260 px- para dejarle más ancho al periódico: su
    /// contenido son tres líneas cortas, nunca necesitó los 300 px que tenía.
    /// </summary>
    private void BuildStatePanel()
    {
        Widgets.Panel(this, new Rect2(396f, 464f, 260f, 242f));
        Widgets.Section(this, UiText.Get("ui.map.state"), new Vector2(412f, 474f), 230f);
        _state = Widgets.Body(this, string.Empty, new Vector2(412f, 496f), 230f);
    }

    /// <summary>
    /// El periódico deportivo de humor negro, apoyado abajo a la derecha (encargo mapa-pregon, RA-025):
    /// solo texto, con breves que salen del estado real de la run (<see cref="Newspaper.Configure"/>).
    /// Gana todo el ancho que deja <see cref="BuildStatePanel"/> -404 a 596 px-, para que sus columnas de
    /// breves respiren en vez de tener que acortar cada frase para que quepa.
    /// </summary>
    private void BuildNewspaperPanel()
    {
        _newspaper = new Newspaper
        {
            Position = new Vector2(672f, 464f),
            Size = new Vector2(596f, 242f),
        };
        AddChild(_newspaper);
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

        // El periódico reacciona al mismo estado, con su propia lectura determinista (Newspaper.Pick):
        // mismo estado, mismo periódico (encargo mapa-pregon).
        _newspaper.Configure(state);

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

    /// <summary>Paso del recorrido corto: saltar a la entrada del acto siguiente y volver al mapa.</summary>
    private void NextAct(int act)
    {
        _run.JumpToAct(act);
        Nav.Go(this, Nav.Map);
    }

    /// <summary>
    /// Último paso del recorrido corto: plantarse a media travesía, en el carril de abajo de una capa
    /// central, que es la situación que enseña la regla de carriles contiguos —desde ahí la parte alta
    /// del acto ya no se alcanza—.
    /// </summary>
    private void MidAct()
    {
        var map = _run.State!.CurrentMap;
        int target = MapInvariants.PathLength(map) / 2;
        MapNode? pick = null;
        foreach (var node in map.Nodes)
        {
            if (node.Layer == target && (pick is null || node.IndexInLayer > pick.IndexInLayer))
            {
                pick = node;
            }
        }

        if (pick is null)
        {
            return;
        }

        _run.JumpToNode(pick.Id);
        Nav.Go(this, Nav.Map);
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
