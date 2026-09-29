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

    // ADR 0171, la tirada del destino: el sello de lacre que gira a la derecha de la banda. Sin él (turba,
    // consumible) la banda es la de siempre.
    private bool _fate;
    private string _sealText = string.Empty;
    private float _sealAngle;
    private FateOutcome _outcome;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0f, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <summary>Cómo acabó la tirada del destino que enseña el sello.</summary>
    public enum FateOutcome
    {
        /// <summary>Los dados aún ruedan: el sello gira.</summary>
        Rolling,

        /// <summary>Se ha salvado: el sello se para con un aspa verde... es decir, con la marca de «a salvo».</summary>
        Saved,

        /// <summary>Ha caído la desgracia: el sello se para con la marca de sangre.</summary>
        Hit,
    }

    public void Show(string header, string body)
    {
        _header = header;
        _body = body;
        _fate = false;
        Visible = true;
        QueueRedraw();
    }

    /// <summary>
    /// La tirada del destino (ADR 0171): el pregón con el porcentaje real y el sello que gira. Se vuelve a llamar
    /// cada fotograma que cambie algo (<paramref name="spin"/> 0..1 mientras rueda, luego el resultado).
    /// </summary>
    public void ShowFate(string header, string body, string sealText, float spin, FateOutcome outcome)
    {
        _header = header;
        _body = body;
        _fate = true;
        _sealText = sealText;

        // Tres vueltas y media que frenan al final (ease-out cúbico): un sello que se para, no que se corta.
        float eased = 1f - Mathf.Pow(1f - Mathf.Clamp(spin, 0f, 1f), 3f);
        _sealAngle = eased * Mathf.Tau * 3.5f;
        _outcome = outcome;
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

        if (_fate)
        {
            DrawSeal(new Vector2(w - 190f, 50f));
        }
    }

    /// <summary>
    /// El lacre: disco de cera con el porcentaje y un asa que gira alrededor (color y forma juntos, UI-002: el
    /// resultado se lee también por la marca, no sólo por el tono).
    /// </summary>
    private void DrawSeal(Vector2 centre)
    {
        const float radius = 40f;
        DrawCircle(centre + new Vector2(0f, 4f), radius, new Color(0f, 0f, 0f, 0.25f));
        var wax = _outcome == FateOutcome.Saved ? new Color("4f6b2a") : Pregon.Wax;
        DrawCircle(centre, radius, wax);
        DrawArc(centre, radius - 6f, 0f, Mathf.Tau, 48, Pregon.Vellum, 2f, true);

        // Las 8 muescas del canto giran con el sello mientras rueda; al parar quedan quietas.
        for (int i = 0; i < 8; i++)
        {
            float a = _sealAngle + (i * Mathf.Tau / 8f);
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(centre + (dir * (radius - 4f)), centre + (dir * (radius + 6f)), Pregon.Sable, 3f);
        }

        string glyph = _outcome switch
        {
            FateOutcome.Saved => "✓",
            FateOutcome.Hit => "✗",
            _ => _sealText,
        };
        int size = _outcome == FateOutcome.Rolling ? Pregon.SizeHeader : Pregon.SizeHeader + 8;
        var font = Pregon.Titular;
        var textSize = font.GetStringSize(glyph, HorizontalAlignment.Left, -1, size);
        Style.DrawText(this, font, centre - new Vector2(textSize.X / 2f, size / 2f), glyph, size, Pregon.Vellum);
    }
}
