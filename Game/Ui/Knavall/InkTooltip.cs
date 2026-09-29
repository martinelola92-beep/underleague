using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Knavall;

/// <summary>Una línea de dato dentro de un cartel de ayuda (p. ej. «+20 fuerza» en verde).</summary>
public readonly record struct TipLine(string Text, Color Color, Glyph Glyph = Glyph.None);

/// <summary>
/// Lo que explica un elemento: icono, título y una o dos frases de <b>qué hace en el juego</b>, no de cómo
/// está implementado. Las líneas son datos cortos con color (modificadores de un objeto).
/// </summary>
public sealed record Tip(string Title, string Body, Glyph Glyph = Glyph.Info, IReadOnlyList<TipLine>? Lines = null);

/// <summary>
/// Cartel de ayuda contextual de Knavall (ADR 0162): pizarra oscura con borde de papel, tipografía grande,
/// icono, título y explicación breve, con una flecha que apunta al elemento. Un elemento, un cartel: se
/// muestra al pasar el ratón por encima, nunca varios a la vez.
/// <para>
/// Lo construye <see cref="Build"/> para el <c>_MakeCustomTooltip</c> de cada control. Godot lo coloca
/// abajo a la derecha del puntero, así que la flecha sale de la esquina superior izquierda hacia él.
/// </para>
/// </summary>
public partial class InkTooltip : Control
{
    private const float MaxWidth = 300f;
    private const float Pad = 12f;
    private const float IconSide = 30f;
    private const float Arrow = 12f;
    private const int TitleSize = 17;
    private const int BodySize = 15;
    private const int LineSize = 15;

    private Tip _tip = new(string.Empty, string.Empty);
    private List<string> _body = new();
    private float _width;

    /// <summary>Crea el cartel ya medido para un contenido.</summary>
    public static InkTooltip Build(Tip tip)
    {
        var card = new InkTooltip { MouseFilter = MouseFilterEnum.Ignore };
        card.Measure(tip);
        return card;
    }

    private void Measure(Tip tip)
    {
        _tip = tip;
        float textLeft = Pad + IconSide + 10f;
        float titleWidth = Ink.Width(Ink.Heavy, tip.Title, TitleSize);
        float lineWidth = 0f;
        foreach (var line in tip.Lines ?? System.Array.Empty<TipLine>())
        {
            lineWidth = Mathf.Max(lineWidth, Ink.Width(Ink.Heavy, line.Text, LineSize) + (line.Glyph != Glyph.None ? 22f : 0f));
        }

        float bodyWidth = Ink.Width(Ink.Plain, tip.Body, BodySize);
        _width = Mathf.Clamp(textLeft + Mathf.Max(titleWidth, Mathf.Max(lineWidth, Mathf.Min(bodyWidth, MaxWidth - textLeft - Pad))) + Pad, 180f, MaxWidth);
        _body = tip.Body.Length > 0 ? Style.Wrap(Ink.Plain, tip.Body, BodySize, _width - textLeft - Pad) : new List<string>();

        float height = Pad + Mathf.Max(IconSide, Ink.Heavy.GetHeight(TitleSize) + 2f);
        height += _body.Count * (Ink.Plain.GetHeight(BodySize) + 1f);
        height += (tip.Lines?.Count ?? 0) * (Ink.Heavy.GetHeight(LineSize) + 2f);
        height += Pad;

        CustomMinimumSize = new Vector2(_width + 10f, height + Arrow + 10f);
        Size = CustomMinimumSize;
    }

    public override void _Draw()
    {
        var card = new Rect2(new Vector2(0f, Arrow), new Vector2(_width, Size.Y - Arrow - 10f));

        // Flecha hacia el elemento (arriba a la izquierda, donde está el puntero).
        var arrow = new[] { new Vector2(10f, Arrow + 2f), new Vector2(4f, 0f), new Vector2(34f, Arrow + 2f) };
        DrawColoredPolygon(Ink.Shift(arrow, new Vector2(3f, 4f)), Ink.Shadow);
        Ink.Slab(this, card, Ink.Night, _tip.Title.Length * 3, 1.2f, 0f, new Vector2(4f, 5f));
        DrawColoredPolygon(arrow, Ink.Night);
        DrawPolyline(new[] { arrow[0], arrow[1], arrow[2] }, Ink.Paper, 2.2f, true);
        DrawPolyline(Ink.Closed(Ink.Rough(card, 1.2f, _tip.Title.Length * 3)), Ink.Paper, 2.2f, true);
        DrawRect(new Rect2(arrow[0] + new Vector2(2f, -1f), new Vector2(arrow[2].X - arrow[0].X - 4f, 4f)), Ink.Night);

        var iconCenter = card.Position + new Vector2(Pad + (IconSide / 2f), Pad + (IconSide / 2f));
        InkIcons.Draw(this, _tip.Glyph, iconCenter, IconSide);

        float left = card.Position.X + Pad + IconSide + 10f;
        float y = card.Position.Y + Pad + 2f;
        Ink.Text(this, Ink.Heavy, new Vector2(left, y), _tip.Title.ToUpperInvariant(), TitleSize, Ink.Ochre);
        y = card.Position.Y + Pad + Mathf.Max(IconSide, Ink.Heavy.GetHeight(TitleSize) + 2f);

        foreach (string line in _body)
        {
            Ink.Text(this, Ink.Plain, new Vector2(left, y), line, BodySize, Ink.Paper);
            y += Ink.Plain.GetHeight(BodySize) + 1f;
        }

        foreach (var line in _tip.Lines ?? System.Array.Empty<TipLine>())
        {
            float x = left;
            if (line.Glyph != Glyph.None)
            {
                InkIcons.Draw(this, line.Glyph, new Vector2(x + 8f, y + (Ink.Heavy.GetHeight(LineSize) / 2f) + 1f), 16f);
                x += 22f;
            }

            Ink.Text(this, Ink.Heavy, new Vector2(x, y + 1f), line.Text, LineSize, line.Color);
            y += Ink.Heavy.GetHeight(LineSize) + 2f;
        }
    }
}
