using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>Tono del sello: qué tipo de suceso proclama, con su color y su marca (UI-002: nunca solo color).</summary>
public enum StampTone
{
    Foul,
    Unseen,
    Yellow,
    MinorInjury,
    Perk,
    Consumable,
    Cancelled,
}

/// <summary>
/// Sello sobre recorte de papel, torcido (N1/N2 de <c>docs/ui/README.md</c> §4): falta, no vista, amarilla,
/// lesión leve sin decisión, perk activado en juego, consumible, anulado. El giro es determinista —función
/// del orden de creación, nunca <see cref="System.Random"/>— para que la misma composición produzca
/// siempre el mismo trazo.
/// </summary>
public partial class Stamp : Control
{
    private static int _instanceCount;

    private readonly int _seed;
    private string _text = string.Empty;
    private StampTone _tone;
    private bool _large;

    public Stamp()
    {
        _seed = _instanceCount++;
    }

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Muestra el sello con su texto (ya resuelto por el llamador, RT-035) y su tono.</summary>
    public void Show(string text, StampTone tone, bool large)
    {
        _text = text;
        _tone = tone;
        _large = large;
        CustomMinimumSize = large ? new Vector2(220f, 66f) : new Vector2(240f, 42f);
        Size = CustomMinimumSize;
        Visible = true;
        QueueRedraw();
    }

    private (Color Fill, Color Ink, string Glyph) Tone() => _tone switch
    {
        StampTone.Foul => (Pregon.Vellum, Pregon.Wax, string.Empty),
        StampTone.Unseen => (Pregon.Vellum, Pregon.Sable, string.Empty),
        StampTone.Yellow => (Pregon.Vellum, new Color("8a6a12"), string.Empty),
        StampTone.MinorInjury => (Pregon.Vellum, Pregon.Blood, string.Empty),
        StampTone.Perk => (Pregon.Vellum, Pregon.InkBrown, "✦ "),
        StampTone.Consumable => (Pregon.Vellum, Pregon.InkBrown, "❖ "),
        StampTone.Cancelled => (new Color(Pregon.VellumEdge, 0.8f), new Color("5a5248"), string.Empty),
        _ => (Pregon.Vellum, Pregon.Sable, string.Empty),
    };

    public override void _Draw()
    {
        float mag = 2f + Mathf.Abs(Pregon.Jitter(_seed, 2f));
        float sign = Pregon.Jitter(_seed + 1, 1f) >= 0f ? 1f : -1f;
        float angle = Mathf.DegToRad(mag * sign);
        var (fill, ink, glyph) = Tone();
        float w = Size.X, h = Size.Y;

        Pregon.Tilted(this, Vector2.Zero, angle, () =>
        {
            Pregon.DrawParchment(this, Vector2.Zero, w, h, fill, Pregon.VellumEdge, _seed, amplitude: 2f, edgeWidth: 1.5f);

            if (_tone == StampTone.Foul)
            {
                DrawLine(new Vector2(8f, 8f), new Vector2(w - 8f, 8f), ink, 1.5f);
                DrawLine(new Vector2(8f, h - 8f), new Vector2(w - 8f, h - 8f), ink, 1.5f);
            }
            else if (_tone == StampTone.Yellow)
            {
                DrawRect(new Rect2(8f, h / 2f - 10f, 16f, 20f), new Color("d9a72a"));
            }
            else if (_tone == StampTone.MinorInjury)
            {
                DrawRect(new Rect2(10f, (h / 2f) - 2f, 16f, 4f), Pregon.Blood);
                DrawRect(new Rect2(16f, (h / 2f) - 8f, 4f, 16f), Pregon.Blood);
            }
            else if (_tone == StampTone.Cancelled)
            {
                DrawLine(new Vector2(10f, h - 10f), new Vector2(w - 10f, 10f), new Color("5a5248"), 3f);
            }

            // Perk y consumible son la voz de dato (Barlow), no la voz que proclama (Fell): son un
            // recuento en curso, no un suceso arbitrado.
            bool dataVoice = _tone is StampTone.Perk or StampTone.Consumable;
            var font = dataVoice ? Pregon.DataBold : (_large ? Pregon.FellBig : Pregon.Fell);
            int size = dataVoice ? Pregon.SizeData : (_large ? Pregon.SizeTitleSmall / 2 : Pregon.SizeHeader);

            // La cruz médica de MinorInjury vive en x 10-20 (revisión del orquestador, 19 sep 2026: se
            // pintaba encima de la «T» de «Tocado»): el texto empieza después de ella, no a los mismos 8px.
            float textX = _tone == StampTone.MinorInjury ? 30f : 8f;
            Style.DrawText(this, font, new Vector2(textX, (h - size) / 2f), glyph + _text, size, ink, maxWidth: w - textX - 8f);
        });
    }
}
