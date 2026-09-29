using Godot;

namespace Underleague.Game.Ui.Knavall;

/// <summary>Cómo se ve una casilla de inventario.</summary>
public readonly record struct TileLook(
    Glyph Glyph,
    int Count = 1,
    bool Selected = false,
    bool Hover = false,
    bool Dimmed = false,
    Glyph Marker = Glyph.None,
    bool Relic = false,
    string Caption = "");

/// <summary>
/// Casilla de inventario de Knavall (ADR 0162): un hueco de madera clara con el objeto dibujado en grande,
/// cuántos hay en un disco negro y, si hace falta, una marca en la esquina (manual, condicional, reliquia).
/// La usan el cofre y los consumibles, en compacto y en grande, para que un objeto se vea igual en todas
/// partes.
/// </summary>
public static class Tiles
{
    /// <summary>Pinta la casilla en <paramref name="rect"/>. Con <see cref="TileLook.Caption"/>, el nombre va debajo del icono.</summary>
    public static void Draw(CanvasItem t, Rect2 rect, TileLook look, int seed)
    {
        var lift = look.Selected ? new Vector2(-1f, -2f) : look.Hover ? new Vector2(0f, -1f) : Vector2.Zero;
        var box = new Rect2(rect.Position + lift, rect.Size);
        var fill = look.Selected ? Ink.Ochre : look.Hover ? Ink.Paper : Ink.PaperWarm;
        Ink.Slab(t, box, fill, seed, 1.2f, look.Selected ? 3.6f : 2.6f, look.Selected ? new Vector2(5f, 6f) : new Vector2(3f, 4f));

        // Hendidura interior: el hueco donde se apoya el objeto.
        var inner = box.Grow(-6f);
        if (look.Caption.Length > 0)
        {
            inner = new Rect2(inner.Position, new Vector2(inner.Size.X, inner.Size.X));
        }

        t.DrawRect(inner, new Color(Ink.PaperDark, look.Selected ? 0.25f : 0.4f));
        t.DrawRect(inner, new Color(Ink.Muted, 0.35f), false, 1.2f);

        float iconSide = Mathf.Min(inner.Size.X, inner.Size.Y) * 0.74f;
        var center = inner.GetCenter();
        InkIcons.Draw(t, look.Glyph, center, iconSide);
        if (look.Dimmed)
        {
            t.DrawRect(box.Grow(-2f), new Color(Ink.Paper, 0.6f));
        }

        if (look.Relic)
        {
            var seal = inner.Position + new Vector2(10f, 10f);
            var star = Broadcast.Pregon.Burst(10f, 6f, 8, seal);
            Ink.Poly(t, star, Ink.RedDark, 1.4f);
        }

        if (look.Marker != Glyph.None)
        {
            var at = new Vector2(inner.End.X - 9f, inner.Position.Y + 9f);
            Ink.Disc(t, at, 11f, Ink.Ochre, 2f);
            InkIcons.Draw(t, look.Marker, at, 15f);
        }

        if (look.Count > 1)
        {
            string text = UiText.Get("ui.kn.copies", look.Count);
            var font = Ink.Heavy;
            const int Size = 16;
            float width = Ink.Width(font, text, Size);
            var at = new Vector2(inner.End.X - width - 3f, inner.End.Y - font.GetHeight(Size) + 2f);
            Ink.Outlined(t, font, at, text, Size, Ink.Paper, Ink.Black, 5);
        }

        if (look.Caption.Length > 0)
        {
            float top = inner.End.Y + 4f;
            var lines = Style.Wrap(Ink.Heavy, look.Caption.ToUpperInvariant(), 14, box.Size.X - 10f);
            for (int i = 0; i < lines.Count && i < 2; i++)
            {
                float width = Ink.Width(Ink.Heavy, lines[i], 14);
                Ink.Text(t, Ink.Heavy, new Vector2(box.Position.X + ((box.Size.X - width) / 2f), top), lines[i], 14, Ink.Black);
                top += 15f;
            }
        }
    }

    /// <summary>Hueco vacío con borde discontinuo y un texto corto.</summary>
    public static void Empty(CanvasItem t, Rect2 rect, string caption, int seed)
    {
        var edge = Ink.Closed(Ink.Rough(rect, 1f, seed));
        for (int i = 0; i < edge.Length - 1; i++)
        {
            Style.DrawDashed(t, edge[i], edge[i + 1], Ink.Muted, 2.2f, 6f);
        }

        t.DrawColoredPolygon(Ink.Rough(rect.Grow(-3f), 1f, seed), new Color(Ink.PaperDark, 0.18f));
        InkIcons.Draw(t, Glyph.EmptySlot, rect.GetCenter() - new Vector2(0f, caption.Length > 0 ? 10f : 0f), Mathf.Min(rect.Size.X, rect.Size.Y) * 0.35f);
        if (caption.Length > 0)
        {
            string text = caption.ToUpperInvariant();
            float width = Ink.Width(Ink.Heavy, text, 14);
            Ink.Text(t, Ink.Heavy, new Vector2(rect.Position.X + ((rect.Size.X - width) / 2f), rect.GetCenter().Y + 16f), text, 14, Ink.Muted);
        }
    }

    /// <summary>Cabecera de bloque: icono grande, título en rótulo y una etiqueta roja con el recuento.</summary>
    public static void Header(CanvasItem t, Vector2 at, float width, Glyph glyph, string title, string tag, float iconSide = 58f, int titleSize = 30)
    {
        InkIcons.Draw(t, glyph, at + new Vector2(iconSide / 2f, iconSide / 2f), iconSide);
        float left = at.X + iconSide + 12f;
        Ink.Text(t, Ink.Display, new Vector2(left, at.Y + ((iconSide - Ink.Display.GetHeight(titleSize)) / 2f)), title.ToUpperInvariant(), titleSize, Ink.Black);
        if (tag.Length > 0)
        {
            string text = tag.ToUpperInvariant();
            float tagWidth = Ink.Width(Ink.Heavy, text, 15) + 24f;
            var rect = new Rect2(at.X + width - tagWidth - 4f, at.Y + (iconSide / 2f) - 15f, tagWidth, 30f);
            Broadcast.Pregon.Tilted(t, rect.GetCenter(), Mathf.DegToRad(-4f), () =>
            {
                var local = new Rect2(-rect.Size / 2f, rect.Size);
                Ink.Brush(t, local, Ink.Red, title.Length * 7);
                Ink.Outlined(t, Ink.Heavy, local.Position + new Vector2(12f, 5f), text, 15, Ink.Paper, Ink.Black, 4);
            });
        }
    }
}
