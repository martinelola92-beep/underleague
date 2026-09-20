using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Bando de muerte con lacre (N4 de <c>docs/ui/README.md</c> §4): «Se hace saber el fallecimiento de»,
/// nombre y cuerpo, a la izquierda; el sello de lacre en la esquina. Es el segundo tiempo de la muerte —
/// el campo ya contó el primero (cuerpo y mancha) antes de que este bando entre.
/// </summary>
public partial class Edict : Control
{
    public const float DesignWidth = 600f;
    public const float DesignHeight = 700f;

    private string _name = string.Empty;
    private string _body = string.Empty;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(DesignWidth, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <summary>«Se hace saber el fallecimiento de» y «el fallecimiento de» son furniture (ui.pregon.edict.*, RT-035); <paramref name="name"/> y <paramref name="body"/> llegan ya resueltos.</summary>
    public void Show(string name, string body)
    {
        _name = name;
        _body = body;
        Visible = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Pregon.DrawTrumpet(this, new Vector2(20f, 60f), 440f, -5f);

        var sheetAt = new Vector2(20f, 120f);
        Pregon.DrawParchment(this, sheetAt, 560f, 440f, Pregon.Vellum, Pregon.VellumEdge, seed: 900, amplitude: 3f, edgeWidth: 2f);
        DrawLine(sheetAt + new Vector2(40f, 64f), sheetAt + new Vector2(520f, 64f), Pregon.InkBrown, 1.5f);
        DrawLine(sheetAt + new Vector2(40f, 70f), sheetAt + new Vector2(520f, 70f), Pregon.InkBrown, 1f);

        Style.DrawText(this, Pregon.Titular, sheetAt + new Vector2(20f, 10f), UiText.Get("ui.pregon.edict.said"), Pregon.SizeHeader, Pregon.InkBrown, maxWidth: 520f);
        Style.DrawText(this, Pregon.SerifItalic, sheetAt + new Vector2(20f, 72f), UiText.Get("ui.pregon.edict.deathOf"), Pregon.SizeBody, Pregon.InkBrown, maxWidth: 520f);
        int nameSize = Pregon.FitTitleSize(Pregon.Titular, _name, Pregon.SizeTitleSmall - 8, 520f);
        Pregon.DrawTextEllipsized(this, Pregon.Titular, sheetAt + new Vector2(20f, 118f), _name, nameSize, Pregon.Sable, 520f);
        Pregon.DrawWrappedText(this, Pregon.SerifItalic, sheetAt + new Vector2(20f, 206f), _body, Pregon.SizeEssential + 4, Pregon.InkBrown, 520f);

        var wax = new Vector2(450f, sheetAt.Y + 340f);
        DrawColoredPolygon(ShiftedBurst(wax, 48f, 43f, 22), Pregon.Wax);
        DrawPolyline(ClosedBurst(wax, 48f, 43f, 22), Pregon.GulesDark, 1.5f, true);
    }

    private static Vector2[] ShiftedBurst(Vector2 center, float r1, float r2, int n)
    {
        var pts = Pregon.Burst(r1, r2, n, center);
        return pts;
    }

    private static Vector2[] ClosedBurst(Vector2 center, float r1, float r2, int n)
    {
        var pts = Pregon.Burst(r1, r2, n, center);
        var closed = new Vector2[pts.Length + 1];
        System.Array.Copy(pts, closed, pts.Length);
        closed[pts.Length] = pts[0];
        return closed;
    }
}
