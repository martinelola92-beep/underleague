namespace Underleague.Sim.Tests;

/// <summary>
/// Colección a la que pertenecen las seis clases <c>Category=Gate</c>. xUnit paraleliza <b>entre</b>
/// clases por defecto, y desde que cada puerta paraleliza además sus propios partidos con
/// <c>Parallel.For</c>, dejarlas sueltas significaría cuatro clases lanzando cuatro hilos cada una sobre
/// una máquina de cuatro núcleos: ni va más rápido —los núcleos ya estaban saturados— y multiplica por
/// cuatro el pico de memoria, que en este contenedor ya ha terminado en OOM.
///
/// <para>Con la colección, las puertas van <b>en serie entre sí</b> y cada una satura los núcleos por su
/// cuenta. El resto de la suite (<c>Category!=Gate</c>) conserva su paralelismo entre clases: la
/// colección no se marca <c>DisableParallelization</c> a propósito, porque no hay razón para que las
/// puertas bloqueen a los tests baratos.</para>
///
/// <para>Se hace con una colección y no con un <c>xunit.runner.json</c> porque el fichero de
/// configuración es global —apagaría el paralelismo de las 643 pruebas rápidas— y esto solo tiene que
/// valer para las seis puertas.</para>
/// </summary>
[CollectionDefinition("Gate")]
public sealed class GateCollection
{
}
