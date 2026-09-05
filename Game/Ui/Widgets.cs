using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Las piezas sueltas con las que se montan las pantallas del esqueleto jugable: paneles, títulos,
/// cuerpos de texto y botones. Existe para que las decisiones de <c>docs/ui-equipo.md</c> —dos tamaños
/// de texto y ninguno más (UI-004), la paleta de <see cref="Style"/>, la ayuda de mandos al pie— se
/// apliquen solas y no haya que repetirlas pantalla a pantalla.
/// <para>
/// Las pantallas se montan <b>en código</b> y su <c>.tscn</c> es solo la raíz con el script: no hay
/// editor gráfico en este entorno (<c>docs/entorno.md</c>) y una escena de texto escrita a mano con
/// cuarenta nodos es ilegible y frágil. La composición sigue siendo la del documento: 1280x800, columna
/// de fichas de 376 px, ayuda de mandos abajo.
/// </para>
/// </summary>
public static class Widgets
{
    /// <summary>Alto de la cabecera de todas las pantallas (misma que Equipo).</summary>
    public const int HeaderHeight = 52;

    /// <summary>Ancho de la columna de fichas (<c>ui-equipo.md</c> §2: la ficha no cambia de ancho).</summary>
    public const int CardColumnWidth = 376;

    /// <summary>Fondo de pantalla completo.</summary>
    public static ColorRect Background(Control parent)
    {
        var rect = new ColorRect
        {
            Color = Style.Background,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(rect);
        return rect;
    }

    /// <summary>Panel de fondo de una zona de la pantalla.</summary>
    public static ColorRect Panel(Control parent, Rect2 area, Color? color = null)
    {
        var rect = new ColorRect
        {
            Color = color ?? Style.Panel,
            Position = area.Position,
            Size = area.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(rect);
        return rect;
    }

    /// <summary>Título: el tamaño grande de UI-004, en el color de acento.</summary>
    public static Label Title(Control parent, string text, Vector2 at, float width = 600f)
    {
        var label = Label(parent, text, at, width, Style.TextLarge, Style.Accent);
        return label;
    }

    /// <summary>Cuerpo de texto: el tamaño pequeño de UI-004.</summary>
    public static Label Body(Control parent, string text, Vector2 at, float width = 600f, Color? color = null) =>
        Label(parent, text, at, width, Style.TextSmall, color ?? Style.Text);

    /// <summary>Etiqueta de sección: cuerpo en color de acento.</summary>
    public static Label Section(Control parent, string text, Vector2 at, float width = 600f) =>
        Label(parent, text, at, width, Style.TextSmall, Style.Accent);

    private static Label Label(Control parent, string text, Vector2 at, float width, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            Position = at,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);

        // El ancho se fija **después** de entrar en el árbol: un Label recién creado calcula su tamaño
        // mínimo con el texto entero en una línea, y si se le pide antes, el ancho que se le da lo pisa
        // ese mínimo y el texto se sale del panel en vez de envolverse.
        label.CustomMinimumSize = new Vector2(width, 0f);
        label.Size = new Vector2(width, 0f);

        // Y el alto se calcula con las líneas que han salido de envolver, no con el mínimo: quien apila
        // etiquetas necesita saber cuánto ocupa esta de verdad, o la siguiente se le monta encima.
        label.Size = new Vector2(width, Mathf.Max(1, label.GetLineCount()) * label.GetLineHeight(size));
        return label;
    }

    /// <summary>Botón de la interfaz. El texto va siempre en el tamaño pequeño (UI-004).</summary>
    public static Button Button(Control parent, string text, Rect2 area, bool enabled = true)
    {
        var button = new Button
        {
            Text = text,
            Position = area.Position,
            Size = area.Size,
            Disabled = !enabled,
            ClipText = true,
        };
        button.AddThemeFontSizeOverride("font_size", Style.TextSmall);
        parent.AddChild(button);
        return button;
    }

    /// <summary>
    /// Cabecera común: título a la izquierda y subtítulo al lado, como en Equipo. El subtítulo se
    /// devuelve para que la pantalla lo actualice cuando el estado cambie.
    /// </summary>
    public static Label Header(Control parent, string title, string subtitle)
    {
        Title(parent, title, new Vector2(16f, 12f), 300f);
        var label = Body(parent, subtitle, new Vector2(200f, 18f), 1060f, Style.TextDim);
        return label;
    }

    /// <summary>
    /// Ayuda de entrada al pie, dos líneas (ratón y mando): la prueba visible de que los dos flujos de
    /// UI-006 existen. El mando no está implementado fuera de Equipo todavía y la línea lo dice, en vez
    /// de prometerlo.
    /// </summary>
    public static void InputHelp(Control parent, string mouse, string pad)
    {
        Body(parent, mouse, new Vector2(16f, 758f), 1248f, Style.TextDim);
        Body(parent, pad, new Vector2(16f, 776f), 1248f, Style.TextDim);
    }
}
