using Godot;
using Underleague.Game.Ui.Broadcast;

namespace Underleague.Game.Ui;

/// <summary>
/// Fondo de mesa de madera para la pantalla de Mapa (encargo mapa-pregon): el mismo tono oscuro de
/// <see cref="Style.Background"/> que usa <see cref="Widgets.Background"/> en el resto de pantallas, pero
/// con vetas y clavos dibujados por código en vez de un simple color plano — regla de fase 10 de
/// <c>CLAUDE.md</c>: marcador de posición procedural, nunca una imagen importada.
/// <para>
/// Vetas y clavos con <b>trazo irregular determinista</b> (reutiliza <see cref="Pregon.Jitter"/>, nunca
/// <c>System.Random</c>, misma disciplina que RT-021 aunque <c>/Game</c> no está sujeto a ella): con una
/// semilla fija —la mesa no depende del estado de la run— dos ejecuciones pintan la misma mesa, así que
/// una captura de referencia sigue siendo comparable.
/// </para>
/// </summary>
public partial class WoodTable : Control
{
    private const int Seed = 777;

    private static readonly Color Grain = new("140f09");

    private static readonly Color Nail = new("4a3a24");

    private static readonly Color NailHighlight = new("8a6a3c");

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, Size), Style.Background);
        DrawGrain();
        DrawNails();
    }

    /// <summary>Vetas: líneas horizontales onduladas, más oscuras que la base, a intervalos irregulares.</summary>
    private void DrawGrain()
    {
        float y = 18f;
        int line = 0;
        while (y < Size.Y - 10f)
        {
            DrawWavyLine(y, line);
            y += 36f + ((Pregon.Jitter(Seed + (line * 41), 1f) + 1f) * 9f);
            line++;
        }
    }

    private void DrawWavyLine(float y, int line)
    {
        const int Segments = 14;
        var previous = new Vector2(0f, y + Pregon.Jitter((Seed * 13) + (line * 97), 4f));
        for (int i = 1; i <= Segments; i++)
        {
            float x = Size.X * i / Segments;
            float wave = Pregon.Jitter((Seed * 13) + (line * 97) + i, 4f);
            var point = new Vector2(x, y + wave);
            DrawLine(previous, point, new Color(Grain, 0.35f), 1.3f);
            previous = point;
        }
    }

    /// <summary>Clavos: una rejilla dispersa de cabezas de clavo de dos tonos, como los de <c>Chalkboard</c>.</summary>
    private void DrawNails()
    {
        const float Spacing = 210f;
        for (float x = 40f; x < Size.X; x += Spacing)
        {
            for (float y = 40f; y < Size.Y; y += Spacing)
            {
                var at = new Vector2(x + Pregon.Jitter((int)(x + y), 10f), y + Pregon.Jitter((int)(x - y), 10f));
                DrawCircle(at, 2.6f, Nail);
                DrawCircle(at, 1.1f, NailHighlight);
            }
        }
    }
}
