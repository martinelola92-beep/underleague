using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Ui;

/// <summary>Una marca en la barra del partido: el fotograma de un suceso que no hay que perderse.</summary>
public readonly record struct TimelineMark(int Frame, Color Color);

/// <summary>
/// Barra de reproducción del partido: dónde va, hasta dónde llega y <b>dónde están los goles</b>. Se
/// puede pulsar y arrastrar para moverse por el partido, hacia delante y hacia atrás — que es la mitad
/// del valor de todo esto: un gol raro se quiere volver a ver.
/// <para>
/// Las marcas sustituyen a la columna de sucesos clave como forma de <b>llegar</b> a ellos: la lista dice
/// qué pasó, la barra dice cuándo y deja ir. No calcula nada (RT-014): recibe los fotogramas ya resueltos.
/// </para>
/// </summary>
public partial class MatchTimelineView : Control
{
    /// <summary>Se ha pedido saltar a ese fotograma (clic o arrastre sobre la barra).</summary>
    [Signal]
    public delegate void SeekedEventHandler(int frame);

    private bool _dragging;

    /// <summary>Fotogramas del partido; 0 mientras no haya traza.</summary>
    public int FrameCount { get; set; }

    /// <summary>Fotograma que se está pintando.</summary>
    public int Frame { get; set; }

    /// <summary>Fotograma en el que acaba el tiempo reglamentario; -1 si el partido no llegó a la prórroga.</summary>
    public int RegulationFrame { get; set; } = -1;

    /// <summary>Sucesos marcados sobre la barra, en orden de fotograma.</summary>
    public IReadOnlyList<TimelineMark> Marks { get; set; } = System.Array.Empty<TimelineMark>();

    public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                if (button.Pressed)
                {
                    EmitSignal(SignalName.Seeked, FrameAt(button.Position.X));
                }

                AcceptEvent();
                break;

            case InputEventMouseMotion motion when _dragging:
                EmitSignal(SignalName.Seeked, FrameAt(motion.Position.X));
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        float width = Size.X;
        float height = Size.Y;
        DrawRect(new Rect2(0f, 0f, width, height), Style.PanelSoft);

        if (FrameCount <= 1)
        {
            return;
        }

        float played = width * Mathf.Clamp(Frame / (float)(FrameCount - 1), 0f, 1f);
        DrawRect(new Rect2(0f, 0f, played, height), new Color(Style.Accent, 0.22f));

        if (RegulationFrame > 0 && RegulationFrame < FrameCount)
        {
            float x = width * RegulationFrame / (FrameCount - 1);
            Style.DrawDashed(this, new Vector2(x, 0f), new Vector2(x, height), Style.TextDim, 1.5f, 3f);
        }

        foreach (var mark in Marks)
        {
            float x = width * Mathf.Clamp(mark.Frame / (float)(FrameCount - 1), 0f, 1f);
            DrawLine(new Vector2(x, 2f), new Vector2(x, height - 2f), mark.Color, 2f);
        }

        DrawLine(new Vector2(played, 0f), new Vector2(played, height), Style.Accent, 2f);
        DrawColoredPolygon(
            new[]
            {
                new Vector2(played - 4f, 0f),
                new Vector2(played + 4f, 0f),
                new Vector2(played, 6f),
            },
            Style.Accent);
        DrawRect(new Rect2(0f, 0f, width, height), Style.Line, false, 1f);
    }

    private int FrameAt(float x) =>
        FrameCount <= 1 ? 0 : Mathf.Clamp(Mathf.RoundToInt(x / Size.X * (FrameCount - 1)), 0, FrameCount - 1);
}
