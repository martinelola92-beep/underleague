using Godot;
using Underleague.Game.Ui.Broadcast;

namespace Underleague.Game.Ui;

/// <summary>
/// Etiqueta de texto para los componentes del encargo <c>mapa-pregon</c> (leyenda del mapa, periódico):
/// ese encargo pide texto esencial <b>&gt;= 13 px reales a 1280x800</b>, un mínimo más estricto que el de
/// <see cref="Style.TextSmall"/> (12 px, UI-004 general) que usa <see cref="Widgets.Body"/> — así que no
/// vale reutilizar <see cref="Widgets.Body"/> aquí sin bajar de ese mínimo. Es la misma construcción que
/// el <c>Label</c> privado de <see cref="Widgets"/> (ancho fijado tras entrar en el árbol, alto recalculado
/// con las líneas ya envueltas), para que el resto de la pantalla apile etiquetas sin que se monten unas
/// sobre otras.
/// </summary>
public static class EssentialLabel
{
    /// <summary>Tamaño mínimo de este encargo: 13 px reales a 1280x800.</summary>
    public const int Size = 13;

    /// <summary>Cuerpo de texto esencial, en Barlow Condensed SemiBold (la misma familia que <see cref="Widgets.Body"/>).</summary>
    public static Label Body(Control parent, string text, Vector2 at, float width, Color? color = null, int size = Size) =>
        Make(parent, text, at, width, size, color ?? Style.Text, Pregon.DataSemiBold);

    /// <summary>Título corto, en Barlow Condensed Bold: el nombre del tipo de nodo, la cabecera del periódico.</summary>
    public static Label Title(Control parent, string text, Vector2 at, float width, Color? color = null, int size = Size) =>
        Make(parent, text, at, width, size, color ?? Style.Text, Pregon.DataBold);

    private static Label Make(Control parent, string text, Vector2 at, float width, int size, Color color, Font font)
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

        label.CustomMinimumSize = new Vector2(width, 0f);
        label.Size = new Vector2(width, 0f);
        label.Size = new Vector2(width, Mathf.Max(1, label.GetLineCount()) * label.GetLineHeight(size));
        return label;
    }
}
