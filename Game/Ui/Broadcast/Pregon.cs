using System;
using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui.Broadcast;

/// <summary>
/// Paleta, tipografías y primitivas de dibujo de la retransmisión de partido — «voz de pregón»
/// (ADR 0119, ADR 0120; dirección visual en <c>docs/ui/README.md</c> §1). Es a los componentes de
/// <c>Game/Ui/Broadcast</c> lo que <see cref="Style"/> es al resto de la interfaz: un solo sitio para las
/// decisiones de color, tipo y forma, para que ningún componente las repita ni las invente por su cuenta.
/// <para>
/// Materia de fiesta popular medieval, no de retransmisión deportiva de cristal: tela y heráldica
/// (identidad de equipo), madera (estructura persistente), papel y pergamino (información que se lee),
/// voz de pregón (acontecimientos). Color heráldico profundo: nuestro equipo azur y oro, el rival gules y
/// sable; pergamino y tinta parda para lo impreso; el rojo de sangre solo para daño y muerte.
/// </para>
/// </summary>
public static class Pregon
{
    // --- Paleta (docs/ui/README.md §1; prototipo D.3, variantes «P» de docs/ui/prototipo/prototipo-ui.patch) ---
    public static readonly Color Azur = new("1e3a6e");
    public static readonly Color AzurDark = new("142850");
    public static readonly Color Or = new("c9982f");
    public static readonly Color Gules = new("8e1f1f");
    public static readonly Color GulesDark = new("5e1414");
    public static readonly Color Sable = new("1b1712");
    public static readonly Color Vellum = new("efe2c0");
    public static readonly Color VellumEdge = new("b89c68");

    /// <summary>Tinta parda: el cuerpo de texto sobre pergamino, nunca sobre tela ni madera.</summary>
    public static readonly Color InkBrown = new("3a2a1a");

    /// <summary>Rojo de sangre: reservado a daño y muerte (manchas, marcas de estado grave).</summary>
    public static readonly Color Blood = new("b3121b");

    public static readonly Color Wood = new("7a5234");
    public static readonly Color WoodLight = new("c89b62");

    /// <summary>Lacre de los sellos y de los bandos (Edict, Stamp de falta): más oscuro que Blood a propósito.</summary>
    public static readonly Color Wax = new("8f1d1d");

    // --- Tamaños. Texto esencial >= 20 px lógicos (regla del encargo; el mínimo general de UI-004 es 11) ---
    public const int SizeEssential = 20;
    public const int SizeDataSmall = 20;
    public const int SizeData = 22;
    public const int SizeHeader = 26;
    public const int SizeTitleSmall = 60;

    // 130 (revisión visual de la galería, 19 sep 2026): a 104 «Gol» —el único título que lo usa, los de
    // más de 4 letras caen en SizeTitleSmall— se leía pequeño junto al resto del gonfalón en
    // docs/ui/capturas/gol-1920x1080.jpg; el paño (500x640) tiene sitio de sobra.
    public const int SizeTitleLarge = 130;
    public const int SizeBody = 26;
    public const int SizeScoreBoard = 56;
    public const int SizeScoreFinal = 96;

    // --- Fuentes: perezosas, con caída a ThemeDB.FallbackFont si el fichero no está importado. ---
    private static FontFile? _fell;
    private static FontFile? _fellBig;
    private static FontFile? _fellItalic;
    private static FontFile? _titular;
    private static FontFile? _serif;
    private static FontFile? _serifItalic;
    private static FontFile? _dataBold;
    private static FontFile? _dataSemiBold;
    private static FontFile? _score;

    /// <summary>
    /// IM Fell English SC: la voz que proclama en su versión original (romana pura). Sin uso desde el 20
    /// sep 2026 (decisión del revisor: «letra gótica en algún sitio, o una serif intermedia») — la
    /// proclama pasa a <see cref="Titular"/> y el resto de la voz serif a <see cref="Serif"/>. Se deja
    /// cargable, sin llamadas en el resto del árbol, por si el revisor quiere volver a ella.
    /// </summary>
    public static Font Fell => LoadFont(ref _fell, "res://Fonts/IMFellEnglishSC.ttf");

    /// <summary>IM Fell Great Primer SC: la misma voz, para el cuerpo grande del título. Sin uso, ver <see cref="Fell"/>.</summary>
    public static Font FellBig => LoadFont(ref _fellBig, "res://Fonts/IMFellGreatPrimerSC.ttf");

    /// <summary>IM Fell English Italic: la misma voz, en cursiva. Sin uso, ver <see cref="Fell"/>.</summary>
    public static Font FellItalic => LoadFont(ref _fellItalic, "res://Fonts/IMFellEnglish-Italic.ttf");

    /// <summary>
    /// Grenze Gotisch: la voz que proclama — titulares y anuncios cortos (estandarte, bando, acta, sellos,
    /// cabeceras de bandeja, escudo del tablero, títulos de pantalla). Diseñada como punto intermedio entre
    /// romana y gótica (decisión del revisor, 20 sep 2026: sustituye a <see cref="Fell"/>/<see cref="FellBig"/>
    /// en ese papel). Una sola familia variable para las dos tallas de título: el tamaño lo decide el
    /// llamador, no el fichero.
    /// </summary>
    public static Font Titular => LoadFont(ref _titular, "res://Fonts/GrenzeGotisch-Variable.ttf");

    /// <summary>
    /// Grenze: la serif de cuerpo y subtítulos — lo que se lee seguido, no lo que se proclama (etiqueta de
    /// sección, subtítulo). Sustituye a <see cref="Fell"/> en ese papel (decisión del revisor, 20 sep 2026).
    /// </summary>
    public static Font Serif => LoadFont(ref _serif, "res://Fonts/Grenze-Variable.ttf");

    /// <summary>Grenze Italic: la misma serif de cuerpo, en cursiva — sustituye a <see cref="FellItalic"/>.</summary>
    public static Font SerifItalic => LoadFont(ref _serifItalic, "res://Fonts/Grenze-Italic-Variable.ttf");

    /// <summary>Barlow Condensed Bold: la voz de los datos (nombres, tiras, botones).</summary>
    public static Font DataBold => LoadFont(ref _dataBold, "res://Fonts/BarlowCondensed-Bold.ttf");

    /// <summary>Barlow Condensed SemiBold: datos secundarios (subtítulos, ayudas).</summary>
    public static Font DataSemiBold => LoadFont(ref _dataSemiBold, "res://Fonts/BarlowCondensed-SemiBold.ttf");

    /// <summary>Cinzel: las cifras del marcador y del acta final.</summary>
    public static Font Score => LoadFont(ref _score, "res://Fonts/Cinzel-Variable.ttf");

    private static Font LoadFont(ref FontFile? cache, string path)
    {
        if (cache is not null)
        {
            return cache;
        }

        if (ResourceLoader.Exists(path) && GD.Load(path) is FontFile font)
        {
            cache = font;
            return font;
        }

        // Sin el fichero importado (fuente que falta, entorno sin `--import` corrido todavía): se cae al
        // tipo de Godot en vez de reventar. Un dato que falta se ve raro, no tira la pantalla.
        return ThemeDB.FallbackFont;
    }

    /// <summary>
    /// Theme de Godot con las tres variaciones de tipo de <see cref="Label"/> que usan los componentes de
    /// esta carpeta. El color de letra NO se fija aquí: cada componente lo decide según el fondo sobre el
    /// que proclama (pergamino, tela azur, tela gules...), así que se deja al valor por defecto de Godot y
    /// se sobrescribe por instancia con <c>AddThemeColorOverride</c>.
    /// </summary>
    public static Theme BuildTheme()
    {
        var theme = new Theme
        {
            DefaultFont = DataSemiBold,
            DefaultFontSize = SizeData,
        };

        AddLabelVariation(theme, "ProclaimLabel", Titular, SizeHeader);
        AddLabelVariation(theme, "DataLabel", DataBold, SizeData);
        AddLabelVariation(theme, "ScoreLabel", Score, SizeScoreBoard);
        return theme;
    }

    private static void AddLabelVariation(Theme theme, string typeVariation, Font font, int size)
    {
        theme.SetTypeVariation(typeVariation, "Label");
        theme.SetFont("font", typeVariation, font);
        theme.SetFontSize("font_size", typeVariation, size);
    }

    // ------------------------------------------------------------------ jitter determinista

    /// <summary>
    /// Jitter determinista, función del índice — nunca <c>System.Random</c> (misma regla que RT-021 aplica
    /// aquí por disciplina, aunque <c>/Game</c> no está sujeto a ella): dos ejecuciones con el mismo índice
    /// dan siempre el mismo trazo, así una captura de referencia sigue siendo comparable.
    /// </summary>
    public static float Jitter(int seed, float amplitude)
    {
        float h = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
        float frac = h - Mathf.Floor(h);
        return amplitude * ((2f * frac) - 1f);
    }

    // ------------------------------------------------------------------ geometría compartida

    /// <summary>Rectángulo con borde irregular determinista (papel rasgado, tabla desbastada, tela tejida).</summary>
    public static Vector2[] RoughRect(float w, float h, float amplitude, int seed, int steps = 10)
    {
        int half = Math.Max(1, steps / 2);
        var pts = new List<Vector2>(steps + steps + (2 * half));
        for (int i = 0; i < steps; i++)
        {
            pts.Add(new Vector2(w * i / steps, Jitter((seed * 97) + i, amplitude)));
        }

        for (int i = 0; i < half; i++)
        {
            pts.Add(new Vector2(w + Jitter((seed * 97) + steps + i, amplitude), h * i / half));
        }

        for (int i = steps; i > 0; i--)
        {
            pts.Add(new Vector2(w * i / steps, h + Jitter((seed * 97) + steps + half + (steps - i), amplitude)));
        }

        for (int i = half; i > 0; i--)
        {
            pts.Add(new Vector2(Jitter((seed * 97) + (2 * steps) + half + (half - i), amplitude), h * i / half));
        }

        return pts.ToArray();
    }

    /// <summary>Escudo heráldico en punta (la forma de identidad de equipo; UI-002 pide color y forma).</summary>
    public static Vector2[] ShieldPoly(float w, float h) => new[]
    {
        new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h * 0.55f), new Vector2(w / 2f, h), new Vector2(0, h * 0.55f),
    };

    /// <summary>Gonfalón/pendón con cola de golondrina (estandartes, banderolas).</summary>
    public static Vector2[] Swallowtail(float w, float h, float notch) => new[]
    {
        new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(w / 2f, h - notch), new Vector2(0, h),
    };

    /// <summary>Estrella/burst (sello de lacre, marca de estado sano).</summary>
    public static Vector2[] Burst(float r1, float r2, int n, Vector2 center)
    {
        var pts = new Vector2[n * 2];
        for (int i = 0; i < (n * 2); i++)
        {
            float ang = Mathf.Pi * i / n;
            float r = i % 2 == 0 ? r1 : r2;
            pts[i] = center + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
        }

        return pts;
    }

    // ------------------------------------------------------------------ dibujo compartido

    private static Vector2[] Shift(Vector2[] poly, Vector2 offset)
    {
        var r = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            r[i] = poly[i] + offset;
        }

        return r;
    }

    private static Vector2[] Close(Vector2[] poly)
    {
        var r = new Vector2[poly.Length + 1];
        Array.Copy(poly, r, poly.Length);
        r[poly.Length] = poly[0];
        return r;
    }

    /// <summary>
    /// Papel, pergamino, tela o madera: relleno con borde irregular, sombra dura debajo y contorno fino.
    /// La pieza base de casi todos los componentes (tiras, sellos, estandartes, bandos, tablero).
    /// </summary>
    public static void DrawParchment(CanvasItem target, Vector2 topLeft, float w, float h, Color fill, Color edge, int seed, float amplitude = 2f, float edgeWidth = 2f, Vector2? shadowOffset = null)
    {
        var poly = RoughRect(w, h, amplitude, seed);
        var shadow = shadowOffset ?? new Vector2(4f, 5f);
        target.DrawColoredPolygon(Shift(poly, topLeft + shadow), new Color(0f, 0f, 0f, 0.35f));
        var shifted = Shift(poly, topLeft);
        target.DrawColoredPolygon(shifted, fill);
        if (edgeWidth > 0f)
        {
            target.DrawPolyline(Close(shifted), edge, edgeWidth, true);
        }
    }

    /// <summary>
    /// Escudo heráldico partido: la identidad de equipo por color <b>y</b> forma (UI-002). El propio: azur
    /// con una banda diagonal de oro. El rival: gules con un palo de sable.
    /// </summary>
    public static void DrawShield(CanvasItem target, Vector2 topLeft, float w, float h, bool ours)
    {
        var poly = Shift(ShieldPoly(w, h), topLeft);
        target.DrawColoredPolygon(poly, ours ? Azur : Gules);
        if (ours)
        {
            target.DrawLine(topLeft + new Vector2(w * 0.72f, 0f), topLeft + new Vector2(w * 0.72f, h * 0.7f), Or, w * 0.5f);
        }
        else
        {
            target.DrawLine(topLeft, topLeft + new Vector2(w, h * 0.78f), Sable, w * 0.26f);
        }

        target.DrawPolyline(Close(poly), Sable, 2f, true);
    }

    /// <summary>Orla bordada: un filete a <paramref name="inset"/> px hacia dentro del contorno dado.</summary>
    public static void DrawOrla(CanvasItem target, Vector2[] outerLocalPoly, Vector2 topLeft, float inset, Color color, float width = 1.6f)
    {
        var center = Vector2.Zero;
        foreach (var p in outerLocalPoly)
        {
            center += p;
        }

        center /= outerLocalPoly.Length;
        var inner = new Vector2[outerLocalPoly.Length];
        for (int i = 0; i < outerLocalPoly.Length; i++)
        {
            var dir = center - outerLocalPoly[i];
            inner[i] = outerLocalPoly[i] + (dir.LengthSquared() > 0.0001f ? dir.Normalized() * inset : Vector2.Zero) + topLeft;
        }

        target.DrawPolyline(Close(inner), color, width, true);
    }

    /// <summary>
    /// Trompeta de heraldo: tubo y pabellón en oro, la voz visual del pregón (<see cref="HeraldBanner"/>,
    /// <see cref="ProclamationBand"/>, <see cref="Edict"/>). <paramref name="rotationDeg"/> en grados.
    /// </summary>
    public static void DrawTrumpet(CanvasItem target, Vector2 at, float len, float rotationDeg, bool mirror = false)
    {
        float rot = Mathf.DegToRad(rotationDeg);
        Tilted(target, at, rot, () =>
        {
            var tube = new[] { new Vector2(0, 0), new Vector2(len, -8), new Vector2(len, 4), new Vector2(0, 10) };
            target.DrawColoredPolygon(tube, Or);
            target.DrawPolyline(Close(tube), InkBrown, 2f, true);

            var bell = Shift(
                new[] { new Vector2(0, 18), new Vector2(60, -10), new Vector2(66, 20), new Vector2(60, 50), new Vector2(0, 26) },
                new Vector2(len - 6, -22));
            target.DrawColoredPolygon(bell, Or);
            target.DrawPolyline(Close(bell), InkBrown, 2f, true);
        }, mirror ? new Vector2(-1f, 1f) : Vector2.One);
    }

    /// <summary>Como <see cref="Style.DrawText"/>, pero centrado en <paramref name="width"/> (acta final, títulos del bando).</summary>
    public static void DrawTextCentered(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color color, float width)
    {
        target.DrawString(font, new Vector2(topLeft.X, topLeft.Y + font.GetAscent(size)), text, HorizontalAlignment.Center, width, size, color);
    }

    /// <summary>
    /// Texto de varias líneas: respeta los saltos de línea que ya trae <paramref name="text"/> y envuelve
    /// lo que no quepa en <paramref name="maxWidth"/> (<see cref="Style.Wrap"/> hace las dos cosas).
    /// <see cref="Style.DrawText"/> por sí solo dibuja una sola línea — Godot no rompe por <c>\n</c>.
    /// </summary>
    public static void DrawWrappedText(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color color, float maxWidth, bool centered = false)
    {
        float lineHeight = font.GetHeight(size) * 1.2f;
        var lines = Style.Wrap(font, text, size, maxWidth);
        for (int i = 0; i < lines.Count; i++)
        {
            var at = topLeft + new Vector2(0f, i * lineHeight);
            if (centered)
            {
                DrawTextCentered(target, font, at, lines[i], size, color, maxWidth);
            }
            else
            {
                Style.DrawText(target, font, at, lines[i], size, color, maxWidth);
            }
        }
    }

    /// <summary>
    /// Recorta <paramref name="text"/> con puntos suspensivos hasta que quepa en <paramref name="maxWidth"/>
    /// (revisión visual del orquestador, 19 sep 2026): <c>DrawString</c> con un ancho máximo no envuelve,
    /// <b>corta a media letra</b> — nunca se le pasa texto que no quepa ya medido, se mide y se recorta
    /// aquí antes de dibujar. Nombres largos del tablero y de las tiras (C12).
    /// </summary>
    public static string Ellipsize(Font font, string text, int size, float maxWidth)
    {
        if (font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X <= maxWidth)
        {
            return text;
        }

        const string Ellipsis = "…";
        for (int length = text.Length - 1; length > 0; length--)
        {
            string candidate = text[..length].TrimEnd() + Ellipsis;
            if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1f, size).X <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }

    /// <summary>Como <see cref="Style.DrawText"/>, pero con <see cref="Ellipsize"/> aplicado antes de dibujar.</summary>
    public static void DrawTextEllipsized(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color color, float maxWidth)
    {
        Style.DrawText(target, font, topLeft, Ellipsize(font, text, size, maxWidth), size, color);
    }

    /// <summary>
    /// El tamaño más grande, entre <paramref name="preferredSize"/> y <see cref="SizeEssential"/> (20 px
    /// lógicos, el mínimo del encargo), con el que <paramref name="text"/> cabe en una línea de
    /// <paramref name="maxWidth"/>. Nunca baja de <see cref="SizeEssential"/> aunque no llegue a caber:
    /// para eso está <see cref="DrawFittedTitle"/>, que parte en dos líneas si hace falta.
    /// </summary>
    public static int FitTitleSize(Font font, string text, int preferredSize, float maxWidth)
    {
        int size = preferredSize;
        while (size > SizeEssential && font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X > maxWidth)
        {
            size -= 2;
        }

        return size;
    }

    /// <summary>
    /// Título de estandarte/bando/acta (revisión visual, 19 sep 2026: «Comienza el pa» del saque se salía
    /// del paño): se reduce con <see cref="FitTitleSize"/> hasta <see cref="SizeEssential"/>; si ni así
    /// cabe en una línea, se parte en dos con <see cref="Style.Wrap"/>, centradas sobre el mismo punto.
    /// </summary>
    public static void DrawFittedTitle(CanvasItem target, Font font, Vector2 topLeft, string text, int preferredSize, Color color, float maxWidth)
    {
        int size = FitTitleSize(font, text, preferredSize, maxWidth);
        if (font.GetStringSize(text, HorizontalAlignment.Left, -1f, size).X <= maxWidth)
        {
            DrawTextCentered(target, font, topLeft, text, size, color, maxWidth);
            return;
        }

        var lines = Style.Wrap(font, text, size, maxWidth);
        float lineHeight = font.GetHeight(size) * 1.1f;
        for (int i = 0; i < lines.Count; i++)
        {
            DrawTextCentered(target, font, topLeft + new Vector2(0f, i * lineHeight), lines[i], size, color, maxWidth);
        }
    }

    /// <summary>
    /// Dibuja con una transformación local (posición + rotación) activa durante <paramref name="draw"/>, y
    /// la repone al terminar. Así se tuercen sellos y placas 2-4° sin que el resto del <c>_Draw</c> herede
    /// el giro.
    /// </summary>
    public static void Tilted(CanvasItem target, Vector2 origin, float rotation, Action draw, Vector2? scale = null)
    {
        target.DrawSetTransform(origin, rotation, scale ?? Vector2.One);
        draw();
        target.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    // ------------------------------------------------------------------ formas del estandarte (HeraldBanner)

    /// <summary>
    /// Píldora/estadio: rectángulo con los dos extremos redondeados en semicírculo — el varal de madera en
    /// el que se enrolla un pergamino colgado (<see cref="HeraldBanner"/>), nunca una tela plana.
    /// </summary>
    public static Vector2[] StadiumPoly(float w, float h, int capSteps = 12)
    {
        float r = h / 2f;
        float straight = System.Math.Max(0f, w - h);
        var pts = new List<Vector2>((capSteps * 2) + 6);
        pts.Add(new Vector2(r, 0f));
        pts.Add(new Vector2(r + straight, 0f));
        for (int i = 0; i <= capSteps; i++)
        {
            float a = -Mathf.Pi / 2f + (Mathf.Pi * i / capSteps);
            pts.Add(new Vector2(r + straight + (r * Mathf.Cos(a)), r + (r * Mathf.Sin(a))));
        }

        pts.Add(new Vector2(r + straight, h));
        pts.Add(new Vector2(r, h));
        for (int i = 0; i <= capSteps; i++)
        {
            float a = (Mathf.Pi / 2f) + (Mathf.Pi * i / capSteps);
            pts.Add(new Vector2(r + (r * Mathf.Cos(a)), r + (r * Mathf.Sin(a))));
        }

        return pts.ToArray();
    }

    /// <summary>Corona pequeña de tres puntas (pie del estandarte de gol, boceto del revisor): marcador de posición procedural, nunca un icono importado.</summary>
    /// <summary>
    /// Corona de tres puntas TRIANGULARES separadas sobre una banda (revisión del revisor, 20 sep 2026:
    /// la primera versión salía "un borrón negro" — puntas demasiado finas y pegadas entre sí, que a
    /// tamaño pequeño se fundían). Perímetro sin autointersección, recorrido en un solo sentido: banda →
    /// punta derecha → vuelta a la banda → punta central (más alta) → vuelta a la banda → punta
    /// izquierda → vuelta a la banda → cierre.
    /// </summary>
    public static Vector2[] CrownPoly(float w, float h)
    {
        float bandTop = h * 0.62f;
        return new[]
        {
            new Vector2(0f, h), new Vector2(w, h), new Vector2(w, bandTop),
            new Vector2(w * (5f / 6f), bandTop), new Vector2(w * (5f / 6f), 0f), new Vector2(w * (4f / 6f), bandTop),
            new Vector2(w * (3.5f / 6f), bandTop), new Vector2(w * 0.5f, -h * 0.18f), new Vector2(w * (2.5f / 6f), bandTop),
            new Vector2(w * (2f / 6f), bandTop), new Vector2(w * (1f / 6f), 0f), new Vector2(0f, bandTop),
        };
    }
}
