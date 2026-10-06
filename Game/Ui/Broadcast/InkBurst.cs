using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Sello de lacre en estrella (<see cref="Pregon.Burst"/>), centrado en su rectángulo. Relleno provisional: se
/// oculta y se pone un <see cref="TextureRect"/> con el sello dibujado.
/// </summary>
[Tool]
[GlobalClass]
public partial class InkBurst : Control
{
    private float _outer = 48f;
    private float _inner = 43f;
    private int _points = 22;
    private Color _fill = Pregon.Wax;
    private Color _edge = Pregon.GulesDark;
    private float _edgeWidth = 1.5f;

    [Export]
    public float Outer { get => _outer; set { _outer = value; QueueRedraw(); } }

    [Export]
    public float Inner { get => _inner; set { _inner = value; QueueRedraw(); } }

    [Export]
    public int Points { get => _points; set { _points = value; QueueRedraw(); } }

    [Export]
    public Color Fill { get => _fill; set { _fill = value; QueueRedraw(); } }

    [Export]
    public Color Edge { get => _edge; set { _edge = value; QueueRedraw(); } }

    [Export]
    public float EdgeWidth { get => _edgeWidth; set { _edgeWidth = value; QueueRedraw(); } }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (_points < 2)
        {
            return;
        }

        var pts = Pregon.Burst(_outer, _inner, _points, Size / 2f);
        DrawColoredPolygon(pts, _fill);
        if (_edgeWidth > 0f)
        {
            var closed = new Vector2[pts.Length + 1];
            System.Array.Copy(pts, closed, pts.Length);
            closed[pts.Length] = pts[0];
            DrawPolyline(closed, _edge, _edgeWidth, true);
        }
    }
}
