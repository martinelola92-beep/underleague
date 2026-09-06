using Underleague.Sim.Model;

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
/// <para><b>Valores permitidos</b>: <c>k ∈ {1,15 · 1,3 · 1,5 · 2 · 3 · 4 · 6}</c> y sus inversos. En
/// <c>/data</c> se escriben como el porcentaje con signo <c>±15, ±30, ±50, ±100, ±200, ±300, ±500</c>: el
/// positivo es <c>k = 1 + n/100</c> y el negativo es su <b>inverso exacto</b>,
/// <c>k = 1 / (1 + |n|/100)</c>. Por eso <c>-100</c> no es "cero probabilidad" sino "la mitad de cuota",
/// que es lo que significa invertir un ×2.</para>
///
/// <para><b>El techo depende de la rareza</b> (ADR 0058). El <c>k ≤ 2</c> plano con el que se aplicó la
/// P1 se fijó a ojo antes de saber que el catálogo real valía de ×2 a ×2 987, y la medición lo falsificó:
/// con un techo único la capa de perks quedó más débil que antes y el hueco entre una build buena y una
/// mediocre se estrechó de 9,8 a 6,8 puntos. La rareza pasa a <b>comprar cuota</b> —un común mueve poco,
/// un legendario mueve mucho— porque es lo que se paga en el mercado y lo que sueltan los jefes, así que
/// es donde vive la decisión del jugador. Cada rareza añade un escalón de la escala:
/// <c>común ×2 · poco común ×3 · raro ×4 · legendario ×6</c> (ver <see cref="CeilingFor"/>).</para>
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
    /// Las siete magnitudes legales, en porcentaje de cuota. Un valor de <c>/data</c> es una de estas
    /// con signo: positivo multiplica la cuota, negativo la divide por el mismo factor. Siguen siendo un
    /// conjunto pequeño y cerrado con inverso exacto —la propiedad que la P1 introdujo y que la ADR 0058
    /// no toca—; lo que cambia es que las tres últimas solo están al alcance de las rarezas altas.
    /// </summary>
    public static IReadOnlyList<int> Magnitudes { get; } = new[] { 15, 30, 50, 100, 200, 300, 500 };

    /// <summary>
    /// Techo de cada rareza, en porcentaje de cuota (ADR 0058). Cada rareza añade un escalón: un común
    /// llega a ×2, un poco común a ×3, un raro a ×4 y un legendario a ×6. Un valor por encima del techo
    /// de su rareza es un error de datos, no un aviso: es lo que hace que la rareza signifique algo
    /// medible y no solo un color de marco.
    /// </summary>
    public static int CeilingFor(Rarity rarity) => rarity switch
    {
        Rarity.Common => 100,
        Rarity.Uncommon => 200,
        Rarity.Rare => 300,
        _ => 500,
    };

    /// <summary>
    /// Techo de un efecto <b>con contador</b> de esa rareza: un escalón por debajo del de
    /// <see cref="CeilingFor"/>. El motivo es que ahí el multiplicador no se aplica una vez sino hasta
    /// <c>n</c> (ADR 0050 P1, el tope se escribe en copias y el total es <c>k^n</c>), así que el techo de
    /// la rareza acota lo que vale <b>una copia</b> y no lo que vale la línea entera: con el mismo techo
    /// que un efecto suelto, cinco copias de un raro clavarían su canal en el 98% y volvería justo la
    /// patología que la P1 vino a quitar.
    /// </summary>
    public static int CounterCeilingFor(Rarity rarity) => rarity switch
    {
        Rarity.Common => 50,
        Rarity.Uncommon => 100,
        Rarity.Rare => 200,
        _ => 300,
    };

    /// <summary>Los catorce valores legales, para el mensaje de error del cargador.</summary>
    public static string Allowed { get; } = "-500/-300/-200/-100/-50/-30/-15/15/30/50/100/200/300/500";

    /// <summary>Los valores legales que no pasan de <paramref name="ceiling"/>, para el mensaje de error.</summary>
    public static string AllowedUpTo(int ceiling)
    {
        var text = new System.Text.StringBuilder();
        for (int i = Magnitudes.Count - 1; i >= 0; i--)
        {
            if (Magnitudes[i] <= ceiling)
            {
                text.Append('-').Append(Magnitudes[i]).Append('/');
            }
        }

        for (int i = 0; i < Magnitudes.Count; i++)
        {
            if (Magnitudes[i] <= ceiling)
            {
                text.Append(Magnitudes[i]).Append('/');
            }
        }

        return text.ToString(0, text.Length - 1);
    }

    /// <summary>True si <paramref name="percent"/> es uno de los catorce valores legales.</summary>
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

    /// <summary>True si <paramref name="percent"/> es legal y además cabe bajo <paramref name="ceiling"/>.</summary>
    public static bool IsLegalUpTo(int percent, int ceiling) =>
        IsLegal(percent) && Math.Abs(percent) <= ceiling;

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

    /// <summary>True si el multiplicador sube la probabilidad del canal.</summary>
    public static bool IsIncrease(int multiplier) => multiplier > Neutral;

    /// <summary>
    /// Magnitud en porcentaje de cuota de un multiplicador <b>legal</b>, con independencia de su
    /// dirección: tanto <c>×3</c> como <c>1/3</c> devuelven 200. Es lo que la descripción necesita desde
    /// la ADR 0058, que habla de <b>cuota</b> y no de proporción de probabilidad: "multiplica por 3" y
    /// "divide por 3" son la misma cifra con dos verbos, y las dos son exactas.
    /// </summary>
    public static int Magnitude(int multiplier)
    {
        if (multiplier <= 0)
        {
            // Un efecto sin multiplicador (por ejemplo el 'value' vacío de un efecto con contador, que
            // lleva el suyo en 'valuePerCounter'): no hay magnitud que escribir y nadie la pide.
            return 0;
        }

        int upward = multiplier >= Neutral ? multiplier : Invert(multiplier);
        return (upward - Neutral + 50) / 100;
    }

    /// <summary>
    /// El factor <c>k</c> de un multiplicador legal escrito como texto: "1,15", "1,3", "1,5", "2", "3",
    /// "4" o "6" —con coma o con punto según el idioma—. Sin coma flotante y sin depender de la cultura
    /// del proceso (RT-023, RT-024): la parte entera y la decimal salen de la magnitud entera.
    /// </summary>
    /// <param name="decimalSeparator">',' en español, '.' en inglés.</param>
    public static string FactorText(int multiplier, char decimalSeparator)
    {
        int factor = 100 + Magnitude(multiplier);
        int whole = factor / 100;
        int hundredths = factor % 100;
        if (hundredths == 0)
        {
            return whole.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var text = new System.Text.StringBuilder();
        text.Append(whole.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(decimalSeparator);
        text.Append((hundredths / 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (hundredths % 10 != 0)
        {
            text.Append((hundredths % 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

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
        if (multiplier == Neutral || multiplier <= 0)
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
