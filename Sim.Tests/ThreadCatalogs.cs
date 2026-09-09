using Underleague.Sim.Data;

namespace Underleague.Sim.Tests;

/// <summary>
/// Un <see cref="Catalog"/> por hilo, para que las puertas puedan jugar sus partidos en paralelo.
///
/// <para><b>Por qué hace falta.</b> <c>Sim.Perks.CompiledCondition</c> guarda el contexto de la
/// evaluación en curso en campos de la propia instancia y comparte una única <c>NCalc.Expression</c>;
/// su documentación lo dice y lo justifica, porque <c>/Sim</c> es síncrono y de un solo hilo. Las
/// condiciones se compilan una vez al cargar y viven <b>dentro</b> del <see cref="Catalog"/>, así que dos
/// hilos que evalúen la condición del <b>mismo</b> perk sobre el mismo catálogo se pisan el contexto.
/// No es teórico: con el catálogo compartido, dos ejecuciones seguidas de <see cref="Analysis.BuildGateTests"/>
/// daban tasas de activación distintas entre sí (medido: <c>activationRate_high_press_trigger</c> 96,875
/// contra 97,083 sobre 480 partidos, es decir un partido de diferencia), y con él se mueven también los
/// goles y las victorias.</para>
///
/// <para><b>La solución, dentro de <c>/Sim.Tests</c>.</b> Cada hilo carga su propio catálogo de los
/// <b>mismos</b> ficheros, así que tiene sus propias condiciones compiladas y no comparte contexto con
/// nadie. El dato es idéntico —el cargador es determinista y no consulta nada externo—, de modo que el
/// resultado no depende de qué hilo juegue qué partido: es bit a bit el del bucle secuencial. Los
/// <c>TeamSetup</c> se pueden seguir generando una sola vez y compartiendo entre hilos porque no
/// referencian objetos del catálogo: llevan los perks por <b>id</b> y el motor los resuelve contra el
/// catálogo que recibe <c>Simulator.Run</c> o <c>RunPolicy.Play</c>. Lo mismo vale para
/// <c>StandardRunSystems</c> y <c>BossCatalog</c>, que no llevan condiciones compiladas.</para>
///
/// <para>Es el mismo arreglo que <c>Balance/BalanceCatalogs.cs</c>, copiado y no compartido porque aquel
/// es <c>internal</c> de <c>/Balance</c>. El arreglo de raíz —hacer <c>Evaluate</c> reentrante— es un
/// cambio de <c>/Sim</c> que pide su propio ADR.</para>
/// </summary>
internal static class ThreadCatalogs
{
    /// <summary>Instantánea de <c>/data</c> leída una sola vez; a partir de ahí solo se lee (RT-061b).</summary>
    private static readonly Dictionary<string, string> Files = TestData.LoadAllFiles();

    private static readonly ThreadLocal<Catalog> PerThread = new(() => DataLoader.FromJson(Files));

    /// <summary>El catálogo de este hilo. Dentro de un <c>Parallel.For</c>, uno por hilo trabajador.</summary>
    public static Catalog Current => PerThread.Value!;
}
