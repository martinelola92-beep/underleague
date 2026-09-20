using Godot;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// El distintivo de un nodo en una lista: el anillo de color de su tipo con el <see cref="NodeIcon"/> de
/// su tipo encima y, si es un partido, la <b>silueta de su dificultad</b> a la esquina (RF-012). Color y
/// forma juntos, como pide UI-002, en dos capas: el tipo de nodo en el icono central, el nivel de
/// dificultad en el distintivo pequeño.
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

        // El icono del tipo (UI-002: color y forma), no un círculo liso — mismo glifo que MapView y
        // MapLegend (encargo mapa-pregon).
        NodeIcon.Draw(this, Node.Kind, center, 9.5f, Style.Text);

        if (Node.IsMatch && Node.Difficulty > 0)
        {
            // A la esquina, no al centro: el centro lo ocupa ahora el icono del tipo.
            Style.DrawDifficultyIcon(this, center + new Vector2(8f, 8f), 4.2f, Node.Difficulty, Style.DifficultyColor(Node.Difficulty));
        }
    }
}
