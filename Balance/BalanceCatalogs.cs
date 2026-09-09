using Underleague.Sim.Data;

namespace Underleague.Balance;

/// <summary>
/// Un <see cref="Catalog"/> por hilo, para que <c>/Balance</c> pueda jugar sus lotes en paralelo.
///
/// <para><b>Por qué hace falta.</b> <c>Sim.Perks.CompiledCondition</c> guarda el contexto de la
/// evaluación en curso en campos de la propia instancia y comparte una única <c>NCalc.Expression</c>
/// —su documentación lo dice y lo justifica: <c>/Sim</c> es síncrono y de un solo hilo—. Las condiciones
/// se compilan una vez al cargar y viven dentro del <see cref="Catalog"/>, así que dos hilos que evalúen
/// la condición del <b>mismo</b> perk sobre el mismo catálogo se pisan el contexto. Medido: con el
/// catálogo compartido, dos ejecuciones seguidas de <c>--boss-gate</c>, <c>--full-runs</c> o
/// <c>--builds</c> dan resultados distintos entre sí y distintos del secuencial (se mueven las
/// activaciones de perk y, con ellas, goles y victorias).</para>
///
/// <para><b>La solución, dentro de <c>/Balance</c>.</b> Cada hilo carga su propio catálogo de los
/// <b>mismos</b> ficheros, así que tiene sus propias condiciones compiladas y no comparte contexto con
/// nadie. El dato es idéntico —el cargador es determinista y no consulta nada externo—, de modo que el
/// resultado no depende de qué hilo juegue qué partido: es bit a bit el del bucle secuencial. Los
/// <c>TeamSetup</c> se pueden seguir generando una sola vez y compartiendo entre hilos porque no
/// referencian objetos del catálogo: llevan los perks por <b>id</b> (<c>PlayerDefinition.Perks</c>) y el
/// motor los resuelve contra el catálogo que recibe en <c>Simulator.Run</c>.</para>
///
/// <para>No hay estado mutable compartido aquí: <see cref="Init"/> se llama una vez al arrancar, antes
/// de lanzar ningún lote, y a partir de ahí solo se lee.</para>
/// </summary>
internal static class BalanceCatalogs
{
    private static IReadOnlyDictionary<string, string>? files;

    private static ThreadLocal<Catalog>? perThread;

    /// <summary>Fija los ficheros de <c>/data</c> desde los que cada hilo cargará su catálogo.</summary>
    public static void Init(IReadOnlyDictionary<string, string> dataFiles)
    {
        ArgumentNullException.ThrowIfNull(dataFiles);
        files = dataFiles;
        perThread = new ThreadLocal<Catalog>(() => DataLoader.FromJson(files!));
    }

    /// <summary>
    /// El catálogo de este hilo, o <paramref name="fallback"/> si nadie llamó a <see cref="Init"/>
    /// (tests y usos fuera del ejecutable de consola, siempre de un solo hilo).
    /// </summary>
    public static Catalog Current(Catalog fallback) => perThread is null ? fallback : perThread.Value!;
}
