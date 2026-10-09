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
/// pone Grenze Gotisch en los títulos, Barlow Condensed en botones y datos, y la placa de pergamino de los
/// botones, <b>incluidos</b> los pocos botones que vienen ya maquetados en un <c>.tscn</c>
/// (<c>Equipo.tscn</c>) en vez de nacer aquí: por eso el estilo del botón vive en el <c>Theme</c> del
/// tipo base <c>Button</c> y no en cada instancia — un <c>Theme</c> alcanza a los hijos de la escena,
/// una llamada a <see cref="Button"/> no.
/// </para>
/// </summary>
public static partial class Widgets
{
    /// <summary>Alto de la cabecera de todas las pantallas (misma que Equipo).</summary>
    public const int HeaderHeight = 52;

    /// <summary>Ancho de la columna de fichas (<c>ui-equipo.md</c> §2: la ficha no cambia de ancho).</summary>
    public const int CardColumnWidth = 376;

    private static Theme? _legacyTheme;

    /// <summary>Fondo de pantalla completo: madera oscura, la estructura persistente del marco.</summary>
    public static Control Background(Control parent)
    {
        // Pase de arte (9 oct): la misma mesa de tablones que Equipo, con veta (WoodTable pinta plano si falta la textura).
        var table = new WoodTable { MouseFilter = Control.MouseFilterEnum.Ignore };
        table.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(table);
        return table;
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

    /// <summary>
    /// Título: el tamaño grande de UI-004, en Grenze Gotisch —la voz que proclama (decisión del revisor,
    /// 20 sep 2026: sustituye a IM Fell en ese papel)—. Tinta parda salvo que se pida otro color.
    /// </summary>
    public static Label Title(Control parent, string text, Vector2 at, float width = 600f, Color? color = null) =>
        Label(parent, text, at, width, Style.TextLarge, color ?? Style.Text, Pregon.Titular);

    /// <summary>Cuerpo de texto: el tamaño pequeño de UI-004, en Barlow Condensed.</summary>
    public static Label Body(Control parent, string text, Vector2 at, float width = 600f, Color? color = null) =>
        Label(parent, text, at, width, Style.TextSmall, color ?? Style.Text, Pregon.DataSemiBold);

    /// <summary>
    /// Etiqueta de sección: se lee seguido, no se proclama (a 12 px la gótica de <see cref="Pregon.Titular"/>
    /// pierde legibilidad) — en Grenze (<see cref="Pregon.Serif"/>) y en rubrica —el rojo de lacre con el
    /// que un manuscrito destaca sus cabeceras—, que se lee mejor sobre pergamino que el dorado de
    /// <see cref="Style.Accent"/>.
    /// </summary>
    public static Label Section(Control parent, string text, Vector2 at, float width = 600f)
    {
        // Pase de arte (9 oct): la cabecera de bloque es un brochazo rojo con la letra en papel, como TITULARES en
        // Equipo y en las referencias. El brochazo va detrás, a la medida del texto, y la etiqueta sigue midiendo lo
        // mismo para quien apila debajo.
        var label = Label(parent, text.ToUpperInvariant(), at + new Vector2(10f, 0f), width - 10f, Style.TextSmall, Knavall.Ink.Paper, Knavall.Ink.Heavy);
        label.AddThemeColorOverride("font_outline_color", Knavall.Ink.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        float textWidth = Knavall.Ink.Width(Knavall.Ink.Heavy, label.Text, Style.TextSmall);
        var brush = new BrushBack
        {
            Position = at + new Vector2(0f, -3f),
            Size = new Vector2(Mathf.Min(textWidth + 34f, width), label.Size.Y + 6f),
            Seed = (int)(at.X * 3f + at.Y * 7f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(brush);
        parent.MoveChild(brush, label.GetIndex());
        return label;
    }

    /// <summary>El brochazo de detrás de una cabecera de bloque (<see cref="Section"/>).</summary>
    private sealed partial class BrushBack : Control
    {
        public int Seed { get; set; }

        public override void _Draw() => Knavall.Ink.Brush(this, new Rect2(Vector2.Zero, Size), Knavall.Ink.Red, Seed);
    }

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
        // Pase de arte (9 oct): tablón de cabecera con veta y clavos, como el de Equipo.
        parent.AddChild(new HeaderPlank
        {
            Position = Vector2.Zero,
            Size = new Vector2(Layout.LegacySize.X, HeaderHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var heading = Title(parent, title, new Vector2(16f, 10f), 300f, Knavall.Ink.Ochre);
        heading.AddThemeColorOverride("font_outline_color", Knavall.Ink.Black);
        heading.AddThemeConstantOverride("outline_size", 6);
        var label = Body(parent, subtitle, new Vector2(200f, 18f), 1060f, Style.OnWood);
        return label;
    }

    /// <summary>El tablón de la cabecera común (<see cref="Header"/>).</summary>
    private sealed partial class HeaderPlank : Control
    {
        public override void _Draw() => Knavall.Ink.Plank(this, new Rect2(-6f, -6f, Size.X + 12f, Size.Y + 4f), 901, Knavall.Ink.Wood, nails: false);
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
        theme.SetStylebox("hover", "Button", PlaqueBox(Knavall.Ink.PaperWarm));
        theme.SetStylebox("pressed", "Button", PlaqueBox(Knavall.Ink.Ochre));
        theme.SetStylebox("disabled", "Button", PlaqueBox(new Color(Style.Panel, 0.55f)));

        var focus = PlaqueBox(Knavall.Ink.Ochre);
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

    /// <summary>
    /// Placa de un botón. Pase de arte (9 oct): la de Equipo —papel con grano, contorno de tinta grueso y sombra dura—
    /// en vez del rectángulo de borde fino.
    /// </summary>
    private static StyleBox PlaqueBox(Color fill)
    {
        var box = new InkStyleBox
        {
            Fill = fill,
            Edge = Knavall.Ink.Black,
            EdgeWidth = 2.6f,
            Amplitude = 1.2f,
            Seed = 17,
            ShadowColor = Knavall.Ink.Shadow,
            ShadowOffset = new Vector2(3f, 4f),
        };
        box.ContentMarginLeft = 8f;
        box.ContentMarginRight = 8f;
        box.ContentMarginTop = 4f;
        box.ContentMarginBottom = 4f;
        return box;
    }
}
