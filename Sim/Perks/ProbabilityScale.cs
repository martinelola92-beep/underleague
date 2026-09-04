namespace Underleague.Sim.Perks;

/// <summary>
/// Escala de valores de <c>modifyProbability</c> <b>por canal</b> (ADR 0035). Un punto porcentual no vale
/// lo mismo en todos los canales porque las bases son de órdenes de magnitud distintos: sobre
/// <c>intercept</c> (base 250 de 10.000) un <c>+5</c> triplica la probabilidad y sobre <c>pass</c> (base
/// 7.700) la sube un 6,5%. Con una escala única el diseñador escribe la misma cifra en dos perks y pone
/// dos cosas que se diferencian en dos órdenes de magnitud.
///
/// <para>Por eso <b>cada canal declara su propio escalón</b> en <c>data/sim/tuning.json</c>
/// (<c>probabilityChannels.&lt;canal&gt;.step</c>, en puntos porcentuales) y un valor legal es ese escalón
/// multiplicado por <see cref="Multipliers"/>: <c>1, 2, 3, 5, 10</c> pasos. El escalón se fija de modo que
/// un paso valga aproximadamente lo mismo en todos los canales en impacto <b>relativo</b> sobre su base,
/// hasta donde el punto porcentual entero lo permite: en los canales de base diminuta (intercepción,
/// lesión, falta, tarjeta) el paso mínimo posible es 1, y ese es su escalón.</para>
///
/// <para>Lo que el jugador lee no cambia (<c>estilo-descripciones.md</c>): la descripción sigue diciendo
/// el valor absoluto ("+3% de probabilidad de interceptar"), que es verdad y es verificable. Lo que
/// cambia es lo que el <b>diseñador</b> puede escribir.</para>
/// </summary>
public sealed class ProbabilityScale
{
    private static readonly int KindCount = Enum.GetValues<ProbabilityKind>().Length;

    private readonly int[] _steps;

    /// <summary>Construye la escala desde los escalones por canal, indexados por <see cref="ProbabilityKind"/>.</summary>
    public ProbabilityScale(IReadOnlyList<int> stepsByKind)
    {
        ArgumentNullException.ThrowIfNull(stepsByKind);
        if (stepsByKind.Count != KindCount)
        {
            throw new ArgumentException(
                $"la escala necesita un escalón por canal ({KindCount}) y ha recibido {stepsByKind.Count}",
                nameof(stepsByKind));
        }

        _steps = new int[KindCount];
        for (int i = 0; i < KindCount; i++)
        {
            if (stepsByKind[i] < 1)
            {
                throw new ArgumentException(
                    $"el escalón del canal '{Name((ProbabilityKind)i)}' es {stepsByKind[i]} y debe ser al menos 1 punto porcentual",
                    nameof(stepsByKind));
            }

            _steps[i] = stepsByKind[i];
        }
    }

    /// <summary>Pasos admitidos: un valor legal es <c>escalón × uno de estos</c> (ADR 0035).</summary>
    public static IReadOnlyList<int> Multipliers { get; } = new[] { 1, 2, 3, 5, 10 };

    /// <summary>Escalón del canal, en puntos porcentuales.</summary>
    public int Step(ProbabilityKind kind) => _steps[(int)kind];

    /// <summary>True si <paramref name="points"/> (puntos porcentuales, con signo) es legal en ese canal.</summary>
    public bool IsLegal(ProbabilityKind kind, int points)
    {
        int magnitude = Math.Abs(points);
        int step = _steps[(int)kind];
        for (int i = 0; i < Multipliers.Count; i++)
        {
            if (magnitude == step * Multipliers[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Los cinco valores legales de un canal, para el mensaje de error del cargador.</summary>
    public string Allowed(ProbabilityKind kind)
    {
        int step = _steps[(int)kind];
        return string.Join('/', Multipliers.Select(m => (step * m).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Nombre del canal tal y como se escribe en <c>/data</c> (camelCase).</summary>
    public static string Name(ProbabilityKind kind)
    {
        string text = kind.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
