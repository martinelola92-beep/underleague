using Godot;
using Underleague.Game.Ui.Broadcast;

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
/// <para>
/// <b>Voz de pregón (encargo pregon-resto):</b> desde el 20 sep 2026 <see cref="Panel"/> dibuja pergamino
/// con borde rasgado (<see cref="ParchmentPanel"/>), <see cref="Header"/> dibuja un tablero de madera, y
/// <see cref="BuildLegacyTheme"/> —aplicado por <see cref="Layout.CenterLegacy"/> a toda pantalla vieja—
/// pone IM Fell en los títulos, Barlow Condensed en botones y datos, y la placa de pergamino de los
/// botones, <b>incluidos</b> los pocos botones que vienen ya maquetados en un <c>.tscn</c>
/// (<c>Equipo.tscn</c>) en vez de nacer aquí: por eso el estilo del botón vive en el <c>Theme</c> del
/// tipo base <c>Button</c> y no en cada instancia — un <c>Theme</c> alcanza a los hijos de la escena,
/// una llamada a <see cref="Button"/> no.
/// </para>
/// </summary>
public static class Widgets
{
    /// <summary>Alto de la cabecera de todas las pantallas (misma que Equipo).</summary>
    public const int HeaderHeight = 52;

    /// <summary>Ancho de la columna de fichas (<c>ui-equipo.md</c> §2: la ficha no cambia de ancho).</summary>
    public const int CardColumnWidth = 376;

    private static Theme? _legacyTheme;

    /// <summary>Fondo de pantalla completo: madera oscura, la estructura persistente del marco.</summary>
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

    /// <summary>
    /// Panel de fondo de una zona de la pantalla: pergamino con borde rasgado
    /// (<see cref="ParchmentPanel"/>), salvo <paramref name="parchment"/> a <c>false</c> para lo que no
    /// es papel — una cortina modal semitransparente o una raya divisoria de 1 px, donde el rasgado no
    /// tiene sentido y en el segundo caso ni siquiera se puede calcular con un alto de un solo píxel.
    /// </summary>
    public static Control Panel(Control parent, Rect2 area, Color? color = null, bool parchment = true)
    {
        var fill = color ?? Style.Panel;
        if (!parchment)
        {
            var flat = new ColorRect
            {
                Color = fill,
                Position = area.Position,
                Size = area.Size,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            parent.AddChild(flat);
            return flat;
        }

        var panel = new ParchmentPanel
        {
            Position = area.Position,
            Size = area.Size,
        };
        parent.AddChild(panel);

        // La semilla es función de la posición y el tamaño del panel, nunca de System.Random (misma
        // disciplina que RT-021, aunque /Game no está sujeto a ella): dos paneles en el mismo sitio se
        // rasgan igual, así que una captura de referencia sigue siendo comparable.
        int seed = (int)((area.Position.X * 7f) + (area.Position.Y * 13f) + (area.Size.X * 3f) + (area.Size.Y * 5f));
        panel.Configure(fill, Style.Line, seed);
        return panel;
    }

    /// <summary>Título: el tamaño grande de UI-004, en IM Fell. Tinta parda salvo que se pida otro color.</summary>
    public static Label Title(Control parent, string text, Vector2 at, float width = 600f, Color? color = null) =>
        Label(parent, text, at, width, Style.TextLarge, color ?? Style.Text, Pregon.Fell);

    /// <summary>Cuerpo de texto: el tamaño pequeño de UI-004, en Barlow Condensed.</summary>
    public static Label Body(Control parent, string text, Vector2 at, float width = 600f, Color? color = null) =>
        Label(parent, text, at, width, Style.TextSmall, color ?? Style.Text, Pregon.DataSemiBold);

    /// <summary>
    /// Etiqueta de sección: cuerpo en IM Fell y en rubrica —el rojo de lacre con el que un manuscrito
    /// destaca sus cabeceras—, que se lee mejor sobre pergamino que el dorado de <see cref="Style.Accent"/>.
    /// </summary>
    public static Label Section(Control parent, string text, Vector2 at, float width = 600f) =>
        Label(parent, text, at, width, Style.TextSmall, Pregon.Wax, Pregon.Fell);

    private static Label Label(Control parent, string text, Vector2 at, float width, int size, Color color, Font font)
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
        label.AddThemeFontOverride("font", font);
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

    /// <summary>
    /// Botón de la interfaz. El texto va siempre en el tamaño pequeño (UI-004); la placa de pergamino, la
    /// tinta y la fuente salen del <c>Theme</c> del tipo base <c>Button</c> (<see cref="BuildLegacyTheme"/>),
    /// no de aquí, para que un botón maquetado a mano en un <c>.tscn</c> (Equipo) la herede igual.
    /// </summary>
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
    /// Cabecera común: tablero de madera de <see cref="HeaderHeight"/> con título dorado a la izquierda y
    /// subtítulo al lado, como en Equipo. El subtítulo se devuelve para que la pantalla lo actualice
    /// cuando el estado cambie.
    /// </summary>
    public static Label Header(Control parent, string title, string subtitle)
    {
        Panel(parent, new Rect2(0f, 0f, Layout.LegacySize.X, HeaderHeight), Style.Background, parchment: false);
        var seam = new ColorRect
        {
            Color = Style.Line,
            Position = new Vector2(0f, HeaderHeight - 2f),
            Size = new Vector2(Layout.LegacySize.X, 2f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(seam);

        Title(parent, title, new Vector2(16f, 12f), 300f, Style.Accent);
        var label = Body(parent, subtitle, new Vector2(200f, 18f), 1060f, Style.OnWood);
        return label;
    }

    /// <summary>
    /// Ayuda de entrada al pie, dos líneas (ratón y mando): la prueba visible de que los dos flujos de
    /// UI-006 existen. El mando no está implementado fuera de Equipo todavía y la línea lo dice, en vez
    /// de prometerlo. Vive en el margen bajo la última fila de paneles, sobre la madera del fondo —no
    /// sobre pergamino—, así que su tinta tiene que ser clara (<see cref="Style.OnWood"/>).
    /// </summary>
    public static void InputHelp(Control parent, string mouse, string pad)
    {
        Body(parent, mouse, new Vector2(16f, 758f), 1248f, Style.OnWood);
        Body(parent, pad, new Vector2(16f, 776f), 1248f, Style.OnWood);
    }

    /// <summary>
    /// Theme de pregón para las pantallas viejas fuera del partido: fuentes por defecto, tinta y la placa
    /// de pergamino de todo <c>Button</c> del árbol —código o <c>.tscn</c>—, para que hasta los pocos
    /// botones que vienen ya maquetados a mano en una escena (<c>BotonZonas</c>/<c>BotonCobertura</c> de
    /// <c>Equipo.tscn</c>) hablen el mismo idioma sin que la pantalla tenga que estilarlos uno a uno.
    /// Separado de <see cref="Pregon.BuildTheme"/> (la retransmisión, con sus propios tamaños de
    /// <c>Label</c>) para no arrastrarle cambios a <c>BroadcastScreen</c>.
    /// </summary>
    public static Theme BuildLegacyTheme()
    {
        if (_legacyTheme is not null)
        {
            return _legacyTheme;
        }

        var theme = new Theme
        {
            DefaultFont = Pregon.DataSemiBold,
            DefaultFontSize = Style.TextSmall,
        };

        theme.SetColor("font_color", "Label", Style.Text);
        theme.SetFont("font", "Button", Pregon.DataBold);
        theme.SetColor("font_color", "Button", Style.Text);
        theme.SetColor("font_hover_color", "Button", Style.Text);
        theme.SetColor("font_pressed_color", "Button", Style.Text);
        theme.SetColor("font_focus_color", "Button", Style.Text);
        theme.SetColor("font_disabled_color", "Button", new Color(Style.Text, 0.55f));

        theme.SetStylebox("normal", "Button", PlaqueBox(Style.Panel));
        theme.SetStylebox("hover", "Button", PlaqueBox(Style.PanelSoft));
        theme.SetStylebox("pressed", "Button", PlaqueBox(Style.Panel.Darkened(0.12f)));
        theme.SetStylebox("disabled", "Button", PlaqueBox(new Color(Style.Panel, 0.55f)));

        var focus = PlaqueBox(Style.Panel);
        focus.BorderColor = Style.Accent;
        theme.SetStylebox("focus", "Button", focus);

        // El cuadro de semilla de Inicio es el único LineEdit de las pantallas viejas: sin esto se queda
        // con el gris nativo de Godot, que desentona con el resto de la placa de pergamino.
        theme.SetFont("font", "LineEdit", Pregon.DataSemiBold);
        theme.SetColor("font_color", "LineEdit", Style.Text);
        theme.SetColor("caret_color", "LineEdit", Style.Text);
        theme.SetColor("selection_color", "LineEdit", new Color(Style.Accent, 0.45f));
        theme.SetStylebox("normal", "LineEdit", PlaqueBox(Style.PanelSoft));
        theme.SetStylebox("focus", "LineEdit", focus);
        theme.SetStylebox("read_only", "LineEdit", PlaqueBox(new Color(Style.PanelSoft, 0.7f)));

        _legacyTheme = theme;
        return theme;
    }

    /// <summary>Placa de pergamino de un botón: relleno plano, orla de borde y una sombra corta.</summary>
    private static StyleBoxFlat PlaqueBox(Color fill) => new()
    {
        BgColor = fill,
        BorderColor = Style.Line,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ShadowSize = 3,
        ShadowColor = new Color(0f, 0f, 0f, 0.35f),
        ShadowOffset = new Vector2(2f, 3f),
        ContentMarginLeft = 8f,
        ContentMarginRight = 8f,
        ContentMarginTop = 4f,
        ContentMarginBottom = 4f,
    };
}
