using Godot;
using Underleague.Sim.Run;

namespace Underleague.Game.Ui;

/// <summary>
/// Un solo glifo de <see cref="NodeKind"/>: el anillo de color de <see cref="Style.Of"/> más el icono de
/// <see cref="NodeIcon"/> encima, siempre en su aspecto "disponible" (relleno claro, anillo brillante).
/// Es la ficha que usa <see cref="MapLegend"/> para cada fila —el mismo glifo que el jugador ve en el
/// mapa y en la lista de destinos, para que la leyenda enseñe literalmente lo que hay que reconocer, no
/// una versión aparte.
/// </summary>
public partial class KindGlyph : Control
{
    public NodeKind Kind { get; set; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw()
    {
        var center = Size / 2f;
        float radius = Mathf.Min(Size.X, Size.Y) / 2f - 2f;
        var color = Style.Of(Kind);
        DrawCircle(center, radius, new Color(color, 0.30f));
        DrawArc(center, radius, 0f, Mathf.Tau, 24, color, 1.5f, true);
        NodeIcon.Draw(this, Kind, center, radius * 0.78f, Style.Text, 1.4f);
    }
}
