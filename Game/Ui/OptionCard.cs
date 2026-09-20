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
/// impone la pantalla y no la ficha. La usan Recompensa, Mercado e Informe.
/// </para>
/// <para>
/// <b>Dos tamaños, nunca dos diseños</b> (decisión del revisor, 20 sep 2026): <see cref="Bind"/> con
/// <c>large: true</c> agranda el distintivo y sube el nombre y el precio/coste a <see cref="Style.TextLarge"/>
/// —la "carta grande" que usa Recompensa al elegir—, pero es la misma cabecera, el mismo cuerpo y la
/// misma señal <see cref="ActivatedEventHandler"/>; ningún llamador existente tiene que cambiar, porque
/// <c>large</c> por defecto es <c>false</c>.
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
    private bool _large;
    private float _lastWidth;

    /// <summary>
    /// El nombre partido en líneas (revisión del revisor, 20 sep 2026: ningún nombre se corta sin «…», y
    /// a ser posible no se corta en absoluto): la primera línea comparte fila con el distintivo y el
    /// precio, así que se envuelve al ancho que le deja esa fila; el resto usa el ancho entero de la
    /// carta. Se recalcula en <see cref="Relayout"/> —de ahí sale <see cref="HeaderHeight"/>— y
    /// <see cref="_Draw"/> pinta exactamente estas líneas, nunca <see cref="_title"/> directamente.
    /// </summary>
    private readonly List<string> _titleLines = new();

    /// <summary>Alto de la línea de continuación del nombre, por tamaño: igual que la fila del distintivo (grande) o que <see cref="LineHeight"/> (chica).</summary>
    private float TitleContinuationHeight => _large ? 20f : LineHeight;

    /// <summary>
    /// Cabecera de 24 px de UI-011 en la tira; 40 px con distintivo y nombre más grandes en la carta
    /// grande (<see cref="Bind"/>, <c>large</c>). Mismo componente, dos tamaños, nunca dos diseños
    /// (decisión del revisor, 20 sep 2026): la carta grande es la que usa Recompensa al elegir, con sitio
    /// de sobra para leer sin tener que expandir. Crece una línea de <see cref="TitleContinuationHeight"/>
    /// por cada línea que el nombre necesite de más allá de la primera (<see cref="_titleLines"/>): un
    /// nombre largo del catálogo (<c>data/perks</c>, <c>data/items</c>) nunca se corta, se envuelve.
    /// </summary>
    private float HeaderHeight => (_large ? 40f : Style.CollapsedHeight)
        + (Mathf.Max(0, _titleLines.Count - 1) * TitleContinuationHeight);

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
        bool alwaysOpen = false,
        bool large = false)
    {
        Index = index;
        _badge = badge;
        _badgeColor = badgeColor;
        _title = title;
        _right = right;
        _headline = headline;
        _description = description;
        _alwaysOpen = alwaysOpen;
        _large = large;
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
        CustomMinimumSize = new Vector2(0f, HeaderHeight);
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
        // así que el tipo de artículo se lee sin depender del color. Mismo diseño en los dos tamaños; la
        // carta grande (Recompensa) solo lo agranda y sube el nombre y el precio a la letra de título.
        if (_large)
        {
            DrawRect(new Rect2(8f, 8f, 20f, 20f), _dimmed ? new Color(_badgeColor, 0.45f) : _badgeColor);
            Style.DrawText(this, font, new Vector2(32f, 12f), _badge, Style.TextSmall, _dimmed ? Style.TextDim : _badgeColor);

            float largeRightWidth = _right.Length == 0
                ? 0f
                : font.GetStringSize(_right, HorizontalAlignment.Left, -1f, Style.TextLarge).X;
            const float largeTitleLeft = 80f;
            if (_titleLines.Count > 0)
            {
                Style.DrawText(this, font, new Vector2(largeTitleLeft, 8f), _titleLines[0], Style.TextLarge, text);
            }

            if (_right.Length > 0)
            {
                Style.DrawText(this, font, new Vector2(width - largeRightWidth - Padding, 8f), _right, Style.TextLarge, _dimmed ? Style.TextDim : Style.Accent);
            }

            float continuationY = 30f;
            for (int i = 1; i < _titleLines.Count; i++)
            {
                Style.DrawText(this, font, new Vector2(Padding, continuationY), _titleLines[i], Style.TextLarge, text);
                continuationY += TitleContinuationHeight;
            }
        }
        else
        {
            DrawRect(new Rect2(6f, 7f, 10f, 10f), _dimmed ? new Color(_badgeColor, 0.45f) : _badgeColor);
            Style.DrawText(this, font, new Vector2(22f, 5f), _badge, Style.TextSmall, _dimmed ? Style.TextDim : _badgeColor);

            float rightWidth = _right.Length == 0
                ? 0f
                : font.GetStringSize(_right, HorizontalAlignment.Left, -1f, Style.TextSmall).X;
            float titleLeft = 22f + 42f;
            if (_titleLines.Count > 0)
            {
                Style.DrawText(this, font, new Vector2(titleLeft, 5f), _titleLines[0], Style.TextSmall, text);
            }

            if (_right.Length > 0)
            {
                Style.DrawText(this, font, new Vector2(width - rightWidth - Padding, 5f), _right, Style.TextSmall, _dimmed ? Style.TextDim : Style.Accent);
            }

            float continuationY = 19f;
            for (int i = 1; i < _titleLines.Count; i++)
            {
                Style.DrawText(this, font, new Vector2(Padding, continuationY), _titleLines[i], Style.TextSmall, text);
                continuationY += TitleContinuationHeight;
            }
        }

        if (!_expanded)
        {
            return;
        }

        float y = HeaderHeight + (_large ? 6f : 4f);
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

    /// <summary>
    /// Envuelve el nombre en líneas (ver <see cref="_titleLines"/>): la primera al ancho que le deja la
    /// fila del distintivo y el precio; el resto —solo si hace falta más de una— al ancho entero de la
    /// carta, que es mucho más generoso. Dos pasadas de <see cref="Style.Wrap"/> en vez de una sola al
    /// ancho estrecho, para que un nombre de tres palabras no parta cada una en su propia línea.
    /// </summary>
    private void RecomputeTitleLines(Font font, float width)
    {
        _titleLines.Clear();
        if (_title.Length == 0)
        {
            _titleLines.Add(string.Empty);
            return;
        }

        int size = _large ? Style.TextLarge : Style.TextSmall;
        float rightWidth = _right.Length == 0 ? 0f : font.GetStringSize(_right, HorizontalAlignment.Left, -1f, size).X;
        float titleLeft = _large ? 80f : 22f + 42f;
        float firstLineWidth = width - titleLeft - rightWidth - 12f;
        float continuationWidth = width - (Padding * 2);

        // Style.Wrap fuerza la primera palabra en su línea aunque no quepa, para no devolver una línea
        // vacía (tiene que hacer progreso): eso es correcto al ancho entero de la carta, pero la fila del
        // distintivo es estrecha y una sola palabra larga ("Especialista") no cabe ahí. Sin este aviso, esa
        // línea se dibujaría más ancha de lo que la fila permite y el nombre se leería cortado a media
        // palabra — exactamente lo que esta revisión prohíbe. Si ni la primera palabra cabe, esa fila se
        // deja sin nombre y el nombre entero se envuelve al ancho generoso de abajo, desde la línea 0.
        string firstWord = _title.Split(' ')[0];
        bool firstWordFits = font.GetStringSize(firstWord, HorizontalAlignment.Left, -1f, size).X <= firstLineWidth;
        if (!firstWordFits)
        {
            _titleLines.Add(string.Empty);
            _titleLines.AddRange(Style.Wrap(font, _title, size, continuationWidth));
            return;
        }

        var firstPass = Style.Wrap(font, _title, size, firstLineWidth);
        if (firstPass.Count <= 1)
        {
            _titleLines.AddRange(firstPass);
            return;
        }

        _titleLines.Add(firstPass[0]);
        string remainder = string.Join(" ", firstPass.GetRange(1, firstPass.Count - 1));
        _titleLines.AddRange(Style.Wrap(font, remainder, size, continuationWidth));
    }

    /// <summary>Recalcula el alto según el estado; el contenedor se recoloca solo.</summary>
    private void Relayout()
    {
        var wrapFont = GetThemeDefaultFont();
        float wrapWidth = Size.X > 0f ? Size.X : 296f;
        RecomputeTitleLines(wrapFont, wrapWidth);

        float height = HeaderHeight;
        if (_expanded || _alwaysOpen)
        {
            float textWidth = wrapWidth - (Padding * 2);
            height += _large ? 6f : 4f;
            if (_headline.Length > 0)
            {
                height += Style.Wrap(wrapFont, _headline, Style.TextSmall, textWidth).Count * LineHeight;
            }

            if (_description.Length > 0)
            {
                height += Style.Wrap(wrapFont, _description, Style.TextSmall, textWidth).Count * LineHeight;
            }

            foreach (string note in _notes)
            {
                height += Style.Wrap(wrapFont, note, Style.TextSmall, textWidth).Count * LineHeight;
            }

            height += Padding;
        }

        CustomMinimumSize = new Vector2(0f, height);
        QueueRedraw();
    }
}
