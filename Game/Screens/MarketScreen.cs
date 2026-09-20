using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Placement;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Mercado</b> (RF-114..114f): las cuatro categorías con su precio, el oro disponible,
/// comprar, vender y los canteranos gratuitos destacados.
/// <para>
/// Encargo <c>mercado-arrastrar</c> (20 sep 2026, UI-001/UI-006/UI-010): el desplegable de "a quién se lo
/// doy" desaparece. Un perk o un objeto se compra <b>arrastrando</b> su carta (<see cref="OptionCard"/>,
/// modo grande) hasta el hueco libre de una ficha de la plantilla (<see cref="PlayerCard"/>, el mismo
/// componente de Equipo); un consumible se suelta sobre la <b>bolsa del equipo</b>
/// (<see cref="MarketBagSlot"/>), porque no se le da a nadie en concreto. Fichar a un jugador, vender y
/// ampliar plantilla siguen siendo un clic: no representan "dar un artículo a alguien", así que arrastrar
/// no les aporta nada.
/// </para>
/// <para>
/// Es la única tienda del juego y su surtido <b>no se renueva</b>, así que la pantalla lo dice en la
/// cabecera: lo que no se compre aquí no vuelve. Los canteranos van los primeros de su columna y con su
/// propio distintivo (RF-114b) porque son la red de seguridad de una run mala y solo la coge quien pasa
/// por el nodo.
/// </para>
/// <para>
/// Cada objeto enseña su <b>arquetipo</b> antes de comprarse (RF-012d): el maldito dice que baja algo, el
/// frágil dice su probabilidad de rotura y el exclusivo dice de qué raza es. Un objeto que rompe o resta
/// sin haberlo anunciado sería daño no telegrafiado.
/// </para>
/// <para>La pantalla no calcula nada (RT-014): el surtido, los precios y quién puede llevar cada artículo
/// los da <c>Sim.Run.View.MarketView</c>; el arrastre solo decide cómo se pide.</para>
/// </summary>
public partial class MarketScreen : Control
{
    private RunController _run = null!;
    private MarketScreenView _view = null!;
    private Data.TeamState _teamState = null!;
    private Label _error = null!;
    private Control _detailContainer = null!;
    private Label _reasonLabel = null!;
    private MarketBagSlot _bag = null!;

    private string _recruitCategory = string.Empty;
    private int _recruitIndex = -1;
    private int _sellPlayerId = -1;
    private int _expandedRosterId = -1;

    /// <summary>El artículo cogido (perk, objeto o consumible), o null si no se está arrastrando nada.</summary>
    private (string Category, int Index)? _held;

    /// <summary>Artículo bajo el ratón cuando nada está cogido (RF-012d: el detalle también se ve al pasar por encima).</summary>
    private (string Category, int Index)? _hoverOffer;

    /// <summary>Ficha de la plantilla bajo el ratón mientras se arrastra; -1 si ninguna.</summary>
    private int _hoverPlayerId = -1;

    /// <summary>La bolsa está bajo el ratón mientras se arrastra.</summary>
    private bool _hoverBag;

    /// <summary>Segundo flujo de entrada completo (UI-006): false mientras el cursor de mando recorre las
    /// tres columnas arrastrables, true mientras recorre la plantilla y la bolsa.</summary>
    private bool _padTargetsPane;

    private int _offerColumn;
    private int _offerIndex;

    /// <summary>Posición dentro de <see cref="RosterOrder"/>; el valor igual a su longitud es la bolsa.</summary>
    private int _targetIndex;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Route(this);
            return;
        }

        _run = run;
        Rebuild();
    }

    private void Rebuild()
    {
        var view = _run.Market();
        if (view is null)
        {
            Nav.Route(this);
            return;
        }

        _view = view;
        _teamState = Data.TeamState.FromRun(_run);
        Render();
    }

    private void Render()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        Build();
        RefreshDetailPanel();
        RefreshReason();
    }

    private void Build()
    {
        Layout.CenterLegacy(this);
        Widgets.Background(this);
        Widgets.Header(
            this,
            UiText.Get("ui.market.title"),
            UiText.Get("ui.market.subtitle", _view.Act, _view.Gold, _view.RosterSize, _view.RosterCapacity));

        const float top = 64f;
        const float rowHeight = 340f;

        RecruitColumn(16f, 205f, top, rowHeight);
        DraggableColumn(MarketCategories.Perk, 233f, 205f, top, rowHeight, UiText.Get("ui.market.perks"), _view.Perks);
        DraggableColumn(MarketCategories.Item, 450f, 205f, top, rowHeight, UiText.Get("ui.market.items"), _view.Items);
        DraggableColumn(MarketCategories.Consumable, 667f, 205f, top, rowHeight, UiText.Get("ui.market.consumables"), _view.Consumables);
        RosterPanel(892f, 372f, top, rowHeight);

        const float rowBTop = top + rowHeight + 12f;
        const float rowBHeight = 176f;
        DetailPanel(16f, rowBTop, 856f, rowBHeight);
        Sell(892f, rowBTop, 372f, rowBHeight);

        const float rowCTop = rowBTop + rowBHeight + 8f;
        RosterSlot(rowCTop);
        Bag(rowCTop + 32f);
        Widgets.Button(this, UiText.Get("ui.nav.team"), new Rect2(892f, rowCTop, 168f, 26f)).Pressed += ViewTeam;
        Widgets.Button(this, UiText.Get("ui.market.leave"), new Rect2(1088f, rowCTop, 176f, 26f)).Pressed += Leave;

        _error = Widgets.Body(this, string.Empty, new Vector2(12f, rowCTop + 88f), 1256f, Style.Hole);

        Widgets.InputHelp(this, UiText.Get("ui.input.mouseMarket"), UiText.Get("ui.input.padMarket"));
    }

    /// <summary>
    /// Columna de jugadores en venta (RF-114, RF-114b, RF-110..113): la única de las cuatro que sigue
    /// comprándose con un clic, porque fichar no es "dar un artículo a alguien" — es traer a alguien
    /// nuevo. El detalle y el botón de comprar viven en <see cref="DetailPanel"/>, igual que antes.
    /// </summary>
    private void RecruitColumn(float x, float width, float top, float height)
    {
        Widgets.Panel(this, new Rect2(x, top - 24f, width, height + 24f));
        Widgets.Section(this, UiText.Get("ui.market.players"), new Vector2(x + 8f, top - 20f), width - 16f);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(x + 4f, top + 2f),
            Size = new Vector2(width - 8f, height - 8f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width - 24f, 0f) };
        column.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(column);

        if (_view.Players.Count == 0)
        {
            AddEmptyLabel(column);
            return;
        }

        foreach (var row in _view.Players)
        {
            var card = new OptionCard();
            column.AddChild(card);
            card.Bind(row.Index, Badge(row), BadgeColor(row), row.Name, RightOf(row), row.Headline, row.Description, Notes(row), alwaysOpen: row.Youth);
            card.Selected = row.Category == _recruitCategory && row.Index == _recruitIndex;
            card.Dimmed = !row.Affordable || row.Block != RewardBlock.None;

            string category = row.Category;
            int index = row.Index;
            card.Activated += _ => SelectRecruit(category, index);
        }
    }

    /// <summary>
    /// Una columna de artículos que se compran arrastrándolos (perk, objeto o consumible): la misma carta
    /// grande que Recompensa, colapsada (badge, nombre, precio); el detalle completo vive en el panel de
    /// abajo, para que la columna quepa entera sin tener que expandir cada fila.
    /// </summary>
    private void DraggableColumn(string category, float x, float width, float top, float height, string title, IReadOnlyList<MarketRow> rows)
    {
        Widgets.Panel(this, new Rect2(x, top - 24f, width, height + 24f));
        Widgets.Section(this, title, new Vector2(x + 8f, top - 20f), width - 16f);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(x + 4f, top + 2f),
            Size = new Vector2(width - 8f, height - 8f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width - 24f, 0f) };
        column.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(column);

        if (rows.Count == 0)
        {
            AddEmptyLabel(column);
            return;
        }

        bool isOffersPane = !_padTargetsPane && _held is null;
        int columnSlot = ColumnOf(category);

        foreach (var raw in rows)
        {
            var row = raw.Category == MarketCategories.Perk ? WithRequirementHeadline(raw) : raw;
            var card = new OptionCard();
            column.AddChild(card);
            card.Bind(row.Index, Badge(row), BadgeColor(row), row.Name, RightOf(row), row.Headline, row.Description, Notes(row), large: true);

            bool isHeld = _held is { } held && held.Category == category && held.Index == row.Index;
            bool isFocused = isOffersPane && _offerColumn == columnSlot && _offerIndex == row.Index;
            card.Selected = isHeld || isFocused;
            card.Dimmed = !row.Affordable || row.Block != RewardBlock.None || (_held is not null && !isHeld);

            string capturedCategory = category;
            int index = row.Index;

            // Metadatos para que CaptureRunner localice esta carta por dato, no por pixel: dos columnas
            // pueden solaparse en X según el ancho lógico real (16:9 crece el área y Layout la centra,
            // docs/entorno.md), así que un rango de X a ciegas no es fiable.
            card.SetMeta("marketCategory", capturedCategory);
            card.SetMeta("marketIndex", index);

            card.Activated += _ => OnOfferPressed(capturedCategory, index);
            card.MouseEntered += () => OnOfferHoverEnter(capturedCategory, index);
            card.MouseExited += () => OnOfferHoverExit(capturedCategory, index);
        }
    }

    /// <summary>
    /// La plantilla entera, con retrato y huecos de perk/objeto visibles sin expandir (UI-010, UI-002): es
    /// el destino del arrastre. <see cref="PlayerCard"/> es exactamente el componente de Equipo — misma
    /// escena, mismo <c>Bind</c> — así que una ficha se lee igual en las dos pantallas.
    /// </summary>
    private void RosterPanel(float x, float width, float top, float height)
    {
        Widgets.Panel(this, new Rect2(x, top - 24f, width, height + 24f));
        Widgets.Section(this, UiText.Get("ui.team.roster"), new Vector2(x + 8f, top - 20f), width - 16f);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(x + 4f, top + 2f),
            Size = new Vector2(width - 8f, height - 8f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width - 24f, 0f) };
        column.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(column);

        var scene = GD.Load<PackedScene>("res://Scenes/PlayerCard.tscn");
        var links = PlacementView.Links(_teamState.Lineup);
        var order = RosterOrder();
        int starters = _teamState.Lineup.Slots.Count;

        AddRosterSectionLabel(column, UiText.Get("ui.team.starters"));
        for (int i = 0; i < starters && i < order.Count; i++)
        {
            AddRosterCard(column, scene, order[i], i, links);
        }

        AddRosterSectionLabel(column, UiText.Get("ui.team.bench"));
        for (int i = starters; i < order.Count; i++)
        {
            AddRosterCard(column, scene, order[i], i, links);
        }
    }

    private void AddRosterSectionLabel(VBoxContainer column, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        label.AddThemeColorOverride("font_color", Pregon.Wax);
        column.AddChild(label);
    }

    private void AddRosterCard(VBoxContainer column, PackedScene scene, int playerId, int position, IReadOnlyList<PlacementLink> links)
    {
        var player = _teamState.Find(playerId);
        if (player is null)
        {
            return;
        }

        var card = scene.Instantiate<PlayerCard>();
        column.AddChild(card);
        card.Bind(_teamState, player, LinksOf(playerId, links));
        card.Expanded = playerId == _expandedRosterId;

        bool isFocusedTarget = (_padTargetsPane && _targetIndex == position) || (_held is not null && playerId == _hoverPlayerId);
        card.Selected = playerId == _expandedRosterId || isFocusedTarget;

        if (_held is { } held && FindRow(held.Category, held.Index) is { } row)
        {
            bool valid = row.NeedsCarrier && ContainsCarrier(row, playerId);
            card.Drop = valid ? PlayerCard.DropHighlight.Valid : PlayerCard.DropHighlight.Invalid;
            card.TargetSlot = row.Category switch
            {
                MarketCategories.Perk => PlayerCard.DropSlotKind.Perk,
                MarketCategories.Item => PlayerCard.DropSlotKind.Item,
                _ => PlayerCard.DropSlotKind.None,
            };
        }

        card.Activated += _ => OnRosterCardPressed(playerId);
        card.MouseEntered += () => OnTargetHoverEnter(playerId);
        card.MouseExited += () => OnTargetHoverExit(playerId);
    }

    /// <summary>
    /// La bolsa del equipo (RF-114): destino de arrastre para un consumible, que no se le da a un jugador
    /// concreto — se equipa después, en Equipo (<see cref="ConsumablesPanel"/>).
    /// </summary>
    private void Bag(float y)
    {
        _bag = new MarketBagSlot { Position = new Vector2(16f, y), Size = new Vector2(332f, 50f) };
        AddChild(_bag);
        _bag.Bind(UiText.Get("ui.market.bagTitle"), UiText.Get("ui.market.bagHint"));

        bool bagFocused = (_padTargetsPane && _targetIndex == RosterOrder().Count) || (_held is not null && _hoverBag);
        _bag.Selected = bagFocused;
        if (_held is { } held && FindRow(held.Category, held.Index) is { } row)
        {
            _bag.Drop = row.Category == MarketCategories.Consumable ? MarketBagSlot.DropHighlight.Valid : MarketBagSlot.DropHighlight.Invalid;
        }

        _bag.Activated += OnBagPressed;
        _bag.MouseEntered += OnBagHoverEnter;
        _bag.MouseExited += OnBagHoverExit;
    }

    /// <summary>
    /// Panel de detalle del artículo elegido (RF-012d): lo que enseñaba "ARTÍCULO ELEGIDO" antes de este
    /// encargo, ahora alimentado por lo que se está cogiendo o por lo que el ratón o el cursor de mando
    /// están tocando, en vez de por una selección con clic. Se rellena aparte
    /// (<see cref="RefreshDetailPanel"/>) porque el ratón lo actualiza al pasar por encima sin volver a
    /// construir toda la pantalla.
    /// </summary>
    private void DetailPanel(float x, float y, float width, float height)
    {
        Widgets.Panel(this, new Rect2(x, y, width, height));
        Widgets.Section(this, UiText.Get("ui.market.action"), new Vector2(x + 12f, y + 6f), width - 24f);

        _detailContainer = new Control
        {
            Position = new Vector2(x + 12f, y + 26f),
            Size = new Vector2(width - 24f, height - 60f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_detailContainer);

        _reasonLabel = Widgets.Body(this, string.Empty, new Vector2(x + 12f, y + height - 20f), width - 24f, Style.Hole);
    }

    /// <summary>Venta de jugadores (RF-114f), sin cambios de flujo: sigue siendo un clic y un botón.</summary>
    private void Sell(float x, float y, float width, float height)
    {
        Widgets.Panel(this, new Rect2(x, y, width, height));
        Widgets.Section(this, UiText.Get("ui.market.sell"), new Vector2(x + 12f, y + 6f), width - 24f);

        float listTop = y + 26f;
        if (_view.LeavesBelowMinimum)
        {
            var warn = Widgets.Body(this, UiText.Get("ui.market.sellWarn", _view.AvailablePlayers), new Vector2(x + 12f, listTop), width - 24f, Style.Hole);
            listTop += warn.Size.Y + 4f;
        }

        var scroll = new ScrollContainer
        {
            Position = new Vector2(x + 8f, listTop),
            Size = new Vector2(width - 16f, y + height - 34f - listTop),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(width - 32f, 0f) };
        column.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(column);

        MarketSaleRow? selected = null;
        foreach (var player in _view.Sellable)
        {
            var card = new OptionCard();
            column.AddChild(card);
            card.Bind(
                player.PlayerId,
                UiText.Get("ui.pos." + player.Position),
                Style.Of(player.Position),
                UiText.Get("ui.market.sellRow", player.Name, player.Level, player.Perks),
                UiText.Get("ui.market.price", player.Price),
                string.Empty,
                string.Empty);
            card.Selected = player.PlayerId == _sellPlayerId;
            card.Dimmed = player.LastAvailable;
            int id = player.PlayerId;
            card.Activated += _ => SelectSell(id);

            if (player.PlayerId == _sellPlayerId)
            {
                selected = player;
            }
        }

        var button = Widgets.Button(
            this,
            selected is null ? UiText.Get("ui.market.sellNobody") : UiText.Get("ui.market.sellButton", selected.Name, selected.Price),
            new Rect2(x + 8f, y + height - 28f, width - 16f, 26f),
            selected is not null);
        button.Pressed += SellSelected;
    }

    /// <summary>El hueco de plantilla (ADR 0097), sin cambios de flujo: sigue siendo un clic y un botón.</summary>
    private void RosterSlot(float y)
    {
        var area = new Rect2(16f, y, 332f, 26f);
        if (_view.RosterSlotPrice < 0)
        {
            Widgets.Body(this, UiText.Get("ui.market.slotFull"), new Vector2(area.Position.X, area.Position.Y + 4f), area.Size.X, Style.TextDim);
            return;
        }

        bool affordable = _view.Gold >= _view.RosterSlotPrice;
        var button = Widgets.Button(
            this,
            UiText.Get("ui.market.slot", _view.RosterSlotPrice, _view.RosterSize, _view.RosterCapacity),
            area,
            affordable);
        button.Pressed += () => Decide(new ExpandRoster());
    }

    private static void AddEmptyLabel(VBoxContainer column)
    {
        var empty = new Label { Text = UiText.Get("ui.market.empty") };
        empty.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        empty.AddThemeColorOverride("font_color", Style.TextDim);
        column.AddChild(empty);
    }

    // ---- Recogida y suelta -------------------------------------------------------------------------

    /// <summary>Coger un artículo, cancelar el que ya se tenía cogido (al pulsarlo otra vez) o cambiarlo por otro.</summary>
    private void OnOfferPressed(string category, int index)
    {
        if (_held is { } current && current.Category == category && current.Index == index)
        {
            _held = null;
        }
        else
        {
            _held = (category, index);
            _padTargetsPane = true;
            _targetIndex = 0;
            _hoverPlayerId = -1;
            _hoverBag = false;
        }

        Render();
    }

    private void OnOfferHoverEnter(string category, int index)
    {
        if (_held is not null)
        {
            return;
        }

        _hoverOffer = (category, index);
        RefreshDetailPanel();
    }

    private void OnOfferHoverExit(string category, int index)
    {
        if (_hoverOffer is { } current && current.Category == category && current.Index == index)
        {
            _hoverOffer = null;
            RefreshDetailPanel();
        }
    }

    /// <summary>Soltar sobre una ficha (si se está arrastrando algo) o expandir/colapsar la ficha (si no).</summary>
    private void OnRosterCardPressed(int playerId)
    {
        if (_held is not null)
        {
            TryDrop(playerId, bag: false, cancelOnInvalid: true);
            return;
        }

        _expandedRosterId = _expandedRosterId == playerId ? -1 : playerId;
        Render();
    }

    private void OnTargetHoverEnter(int playerId)
    {
        _hoverPlayerId = playerId;
        _hoverBag = false;
        RefreshReason();
    }

    private void OnTargetHoverExit(int playerId)
    {
        if (_hoverPlayerId == playerId)
        {
            _hoverPlayerId = -1;
            RefreshReason();
        }
    }

    private void OnBagPressed()
    {
        if (_held is not null)
        {
            TryDrop(-1, bag: true, cancelOnInvalid: true);
        }
    }

    private void OnBagHoverEnter()
    {
        _hoverBag = true;
        _hoverPlayerId = -1;
        RefreshReason();
    }

    private void OnBagHoverExit()
    {
        if (_hoverBag)
        {
            _hoverBag = false;
            RefreshReason();
        }
    }

    /// <summary>
    /// Intenta cobrar el artículo cogido sobre el destino dado. Si no es válido, se dice el motivo
    /// (RF-012d: nunca un rechazo mudo) y, salvo que sea una confirmación de mando —que tiene su propio
    /// botón de cancelar (B)—, se suelta sin coste (misma regla que soltar fuera del todo).
    /// </summary>
    private void TryDrop(int playerId, bool bag, bool cancelOnInvalid)
    {
        if (_held is not { } held)
        {
            return;
        }

        var row = FindRow(held.Category, held.Index);
        if (row is null)
        {
            _held = null;
            Render();
            return;
        }

        bool valid = bag
            ? row.Category == MarketCategories.Consumable
            : row.NeedsCarrier && ContainsCarrier(row, playerId);

        if (!valid)
        {
            _reasonLabel.Text = Reason(row, playerId, bag);
            if (cancelOnInvalid)
            {
                _held = null;
                Render();
            }

            return;
        }

        _held = null;
        Decide(new BuyOffer(row.Category, row.Index, bag ? -1 : playerId));
    }

    private void SelectRecruit(string category, int index)
    {
        if (_recruitCategory == category && _recruitIndex == index)
        {
            _recruitCategory = string.Empty;
            _recruitIndex = -1;
        }
        else
        {
            _recruitCategory = category;
            _recruitIndex = index;
        }

        Render();
    }

    private void SelectSell(int playerId)
    {
        _sellPlayerId = _sellPlayerId == playerId ? -1 : playerId;
        Render();
    }

    private void BuyRecruit(string category, int index)
    {
        var row = FindRow(category, index);
        if (row is null)
        {
            return;
        }

        Decide(row.Category == MarketView.MercenaryCategory ? new HireMercenary(row.Index) : new BuyOffer(row.Category, row.Index));
    }

    private void SellSelected()
    {
        if (_sellPlayerId >= 0)
        {
            Decide(new SellPlayer(_sellPlayerId));
        }
    }

    // ---- Mando (UI-006) ----------------------------------------------------------------------------

    /// <summary>
    /// Segundo flujo de entrada completo, mismo patrón que Equipo: A coge/confirma, la cruceta mueve el
    /// cursor, B cancela. Solo cubre el arrastre (perk, objeto, consumible y sus destinos); fichar,
    /// vender y ampliar plantilla siguen siendo de ratón, como ya lo eran antes de este encargo.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_accept"))
        {
            Confirm();
            return;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            CancelOrCollapse();
            return;
        }

        int dx = @event.IsActionPressed("ui_right") ? 1 : @event.IsActionPressed("ui_left") ? -1 : 0;
        int dy = @event.IsActionPressed("ui_down") ? 1 : @event.IsActionPressed("ui_up") ? -1 : 0;
        if (dx != 0 || dy != 0)
        {
            MoveFocus(dx, dy);
            return;
        }

        // El ratón suelta donde esté en ese instante (arrastre de verdad, no clic a clic): soltar fuera de
        // un destino válido cancela sin coste (encargo mercado-arrastrar).
        if (_held is not null && @event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
        {
            if (_hoverBag)
            {
                TryDrop(-1, bag: true, cancelOnInvalid: true);
            }
            else if (_hoverPlayerId >= 0)
            {
                TryDrop(_hoverPlayerId, bag: false, cancelOnInvalid: true);
            }
            else
            {
                _held = null;
                Render();
            }
        }
    }

    private void Confirm()
    {
        if (!_padTargetsPane)
        {
            if (_held is not null)
            {
                return;
            }

            string category = ColumnCategory(_offerColumn);
            var rows = RowsOf(category);
            if (_offerIndex < rows.Count)
            {
                OnOfferPressed(category, rows[_offerIndex].Index);
            }

            return;
        }

        var order = RosterOrder();
        if (_held is not null)
        {
            if (_targetIndex == order.Count)
            {
                TryDrop(-1, bag: true, cancelOnInvalid: false);
            }
            else if (_targetIndex < order.Count)
            {
                TryDrop(order[_targetIndex], bag: false, cancelOnInvalid: false);
            }

            return;
        }

        if (_targetIndex < order.Count)
        {
            OnRosterCardPressed(order[_targetIndex]);
        }
    }

    private void CancelOrCollapse()
    {
        if (_held is not null)
        {
            _held = null;
            Render();
            return;
        }

        if (_expandedRosterId >= 0)
        {
            _expandedRosterId = -1;
            Render();
        }
    }

    /// <summary>
    /// Mientras nada está cogido, la cruceta recorre las tres columnas arrastrables (izquierda/derecha) y
    /// sus filas (arriba/abajo); al llegar al final de la última columna, pasa a la plantilla y la bolsa.
    /// Mientras algo está cogido, no se puede volver a las columnas —igual que Equipo bloquea la vuelta a
    /// la lista con un jugador en la mano—: hay que soltar o cancelar con B primero.
    /// </summary>
    private void MoveFocus(int dx, int dy)
    {
        if (!_padTargetsPane)
        {
            if (dx > 0)
            {
                if (_offerColumn < 2)
                {
                    _offerColumn++;
                    _offerIndex = 0;
                }
                else
                {
                    _padTargetsPane = true;
                    _targetIndex = 0;
                }
            }
            else if (dx < 0 && _offerColumn > 0)
            {
                _offerColumn--;
                _offerIndex = 0;
            }

            if (dy != 0)
            {
                int count = RowsOf(ColumnCategory(_offerColumn)).Count;
                _offerIndex = count == 0 ? 0 : Math.Clamp(_offerIndex + dy, 0, count - 1);
            }
        }
        else
        {
            if (dx < 0 && _held is null)
            {
                _padTargetsPane = false;
            }
            else if (dy != 0)
            {
                int count = RosterOrder().Count;
                _targetIndex = Math.Clamp(_targetIndex + dy, 0, count);
            }
        }

        Render();
    }

    // ---- Lecturas -----------------------------------------------------------------------------------

    /// <summary>
    /// Reconstruye solo el panel de detalle (RF-012d), sin repintar toda la pantalla: lo llama el ratón al
    /// pasar por encima de una carta, y no se puede tirar de <see cref="Render"/> ahí —destruiría la carta
    /// que Godot está a mitad de notificar como "el ratón ha entrado", con el riesgo de una notificación de
    /// salida encadenada sobre un nodo que ya no existe.
    /// </summary>
    private void RefreshDetailPanel()
    {
        foreach (var child in _detailContainer.GetChildren())
        {
            _detailContainer.RemoveChild(child);
            child.QueueFree();
        }

        (string Category, int Index)? current = _held ?? _hoverOffer ?? (_recruitIndex >= 0 ? (_recruitCategory, _recruitIndex) : null);
        float width = _detailContainer.Size.X;

        if (current is not { } picked)
        {
            Widgets.Body(_detailContainer, UiText.Get("ui.market.detailNone"), Vector2.Zero, width, Style.TextDim);
            return;
        }

        var row = FindRow(picked.Category, picked.Index);
        if (row is null)
        {
            return;
        }

        string prefix = _held is not null ? UiText.Get("ui.market.holding") + " " : string.Empty;
        Widgets.Body(_detailContainer, prefix + row.Name + " · " + RightOf(row), new Vector2(0f, 0f), width, Style.Accent);
        float y = 18f;

        if (row.Headline.Length > 0)
        {
            var headline = Widgets.Body(_detailContainer, row.Headline, new Vector2(0f, y), width, Style.TextDim);
            y += headline.Size.Y + 2f;
        }

        var description = Widgets.Body(_detailContainer, row.Description, new Vector2(0f, y), width);
        y += description.Size.Y + 4f;

        foreach (string note in Notes(row))
        {
            var label = Widgets.Body(_detailContainer, note, new Vector2(0f, y), width, Style.Accent);
            y += label.Size.Y + 3f;
        }

        string blocked = Blocked(row);
        if (blocked.Length > 0)
        {
            var label = Widgets.Body(_detailContainer, blocked, new Vector2(0f, y), width, Style.Hole);
            y += label.Size.Y + 3f;
        }

        bool isRecruit = row.Category is MarketCategories.Player or MarketCategories.Youth || row.Category == MarketView.MercenaryCategory;
        if (isRecruit)
        {
            var button = Widgets.Button(_detailContainer, BuyLabel(row), new Rect2(0f, y, 300f, 26f), CanBuyRecruit(row));
            string category = row.Category;
            int index = row.Index;
            button.Pressed += () => BuyRecruit(category, index);
        }
        else
        {
            Widgets.Body(
                _detailContainer,
                row.Category == MarketCategories.Consumable ? UiText.Get("ui.market.dragToBag") : UiText.Get("ui.market.dragToPlayer"),
                new Vector2(0f, y),
                width,
                Style.TextDim);
        }
    }

    /// <summary>Actualiza solo la línea de motivo (RF-012d): por qué el destino bajo el ratón acepta o no lo que se arrastra.</summary>
    private void RefreshReason()
    {
        if (_held is not { } held)
        {
            _reasonLabel.Text = string.Empty;
            return;
        }

        var row = FindRow(held.Category, held.Index);
        if (row is null)
        {
            _reasonLabel.Text = string.Empty;
            return;
        }

        if (_hoverBag)
        {
            _reasonLabel.Text = row.Category == MarketCategories.Consumable
                ? UiText.Get("ui.market.reasonOk")
                : UiText.Get("ui.market.reasonWrongBag");
        }
        else if (_hoverPlayerId >= 0)
        {
            bool valid = row.NeedsCarrier && ContainsCarrier(row, _hoverPlayerId);
            _reasonLabel.Text = valid ? UiText.Get("ui.market.reasonOk") : Reason(row, _hoverPlayerId, bag: false);
        }
        else
        {
            _reasonLabel.Text = row.Category == MarketCategories.Consumable
                ? UiText.Get("ui.market.dragToBag")
                : UiText.Get("ui.market.dragToPlayer");
        }
    }

    private string Reason(MarketRow row, int playerId, bool bag)
    {
        if (!row.Affordable)
        {
            return UiText.Get("ui.market.poor", row.Price, _view.Gold);
        }

        if (bag)
        {
            return row.Category == MarketCategories.Consumable ? UiText.Get("ui.market.reasonOk") : UiText.Get("ui.market.reasonWrongBag");
        }

        if (row.Category == MarketCategories.Consumable)
        {
            return UiText.Get("ui.market.reasonWrongPlayer");
        }

        if (row.Carriers.Count == 0)
        {
            return UiText.Get("ui.market.noCarrier");
        }

        string name = _teamState.Find(playerId)?.Name ?? "?";
        return row.Category == MarketCategories.Perk
            ? UiText.Get("ui.market.reasonPerkCarrier", name)
            : UiText.Get("ui.market.reasonItemCarrier", name);
    }

    private void Decide(RunDecision decision)
    {
        try
        {
            _run.Apply(decision);
        }
        catch (Exception error)
        {
            _error.Text = UiText.Get("ui.market.error", error.Message);
            return;
        }

        _sellPlayerId = -1;
        _held = null;
        _hoverOffer = null;
        _hoverPlayerId = -1;
        _hoverBag = false;
        Rebuild();
    }

    private void Leave()
    {
        if (_run.State is { Phase: RunPhase.NodeOpen, PendingNodeId: >= 0 })
        {
            _run.Apply(new LeaveNode());
        }

        Nav.Route(this);
    }

    /// <summary>Botón "Ver equipo" (AW-N): deja dicho el camino de vuelta y navega. El surtido y el oro
    /// no se pierden al volver: los dos viven en <c>RunState</c>, no en esta pantalla.</summary>
    private void ViewTeam()
    {
        Nav.ReturnTo = Nav.Market;
        Nav.Go(this, Nav.Team);
    }

    private static int ColumnOf(string category) => category switch
    {
        MarketCategories.Perk => 0,
        MarketCategories.Item => 1,
        _ => 2,
    };

    private static string ColumnCategory(int column) => column switch
    {
        0 => MarketCategories.Perk,
        1 => MarketCategories.Item,
        _ => MarketCategories.Consumable,
    };

    private IReadOnlyList<MarketRow> RowsOf(string category) => category switch
    {
        MarketCategories.Perk => _view.Perks,
        MarketCategories.Item => _view.Items,
        MarketCategories.Consumable => _view.Consumables,
        _ => _view.Players,
    };

    private MarketRow? FindRow(string category, int index)
    {
        foreach (var row in RowsOf(category))
        {
            if (row.Category == category && row.Index == index)
            {
                return row.Category == MarketCategories.Perk ? WithRequirementHeadline(row) : row;
            }
        }

        return null;
    }

    private static bool ContainsCarrier(MarketRow row, int playerId)
    {
        foreach (var carrier in row.Carriers)
        {
            if (carrier.PlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanBuyRecruit(MarketRow row) => row.Affordable && row.Block == RewardBlock.None;

    /// <summary>Titulares primero (de portería a ataque, como se lee el campo) y luego suplentes; misma orden que Equipo.</summary>
    private List<int> RosterOrder()
    {
        var starters = new List<(Cell Cell, int Id)>();
        foreach (var slot in _teamState.Lineup.Slots)
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

        foreach (var player in _teamState.Players)
        {
            if (!_teamState.IsStarter(player.Id))
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

            var other = _teamState.Find(link.ToPlayerId);
            if (other is not null)
            {
                result.Add(UiText.Get("ui.team.linkOf", RelationName(link.Relation), other.Name));
            }
        }

        return result;
    }

    private string RelationName(LinkRelation relation)
    {
        string name = relation.ToString();
        string key = char.ToLowerInvariant(name[0]) + name[1..];
        return _teamState.Templates.Get("links", key);
    }

    /// <summary>
    /// Cuántos de la plantilla llevan ya la etiqueta que pide el perk (RF-012d, BB-J): antes de comprarlo,
    /// no solo tras leer su descripción. La fila de perk llega con <c>Headline</c> vacío
    /// (<c>MarketView.Build</c>), así que se rellena aquí sin tocar <c>/Sim</c>; con varios requisitos se
    /// unen con " · ", igual que el resto de líneas apiladas de esta pantalla.
    /// </summary>
    private MarketRow WithRequirementHeadline(MarketRow row)
    {
        var perk = _run.Catalog!.Perks.Find(row.Id);
        if (perk is null)
        {
            return row;
        }

        var requirements = PerkSquadRequirements.For(perk, Squad());
        if (requirements.Count == 0)
        {
            return row;
        }

        var templates = _run.Catalog.Localization.Get(Data.GameData.Language);
        var lines = new List<string>(requirements.Count);
        foreach (var requirement in requirements)
        {
            lines.Add(UiText.Get(
                "ui.card.perkRequirement",
                templates.Get("tags", requirement.Tag),
                requirement.Current,
                requirement.Required));
        }

        return row with { Headline = string.Join(" · ", lines) };
    }

    /// <summary>
    /// Plantilla entera convertida a <see cref="PlayerDefinition"/>, igual conversión que
    /// <c>TeamState.FromRun</c>: sin excluir a nadie, porque en el Mercado el perk todavía no tiene
    /// portador (<see cref="PerkSquadRequirements"/>).
    /// </summary>
    private List<PlayerDefinition> Squad()
    {
        var catalog = _run.Catalog!;
        var players = new List<PlayerDefinition>(_run.State!.Roster.Count);
        foreach (var slot in _run.State.Roster)
        {
            players.Add(slot.ToDefinition(catalog));
        }

        return players;
    }

    /// <summary>Lo que va a la derecha de la tira: el precio, o el salario si es un mercenario (RF-111).</summary>
    private static string RightOf(MarketRow row)
    {
        if (row.Mercenary)
        {
            return UiText.Get("ui.market.wage", row.Wage);
        }

        return row.Free ? UiText.Get("ui.market.free") : UiText.Get("ui.market.price", row.Price);
    }

    private static string BuyLabel(MarketRow row)
    {
        if (row.Mercenary)
        {
            return UiText.Get("ui.market.hire", row.Wage);
        }

        return row.Free ? UiText.Get("ui.market.take") : UiText.Get("ui.market.buy", row.Price);
    }

    private string Blocked(MarketRow row)
    {
        if (row.Block == RewardBlock.RosterFull)
        {
            return UiText.Get("ui.market.rosterFull", _view.RosterSize, _view.RosterCapacity);
        }

        if (row.Block == RewardBlock.NoCarrier)
        {
            return UiText.Get("ui.market.noCarrier");
        }

        return row.Affordable ? string.Empty : UiText.Get("ui.market.poor", row.Price, _view.Gold);
    }

    /// <summary>
    /// Lo que hay que saber del artículo antes de comprarlo y que no cabe en su descripción: el
    /// arquetipo del objeto (RF-077) y las dos condiciones de los jugadores gratuitos.
    /// </summary>
    private static IReadOnlyList<string> Notes(MarketRow row)
    {
        var notes = new List<string>();
        if (row.Youth)
        {
            notes.Add(UiText.Get("ui.market.youthHint"));
        }

        if (row.Mercenary)
        {
            notes.Add(UiText.Get("ui.market.mercenaryHint", row.Wage));
        }

        switch (row.Archetype)
        {
            case ItemArchetype.Cursed:
                notes.Add(UiText.Get("ui.market.cursedHint"));
                break;
            case ItemArchetype.Fragile:
                notes.Add(UiText.Get("ui.market.fragileHint", row.BreakChancePercent));
                break;
            case ItemArchetype.Restricted:
                notes.Add(UiText.Get("ui.market.restrictedHint", row.RaceRestriction));
                break;
            default:
                break;
        }

        return notes;
    }

    private static string Badge(MarketRow row)
    {
        if (row.Youth)
        {
            return UiText.Get("ui.market.badgeYouth");
        }

        if (row.Mercenary)
        {
            return UiText.Get("ui.market.badgeMercenary");
        }

        return row.Category switch
        {
            MarketCategories.Player => UiText.Get("ui.market.badgePlayer"),
            MarketCategories.Perk => UiText.Get("ui.market.badgePerk"),
            MarketCategories.Item => UiText.Get("ui.market.badgeItem"),
            _ => UiText.Get("ui.market.badgeConsumable"),
        };
    }

    private static Color BadgeColor(MarketRow row)
    {
        if (row.Youth)
        {
            return Style.LinkCreated;
        }

        if (row.Mercenary)
        {
            return Style.Of(Sim.Model.Position.Forward);
        }

        return row.Category switch
        {
            MarketCategories.Player => Style.Of(Sim.Model.Position.Midfielder),
            MarketCategories.Perk => Style.Accent,
            MarketCategories.Item => Style.Of(Sim.Model.Position.Defender),

            // Style.LinkLine es un gris translúcido pensado para dibujarse sobre el césped de PitchView,
            // no como una placa opaca de distintivo sobre el pergamino de OptionCard: se lavaba casi
            // invisible ahí. Style.NeutralBadge es el mismo papel, opaco.
            _ => Style.NeutralBadge,
        };
    }
}
