using Godot;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// El distintivo de un nodo en una lista: un disco del color de su tipo con, si es un partido, la
/// <b>silueta de su dificultad</b> dentro (RF-012). Color y forma juntos, como pide UI-002: los cinco
/// niveles se distinguen sin percibir el color, y el número va al lado en texto.
/// </summary>
public partial class NodeBadge : Control
{
    /// <summary>Nodo que se distingue. Null deja el distintivo vacío.</summary>
    public MapNode? Node { get; set; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        if (Node is null)
        {
            return;
        }

        var center = Size / 2f;
        var color = Style.Of(Node.Kind);
        DrawCircle(center, 12f, new Color(color, 0.30f));
        DrawArc(center, 12f, 0f, Mathf.Tau, 24, color, 1.5f);

        if (Node.Kind == NodeKind.Market)
        {
            DrawArc(center, 8f, 0f, Mathf.Tau, 20, color, 1.5f);
        }

        if (Node.IsMatch && Node.Difficulty > 0)
        {
            Style.DrawDifficultyIcon(this, center, 6f, Node.Difficulty, Style.DifficultyColor(Node.Difficulty));
        }
    }
}
