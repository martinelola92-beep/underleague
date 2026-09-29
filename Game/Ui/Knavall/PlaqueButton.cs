using Godot;

namespace Underleague.Game.Ui.Knavall;

/// <summary>El material de una placa-botón.</summary>
public enum PlaqueKind
{
    /// <summary>Acción principal: brochazo rojo con letra clara (EQUIPAR).</summary>
    Primary,

    /// <summary>Acción corriente: placa de papel con letra de tinta.</summary>
    Paper,

    /// <summary>Estructura: tabla de madera con letra clara (VOLVER).</summary>
    Wood,

    /// <summary>Pestaña: papel; ocre cuando está activa (<see cref="PlaqueButton.Active"/>).</summary>
    Tab,
}

/// <summary>
/// Botón con forma de <b>placa física</b> del mundo de Knavall (ADR 0162): tabla o cartel con contorno de
/// tinta y sombra dura, icono grande y rótulo. Al pasar el ratón se levanta; al pulsar, se hunde contra su
/// sombra. Es un <see cref="Button"/> de verdad —foco, teclado, mando, desactivado—: solo cambia cómo se ve.
/// </summary>
public partial class PlaqueButton : Button
{
    private string _caption = string.Empty;
    private Glyph _glyph = Glyph.None;
    private PlaqueKind _kind = PlaqueKind.Paper;
    private bool _active;

    /// <summary>Cartel de ayuda del botón: qué pasa al pulsarlo.</summary>
    public Tip? Tip { get; set; }

    /// <summary>Rótulo (se pinta en mayúsculas). Puede llevar un salto de línea.</summary>
    public string Caption
    {
        get => _caption;
        set
        {
            _caption = value;
            Text = string.Empty;
            TooltipText = value;
            QueueRedraw();
        }
    }

    public Glyph Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            QueueRedraw();
        }
    }

    public PlaqueKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    /// <summary>Pestaña seleccionada: se pinta en ocre y un poco más grande.</summary>
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            QueueRedraw();
        }
    }

    /// <summary>Semilla del trazo irregular, para que dos placas iguales no tengan el mismo borde.</summary>
    public int Seed { get; set; } = 1;

    /// <summary>Tamaño del rótulo.</summary>
    public int FontSize { get; set; } = Ink.SizeHeading;

    public static PlaqueButton Create(Control parent, string caption, Glyph glyph, PlaqueKind kind, Rect2 area, int seed)
    {
        var button = new PlaqueButton
        {
            Position = area.Position,
            Size = area.Size,
            Kind = kind,
            Glyph = glyph,
            Seed = seed,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        button.Caption = caption;
        parent.AddChild(button);
        return button;
    }

    public override void _Ready()
    {
        var empty = new StyleBoxEmpty();
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" })
        {
            AddThemeStyleboxOverride(state, empty);
        }

        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
    }

    public override GodotObject? _MakeCustomTooltip(string forText) => Tip is { } tip ? InkTooltip.Build(tip) : null;

    public override void _Draw()
    {
        bool hover = IsHovered() && !Disabled;
        bool down = ButtonPressed || GetDrawMode() == DrawMode.Pressed;
        var lift = down ? new Vector2(2f, 3f) : hover ? new Vector2(-1f, -2f) : Vector2.Zero;
        var shadow = down ? new Vector2(1f, 1f) : hover ? new Vector2(5f, 7f) : new Vector2(4f, 5f);
        var rect = new Rect2(lift + new Vector2(2f, 2f), Size - new Vector2(8f, 8f));

        Color fill;
        Color ink;
        switch (_kind)
        {
            case PlaqueKind.Primary:
                fill = Ink.Red;
                ink = Ink.Paper;
                break;
            case PlaqueKind.Wood:
                fill = Ink.Wood;
                ink = Ink.Paper;
                break;
            case PlaqueKind.Tab:
                fill = _active ? Ink.Ochre : Ink.Paper;
                ink = Ink.Black;
                break;
            default:
                fill = Ink.Paper;
                ink = Ink.Black;
                break;
        }

        if (hover && !_active)
        {
            fill = fill.Lightened(0.1f);
        }

        if (Disabled)
        {
            fill = new Color(fill.Lerp(Ink.PaperDark, 0.6f), 0.75f);
            ink = new Color(Ink.Muted, 0.8f);
        }

        if (_kind == PlaqueKind.Wood && !Disabled)
        {
            Ink.Plank(this, rect, Seed, fill, nails: false);
        }
        else
        {
            Ink.Slab(this, rect, fill, Seed, 1.5f, 3f, shadow);
        }

        if (_kind == PlaqueKind.Primary && !Disabled)
        {
            // Veta de brochazo sobre la placa roja.
            for (int i = 0; i < 3; i++)
            {
                float streak = rect.Position.Y + 6f + (i * (rect.Size.Y - 12f) / 2f);
                DrawLine(new Vector2(rect.Position.X + 10f + (i * 13f), streak), new Vector2(rect.End.X - 14f - (i * 9f), streak + 1f), new Color(Ink.RedDark, 0.4f), 1.3f, true);
            }
        }

        if (HasFocus())
        {
            DrawPolyline(Ink.Closed(Ink.Rough(rect.Grow(3f), 1.2f, Seed + 5)), Ink.Ochre, 2.5f, true);
        }

        float iconSide = Mathf.Min(rect.Size.Y - 14f, 34f);
        string[] lines = _caption.ToUpperInvariant().Split('\n');
        var font = Ink.Heavy;
        int size = FontSize;
        float textWidth = 0f;
        foreach (string line in lines)
        {
            textWidth = Mathf.Max(textWidth, Ink.Width(font, line, size));
        }

        float available = rect.Size.X - 20f - (_glyph != Glyph.None ? iconSide + 10f : 0f);
        while (textWidth > available && size > Ink.SizeSmall)
        {
            size--;
            textWidth = 0f;
            foreach (string line in lines)
            {
                textWidth = Mathf.Max(textWidth, Ink.Width(font, line, size));
            }
        }

        float blockWidth = textWidth + (_glyph != Glyph.None ? iconSide + 10f : 0f);
        float x = rect.Position.X + ((rect.Size.X - blockWidth) / 2f);
        var center = rect.GetCenter();
        if (_glyph != Glyph.None)
        {
            InkIcons.Draw(this, _glyph, new Vector2(x + (iconSide / 2f), center.Y), iconSide);

            x += iconSide + 10f;
        }

        float lineHeight = font.GetHeight(size) - 2f;
        float y = center.Y - (lineHeight * lines.Length / 2f) - 1f;
        foreach (string line in lines)
        {
            if (ink == Ink.Paper)
            {
                Ink.Outlined(this, font, new Vector2(x, y), line, size, ink, Ink.Black, 4);
            }
            else
            {
                Ink.Text(this, font, new Vector2(x, y), line, size, ink);
            }

            y += lineHeight;
        }

        if (Disabled)
        {
            // Desactivado: la placa entera se apaga bajo un velo de papel, icono incluido.
            DrawColoredPolygon(Ink.Rough(rect, 1.5f, Seed), new Color(Ink.PaperDark, 0.6f));
        }
    }
}
