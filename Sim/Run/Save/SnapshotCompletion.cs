using System.Collections.ObjectModel;

namespace Underleague.Sim.Run.Save;

/// <summary>
/// Completa la instantánea de <c>/data</c> de un guardado <b>antiguo</b> con los ficheros que el juego
/// actual exige y aquél no traía (RT-061, RT-061b, ADR 0163).
///
/// <para><b>El problema.</b> La run se retoma con la instantánea que congeló al empezar (RT-061b), pero
/// <c>StandardRunSystems.FromJson</c> exige un fichero por cada catálogo nuevo. Cada vez que el juego
/// añade uno (apodos, Gaceta, prótesis), un guardado de la versión anterior deja de poder retomarse aunque
/// su esquema (<see cref="RunSave.SchemaVersion"/>) sea el mismo: la run ironman se perdería por
/// actualizar el juego, que es lo que RT-061b existe para impedir.</para>
///
/// <para><b>La regla.</b> Sólo se completan los ficheros de <see cref="AddableFiles"/>, y sólo si el
/// guardado no los trae. Un fichero que el guardado ya trae <b>no se toca nunca</b>, y todo lo que no está
/// en la lista sigue siendo el de la run: cambiar reglas de una run en curso (perks, economía, mapa,
/// rivales, pesos de IA...) es justo lo que la instantánea prohíbe. Es una <b>lista explícita</b> y no una
/// regla «lo que falte» a propósito: un fichero nuevo de reglas debe dejar el guardado sin poder cargarse
/// (error explícito, RT-032) hasta que alguien decida que es añadible y lo escriba aquí, con su motivo.</para>
///
/// <para><b>Criterio para entrar en la lista</b>: el catálogo es aditivo —añade algo que la run antigua
/// nunca tuvo— y no reescribe ningún estado guardado ni desplaza ningún flujo de RNG de la run (RT-022).
/// No hace falta subir la versión del esquema: el guardado no cambia de forma.</para>
/// </summary>
public static class SnapshotCompletion
{
    /// <summary>
    /// Ficheros añadibles y por qué (ADR 0163, «Guardados anteriores»):
    /// <list type="bullet">
    /// <item><c>nicknames/nicknames.json</c> — apodos por umbral sobre la carrera que la run ya lleva
    /// (RF-122): se derivan al leer, no se guardan ni cambian ningún partido.</item>
    /// <item><c>gazette/gazette.json</c> — plantillas de la portada de fin de run: sólo presentación.</item>
    /// <item><c>prostheses/prostheses.json</c> — el catálogo de prótesis (RF-095): la run antigua no llevaba
    /// ninguna instalada, y una prótesis sólo entra en juego cuando el jugador la elige en la clínica; no
    /// toca ningún jugador ya guardado ni el flujo de RNG de los partidos.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> AddableFiles { get; } = new ReadOnlyCollection<string>(new[]
    {
        "gazette/gazette.json",
        "nicknames/nicknames.json",
        "prostheses/prostheses.json",
    });

    /// <summary>
    /// La instantánea del guardado más los ficheros añadibles que le faltan, tomados de
    /// <paramref name="current"/> (el <c>/data</c> del juego actual). Devuelve una copia ordenada por ruta
    /// ordinal; <paramref name="snapshot"/> no se modifica. Un fichero añadible que el juego actual
    /// tampoco tiene se deja sin añadir: el cargador que lo exija lo dirá con su error explícito.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Complete(
        IReadOnlyDictionary<string, string> snapshot, IReadOnlyDictionary<string, string> current)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(current);

        var completed = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, content) in snapshot)
        {
            completed[path] = content;
        }

        for (int i = 0; i < AddableFiles.Count; i++)
        {
            string path = AddableFiles[i];
            if (!completed.ContainsKey(path) && current.TryGetValue(path, out string? content))
            {
                completed[path] = content;
            }
        }

        return completed;
    }

    /// <summary>
    /// El estado con la instantánea completada (<see cref="Complete"/>): al guardarse de nuevo la run ya
    /// lleva los ficheros y deja de necesitar la compleción. Nada más del estado cambia.
    /// </summary>
    public static RunState Complete(RunState state, IReadOnlyDictionary<string, string> current)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.WithDataSnapshot(Complete(state.DataSnapshot, current));
    }
}
