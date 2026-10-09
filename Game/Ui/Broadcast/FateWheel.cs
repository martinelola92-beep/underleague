using Godot;
using Underleague.Game.Ui.Knavall;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// La ruleta de la tirada del destino (BX-16, enmienda de la ADR 0171): «tiene que ser algo gráfico, aunque pare el
/// partido: una ruleta verde y roja con una aguja que gira». Con el partido congelado en el fotograma anterior a los
/// dados, la aguja gira y frena hasta caer en el sector del resultado que <c>/Sim</c> ya decidió (RT-014: aquí no se
/// tira nada, se enseña lo que trae <c>FATE_ROLL</c>).
/// <para>
/// El sector rojo mide exactamente la probabilidad del golpe y el verde el resto: lo que se ve es la cuenta de verdad
/// (RF-012d, previsible). Dónde cae la aguja dentro de su sector sale de una semilla (el tick), nunca del azar.
/// </para>
/// <para>Dibujada por código con los materiales del pase de arte; relleno sustituible por sprites (disco, aguja).</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class FateWheel : Control
{
    private const float TurnsBeforeStop = 3.5f;

    private string _title = string.Empty;
    private string _announce = string.Empty;
    private string _hitLabel = string.Empty;
    private string _saveLabel = string.Empty;
    private string _result = string.Empty;
    private int _basisPoints = 2500;
    private bool _hit;
    private int _seed;
    private float _spin;
    private bool _settled;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (Engine.IsEditorHint())
        {
            Show("Se hace saber: los dados del destino", "Ivarr Forjagrís tiene un 34 % de mandar a Morg al otro barrio",
                "AL OTRO BARRIO", "SE SALVA", "¡SE SALVA!", 3400, hit: false, seed: 7, spin: 1f, settled: true);
        }
    }

    /// <summary>
    /// Pinta el estado de la tirada. <paramref name="basisPoints"/>: probabilidad del golpe en puntos básicos (la que
    /// trae el evento). <paramref name="spin"/>: 0..1 lo que lleva girado; <paramref name="settled"/>: ya paró.
    /// </summary>
    public void Show(string title, string announce, string hitLabel, string saveLabel, string result, int basisPoints, bool hit, int seed, float spin, bool settled)
    {
        _title = title;
        _announce = announce;
        _hitLabel = hitLabel;
        _saveLabel = saveLabel;
        _result = result;
        _basisPoints = Mathf.Clamp(basisPoints, 0, 10000);
        _hit = hit;
        _seed = seed;
        _spin = Mathf.Clamp(spin, 0f, 1f);
        _settled = settled;
        Visible = true;
        QueueRedraw();
    }

    /// <summary>Ángulo final de la aguja: dentro del sector del resultado, a una fracción que sale de la semilla.</summary>
    private float FinalAngle()
    {
        float hitSpan = Mathf.Tau * _basisPoints / 10000f;
        float fraction = 0.18f + (0.64f * ((Pregon.Jitter(_seed * 31 + 5, 0.5f) + 0.5f)));
        float start = -Mathf.Pi / 2f;
        return _hit
            ? start + (hitSpan * fraction)
            : start + hitSpan + ((Mathf.Tau - hitSpan) * fraction);
    }

    public override void _Draw()
    {
        float side = Mathf.Min(Size.X, Size.Y - 130f);
        if (side <= 40f)
        {
            return;
        }

        var centre = new Vector2(Size.X / 2f, 96f + (side / 2f));
        float radius = side / 2f;

        // Cartela de arriba: el pregón de la tirada.
        float headerWidth = Size.X - 8f;
        var header = new Rect2(new Vector2((Size.X - headerWidth) / 2f, 0f), new Vector2(headerWidth, 82f));
        Ink.Slab(this, header, Ink.Paper, _seed + 3, 2f, 3.5f, new Vector2(5f, 6f));
        Ink.Text(this, Ink.Display, header.Position + new Vector2(0f, 6f), _title, 28, Ink.Black, header.Size.X, HorizontalAlignment.Center);
        int announceSize = Ink.FitSize(Pregon.SerifItalic, _announce, 21, header.Size.X - 28f, 14);
        Ink.Text(this, Pregon.SerifItalic, header.Position + new Vector2(14f, 44f), _announce, announceSize, Ink.RedDark, header.Size.X - 28f, HorizontalAlignment.Center);

        // Aro de madera con sombra dura.
        DrawCircle(centre + new Vector2(6f, 8f), radius + 14f, Ink.Shadow);
        DrawDisc(centre, radius + 14f, Ink.WoodDark, Art.Wood, _seed);
        DrawArc(centre, radius + 14f, 0f, Mathf.Tau, 96, Ink.Black, 4f, true);

        // Sectores: rojo = probabilidad del golpe, empezando arriba y en sentido horario; verde = el resto.
        float hitSpan = Mathf.Tau * _basisPoints / 10000f;
        float start = -Mathf.Pi / 2f;
        DrawSector(centre, radius, start, start + hitSpan, Ink.Red);
        DrawSector(centre, radius, start + hitSpan, start + Mathf.Tau, Ink.Green);
        DrawArc(centre, radius, 0f, Mathf.Tau, 96, Ink.Black, 3.5f, true);
        DrawLine(centre, centre + Polar(start, radius), Ink.Black, 3.5f, true);
        DrawLine(centre, centre + Polar(start + hitSpan, radius), Ink.Black, 3.5f, true);

        // Rótulos de los sectores, con el porcentaje del golpe.
        string percent = ((_basisPoints + 50) / 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + " %";
        SectorLabel(centre, radius, start + (hitSpan / 2f), _hitLabel, percent);
        SectorLabel(centre, radius, start + hitSpan + ((Mathf.Tau - hitSpan) / 2f), _saveLabel, null);

        // Aguja: gira TurnsBeforeStop vueltas y frena (ease-out cúbico) hasta su ángulo final.
        float eased = 1f - Mathf.Pow(1f - _spin, 3f);
        float angle = FinalAngle() - ((1f - eased) * TurnsBeforeStop * Mathf.Tau);
        DrawNeedle(centre, radius, angle);

        // El resultado, cuando para: brochazo del color del sector.
        if (_settled && _result.Length > 0)
        {
            var band = new Rect2(new Vector2((Size.X / 2f) - 230f, centre.Y + radius + 24f), new Vector2(460f, 64f));
            Ink.Brush(this, band, _hit ? Ink.Red : Ink.Green, _seed);
            Ink.Outlined(this, Ink.Display, band.Position + new Vector2(0f, 10f), _result, 34, Ink.Paper, Ink.Black, 6, band.Size.X, HorizontalAlignment.Center);
        }
    }

    private static Vector2 Polar(float angle, float radius) => new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

    private void DrawDisc(Vector2 centre, float radius, Color fill, Texture2D? material, int seed)
    {
        var points = new Vector2[64];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = centre + Polar(i * Mathf.Tau / points.Length, radius);
        }

        Art.FillPolygon(this, points, fill, material, seed);
    }

    private void DrawSector(Vector2 centre, float radius, float from, float to, Color fill)
    {
        if (to - from <= 0.001f)
        {
            return;
        }

        int steps = Mathf.Max(3, (int)((to - from) / Mathf.Tau * 72f));
        var points = new Vector2[steps + 2];
        points[0] = centre;
        for (int i = 0; i <= steps; i++)
        {
            points[i + 1] = centre + Polar(from + ((to - from) * i / steps), radius);
        }

        Art.FillPolygon(this, points, fill, Art.Parchment, _seed + (int)(from * 100f));
    }

    private void SectorLabel(Vector2 centre, float radius, float angle, string text, string? second)
    {
        if (text.Length == 0)
        {
            return;
        }

        var at = centre + Polar(angle, radius * 0.58f);
        var box = new Vector2(radius * 0.9f, 30f);
        Ink.Outlined(this, Ink.Heavy, at - new Vector2(box.X / 2f, second is null ? 15f : 26f), text, 22, Ink.Paper, Ink.Black, 5, box.X, HorizontalAlignment.Center);
        if (second is not null)
        {
            Ink.Outlined(this, Ink.Heavy, at - new Vector2(box.X / 2f, 0f), second, 26, Ink.Paper, Ink.Black, 5, box.X, HorizontalAlignment.Center);
        }
    }

    private void DrawNeedle(Vector2 centre, float radius, float angle)
    {
        var tip = centre + Polar(angle, radius * 0.92f);
        var tail = centre + Polar(angle + Mathf.Pi, radius * 0.22f);
        var side = Polar(angle + (Mathf.Pi / 2f), radius * 0.06f);
        var needle = new[] { tip, centre + side, tail, centre - side };
        var shadow = new Vector2(4f, 5f);
        DrawColoredPolygon(new[] { needle[0] + shadow, needle[1] + shadow, needle[2] + shadow, needle[3] + shadow }, Ink.Shadow);
        Ink.Poly(this, needle, Ink.Ochre, 3f);
        Ink.Disc(this, centre, radius * 0.09f, Pregon.Wax, 3f);
    }
}
