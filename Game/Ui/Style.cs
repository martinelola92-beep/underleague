using System.Collections.Generic;
using Godot;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui;

/// <summary>
/// Paleta, medidas y primitivas de dibujo de la interfaz. Un solo sitio para las decisiones visuales que
/// las demás pantallas heredan (UI-021).
/// <para>
/// <b>Dos tamaños de texto y ninguno menor de 11 px a 1280x800</b> (UI-004): <see cref="TextSmall"/> para
/// el cuerpo y <see cref="TextLarge"/> para los títulos y el nombre del jugador expandido. No hay un
/// tercero. <b>Color y forma siempre juntos</b> (UI-002): cada posición tiene color y silueta, cada
/// estado físico color e icono, y las dos capas de la zona de acción se distinguen por tono <b>y</b> por
/// tipo de borde (sólido frente a punteado con trama).
/// </para>
/// </summary>
public static class Style
{
    /// <summary>Cuerpo de texto. 12 px a 1280x800, por encima del mínimo de UI-004.</summary>
    public const int TextSmall = 12;

    /// <summary>Titulares y nombre del jugador. El segundo y último tamaño (UI-004).</summary>
    public const int TextLarge = 17;

    /// <summary>Alto de la ficha colapsada (UI-011): una tira de 24 px, ni uno más.</summary>
    public const int CollapsedHeight = 24;

    public static readonly Color Background = new("14171c");
    public static readonly Color Panel = new("1c2027");
    public static readonly Color PanelSoft = new("242933");
    public static readonly Color Text = new("e6e9ee");
    public static readonly Color TextDim = new("97a0ae");
    public static readonly Color Accent = new("f0b429");
    public static readonly Color Line = new("39404d");

    public static readonly Color Grass = new("223028");
    public static readonly Color GrassOwn = new("2a3b31");
    public static readonly Color GrassLine = new("3d5346");

    /// <summary>Capa 1 de RF-045: la zona propia, tono sólido y borde continuo.</summary>
    public static readonly Color ZoneFill = new(0.31f, 0.68f, 0.92f, 0.30f);
    public static readonly Color ZoneEdge = new(0.47f, 0.80f, 1.00f, 0.95f);

    /// <summary>Capa 2 de RF-045: el margen exterior, tono claro, borde punteado y trama diagonal.</summary>
    public static readonly Color MarginFill = new(0.47f, 0.80f, 1.00f, 0.07f);
    public static readonly Color MarginEdge = new(0.62f, 0.86f, 1.00f, 0.60f);

    public static readonly Color LinkLine = new(0.72f, 0.76f, 0.83f, 0.45f);
    public static readonly Color LinkCreated = new("5fd07a");
    public static readonly Color LinkBroken = new("e2585a");
    public static readonly Color Hole = new("e2585a");
    public static readonly Color Cursor = new("f0b429");

    private static readonly Color[] PositionColors =
    {
        new("e8c547"), // portero
        new("4f86c6"), // defensa
        new("5fad56"), // centrocampista
        new("d9544d"), // delantero
    };

    private static readonly Color[] StateColors =
    {
        new("5fad56"), // sano
        new("e8c547"), // lesión leve
        new("d9544d"), // lesión grave
        new("6b7280"), // muerto
    };

    /// <summary>Color de la posición (RF fase 1: círculos de colores, sin arte).</summary>
    public static Color Of(Position position) => PositionColors[(int)position];

    /// <summary>Color del estado físico (UI-002: cuatro colores, y cuatro iconos en <see cref="StateIcon"/>).</summary>
    public static Color Of(PhysicalState state) => StateColors[(int)state];

    /// <summary>Rampa del mapa de calor de cobertura (ADR 0029 §4); 0 jugadores se pinta aparte, como hueco.</summary>
    public static Color CoverageColor(int count, int max)
    {
        if (count <= 0)
        {
            return new Color(Hole, 0.32f);
        }

        float t = max <= 1 ? 1f : (count - 1) / (float)(max - 1);
        return new Color(0.20f + (0.55f * t), 0.55f + (0.30f * t), 0.35f + (0.10f * t), 0.20f + (0.45f * t));
    }

    /// <summary>Dibuja el texto tomando <paramref name="topLeft"/> como esquina, no como línea base.</summary>
    public static void DrawText(CanvasItem target, Font font, Vector2 topLeft, string text, int size, Color color, float maxWidth = -1f)
    {
        target.DrawString(font, new Vector2(topLeft.X, topLeft.Y + font.GetAscent(size)), text, HorizontalAlignment.Left, maxWidth, size, color);
    }

    /// <summary>Parte el texto en líneas que caben en <paramref name="maxWidth"/> (DrawString recorta, no parte).</summary>
    public static List<string> Wrap(Font font, string text, int size, float maxWidth)
    {
        var lines = new List<string>();
        string current = string.Empty;
        foreach (string word in text.Split(' '))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1f, size).X <= maxWidth || current.Length == 0)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }

    /// <summary>Línea punteada: la forma con la que el margen exterior se distingue de la zona sin depender del color (UI-002).</summary>
    public static void DrawDashed(CanvasItem target, Vector2 from, Vector2 to, Color color, float width, float dash = 5f)
    {
        float length = from.DistanceTo(to);
        if (length <= 0.01f)
        {
            return;
        }

        Vector2 step = (to - from) / length;
        for (float d = 0f; d < length; d += dash * 2f)
        {
            target.DrawLine(from + (step * d), from + (step * Mathf.Min(d + dash, length)), color, width);
        }
    }

    /// <summary>Trama diagonal dentro de una casilla: la segunda marca de forma del margen exterior.</summary>
    public static void DrawHatch(CanvasItem target, Rect2 rect, Color color, float spacing = 7f)
    {
        for (float x = rect.Position.X - rect.Size.Y; x < rect.Position.X + rect.Size.X; x += spacing)
        {
            float x0 = Mathf.Max(x, rect.Position.X);
            float y0 = rect.Position.Y + (x0 - x);
            float x1 = Mathf.Min(x + rect.Size.Y, rect.Position.X + rect.Size.X);
            float y1 = rect.Position.Y + (x1 - x);
            if (x1 > x0 && y0 < rect.Position.Y + rect.Size.Y)
            {
                target.DrawLine(new Vector2(x0, y0), new Vector2(x1, y1), color, 1f);
            }
        }
    }

    /// <summary>
    /// Silueta de la posición (UI-002: la posición no se distingue solo por color). Portero cuadrado,
    /// defensa triángulo hacia su portería, centrocampista rombo, delantero triángulo hacia la rival.
    /// </summary>
    public static void DrawPositionIcon(CanvasItem target, Vector2 center, float radius, Position position, Color color)
    {
        switch (position)
        {
            case Position.Goalkeeper:
                target.DrawRect(new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), color);
                break;
            case Position.Defender:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(-radius, 0f),
                    center + new Vector2(radius, -radius),
                    center + new Vector2(radius, radius),
                }, color);
                break;
            case Position.Midfielder:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius, 0f),
                    center + new Vector2(0f, radius),
                    center + new Vector2(-radius, 0f),
                }, color);
                break;
            default:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(radius, 0f),
                    center + new Vector2(-radius, -radius),
                    center + new Vector2(-radius, radius),
                }, color);
                break;
        }
    }

    /// <summary>
    /// Icono del estado físico (UI-002: cuatro colores <b>y</b> cuatro iconos). Sano círculo lleno,
    /// lesión leve triángulo, lesión grave cuadrado partido, muerto cruz.
    /// </summary>
    public static void DrawStateIcon(CanvasItem target, Vector2 center, float radius, PhysicalState state, Color color)
    {
        switch (state)
        {
            case PhysicalState.Healthy:
                target.DrawCircle(center, radius, color);
                break;
            case PhysicalState.MinorInjury:
                target.DrawColoredPolygon(new[]
                {
                    center + new Vector2(0f, -radius),
                    center + new Vector2(radius, radius),
                    center + new Vector2(-radius, radius),
                }, color);
                break;
            case PhysicalState.SevereInjury:
                target.DrawRect(new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2f, radius)), color);
                target.DrawRect(new Rect2(center + new Vector2(-radius, radius * 0.35f), new Vector2(radius * 2f, radius * 0.65f)), color);
                break;
            default:
                target.DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 2f);
                target.DrawLine(center + new Vector2(-radius, radius), center + new Vector2(radius, -radius), color, 2f);
                break;
        }
    }
}
