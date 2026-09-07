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
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Items;
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
    private const string ZonesAction = "team_zones";

    private readonly List<PlayerCard> _cards = new();

    private TeamState _state = null!;
    private PitchView _pitch = null!;
    private LegendView _legend = null!;
    private VBoxContainer _roster = null!;
    private Label _subtitle = null!;
    private Label _info = null!;
    private Label _lineupTable = null!;
    private Label _riskTitle = null!;
    private Label _risk = null!;
    private Toast _toast = null!;

    private int _selected = -1;
    private int _held = -1;
    private int _rosterIndex;
    private bool _focusRoster;
    private bool _coverage;
    private bool _zones;
    private Cell _pressCell;
    private Cell _cursor = PlacementView.GoalkeeperCell;

    public override void _Ready()
    {
        RegisterActions();

        _pitch = GetNode<PitchView>("Campo");
        _legend = GetNode<LegendView>("Leyenda");
        _roster = GetNode<VBoxContainer>("Roster");
        _subtitle = GetNode<Label>("Subtitulo");
        _info = GetNode<Label>("Info");
        _lineupTable = GetNode<Label>("Vinculos");
        _riskTitle = GetNode<Label>("TituloRiesgo");
        _risk = GetNode<Label>("Riesgo");

        GetNode<Label>("Titulo").Text = UiText.Get("ui.team.title");
        GetNode<Label>("TituloPlantilla").Text = UiText.Get("ui.team.roster");
        GetNode<Label>("TituloCampo").Text = UiText.Get("ui.team.pitch");
        GetNode<Label>("SubtituloCampo").Text = UiText.Get("ui.team.pitchHint");
        GetNode<Label>("TituloVinculos").Text = UiText.Get("ui.team.lineup");
        _riskTitle.Text = UiText.Get("ui.scout.risk");

        // El modo de cobertura también tiene disparador de ratón: los dos flujos de UI-006 son completos,
        // ninguno es un añadido del otro.
        var coverageButton = GetNode<Button>("BotonCobertura");
        coverageButton.Text = UiText.Get("ui.team.coverageButton");
        coverageButton.Pressed += ToggleCoverage;

        // El botón de zonas es el "qué significan estas palabras" de los perks de colocación: sin él, los
        // textos "empieza en su tercio adelantado" o "en una fila de su izquierda" describen una
        // cuadrícula que el jugador no ve. Va al lado del de cobertura porque los dos son lecturas del
        // mismo campo, y son excluyentes.
        var zonesButton = GetNode<Button>("BotonZonas");
        zonesButton.Text = UiText.Get("ui.team.zonesButton");
        zonesButton.Pressed += ToggleZones;

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
            inRun ? _state.Team.Name : UiText.Get("ui.team.placeholderClub"),
            _state.Players.Count);

        if (inRun)
        {
            AddBackButton();
        }

        // El aviso vive por encima de todo y no recibe ratón: se apoya en el borde inferior del panel del
        // campo, justo encima de la ayuda de mandos, para que el ojo no tenga que salir de la cuadrícula.
        _toast = new Toast { BottomLeft = new Vector2(410f, 726f), MaximumWidth = 846f };
        AddChild(_toast);

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
    /// Vuelve a donde se estaba. Si Mercado, Recompensa o Informe dejaron dicho un desvío en
    /// <see cref="Nav.ReturnTo"/> (AW-N, botón "Ver equipo" de esas pantallas), se vuelve ahí y se
    /// limpia el desvío para no arrastrarlo a la próxima vez que se entre a Equipo. Si no hay desvío, la
    /// pantalla de Equipo no sabe navegar por su cuenta —no es suya esa decisión—: mira si hay un nodo
    /// elegido, que es el dato que lo dice, y va al ojeo si se vino a repasar la alineación antes de un
    /// partido, o al mapa si no.
    /// </summary>
    private void AddBackButton()
    {
        // AW-P (docs/pendientes.md): x=940 pisaba "BotonZonas" (900-1068, Equipo.tscn), visible en
        // equipo-run.png con una run activa (--tour). Reubicado junto al título superior (Titulo/
        // Subtitulo ocupan hasta x=900 en y=8-34), lejos de la fila de BotonZonas/BotonCobertura
        // (900-1256, y=56-82) y del borde derecho del panel de campo (1268).
        var button = new Button
        {
            Text = UiText.Get("ui.nav.back"),
            Position = new Vector2(1140f, 8f),
            Size = new Vector2(120f, 26f),
            FocusMode = FocusModeEnum.None,
        };
        button.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        button.Pressed += () =>
        {
            if (!string.IsNullOrEmpty(Nav.ReturnTo))
            {
                string returnTo = Nav.ReturnTo;
                Nav.ReturnTo = string.Empty;
                Nav.Go(this, returnTo);
                return;
            }

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

        if (@event.IsActionPressed(ZonesAction))
        {
            ToggleZones();
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
                ActivateRosterCard(_cards[_rosterIndex].PlayerId);
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
        if (player >= 0)
        {
            // El "antes" se toma con la alineación todavía sin tocar: el aviso compara dos fotos, no
            // recalcula nada (RT-014). Las dos las hace Sim.Perks.LineupPerkPreviewer.
            var before = LineupPerkPreviewer.Preview(_state.Lineup, _state.Players, _state.Catalog);
            if (_state.Move(player, target))
            {
                Announce(player, before, LineupPerkPreviewer.Preview(_state.Lineup, _state.Players, _state.Catalog));
                Flash(player);
                RefreshCards();
            }
        }

        RefreshPitch();
    }

    /// <summary>
    /// Aviso de lo que la casilla acaba de encender o apagar (RF-012d). Se dicen <b>todos</b> los perks
    /// decidibles del jugador movido —también los que siguen apagados, porque saber que ahí no se activa
    /// es la mitad de la decisión— y solo los <b>cambios</b> de sus compañeros: los de los demás no los
    /// ha tocado a propósito y listarlos enteros taparía el campo. Si no hay nada que decir, no hay aviso.
    /// </summary>
    private void Announce(
        int playerId, IReadOnlyList<LineupPerkPreview> before, IReadOnlyList<LineupPerkPreview> after)
    {
        var lines = new List<ToastLine>();
        foreach (var entry in after)
        {
            string? name = PerkName(entry.PerkId);
            if (name is null)
            {
                continue;
            }

            bool active = entry.Status == LineupPerkStatus.Active;
            if (entry.PlayerId == playerId)
            {
                lines.Add(new ToastLine(
                    UiText.Get(active ? "ui.team.perkOn" : "ui.team.perkOff", name),
                    active ? Style.LinkCreated : Style.LinkBroken));
                continue;
            }

            var previous = FindPreview(before, entry.PlayerId, entry.PerkId);
            if (previous is not null && previous.Status == entry.Status)
            {
                continue;
            }

            lines.Add(new ToastLine(
                UiText.Get(
                    active ? "ui.team.perkOnOther" : "ui.team.perkOffOther",
                    name,
                    _state.Find(entry.PlayerId)?.Name ?? "?"),
                active ? Style.LinkCreated : Style.LinkBroken));
        }

        _toast.Post(lines);
    }

    private static LineupPerkPreview? FindPreview(IReadOnlyList<LineupPerkPreview> preview, int playerId, string perkId)
    {
        foreach (var entry in preview)
        {
            if (entry.PlayerId == playerId && string.Equals(entry.PerkId, perkId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Nombre localizado del perk, del mismo catálogo del que sale su descripción (RT-073).</summary>
    private string? PerkName(string perkId) => _state.Catalog.Perks.Find(perkId)?.Name.Es;

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

    /// <summary>
    /// Activar una ficha de la lista (AW-L): el mismo gesto de siempre —clic en la ficha o botón de
    /// acción con el foco en la plantilla— pero un suplente tiene, además, una tercera opción: cogerlo
    /// para soltarlo en el campo, exactamente como ya se coge a un titular desde una casilla
    /// (<see cref="OnCellPressed"/>). Coger tiene prioridad sobre expandir, así que un suplente libre no
    /// se expande al primer toque: se expande solo si ya estaba cogido y se vuelve a tocar sin soltar
    /// antes en una casilla, que es el gesto de cancelar.
    /// </summary>
    private void ActivateRosterCard(int playerId)
    {
        if (!_state.IsStarter(playerId) && _held < 0)
        {
            _held = playerId;
            Select(playerId);
            RefreshPitch();
            return;
        }

        if (_held == playerId)
        {
            _held = -1;
            RefreshPitch();
            return;
        }

        Toggle(playerId);
    }

    private void ApplyCardFlags()
    {
        // Colapsar una ficha esconde sus etiquetas de zona sin que llegue a emitirse el "he dejado de
        // mirar" del hover, así que el resaltado se apaga aquí: cambiar de ficha seleccionada nunca puede
        // dejar teñida una franja que ya no está a la vista de nadie.
        OnCardZoneHint(string.Empty, string.Empty);

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

    /// <summary>Los dos modos de campo son excluyentes: superpuestos no se entiende ninguno (§6).</summary>
    private void ToggleCoverage()
    {
        _coverage = !_coverage;
        if (_coverage)
        {
            _zones = false;
        }

        RefreshPitch();
    }

    private void ToggleZones()
    {
        _zones = !_zones;
        if (_zones)
        {
            _coverage = false;
        }

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
        card.ZoneHint += OnCardZoneHint;
        _cards.Add(card);
    }

    private void OnCardActivated(int playerId)
    {
        _focusRoster = true;
        ActivateRosterCard(playerId);
    }

    /// <summary>
    /// El ratón descansa sobre el nombre de un tercio o de una banda dentro de la descripción de un perk
    /// (AW-F): además del tooltip que pinta el propio <c>RichTextLabel</c>, se tiñe <b>esa</b> franja
    /// sobre la cuadrícula, que es la respuesta a "¿dónde está eso?" sin salir de la ficha. Da igual de
    /// quién sea la ficha —titular o suplente—: la zona la nombra el texto, no su portador. Con las dos
    /// cadenas vacías se apaga.
    /// </summary>
    private void OnCardZoneHint(string kind, string value)
    {
        _pitch.HighlightedZoneKey = kind == ZoneHintText.ZoneKind ? value : null;
        _pitch.HighlightedFlankKey = kind == ZoneHintText.FlankKind ? value : null;
        _pitch.QueueRedraw();
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
        _pitch.ZonesMode = _zones;
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

        RefreshRisk(lineup);

        int shown = _held >= 0 ? _held : _selected;
        _pitch.Zone = null;
        if (!_coverage && !_zones && shown >= 0 && _state.Find(shown) is { } player)
        {
            foreach (var slot in lineup.Slots)
            {
                if (slot.PlayerId == shown)
                {
                    _pitch.Zone = PlacementView.ZoneOf(player, slot.HomeCell, _state.Catalog);
                }
            }
        }

        // En el modo de zonas la leyenda estorba: sus muestras hablan de la zona de acción y del margen,
        // que en ese modo no se pintan. El campo se rotula a sí mismo.
        _legend.Visible = !_zones;
        _legend.CoverageMode = _coverage;
        _legend.Moving = _held >= 0;
        _legend.QueueRedraw();
        _pitch.QueueRedraw();
        UpdateInfo(links, created, broken);
    }

    /// <summary>
    /// AW-G: el mismo riesgo de muerte por titular que enseña el ojeo (RF-012c,
    /// <c>ScoutScreen.BuildReport</c>), aquí en Equipo y sobre la alineación que se está mirando en ese
    /// instante —la que arrastra un jugador cogido incluida, no solo la ya guardada—, porque aquí es
    /// donde RF-012c pide que se pueda "reducir el riesgo con la alineación" (ADR 0048): sin recalcular
    /// al mover una ficha, el jugador solo vería el número viejo.
    /// <para>
    /// Solo tiene sentido con una run en curso y un nodo de partido ya elegido (se viene a repasar la
    /// alineación antes de <b>ese</b> rival); sin nodo no hay rival del que salga el riesgo y el bloque
    /// se oculta entero, en vez de enseñar un "sin riesgo" que no sería cierto —no es que no haya riesgo,
    /// es que todavía no hay partido que jugar.
    /// </para>
    /// </summary>
    private void RefreshRisk(Lineup lineup)
    {
        var run = RunController.Instance;
        if (run is not { HasRun: true } || run.SelectedNodeId < 0)
        {
            _riskTitle.Visible = false;
            _risk.Visible = false;
            return;
        }

        _riskTitle.Visible = true;
        _risk.Visible = true;

        var risks = RunEngine.LethalRisks(run.State!, run.SelectedNodeId, _state.Catalog, run.Engine, lineup);
        var lines = new List<string>();
        foreach (var risk in risks)
        {
            if (risk.Risk <= 0)
            {
                continue;
            }

            var player = _state.Find(risk.PlayerId);
            lines.Add(UiText.Get("ui.scout.riskLine", player?.Name ?? "?", Percent(risk.Risk)));
        }

        _risk.Text = lines.Count > 0 ? string.Join("\n", lines) : UiText.Get("ui.scout.riskNone");
        _risk.AddThemeColorOverride("font_color", lines.Count > 0 ? Style.Text : Style.TextDim);
    }

    /// <summary>Probabilidad en base 10.000 escrita como porcentaje con un decimal (RF-012c), igual que <c>ScoutScreen.Percent</c>.</summary>
    private static string Percent(int risk) => UiText.Get("ui.risk.percent", risk / 100, (risk % 100) / 10);

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

        // Mientras el modo de zonas está encendido, el texto de al lado explica lo que el campo dibuja:
        // qué mira un perk de inicio (la casilla de alineación, no dónde acabe el jugador), qué cuenta
        // como banda y qué es un vínculo (RF-044). Es la respuesta a los textos de los perks, así que
        // ocupa el panel entero en vez de compartirlo con la selección.
        if (_zones)
        {
            _info.Text = UiText.Get("ui.team.zones") + "\n\n" + UiText.Get("ui.team.zonesHelp");
            _lineupTable.Text = string.Join("\n", LineupTable(links));
            return;
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

    /// <summary>Los modos de campo se declaran en código para no depender del formato binario del InputMap.</summary>
    private static void RegisterActions()
    {
        Register(CoverageAction, Key.C, JoyButton.X);
        Register(ZonesAction, Key.Z, JoyButton.Y);
    }

    private static void Register(string action, Key key, JoyButton button)
    {
        if (InputMap.HasAction(action))
        {
            return;
        }

        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
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
        var steps = new (string Name, Action Setup, bool Hover)[]
        {
            ("equipo", () => { }, false),
            ("equipo-zona", () =>
            {
                Pad("ui_right");
                Pad("ui_right");
                Pad("ui_right");
                Pad("ui_accept");
                Pad("ui_right");
                Pad("ui_right");
                Pad("ui_up");
            }, false),
            ("equipo-cobertura", () =>
            {
                Pad("ui_cancel");
                Pad(CoverageAction);
            }, false),
            ("equipo-ficha", () =>
            {
                Pad(CoverageAction);
                _focusRoster = true;
                _rosterIndex = IndexOfCard(FindRare());
                Pad("ui_accept");
            }, false),
            ("equipo-objeto", () =>
            {
                Pad("ui_cancel");
                _toast.Post(Array.Empty<ToastLine>());
                _focusRoster = true;
                _rosterIndex = IndexOfCard(EnsurePlacementItem());
                Pad("ui_accept");
            }, false),
            ("equipo-zonas", () =>
            {
                Pad("ui_cancel");
                Pad(ZonesAction);
            }, false),
            ("equipo-aviso", () =>
            {
                Pad(ZonesAction);
                _focusRoster = false;
                EnsurePlacementPerks();
                if (FindPerkMove() is not { } move)
                {
                    GD.PushWarning("ningún movimiento cambia el estado de un perk: la captura del aviso saldrá vacía");
                    return;
                }

                _cursor = move.From;
                Pad("ui_accept");
                _cursor = move.To;
                Pad("ui_accept");
            }, false),
            ("equipo-suplente", () =>
            {
                // AW-L: un suplente se coge exactamente igual que un titular. Se coge por el flujo de
                // mando (foco en la plantilla + botón de acción), que es el mismo camino que usa
                // ActivateRosterCard para el clic de ratón (UI-006).
                Pad("ui_cancel");
                int bench = FindOutfieldBenchPlayer();
                if (bench < 0)
                {
                    GD.PushWarning("no hay suplentes de campo: la captura de \"cogido\" no tiene a quién coger");
                    return;
                }

                _focusRoster = true;
                _rosterIndex = IndexOfCard(bench);
                Pad("ui_accept");
            }, false),
            ("equipo-sustitucion", () =>
            {
                // Soltarlo sobre la casilla de un titular de campo sustituye a ese titular (Sim.Placement
                // .PlacementView.WithPlayerAt ya lo resolvía; lo que faltaba era este camino de interfaz).
                _focusRoster = false;
                _cursor = FindOutfieldStarterCell();
                Pad("ui_accept");
            }, false),
            ("equipo-zona-frase", () =>
            {
                // AW-F: la ficha de un portador de perk de zona, con la frase que nombra la banda ya
                // marcada, y el campo tiñendo esa banda sola. El hover no se puede inyectar como se
                // inyecta una acción de mando (no hay evento de "ratón encima" que valga), así que el
                // paso se marca como Hover y la secuencia mueve el puntero de verdad y espera a que
                // salte el tooltip nativo antes de disparar.
                Pad("ui_cancel");
                _toast.Post(Array.Empty<ToastLine>());
                int carrier = EnsureZonePerk();
                if (carrier < 0)
                {
                    GD.PushWarning("ningún titular lleva un perk de zona: la captura del tooltip saldrá sin frase marcada");
                    return;
                }

                _focusRoster = true;
                _rosterIndex = IndexOfCard(carrier);
                Pad("ui_accept");
            }, true),
        };

        string directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(directory);

        foreach (var (name, setup, hover) in steps)
        {
            setup();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            // El puntero se mueve con la lista ya recolocada —expandir una ficha desplaza a las de
            // abajo, y eso lo resuelve el contenedor en el fotograma siguiente— y luego se espera a que
            // pase el retardo del tooltip, que es tiempo real, no fotogramas.
            if (hover)
            {
                HoverZoneHint();
                await ToSignal(GetTree().CreateTimer(TooltipSettle), SceneTreeTimer.SignalName.Timeout);
            }

            await ToSignal(RenderingServer.Singleton, "frame_post_draw");
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng(Path.Combine(directory, name + ".png"));
            GD.Print($"captura: {name}.png");
        }

        GetTree().Quit();
    }

    /// <summary>Inyecta una acción como si viniera del mando, por el mismo camino que la entrada real.</summary>
    private void Pad(string action) => _UnhandledInput(new InputEventAction { Action = action, Pressed = true });

    /// <summary>
    /// Segundos que la captura del tooltip espera con el puntero quieto. El retardo del tooltip nativo
    /// (<c>gui/timers/tooltip_delay_sec</c>) es medio segundo por defecto; con el doble sobra.
    /// </summary>
    private const double TooltipSettle = 1.2;

    /// <summary>
    /// <b>Solo para la secuencia de capturas.</b> Lleva el puntero encima de la primera frase de zona de
    /// la ficha enfocada, que es lo más parecido a un hover de verdad que se puede provocar: no existe
    /// una acción de entrada de "ratón encima" que inyectar como se inyecta un botón de mando. El
    /// resaltado del campo se pide además a mano, con la misma carga que el marcado le daría al hover,
    /// para que la captura enseñe la zona aunque el puntero sintético no llegue a disparar
    /// <c>meta_hover_started</c> bajo Xvfb.
    /// </summary>
    private void HoverZoneHint()
    {
        var card = _rosterIndex < _cards.Count ? _cards[_rosterIndex] : null;
        if (card is null || !card.TryZoneHint(out var point, out string kind, out string value))
        {
            GD.PushWarning("la ficha enfocada no marca ninguna zona: la captura del tooltip saldrá sin resaltado");
            return;
        }

        OnCardZoneHint(kind, value);
        Input.WarpMouse(point);
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (AW-F): un titular cuya ficha nombre una zona de inicio.
    /// Si la plantilla de pruebas no trae ninguno —los perks iniciales se reparten por rareza y con esta
    /// semilla casi nadie lleva uno—, se le pone <c>flank_specialist</c> a un centrocampista, que nombra
    /// las dos bandas y por tanto marca dos frases. No toca nada con una run detrás.
    /// </summary>
    private int EnsureZonePerk()
    {
        int carrier = FindZonePerkCarrier();
        if (carrier >= 0 || RunController.Instance is { HasRun: true })
        {
            return carrier;
        }

        int target = FindByPosition(SimPosition.Midfielder);
        if (target < 0)
        {
            return -1;
        }

        var players = new List<PlayerDefinition>(_state.Players.Count);
        foreach (var player in _state.Players)
        {
            players.Add(player.Id == target ? player with { Perks = new[] { "flank_specialist" } } : player);
        }

        _state = TeamState.Of(_state.Catalog, _state.Team with { Players = players });
        _pitch.State = _state;
        RefreshCards();
        return FindZonePerkCarrier();
    }

    /// <summary>Primer titular con un perk cuya descripción generada nombre un tercio o una banda.</summary>
    private int FindZonePerkCarrier()
    {
        foreach (var slot in _state.Lineup.Slots)
        {
            var player = _state.Find(slot.PlayerId);
            if (player is null)
            {
                continue;
            }

            foreach (string id in player.Perks)
            {
                if (_state.Catalog.Perks.Find(id) is { } perk
                    && ZoneHintText.Mentions(DescriptionGenerator.Describe(perk, _state.Templates), _state.Templates))
                {
                    return player.Id;
                }
            }
        }

        return -1;
    }

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

    /// <summary>
    /// <b>Solo para la secuencia de capturas.</b> La plantilla de pruebas reparte los perks iniciales por
    /// rareza (<c>PerkAssignment.AssignInitial</c>) y con esta semilla eso son cero perks en nueve de los
    /// diez jugadores: sin ningún perk de colocación el aviso no tendría nada que decir y la captura no
    /// enseñaría lo que documenta. Si no hay ninguno, se le pone uno real del catálogo a un titular. No
    /// toca nada con una run detrás: ahí los perks los reparte el bucle de run.
    /// </summary>
    private void EnsurePlacementPerks()
    {
        if (RunController.Instance is { HasRun: true } || FindPerkMove() is not null)
        {
            return;
        }

        int midfielder = FindByPosition(SimPosition.Midfielder);
        int defender = FindByPosition(SimPosition.Defender);
        var players = new List<PlayerDefinition>(_state.Players.Count);
        foreach (var player in _state.Players)
        {
            string? perk = player.Id == midfielder ? "flank_specialist" : player.Id == defender ? "spearpoint" : null;
            players.Add(perk is null || player.Perks.Count > 0 ? player : player with { Perks = new[] { perk } });
        }

        _state = TeamState.Of(_state.Catalog, _state.Team with { Players = players });
        _pitch.State = _state;
        RefreshCards();
    }

    /// <summary>
    /// Movimiento válido que más estados de perk cambia, para que la captura del aviso enseñe un aviso
    /// de verdad y no un campo mudo. Se busca con el mismo previsualizador que usa el aviso
    /// (<c>Sim.Perks.LineupPerkPreviewer</c>) sobre alineaciones hipotéticas, sin mover nada.
    /// </summary>
    private (Cell From, Cell To)? FindPerkMove()
    {
        var current = LineupPerkPreviewer.Preview(_state.Lineup, _state.Players, _state.Catalog);
        (Cell From, Cell To)? best = null;
        int bestScore = 0;

        foreach (var slot in _state.Lineup.Slots)
        {
            var player = _state.Find(slot.PlayerId);
            if (player is null || !PlacementView.CanPlace(player.Position, slot.HomeCell))
            {
                continue;
            }

            for (int column = 0; column < Pitch.PlacementColumns; column++)
            {
                for (int row = 0; row < Pitch.Rows; row++)
                {
                    var target = new Cell(column, row);
                    if (target == slot.HomeCell || !PlacementView.CanPlace(player.Position, target))
                    {
                        continue;
                    }

                    var lineup = _state.Preview(slot.PlayerId, target);
                    int score = Changed(current, LineupPerkPreviewer.Preview(lineup, _state.Players, _state.Catalog));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = (slot.HomeCell, target);
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Cuántos estados de perk cambia esa alineación respecto de la actual. Una activación puntúa doble:
    /// la captura tiene que enseñar el caso bueno, no solo el aviso de que algo se ha apagado.
    /// </summary>
    private static int Changed(IReadOnlyList<LineupPerkPreview> current, IReadOnlyList<LineupPerkPreview> next)
    {
        int score = 0;
        foreach (var entry in next)
        {
            if (FindPreview(current, entry.PlayerId, entry.PerkId) is { } previous && previous.Status != entry.Status)
            {
                score += entry.Status == LineupPerkStatus.Active ? 2 : 1;
            }
        }

        return score;
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

    /// <summary>
    /// Primer suplente que no sea portero (AW-L, solo para capturas): la sustitución que enseña la
    /// captura tiene que poder soltarse en cualquier casilla de campo, y el portero solo puede ir a la
    /// suya (<c>PlacementView.CanPlace</c>).
    /// </summary>
    private int FindOutfieldBenchPlayer()
    {
        foreach (var player in _state.Players)
        {
            if (!_state.IsStarter(player.Id) && player.Position != SimPosition.Goalkeeper)
            {
                return player.Id;
            }
        }

        return -1;
    }

    /// <summary>Casilla-hogar de un titular de campo (no portero), para la captura de la sustitución.</summary>
    private Cell FindOutfieldStarterCell()
    {
        foreach (var slot in _state.Lineup.Slots)
        {
            var player = _state.Find(slot.PlayerId);
            if (player is not null && player.Position != SimPosition.Goalkeeper)
            {
                return slot.HomeCell;
            }
        }

        return PlacementView.GoalkeeperCell;
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

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (AW-K): la plantilla de pruebas no tiene ningún jugador
    /// equipado (<c>TeamState.Load</c> no arrastra ninguna run), y sin uno la ficha no tendría delta que
    /// enseñar ni sección de objeto real que fotografiar. Es el mismo apaño que
    /// <see cref="EnsurePlacementPerks"/> hace con perks, pero para objetos: fuerza un maldito de verdad
    /// (<c>berserker_totem</c>, sube fuerza/velocidad/resistencia y baja técnica) en el primer titular,
    /// para que la captura enseñe el delta positivo y el negativo de la barra a la vez. No toca nada con
    /// una run detrás. Devuelve el id del jugador al que se le ha puesto, para enfocar su ficha.
    /// </summary>
    private int EnsurePlacementItem()
    {
        if (_state.Lineup.Slots.Count == 0)
        {
            return -1;
        }

        int target = _state.Lineup.Slots[0].PlayerId;
        if (RunController.Instance is { HasRun: true } || _state.EquippedItemOf(target) is not null)
        {
            return target;
        }

        var items = ItemLoader.FromJson(GameData.Snapshot);
        if (items.Find("berserker_totem") is { } item)
        {
            _state.ForceTestItem(target, item);
            RefreshCards();
        }

        return target;
    }
}
