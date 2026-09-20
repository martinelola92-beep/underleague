using Godot;
using Underleague.Game.Ui.Broadcast;

namespace Underleague.Game.Ui;

/// <summary>
/// Pizarra de pizarrón para la cuadrícula de colocación de Equipo (decisión del revisor, 20 sep 2026:
/// "quédate sobre todo con el concepto de la pizarra y líneas dibujadas con tiza"). Es a
/// <c>PitchView</c> lo que <see cref="ParchmentPanel"/>/<see cref="Broadcast.Pregon"/> son al resto de
/// paneles: solo dibujo, sin estado ni lógica de colocación (RT-014 sigue resuelta por
/// <c>Sim.Placement.PlacementView</c>; esto pinta exactamente lo que la pantalla ya tiene calculado).
/// <para>
/// Fondo de pizarrón, marco de madera con herrajes de esquina, y líneas de tiza con
/// <b>trazo irregular determinista</b> (reutiliza <see cref="Pregon.Jitter"/>, nunca <c>System.Random</c>,
/// misma disciplina que RT-021 aunque <c>/Game</c> no está sujeto a ella): dos ejecuciones dibujan el
/// mismo trazo, así que una captura de referencia sigue siendo comparable.
/// </para>
/// </summary>
public static class Chalkboard
{
    /// <summary>Fondo de pizarrón: verde casi negro. La mitad propia usa <see cref="SlateOwn"/>, algo más clara.</summary>
    public static readonly Color Slate = new("1b241e");

    public static readonly Color SlateOwn = new("222f27");

    /// <summary>Tiza, para líneas y números: blanco roto, distinto del blanco puro del balón (Style.Ball).</summary>
    public static readonly Color Chalk = new("e8e4da");

    public static readonly Color Frame = new("6b4a24");

    public static readonly Color FrameEdge = new("c9982f");

    /// <summary>
    /// Marco de madera con borde rasgado y clavos de esquina, ceñido a <paramref name="boardSize"/> —el
    /// área útil de la cuadrícula, el mismo rectángulo (0,0)-(cell*Columns, cell*Rows) que usan
    /// <c>CellAt</c>/<c>CenterOf</c>— más <paramref name="overflow"/> px de margen <b>igual por los
    /// cuatro lados</b> (arreglo del revisor, 20 sep 2026: el marco desbordaba hasta el borde del control
    /// aunque la cuadrícula, limitada por la altura, no llegara a llenarlo, dejando una banda de madera
    /// vacía a la derecha). No se rellena nada más allá de ese margen: lo que quede fuera es el panel de
    /// pergamino de detrás, no madera.
    /// </summary>
    public static void DrawFrame(CanvasItem target, Vector2 boardSize, float overflow, int seed)
    {
        var outer = Pregon.RoughRect(boardSize.X + (2f * overflow), boardSize.Y + (2f * overflow), 2f, seed);
        var shifted = Shift(outer, new Vector2(-overflow, -overflow));
        target.DrawColoredPolygon(shifted, Frame);
        target.DrawPolyline(Close(shifted), FrameEdge, 2f, true);

        float nail = overflow * 0.4f;
        DrawNail(target, new Vector2(-nail, -nail));
        DrawNail(target, new Vector2(boardSize.X + nail, -nail));
        DrawNail(target, new Vector2(-nail, boardSize.Y + nail));
        DrawNail(target, new Vector2(boardSize.X + nail, boardSize.Y + nail));
    }

    private static void DrawNail(CanvasItem target, Vector2 point)
    {
        target.DrawCircle(point, 2.2f, FrameEdge);
        target.DrawCircle(point, 1f, Frame.Darkened(0.3f));
    }

    /// <summary>
    /// Trazo de tiza entre dos puntos: recto en los extremos (para que encaje con el resto de la retícula
    /// sin huecos) y con un pequeño desvío perpendicular determinista en los puntos intermedios.
    /// </summary>
    public static void ChalkLine(CanvasItem target, Vector2 a, Vector2 b, Color color, float width, int seed, float amplitude = 1.3f, int segments = 5)
    {
        var delta = b - a;
        float length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        var normal = new Vector2(-delta.Y, delta.X) / length;
        var previous = a;
        for (int i = 1; i <= segments; i++)
        {
            bool last = i == segments;
            var point = last ? b : a + (delta * i / segments) + (normal * Pregon.Jitter((seed * 131) + i, amplitude));
            target.DrawLine(previous, point, color, width);
            previous = point;
        }
    }

    /// <summary>Trazo de tiza discontinuo: cada guion es, a su vez, un <see cref="ChalkLine"/> corto.</summary>
    public static void ChalkDashedLine(CanvasItem target, Vector2 a, Vector2 b, Color color, float width, int seed, float dash = 6f)
    {
        float length = a.DistanceTo(b);
        if (length <= 0.01f)
        {
            return;
        }

        var step = (b - a) / length;
        int chunk = 0;
        for (float d = 0f; d < length; d += dash * 2f)
        {
            var from = a + (step * d);
            var to = a + (step * Mathf.Min(d + dash, length));
            ChalkLine(target, from, to, color, width, seed + (chunk++ * 17), amplitude: 0.8f, segments: 2);
        }
    }

    /// <summary>Rectángulo de tiza: cuatro <see cref="ChalkLine"/>, una por lado, cada una con su propia semilla.</summary>
    public static void ChalkRect(CanvasItem target, Rect2 rect, Color color, float width, int seed)
    {
        var tl = rect.Position;
        var tr = rect.Position + new Vector2(rect.Size.X, 0f);
        var br = rect.Position + rect.Size;
        var bl = rect.Position + new Vector2(0f, rect.Size.Y);
        ChalkLine(target, tl, tr, color, width, seed);
        ChalkLine(target, tr, br, color, width, seed + 31);
        ChalkLine(target, br, bl, color, width, seed + 62);
        ChalkLine(target, bl, tl, color, width, seed + 93);
    }

    private static Vector2[] Shift(Vector2[] poly, Vector2 offset)
    {
        var shifted = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            shifted[i] = poly[i] + offset;
        }

        return shifted;
    }

    private static Vector2[] Close(Vector2[] poly)
    {
        var closed = new Vector2[poly.Length + 1];
        System.Array.Copy(poly, closed, poly.Length);
        closed[poly.Length] = poly[0];
        return closed;
    }
}
