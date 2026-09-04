namespace Underleague.Sim.Engine;

/// <summary>Vector 2D en coordenadas de casillas. Único lugar de /Sim donde se usa float libremente (posiciones, RT-023).</summary>
public readonly record struct Vec2(float X, float Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vec2 operator *(Vec2 a, float scalar) => new(a.X * scalar, a.Y * scalar);

    /// <summary>Longitud euclídea del vector.</summary>
    public float Length => MathF.Sqrt((X * X) + (Y * Y));

    /// <summary>Distancia euclídea entre dos puntos.</summary>
    public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

    /// <summary>Vector normalizado; Vec2(0,0) si la longitud es 0.</summary>
    public Vec2 Normalized
    {
        get
        {
            float length = Length;
            return length <= 0f ? new Vec2(0f, 0f) : new Vec2(X / length, Y / length);
        }
    }

    /// <summary>Interpolación lineal entre a y b, t en [0,1].</summary>
    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + ((b - a) * t);

    /// <summary>
    /// Texto del vector, en cultura invariante. <b>Tiene que estar escrito a mano</b>: el
    /// <c>PrintMembers</c> que genera el compilador para un <c>record struct</c> recorre todas las
    /// propiedades públicas, incluida <see cref="Normalized"/>, que es otro <see cref="Vec2"/> —así que
    /// el <c>ToString()</c> por defecto se llamaba a sí mismo hasta desbordar la pila. No era un
    /// problema teórico: cualquier aserción fallida de xUnit que formateara un Vec2 tumbaba el proceso
    /// entero y abortaba la ejecución de los tests (encontrado en el paquete U).
    /// </summary>
    public override string ToString() => FormattableString.Invariant($"Vec2 {{ X = {X}, Y = {Y} }}");
}
