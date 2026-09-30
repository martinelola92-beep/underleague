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
/// Sección de <b>consumibles</b> de la pantalla de Equipo (RF-080..085, ADR 0172): los <b>dos huecos</b> que
/// el equipo lleva al partido y qué hacer con cada uno. Un consumible se compra en el mercado o lo da un
/// evento y <b>sale ya equipado</b>, manual —un clic en el tablero de la retransmisión—; aquí se le puede
/// pasar a condicional (salta solo cuando pasa lo que se le diga) o descartarlo para liberar el hueco. No
/// hay «zurrón» ni inventario suelto: el hueco es la posesión, y lo que no se usa se queda en él para el
/// partido siguiente.
///
/// <para>
/// Cada hueco se configura por separado. Ya no hay invariante de «uno manual como mucho» ni de «al menos
/// uno manual» (RF-082 enmendada): con dos huecos y consumibles que persisten, pasar los dos a condicional
/// es una decisión legítima. <c>/Sim</c> valida lo que de verdad importa —que sólo se reconfigure lo que ya
/// se lleva, sin repetir— y esta pantalla no ofrece nada que lo viole.
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
/// <b>Lenguaje de Knavall (ADR 0162):</b> las reglas se leen en la forma —dos huecos grandes con su marca de
/// mano (manual) o reloj de arena (condicional)— y no en una línea de texto. A la derecha, el consumible
/// elegido en grande con sus tres placas.
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
        _equip = PlaqueButton.Create(this, UiText.Get("ui.kn.discard"), Glyph.None, PlaqueKind.Paper, new Rect2(), 21);
        _equip.Tip = new Tip(UiText.Get("ui.kn.discard"), UiText.Get("ui.kn.tip.discardConsumable"), Glyph.Potion);
        _equip.Pressed += () => OnDiscard(_selectedId);
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

        // Un consumible que ya no se lleva (se gastó en el partido o se descartó) no puede seguir elegido: la
        // columna de la derecha enseñaría algo que ya no está en ningún hueco.
        if (_selectedId.Length > 0 && Inventory.Equipped(state, _selectedId) is null)
        {
            _selectedId = string.Empty;
        }

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

        var equipped = FindEquipped(_selectedId);
        bool carried = equipped is not null;
        bool conditional = equipped is { Mode: ConsumableMode.Conditional };

        float left = Size.X - DetailWidth + 10f;
        float width = DetailWidth - 36f;
        float y = Size.Y - 196f;
        _equip.Position = new Vector2(left, y + 120f);
        _equip.Size = new Vector2(width, 52f);
        _equip.Disabled = !carried;

        _manual.Position = new Vector2(left, y);
        _manual.Size = new Vector2(width, 56f);
        _manual.Disabled = !conditional;

        _trigger.Position = new Vector2(left, y + 62f);
        _trigger.Size = new Vector2(width, 52f);
        _trigger.Caption = conditional ? UiText.Get("ui.kn.changeTrigger") : UiText.Get("ui.kn.makeConditional");
        _trigger.Disabled = !carried;
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

        int slots = RunRules.ConsumableSlots;
        Tiles.Header(this, new Vector2(18f, 12f), main.Size.X - 30f, Glyph.Potion, UiText.Get("ui.kn.consumables"), UiText.Get("ui.kn.consumablesCount", _state.EquippedConsumables.Count, slots), 60f, 34);
        Zone(new Rect2(12f, 8f, main.Size.X - 24f, 68f), TeamTips.Consumables());

        // Los dos huecos (RF-080, ADR 0172): lo que se lleva al partido. Cada uno con su marca de mano (manual)
        // o de reloj de arena (condicional); el libre, vacío y dicho.
        float y = 92f;
        Ink.Text(this, Ink.Display, new Vector2(22f, y), UiText.Get("ui.kn.equipped").ToUpperInvariant(), 20, Ink.RedDark);
        y += 30f;
        float slotGap = 18f;
        float slot = Mathf.Min(200f, (main.Size.X - 44f - (slotGap * (slots - 1))) / slots);
        for (int i = 0; i < slots; i++)
        {
            var rect = new Rect2(22f + (i * (slot + slotGap)), y, slot, slot + 72f);
            if (i < _state.EquippedConsumables.Count && _state.Consumable(_state.EquippedConsumables[i].Id) is { } definition)
            {
                var equipped = _state.EquippedConsumables[i];
                string key = "slot:" + equipped.Id;
                var look = new TileLook(InkIcons.Of(definition.Family), 1, equipped.Id == _selectedId, HoverKey == key, false, Inventory.Marker(equipped), Caption: UiText.Name(definition.Name));
                Tiles.Draw(this, rect, look, 300 + i);

                // Debajo del nombre, cómo se usa: «manual» o cuándo salta solo. Es lo que hace legible la regla sin
                // abrir el consumible (principio: comportamiento observable).
                string mode = equipped.Mode == ConsumableMode.Manual
                    ? UiText.Get("ui.kn.manual")
                    : UiText.Get("ui.kn.conditional", TriggerName(equipped.Trigger));
                var modeColor = equipped.Id == _selectedId ? Ink.Black : Ink.Brown;
                float modeTop = rect.End.Y - 40f;
                var modeLines = Style.Wrap(Ink.Heavy, mode, 14, slot - 14f);
                for (int line = 0; line < modeLines.Count && line < 2; line++)
                {
                    float modeWidth = Ink.Width(Ink.Heavy, modeLines[line], 14);
                    Ink.Text(this, Ink.Heavy, new Vector2(rect.Position.X + ((slot - modeWidth) / 2f), modeTop), modeLines[line], 14, modeColor);
                    modeTop += 16f;
                }

                string captured = equipped.Id;
                Zone(rect, TeamTips.Consumable(_state, definition, equipped, TriggerName(equipped.Trigger)), () => Select(captured), key);
            }
            else
            {
                Tiles.Empty(this, rect, UiText.Get("ui.kn.freeSlot"), 310 + i);
                Zone(rect, TeamTips.Consumables());
            }
        }

        // Cómo se llenan y cómo se usan: las reglas de RF-080..085 en palabras cortas, con la marca de cada modo.
        y += slot + 96f;
        Ink.Text(this, Ink.Display, new Vector2(22f, y), UiText.Get("ui.kn.slotsHow").ToUpperInvariant(), 20, Ink.RedDark);
        y += 30f;
        var rules = new (Glyph Glyph, string Text)[]
        {
            (Glyph.Potion, UiText.Get("ui.kn.slotsBuy")),
            (Glyph.Manual, UiText.Get("ui.kn.tip.manual")),
            (Glyph.Conditional, UiText.Get("ui.kn.slotsConditional")),
            (Glyph.None, UiText.Get("ui.kn.slotsKeep")),
        };
        foreach (var (glyph, text) in rules)
        {
            float textLeft = 22f + 36f;
            if (glyph != Glyph.None)
            {
                InkIcons.Draw(this, glyph, new Vector2(22f + 14f, y + 11f), 26f);
            }

            foreach (string line in Style.Wrap(Ink.Plain, text, 17, main.Size.X - textLeft - 20f))
            {
                if (y + 20f > main.End.Y - 6f)
                {
                    break;
                }

                Ink.Text(this, Ink.Plain, new Vector2(textLeft, y), line, 17, Ink.Brown);
                y += 21f;
            }

            y += 8f;
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
            ? string.Empty
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
            Ink.Text(this, Ink.Heavy, new Vector2(statusLeft, y), line, 15, Ink.OchreDark);
            y += 18f;
        }

        if (_error.Length > 0)
        {
            var warningLines = Style.Wrap(Ink.Heavy, _error, 14, width);
            float wy = side.End.Y - 226f - (warningLines.Count * 17f);
            foreach (string line in warningLines)
            {
                Ink.Text(this, Ink.Heavy, new Vector2(left, wy), line, 14, Ink.Red);
                wy += 17f;
            }
        }
    }

    /// <summary>
    /// Descartar el consumible elegido (ADR 0172): lo tira y libera su hueco, por si el jugador prefiere
    /// comprar otro mejor. No vuelve a ninguna parte —no hay zurrón— y por eso el botón lo dice con todas
    /// las letras.
    /// </summary>
    private void OnDiscard(string id)
    {
        var list = new List<EquippedConsumable>(_state!.EquippedConsumables);
        list.RemoveAll(e => e.Id == id);
        _selectedId = string.Empty;
        Apply(list);
    }

    /// <summary>
    /// Hace manual al consumible elegido (RF-082, ADR 0172): lo activará el jugador con un clic en el tablero
    /// del partido. Sólo cambia ese hueco; el otro no se toca.
    /// </summary>
    private void OnMakeManual(string id)
    {
        var list = new List<EquippedConsumable>(_state!.EquippedConsumables);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id)
            {
                list[i] = list[i] with { Mode = ConsumableMode.Manual, Trigger = string.Empty };
            }
        }

        Apply(list);
    }

    /// <summary>
    /// El botón de disparador (RF-081, RF-083): un consumible manual pasa a condicional con el disparador por
    /// defecto de su familia; uno que ya es condicional cicla al siguiente de la lista.
    /// </summary>
    private void OnCycleTrigger(string id)
    {
        var list = new List<EquippedConsumable>(_state!.EquippedConsumables);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id != id)
            {
                continue;
            }

            list[i] = list[i].Mode == ConsumableMode.Conditional
                ? list[i] with { Trigger = NextTrigger(list[i].Trigger) }
                : list[i] with { Mode = ConsumableMode.Conditional, Trigger = _state.Consumable(id)?.SuggestedTrigger ?? CycleTriggers[0] };
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

    private static string NextTrigger(string current)
    {
        int index = Array.IndexOf(CycleTriggers, current);
        int next = index < 0 ? 0 : (index + 1) % CycleTriggers.Length;
        return CycleTriggers[next];
    }

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
