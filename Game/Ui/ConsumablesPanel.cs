using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Game.Ui;

/// <summary>
/// Sección de <b>consumibles</b> de la pantalla de Equipo (CAT-B, RF-080..085): equipar y desequipar lo
/// que ya se ha comprado en el mercado, que hasta ahora era la única mitad de la cadena que faltaba —se
/// podía comprar pero nunca equipar, así que nunca llegaba a un partido.
///
/// <para>
/// <b>Simplificación de esta pantalla, no de <c>/Sim</c></b>: cada id se equipa como mucho una vez, sin
/// importar cuántas copias sueltas tenga el inventario. <c>/Sim</c> sí admitiría equipar la misma copia en
/// dos slots a la vez (<c>RunEngine.ApplyConsumables</c> solo cuenta manual/condicional, no ids
/// repetidos), pero "cada entrada se puede equipar y desequipar" describe un interruptor por id, no un
/// contador de slots por id, así que la interfaz no ofrece esa segunda dimensión.
/// </para>
///
/// <para>
/// Invariante que mantiene esta pantalla (más estricto que lo que exige <c>/Sim</c>, nunca lo viola):
/// como mucho <b>un</b> consumible manual a la vez. <c>RunEngine.ApplyConsumables</c> solo exige que haya
/// al menos uno si hay alguno equipado (RF-082); esta pantalla añade la lectura de que hay como mucho uno,
/// que es lo que hace que "hacer manual a otro" (punto 4 del encargo) sea una operación bien definida: el
/// que lo era pasa a condicional con su disparador por defecto, nunca quedan dos manuales a la vez.
/// </para>
///
/// <para>
/// Solo de ratón hoy: los botones no reclaman el foco de mando de Equipo (<c>FocusMode.None</c>, para no
/// abrir un segundo anillo de foco que compita con el de la pantalla), y la ayuda de mandos de
/// <c>TeamScreen</c> lo dice en vez de fingir un segundo flujo que no existe — mismo criterio que
/// <c>ui.input.padPending</c> en Mercado.
/// </para>
/// </summary>
public partial class ConsumablesPanel : Control
{
    /// <summary>Ancho fijo del panel: el que le deja <c>TeamScreen</c> dentro del panel de campo.</summary>
    public const float Width = 846f;

    /// <summary>Alto fijo del panel.</summary>
    public const float Height = 676f;

    /// <summary>
    /// Disparadores condicionales que ofrece esta pantalla para ciclar (RF-083): los seis que no piden
    /// umbral. <c>goalsConceded</c> y <c>refereeBiasBelow</c> se quedan fuera a propósito (el encargo no
    /// los exige y pedir un número aquí complicaría la interacción sin necesidad).
    /// </summary>
    private static readonly string[] CycleTriggers =
    {
        "scoreBehind", "scoreTied", "lastSeconds", "mobStart", "ownInjury", "ownRedCard",
    };

    private TeamState? _state;
    private string _selectedId = string.Empty;
    private string _error = string.Empty;

    /// <summary>Reconstruye la sección entera con el estado actual: inventario, equipados y selección.</summary>
    public void Rebuild(TeamState state)
    {
        _state = state;
        _error = string.Empty;
        Render();
    }

    /// <summary>Solo para la secuencia de capturas: preselecciona una fila para enseñar el panel de acción.</summary>
    public void SelectForTest(string id)
    {
        _selectedId = id;
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

        Widgets.Section(this, UiText.Get("ui.team.consumableTitle"), new Vector2(0f, 0f), Width);
        var hint = Widgets.Body(this, UiText.Get("ui.team.consumableHint"), new Vector2(0f, 16f), Width, Style.TextDim);

        float listTop = 16f + hint.Size.Y + 8f;
        var ids = RowIds();

        if (ids.Count == 0)
        {
            Widgets.Body(this, UiText.Get("ui.team.consumableEmpty"), new Vector2(0f, listTop), Width, Style.TextDim);
            return;
        }

        const float listHeight = 300f;
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

        foreach (string id in ids)
        {
            var definition = _state.Consumable(id);
            if (definition is null)
            {
                continue;
            }

            int owned = _state.ConsumablesOwned(id);
            var equipped = FindEquipped(id);

            var card = new OptionCard();
            column.AddChild(card);
            card.Bind(
                0,
                Badge(definition.Family),
                BadgeColor(definition.Family),
                definition.Name.Es + " · " + UiText.Get("ui.card.rarity." + definition.Rarity) + " · " + FamilyName(definition.Family),
                owned > 0 ? UiText.Get("ui.team.consumableCopies", owned) : string.Empty,
                string.Empty,
                DescriptionGenerator.DescribeEffects(definition.Effects, _state.Templates),
                new[] { EquipStatus(equipped) });
            bool selected = id == _selectedId;
            card.Expanded = selected;
            card.Selected = selected;
            card.Dimmed = owned <= 0 && equipped is null;
            string capturedId = id;
            card.Activated += _ => Select(capturedId);
        }

        Action(listTop + listHeight + 12f);

        if (_error.Length > 0)
        {
            Widgets.Body(this, _error, new Vector2(0f, Height - 20f), Width, Style.Hole);
        }
    }

    /// <summary>Panel de acción del consumible elegido: descripción, estado y los botones que la cambian.</summary>
    private void Action(float top)
    {
        Widgets.Panel(this, new Rect2(0f, top, Width, Height - top));
        Widgets.Section(this, UiText.Get("ui.market.action"), new Vector2(12f, top + 6f), Width - 24f);

        if (_selectedId.Length == 0 || _state!.Consumable(_selectedId) is not { } definition)
        {
            Widgets.Body(this, UiText.Get("ui.team.consumableNothingSelected"), new Vector2(12f, top + 26f), Width - 24f, Style.TextDim);
            return;
        }

        int owned = _state.ConsumablesOwned(_selectedId);
        var equipped = FindEquipped(_selectedId);

        Widgets.Body(
            this,
            definition.Name.Es + " · " + UiText.Get("ui.card.rarity." + definition.Rarity) + " · " + FamilyName(definition.Family),
            new Vector2(12f, top + 26f),
            Width - 24f,
            Style.Accent);
        var description = Widgets.Body(
            this,
            DescriptionGenerator.DescribeEffects(definition.Effects, _state.Templates),
            new Vector2(12f, top + 44f),
            Width - 24f);
        float y = top + 44f + description.Size.Y + 6f;
        var status = Widgets.Body(this, EquipStatus(equipped), new Vector2(12f, y), Width - 24f, Style.TextDim);
        y += status.Size.Y + 10f;

        int equippedTotal = _state.EquippedConsumables.Count;
        bool canEquip = equipped is null && owned > 0 && equippedTotal < 3;
        bool canUnequip = equipped is not null;
        bool conditional = equipped is { Mode: ConsumableMode.Conditional };

        string selectedId = _selectedId;
        var equipButton = Widgets.Button(
            this,
            equipped is null ? UiText.Get("ui.team.consumableEquip") : UiText.Get("ui.team.consumableUnequip"),
            new Rect2(12f, y, 180f, 26f),
            equipped is null ? canEquip : canUnequip);
        equipButton.FocusMode = FocusModeEnum.None;
        equipButton.Pressed += () => OnEquipToggle(selectedId);

        var manualButton = Widgets.Button(this, UiText.Get("ui.team.consumableMakeManual"), new Rect2(200f, y, 180f, 26f), conditional);
        manualButton.FocusMode = FocusModeEnum.None;
        manualButton.Pressed += () => OnMakeManual(selectedId);

        var triggerButton = Widgets.Button(
            this,
            conditional
                ? UiText.Get("ui.team.consumableCycleTrigger") + ": " + TriggerName(equipped!.Trigger)
                : UiText.Get("ui.team.consumableCycleTrigger"),
            new Rect2(388f, y, 340f, 26f),
            conditional);
        triggerButton.FocusMode = FocusModeEnum.None;
        triggerButton.Pressed += () => OnCycleTrigger(selectedId);

        y += 34f;
        if (equipped is null && owned <= 0)
        {
            Widgets.Body(this, UiText.Get("ui.team.consumableNoCopies"), new Vector2(12f, y), Width - 24f, Style.TextDim);
        }
        else if (equipped is null && equippedTotal >= 3)
        {
            Widgets.Body(this, UiText.Get("ui.team.consumableFull"), new Vector2(12f, y), Width - 24f, Style.TextDim);
        }
        else if (equipped is not null && owned <= 0)
        {
            Widgets.Body(this, UiText.Get("ui.team.consumableGone"), new Vector2(12f, y), Width - 24f, Style.TextDim);
        }
    }

    /// <summary>
    /// Ids a listar: el inventario (una entrada por id, aunque tenga varias copias) más los que estén
    /// equipados aunque ya no queden copias sueltas —para poder quitarlos igualmente—, todo por orden
    /// ordinal (RT-041).
    /// </summary>
    private List<string> RowIds()
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        string last = string.Empty;
        foreach (string id in _state!.OwnedConsumables)
        {
            if (id != last)
            {
                set.Add(id);
                last = id;
            }
        }

        foreach (var equipped in _state.EquippedConsumables)
        {
            set.Add(equipped.Id);
        }

        return new List<string>(set);
    }

    private EquippedConsumable? FindEquipped(string id)
    {
        foreach (var item in _state!.EquippedConsumables)
        {
            if (item.Id == id)
            {
                return item;
            }
        }

        return null;
    }

    private void Select(string id)
    {
        _selectedId = _selectedId == id ? string.Empty : id;
        Render();
    }

    /// <summary>
    /// Equipar o quitar el consumible elegido (punto 2 del encargo): el primero que se equipa entra
    /// manual; los siguientes, condicionales con el disparador por defecto de su familia. Al quitar uno,
    /// si era el manual y quedan otros equipados, se asciende al primero que quede (nunca se manda a
    /// <c>/Sim</c> una lista no vacía sin manual, RF-082).
    /// </summary>
    private void OnEquipToggle(string id)
    {
        var list = new List<EquippedConsumable>(_state!.EquippedConsumables);
        var equipped = FindEquipped(id);

        if (equipped is not null)
        {
            list.RemoveAll(e => e.Id == id);
            EnsureManual(list);
        }
        else
        {
            var definition = _state.Consumable(id);
            if (definition is null)
            {
                return;
            }

            bool hasManual = HasManual(list);
            list.Add(new EquippedConsumable(
                id,
                hasManual ? ConsumableMode.Conditional : ConsumableMode.Manual,
                hasManual ? DefaultTrigger(definition.Family) : string.Empty));
        }

        Apply(list);
    }

    /// <summary>
    /// Hace manual al consumible elegido (punto 4 del encargo): el que lo era pasa a condicional con el
    /// disparador por defecto de su propia familia. Nunca quedan dos manuales ni ninguno.
    /// </summary>
    private void OnMakeManual(string id)
    {
        var list = new List<EquippedConsumable>(_state!.EquippedConsumables);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Mode == ConsumableMode.Manual)
            {
                var previous = _state.Consumable(list[i].Id);
                list[i] = list[i] with
                {
                    Mode = ConsumableMode.Conditional,
                    Trigger = previous is null ? CycleTriggers[0] : DefaultTrigger(previous.Family),
                };
            }
            else if (list[i].Id == id)
            {
                list[i] = list[i] with { Mode = ConsumableMode.Manual, Trigger = string.Empty };
            }
        }

        Apply(list);
    }

    /// <summary>Cicla el disparador del condicional elegido (punto 3 del encargo).</summary>
    private void OnCycleTrigger(string id)
    {
        var list = new List<EquippedConsumable>(_state!.EquippedConsumables);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id && list[i].Mode == ConsumableMode.Conditional)
            {
                list[i] = list[i] with { Trigger = NextTrigger(list[i].Trigger) };
            }
        }

        Apply(list);
    }

    private void Apply(List<EquippedConsumable> list)
    {
        try
        {
            _state!.ApplyConsumables(list);
            _error = string.Empty;
        }
        catch (Exception ex)
        {
            _error = UiText.Get("ui.team.consumableError", ex.Message);
        }

        Render();
    }

    private static void EnsureManual(List<EquippedConsumable> list)
    {
        if (list.Count == 0 || HasManual(list))
        {
            return;
        }

        list[0] = list[0] with { Mode = ConsumableMode.Manual, Trigger = string.Empty };
    }

    private static bool HasManual(List<EquippedConsumable> list)
    {
        foreach (var item in list)
        {
            if (item.Mode == ConsumableMode.Manual)
            {
                return true;
            }
        }

        return false;
    }

    private static string NextTrigger(string current)
    {
        int index = Array.IndexOf(CycleTriggers, current);
        int next = index < 0 ? 0 : (index + 1) % CycleTriggers.Length;
        return CycleTriggers[next];
    }

    /// <summary>Disparador por defecto según la familia (punto 2 del encargo).</summary>
    private static string DefaultTrigger(ConsumableFamily family) => family switch
    {
        ConsumableFamily.Medical => "ownInjury",
        ConsumableFamily.Dirty => "scoreBehind",
        ConsumableFamily.Tactical => "scoreBehind",
        _ => "lastSeconds",
    };

    private static string EquipStatus(EquippedConsumable? equipped) => equipped switch
    {
        null => UiText.Get("ui.team.consumableNotEquipped"),
        { Mode: ConsumableMode.Manual } => UiText.Get("ui.team.consumableEquippedManual"),
        _ => UiText.Get("ui.team.consumableEquippedConditional", TriggerName(equipped.Trigger)),
    };

    private static string TriggerName(string trigger) => trigger switch
    {
        "scoreBehind" => UiText.Get("ui.team.consumableTriggerScoreBehind"),
        "scoreTied" => UiText.Get("ui.team.consumableTriggerScoreTied"),
        "lastSeconds" => UiText.Get("ui.team.consumableTriggerLastSeconds"),
        "mobStart" => UiText.Get("ui.team.consumableTriggerMobStart"),
        "ownInjury" => UiText.Get("ui.team.consumableTriggerOwnInjury"),
        "ownRedCard" => UiText.Get("ui.team.consumableTriggerOwnRedCard"),
        _ => trigger,
    };

    private static string FamilyName(ConsumableFamily family) => family switch
    {
        ConsumableFamily.Medical => UiText.Get("ui.team.consumableFamilyMedical"),
        ConsumableFamily.Tactical => UiText.Get("ui.team.consumableFamilyTactical"),
        ConsumableFamily.Dirty => UiText.Get("ui.team.consumableFamilyDirty"),
        _ => UiText.Get("ui.team.consumableFamilySupernatural"),
    };

    private static string Badge(ConsumableFamily family) => family switch
    {
        ConsumableFamily.Medical => UiText.Get("ui.team.consumableBadgeMedical"),
        ConsumableFamily.Tactical => UiText.Get("ui.team.consumableBadgeTactical"),
        ConsumableFamily.Dirty => UiText.Get("ui.team.consumableBadgeDirty"),
        _ => UiText.Get("ui.team.consumableBadgeSupernatural"),
    };

    private static Color BadgeColor(ConsumableFamily family) => family switch
    {
        ConsumableFamily.Medical => Style.LinkCreated,
        ConsumableFamily.Tactical => Style.Of(Underleague.Sim.Model.Position.Defender),
        ConsumableFamily.Dirty => Style.Hole,
        _ => Style.Accent,
    };
}
