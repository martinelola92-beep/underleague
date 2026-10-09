using System;
using System.Collections.Generic;
using Godot;
using Underleague.Game.Ui.Broadcast;

namespace Underleague.Game.Ui.Knavall;

/// <summary>
/// Kit de dibujo del <b>lenguaje visual de Knavall</b> (ADR 0162): tinta gruesa, papel, madera, brochazos y
/// sellos dibujados por código. Nace con la pantalla de Equipo, que es la referencia del lenguaje, y está
/// pensado para que el resto de pantallas lo adopten después sin reinventarlo.
/// <para>
/// Reglas que aplica solas, para que ningún componente tenga que recordarlas:
/// </para>
/// <list type="bullet">
/// <item><b>Contorno negro grueso y sombra dura</b> en todo lo que es objeto (placa, papel, botón). El
/// contenido que se lee (cifras, nombres) va plano encima: irregularidad en el marco, nunca en el texto
/// (<c>docs/ui/README.md</c> §1).</item>
/// <item><b>Determinista</b>: el trazo irregular sale de <see cref="Pregon.Jitter"/> con una semilla que da
/// el llamador, nunca de <c>System.Random</c>, para que una captura de referencia siga siendo comparable.</item>
/// <item><b>Procedural</b>: nada de imágenes importadas (regla de fase: no se produce arte hasta cerrar el
/// diseño de la fase 2). Todo esto es marcador de posición con intención, sustituible por arte.</item>
/// </list>
/// </summary>
public static class Ink
{
    // ------------------------------------------------------------------ paleta

    /// <summary>Tinta: contornos, sombras y texto sobre papel claro. Casi negro, algo cálido.</summary>
    public static readonly Color Black = new("1b1510");

    /// <summary>Papel del cartel: el pergamino de siempre (<see cref="Style.Panel"/>).</summary>
    public static readonly Color Paper = new("efe2c0");

    /// <summary>Papel más tostado: filas, casillas y fondos de segundo plano sobre el papel claro.</summary>
    public static readonly Color PaperWarm = new("e4cf9f");

    /// <summary>Papel sucio: manchas, casillas vacías y la parte vacía de las barras claras.</summary>
    public static readonly Color PaperDark = new("c9ae7c");

    /// <summary>Ocre: lo señalado (jugador elegido, pestaña activa, relleno de barra).</summary>
    public static readonly Color Ochre = new("e8ad2c");

    public static readonly Color OchreDark = new("a8720f");

    /// <summary>Rojo de brochazo: cabeceras, rasgos, sellos, lo negativo y la acción principal.</summary>
    public static readonly Color Red = new("b3261d");

    public static readonly Color RedDark = new("741510");

    /// <summary>Verde de lo que suma (modificador positivo, estado sano).</summary>
    public static readonly Color Green = new("3d8a2a");

    public static readonly Color GreenLight = new("79b845");

    public static readonly Color Wood = new("6b4323");

    public static readonly Color WoodDark = new("3a2312");

    public static readonly Color WoodLight = new("946238");

    /// <summary>Pizarra de los carteles de ayuda (tooltip): oscuro, con borde de papel.</summary>
    public static readonly Color Night = new("1f1a15");

    /// <summary>Tinta parda: texto secundario sobre papel.</summary>
    public static readonly Color Brown = new("3a2a1a");

    public static readonly Color Muted = new("6e5436");

    /// <summary>Sombra dura de todo objeto: negra, opaca a medias, desplazada abajo a la derecha.</summary>
    public static readonly Color Shadow = new(0f, 0f, 0f, 0.55f);

    /// <summary>Desplazamiento de la sombra dura por defecto.</summary>
    public static readonly Vector2 ShadowOffset = new(4f, 5f);

    // ------------------------------------------------------------------ tamaños

    /// <summary>Nombre del club en la cabecera.</summary>
    public const int SizeHuge = 42;

    /// <summary>Nombre del jugador elegido.</summary>
    public const int SizeTitle = 30;

    /// <summary>Cabeceras de bloque (brochazos, pestañas, botones).</summary>
    public const int SizeHeading = 19;

    /// <summary>Nombres y datos esenciales.</summary>
    public const int SizeBody = 16;

    /// <summary>
    /// El mínimo de este lenguaje: 13 px a 1280x800, que es el «texto esencial a 20 px lógicos» de
    /// <c>docs/ui/README.md</c> §1 en el lienzo de 1920x1200.
    /// </summary>
    public const int SizeSmall = 13;

    /// <summary>Cifra grande de un atributo.</summary>
    public const int SizeFigure = 22;

    // ------------------------------------------------------------------ fuentes

    // Cacheadas en estático a propósito, como las fuentes de Pregon: se crean una vez para toda la pantalla.
    // Por eso Godot avisa al salir de «2 resources still in use at exit» (mismo caso que PlayerModel): no es
    // una fuga en tiempo de juego.
    private static Font? _display;
    private static Font? _heavy;

    /// <summary>
    /// Grenze Gotisch a peso 800: la voz que proclama (ADR 0120 enmendada, <c>docs/ui/README.md</c> §1),
    /// engordada para que lea como rótulo pintado a brocha y no como libro.
    /// </summary>
    public static Font Display => _display ??= Weighted(Pregon.Titular, 800, 0f);

    /// <summary>Barlow Condensed Bold algo engordada: nombres, cifras, botones.</summary>
    public static Font Heavy => _heavy ??= Weighted(Pregon.DataBold, 0, 0.35f);

    /// <summary>Barlow Condensed Bold tal cual: datos de apoyo.</summary>
    public static Font Data => Pregon.DataBold;

    /// <summary>Barlow Condensed SemiBold: texto corrido (explicaciones de los carteles de ayuda).</summary>
    public static Font Plain => Pregon.DataSemiBold;

    private static Font Weighted(Font baseFont, int weight, float embolden)
    {
        var variation = new FontVariation { BaseFont = baseFont };
        if (weight > 0)
        {
            var axes = new Godot.Collections.Dictionary();
            axes[TextServerManager.GetPrimaryInterface().NameToTag("wght")] = weight;
            variation.VariationOpentype = axes;
        }

        if (embolden > 0f)
        {
            variation.VariationEmbolden = embolden;
        }

        return variation;
    }

    // ------------------------------------------------------------------ geometría

    /// <summary>Hash determinista entero, para decidir variantes (0..n-1) a partir de una semilla.</summary>
    public static int Pick(int seed, int n) => n <= 1 ? 0 : (int)(((Pregon.Jitter(seed, 1f) + 1f) * 0.5f * n) % n);

    /// <summary>Rectángulo de borde irregular, ya colocado en <paramref name="rect"/>.</summary>
    public static Vector2[] Rough(Rect2 rect, float amplitude, int seed, int steps = 12)
    {
        var poly = Pregon.RoughRect(rect.Size.X, rect.Size.Y, amplitude, seed, steps);
        for (int i = 0; i < poly.Length; i++)
        {
            poly[i] += rect.Position;
        }

        return poly;
    }

    /// <summary>Desplaza un polígono.</summary>
    public static Vector2[] Shift(Vector2[] poly, Vector2 offset)
    {
        var result = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            result[i] = poly[i] + offset;
        }

        return result;
    }

    /// <summary>Cierra un polígono para <c>DrawPolyline</c>.</summary>
    public static Vector2[] Closed(Vector2[] poly)
    {
        var result = new Vector2[poly.Length + 1];
        Array.Copy(poly, result, poly.Length);
        result[poly.Length] = poly[0];
        return result;
    }

    /// <summary>Relleno con contorno de tinta. La base de cada icono y cada placa.</summary>
    public static void Poly(CanvasItem target, Vector2[] points, Color fill, float outline = 2.5f, Color? outlineColor = null)
    {
        if (points.Length < 3)
        {
            return;
        }

        target.DrawColoredPolygon(points, fill);
        if (outline > 0f)
        {
            target.DrawPolyline(Closed(points), outlineColor ?? Black, outline, true);
        }
    }

    /// <summary>Círculo con contorno de tinta.</summary>
    public static void Disc(CanvasItem target, Vector2 center, float radius, Color fill, float outline = 2.5f)
    {
        target.DrawCircle(center, radius, fill);
        if (outline > 0f)
        {
            target.DrawArc(center, radius, 0f, Mathf.Tau, Mathf.Max(16, (int)(radius * 1.5f)), Black, outline, true);
        }
    }

    // ------------------------------------------------------------------ materiales

    /// <summary>
    /// Placa: relleno plano, contorno de tinta y sombra dura. La pieza de cualquier objeto físico pequeño
    /// (botón, pestaña, etiqueta, casilla).
    /// </summary>
    public static void Slab(CanvasItem target, Rect2 rect, Color fill, int seed, float amplitude = 1.4f, float outline = 3f, Vector2? shadow = null, Texture2D? material = null)
    {
        var poly = Rough(rect, amplitude, seed, Mathf.Clamp((int)(rect.Size.X / 26f), 6, 24));
        var offset = shadow ?? ShadowOffset;
        if (offset != Vector2.Zero)
        {
            target.DrawColoredPolygon(Shift(poly, offset), Shadow);
        }

        // Sin material pedido, toda placa lleva grano de papel (o de paño, en las oscuras): el arte de referencia
        // no tiene ni una superficie plana. Sin textura en disco, el relleno plano de siempre.
        material ??= Art.Parchment;
        if (material is null)
        {
            Poly(target, poly, fill, outline);
            return;
        }

        Art.FillPolygon(target, poly, fill, material, seed);
        if (outline > 0f)
        {
            target.DrawPolyline(Closed(poly), Black, outline, true);
        }
    }

    /// <summary>
    /// Borde envejecido de un papel: tres filetes translúcidos hacia dentro, del más oscuro al más tenue, como el
    /// tostado que deja el uso en los bordes de una hoja.
    /// </summary>
    private static void BurnEdge(CanvasItem target, Rect2 rect, int seed)
    {
        float[] depth = { 4f, 9f, 16f };
        float[] alpha = { 0.30f, 0.16f, 0.07f };
        float[] width = { 7f, 9f, 12f };
        for (int i = 0; i < depth.Length; i++)
        {
            var ring = Rough(rect.Grow(-depth[i]), 1.6f, seed + 11 + i, Mathf.Clamp((int)(rect.Size.X / 26f), 6, 24));
            Art.Burn(target, ring, Muted, width[i], alpha[i]);
        }
    }

    /// <summary>
    /// Cartel de papel: una <see cref="Slab"/> grande con manchas de uso y un filete interior, para los
    /// paneles donde vive la información. Las manchas son pocas y tenues a propósito: textura, no ruido.
    /// </summary>
    public static void Sheet(CanvasItem target, Rect2 rect, int seed, Color? fill = null)
    {
        var paper = Art.Parchment;
        Slab(target, rect, fill ?? Ink.Paper, seed, 2f, 3.5f, new Vector2(6f, 7f), paper);
        if (paper is not null)
        {
            // Con material, las manchas y las motas ya vienen en la textura: aquí sólo el borde tostado y el filete.
            BurnEdge(target, rect, seed);
            var filet = Rough(rect.Grow(-7f), 1.4f, seed + 3, Mathf.Clamp((int)(rect.Size.X / 26f), 6, 24));
            target.DrawPolyline(Closed(filet), new Color(PaperDark, 0.45f), 1.6f, true);
            return;
        }

        // Papel envejecido: un filete interior más oscuro, dos o tres manchas irregulares muy tenues y un
        // puñado de motas. Textura, no ruido: nada de esto compite con lo que se lee encima.
        var inner = Rough(rect.Grow(-7f), 1.4f, seed + 3, Mathf.Clamp((int)(rect.Size.X / 26f), 6, 24));
        target.DrawPolyline(Closed(inner), new Color(PaperDark, 0.55f), 2f, true);

        for (int i = 0; i < 3; i++)
        {
            var at = rect.Position + new Vector2(
                (Pregon.Jitter((seed * 31) + i, 0.4f) + 0.5f) * rect.Size.X,
                (Pregon.Jitter((seed * 17) + i + 7, 0.4f) + 0.5f) * rect.Size.Y);
            float radius = 16f + ((Pregon.Jitter((seed * 13) + i, 0.5f) + 0.5f) * Mathf.Min(rect.Size.X, rect.Size.Y) * 0.12f);
            var blob = new Vector2[11];
            for (int k = 0; k < blob.Length; k++)
            {
                float angle = k * Mathf.Tau / blob.Length;
                float r = radius * (0.7f + ((Pregon.Jitter((seed * 5) + (i * 23) + k, 0.5f) + 0.5f) * 0.6f));
                blob[k] = at + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.75f);
            }

            target.DrawColoredPolygon(blob, new Color(PaperDark, 0.13f));
        }

        for (int i = 0; i < 22; i++)
        {
            var at = rect.Position + new Vector2(
                12f + ((Pregon.Jitter((seed * 7) + (i * 3), 0.5f) + 0.5f) * (rect.Size.X - 24f)),
                12f + ((Pregon.Jitter((seed * 11) + (i * 5), 0.5f) + 0.5f) * (rect.Size.Y - 24f)));
            target.DrawCircle(at, 0.7f + ((Pregon.Jitter(seed + i, 0.5f) + 0.5f) * 0.9f), new Color(Muted, 0.2f));
        }
    }

    /// <summary>Tabla de madera: vetas, dos clavos y contorno. La estructura (cabecera, travesaños).</summary>
    public static void Plank(CanvasItem target, Rect2 rect, int seed, Color? tone = null, bool nails = true)
    {
        var fill = tone ?? Wood;
        var wood = Art.Wood;
        Slab(target, rect, wood is null ? fill : fill.Lightened(0.12f), seed, 1.2f, 3f, new Vector2(3f, 5f), wood);

        // Con material la veta viene en la textura; sin él, las vetas de siempre.
        int lines = wood is null ? Mathf.Max(2, (int)(rect.Size.Y / 9f)) : 0;
        for (int i = 1; i < lines; i++)
        {
            float y = rect.Position.Y + (rect.Size.Y * i / lines);
            var previous = new Vector2(rect.Position.X + 6f, y + Pregon.Jitter((seed * 5) + i, 1.5f));
            int segments = Mathf.Max(4, (int)(rect.Size.X / 40f));
            for (int s = 1; s <= segments; s++)
            {
                var point = new Vector2(
                    rect.Position.X + 6f + ((rect.Size.X - 12f) * s / segments),
                    y + Pregon.Jitter((seed * 5) + (i * 31) + s, 1.8f));
                target.DrawLine(previous, point, new Color(WoodDark, 0.45f), 1.4f, true);
                previous = point;
            }
        }

        if (nails && rect.Size.X > 40f)
        {
            Nail(target, rect.Position + new Vector2(10f, rect.Size.Y / 2f));
            Nail(target, rect.Position + new Vector2(rect.Size.X - 10f, rect.Size.Y / 2f));
        }
    }

    /// <summary>Cabeza de clavo de dos tonos.</summary>
    public static void Nail(CanvasItem target, Vector2 at)
    {
        target.DrawCircle(at, 3.2f, Black);
        target.DrawCircle(at, 2.2f, new Color("8b7a60"));
        target.DrawCircle(at - new Vector2(0.7f, 0.7f), 0.9f, new Color("d8c9a6"));
    }

    /// <summary>
    /// Brochazo: una franja de pintura con los extremos deshilachados y vetas de cerda. Es la cabecera de
    /// bloque (TITULARES, RASGOS) y el fondo de las etiquetas de rasgo. Sin contorno: la pintura no lo tiene.
    /// </summary>
    public static void Brush(CanvasItem target, Rect2 rect, Color fill, int seed)
    {
        // Pase de materiales: un brochazo de verdad (cerdas, cola deshilachada) teñido del color pedido. El trazo
        // ocupa el 68 % central del alto de la textura y del 2 al 90 % del ancho: esa región es la que se estira
        // sobre el rectángulo, con un poco de margen para que la cola y los bordes respiren.
        if (Art.Brush(seed) is { } stroke)
        {
            var size = stroke.GetSize();
            var source = new Rect2(size.X * 0.0f, size.Y * 0.13f, size.X * 0.97f, size.Y * 0.74f);
            var dest = new Rect2(rect.Position - new Vector2(4f, 1f), rect.Size + new Vector2(14f, 2f));
            target.DrawTextureRectRegion(stroke, new Rect2(dest.Position + new Vector2(3f, 3f), dest.Size), source, new Color(0f, 0f, 0f, 0.3f));
            target.DrawTextureRectRegion(stroke, dest, source, fill);
            return;
        }

        var points = new List<Vector2>();
        int steps = Mathf.Max(6, (int)(rect.Size.X / 14f));
        float top = rect.Position.Y;
        float bottom = rect.End.Y;

        // Borde superior, casi recto.
        for (int i = 0; i <= steps; i++)
        {
            float x = rect.Position.X + 6f + ((rect.Size.X - 12f) * i / steps);
            points.Add(new Vector2(x, top + Pregon.Jitter((seed * 3) + i, 1.6f)));
        }

        // Extremo derecho: deshilachado en dientes.
        const int Teeth = 5;
        for (int i = 1; i < Teeth; i++)
        {
            float y = top + (rect.Size.Y * i / Teeth);
            float reach = i % 2 == 0 ? 0f : 7f + Pregon.Jitter((seed * 7) + i, 3f);
            points.Add(new Vector2(rect.End.X - 6f + reach, y));
        }

        // Borde inferior.
        for (int i = steps; i >= 0; i--)
        {
            float x = rect.Position.X + 6f + ((rect.Size.X - 12f) * i / steps);
            points.Add(new Vector2(x, bottom + Pregon.Jitter((seed * 5) + i + 40, 1.8f)));
        }

        // Extremo izquierdo.
        for (int i = Teeth - 1; i > 0; i--)
        {
            float y = top + (rect.Size.Y * i / Teeth);
            float reach = i % 2 == 1 ? 0f : 7f + Pregon.Jitter((seed * 11) + i, 3f);
            points.Add(new Vector2(rect.Position.X + 6f - reach, y));
        }

        var poly = points.ToArray();
        target.DrawColoredPolygon(Shift(poly, new Vector2(3f, 3f)), new Color(0f, 0f, 0f, 0.35f));
        target.DrawColoredPolygon(poly, fill);

        // Vetas de cerda: líneas finas, más oscuras y más claras, que no llegan de punta a punta.
        for (int i = 0; i < 5; i++)
        {
            float y = top + 3f + ((rect.Size.Y - 6f) * (i + 0.5f) / 5f);
            float start = rect.Position.X + 8f + ((Pregon.Jitter((seed * 19) + i, 0.5f) + 0.5f) * rect.Size.X * 0.3f);
            float end = rect.End.X - 8f - ((Pregon.Jitter((seed * 23) + i, 0.5f) + 0.5f) * rect.Size.X * 0.3f);
            var tone = i % 2 == 0 ? new Color(fill.Darkened(0.35f), 0.45f) : new Color(fill.Lightened(0.25f), 0.35f);
            target.DrawLine(new Vector2(start, y), new Vector2(end, y + Pregon.Jitter(seed + i, 1f)), tone, 1.2f, true);
        }
    }

    /// <summary>
    /// Sello de tinta torcido (TITULAR, SUPLENTE, EQUIPADO): texto en mayúsculas dentro de un doble
    /// recuadro, en un solo color, como un tampón. Se dibuja centrado en <paramref name="center"/>.
    /// </summary>
    public static void Stamp(CanvasItem target, Vector2 center, string text, Color color, float rotationDeg, int size = 15, Color? fill = null)
    {
        var font = Heavy;
        var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1f, size);
        var box = new Vector2(textSize.X + 18f, textSize.Y + 6f);
        Pregon.Tilted(target, center, Mathf.DegToRad(rotationDeg), () =>
        {
            var rect = new Rect2(-box / 2f, box);
            if (fill is { } paper)
            {
                target.DrawColoredPolygon(Rough(rect, 1.2f, text.Length * 7), paper);
            }

            target.DrawPolyline(Closed(Rough(rect, 1.2f, text.Length * 7)), color, 2.6f, true);
            target.DrawPolyline(Closed(Rough(rect.Grow(-4f), 0.8f, text.Length * 9)), color, 1.2f, true);
            Text(target, font, new Vector2(-textSize.X / 2f, -textSize.Y / 2f), text, size, color);
        });
    }

    // ------------------------------------------------------------------ texto

    /// <summary>Texto tomando <paramref name="topLeft"/> como esquina superior (no como línea base).</summary>
    public static void Text(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color color, float maxWidth = -1f, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        target.DrawString(font, new Vector2(topLeft.X, topLeft.Y + font.GetAscent(size)), text, align, maxWidth, size, color);
    }

    /// <summary>
    /// Texto de rótulo: relleno sobre un contorno grueso de tinta, como la letra pintada de un cartel de
    /// feria. Para lo que va sobre pintura o madera (brochazos, placas oscuras, pestañas).
    /// </summary>
    public static void Outlined(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color fill, Color? outline = null, int outlineSize = 5, float maxWidth = -1f, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var baseline = new Vector2(topLeft.X, topLeft.Y + font.GetAscent(size));
        target.DrawStringOutline(font, baseline, text, align, maxWidth, size, outlineSize, outline ?? Black);
        target.DrawString(font, baseline, text, align, maxWidth, size, fill);
    }

    /// <summary>Ancho de un texto, para maquetar a mano.</summary>
    public static float Width(Font font, string text, int size) =>
        font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X;

    /// <summary>Recorta con puntos suspensivos si no cabe.</summary>
    public static string Fit(Font font, string text, int size, float maxWidth) => Pregon.Ellipsize(font, text, size, maxWidth);

    /// <summary>Mayor tamaño (desde <paramref name="preferred"/> hacia abajo) al que el texto cabe en una línea.</summary>
    public static int FitSize(Font font, string text, int preferred, float maxWidth, int minimum = SizeSmall)
    {
        int size = preferred;
        while (size > minimum && Width(font, text, size) > maxWidth)
        {
            size--;
        }

        return size;
    }

    // ------------------------------------------------------------------ piezas de datos

    /// <summary>
    /// Barra de atributo: carril oscuro con contorno, relleno ocre hasta el valor <b>base</b> y el tramo del
    /// objeto encima —verde claro si suma, rayado rojo sobre lo que quita—. El valor con el que se juega es
    /// <c>base + modificador</c>, recortado a 1..99 como hace el motor (<c>MatchPlayer</c>).
    /// </summary>
    public static void Bar(CanvasItem target, Rect2 rect, int baseValue, int modifier)
    {
        const float Max = 99f;
        int effective = Math.Clamp(baseValue + modifier, 1, 99);
        target.DrawRect(new Rect2(rect.Position + new Vector2(2f, 3f), rect.Size), Shadow);
        target.DrawRect(rect, new Color("2b2219"));

        float width = rect.Size.X;
        float baseEnd = width * Mathf.Clamp(baseValue / Max, 0f, 1f);
        float effectiveEnd = width * (effective / Max);
        var inner = new Rect2(rect.Position + new Vector2(0f, 2f), new Vector2(0f, rect.Size.Y - 4f));

        if (modifier >= 0)
        {
            target.DrawRect(new Rect2(inner.Position, new Vector2(baseEnd, inner.Size.Y)), Ochre);
            if (modifier > 0)
            {
                target.DrawRect(new Rect2(inner.Position + new Vector2(baseEnd, 0f), new Vector2(effectiveEnd - baseEnd, inner.Size.Y)), GreenLight);
            }

            // Brillo de pincel arriba: da volumen sin degradado.
            target.DrawLine(inner.Position + new Vector2(2f, 2f), inner.Position + new Vector2(Mathf.Max(2f, effectiveEnd - 3f), 2f), new Color(1f, 1f, 1f, 0.35f), 1.5f);
        }
        else
        {
            var fill = effective <= 15 ? Red : Ochre;
            target.DrawRect(new Rect2(inner.Position, new Vector2(effectiveEnd, inner.Size.Y)), fill);

            // Lo que el objeto quita: rayado rojo entre el valor final y el base.
            var lost = new Rect2(inner.Position + new Vector2(effectiveEnd, 0f), new Vector2(baseEnd - effectiveEnd, inner.Size.Y));
            target.DrawRect(lost, new Color(Red, 0.35f));
            Hatch(target, lost, new Color(Red, 0.9f), 5f);
        }

        target.DrawRect(rect, Black, false, 2.5f);
    }

    /// <summary>Trama diagonal dentro de un rectángulo (recortada a él).</summary>
    public static void Hatch(CanvasItem target, Rect2 rect, Color color, float spacing)
    {
        if (rect.Size.X <= 0.5f || rect.Size.Y <= 0.5f)
        {
            return;
        }

        Style.DrawHatch(target, rect, color, spacing);
    }

    /// <summary>
    /// Modificador con signo en grande (+20 verde, −60 rojo en placa), el que tiene que leerse de un
    /// vistazo. El negativo lleva placa roja con letra clara: más contundente que un número rojo suelto.
    /// </summary>
    public static float Modifier(CanvasItem target, Vector2 topLeft, int value, int size = 18)
    {
        if (value == 0)
        {
            return 0f;
        }

        string text = UiText.Signed(value);
        var font = Heavy;
        float width = Width(font, text, size);
        if (value > 0)
        {
            Outlined(target, font, topLeft, text, size, GreenLight, Black, 4);
            return width;
        }

        var plate = new Rect2(topLeft + new Vector2(-4f, -1f), new Vector2(width + 8f, font.GetHeight(size) + 2f));
        Slab(target, plate, Red, value * 13, 1f, 2f, new Vector2(2f, 3f));
        Text(target, font, topLeft, text, size, Paper);
        return width + 4f;
    }

    /// <summary>Insignia redonda de nivel: disco negro con la cifra clara, como la de la referencia.</summary>
    public static void LevelBadge(CanvasItem target, Vector2 center, float radius, int level)
    {
        target.DrawCircle(center + new Vector2(1.5f, 2f), radius, Shadow);
        Disc(target, center, radius, Black, 2f);
        target.DrawArc(center, radius - 2.5f, 0f, Mathf.Tau, 24, Paper, 1.4f, true);
        string text = level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int size = Mathf.RoundToInt(radius * 1.25f);
        var font = Heavy;
        var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1f, size);
        Text(target, font, center - new Vector2(textSize.X / 2f, (font.GetHeight(size) / 2f) + 0.5f), text, size, Paper);
    }
}
