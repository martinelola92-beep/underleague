using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Ui;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Market;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Mercado</b> (RF-114..114f): las cuatro categorías con su precio, el oro disponible,
/// comprar, vender y los canteranos gratuitos destacados.
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
/// <para>La pantalla no calcula nada (RT-014): el surtido y los precios los da <c>Sim.Run.View.MarketView</c>.</para>
/// </summary>
public partial class MarketScreen : Control
{
    private const float ColumnWidth = 296f;
    private const float ColumnTop = 80f;
    private const float ColumnHeight = 390f;

    private readonly List<OptionCard> _cards = new();
    private readonly List<OptionCard> _sellCards = new();

    private RunController _run = null!;
    private MarketScreenView _view = null!;
    private OptionButton? _carrier;
    private Label _error = null!;
    private string _selectedCategory = string.Empty;
    private int _selectedIndex = -1;
    private int _sellPlayerId = -1;

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
        Render();
    }

    private void Render()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _cards.Clear();
        _sellCards.Clear();
        _carrier = null;
        Build();
    }

    private void Build()
    {
        Widgets.Background(this);
        Widgets.Header(
            this,
            UiText.Get("ui.market.title"),
            UiText.Get("ui.market.subtitle", _view.Act, _view.Gold, _view.RosterSize, _view.RosterCapacity));

        Column(0, UiText.Get("ui.market.players"), _view.Players);
        Column(1, UiText.Get("ui.market.perks"), _view.Perks);
        Column(2, UiText.Get("ui.market.items"), _view.Items);
        Column(3, UiText.Get("ui.market.consumables"), _view.Consumables);

        Action();
        Sell();

        Widgets.Button(this, UiText.Get("ui.market.leave"), new Rect2(1076f, 706f, 180f, 26f)).Pressed += Leave;
        _error = Widgets.Body(this, string.Empty, new Vector2(12f, 736f), 1040f, Style.Hole);
        Widgets.InputHelp(this, UiText.Get("ui.input.mouseMarket"), UiText.Get("ui.input.padPending"));
    }

    /// <summary>Una de las cuatro columnas de RF-114. Las tiras son de 24 px, como en todas las listas.</summary>
    private void Column(int index, string title, IReadOnlyList<MarketRow> rows)
    {
        float x = 16f + (index * (ColumnWidth + 12f));
        Widgets.Panel(this, new Rect2(x, ColumnTop - 24f, ColumnWidth, ColumnHeight + 24f));
        Widgets.Section(this, title, new Vector2(x + 8f, ColumnTop - 20f), ColumnWidth - 16f);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(x + 4f, ColumnTop + 2f),
            Size = new Vector2(ColumnWidth - 8f, ColumnHeight - 8f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth - 24f, 0f) };
        column.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(column);

        if (rows.Count == 0)
        {
            var empty = new Label { Text = UiText.Get("ui.market.empty") };
            empty.AddThemeFontSizeOverride("font_size", Style.TextSmall);
            empty.AddThemeColorOverride("font_color", Style.TextDim);
            column.AddChild(empty);
            return;
        }

        foreach (var row in rows)
        {
            var card = new OptionCard();
            column.AddChild(card);
            bool selected = row.Category == _selectedCategory && row.Index == _selectedIndex;
            card.Bind(
                row.Index,
                Badge(row),
                BadgeColor(row),
                row.Name,
                RightOf(row),
                row.Headline,
                row.Description,
                Notes(row),

                // El canterano gratuito se enseña abierto (RF-114b): es la red de seguridad de una run
                // mala y solo la coge quien pasa por el nodo, así que no puede depender de un clic.
                alwaysOpen: row.Youth);
            card.Expanded = selected;
            card.Selected = selected;
            card.Dimmed = !row.Affordable || row.Block != RewardBlock.None;

            string category = row.Category;
            int rowIndex = row.Index;
            card.Activated += _ => Select(category, rowIndex);
            _cards.Add(card);
        }
    }

    /// <summary>Panel de compra: el artículo elegido, a quién se le da y el botón que ejecuta la decisión.</summary>
    private void Action()
    {
        Widgets.Panel(this, new Rect2(12f, 482f, 740f, 218f));
        Widgets.Section(this, UiText.Get("ui.market.action"), new Vector2(24f, 488f), 700f);

        var row = Selected();
        if (row is null)
        {
            Widgets.Body(this, UiText.Get("ui.market.nothing"), new Vector2(24f, 510f), 716f, Style.TextDim);
            return;
        }

        Widgets.Body(
            this,
            row.Name + " · " + RightOf(row),
            new Vector2(24f, 510f),
            716f,
            Style.Accent);
        Widgets.Body(this, row.Headline, new Vector2(24f, 528f), 716f, Style.TextDim);

        // El texto se apila midiendo cada bloque, no con posiciones fijas: una descripción de cuatro
        // líneas y una de una tienen que caber igual de bien.
        var description = Widgets.Body(this, row.Description, new Vector2(24f, 546f), 716f);
        float y = 546f + description.Size.Y + 6f;
        foreach (string note in Notes(row))
        {
            var label = Widgets.Body(this, note, new Vector2(24f, y), 716f, Style.Accent);
            y += label.Size.Y + 4f;
        }

        string blocked = Blocked(row);
        if (blocked.Length > 0 && y < 640f)
        {
            Widgets.Body(this, blocked, new Vector2(24f, y), 716f, Style.Hole);
        }

        if (row.NeedsCarrier)
        {
            Widgets.Body(this, UiText.Get("ui.market.carrier"), new Vector2(24f, 646f), 120f, Style.TextDim);
            _carrier = new OptionButton
            {
                Position = new Vector2(24f, 664f),
                Size = new Vector2(330f, 26f),
                ClipText = true,
            };
            _carrier.AddThemeFontSizeOverride("font_size", Style.TextSmall);
            AddChild(_carrier);

            foreach (var carrier in row.Carriers)
            {
                _carrier.AddItem(UiText.Get(
                    "ui.reward.carrierRow",
                    carrier.Name,
                    UiText.Get("ui.pos." + carrier.Position),
                    carrier.Level,
                    UiText.Get("ui.state." + carrier.PhysicalState),
                    row.Category == MarketCategories.Perk
                        ? UiText.Get(carrier.FreeSlots == 1 ? "ui.reward.slot" : "ui.reward.slots", carrier.FreeSlots)
                        : carrier.CurrentItemId is not null
                            ? UiText.Get("ui.reward.carrierHasItem", carrier.CurrentItemName)
                            : UiText.Get("ui.reward.carrierNoItem")));
            }
        }

        var button = Widgets.Button(this, BuyLabel(row), new Rect2(372f, 664f, 356f, 26f), CanBuy(row));
        button.Pressed += Buy;
    }

    /// <summary>Venta de jugadores (RF-114f) con el precio que se va a cobrar, no una estimación.</summary>
    private void Sell()
    {
        Widgets.Panel(this, new Rect2(764f, 482f, 504f, 218f));
        Widgets.Section(this, UiText.Get("ui.market.sell"), new Vector2(776f, 488f), 480f);

        if (_view.LeavesBelowMinimum)
        {
            Widgets.Body(this, UiText.Get("ui.market.sellWarn", _view.AvailablePlayers), new Vector2(776f, 506f), 480f, Style.Hole);
        }

        var scroll = new ScrollContainer
        {
            Position = new Vector2(772f, 526f),
            Size = new Vector2(488f, 110f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(468f, 0f) };
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
            card.Activated += _ =>
            {
                _sellPlayerId = _sellPlayerId == id ? -1 : id;
                Render();
            };
            _sellCards.Add(card);

            if (player.PlayerId == _sellPlayerId)
            {
                selected = player;
            }
        }

        var button = Widgets.Button(
            this,
            selected is null
                ? UiText.Get("ui.market.sellNobody")
                : UiText.Get("ui.market.sellButton", selected.Name, selected.Price),
            new Rect2(776f, 648f, 480f, 26f),
            selected is not null);
        button.Pressed += SellSelected;
    }

    private void Select(string category, int index)
    {
        if (_selectedCategory == category && _selectedIndex == index)
        {
            _selectedCategory = string.Empty;
            _selectedIndex = -1;
        }
        else
        {
            _selectedCategory = category;
            _selectedIndex = index;
        }

        Render();
    }

    private MarketRow? Selected()
    {
        foreach (var list in new[] { _view.Players, _view.Perks, _view.Items, _view.Consumables })
        {
            foreach (var row in list)
            {
                if (row.Category == _selectedCategory && row.Index == _selectedIndex)
                {
                    return row;
                }
            }
        }

        return null;
    }

    private void Buy()
    {
        var row = Selected();
        if (row is null)
        {
            return;
        }

        int carrierId = -1;
        if (row.NeedsCarrier && _carrier is not null && _carrier.Selected >= 0 && _carrier.Selected < row.Carriers.Count)
        {
            carrierId = row.Carriers[_carrier.Selected].PlayerId;
        }

        Decide(row.Category == MarketView.MercenaryCategory
            ? new HireMercenary(row.Index)
            : new BuyOffer(row.Category, row.Index, carrierId));
    }

    private void SellSelected()
    {
        if (_sellPlayerId >= 0)
        {
            Decide(new SellPlayer(_sellPlayerId));
        }
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

    private bool CanBuy(MarketRow row) =>
        row.Affordable
        && row.Block == RewardBlock.None
        && (!row.NeedsCarrier || row.Carriers.Count > 0);

    /// <summary>Lo que va a la derecha de la tira: el precio, o el salario si es un mercenario (RF-111).</summary>
    private static string RightOf(MarketRow row)
    {
        if (row.Mercenary)
        {
            return UiText.Get("ui.market.wage", row.Wage);
        }

        return row.Free ? UiText.Get("ui.market.free") : UiText.Get("ui.market.price", row.Price);
    }

    private string BuyLabel(MarketRow row)
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
            _ => Style.LinkLine,
        };
    }
}
