namespace Underleague.Sim.Tests;

/// <summary>
/// Tamaño de las puertas estadísticas (<c>Category=Gate</c>). Por defecto el 100 %: las puertas completas,
/// que son las que cuentan para cerrar un hito. Con <c>UNDERLEAGUE_GATE_SCALE=25</c> (o
/// <c>tools/puertas-rapidas.sh</c>) cada puerta juega la cuarta parte de su muestra: sirve para cazar
/// roturas grandes mientras se trabaja, no para decidir. Los umbrales no se tocan, así que con menos muestra
/// el ruido es mayor y una roja en modo rápido es una pista que se confirma con las completas (Regla J: el
/// instrumento rápido no sustituye al que ya está validado).
/// <para>Solo escala el número de plantillas o runs, nunca los partidos por plantilla: el emparejamiento de
/// cada puerta no cambia de forma. Leer el entorno aquí es legítimo: es el arnés, no <c>/Sim</c> (RT-012).</para>
/// </summary>
public static class GateScale
{
    /// <summary>Porcentaje de muestra en vigor (1-100).</summary>
    public static int Percent { get; } = Read();

    /// <summary>True si las puertas corren con menos muestra que la completa.</summary>
    public static bool IsQuick => Percent < 100;

    /// <summary>La muestra escalada, nunca por debajo de <paramref name="minimum"/>.</summary>
    public static int Of(int full, int minimum = 4) =>
        Math.Max(Math.Min(full, minimum), full * Percent / 100);

    private static int Read()
    {
        string? raw = Environment.GetEnvironmentVariable("UNDERLEAGUE_GATE_SCALE");
        return int.TryParse(raw, out int value) ? Math.Clamp(value, 1, 100) : 100;
    }
}
