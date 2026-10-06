using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Texto que debe caber en el ancho de su <see cref="Label"/> (títulos de estandarte, bando y acta). La fuente,
/// el tamaño preferido y el ancho son los de la escena; lo único que pone el código es el tamaño reducido
/// cuando el texto no cabe, que depende del dato (misma regla que <see cref="Pregon.DrawFittedTitle"/>).
/// </summary>
internal static class FitText
{
    private const string PreferredKey = "preferred_font_size";

    /// <summary>
    /// Pone <paramref name="text"/> y baja el tamaño de letra, hasta <see cref="Pregon.SizeEssential"/>, lo
    /// justo para que quepa en <paramref name="maxWidth"/> (o en el ancho del nodo si es negativo). El tamaño
    /// preferido es el que la escena dio al nodo la primera vez.
    /// </summary>
    public static void Fit(Label label, string text, float maxWidth = -1f)
    {
        label.Text = text;
        if (!label.HasMeta(PreferredKey))
        {
            label.SetMeta(PreferredKey, label.GetThemeFontSize("font_size"));
        }

        int preferred = label.GetMeta(PreferredKey).AsInt32();
        float width = maxWidth > 0f ? maxWidth : label.Size.X;
        var font = label.GetThemeFont("font");
        label.AddThemeFontSizeOverride("font_size", Pregon.FitTitleSize(font, text, preferred, width));
    }
}
