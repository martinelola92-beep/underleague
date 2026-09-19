using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Estandarte de pregón (N3 de <c>docs/ui/README.md</c> §4): gonfalón con orla bordada, escudo, «Se hace
/// saber», título grande, cuerpo en cursiva y pie. Nuestro equipo en azur y oro; el rival, en gules y
/// sable. Cubre gol, roja y lesión grave — los tres comparten formato, solo cambian el color del paño y
/// los cuatro textos, que llegan ya resueltos por quien lo muestra (RT-035: nada de texto de efecto
/// escrito a mano aquí).
/// </summary>
public partial class HeraldBanner : Control
{
    public const float DesignWidth = 500f;
    public const float DesignHeight = 640f;

    private bool _ours;
    private string _header = string.Empty;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _footer = string.Empty;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(DesignWidth, DesignHeight);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public void Show(bool ours, string header, string title, string body, string footer)
    {
        _ours = ours;
        _header = header;
        _title = title;
        _body = body;
        _footer = footer;
        Visible = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var teamColor = _ours ? Pregon.Azur : Pregon.Gules;
        var flagAt = new Vector2(20f, 40f);
        float w = 440f, h = 560f;
        var flag = Pregon.Swallowtail(w, h, 70f);
        var shifted = new Vector2[flag.Length];
        for (int i = 0; i < flag.Length; i++)
        {
            shifted[i] = flag[i] + flagAt;
        }

        var shadow = new Vector2[flag.Length];
        for (int i = 0; i < flag.Length; i++)
        {
            shadow[i] = shifted[i] + new Vector2(8f, 9f);
        }

        DrawColoredPolygon(shadow, new Color(0f, 0f, 0f, 0.3f));
        var closed = new Vector2[flag.Length + 1];
        System.Array.Copy(shifted, closed, flag.Length);
        closed[flag.Length] = shifted[0];
        DrawColoredPolygon(shifted, teamColor);
        DrawPolyline(closed, Pregon.InkBrown, 2.5f, true);

        // Textura tejida: hilos horizontales tenues.
        for (int i = 0; i < (int)(h / 8f); i++)
        {
            float y = flagAt.Y + 8f + (i * 8f);
            DrawLine(new Vector2(flagAt.X + 4f, y), new Vector2(flagAt.X + w - 4f, y), new Color(0f, 0f, 0f, 0.12f), 1f);
        }

        Pregon.DrawOrla(this, flag, flagAt, 14f, Pregon.Or, 1.8f);
        Pregon.DrawOrla(this, flag, flagAt, 20f, Pregon.Or, 1.2f);
        Pregon.DrawTrumpet(this, new Vector2(flagAt.X - 20f, flagAt.Y + 26f), 470f, -3f);
        Pregon.DrawShield(this, new Vector2(flagAt.X + 175f, flagAt.Y + 50f), 70f, 88f, _ours);

        // Margen interior: el texto no se pega al pliegue del paño (IM Fell SC ya distingue mayúscula de
        // versalita con el original, así que no se fuerza a mayúsculas — RT-035 solo pide texto resuelto).
        // Centrado en el paño (revisión visual de la galería, 19 sep 2026, docs/ui/capturas/gol-1920x1080.jpg):
        // cabecera, título, cuerpo y pie leían pegados al pliegue izquierdo en vez de en el eje del gonfalón.
        float tx = flagAt.X + 16f;
        float tw = w - 32f;
        Pregon.DrawFittedTitle(this, Pregon.Fell, new Vector2(tx, flagAt.Y + 150f), _header, Pregon.SizeHeader, Pregon.Or, tw);
        int titlePreferred = _title.Length > 4 ? Pregon.SizeTitleSmall : Pregon.SizeTitleLarge;
        Pregon.DrawFittedTitle(this, Pregon.FellBig, new Vector2(tx, flagAt.Y + 194f), _title, titlePreferred, Pregon.Vellum, tw);
        Pregon.DrawWrappedText(this, Pregon.FellItalic, new Vector2(tx, flagAt.Y + 336f), _body, Pregon.SizeBody, Pregon.Vellum, tw, centered: true);
        Pregon.DrawFittedTitle(this, Pregon.Fell, new Vector2(tx, flagAt.Y + 412f), _footer, Pregon.SizeHeader, Pregon.Or, tw);
    }
}
