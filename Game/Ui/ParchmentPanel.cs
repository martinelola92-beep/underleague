using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// Panel de fondo con textura de pergamino: relleno, sombra dura y borde rasgado
/// (<see cref="Underleague.Game.Ui.Broadcast.Pregon.DrawParchment"/>). Es el "marco" de
/// <c>docs/ui/README.md</c> §2 principio 7 (material en el marco, plano en el contenido):
/// <see cref="Widgets.Panel"/> lo usa para las zonas de todas las pantallas viejas, salvo las que son un
/// simple divisor de una línea o una cortina modal (<c>parchment: false</c> en <see cref="Widgets.Panel"/>),
/// que no son papel y no tienen sentido con un borde rasgado.
/// </summary>
public partial class ParchmentPanel : Control
{
    private Color _fill = Style.Panel;
    private Color _edge = Style.Line;
    private int _seed;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>
    /// Fija el color de relleno, el color de borde y la semilla del rasgado antes del primer dibujo.
    /// <paramref name="seed"/> es <b>función de la posición</b> del panel (la calcula
    /// <see cref="Widgets.Panel"/>), nunca de <c>System.Random</c>: dos ejecuciones pintan el mismo
    /// panel igual, y una captura de referencia sigue siendo comparable.
    /// </summary>
    public void Configure(Color fill, Color edge, int seed)
    {
        _fill = fill;
        _edge = edge;
        _seed = seed;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        Underleague.Game.Ui.Broadcast.Pregon.DrawParchment(this, Vector2.Zero, Size.X, Size.Y, _fill, _edge, _seed);
    }
}
