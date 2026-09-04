using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Zona de acción con forma (ADR 0028, fase1b-diseno.md §2.2). Sustituye al radio circular duro de la
/// correa: la zona es un rectángulo <b>en el marco local del jugador</b>, relativo a su casilla-hogar
/// efectiva y orientado según el sentido de ataque de su equipo, con extensiones distintas hacia
/// delante, hacia atrás y a los lados.
///
/// <para>Marco local: <c>u = (punto.X − hogar.X) × direcciónDeAtaque</c> (positivo hacia la portería
/// rival) y <c>v = punto.Y − hogar.Y</c>. La zona es <c>u ∈ [−atrás, adelante]</c>, <c>|v| ≤ lados</c>.
/// Con el marco local, el mismo dato de forma describe la misma geometría para los dos equipos: el
/// visitante refleja columnas sin necesidad de datos propios.</para>
///
/// <para>Una extensión <see cref="Unlimited"/> (el <c>-1</c> de <c>tuning.actionZone.shape</c>) significa
/// "sin límite en esa dirección": el borde del campo es el único tope, y de eso ya se encarga
/// <see cref="Utility.ClampToPitch"/>.</para>
///
/// <para>Todo el cálculo de extensiones es entero (milicasillas, RT-023); solo la conversión final a
/// coordenadas usa <c>float</c>, igual que el resto de posiciones.</para>
/// </summary>
internal readonly struct ActionZone
{
    /// <summary>Valor de extensión que representa "sin límite en esa dirección" (el -1 del dato).</summary>
    public const int Unlimited = -1;

    private readonly int _forwardMilli;
    private readonly int _backMilli;
    private readonly int _sidesMilli;

    /// <summary>Construye la zona con las tres extensiones en milicasillas; <see cref="Unlimited"/> se propaga.</summary>
    public ActionZone(int forwardMilli, int backMilli, int sidesMilli)
    {
        _forwardMilli = forwardMilli;
        _backMilli = backMilli;
        _sidesMilli = sidesMilli;
    }

    /// <summary>Extensión hacia la portería rival, en milicasillas; <see cref="Unlimited"/> si no hay tope.</summary>
    public int ForwardMilli => _forwardMilli;

    /// <summary>Extensión hacia la portería propia, en milicasillas; <see cref="Unlimited"/> si no hay tope.</summary>
    public int BackMilli => _backMilli;

    /// <summary>Extensión lateral (a cada lado), en milicasillas; <see cref="Unlimited"/> si no hay tope.</summary>
    public int SidesMilli => _sidesMilli;

    /// <summary>Esta misma zona multiplicada por un porcentaje entero (el límite duro exterior, §2.2).</summary>
    public ActionZone Scaled(int percent) => new(
        Scale(_forwardMilli, percent),
        Scale(_backMilli, percent),
        Scale(_sidesMilli, percent));

    /// <summary>
    /// Distancia en casillas desde <paramref name="point"/> hasta la zona: 0 si está dentro, y si no la
    /// longitud del vector de exceso (distancia al rectángulo, la generalización natural del radio que
    /// había antes). Las direcciones sin límite no aportan exceso.
    /// </summary>
    public float DistanceOutside(Vec2 point, Vec2 home, int direction)
    {
        Local(point, home, direction, out float along, out float lateral);
        float excessAlong = 0f;
        if (_forwardMilli != Unlimited && along > Cells(_forwardMilli))
        {
            excessAlong = along - Cells(_forwardMilli);
        }
        else if (_backMilli != Unlimited && along < -Cells(_backMilli))
        {
            excessAlong = -Cells(_backMilli) - along;
        }

        float excessLateral = 0f;
        if (_sidesMilli != Unlimited)
        {
            float sides = Cells(_sidesMilli);
            if (lateral > sides)
            {
                excessLateral = lateral - sides;
            }
            else if (lateral < -sides)
            {
                excessLateral = -sides - lateral;
            }
        }

        if (excessAlong <= 0f)
        {
            return excessLateral;
        }

        return excessLateral <= 0f
            ? excessAlong
            : new Vec2(excessAlong, excessLateral).Length;
    }

    /// <summary>
    /// Acota <paramref name="point"/> al rectángulo de la zona, en el marco local del jugador. Si el
    /// punto ya está dentro se devuelve <b>tal cual</b>, sin pasar por el marco local: la ida y vuelta
    /// introduce un error de redondeo de flotante que, aplicado en cada tick a cada jugador, sería una
    /// deriva gratuita (y haría imposible comparar el punto acotado con el original).
    /// </summary>
    public Vec2 Clamp(Vec2 point, Vec2 home, int direction)
    {
        Local(point, home, direction, out float along, out float lateral);
        bool changed = false;

        if (_forwardMilli != Unlimited && along > Cells(_forwardMilli))
        {
            along = Cells(_forwardMilli);
            changed = true;
        }
        else if (_backMilli != Unlimited && along < -Cells(_backMilli))
        {
            along = -Cells(_backMilli);
            changed = true;
        }

        if (_sidesMilli != Unlimited)
        {
            float sides = Cells(_sidesMilli);
            if (lateral > sides)
            {
                lateral = sides;
                changed = true;
            }
            else if (lateral < -sides)
            {
                lateral = -sides;
                changed = true;
            }
        }

        return changed ? new Vec2(home.X + (along * direction), home.Y + lateral) : point;
    }

    /// <summary>
    /// Punto del segmento <paramref name="from"/> → <paramref name="to"/> por el que el segmento
    /// <b>entra</b> en la zona (el más cercano a <paramref name="from"/>), o null si el segmento no la
    /// corta. Recorte por franjas (algoritmo de slabs) sobre el marco local, sin trigonometría.
    /// Sustituye al corte circular <c>SegmentPointAtLeash</c> de la fase 0.
    /// </summary>
    public Vec2? SegmentEntry(Vec2 from, Vec2 to, Vec2 home, int direction)
    {
        Local(from, home, direction, out float u0, out float v0);
        Local(to, home, direction, out float u1, out float v1);
        float du = u1 - u0;
        float dv = v1 - v0;

        float tMin = 0f;
        float tMax = 1f;

        if (_forwardMilli != Unlimited || _backMilli != Unlimited)
        {
            float low = _backMilli == Unlimited ? float.NegativeInfinity : -Cells(_backMilli);
            float high = _forwardMilli == Unlimited ? float.PositiveInfinity : Cells(_forwardMilli);
            if (!ClipSlab(u0, du, low, high, ref tMin, ref tMax))
            {
                return null;
            }
        }

        if (_sidesMilli != Unlimited)
        {
            float sides = Cells(_sidesMilli);
            if (!ClipSlab(v0, dv, -sides, sides, ref tMin, ref tMax))
            {
                return null;
            }
        }

        return from + ((to - from) * tMin);
    }

    /// <summary>Convierte milicasillas a casillas; <see cref="Unlimited"/> no llega nunca aquí.</summary>
    public static float Cells(int milli) => milli / 1000f;

    /// <summary>Multiplica una extensión por un porcentaje entero, propagando <see cref="Unlimited"/>.</summary>
    private static int Scale(int milli, int percent) => milli == Unlimited ? Unlimited : milli * percent / 100;

    /// <summary>Coordenadas locales de un punto: avance hacia la portería rival y desvío lateral.</summary>
    private static void Local(Vec2 point, Vec2 home, int direction, out float along, out float lateral)
    {
        along = (point.X - home.X) * direction;
        lateral = point.Y - home.Y;
    }

    /// <summary>Recorte de un parámetro de segmento contra una franja [low, high]; false si no queda nada.</summary>
    private static bool ClipSlab(float origin, float delta, float low, float high, ref float tMin, ref float tMax)
    {
        if (delta > -1e-6f && delta < 1e-6f)
        {
            return origin >= low && origin <= high;
        }

        float t0 = (low - origin) / delta;
        float t1 = (high - origin) / delta;
        if (t0 > t1)
        {
            (t0, t1) = (t1, t0);
        }

        if (t0 > tMin)
        {
            tMin = t0;
        }

        if (t1 < tMax)
        {
            tMax = t1;
        }

        return tMin <= tMax;
    }
}
