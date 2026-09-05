using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Artículo o recompensa que se puede inspeccionar y elegir: una <b>tira de 24 px</b> con su distintivo,
/// su nombre y su precio, que al activarse se expande y enseña la descripción generada.
/// <para>
/// Es la ficha de jugador de <c>ui-equipo.md</c> §4 aplicada a lo que no es un jugador: el mismo alto de
/// tira (UI-011), los mismos dos tamaños de texto (UI-004), el mismo patrón de inspección —activar
/// expande, activar otra vez colapsa (UI-001)— y la misma regla de una sola expandida a la vez, que la
/// impone la pantalla y no la ficha. La usan Recompensa y Mercado.
/// </para>
/// </summary>
public partial class OptionCard : Control
{
    private const int Padding = 8;
    private const int LineHeight = 14;

    private readonly List<string> _notes = new();

    private string _badge = string.Empty;
    private Color _badgeColor = Style.TextDim;
    private string _title = string.Empty;
    private string _right = string.Empty;
    private string _headline = string.Empty;
    private string _description = string.Empty;
    private bool _expanded;
    private bool _selected;
    private bool _dimmed;
    private bool _alwaysOpen;
    private float _lastWidth;

    /// <summary>La ficha ha sido activada: un clic o el botón de acción (UI-001, mismo gesto).</summary>
    [Signal]
    public delegate void ActivatedEventHandler(int index);

    /// <summary>Índice del artículo dentro de su lista: es el que viaja en la decisión de <c>/Sim</c>.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>Expandida (UI-012): enseña cabecera, descripción y avisos. Solo una a la vez por pantalla.</summary>
    public bool Expanded
    {
        get => _expanded;
        set
        {
            // Una ficha declarada siempre abierta no se colapsa: es como se destaca lo que el jugador
            // tiene que ver sin tener que pulsarlo (RF-114b, el canterano gratuito).
            if (_expanded == value || (_alwaysOpen && !value))
            {
                return;
            }

            _expanded = value;
            Relayout();
        }
    }

    /// <summary>Marca de selección: la que el botón de acción de la pantalla va a usar.</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            QueueRedraw();
        }
    }

    /// <summary>Apagada: se puede leer pero no elegir (sin oro, sin hueco, sin portador).</summary>
    public bool Dimmed
    {
        get => _dimmed;
        set
        {
            _dimmed = value;
            QueueRedraw();
        }
    }

    /// <summary>Rellena la ficha. Todo el texto llega ya compuesto: la ficha no sabe de dónde sale.</summary>
    public void Bind(
        int index,
        string badge,
        Color badgeColor,
        string title,
        string right,
        string headline,
        string description,
        IReadOnlyList<string>? notes = null,
        bool alwaysOpen = false)
    {
        Index = index;
        _badge = badge;
        _badgeColor = badgeColor;
        _title = title;
        _right = right;
        _headline = headline;
        _description = description;
        _alwaysOpen = alwaysOpen;
        _notes.Clear();
        if (notes is not null)
        {
            _notes.AddRange(notes);
        }

        if (alwaysOpen)
        {
            _expanded = true;
        }

        Relayout();
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(0f, Style.CollapsedHeight);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && !Mathf.IsEqualApprox(_lastWidth, Size.X))
        {
            _lastWidth = Size.X;
            Relayout();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            EmitSignal(SignalName.Activated, Index);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        float width = Size.X;
        DrawRect(new Rect2(Vector2.Zero, Size), _expanded ? Style.PanelSoft : Style.Panel);

        if (_selected)
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Style.Accent, false, 1f);
        }

        var text = _dimmed ? Style.TextDim : Style.Text;

        // Distintivo: color y forma juntos (UI-002). El cuadrado de color lleva al lado sus tres letras,
        // así que el tipo de artículo se lee sin depender del color.
        DrawRect(new Rect2(6f, 7f, 10f, 10f), _dimmed ? new Color(_badgeColor, 0.45f) : _badgeColor);
        Style.DrawText(this, font, new Vector2(22f, 5f), _badge, Style.TextSmall, _dimmed ? Style.TextDim : _badgeColor);

        float rightWidth = _right.Length == 0
            ? 0f
            : font.GetStringSize(_right, HorizontalAlignment.Left, -1f, Style.TextSmall).X;
        float titleLeft = 22f + 42f;
        Style.DrawText(this, font, new Vector2(titleLeft, 5f), _title, Style.TextSmall, text, width - titleLeft - rightWidth - 12f);

        if (_right.Length > 0)
        {
            Style.DrawText(this, font, new Vector2(width - rightWidth - Padding, 5f), _right, Style.TextSmall, _dimmed ? Style.TextDim : Style.Accent);
        }

        if (!_expanded)
        {
            return;
        }

        float y = Style.CollapsedHeight + 4f;
        float textWidth = width - (Padding * 2);

        if (_headline.Length > 0)
        {
            foreach (string line in Style.Wrap(font, _headline, Style.TextSmall, textWidth))
            {
                Style.DrawText(this, font, new Vector2(Padding, y), line, Style.TextSmall, Style.TextDim);
                y += LineHeight;
            }
        }

        if (_description.Length > 0)
        {
            foreach (string line in Style.Wrap(font, _description, Style.TextSmall, textWidth))
            {
                Style.DrawText(this, font, new Vector2(Padding, y), line, Style.TextSmall, text);
                y += LineHeight;
            }
        }

        foreach (string note in _notes)
        {
            foreach (string line in Style.Wrap(font, note, Style.TextSmall, textWidth))
            {
                Style.DrawText(this, font, new Vector2(Padding, y), line, Style.TextSmall, Style.Accent);
                y += LineHeight;
            }
        }
    }

    /// <summary>Recalcula el alto según el estado; el contenedor se recoloca solo.</summary>
    private void Relayout()
    {
        float height = Style.CollapsedHeight;
        if (_expanded || _alwaysOpen)
        {
            var font = GetThemeDefaultFont();
            float width = Size.X > 0f ? Size.X : 296f;
            float textWidth = width - (Padding * 2);
            height += 4f;
            if (_headline.Length > 0)
            {
                height += Style.Wrap(font, _headline, Style.TextSmall, textWidth).Count * LineHeight;
            }

            if (_description.Length > 0)
            {
                height += Style.Wrap(font, _description, Style.TextSmall, textWidth).Count * LineHeight;
            }

            foreach (string note in _notes)
            {
                height += Style.Wrap(font, note, Style.TextSmall, textWidth).Count * LineHeight;
            }

            height += Padding;
        }

        CustomMinimumSize = new Vector2(0f, height);
        QueueRedraw();
    }
}
