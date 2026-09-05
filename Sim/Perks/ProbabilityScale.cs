namespace Underleague.Sim.Perks;

/// <summary>
/// Escala de <c>modifyProbability</c> (ADR 0050 P1). Un perk ya no <b>suma puntos porcentuales</b> a una
/// probabilidad: <b>multiplica su cuota</b>.
///
/// <code>
/// cuota  = p / (1 − p)
/// cuota' = cuota × k
/// p'     = cuota' / (1 + cuota')
/// </code>
///
/// <para>Multiplicar la <b>cuota</b>, y no la probabilidad, es lo que hace que un perk valga lo mismo
/// cerca de 0 que cerca de 1: la operación no puede sacar la probabilidad del intervalo (0,1) y no
/// depende de la base del canal. Con la fórmula aditiva anterior, la misma cifra escrita en dos perks
/// producía efectos que se diferenciaban en dos órdenes de magnitud —un <c>+5</c> multiplicaba por trece
/// la probabilidad de lesionar (base 0,4%) y no movía la de pase (base 77%)—, y eso obligó a la tabla de
/// escalones por canal de la <b>ADR 0035</b>, que esta escala <b>retira</b>: con cuotas, un perk vale lo
/// mismo en cualquier canal por construcción y ya no hace falta declarar nada por canal.</para>
///
/// <para><b>Valores permitidos</b>: <c>k ∈ {1,15 · 1,3 · 1,5 · 2}</c> y sus inversos. En <c>/data</c> se
/// escriben como el porcentaje con signo <c>±15, ±30, ±50, ±100</c>: el positivo es <c>k = 1 + n/100</c> y
/// el negativo es su <b>inverso exacto</b>, <c>k = 1 / (1 + |n|/100)</c>. Por eso <c>-100</c> no es "cero
/// probabilidad" sino "la mitad de cuota", que es lo que significa invertir un ×2.</para>
///
/// <para>Internamente todo es entero (RT-023): el multiplicador vive en base 10.000 igual que las
/// probabilidades, de modo que <see cref="Neutral"/> es <c>k = 1</c>.</para>
/// </summary>
public static class ProbabilityScale
{
    /// <summary>Multiplicador que no hace nada, <c>k = 1</c>. Es el valor "vacío" de la tabla de modificadores.</summary>
    public const int Neutral = 10000;

    /// <summary>Base de las probabilidades del motor: 10.000 puntos son el 100%.</summary>
    public const int Full = 10000;

    /// <summary>
    /// Cota del multiplicador acumulado. Existe por dos razones: que el producto de muchos perks no
    /// desborde la aritmética de <see cref="Apply"/> (con este tope el mayor producto intermedio es
    /// 10⁷ × 10⁴ × 10⁴ = 10¹⁵, dentro de <c>long</c>), y que un apilamiento absurdo no se convierta en un
    /// interruptor: multiplicar la cuota por mil ya deja cualquier probabilidad pegada a su techo.
    /// </summary>
    public const int MaxMultiplier = 10_000_000;

    /// <summary>Cota inferior simétrica de <see cref="MaxMultiplier"/>; nunca cero, para no perder la reversibilidad.</summary>
    public const int MinMultiplier = 1;

    /// <summary>
    /// Las cuatro magnitudes legales, en porcentaje de cuota. Un valor de <c>/data</c> es una de estas
    /// con signo: positivo multiplica la cuota, negativo la divide por el mismo factor.
    /// </summary>
    public static IReadOnlyList<int> Magnitudes { get; } = new[] { 15, 30, 50, 100 };

    /// <summary>Los ocho valores legales, para el mensaje de error del cargador.</summary>
    public static string Allowed { get; } = "-100/-50/-30/-15/15/30/50/100";

    /// <summary>True si <paramref name="percent"/> es uno de los ocho valores legales.</summary>
    public static bool IsLegal(int percent)
    {
        int magnitude = Math.Abs(percent);
        for (int i = 0; i < Magnitudes.Count; i++)
        {
            if (magnitude == Magnitudes[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Multiplicador de cuota (base 10.000) del porcentaje con signo de <c>/data</c>. El negativo es el
    /// <b>inverso</b> del positivo de la misma magnitud, no su reflejo: <c>-30</c> es <c>1/1,3</c>, no
    /// <c>0,7</c>. Redondeo al entero más cercano, determinista y sin coma flotante.
    /// </summary>
    public static int ToMultiplier(int percent)
    {
        if (percent == 0)
        {
            return Neutral;
        }

        if (percent > 0)
        {
            return (Neutral / 100) * (100 + percent);
        }

        int denominator = 100 - percent; // percent < 0, así que esto es 100 + |percent|
        return (int)((((long)Neutral * 100) + (denominator / 2)) / denominator);
    }

    /// <summary>
    /// Magnitud en porcentaje de cuota de un multiplicador, <b>sin signo</b>: es lo que la descripción
    /// escribe ("un 30% más", "un 23% menos"). Un ×1,3 da 30 y su inverso da 23, que es la cifra
    /// verdadera de la reducción y no la del aumento que la genera.
    /// </summary>
    public static int ToPercent(int multiplier) => multiplier >= Neutral
        ? (multiplier - Neutral + 50) / 100
        : (Neutral - multiplier + 50) / 100;

    /// <summary>True si el multiplicador sube la probabilidad del canal.</summary>
    public static bool IsIncrease(int multiplier) => multiplier > Neutral;

    /// <summary>
    /// Compone dos multiplicadores. Apilar dos perks es multiplicar sus cuotas, así que la composición es
    /// el producto: es lo que hace que el décimo perk de una línea siga valiendo algo en vez de chocar
    /// contra el techo del canal como ocurría con la suma.
    /// </summary>
    public static int Combine(int a, int b)
    {
        if (a == Neutral)
        {
            return b;
        }

        if (b == Neutral)
        {
            return a;
        }

        long product = (((long)a * b) + (Neutral / 2)) / Neutral;
        return (int)Math.Clamp(product, MinMultiplier, MaxMultiplier);
    }

    /// <summary>
    /// Inverso de un multiplicador. Lo usan los canales que <b>restan</b> a otro: la resistencia a las
    /// intercepciones divide la cuota de interceptar, y el canal de tiro a puerta divide la cuota de
    /// tirar fuera. Multiplicar la cuota de un suceso por k es exactamente dividir la de su contrario
    /// por k, así que el par sigue siendo consistente.
    /// </summary>
    public static int Invert(int multiplier)
    {
        if (multiplier == Neutral)
        {
            return Neutral;
        }

        long inverse = ((((long)Neutral * Neutral) + (multiplier / 2)) / multiplier);
        return (int)Math.Clamp(inverse, MinMultiplier, MaxMultiplier);
    }

    /// <summary>
    /// <paramref name="multiplier"/> elevado a <paramref name="times"/>, que es lo que significa un
    /// efecto <c>valuePerCounter</c>: cada unidad del contador vale exactamente lo mismo que una copia
    /// más del perk (ADR 0050 P1). Con <paramref name="times"/> = 0 devuelve <see cref="Neutral"/>.
    /// </summary>
    public static int Power(int multiplier, int times)
    {
        if (times <= 0 || multiplier == Neutral)
        {
            return Neutral;
        }

        int result = Neutral;
        for (int i = 0; i < times; i++)
        {
            int next = Combine(result, multiplier);
            if (next == result)
            {
                break; // saturado en la cota; seguir multiplicando no cambia nada
            }

            result = next;
        }

        return result;
    }

    /// <summary>
    /// Aplica el multiplicador a una probabilidad en base 10.000:
    /// <c>p' = k·p / (k·p + (1 − p))</c>, que es <c>cuota × k</c> escrito sin división.
    /// <para>
    /// El 0 y el 100% son puntos fijos de la operación y se devuelven tal cual: lo imposible sigue siendo
    /// imposible y lo seguro sigue siendo seguro por muchos perks que se apilen. Un valor fuera de
    /// (0, 10.000) —que el motor produce antes de acotar— también se devuelve intacto, para que la cota
    /// del canal siga siendo quien decide y no esta función.
    /// </para>
    /// </summary>
    public static int Apply(int probability, int multiplier)
    {
        if (multiplier == Neutral || probability <= 0 || probability >= Full)
        {
            return probability;
        }

        long numerator = (long)multiplier * probability;
        long denominator = numerator + ((long)Neutral * (Full - probability));
        return (int)(((numerator * Full) + (denominator / 2)) / denominator);
    }

    /// <summary>
    /// Aplica el multiplicador en un canal que se resuelve con el <b>promedio de dos tiradas</b>
    /// (ADR 0050 P2: regate, entrada, tiro a puerta y parada). Ahí el número que el motor calcula
    /// <b>no</b> es la probabilidad con la que ocurre el suceso: el promedio de dos uniformes tiene la
    /// acumulada triangular <c>F(p) = 2p²</c> por debajo del centro y <c>1 − 2(1−p)²</c> por encima, y
    /// eso es lo que se realiza. Multiplicar la cuota del parámetro en vez de la del suceso valdría vez
    /// y media o el doble según el canal —una entrada con ×2 sobre el parámetro sale a ×3,6 sobre la
    /// entrada de verdad—, que es exactamente el defecto que la P1 viene a quitar.
    /// <para>
    /// Así que se pasa a la probabilidad realizada, se multiplica ahí, y se vuelve con la inversa. La
    /// raíz cuadrada es entera y redondeada (RT-023): sin coma flotante y con el mismo resultado en
    /// cualquier máquina (RT-024).
    /// </para>
    /// </summary>
    public static int ApplyAveraged(int probability, int multiplier)
    {
        if (multiplier == Neutral || probability <= 0 || probability >= Full)
        {
            return probability;
        }

        return InverseTriangular(Apply(Triangular(probability), multiplier));
    }

    /// <summary>Acumulada del promedio de dos uniformes, en base 10.000.</summary>
    internal static int Triangular(int probability)
    {
        if (probability <= Full / 2)
        {
            return (int)((2L * probability * probability) / Full);
        }

        long rest = Full - probability;
        return (int)(Full - ((2L * rest * rest) / Full));
    }

    /// <summary>Inversa de <see cref="Triangular"/>, en base 10.000.</summary>
    internal static int InverseTriangular(int realized)
    {
        if (realized <= Full / 2)
        {
            return Sqrt((long)realized * Full / 2);
        }

        return Full - Sqrt((long)(Full - realized) * Full / 2);
    }

    /// <summary>Raíz cuadrada entera redondeada al más cercano, sin coma flotante (RT-023, RT-024).</summary>
    private static int Sqrt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        long root = 1;
        while (root * root < value)
        {
            root <<= 1;
        }

        long low = root >> 1;
        long high = root;
        while (low < high)
        {
            long mid = low + ((high - low) / 2);
            if (mid * mid < value)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        // Redondeo al entero más cercano: se compara contra el punto medio de los dos cuadrados vecinos.
        if (low > 0 && ((low - 1) * (low - 1)) + (low * low) >= 2 * value)
        {
            low--;
        }

        return (int)low;
    }

    /// <summary>Nombre del canal tal y como se escribe en <c>/data</c> (camelCase).</summary>
    public static string Name(ProbabilityKind kind)
    {
        string text = kind.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
