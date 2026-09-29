using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Game.Ui.Knavall;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Ui;

/// <summary>
/// <b>Cofre</b> de la pantalla de Equipo (ADR 0161 §3): el almacén de objetos sueltos —heredados de un
/// muerto (ADR 0048), botín de liga o reliquia (ADR 0161 §1, §2)— y los tres gestos sobre el jugador
/// señalado en la plantilla: <b>equipar</b> desde el cofre, <b>guardar</b> su objeto en el cofre y
/// <b>pasarlo</b> a otro jugador. Ninguno cuesta oro: todo lo que hay aquí ya estaba pagado.
///
/// <para>
/// <b>Lenguaje de Knavall (ADR 0162):</b> es la pestaña Cofre, el mismo cofre que la columna derecha de
/// Plantilla enseña en compacto, pero en grande y con el nombre de cada objeto. El "sobre quién actúa" sigue
/// sin vivir aquí —es el jugador señalado en la plantilla— y lo elegido del cofre tampoco: los dos los guarda
/// <c>TeamScreen</c>, que es quien aplica las tres decisiones, para que la ficha de Plantilla y esta pestaña
/// no puedan discrepar sobre qué está elegido.
/// </para>
/// </summary>
public partial class ChestPanel : InkCanvas
{
    private const float DetailWidth = 330f;

    private readonly List<PlaqueButton> _targets = new();
    private TeamState? _state;
    private int _playerId = -1;
    private string _pick = string.Empty;
    private bool _picking;
    private PlaqueButton _equip = null!;
    private PlaqueButton _store = null!;
    private PlaqueButton _pass = null!;

    /// <summary>Se ha pulsado un objeto del cofre.</summary>
    public event Action<string>? Picked;

    public event Action? EquipPressed;

    public event Action? StorePressed;

    public event Action? PassToggled;

    public event Action<int>? TransferTo;

    public override void _Ready()
    {
        base._Ready();
        _equip = PlaqueButton.Create(this, UiText.Get("ui.kn.equip"), Glyph.Shirt, PlaqueKind.Primary, new Rect2(), 31);
        _equip.Tip = new Tip(UiText.Get("ui.kn.equip"), UiText.Get("ui.kn.tip.equip"), Glyph.Shirt);
        _equip.Pressed += () => EquipPressed?.Invoke();
        _store = PlaqueButton.Create(this, UiText.Get("ui.kn.store").Replace("\n", " "), Glyph.Chest, PlaqueKind.Paper, new Rect2(), 32);
        _store.Tip = new Tip(UiText.Get("ui.kn.store").Replace("\n", " "), UiText.Get("ui.kn.tip.store"), Glyph.Chest);
        _store.FontSize = 17;
        _store.Pressed += () => StorePressed?.Invoke();
        _pass = PlaqueButton.Create(this, UiText.Get("ui.kn.pass"), Glyph.Swap, PlaqueKind.Paper, new Rect2(), 33);
        _pass.Tip = new Tip(UiText.Get("ui.kn.pass"), UiText.Get("ui.kn.tip.pass"), Glyph.Swap);
        _pass.Pressed += () => PassToggled?.Invoke();
        Arrange();
    }

    /// <summary>
    /// Reconstruye el panel con el jugador señalado (-1 si ninguno), el objeto elegido en el cofre (vacío si
    /// ninguno) y si está abierta la lista de destinatarios de PASAR A OTRO.
    /// </summary>
    public void Rebuild(TeamState state, int playerId, string pick, bool picking)
    {
        _state = state;
        _playerId = playerId;
        _pick = pick;
        _picking = picking && playerId >= 0 && state.EquippedItemOf(playerId) is not null;
        Arrange();
        RebuildTargets();
        QueueRedraw();
    }

    private void Arrange()
    {
        if (_equip is null)
        {
            return;
        }

        float left = Size.X - DetailWidth + 10f;
        float width = DetailWidth - 36f;
        float y = Size.Y - 196f;
        bool has = _state is not null && _playerId >= 0;
        var equipped = has ? _state!.EquippedItemOf(_playerId) : null;
        _equip.Position = new Vector2(left, y);
        _equip.Size = new Vector2(width, 56f);
        var pick = _pick.Length > 0 && _state is not null ? _state.Item(_pick) : null;
        _equip.Disabled = !has || pick is null;
        _equip.Glyph = pick is not null ? InkIcons.OfItem(pick.Id) : Glyph.Shirt;
        _store.Position = new Vector2(left, y + 62f);
        _store.Size = new Vector2(width, 52f);
        _store.Disabled = !has || equipped is null;
        _pass.Position = new Vector2(left, y + 120f);
        _pass.Size = new Vector2(width, 52f);
        _pass.Disabled = !has || equipped is null;
        _pass.Kind = _picking ? PlaqueKind.Tab : PlaqueKind.Paper;
        _pass.Active = _picking;
    }

    private Rect2 Main() => new(0f, 0f, Size.X - DetailWidth - 8f, Size.Y - 8f);

    private void RebuildTargets()
    {
        foreach (var button in _targets)
        {
            RemoveChild(button);
            button.QueueFree();
        }

        _targets.Clear();
        if (!_picking || _state is null)
        {
            return;
        }

        var area = Main().Grow(-24f);
        float width = (area.Size.X - 24f) / 3f;
        int index = 0;
        foreach (var candidate in _state.Players)
        {
            if (candidate.Id == _playerId || candidate.PhysicalState == PhysicalState.Dead)
            {
                continue;
            }

            var rect = new Rect2(area.Position.X + 12f + ((index % 3) * (width + 4f)), area.Position.Y + 110f + ((index / 3) * 52f), width, 48f);
            var button = PlaqueButton.Create(this, candidate.Name, Glyph.None, PlaqueKind.Paper, rect, 60 + index);
            button.FontSize = 17;
            int targetId = candidate.Id;
            button.Pressed += () => TransferTo?.Invoke(targetId);
            _targets.Add(button);
            index++;
        }
    }

    /// <summary>Solo para la secuencia de capturas: que el panel enseñe algo elegido.</summary>
    public void SelectForTest(string id) => Picked?.Invoke(id);

    public override void _Draw()
    {
        ClearZones();
        var main = Main();
        var side = new Rect2(Size.X - DetailWidth, 0f, DetailWidth - 8f, Size.Y - 8f);
        Ink.Sheet(this, main, 9191);
        Ink.Sheet(this, side, 9292, Ink.PaperWarm);
        if (_state is null)
        {
            return;
        }

        int total = _state.StoredItems.Count;
        Tiles.Header(this, new Vector2(18f, 12f), main.Size.X - 30f, Glyph.Chest, UiText.Get("ui.kn.chest"), total > 0 ? UiText.Get("ui.kn.chestCount", total) : string.Empty, 72f, 38);
        Zone(new Rect2(12f, 8f, main.Size.X - 24f, 80f), TeamTips.Chest());

        var groups = Inventory.Grouped(_state.StoredItems);
        float top = 104f;
        if (groups.Count == 0)
        {
            Tiles.Empty(this, new Rect2(22f, top, main.Size.X - 44f, 160f), string.Empty, 5);
            Ink.Text(this, Ink.Plain, new Vector2(30f, top + 176f), UiText.Get("ui.kn.chestEmpty"), 17, Ink.Muted);
        }

        int columns = groups.Count <= 10 ? 5 : groups.Count <= 18 ? 6 : 7;
        float gap = 12f;
        float tile = (main.Size.X - 44f - (gap * (columns - 1))) / columns;
        int rows = Mathf.Max(1, (groups.Count + columns - 1) / columns);
        float available = main.End.Y - top - 12f;
        if (rows * (tile + 34f + gap) > available)
        {
            tile = (available / rows) - 34f - gap;
        }

        // Con pocos objetos, el resto de las dos primeras filas se dibuja vacío: se lee como un cofre.
        if (groups.Count > 0)
        {
            for (int i = groups.Count; i < columns * 2; i++)
            {
                var slot = new Rect2(22f + ((i % columns) * (tile + gap)), top + ((i / columns) * (tile + 34f + gap)), tile, tile + 34f);
                Tiles.Empty(this, slot, string.Empty, 520 + i);
            }
        }

        for (int i = 0; i < groups.Count; i++)
        {
            var (id, count) = groups[i];
            if (_state.Item(id) is not { } item)
            {
                continue;
            }

            var rect = new Rect2(22f + ((i % columns) * (tile + gap)), top + ((i / columns) * (tile + 34f + gap)), tile, tile + 34f);
            string key = "item:" + id;
            Tiles.Draw(this, rect, new TileLook(InkIcons.OfItem(id), count, id == _pick, HoverKey == key, Relic: item.IsRelic, Caption: UiText.Name(item.Name)), 500 + i);
            string captured = id;
            Zone(rect, TeamTips.Item(_state, item), () => Picked?.Invoke(captured), key);
        }

        if (_picking)
        {
            var area = main.Grow(-24f);
            Ink.Plank(this, area, 123, Ink.WoodDark, nails: true);
            Ink.Outlined(this, Ink.Display, area.Position + new Vector2(24f, 30f), UiText.Get("ui.kn.passTo").ToUpperInvariant(), 30, Ink.Paper);
        }

        DrawDetail(side);
    }

    /// <summary>Columna derecha: para quién, qué lleva puesto y qué está elegido del cofre.</summary>
    private void DrawDetail(Rect2 side)
    {
        float left = side.Position.X + 18f;
        float width = side.Size.X - 36f;
        var player = _playerId >= 0 ? _state!.Find(_playerId) : null;
        if (player is null)
        {
            float y0 = side.Position.Y + 60f;
            foreach (string line in Style.Wrap(Ink.Display, UiText.Get("ui.kn.nobody").ToUpperInvariant(), 22, width))
            {
                Ink.Text(this, Ink.Display, new Vector2(left, y0), line, 22, Ink.Muted);
                y0 += 28f;
            }

            return;
        }

        // Para quién: retrato, nombre y lo que lleva.
        Ink.Text(this, Ink.Display, new Vector2(left, side.Position.Y + 14f), UiText.Get("ui.kn.for").ToUpperInvariant(), 18, Ink.RedDark);
        var portrait = new Rect2(left, side.Position.Y + 42f, 86f, 86f);
        Portrait.Draw(this, portrait, player.Race, player.Position, player.Id);
        Ink.LevelBadge(this, portrait.End - new Vector2(2f, 10f), 11f, player.Level);
        float textLeft = portrait.End.X + 16f;
        var words = player.Name.ToUpperInvariant().Split(' ', 2);
        float y = portrait.Position.Y;
        foreach (string word in words)
        {
            int size = Ink.FitSize(Ink.Display, word, 22, side.End.X - textLeft - 14f, 15);
            Ink.Text(this, Ink.Display, new Vector2(textLeft, y), word, size, Ink.Black);
            y += Ink.Display.GetHeight(size) - 6f;
        }

        var wears = _state!.EquippedItemOf(player.Id);
        var wearsRect = new Rect2(left, portrait.End.Y + 10f, 40f, 40f);
        Ink.Text(this, Ink.Heavy, new Vector2(left, wearsRect.Position.Y - 2f), string.Empty, 14, Ink.Muted);
        if (wears is not null)
        {
            Tiles.Draw(this, wearsRect, new TileLook(InkIcons.OfItem(wears.Id)), 77);
            Ink.Text(this, Ink.Data, new Vector2(wearsRect.End.X + 10f, wearsRect.Position.Y + 1f), UiText.Get("ui.kn.wears").ToUpperInvariant(), 13, Ink.Muted);
            Ink.Text(this, Ink.Heavy, new Vector2(wearsRect.End.X + 10f, wearsRect.Position.Y + 17f), Ink.Fit(Ink.Heavy, UiText.Name(wears.Name), 16, side.End.X - wearsRect.End.X - 24f), 16, Ink.Brown);
            Zone(wearsRect, TeamTips.Item(_state, wears));
        }
        else
        {
            Tiles.Empty(this, wearsRect, string.Empty, 78);
            Ink.Text(this, Ink.Heavy, new Vector2(wearsRect.End.X + 10f, wearsRect.Position.Y + 10f), UiText.Get("ui.kn.noItem").ToUpperInvariant(), 16, Ink.Muted);
            Zone(wearsRect, TeamTips.NoItem());
        }

        // Lo elegido del cofre, en grande.
        float top = portrait.End.Y + 70f;
        Ink.Text(this, Ink.Display, new Vector2(left, top), UiText.Get("ui.kn.selectedItem").ToUpperInvariant(), 18, Ink.RedDark);
        top += 30f;
        if (_pick.Length > 0 && _state.Item(_pick) is { } item)
        {
            var art = new Vector2(left + 50f, top + 48f);
            var splash = Broadcast.Pregon.Burst(46f, 36f, 11, art);
            DrawColoredPolygon(Ink.Shift(splash, new Vector2(3f, 3f)), new Color(0f, 0f, 0f, 0.25f));
            DrawColoredPolygon(splash, Ink.Ochre);
            InkIcons.Draw(this, InkIcons.OfItem(item.Id), art, 72f);
            float x = left + 110f;
            float ty = top;
            foreach (string line in Style.Wrap(Ink.Display, UiText.Name(item.Name).ToUpperInvariant(), 19, side.End.X - x - 14f))
            {
                Ink.Text(this, Ink.Display, new Vector2(x, ty), line, 19, Ink.Black);
                ty += 22f;
            }

            ty += 4f;
            foreach (var line in TeamTips.Modifiers(_state, item))
            {
                InkIcons.Draw(this, line.Glyph, new Vector2(x + 8f, ty + 9f), 16f);
                Ink.Text(this, Ink.Heavy, new Vector2(x + 22f, ty), line.Text, 15, line.Color == Ink.GreenLight ? Ink.Green : Ink.Red);
                ty += 19f;
            }

            Zone(new Rect2(left, top, width, 110f), TeamTips.Item(_state, item));
        }
        else
        {
            Tiles.Empty(this, new Rect2(left, top, width, 96f), UiText.Get("ui.kn.pickItem"), 13);
        }
    }
}
