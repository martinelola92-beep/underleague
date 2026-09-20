using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// La <b>bolsa del equipo</b> (encargo mercado-arrastrar, UI-006): el destino propio y visible donde se
/// suelta un consumible comprado, porque un consumible no se le da a un jugador concreto —se equipa
/// después, en Equipo (<see cref="ConsumablesPanel"/>)—. Es la misma idea de destino de arrastre que
/// <see cref="PlayerCard"/>, pero como no representa a un jugador no comparte su componente: solo un
/// distintivo, un rótulo y el mismo estado de <see cref="DropHighlight"/>.
/// </summary>
public partial class MarketBagSlot : Control
{
    private const int Padding = 8;

    private string _title = string.Empty;
    private string _hint = string.Empty;
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

    /// <summary>Rellena los dos textos. Llegan compuestos desde <c>UiText</c>; la bolsa no sabe de dónde salen.</summary>
    public void Bind(string title, string hint)
    {
        _title = title;
        _hint = hint;
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

        // Distintivo de bolsa: un saco simple, sin arte (regla 10) — un cuerpo redondeado y el cordón
        // atado arriba, lo bastante distinto del medallón de retrato como para no confundirse con una
        // ficha de jugador (UI-002: la forma dice qué es, no solo el sitio en el que está).
        var pouchColor = _drop == DropHighlight.Invalid ? Style.TextDim : Style.Accent;
        var center = new Vector2(Padding + 14f, size.Y / 2f + 2f);
        DrawCircle(center, 13f, pouchColor);
        DrawRect(new Rect2(center + new Vector2(-4f, -17f), new Vector2(8f, 6f)), Style.Line);

        var font = GetThemeDefaultFont();
        float textLeft = Padding + 34f;
        Style.DrawText(this, font, new Vector2(textLeft, 8f), _title, Style.TextSmall, Style.Text, size.X - textLeft - Padding);
        foreach (string line in Style.Wrap(font, _hint, Style.TextSmall, size.X - textLeft - Padding))
        {
            Style.DrawText(this, font, new Vector2(textLeft, 24f), line, Style.TextSmall, Style.TextDim, size.X - textLeft - Padding);
            break; // una sola línea de pista: la bolsa es baja (Style.CollapsedHeight * 2), no hay sitio para más.
        }
    }
}
