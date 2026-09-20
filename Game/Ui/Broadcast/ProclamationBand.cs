using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Banda de pregón a lo ancho (N3 de <c>docs/ui/README.md</c> §4): turba y árbitro que abandona el campo,
/// <b>sin congelar</b> el partido. Es la única presentación de N3 que no es un gonfalón vertical: se lee de
/// un vistazo mientras el mundo sigue corriendo por debajo.
/// </summary>
public partial class ProclamationBand : Control
{
    public const float DesignHeight = 100f;

    private string _header = string.Empty;
    private string _body = string.Empty;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public void Show(string header, string body)
    {
        _header = header;
        _body = body;
        Visible = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = Size.X;
        var pts = new[]
        {
            new Vector2(0, 0), new Vector2(w, 0), new Vector2(w - 30f, 50f), new Vector2(w, 100f),
            new Vector2(0, 100f), new Vector2(30f, 50f),
        };
        var shadow = new Vector2[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            shadow[i] = pts[i] + new Vector2(0f, 6f);
        }

        DrawColoredPolygon(shadow, new Color(0f, 0f, 0f, 0.25f));
        DrawColoredPolygon(pts, Pregon.Vellum);
        var closed = new Vector2[pts.Length + 1];
        System.Array.Copy(pts, closed, pts.Length);
        closed[pts.Length] = pts[0];
        DrawPolyline(closed, Pregon.VellumEdge, 2f, true);
        DrawLine(new Vector2(40f, 12f), new Vector2(w - 40f, 12f), Pregon.Gules, 2f);
        DrawLine(new Vector2(40f, 88f), new Vector2(w - 40f, 88f), Pregon.Gules, 2f);

        Pregon.DrawTrumpet(this, new Vector2(48f, 46f), 240f, 2f);

        Style.DrawText(this, Pregon.Titular, new Vector2(380f, 14f), _header, Pregon.SizeHeader, Pregon.Sable, maxWidth: w - 760f);
        Style.DrawText(this, Pregon.SerifItalic, new Vector2(380f, 56f), _body, Pregon.SizeBody, Pregon.Gules, maxWidth: w - 760f);
    }
}
