using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Los <b>huecos de consumible del equipo</b> (encargo mercado-arrastrar, UI-006, ADR 0172): el destino propio
/// y visible donde se suelta un consumible que se compra, porque un consumible no se le da a un jugador
/// concreto: entra ya equipado en un hueco libre —y se reconfigura después en Equipo
/// (<see cref="ConsumablesPanel"/>)—. Enseña cuántos huecos hay y cuántos están llenos. Es la misma idea de
/// destino de arrastre que <see cref="PlayerCard"/>, pero como no representa a un jugador no comparte su
/// componente: solo un distintivo, un rótulo y el mismo estado de <see cref="DropHighlight"/>.
/// </summary>
public partial class MarketBagSlot : Control
{
    private const int Padding = 8;

    private string _title = string.Empty;
    private string _hint = string.Empty;
    private int _taken;
    private int _slots = 1;
    private bool _selected;
    private DropHighlight _drop;

    /// <summary>Mismo significado que <see cref="PlayerCard.DropHighlight"/>: qué le pasaría si se soltara ahora.</summary>
    public enum DropHighlight
    {
        None,
        Valid,
        Invalid,
    }

    /// <summary>La bolsa ha sido activada: un clic o el botón de acción del mando (UI-001, mismo gesto que las cartas).</summary>
    [Signal]
    public delegate void ActivatedEventHandler();

    public DropHighlight Drop
    {
        get => _drop;
        set
        {
            if (_drop == value)
            {
                return;
            }

            _drop = value;
            QueueRedraw();
        }
    }

    /// <summary>Foco de mando sobre la bolsa (UI-006), o la marca de "esto es lo que pasa si sueltas ahora" del ratón.</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            QueueRedraw();
        }
    }

    /// <summary>Rellena los dos textos y cuántos huecos hay llenos. Los textos llegan compuestos desde <c>UiText</c>; la bolsa no sabe de dónde salen.</summary>
    public void Bind(string title, string hint, int taken, int slots)
    {
        _title = title;
        _hint = hint;
        _taken = taken;
        _slots = Mathf.Max(1, slots);
        QueueRedraw();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            EmitSignal(SignalName.Activated);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Style.Panel);

        if (_drop == DropHighlight.Valid)
        {
            DrawRect(new Rect2(Vector2.Zero, size), new Color(Style.LinkCreated, 0.22f));
            DrawRect(new Rect2(Vector2.Zero, size), Style.LinkCreated, false, 2f);
        }
        else if (_drop == DropHighlight.Invalid)
        {
            DrawRect(new Rect2(Vector2.Zero, size), new Color(Style.LinkBroken, 0.16f));
        }

        if (_selected)
        {
            DrawRect(new Rect2(Vector2.Zero, size), Style.Accent, false, 1f);
        }

        // Un saquito por hueco (sin arte, regla 10): relleno si está ocupado, sólo el contorno si está libre. La
        // forma dice qué es (UI-002) y el número de saquitos, cuántos huecos hay.
        var pouchColor = _drop == DropHighlight.Invalid ? Style.TextDim : Style.Accent;
        for (int i = 0; i < _slots; i++)
        {
            var center = new Vector2(Padding + 12f + (i * 28f), size.Y / 2f + 2f);
            if (i < _taken)
            {
                DrawCircle(center, 11f, pouchColor);
            }
            else
            {
                DrawArc(center, 10f, 0f, Mathf.Tau, 24, pouchColor, 2f);
            }

            DrawRect(new Rect2(center + new Vector2(-3f, -15f), new Vector2(6f, 5f)), Style.Line);
        }

        var font = GetThemeDefaultFont();
        float textLeft = Padding + 10f + (_slots * 28f);
        Style.DrawText(this, font, new Vector2(textLeft, 8f), _title, Style.TextSmall, Style.Text, size.X - textLeft - Padding);
        foreach (string line in Style.Wrap(font, _hint, Style.TextSmall, size.X - textLeft - Padding))
        {
            Style.DrawText(this, font, new Vector2(textLeft, 24f), line, Style.TextSmall, Style.TextDim, size.X - textLeft - Padding);
            break; // una sola línea de pista: la bolsa es baja (Style.CollapsedHeight * 2), no hay sitio para más.
        }
    }
}
