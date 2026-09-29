using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui.Knavall;

/// <summary>Lecturas del inventario que comparten el cofre y los consumibles, en compacto y en grande.</summary>
public static class Inventory
{
    /// <summary>
    /// Agrupa el almacén (una entrada por copia, ya ordenado por id, RT-041) en pares (id, copias): «tres
    /// botas», no tres filas iguales.
    /// </summary>
    public static List<(string Id, int Count)> Grouped(IReadOnlyList<string> stored)
    {
        var result = new List<(string, int)>();
        string last = string.Empty;
        int count = 0;
        foreach (string id in stored)
        {
            if (id == last)
            {
                count++;
                continue;
            }

            if (count > 0)
            {
                result.Add((last, count));
            }

            last = id;
            count = 1;
        }

        if (count > 0)
        {
            result.Add((last, count));
        }

        return result;
    }

    /// <summary>
    /// Consumibles que enseñar: el inventario (uno por id) más los equipados aunque no queden copias —para
    /// poder quitarlos—, en orden ordinal (RT-041).
    /// </summary>
    public static List<string> ConsumableIds(TeamState state)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string id in state.OwnedConsumables)
        {
            set.Add(id);
        }

        foreach (var equipped in state.EquippedConsumables)
        {
            set.Add(equipped.Id);
        }

        return new List<string>(set);
    }

    public static EquippedConsumable? Equipped(TeamState state, string id)
    {
        foreach (var item in state.EquippedConsumables)
        {
            if (item.Id == id)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Marca de la esquina de un consumible equipado: mano (manual) o reloj de arena (condicional).</summary>
    public static Glyph Marker(EquippedConsumable? equipped) => equipped switch
    {
        null => Glyph.None,
        { Mode: ConsumableMode.Manual } => Glyph.Manual,
        _ => Glyph.Conditional,
    };
}

/// <summary>
/// Cofre en compacto, para la columna derecha de la pestaña Plantilla (ADR 0162): cabecera con el cofre
/// dibujado y el recuento, y una rejilla de casillas grandes. Pulsar una casilla la elige para EQUIPAR en la
/// ficha del centro; si hay más objetos de los que caben, la última casilla lleva a la pestaña Cofre.
/// </summary>
public partial class ChestView : InkCanvas
{
    private const int Columns = 4;
    private TeamState? _state;
    private string _pick = string.Empty;

    /// <summary>Se ha pulsado un objeto (su id).</summary>
    public event Action<string>? Picked;

    /// <summary>Se ha pulsado «ver todo»: la casilla de más.</summary>
    public event Action? MorePressed;

    public void Bind(TeamState state, string pick)
    {
        _state = state;
        _pick = pick;
        QueueRedraw();
    }

    public override void _Draw()
    {
        ClearZones();
        var sheet = new Rect2(Vector2.Zero, Size - new Vector2(8f, 8f));
        Ink.Sheet(this, sheet, 6161);
        if (_state is null)
        {
            return;
        }

        var groups = Inventory.Grouped(_state.StoredItems);
        int total = _state.StoredItems.Count;
        Tiles.Header(this, new Vector2(14f, 10f), sheet.Size.X - 24f, Glyph.Chest, UiText.Get("ui.kn.chest"), total > 0 ? UiText.Get("ui.kn.chestCount", total) : string.Empty, 62f, 32);
        Zone(new Rect2(10f, 6f, sheet.Size.X - 20f, 70f), TeamTips.Chest());

        float gap = 8f;
        float side = (sheet.Size.X - 28f - (gap * (Columns - 1))) / Columns;
        float top = 88f;
        int rows = Mathf.Max(1, (int)((sheet.Size.Y - top - 8f) / (side + gap)));
        int capacity = rows * Columns;

        if (groups.Count == 0)
        {
            Tiles.Empty(this, new Rect2(14f, top, sheet.Size.X - 28f, side), string.Empty, 3);
            Ink.Text(this, Ink.Plain, new Vector2(24f, top + side + 8f), Ink.Fit(Ink.Plain, UiText.Get("ui.kn.chestEmpty"), 15, sheet.Size.X - 48f), 15, Ink.Muted);
            return;
        }

        bool overflow = groups.Count > capacity;
        int shown = overflow ? capacity - 1 : groups.Count;

        // Los huecos que quedan se dibujan vacíos: se lee como un inventario, no como una lista.
        for (int i = shown; i < capacity; i++)
        {
            var slot = new Rect2(14f + ((i % Columns) * (side + gap)), top + ((i / Columns) * (side + gap)), side, side);
            Tiles.Empty(this, slot, string.Empty, 60 + i);
        }
        for (int i = 0; i < shown; i++)
        {
            var (id, count) = groups[i];
            var item = _state.Item(id);
            if (item is null)
            {
                continue;
            }

            var rect = new Rect2(14f + ((i % Columns) * (side + gap)), top + ((i / Columns) * (side + gap)), side, side);
            string key = "item:" + id;
            Tiles.Draw(this, rect, new TileLook(InkIcons.OfItem(id), count, id == _pick, HoverKey == key, Relic: item.IsRelic), i + 50);
            string captured = id;
            Zone(rect, TeamTips.Item(_state, item), () => Picked?.Invoke(captured), key);
        }

        if (overflow)
        {
            int i = capacity - 1;
            var rect = new Rect2(14f + ((i % Columns) * (side + gap)), top + ((i / Columns) * (side + gap)), side, side);
            Ink.Slab(this, rect, HoverKey == "more" ? Ink.Paper : Ink.PaperWarm, 90, 1.2f, 2.6f, new Vector2(3f, 4f));
            string text = "+" + (groups.Count - shown).ToString(System.Globalization.CultureInfo.InvariantCulture);
            float width = Ink.Width(Ink.Display, text, 30);
            Ink.Text(this, Ink.Display, rect.GetCenter() - new Vector2(width / 2f, 20f), text, 30, Ink.Brown);
            Zone(rect, TeamTips.Chest(), () => MorePressed?.Invoke(), "more");
        }
    }
}

/// <summary>
/// Consumibles en compacto, bajo el cofre en la pestaña Plantilla: lo que va al partido y lo que queda en el
/// zurrón, con su recuento y su marca de manual o condicional. Pulsar uno abre la pestaña Consumibles con él
/// elegido: equipar tiene reglas (tres como mucho, uno manual) que merecen su pantalla.
/// </summary>
public partial class ConsumablesView : InkCanvas
{
    private const int Columns = 4;
    private TeamState? _state;

    /// <summary>Se ha pulsado un consumible (su id).</summary>
    public event Action<string>? Picked;

    public void Bind(TeamState state)
    {
        _state = state;
        QueueRedraw();
    }

    public override void _Draw()
    {
        ClearZones();
        var sheet = new Rect2(Vector2.Zero, Size - new Vector2(8f, 8f));
        Ink.Sheet(this, sheet, 8080);
        if (_state is null)
        {
            return;
        }

        var ids = Inventory.ConsumableIds(_state);
        Tiles.Header(this, new Vector2(14f, 8f), sheet.Size.X - 24f, Glyph.Potion, UiText.Get("ui.kn.consumables"), UiText.Get("ui.kn.consumablesCount", _state.EquippedConsumables.Count), 46f, 26);
        Zone(new Rect2(10f, 4f, sheet.Size.X - 20f, 54f), TeamTips.Consumables());

        float gap = 8f;
        float side = (sheet.Size.X - 28f - (gap * (Columns - 1))) / Columns;
        float top = 66f;
        if (ids.Count == 0)
        {
            Tiles.Empty(this, new Rect2(14f, top, sheet.Size.X - 28f, side), string.Empty, 4);
            Ink.Text(this, Ink.Plain, new Vector2(24f, top + side + 8f), Ink.Fit(Ink.Plain, UiText.Get("ui.kn.consumablesEmpty"), 15, sheet.Size.X - 48f), 15, Ink.Muted);
            return;
        }

        int rows = Mathf.Max(1, (int)((sheet.Size.Y - top - 8f) / (side + gap)));
        int shown = Math.Min(ids.Count, rows * Columns);
        for (int i = 0; i < shown; i++)
        {
            string id = ids[i];
            var definition = _state.Consumable(id);
            if (definition is null)
            {
                continue;
            }

            var equipped = Inventory.Equipped(_state, id);
            int owned = _state.ConsumablesOwned(id);
            var rect = new Rect2(14f + ((i % Columns) * (side + gap)), top + ((i / Columns) * (side + gap)), side, side);
            string key = "cons:" + id;
            Tiles.Draw(this, rect, new TileLook(InkIcons.Of(definition.Family), owned, false, HoverKey == key, owned <= 0 && equipped is null, Inventory.Marker(equipped)), i + 70);
            string captured = id;
            Zone(rect, TeamTips.Consumable(_state, definition, equipped, owned, equipped is null ? string.Empty : ConsumablesPanel.TriggerName(equipped.Trigger)), () => Picked?.Invoke(captured), key);
        }
    }
}
