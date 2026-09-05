using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Placement;
using SimPosition = Underleague.Sim.Model.Position;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Equipo</b> (UI-020, UI-021): donde se toman todas las decisiones de plantilla. Se
/// diseña la primera y en detalle porque las demás derivan de sus decisiones; están escritas en
/// <c>docs/ui-equipo.md</c>.
/// <para>
/// La pantalla <b>no calcula nada del juego</b> (RT-014). Zona de acción, vínculos, cobertura y validez
/// de una colocación los resuelve <c>Sim.Placement.PlacementView</c>; aquí solo se decide qué se pinta y
/// se traduce la entrada del jugador en una petición a <c>/Sim</c>.
/// </para>
/// </summary>
public partial class TeamScreen : Control
{
    private const string CoverageAction = "team_coverage";

    private readonly List<PlayerCard> _cards = new();

    private TeamState _state = null!;
    private PitchView _pitch = null!;
    private LegendView _legend = null!;
    private VBoxContainer _roster = null!;
    private Label _subtitle = null!;
    private Label _info = null!;
    private Label _lineupTable = null!;

    private int _selected = -1;
    private int _held = -1;
    private int _rosterIndex;
    private bool _focusRoster;
    private bool _coverage;
    private Cell _pressCell;
    private Cell _cursor = PlacementView.GoalkeeperCell;

    public override void _Ready()
    {
        RegisterCoverageAction();

        _pitch = GetNode<PitchView>("Campo");
        _legend = GetNode<LegendView>("Leyenda");
        _roster = GetNode<VBoxContainer>("Roster");
        _subtitle = GetNode<Label>("Subtitulo");
        _info = GetNode<Label>("Info");
        _lineupTable = GetNode<Label>("Vinculos");

        GetNode<Label>("Titulo").Text = UiText.Get("ui.team.title");
        GetNode<Label>("TituloPlantilla").Text = UiText.Get("ui.team.roster");
        GetNode<Label>("TituloCampo").Text = UiText.Get("ui.team.pitch");
        GetNode<Label>("SubtituloCampo").Text = UiText.Get("ui.team.pitchHint");
        GetNode<Label>("TituloVinculos").Text = UiText.Get("ui.team.lineup");

        // El modo de cobertura también tiene disparador de ratón: los dos flujos de UI-006 son completos,
        // ninguno es un añadido del otro.
        var coverageButton = GetNode<Button>("BotonCobertura");
        coverageButton.Text = UiText.Get("ui.team.coverageButton");
        coverageButton.Pressed += ToggleCoverage;
        GetNode<Label>("AyudaRaton").Text = UiText.Get("ui.input.mouse");
        GetNode<Label>("AyudaMando").Text = UiText.Get("ui.input.pad");

        // Con una run en curso, la plantilla es la suya: esta pantalla es donde se toman todas las
        // decisiones de plantilla (UI-020) y las decisiones son sobre los jugadores de verdad. Sin run
        // —al regenerar las capturas, o al abrir la escena suelta— sigue valiendo la plantilla de pruebas
        // con la que se diseñó, con semilla fija para que enseñe siempre lo mismo.
        var run = RunController.Instance;
        bool inRun = run is { HasRun: true };
        _state = inRun ? TeamState.FromRun(run!) : TeamState.Load(20260904UL);
        _subtitle.Text = UiText.Get(
            "ui.team.subtitle",
            _state.Catalog.Race(_state.Team.Race).Name.Es,
            inRun ? run!.State!.ClubId : UiText.Get("ui.team.placeholderClub"),
            _state.Players.Count);

        if (inRun)
        {
            AddBackButton();
        }

        _pitch.State = _state;
        _pitch.CellPressed += OnCellPressed;
        _pitch.CellReleased += OnCellReleased;
        _pitch.CellHovered += OnCellHovered;

        BuildRoster();
        RefreshCards();
        RefreshPitch();

        if (WantsScreenshots())
        {
            CaptureSequence();
        }
        else if (Tour.Active && inRun)
        {
            // El recorrido pasa por aquí para comprobar lo que más se puede romper al enchufar la run:
            // que esta pantalla, escrita antes que el bucle, enseña la plantilla de la run de verdad.
            Tour.Step(this, "equipo-run", () => Nav.Go(this, Nav.Scout));
        }
    }

    /// <summary>
    /// Vuelve a donde se estaba: al ojeo si se vino a repasar la alineación antes de un partido, y al
    /// mapa si no. La pantalla de Equipo no sabe navegar por su cuenta —no es suya esa decisión—: mira
    /// si hay un nodo elegido, que es el dato que lo dice.
    /// </summary>
    private void AddBackButton()
    {
        var button = new Button
        {
            Text = UiText.Get("ui.nav.back"),
            Position = new Vector2(940f, 56f),
            Size = new Vector2(120f, 26f),
            FocusMode = FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        button.Pressed += () =>
        {
            var run = RunController.Instance;
            Nav.Go(this, run is { SelectedNodeId: >= 0 } ? Nav.Scout : Nav.Map);
        };
        AddChild(button);
    }

    /// <summary>
    /// Segundo flujo de entrada completo (UI-006, RT-071): cruceta para el cursor, botón de acción para
    /// seleccionar y para coger y soltar, cancelar para soltar sin mover, y una sola pulsación para el
    /// modo de cobertura. No hay ninguna acción exclusiva del ratón.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(CoverageAction))
        {
            ToggleCoverage();
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            if (_held >= 0)
            {
                _held = -1;
                RefreshPitch();
            }
            else
            {
                Select(-1);
            }

            return;
        }

        if (@event.IsActionPressed("ui_accept"))
        {
            Confirm();
            return;
        }

        int dx = @event.IsActionPressed("ui_right") ? 1 : @event.IsActionPressed("ui_left") ? -1 : 0;
        int dy = @event.IsActionPressed("ui_down") ? 1 : @event.IsActionPressed("ui_up") ? -1 : 0;
        if (dx != 0 || dy != 0)
        {
            MoveFocus(dx, dy);
        }
    }

    /// <summary>Un único anillo de foco: la lista a la izquierda y la cuadrícula a la derecha, sin botón extra.</summary>
    private void MoveFocus(int dx, int dy)
    {
        if (_focusRoster)
        {
            if (dx > 0)
            {
                _focusRoster = false;
                RefreshPitch();
                return;
            }

            if (dy != 0 && _cards.Count > 0)
            {
                _rosterIndex = Math.Clamp(_rosterIndex + dy, 0, _cards.Count - 1);
                Select(_cards[_rosterIndex].PlayerId);
            }

            return;
        }

        if (dx < 0 && _cursor.Column == 0 && _held < 0)
        {
            _focusRoster = true;
            RefreshPitch();
            return;
        }

        _cursor = new Cell(
            Math.Clamp(_cursor.Column + dx, 0, Pitch.Columns - 1),
            Math.Clamp(_cursor.Row + dy, 0, Pitch.Rows - 1));
        RefreshPitch();
    }

    /// <summary>
    /// Botón de acción sobre la cuadrícula: si no hay nadie cogido, selecciona al jugador de la casilla y
    /// lo levanta; si lo hay, lo suelta. Es el mismo gesto que el clic (UI-001).
    /// </summary>
    private void Confirm()
    {
        if (_focusRoster)
        {
            if (_rosterIndex < _cards.Count)
            {
                Toggle(_cards[_rosterIndex].PlayerId);
            }

            return;
        }

        if (_held >= 0)
        {
            Drop(_cursor);
            return;
        }

        var player = _state.At(_cursor);
        if (player is not null)
        {
            Select(player.Id);
            _held = player.Id;
            RefreshPitch();
        }
    }

    private void OnCellPressed(int column, int row)
    {
        _focusRoster = false;
        _pressCell = new Cell(column, row);
        _cursor = _pressCell;

        if (_held >= 0)
        {
            Drop(_cursor);
            return;
        }

        var player = _state.At(_cursor);
        if (player is null)
        {
            RefreshPitch();
            return;
        }

        Select(player.Id);
        _held = player.Id;
        RefreshPitch();
    }

    private void OnCellReleased(int column, int row)
    {
        var cell = new Cell(column, row);

        // Soltar donde se pulsó no es un arrastre: el jugador queda cogido y se suelta con el siguiente
        // clic. Así el mismo ratón sirve para arrastrar y soltar y para pulsar dos veces, y el mando hace
        // exactamente lo mismo con el botón de acción.
        if (_held >= 0 && cell != _pressCell)
        {
            _cursor = cell;
            Drop(cell);
        }
    }

    private void OnCellHovered(int column, int row)
    {
        var cell = new Cell(column, row);
        if (cell.Column < 0 || cell.Column >= Pitch.Columns || cell.Row < 0 || cell.Row >= Pitch.Rows)
        {
            return;
        }

        _focusRoster = false;
        _cursor = cell;
        RefreshPitch();
    }

    /// <summary>Suelta al jugador cogido. La colocación la resuelve <c>/Sim</c>; aquí solo se pide.</summary>
    private void Drop(Cell target)
    {
        int player = _held;
        _held = -1;
        if (player >= 0 && _state.Move(player, target))
        {
            Flash(player);
            RefreshCards();
        }

        RefreshPitch();
    }

    /// <summary>
    /// Un solo patrón de inspección (UI-001): activar a un jugador expande su ficha —solo una a la vez,
    /// UI-012— y pinta su zona de acción en el campo. Da igual que la activación venga de un clic en la
    /// ficha, de un clic en la casilla o del botón de acción del mando.
    /// </summary>
    private void Select(int playerId)
    {
        _selected = playerId;
        if (_selected >= 0 && _state.CellOf(_selected) is { } cell)
        {
            _cursor = cell;
        }

        ApplyCardFlags();
        RefreshPitch();
    }

    /// <summary>Activar al ya seleccionado lo colapsa: el mismo gesto abre y cierra la ficha.</summary>
    private void Toggle(int playerId) => Select(_selected == playerId ? -1 : playerId);

    private void ApplyCardFlags()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            bool current = _cards[i].PlayerId == _selected;
            _cards[i].Expanded = current;
            _cards[i].Selected = current;
            if (current)
            {
                _rosterIndex = i;
            }
        }
    }

    private void ToggleCoverage()
    {
        _coverage = !_coverage;
        RefreshPitch();
    }

    private void Flash(int playerId)
    {
        foreach (var card in _cards)
        {
            if (card.PlayerId == playerId)
            {
                card.Flash();
            }
        }
    }

    /// <summary>
    /// Construye la lista: una ficha por jugador, titulares primero en orden de columna y fila —de la
    /// portería al ataque, como se lee el campo— y después los suplentes. Las fichas son instancias de la
    /// misma escena que usarán Alineación, Partido y Mercado (UI-010).
    /// </summary>
    private void BuildRoster()
    {
        var scene = GD.Load<PackedScene>("res://Scenes/PlayerCard.tscn");
        int starters = _state.Lineup.Slots.Count;

        AddSectionLabel(UiText.Get("ui.team.starters"));
        for (int i = 0; i < starters; i++)
        {
            AddCard(scene);
        }

        AddSectionLabel(UiText.Get("ui.team.bench"));
        for (int i = starters; i < _state.Players.Count; i++)
        {
            AddCard(scene);
        }
    }

    private void AddSectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        label.AddThemeColorOverride("font_color", Style.Accent);
        _roster.AddChild(label);
    }

    private void AddCard(PackedScene scene)
    {
        var card = scene.Instantiate<PlayerCard>();
        _roster.AddChild(card);
        card.Activated += OnCardActivated;
        _cards.Add(card);
    }

    private void OnCardActivated(int playerId)
    {
        _focusRoster = true;
        Toggle(playerId);
    }

    /// <summary>Rellena las fichas. Se llama al cambiar la alineación, no al mover el cursor.</summary>
    private void RefreshCards()
    {
        var links = PlacementView.Links(_state.Lineup);
        int index = 0;
        var ordered = OrderedRoster();
        foreach (var card in _cards)
        {
            if (index >= ordered.Count)
            {
                break;
            }

            var player = _state.Find(ordered[index]);
            index++;
            if (player is not null)
            {
                card.Bind(_state, player, LinksOf(player.Id, links));
            }
        }

        ApplyCardFlags();
    }

    private List<int> OrderedRoster()
    {
        var starters = new List<(Cell Cell, int Id)>();
        foreach (var slot in _state.Lineup.Slots)
        {
            starters.Add((slot.HomeCell, slot.PlayerId));
        }

        starters.Sort(static (a, b) => a.Cell.Column != b.Cell.Column
            ? a.Cell.Column.CompareTo(b.Cell.Column)
            : a.Cell.Row.CompareTo(b.Cell.Row));

        var ordered = new List<int>();
        foreach (var (_, id) in starters)
        {
            ordered.Add(id);
        }

        foreach (var player in _state.Players)
        {
            if (!_state.IsStarter(player.Id))
            {
                ordered.Add(player.Id);
            }
        }

        return ordered;
    }

    private List<string> LinksOf(int playerId, IReadOnlyList<PlacementLink> links)
    {
        var result = new List<string>();
        foreach (var link in links)
        {
            if (link.FromPlayerId != playerId)
            {
                continue;
            }

            var other = _state.Find(link.ToPlayerId);
            if (other is not null)
            {
                result.Add(UiText.Get("ui.team.linkOf", RelationName(link.Relation), other.Name));
            }
        }

        return result;
    }

    /// <summary>Nombre de la relación, del mismo fichero de localización que usan las descripciones (RT-073).</summary>
    private string RelationName(LinkRelation relation)
    {
        string name = relation.ToString();
        string key = char.ToLowerInvariant(name[0]) + name[1..];
        return _state.Templates.Get("links", key);
    }

    /// <summary>Nombre corto de la relación, para las listas donde la frase larga no cabe.</summary>
    private static string ShortRelation(LinkRelation relation) => UiText.Get("ui.link." + relation);

    /// <summary>Recalcula lo que se pinta sobre el campo. Todo sale de <c>/Sim</c>; aquí no hay reglas.</summary>
    private void RefreshPitch()
    {
        var lineup = _held >= 0 ? _state.Preview(_held, _cursor) : _state.Lineup;
        var links = PlacementView.Links(lineup);

        _pitch.Preview = lineup;
        _pitch.Links = links;
        _pitch.Cursor = _cursor;
        _pitch.SelectedId = _selected;
        _pitch.HeldId = _held;
        _pitch.CoverageMode = _coverage;
        _pitch.Coverage = _coverage ? PlacementView.Coverage(_state.Players, lineup, _state.Catalog) : null;

        var created = new List<PlacementLink>();
        var broken = new List<PlacementLink>();
        if (_held >= 0)
        {
            var current = PlacementView.Links(_state.Lineup);
            Difference(links, current, created);
            Difference(current, links, broken);
            var moved = _state.Find(_held);
            _pitch.CursorValid = moved is not null && PlacementView.CanPlace(moved.Position, _cursor);
        }
        else
        {
            _pitch.CursorValid = true;
        }

        _pitch.Created = created;
        _pitch.Broken = broken;

        int shown = _held >= 0 ? _held : _selected;
        _pitch.Zone = null;
        if (!_coverage && shown >= 0 && _state.Find(shown) is { } player)
        {
            foreach (var slot in lineup.Slots)
            {
                if (slot.PlayerId == shown)
                {
                    _pitch.Zone = PlacementView.ZoneOf(player, slot.HomeCell, _state.Catalog);
                }
            }
        }

        _legend.CoverageMode = _coverage;
        _legend.Moving = _held >= 0;
        _legend.QueueRedraw();
        _pitch.QueueRedraw();
        UpdateInfo(links, created, broken);
    }

    private static void Difference(IReadOnlyList<PlacementLink> from, IReadOnlyList<PlacementLink> other, List<PlacementLink> into)
    {
        foreach (var link in from)
        {
            bool found = false;
            foreach (var candidate in other)
            {
                if (candidate.FromPlayerId == link.FromPlayerId && candidate.ToPlayerId == link.ToPlayerId && candidate.Relation == link.Relation)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                into.Add(link);
            }
        }
    }

    private void UpdateInfo(IReadOnlyList<PlacementLink> links, IReadOnlyList<PlacementLink> created, IReadOnlyList<PlacementLink> broken)
    {
        var lines = new List<string>();

        if (_coverage)
        {
            lines.Add(UiText.Get("ui.team.coverage"));
            lines.Add(UiText.Get("ui.team.coverageHint", _pitch.Coverage?.Holes ?? 0));
            lines.Add(string.Empty);
        }

        int shown = _held >= 0 ? _held : _selected;
        var player = shown >= 0 ? _state.Find(shown) : null;

        if (player is null)
        {
            lines.Add(UiText.Get("ui.team.nobody"));
        }
        else if (_held >= 0)
        {
            lines.Add(UiText.Get("ui.team.moving", player.Name));
            lines.Add(UiText.Get("ui.team.dropHint"));
            lines.Add(string.Empty);
            int others = Changes(lines, created, "ui.team.created", player.Id) + Changes(lines, broken, "ui.team.broken", player.Id);
            if (others > 0)
            {
                lines.Add(UiText.Get("ui.team.moreChanges", others));
            }
        }
        else
        {
            lines.Add(UiText.Get("ui.team.selected") + ": " + player.Name);
            lines.Add(UiText.Get("ui.team.links"));
            var own = LinksOf(player.Id, links);
            if (own.Count == 0)
            {
                lines.Add(UiText.Get("ui.team.linksNone"));
            }
            else
            {
                lines.AddRange(own);
            }
        }

        _info.Text = string.Join("\n", lines);
        _lineupTable.Text = string.Join("\n", LineupTable(links));
    }

    /// <summary>
    /// Cambios de vínculo que se enseñan al mover: los del <b>jugador manipulado</b>, agrupados por
    /// compañero, que son los que ha provocado a propósito. Los recíprocos y los de sus compañeros —que
    /// también cambian— se cuentan y se resumen en una línea, para que la lista no tape la pantalla.
    /// </summary>
    private int Changes(List<string> lines, IReadOnlyList<PlacementLink> links, string key, int playerId)
    {
        var order = new List<int>();
        var grouped = new Dictionary<int, List<string>>();
        int others = 0;

        foreach (var link in links)
        {
            if (link.FromPlayerId != playerId)
            {
                others++;
                continue;
            }

            if (!grouped.TryGetValue(link.ToPlayerId, out var relations))
            {
                relations = new List<string>();
                grouped[link.ToPlayerId] = relations;
                order.Add(link.ToPlayerId);
            }

            relations.Add(ShortRelation(link.Relation));
        }

        foreach (int other in order)
        {
            lines.Add(UiText.Get(key, string.Join(", ", grouped[other]), _state.Find(other)?.Name ?? "?"));
        }

        return others;
    }

    /// <summary>
    /// Lectura en texto de la cuadrícula: quién ocupa qué casilla y cuántos vínculos le salen. Es la
    /// misma información que dibuja el campo, para quien prefiera leerla, y el sitio natural para el
    /// resto de columnas de plantilla que lleguen en fase 2 (salario, objeto, riesgo de lesión).
    /// </summary>
    private List<string> LineupTable(IReadOnlyList<PlacementLink> links)
    {
        var rows = new List<(Cell Cell, string Text)>();
        foreach (var slot in _pitch.Preview?.Slots ?? _state.Lineup.Slots)
        {
            var player = _state.Find(slot.PlayerId);
            if (player is null)
            {
                continue;
            }

            int count = 0;
            foreach (var link in links)
            {
                if (link.FromPlayerId == player.Id)
                {
                    count++;
                }
            }

            rows.Add((slot.HomeCell, UiText.Get(
                "ui.team.lineupRow",
                player.Name,
                UiText.Get("ui.pos." + player.Position),
                slot.HomeCell.Column,
                slot.HomeCell.Row,
                count,
                UiText.Get(count == 1 ? "ui.team.linkOne" : "ui.team.linkMany"))));
        }

        rows.Sort(static (a, b) => a.Cell.Column != b.Cell.Column
            ? a.Cell.Column.CompareTo(b.Cell.Column)
            : a.Cell.Row.CompareTo(b.Cell.Row));

        var lines = new List<string>();
        foreach (var (_, text) in rows)
        {
            lines.Add(text);
        }

        return lines;
    }

    private string Describe(PlacementLink link)
    {
        var from = _state.Find(link.FromPlayerId);
        var to = _state.Find(link.ToPlayerId);
        return (from?.Name ?? "?") + " -> " + (to?.Name ?? "?");
    }

    /// <summary>El modo de cobertura se declara en código para no depender del formato binario del InputMap.</summary>
    private static void RegisterCoverageAction()
    {
        if (InputMap.HasAction(CoverageAction))
        {
            return;
        }

        InputMap.AddAction(CoverageAction);
        InputMap.ActionAddEvent(CoverageAction, new InputEventKey { PhysicalKeycode = Key.C });
        InputMap.ActionAddEvent(CoverageAction, new InputEventJoypadButton { ButtonIndex = JoyButton.X });
    }

    private static bool WantsScreenshots()
    {
        foreach (string argument in OS.GetCmdlineArgs())
        {
            if (argument == "--screenshots")
            {
                return true;
            }
        }

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--screenshots")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Deja en <c>Game/screenshots/</c> las cuatro capturas que cuentan la pantalla. Es la única forma de
    /// que el revisor la juzgue sin abrir el editor; el comando está en <c>docs/ui-equipo.md</c>.
    /// </summary>
    private async void CaptureSequence()
    {
        // Las capturas 2, 3 y 4 se producen **con el flujo de mando** (eventos de acción sintéticos), no
        // llamando a los métodos por dentro: así la secuencia comprueba de paso que la navegación sin
        // ratón lleva a los mismos estados (UI-006, RT-071).
        var steps = new (string Name, Action Setup)[]
        {
            ("equipo", () => { }),
            ("equipo-zona", () =>
            {
                Pad("ui_right");
                Pad("ui_right");
                Pad("ui_right");
                Pad("ui_accept");
                Pad("ui_right");
                Pad("ui_right");
                Pad("ui_up");
            }),
            ("equipo-cobertura", () =>
            {
                Pad("ui_cancel");
                Pad(CoverageAction);
            }),
            ("equipo-ficha", () =>
            {
                Pad(CoverageAction);
                _focusRoster = true;
                _rosterIndex = IndexOfCard(FindRare());
                Pad("ui_accept");
            }),
        };

        string directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(directory);

        foreach (var (name, setup) in steps)
        {
            setup();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, "frame_post_draw");
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(Path.Combine(directory, name + ".png"));
            GD.Print($"captura: {name}.png");
        }

        GetTree().Quit();
    }

    /// <summary>Inyecta una acción como si viniera del mando, por el mismo camino que la entrada real.</summary>
    private void Pad(string action) => _UnhandledInput(new InputEventAction { Action = action, Pressed = true });

    private int IndexOfCard(int playerId)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].PlayerId == playerId)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Primer titular con perk asignado: la captura de la ficha tiene que enseñar uno de verdad.</summary>
    private int FindRare()
    {
        foreach (var slot in _state.Lineup.Slots)
        {
            var player = _state.Find(slot.PlayerId);
            if (player is not null && player.Perks.Count > 0)
            {
                return player.Id;
            }
        }

        return -1;
    }

    private int FindByPosition(SimPosition position)
    {
        foreach (var slot in _state.Lineup.Slots)
        {
            var player = _state.Find(slot.PlayerId);
            if (player is not null && player.Position == position)
            {
                return player.Id;
            }
        }

        return -1;
    }
}
