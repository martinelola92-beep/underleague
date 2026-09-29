using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Game.Ui;

/// <summary>
/// <b>Cofre</b> de la pantalla de Equipo (ADR 0161 §3): el almacén de objetos sueltos —heredados de un
/// muerto (ADR 0048), botín de liga o reliquia (ADR 0161 §1, §2)— y los tres gestos sobre el jugador
/// señalado en la plantilla: <b>equipar</b> desde el cofre, <b>guardar</b> su objeto en el cofre y
/// <b>pasarlo</b> a otro jugador. Ninguno cuesta oro: todo lo que hay aquí ya estaba pagado.
///
/// <para>
/// Mismo patrón que <see cref="ConsumablesPanel"/>: un panel de campo completo que sustituye a la
/// cuadrícula mientras está encendido (<c>TeamScreen.ApplyFieldMode</c>), solo de ratón por ahora. La
/// diferencia es que el "sobre quién actúa" no vive en este panel —lo decide la ficha señalada en la
/// plantilla de <c>TeamScreen</c>, el mismo <c>_selected</c> que ya usa la ficha expandida—, así que
/// <see cref="Rebuild"/> recibe el id del jugador en cada llamada, incluida cada vez que cambia la
/// selección mientras el cofre está abierto.
/// </para>
/// </summary>
public partial class ChestPanel : Control
{
    /// <summary>Ancho fijo del panel: el que le deja <c>TeamScreen</c> dentro del panel de campo.</summary>
    public const float Width = 846f;

    /// <summary>Alto fijo del panel.</summary>
    public const float Height = 676f;

    private TeamState? _state;
    private int _playerId = -1;
    private string _selectedItemId = string.Empty;
    private bool _pickingTarget;
    private string _error = string.Empty;

    /// <summary>Reconstruye el panel entero con el estado actual y el jugador señalado (-1 si ninguno).</summary>
    public void Rebuild(TeamState state, int playerId)
    {
        _state = state;
        _playerId = playerId;
        _error = string.Empty;
        Render();
    }

    /// <summary>Solo para la secuencia de capturas: preselecciona una fila del almacén.</summary>
    public void SelectForTest(string id)
    {
        _selectedItemId = id;
        Render();
    }

    private void Render()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        if (_state is null)
        {
            return;
        }

        Widgets.Section(this, UiText.Get("ui.team.chestTitle"), new Vector2(0f, 0f), Width);
        var hint = Widgets.Body(this, UiText.Get("ui.team.chestHint"), new Vector2(0f, 16f), Width, Style.TextDim);

        float listTop = 16f + hint.Size.Y + 8f;
        var rows = Grouped(_state.StoredItems);

        const float listHeight = 300f;
        if (rows.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.team.chestEmpty"), new Vector2(0f, listTop), Width, Style.TextDim);
        }
        else
        {
            var scroll = new ScrollContainer
            {
                Position = new Vector2(0f, listTop),
                Size = new Vector2(Width, listHeight),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            AddChild(scroll);

            var column = new VBoxContainer { CustomMinimumSize = new Vector2(Width - 16f, 0f) };
            column.AddThemeConstantOverride("separation", 3);
            scroll.AddChild(column);

            foreach (var (id, count) in rows)
            {
                var item = _state.Item(id);
                if (item is null)
                {
                    continue;
                }

                var card = new OptionCard();
                column.AddChild(card);
                card.Bind(
                    0,
                    UiText.Get("ui.team.chestBadge"),
                    Style.NeutralBadge,
                    item.Name.Es + " · " + UiText.Get("ui.card.rarity." + item.Rarity),
                    count > 1 ? UiText.Get("ui.team.chestCopies", count) : string.Empty,
                    string.Empty,
                    ItemDescriptions.Describe(item, _state.Templates.Language));
                bool selected = id == _selectedItemId;
                card.Expanded = selected;
                card.Selected = selected;
                string capturedId = id;
                card.Activated += _ => SelectItem(capturedId);
            }
        }

        Action(listTop + listHeight + 12f);
    }

    /// <summary>Panel de acción: el jugador señalado, lo que lleva puesto, y los tres gestos.</summary>
    private void Action(float top)
    {
        Widgets.Panel(this, new Rect2(0f, top, Width, Height - top));
        Widgets.Section(this, UiText.Get("ui.team.chestAction"), new Vector2(12f, top + 6f), Width - 24f);

        var player = _playerId >= 0 ? _state!.Find(_playerId) : null;
        if (player is null)
        {
            Widgets.Body(this, UiText.Get("ui.team.chestNoPlayer"), new Vector2(12f, top + 26f), Width - 24f, Style.TextDim);
            return;
        }

        var equipped = _state!.EquippedItemOf(_playerId);
        Widgets.Body(this, UiText.Get("ui.team.chestPlayer", player.Name), new Vector2(12f, top + 26f), Width - 24f, Style.Accent);
        Widgets.Body(
            this,
            equipped is null
                ? UiText.Get("ui.team.chestPlayerEmpty")
                : UiText.Get("ui.team.chestPlayerHas", equipped.Name.Es),
            new Vector2(12f, top + 44f),
            Width - 24f,
            Style.TextDim);

        float y = top + 66f;

        var equipButton = Widgets.Button(
            this, UiText.Get("ui.team.chestEquip"), new Rect2(12f, y, 200f, 26f), _selectedItemId.Length > 0);
        equipButton.FocusMode = FocusModeEnum.None;
        equipButton.Pressed += OnEquip;

        var storeButton = Widgets.Button(
            this, UiText.Get("ui.team.chestStore"), new Rect2(220f, y, 200f, 26f), equipped is not null);
        storeButton.FocusMode = FocusModeEnum.None;
        storeButton.Pressed += OnStore;

        var passButton = Widgets.Button(
            this, UiText.Get("ui.team.chestPass"), new Rect2(428f, y, 200f, 26f), equipped is not null);
        passButton.FocusMode = FocusModeEnum.None;
        passButton.Pressed += OnTogglePass;

        y += 34f;

        if (_pickingTarget && equipped is not null)
        {
            y = RenderTargets(y);
        }

        if (_error.Length > 0)
        {
            Widgets.Body(this, _error, new Vector2(12f, y + 4f), Width - 24f, Style.Hole);
        }
    }

    /// <summary>Fila de botones, uno por cada otro jugador vivo, para elegir el destino del traspaso.</summary>
    private float RenderTargets(float y)
    {
        Widgets.Body(this, UiText.Get("ui.team.chestPassHint"), new Vector2(12f, y), Width - 24f, Style.TextDim);
        y += 20f;

        float x = 12f;
        const float buttonWidth = 156f;
        foreach (var candidate in _state!.Players)
        {
            if (candidate.Id == _playerId || candidate.PhysicalState == Sim.Model.PhysicalState.Dead)
            {
                continue;
            }

            var button = Widgets.Button(this, candidate.Name, new Rect2(x, y, buttonWidth, 24f));
            button.FocusMode = FocusModeEnum.None;
            int targetId = candidate.Id;
            button.Pressed += () => OnTransfer(targetId);

            x += buttonWidth + 8f;
            if (x + buttonWidth > Width - 12f)
            {
                x = 12f;
                y += 30f;
            }
        }

        return y + 30f;
    }

    private void SelectItem(string id)
    {
        _selectedItemId = _selectedItemId == id ? string.Empty : id;
        _pickingTarget = false;
        Render();
    }

    private void OnEquip()
    {
        if (_playerId < 0 || _selectedItemId.Length == 0)
        {
            return;
        }

        Try(() => _state!.EquipStored(_playerId, _selectedItemId));
        _selectedItemId = string.Empty;
        Render();
    }

    private void OnStore()
    {
        if (_playerId < 0)
        {
            return;
        }

        Try(() => _state!.StoreEquipped(_playerId));
        Render();
    }

    private void OnTogglePass()
    {
        _pickingTarget = !_pickingTarget;
        Render();
    }

    private void OnTransfer(int targetId)
    {
        if (_playerId < 0)
        {
            return;
        }

        Try(() => _state!.TransferEquipped(_playerId, targetId));
        _pickingTarget = false;
        Render();
    }

    private void Try(Action action)
    {
        try
        {
            action();
            _error = string.Empty;
        }
        catch (Exception ex)
        {
            _error = UiText.Get("ui.team.chestError", ex.Message);
        }
    }

    /// <summary>
    /// Agrupa el almacén (una entrada por copia, ya ordenado por id, RT-041) en pares (id, copias): la
    /// misma lectura que un jugador haría, "tres capas de tres", en vez de tres filas idénticas.
    /// </summary>
    private static List<(string Id, int Count)> Grouped(IReadOnlyList<string> stored)
    {
        var result = new List<(string, int)>();
        string last = string.Empty;
        int count = 0;
        foreach (string id in stored)
        {
            if (id == last)
            {
                count++;
            }
            else
            {
                if (count > 0)
                {
                    result.Add((last, count));
                }

                last = id;
                count = 1;
            }
        }

        if (count > 0)
        {
            result.Add((last, count));
        }

        return result;
    }
}
