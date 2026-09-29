using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Data;
using Underleague.Game.Ui.Knavall;
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
///
/// <para>
/// <b>Lenguaje de Knavall (ADR 0162):</b> las reglas de RF-080..082 ya no se leen en una línea de texto sino
/// en la forma: tres huecos «al partido» —el manual con su mano, los condicionales con su reloj de arena— y
/// el zurrón debajo. A la derecha, el consumible elegido en grande con sus tres placas. La lógica de equipar,
/// hacer manual y cambiar el disparador es la de siempre.
/// </para>
/// </summary>
public partial class ConsumablesPanel : InkCanvas
{
    /// <summary>Ancho de la columna de detalle, a la derecha.</summary>
    private const float DetailWidth = 330f;

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
    private PlaqueButton _equip = null!;
    private PlaqueButton _manual = null!;
    private PlaqueButton _trigger = null!;

    public override void _Ready()
    {
        base._Ready();
        _equip = PlaqueButton.Create(this, UiText.Get("ui.kn.takeIt"), Glyph.Potion, PlaqueKind.Primary, new Rect2(), 21);
        _equip.Pressed += () => OnEquipToggle(_selectedId);
        _manual = PlaqueButton.Create(this, UiText.Get("ui.kn.makeManual"), Glyph.Manual, PlaqueKind.Paper, new Rect2(), 22);
        _manual.Tip = new Tip(UiText.Get("ui.kn.makeManual"), UiText.Get("ui.kn.tip.makeManual"), Glyph.Manual);
        _manual.Pressed += () => OnMakeManual(_selectedId);
        _trigger = PlaqueButton.Create(this, UiText.Get("ui.kn.trigger"), Glyph.Conditional, PlaqueKind.Paper, new Rect2(), 23);
        _trigger.Tip = new Tip(UiText.Get("ui.kn.trigger"), UiText.Get("ui.kn.tip.cycleTrigger"), Glyph.Conditional);
        _trigger.FontSize = 16;
        _trigger.Pressed += () => OnCycleTrigger(_selectedId);
        Render();
    }

    /// <summary>Reconstruye la sección entera con el estado actual: inventario, equipados y selección.</summary>
    public void Rebuild(TeamState state)
    {
        _state = state;
        _error = string.Empty;
        Render();
    }

    /// <summary>Elige un consumible (desde la vista compacta de Plantilla o desde las capturas).</summary>
    public void Select(string id)
    {
        _selectedId = id;
        Render();
    }

    /// <summary>Solo para la secuencia de capturas: preselecciona una fila para enseñar el panel de acción.</summary>
    public void SelectForTest(string id) => Select(id);

    /// <summary>Coloca y habilita las tres placas según lo elegido; el resto lo pinta <see cref="_Draw"/>.</summary>
    private void Render()
    {
        QueueRedraw();
        if (_equip is null)
        {
            return;
        }

        var definition = _state is not null && _selectedId.Length > 0 ? _state.Consumable(_selectedId) : null;
        bool show = definition is not null;
        _equip.Visible = show;
        _manual.Visible = show;
        _trigger.Visible = show;
        if (!show)
        {
            return;
        }

        int owned = _state!.ConsumablesOwned(_selectedId);
        var equipped = FindEquipped(_selectedId);
        int equippedTotal = _state.EquippedConsumables.Count;
        bool canEquip = equipped is null && owned > 0 && equippedTotal < 3;
        bool conditional = equipped is { Mode: ConsumableMode.Conditional };

        float left = Size.X - DetailWidth + 10f;
        float width = DetailWidth - 36f;
        float y = Size.Y - 196f;
        _equip.Position = new Vector2(left, y);
        _equip.Size = new Vector2(width, 56f);
        _equip.Caption = equipped is null ? UiText.Get("ui.kn.takeIt") : UiText.Get("ui.kn.leaveIt");
        _equip.Kind = equipped is null ? PlaqueKind.Primary : PlaqueKind.Paper;
        _equip.Tip = new Tip(_equip.Caption, UiText.Get(equipped is null ? "ui.kn.tip.equipConsumable" : "ui.kn.tip.unequipConsumable"), Glyph.Potion);
        _equip.Disabled = equipped is null ? !canEquip : false;

        _manual.Position = new Vector2(left, y + 62f);
        _manual.Size = new Vector2(width, 52f);
        _manual.Disabled = !conditional;

        _trigger.Position = new Vector2(left, y + 120f);
        _trigger.Size = new Vector2(width, 52f);
        _trigger.Caption = conditional ? UiText.Get("ui.kn.trigger") + " " + TriggerName(equipped!.Trigger) : UiText.Get("ui.kn.trigger");
        _trigger.Disabled = !conditional;
    }

    public override void _Draw()
    {
        ClearZones();
        var main = new Rect2(0f, 0f, Size.X - DetailWidth - 8f, Size.Y - 8f);
        var side = new Rect2(Size.X - DetailWidth, 0f, DetailWidth - 8f, Size.Y - 8f);
        Ink.Sheet(this, main, 8181);
        Ink.Sheet(this, side, 8282, Ink.PaperWarm);
        if (_state is null)
        {
            return;
        }

        int equippedTotal = _state.EquippedConsumables.Count;
        Tiles.Header(this, new Vector2(18f, 12f), main.Size.X - 30f, Glyph.Potion, UiText.Get("ui.kn.consumables"), UiText.Get("ui.kn.consumablesCount", equippedTotal), 60f, 34);
        Zone(new Rect2(12f, 8f, main.Size.X - 24f, 68f), TeamTips.Consumables());

        // Al partido: tres huecos. El primero es el manual (RF-082), los otros dos condicionales (RF-081).
        float y = 92f;
        Ink.Text(this, Ink.Display, new Vector2(22f, y), UiText.Get("ui.kn.equipped").ToUpperInvariant(), 20, Ink.RedDark);
        y += 30f;
        float slotGap = 14f;
        float slot = Mathf.Min(150f, (main.Size.X - 44f - (slotGap * 2f)) / 3f);
        var equippedList = new List<EquippedConsumable>(_state.EquippedConsumables);
        equippedList.Sort((a, b) => a.Mode == b.Mode ? 0 : a.Mode == ConsumableMode.Manual ? -1 : 1);
        for (int i = 0; i < 3; i++)
        {
            var rect = new Rect2(22f + (i * (slot + slotGap)), y, slot, slot + 34f);
            if (i < equippedList.Count && _state.Consumable(equippedList[i].Id) is { } definition)
            {
                var equipped = equippedList[i];
                string key = "slot:" + equipped.Id;
                string caption = equipped.Mode == ConsumableMode.Manual
                    ? UiText.Get("ui.kn.manual")
                    : UiText.Get("ui.kn.conditional", TriggerName(equipped.Trigger));
                Tiles.Draw(this, rect, new TileLook(InkIcons.Of(definition.Family), 1, equipped.Id == _selectedId, HoverKey == key, false, Inventory.Marker(equipped), Caption: caption), 300 + i);
                string captured = equipped.Id;
                Zone(rect, TeamTips.Consumable(_state, definition, equipped, _state.ConsumablesOwned(equipped.Id), TriggerName(equipped.Trigger)), () => Select(captured), key);
            }
            else
            {
                Tiles.Empty(this, rect, UiText.Get("ui.kn.freeSlot"), 310 + i);
                Zone(rect, TeamTips.Consumables());
            }
        }

        // En el zurrón: lo comprado que no va al partido.
        y += slot + 50f;
        Ink.Text(this, Ink.Display, new Vector2(22f, y), UiText.Get("ui.kn.pouch").ToUpperInvariant(), 20, Ink.RedDark);
        y += 30f;
        var pouch = new List<string>();
        foreach (string id in Inventory.ConsumableIds(_state))
        {
            if (FindEquipped(id) is null)
            {
                pouch.Add(id);
            }
        }

        const int Columns = 5;
        float gap = 10f;
        float tile = (main.Size.X - 44f - (gap * (Columns - 1))) / Columns;
        if (pouch.Count == 0)
        {
            Ink.Text(this, Ink.Plain, new Vector2(24f, y + 4f), UiText.Get("ui.kn.consumablesEmpty"), 15, Ink.Muted);
        }

        for (int i = 0; i < pouch.Count; i++)
        {
            string id = pouch[i];
            if (_state.Consumable(id) is not { } definition)
            {
                continue;
            }

            int owned = _state.ConsumablesOwned(id);
            var rect = new Rect2(22f + ((i % Columns) * (tile + gap)), y + ((i / Columns) * (tile + 44f)), tile, tile + 34f);
            if (rect.End.Y > main.End.Y - 4f)
            {
                break;
            }

            string key = "pouch:" + id;
            Tiles.Draw(this, rect, new TileLook(InkIcons.Of(definition.Family), owned, id == _selectedId, HoverKey == key, owned <= 0, Caption: UiText.Name(definition.Name)), 400 + i);
            string captured = id;
            Zone(rect, TeamTips.Consumable(_state, definition, null, owned, string.Empty), () => Select(captured), key);
        }

        DrawDetail(side);
    }

    private EquippedConsumable? FindEquipped(string id) => Inventory.Equipped(_state!, id);

    /// <summary>Columna de la derecha: el consumible elegido en grande, qué hace y cómo va al partido.</summary>
    private void DrawDetail(Rect2 side)
    {
        float left = side.Position.X + 18f;
        float width = side.Size.X - 36f;
        if (_selectedId.Length == 0 || _state!.Consumable(_selectedId) is not { } definition)
        {
            var lines = Style.Wrap(Ink.Display, UiText.Get("ui.kn.pickConsumable").ToUpperInvariant(), 22, width);
            float y0 = side.Position.Y + 60f;
            foreach (string line in lines)
            {
                Ink.Text(this, Ink.Display, new Vector2(left, y0), line, 22, Ink.Muted);
                y0 += 28f;
            }

            return;
        }

        var equipped = FindEquipped(_selectedId);
        int owned = _state.ConsumablesOwned(_selectedId);
        var art = new Vector2(side.GetCenter().X, side.Position.Y + 84f);
        var splash = Broadcast.Pregon.Burst(62f, 48f, 11, art);
        DrawColoredPolygon(Ink.Shift(splash, new Vector2(3f, 4f)), new Color(0f, 0f, 0f, 0.25f));
        DrawColoredPolygon(splash, Ink.Ochre);
        InkIcons.Draw(this, InkIcons.Of(definition.Family), art, 96f);

        float y = side.Position.Y + 160f;
        string name = UiText.Name(definition.Name).ToUpperInvariant();
        int size = Ink.FitSize(Ink.Display, name, 26, width, 17);
        Ink.Text(this, Ink.Display, new Vector2(left, y), name, size, Ink.Black);
        y += Ink.Display.GetHeight(size);
        string sub = (UiText.Get("ui.card.rarity." + definition.Rarity) + " · " + FamilyName(definition.Family)).ToUpperInvariant();
        Ink.Text(this, Ink.Heavy, new Vector2(left, y), sub, 15, Ink.Muted);
        y += 26f;

        foreach (string line in Style.Wrap(Ink.Plain, DescriptionGenerator.DescribeEffects(definition.Effects, _state.Templates), 16, width))
        {
            Ink.Text(this, Ink.Plain, new Vector2(left, y), line, 16, Ink.Brown);
            y += 19f;
        }

        y += 8f;
        var marker = Inventory.Marker(equipped);
        string status = equipped is null
            ? UiText.Get("ui.kn.tip.pouch", owned)
            : equipped.Mode == ConsumableMode.Manual
                ? UiText.Get("ui.kn.tip.manual")
                : UiText.Get("ui.kn.tip.conditional", TriggerName(equipped.Trigger));
        if (marker != Glyph.None)
        {
            InkIcons.Draw(this, marker, new Vector2(left + 11f, y + 11f), 22f);
        }

        float statusLeft = left + (marker != Glyph.None ? 28f : 0f);
        foreach (string line in Style.Wrap(Ink.Heavy, status, 15, width - (statusLeft - left)))
        {
            Ink.Text(this, Ink.Heavy, new Vector2(statusLeft, y), line, 15, equipped is null ? Ink.Muted : Ink.OchreDark);
            y += 18f;
        }

        string? warning = null;
        int equippedTotal = _state.EquippedConsumables.Count;
        if (equipped is null && owned <= 0)
        {
            warning = UiText.Get("ui.team.consumableNoCopies");
        }
        else if (equipped is null && equippedTotal >= 3)
        {
            warning = UiText.Get("ui.team.consumableFull");
        }
        else if (equipped is not null && owned <= 0)
        {
            warning = UiText.Get("ui.team.consumableGone");
        }

        if (_error.Length > 0)
        {
            warning = _error;
        }

        if (warning is not null)
        {
            var warningLines = Style.Wrap(Ink.Heavy, warning, 14, width);
            float wy = side.End.Y - 226f - (warningLines.Count * 17f);
            foreach (string line in warningLines)
            {
                Ink.Text(this, Ink.Heavy, new Vector2(left, wy), line, 14, Ink.Red);
                wy += 17f;
            }
        }
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

    /// <summary>Frase del disparador («vas por debajo en el marcador»), también para la vista compacta.</summary>
    public static string TriggerName(string trigger) => trigger switch
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
}
