using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Esquina doblada de un pergamino: un triángulo del dorso del papel, algo más oscuro, con la línea del
/// pliegue en diagonal (encargo mapa-pregon: "el mapa vive sobre un pergamino con las esquinas
/// dobladas"). Se coloca <b>encima</b> del <see cref="ParchmentPanel"/> del mapa, ceñido a su esquina
/// superior derecha — no dibuja el resto del pergamino, que sigue siendo cosa de
/// <see cref="Broadcast.Pregon.DrawParchment"/>.
/// </summary>
public partial class FoldedCorner : Control
{
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        var a = new Vector2(0f, 0f);
        var b = new Vector2(Size.X, 0f);
        var c = new Vector2(Size.X, Size.Y);

        DrawColoredPolygon(new[] { a + new Vector2(3f, 3f), b + new Vector2(3f, 3f), c + new Vector2(3f, 3f) }, new Color(0f, 0f, 0f, 0.25f));
        DrawColoredPolygon(new[] { a, b, c }, Style.Panel.Darkened(0.18f));
        DrawLine(a, c, Style.Panel.Darkened(0.4f), 1.3f);
        DrawLine(a + new Vector2(2f, 1f), c + new Vector2(-1f, -2f), Style.PanelSoft, 1f);
    }
}
