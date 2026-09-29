using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Data;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
using Underleague.Game.Ui.Knavall;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Placement;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
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
/// <para>
/// <b>Lenguaje de Knavall (ADR 0162).</b> Es la pantalla de referencia del lenguaje visual: cabecera de
/// madera con pestañas colgadas, la plantilla con retratos grandes a la izquierda (siempre visible) y cuatro
/// pestañas para el resto — <b>Plantilla</b> (la ficha del jugador señalado, protagonista, con el cofre y los
/// consumibles en compacto), <b>Alineación</b> (el campo de colocación de siempre), <b>Consumibles</b> y
/// <b>Cofre</b>. Todo se monta en código (<c>Equipo.tscn</c> es solo la raíz con este script, como el resto
/// de pantallas, <c>Widgets</c>), y todo lo que explica algo lo hace en un cartel de ayuda al pasar el ratón,
/// no con texto fijo en pantalla.
/// </para>
/// </summary>
public partial class TeamScreen : Control
{
    private const string CoverageAction = "team_coverage";
    private const string ZonesAction = "team_zones";
    private const string TabPrevAction = "team_tab_prev";
    private const string TabNextAction = "team_tab_next";

    /// <summary>Área útil bajo la cabecera, a 1280x800.</summary>
    private const float BodyTop = 96f;
    private const float BodyBottom = 764f;

    /// <summary>Columna de la plantilla (la de siempre, 376 px en la versión anterior).</summary>
    private static readonly Rect2 RosterArea = new(12f, BodyTop, 326f, BodyBottom - BodyTop);

    /// <summary>Todo lo que queda a la derecha de la plantilla: lo que cambia con la pestaña.</summary>
    private static readonly Rect2 PageArea = new(344f, BodyTop, 924f, BodyBottom - BodyTop);

    /// <summary>Las pestañas, en el orden de la cabecera.</summary>
    private enum Tab
    {
        Roster,
        Lineup,
        Consumables,
        Chest,
    }

    private readonly List<RosterRow> _rows = new();

    private TeamState _state = null!;
    private TeamHeader _header = null!;
    private RosterBoard _board = null!;

    private Control _rosterPage = null!;
    private PlayerDossier _dossier = null!;
    private ChestView _chestView = null!;
    private ConsumablesView _consumablesView = null!;

    private Control _lineupPage = null!;
    private PitchView _pitch = null!;
    private LegendView _legend = null!;
    private Label _info = null!;
    private RichTextLabel _perksNote = null!;
    private Label _lineupTable = null!;
    private Label _lineupTitle = null!;
    private Label _riskTitle = null!;
    private Label _risk = null!;
    private PlaqueButton _zonesButton = null!;
    private PlaqueButton _coverageButton = null!;

    private ConsumablesPanel _consumables = null!;
    private ChestPanel _chest = null!;
    private Toast _toast = null!;
    private Label _mouseHelp = null!;
    private Label _padHelp = null!;

    private Tab _tab = Tab.Roster;
    private int _selected = -1;
    private int _held = -1;
    private int _rosterIndex;
    private bool _focusRoster = true;
    private bool _coverage;
    private bool _zones;
    private string _chestPick = string.Empty;
    private bool _picking;
    private Cell _pressCell;
    private Cell _cursor = PlacementView.GoalkeeperCell;

    public override void _Ready()
    {
        Layout.CenterLegacy(this);

        // Los carteles de ayuda se dibujan enteros ellos mismos (InkTooltip): el panel nativo de Godot
        // que los envuelve no tiene que pintar nada. Se hace sobre una copia del Theme de pantallas viejas
        // para no cambiar los tooltips del resto de pantallas.
        // Mesa de madera con vetas y clavos detrás de todo (la de Mapa): los carteles se apoyan en algo.
        var table = new WoodTable { Position = Vector2.Zero, Size = Layout.LegacySize };
        AddChild(table);
        MoveChild(table, 1);

        var theme = (Theme)Widgets.BuildLegacyTheme().Duplicate();
        theme.SetStylebox("panel", "TooltipPanel", new StyleBoxEmpty());
        Theme = theme;

        RegisterActions();

        // Con una run en curso, la plantilla es la suya: esta pantalla es donde se toman todas las
        // decisiones de plantilla (UI-020) y las decisiones son sobre los jugadores de verdad. Sin run
        // —al regenerar las capturas, o al abrir la escena suelta— sigue valiendo la plantilla de pruebas
        // con la que se diseñó, con semilla fija para que enseñe siempre lo mismo.
        var run = RunController.Instance;
        bool inRun = run is { HasRun: true };
        _state = inRun ? TeamState.FromRun(run!) : TeamState.Load(20260904UL);

        BuildHeader(inRun);
        BuildRosterPage();
        BuildLineupPage();

        _consumables = new ConsumablesPanel { Position = PageArea.Position, Size = PageArea.Size };
        AddChild(_consumables);

        _chest = new ChestPanel { Position = PageArea.Position, Size = PageArea.Size };
        AddChild(_chest);
        _chest.Picked += OnChestPicked;
        _chest.EquipPressed += OnEquipFromChest;
        _chest.StorePressed += OnStoreInChest;
        _chest.PassToggled += OnPassToggled;
        _chest.TransferTo += OnTransfer;

        _mouseHelp = HelpLine(768f);
        _padHelp = HelpLine(783f);

        // El aviso vive por encima de todo y no recibe ratón: se apoya en el borde inferior del panel de la
        // derecha, para que el ojo no tenga que salir de lo que acaba de cambiar.
        _toast = new Toast { BottomLeft = new Vector2(404f, 512f), MaximumWidth = 800f };
        AddChild(_toast);

        BuildRoster();
        RefreshCards();
        var ordered = OrderedRoster();
        if (ordered.Count > 0)
        {
            Select(ordered[0]);
        }

        RefreshPitch();
        _consumables.Rebuild(_state);
        SetTab(Tab.Roster);

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

    // ------------------------------------------------------------------ montaje

    private void BuildHeader(bool inRun)
    {
        _header = new TeamHeader { Position = Vector2.Zero, Size = new Vector2(Layout.LegacySize.X, TeamHeader.HeaderHeight) };
        AddChild(_header);
        _header.Setup(
            new (string, Glyph, Tip)[]
            {
                (UiText.Get("ui.kn.tab.roster"), Glyph.Shirt, new Tip(UiText.Get("ui.kn.tab.roster"), UiText.Get("ui.kn.tab.rosterTip"), Glyph.Shirt)),
                (UiText.Get("ui.kn.tab.lineup"), Glyph.Pitch, new Tip(UiText.Get("ui.kn.tab.lineup"), UiText.Get("ui.kn.tab.lineupTip"), Glyph.Pitch)),
                (UiText.Get("ui.kn.tab.consumables"), Glyph.Potion, new Tip(UiText.Get("ui.kn.tab.consumables"), UiText.Get("ui.kn.tab.consumablesTip"), Glyph.Potion)),
                (UiText.Get("ui.kn.tab.chest"), Glyph.Chest, new Tip(UiText.Get("ui.kn.tab.chest"), UiText.Get("ui.kn.tab.chestTip"), Glyph.Chest)),
            },
            inRun);
        _header.TabPressed += index => SetTab((Tab)index);
        _header.BackPressed += GoBack;
        _header.Bind(
            _state.Catalog.Race(_state.Team.Race).Name.Es,
            inRun ? _state.Team.Name : UiText.Get("ui.team.placeholderClub"),
            _state.Players.Count);
    }

    private void BuildRosterPage()
    {
        _board = new RosterBoard { Position = RosterArea.Position, Size = RosterArea.Size };
        AddChild(_board);

        _rosterPage = new Control { Position = PageArea.Position, Size = PageArea.Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_rosterPage);

        _dossier = new PlayerDossier { Position = Vector2.Zero, Size = new Vector2(512f, PageArea.Size.Y) };
        _rosterPage.AddChild(_dossier);
        _dossier.EquipPressed += OnEquipFromChest;
        _dossier.StorePressed += OnStoreInChest;
        _dossier.PassToggled += OnPassToggled;
        _dossier.TransferTo += OnTransfer;

        _chestView = new ChestView { Position = new Vector2(516f, 0f), Size = new Vector2(408f, 380f) };
        _rosterPage.AddChild(_chestView);
        _chestView.Picked += OnChestPicked;
        _chestView.MorePressed += () => SetTab(Tab.Chest);

        _consumablesView = new ConsumablesView { Position = new Vector2(516f, 384f), Size = new Vector2(408f, PageArea.Size.Y - 384f) };
        _rosterPage.AddChild(_consumablesView);
        _consumablesView.Picked += id =>
        {
            SetTab(Tab.Consumables);
            _consumables.Select(id);
        };
    }

    private void BuildLineupPage()
    {
        _lineupPage = new Control { Position = Vector2.Zero, Size = Layout.LegacySize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_lineupPage);

        var sheet = new SheetCanvas { Position = PageArea.Position, Size = PageArea.Size, Seed = 1717 };
        _lineupPage.AddChild(sheet);

        var title = new Label { Text = UiText.Get("ui.kn.lineupTitle").ToUpperInvariant(), Position = new Vector2(364f, 104f) };
        Styled(title, Ink.Display, 32, Ink.Black);
        _lineupPage.AddChild(title);

        var hint = new Label { Text = UiText.Get("ui.team.pitchHint"), Position = new Vector2(366f, 142f) };
        Styled(hint, Ink.Data, Ink.SizeSmall, Ink.Muted);
        _lineupPage.AddChild(hint);

        // Zonas y cobertura: las dos lecturas del mismo campo, excluyentes. Con tecla y botón de mando
        // (UI-006: los dos flujos completos); las placas son el disparador de ratón.
        _zonesButton = PlaqueButton.Create(_lineupPage, UiText.Get("ui.kn.zones"), Glyph.Zones, PlaqueKind.Paper, new Rect2(1000f, 104f, 124f, 50f), 91);
        _zonesButton.Tip = new Tip(UiText.Get("ui.kn.zones"), UiText.Get("ui.kn.zonesTip"), Glyph.Zones);
        _zonesButton.Pressed += ToggleZones;
        _coverageButton = PlaqueButton.Create(_lineupPage, UiText.Get("ui.kn.coverage"), Glyph.Coverage, PlaqueKind.Paper, new Rect2(1128f, 104f, 128f, 50f), 92);
        _coverageButton.Tip = new Tip(UiText.Get("ui.kn.coverage"), UiText.Get("ui.kn.coverageTip"), Glyph.Coverage);
        _coverageButton.Pressed += ToggleCoverage;

        _pitch = new PitchView { Position = new Vector2(398f, 164f), Size = new Vector2(816f, 357f) };
        _lineupPage.AddChild(_pitch);
        _pitch.State = _state;
        _pitch.CellPressed += OnCellPressed;
        _pitch.CellReleased += OnCellReleased;
        _pitch.CellHovered += OnCellHovered;

        _legend = new LegendView { Position = new Vector2(398f, 530f), Size = new Vector2(816f, 26f), TextColor = Ink.Brown };
        _lineupPage.AddChild(_legend);

        _info = BodyLabel(new Rect2(364f, 564f, 440f, 96f));
        _perksNote = new RichTextLabel
        {
            Position = new Vector2(364f, 664f),
            Size = new Vector2(440f, 92f),
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _perksNote.AddThemeFontOverride("normal_font", Ink.Plain);
        _perksNote.AddThemeFontOverride("bold_font", Ink.Heavy);
        _perksNote.AddThemeFontSizeOverride("bold_font_size", 14);
        _perksNote.AddThemeFontSizeOverride("normal_font_size", 14);
        _perksNote.AddThemeColorOverride("default_color", Ink.Brown);
        _perksNote.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _perksNote.MetaHoverStarted += meta =>
        {
            if (ZoneHintText.TryParse(meta.AsString(), out string kind, out string key))
            {
                OnCardZoneHint(kind, key);
            }
        };
        _perksNote.MetaHoverEnded += _ => OnCardZoneHint(string.Empty, string.Empty);
        _lineupPage.AddChild(_perksNote);

        _lineupTitle = new Label { Text = UiText.Get("ui.kn.onPitch").ToUpperInvariant(), Position = new Vector2(826f, 562f) };
        Styled(_lineupTitle, Ink.Display, 17, Ink.RedDark);
        _lineupPage.AddChild(_lineupTitle);
        _lineupTable = BodyLabel(new Rect2(826f, 586f, 420f, 122f));
        Styled(_lineupTable, Ink.Data, 13, Ink.Brown);

        _riskTitle = new Label { Text = UiText.Get("ui.scout.risk").ToUpperInvariant(), Position = new Vector2(826f, 706f) };
        Styled(_riskTitle, Ink.Display, 17, Ink.RedDark);
        _lineupPage.AddChild(_riskTitle);
        _risk = BodyLabel(new Rect2(826f, 726f, 420f, 30f));
        Styled(_risk, Ink.Data, 13, Ink.Brown);
    }

    private Label BodyLabel(Rect2 area)
    {
        var label = new Label
        {
            Position = area.Position,
            Size = area.Size,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ClipText = true,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Styled(label, Ink.Heavy, 14, Ink.Brown);
        _lineupPage.AddChild(label);
        return label;
    }

    private static void Styled(Label label, Font font, int size, Color color)
    {
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.MouseFilter = MouseFilterEnum.Ignore;
    }

    private Label HelpLine(float y)
    {
        var label = new Label { Position = new Vector2(16f, y), Size = new Vector2(1248f, 16f) };
        Styled(label, Ink.Plain, Ink.SizeSmall, Style.OnWood);
        label.ClipText = true;
        AddChild(label);
        return label;
    }

    /// <summary>
    /// Vuelve a donde se estaba. Si Mercado, Recompensa o Informe dejaron dicho un desvío en
    /// <see cref="Nav.ReturnTo"/> (AW-N, botón "Ver equipo" de esas pantallas), se vuelve ahí y se
    /// limpia el desvío para no arrastrarlo a la próxima vez que se entre a Equipo. Si no hay desvío, la
    /// pantalla de Equipo no sabe navegar por su cuenta —no es suya esa decisión—: mira si hay un nodo
    /// elegido, que es el dato que lo dice, y va al ojeo si se vino a repasar la alineación antes de un
    /// partido, o al mapa si no.
    /// </summary>
    private void GoBack()
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
    }

    // ------------------------------------------------------------------ pestañas

    /// <summary>
    /// Cambia de pestaña. Zonas y cobertura son lecturas del campo, así que solo viven en Alineación: al
    /// salir se apagan, igual que antes se apagaban al abrir consumibles o cofre (los modos eran excluyentes).
    /// </summary>
    private void SetTab(Tab tab)
    {
        _tab = tab;
        if (tab != Tab.Lineup)
        {
            _coverage = false;
            _zones = false;
            _held = -1;
            OnCardZoneHint(string.Empty, string.Empty);
        }

        if (tab != Tab.Lineup)
        {
            _focusRoster = true;
        }

        if (_selected < 0 && tab is Tab.Roster or Tab.Chest && OrderedRoster() is { Count: > 0 } ordered)
        {
            Select(ordered[0]);
        }

        _picking = false;
        _header.Active = (int)tab;
        _rosterPage.Visible = tab == Tab.Roster;
        _lineupPage.Visible = tab == Tab.Lineup;
        _consumables.Visible = tab == Tab.Consumables;
        _chest.Visible = tab == Tab.Chest;
        if (tab == Tab.Consumables)
        {
            _consumables.Rebuild(_state);
        }

        RefreshPitch();
        RefreshItems();
        ApplyRowFlags();
        UpdateInputHelp();
    }

    private void CycleTab(int step)
    {
        int count = Enum.GetValues<Tab>().Length;
        SetTab((Tab)(((int)_tab + step + count) % count));
    }

    /// <summary>
    /// La ayuda de entrada al pie cambia con la pestaña: en Alineación es la de siempre (ratón y mando
    /// completos, UI-006); en las demás, la ficha, el cofre y los consumibles son hoy solo de ratón y la
    /// línea de mando lo dice en vez de prometer un segundo flujo que no existe.
    /// </summary>
    private void UpdateInputHelp()
    {
        switch (_tab)
        {
            case Tab.Lineup:
                _mouseHelp.Text = UiText.Get("ui.input.mouse");
                _padHelp.Text = UiText.Get("ui.input.pad");
                break;
            case Tab.Consumables:
                _mouseHelp.Text = UiText.Get("ui.team.consumableInputMouse");
                _padHelp.Text = UiText.Get("ui.kn.inputPad");
                break;
            case Tab.Chest:
                _mouseHelp.Text = UiText.Get("ui.team.chestInputMouse");
                _padHelp.Text = UiText.Get("ui.kn.inputPad");
                break;
            default:
                _mouseHelp.Text = UiText.Get("ui.kn.inputMouse");
                _padHelp.Text = UiText.Get("ui.kn.inputPad");
                break;
        }
    }

    // ------------------------------------------------------------------ cofre (ADR 0161 §3)

    private void OnChestPicked(string id)
    {
        _chestPick = _chestPick == id ? string.Empty : id;
        _picking = false;
        RefreshItems();
    }

    private void OnEquipFromChest()
    {
        if (_selected < 0 || _chestPick.Length == 0)
        {
            return;
        }

        if (TryItem(() => _state.EquipStored(_selected, _chestPick)))
        {
            _chestPick = string.Empty;
            Flash(_selected);
        }

        AfterItemChange();
    }

    private void OnStoreInChest()
    {
        if (_selected < 0)
        {
            return;
        }

        TryItem(() => _state.StoreEquipped(_selected));
        AfterItemChange();
    }

    private void OnPassToggled()
    {
        _picking = !_picking;
        RefreshItems();
    }

    private void OnTransfer(int targetId)
    {
        if (_selected < 0)
        {
            return;
        }

        if (TryItem(() => _state.TransferEquipped(_selected, targetId)))
        {
            Flash(targetId);
        }

        _picking = false;
        AfterItemChange();
    }

    /// <summary>
    /// /Sim rechaza con mensajes de desarrollo (RT-032); el jugador lee uno localizado y genérico, en el
    /// aviso, que es donde se lee lo que acaba de pasar.
    /// </summary>
    private bool TryItem(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception)
        {
            _toast.Post(new[] { new ToastLine(UiText.Get("ui.team.chestError"), Style.LinkBroken) });
            return false;
        }
    }

    private void AfterItemChange()
    {
        RefreshCards();
        RefreshItems();
    }

    /// <summary>Repinta todo lo que enseña objetos: la ficha, el cofre compacto, el grande y los consumibles.</summary>
    private void RefreshItems()
    {
        _dossier.Bind(_state, _selected, _chestPick, _picking);
        _chestView.Bind(_state, _chestPick);
        _consumablesView.Bind(_state);
        _chest.Rebuild(_state, _selected, _chestPick, _picking);
    }

    // ------------------------------------------------------------------ entrada

    /// <summary>
    /// Segundo flujo de entrada completo (UI-006, RT-071): cruceta para el cursor, botón de acción para
    /// seleccionar y para coger y soltar, cancelar para soltar sin mover, y una sola pulsación para el
    /// modo de cobertura. No hay ninguna acción exclusiva del ratón en la colocación; gatillos o Q / E
    /// cambian de pestaña.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(TabPrevAction))
        {
            CycleTab(-1);
            return;
        }

        if (@event.IsActionPressed(TabNextAction))
        {
            CycleTab(1);
            return;
        }

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
            if (_tab != Tab.Lineup)
            {
                if (_picking)
                {
                    OnPassToggled();
                }
                else if (_tab != Tab.Roster)
                {
                    SetTab(Tab.Roster);
                }

                return;
            }

            if (_held >= 0)
            {
                _held = -1;
                RefreshPitch();
                ApplyRowFlags();
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

    /// <summary>
    /// Un único anillo de foco: la lista a la izquierda y, en Alineación, la cuadrícula a la derecha. En las
    /// demás pestañas la cruceta solo recorre la plantilla.
    /// </summary>
    private void MoveFocus(int dx, int dy)
    {
        if (_focusRoster || _tab != Tab.Lineup)
        {
            if (dx > 0 && _tab == Tab.Lineup)
            {
                _focusRoster = false;
                RefreshPitch();
                return;
            }

            if (dy != 0 && _rows.Count > 0)
            {
                _rosterIndex = Math.Clamp(_rosterIndex + dy, 0, _rows.Count - 1);
                Select(_rows[_rosterIndex].PlayerId);
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
        if (_focusRoster || _tab != Tab.Lineup)
        {
            if (_rosterIndex < _rows.Count)
            {
                ActivateRosterCard(_rows[_rosterIndex].PlayerId);
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
            ApplyRowFlags();
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
        ApplyRowFlags();
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
                RefreshCards();
                Flash(player);
            }
        }

        RefreshPitch();
        ApplyRowFlags();
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
    /// Un solo patrón de inspección (UI-001): activar a un jugador lo señala —su ficha en Plantilla, su zona
    /// de acción en Alineación, el destinatario de las acciones en Cofre—. Da igual que la activación venga
    /// de un clic en la fila, de un clic en la casilla o del botón de acción del mando.
    /// </summary>
    private void Select(int playerId)
    {
        if (playerId != _selected)
        {
            _picking = false;
        }

        _selected = playerId;
        if (_selected >= 0 && _state.CellOf(_selected) is { } cell)
        {
            _cursor = cell;
        }

        ApplyRowFlags();
        RefreshPitch();
        RefreshItems();
    }

    /// <summary>
    /// Activar una fila de la plantilla. En Alineación es el gesto de siempre (AW-L): un suplente se coge
    /// para soltarlo en el campo, exactamente como se coge a un titular desde una casilla; coger tiene
    /// prioridad, y activar al ya señalado lo deja de señalar. En las demás pestañas no hay campo donde
    /// soltar a nadie, así que activar es solo señalar, y la ficha nunca se queda vacía por un segundo clic.
    /// </summary>
    private void ActivateRosterCard(int playerId)
    {
        if (_tab != Tab.Lineup)
        {
            Select(playerId);
            return;
        }

        if (!_state.IsStarter(playerId) && _held < 0)
        {
            _held = playerId;
            Select(playerId);
            RefreshPitch();
            ApplyRowFlags();
            return;
        }

        if (_held == playerId)
        {
            _held = -1;
            RefreshPitch();
            ApplyRowFlags();
            return;
        }

        Select(_selected == playerId ? -1 : playerId);
    }

    private void ApplyRowFlags()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            bool current = _rows[i].PlayerId == _selected;
            _rows[i].Selected = current;
            _rows[i].Held = _rows[i].PlayerId == _held;
            if (current)
            {
                _rosterIndex = i;
            }
        }
    }

    /// <summary>Zonas y cobertura son excluyentes: superpuestas no se entiende ninguna (§6). Las dos viven en Alineación.</summary>
    private void ToggleCoverage()
    {
        if (_tab != Tab.Lineup)
        {
            SetTab(Tab.Lineup);
        }

        _coverage = !_coverage;
        if (_coverage)
        {
            _zones = false;
        }

        RefreshPitch();
    }

    private void ToggleZones()
    {
        if (_tab != Tab.Lineup)
        {
            SetTab(Tab.Lineup);
        }

        _zones = !_zones;
        if (_zones)
        {
            _coverage = false;
        }

        RefreshPitch();
    }

    private void Flash(int playerId)
    {
        foreach (var row in _rows)
        {
            if (row.PlayerId == playerId)
            {
                row.Flash();
            }
        }
    }

    /// <summary>
    /// Construye la plantilla: una fila por jugador, titulares primero en orden de columna y fila —de la
    /// portería al ataque, como se lee el campo— y después los suplentes, con un brochazo de cabecera para
    /// cada grupo. El alto de fila se reparte para que quepa la plantilla entera (hasta 12, RunState.MaxRosterSize)
    /// sin barra de desplazamiento.
    /// </summary>
    private void BuildRoster()
    {
        int starters = _state.Lineup.Slots.Count;
        int total = _state.Players.Count;
        const float Banner = 40f;
        float available = RosterArea.Size.Y - 24f - (Banner * 2f) - 8f;
        float rowHeight = Mathf.Clamp(Mathf.Floor(available / Mathf.Max(1, total)), 44f, 60f);

        float y = 12f;
        _board.Banners.Clear();
        _board.Banners.Add((y, UiText.Get("ui.kn.starters")));
        y += Banner;
        for (int i = 0; i < starters; i++)
        {
            AddRow(y, rowHeight);
            y += rowHeight;
        }

        y += 4f;
        _board.Banners.Add((y, UiText.Get("ui.kn.bench")));
        y += Banner;
        for (int i = starters; i < total; i++)
        {
            AddRow(y, rowHeight);
            y += rowHeight;
        }

        _board.QueueRedraw();
    }

    private void AddRow(float y, float height)
    {
        var row = new RosterRow { Position = new Vector2(10f, y), Size = new Vector2(RosterArea.Size.X - 20f, height) };
        _board.AddChild(row);
        row.Activated += OnRowActivated;
        _rows.Add(row);
    }

    private void OnRowActivated(int playerId)
    {
        _focusRoster = true;
        ActivateRosterCard(playerId);
    }

    /// <summary>
    /// El ratón descansa sobre el nombre de un tercio o de una banda dentro de la descripción de un perk
    /// (AW-F): además del tooltip que pinta el propio <c>RichTextLabel</c>, se tiñe <b>esa</b> franja
    /// sobre la cuadrícula, que es la respuesta a "¿dónde está eso?" sin salir de la pantalla. Con las dos
    /// cadenas vacías se apaga.
    /// </summary>
    private void OnCardZoneHint(string kind, string value)
    {
        _pitch.HighlightedZoneKey = kind == ZoneHintText.ZoneKind ? value : null;
        _pitch.HighlightedFlankKey = kind == ZoneHintText.FlankKind ? value : null;
        _pitch.QueueRedraw();
    }

    /// <summary>Rellena las filas. Se llama al cambiar la alineación o los objetos, no al mover el cursor.</summary>
    private void RefreshCards()
    {
        int index = 0;
        var ordered = OrderedRoster();
        foreach (var row in _rows)
        {
            if (index >= ordered.Count)
            {
                break;
            }

            var player = _state.Find(ordered[index]);
            index++;
            if (player is not null)
            {
                row.Bind(_state, player);
            }
        }

        ApplyRowFlags();
        RefreshItems();
    }

    /// <summary>
    /// Los perks del jugador señalado bajo el campo, con las frases que nombran una zona marcadas (AW-F):
    /// es donde el campo está a la vista, así que es donde tiene sentido pasar el ratón por «su tercio
    /// adelantado» y verlo teñirse.
    /// </summary>
    private void RefreshPerksNote(PlayerDefinition? player)
    {
        if (player is null || _held >= 0 || _zones || _coverage)
        {
            _perksNote.Text = string.Empty;
            return;
        }

        var lines = new List<string>();
        foreach (string id in player.Perks)
        {
            if (_state.Catalog.Perks.Find(id) is { } perk)
            {
                lines.Add(Line(perk));
            }
        }

        if (_state.Catalog.Perks.Find(_state.Catalog.Race(player.Race).Ability) is { } racial)
        {
            lines.Add(Line(racial));
        }

        _perksNote.Text = string.Join("\n", lines);

        string Line(PerkDefinition perk)
        {
            string text = DescriptionGenerator.Describe(perk, _state.Templates).Replace("\n", " ");
            string body = ZoneHintText.Markup(text, _state.Templates) ?? text.Replace("[", "[lb]");
            return "[b]" + UiText.Name(perk.Name) + ":[/b] " + body;
        }
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
        _zonesButton.Kind = _zones ? PlaqueKind.Tab : PlaqueKind.Paper;
        _zonesButton.Active = _zones;
        _coverageButton.Kind = _coverage ? PlaqueKind.Tab : PlaqueKind.Paper;
        _coverageButton.Active = _coverage;
        RefreshPerksNote(shown >= 0 ? _state.Find(shown) : null);
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

    /// <summary>Los modos de campo y las pestañas se declaran en código para no depender del formato binario del InputMap.</summary>
    private static void RegisterActions()
    {
        Register(CoverageAction, Key.C, JoyButton.X);
        Register(ZonesAction, Key.Z, JoyButton.Y);
        Register(TabPrevAction, Key.Q, JoyButton.LeftShoulder);
        Register(TabNextAction, Key.E, JoyButton.RightShoulder);
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
    /// Deja en <c>Game/screenshots/</c> las capturas que cuentan la pantalla. Es la única forma de que el
    /// revisor la juzgue sin abrir el editor; el comando está en <c>docs/ui-equipo.md</c>.
    /// </summary>
    private async void CaptureSequence()
    {
        // Las capturas de colocación se producen **con el flujo de mando** (eventos de acción sintéticos),
        // no llamando a los métodos por dentro: así la secuencia comprueba de paso que la navegación sin
        // ratón lleva a los mismos estados (UI-006, RT-071).
        // El ratón fuera de todo: un cartel de ayuda por hover real saldría en unas capturas y en otras no.
        Input.WarpMouse(GetViewport().GetVisibleRect().Size - new Vector2(2f, 2f));

        InkTooltip? shownTip = null;
        void ShowTip(Tip tip, Vector2 at)
        {
            // El cartel de ayuda sale con el ratón encima, y bajo Xvfb no hay hover fiable: para la captura
            // se coloca el mismo cartel que construiría el control, donde lo pondría Godot (bajo el puntero).
            shownTip = InkTooltip.Build(tip);
            shownTip.Position = at;
            AddChild(shownTip);
        }

        void HideTip()
        {
            if (shownTip is not null)
            {
                RemoveChild(shownTip);
                shownTip.QueueFree();
                shownTip = null;
            }
        }

        var steps = new (string Name, Action Setup, bool Hover)[]
        {
            ("equipo", () => { }, false),
            ("equipo-objeto", () =>
            {
                // El escaparate: un jugador con objeto (un maldito, para que se vea el modificador positivo y
                // el negativo a la vez), el cofre con cosas y consumibles comprados.
                int carrier = EnsurePlacementItem();
                EnsureTestStoredItems();
                EnsureTestConsumables();
                RefreshItems();
                Select(carrier);
                _chestPick = "worn_boots";
                RefreshItems();
            }, false),
            ("equipo-ayuda", () =>
            {
                // Un cartel de ayuda de verdad: el de la técnica del maldito, que es el que más explica.
                var player = _state.Find(_selected);
                int modifier = _state.EquippedItemOf(_selected)?.Modifier.Get(AttributeKind.Technique) ?? 0;
                if (player is not null)
                {
                    ShowTip(TeamTips.Attribute(_state, AttributeKind.Technique, player.Attributes.Technique, modifier), PageArea.Position + new Vector2(170f, 290f));
                }
            }, false),
            ("equipo-ayuda-rasgo", () =>
            {
                HideTip();
                var player = _state.Find(_selected);
                if (player is { Traits.Count: > 0 })
                {
                    ShowTip(TeamTips.Trait(_state, player.Traits[0]), PageArea.Position + new Vector2(140f, 372f));
                }
            }, false),
            ("equipo-pasar", () =>
            {
                HideTip();
                _chestPick = string.Empty;
                OnPassToggled();
            }, false),
            ("equipo-ficha", () =>
            {
                _picking = false;
                int rare = FindRare();
                EnsureTestCareer(rare);
                _rosterIndex = IndexOfCard(rare);
                Pad("ui_accept");
            }, false),
            ("equipo-zona", () =>
            {
                SetTab(Tab.Lineup);
                Select(-1);
                _focusRoster = false;
                _cursor = PlacementView.GoalkeeperCell;
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
            ("equipo-zonas", () =>
            {
                Pad(CoverageAction);
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
                // AW-F: el portador de un perk de zona, con su frase bajo el campo y el campo tiñendo esa
                // franja sola. El hover no se puede inyectar como una acción de mando, así que el paso
                // pide el resaltado con la misma carga que le daría el hover (HoverZoneHint).
                Pad("ui_cancel");
                _toast.Post(Array.Empty<ToastLine>());
                int carrier = EnsureZonePerk();
                if (carrier < 0)
                {
                    GD.PushWarning("ningún titular lleva un perk de zona: la captura saldrá sin frase marcada");
                    return;
                }

                Select(carrier);
            }, true),
            ("equipo-consumibles", () =>
            {
                // CAT-B: aquí se equipa lo que ya se compró en el mercado (inventario forzado por
                // EnsureTestConsumables, ver equipo-objeto).
                _toast.Post(Array.Empty<ToastLine>());
                OnCardZoneHint(string.Empty, string.Empty);
                EnsureTestConsumables();

                // Desde Alineación, el gatillo derecho lleva a Consumibles: el cambio de pestaña con mando.
                Pad(TabNextAction);
                _consumables.SelectForTest("smoke_flare");
            }, false),
            ("equipo-cofre", () =>
            {
                // ADR 0161 §3: el cofre en grande, con el portador del maldito señalado para que las tres
                // placas estén vivas y un objeto elegido.
                int carrier = EnsurePlacementItem();
                EnsureTestStoredItems();
                Select(carrier);
                SetTab(Tab.Chest);
                _chestPick = "worn_boots";
                RefreshItems();
            }, false),
        };

        string directory = ProjectSettings.GlobalizePath("res://screenshots");
        Directory.CreateDirectory(directory);

        foreach (var (name, setup, hover) in steps)
        {
            setup();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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
    /// <b>Solo para la secuencia de capturas.</b> Pide el resaltado de la primera zona que nombran los perks
    /// del jugador señalado, con la misma carga que le daría el hover sobre su frase bajo el campo: no existe
    /// una acción de entrada de "ratón encima" que inyectar como se inyecta un botón de mando.
    /// </summary>
    private void HoverZoneHint()
    {
        var player = _selected >= 0 ? _state.Find(_selected) : null;
        if (player is not null)
        {
            foreach (string id in player.Perks)
            {
                if (_state.Catalog.Perks.Find(id) is { } perk
                    && ZoneHintText.Spans(DescriptionGenerator.Describe(perk, _state.Templates), _state.Templates) is { Count: > 0 } spans)
                {
                    OnCardZoneHint(spans[0].Kind, spans[0].Key);
                    return;
                }
            }
        }

        GD.PushWarning("el jugador señalado no nombra ninguna zona: la captura saldrá sin resaltado");
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
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].PlayerId == playerId)
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

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (F1 §6, ADR 0124): la plantilla de pruebas no arrastra
    /// ninguna run (<c>TeamState.Load</c>), así que sin esto la sección de carrera nunca aparecería en
    /// "equipo-ficha" -<c>RunCareer</c> solo vive en <c>RunPlayer</c>, que sin run no existe- y la captura
    /// no enseñaría lo que documenta el hito. Mismo apaño que <see cref="EnsurePlacementItem"/> con los
    /// objetos: una carrera de verdad (goles, entradas ganadas, una lesión y una muerte causadas, todas a
    /// la vez para que la línea se vea completa) en el mismo jugador que ya enfoca esta captura. No toca
    /// nada con una run detrás: ahí la carrera la acumula <c>MatchResolution</c>, no una captura.
    /// </summary>
    private void EnsureTestCareer(int playerId)
    {
        if (playerId < 0 || RunController.Instance is { HasRun: true })
        {
            return;
        }

        _state.ForceTestCareer(playerId, new RunCareer(
            Matches: 14,
            Goals: 3,
            Assists: 2,
            Tackles: 20,
            TacklesWon: 9,
            Fouls: 4,
            Cards: 1,
            InjuriesCaused: 2,
            DeathsCaused: 1,
            InjuriesSuffered: 1,
            TicksOnPitch: 12_000));
        RefreshCards();
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (CAT-B): la plantilla de pruebas no arrastra ninguna run
    /// (<c>TeamState.Load</c>), así que sin esto la sección de consumibles enseñaría el mensaje de
    /// inventario vacío y ninguna otra cosa. Mismo apaño que <see cref="EnsurePlacementItem"/> con los
    /// objetos: fuerza un catálogo cargado directo de <c>/data</c> y un inventario de verdad —dos vendajes
    /// (uno equipado como manual), una bengala equipada como condicional y un amuleto sin equipar— para
    /// que la captura enseñe las tres filas y el panel de acción a la vez. No toca nada con una run detrás.
    /// </summary>
    private void EnsureTestConsumables()
    {
        if (RunController.Instance is { HasRun: true })
        {
            return;
        }

        var catalog = ConsumableLoader.FromJson(GameData.Snapshot);
        var owned = new Dictionary<string, int>
        {
            ["field_bandage"] = 2,
            ["smoke_flare"] = 1,
            ["lucky_charm"] = 1,
        };
        var equipped = new[]
        {
            new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty),
            new EquippedConsumable("smoke_flare", ConsumableMode.Conditional, "scoreBehind"),
        };

        _state.ForceTestConsumables(catalog, owned, equipped);
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b> (ADR 0161): la plantilla de pruebas no arrastra ninguna
    /// run (<c>TeamState.Load</c>), así que sin esto el cofre enseñaría el mensaje de almacén vacío y
    /// ninguna otra cosa. Mismo apaño que <see cref="EnsureTestConsumables"/>: dos copias de un objeto
    /// común (para enseñar el agrupado "×2") y una reliquia, para que la captura enseñe una fila de cada
    /// clase y el panel de acción a la vez. No toca nada con una run detrás: ahí el almacén lo llena el
    /// bucle de run (botín, reliquia, herencia), no una captura.
    /// </summary>
    private void EnsureTestStoredItems()
    {
        if (RunController.Instance is { HasRun: true })
        {
            return;
        }

        var catalog = ItemLoader.FromJson(GameData.Snapshot);
        var items = new List<ItemDefinition>();
        if (catalog.Find("worn_boots") is { } common)
        {
            items.Add(common);
            items.Add(common);
        }

        if (catalog.Find("relic_scorer") is { } relic)
        {
            items.Add(relic);
        }

        _state.ForceTestStoredItems(items);
    }
}
