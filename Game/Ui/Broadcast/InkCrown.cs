using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Corona de tres puntas (<see cref="Pregon.CrownPoly"/>) que cierra el estandarte, ocupando su rectángulo.
/// Relleno provisional: se oculta y se pone un <see cref="TextureRect"/> con la corona dibujada.
/// </summary>
[Tool]
[GlobalClass]
public partial class InkCrown : Control
{
    private Color _fill = Pregon.Or;

    [Export]
    public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var poly = Pregon.CrownPoly(Size.X, Size.Y);
        DrawColoredPolygon(poly, _fill);
        var closed = new Vector2[poly.Length + 1];
        System.Array.Copy(poly, closed, poly.Length);
        closed[poly.Length] = poly[0];
        DrawPolyline(closed, Pregon.InkBrown, 1.6f, true);
    }
}
