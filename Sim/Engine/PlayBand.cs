using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// La banda jugable de la fase (ADR 0175, RF-055b): el campo entero en tiempo reglamentario y, en la turba, sin las
/// <see cref="Inset"/> filas exteriores por lado que invade el público. <b>Es el único sitio que sabe acotar «a la
/// banda»</b>: el movimiento, los destinos de la utilidad y de los pases, los saques, los empujones, el balón
/// aparcado y el suplente que entra pasan por aquí, y con <see cref="Inset"/> 0 todo devuelve su argumento sin una
/// operación más (el reglamentario es byte a byte el de antes, RT-024). <c>Pitch.Rows</c> sigue siendo constante.
/// </summary>
/// <param name="Inset">Filas invadidas por lado. El esquema de datos lo limita a 1: con más, la banda mordería el
/// área (filas 1,5-5,5) y habría que rediseñar al portero.</param>
internal readonly record struct PlayBand(int Inset)
{
    /// <summary>La banda entera: tiempo reglamentario.</summary>
    public static PlayBand Full => new(0);

    /// <summary>Borde superior de la banda.</summary>
    public float Min => Inset;

    /// <summary>Borde inferior de la banda.</summary>
    public float Max => Pitch.Rows - Inset;

    /// <summary>Acota un punto (sólo la fila) a la banda.</summary>
    public Vec2 Clamp(Vec2 point) =>
        Inset == 0 ? point : new Vec2(point.X, Math.Clamp(point.Y, Min, Max));

    /// <summary>
    /// Acota el paso de un jugador <b>sin teletransportarlo</b> (RF-053, ADR 0143): quien está dentro no sale y quien
    /// está en una fila invadida no puede alejarse más de ella, sólo andar hacia la banda.
    /// </summary>
    public Vec2 ClampStep(Vec2 next, Vec2 current) =>
        Inset == 0
            ? next
            : new Vec2(next.X, Math.Clamp(next.Y, MathF.Min(Min, current.Y), MathF.Max(Max, current.Y)));

    /// <summary>¿Está la fila <paramref name="y"/> dentro de la banda?</summary>
    public bool Contains(float y) => y >= Min && y <= Max;
}
