using Underleague.Sim.Run.View;

namespace Underleague.Sim.Run;

/// <summary>
/// Un partido que se estaba viendo cuando el jugador guardó o salió (RT-061, ADR 0183). Acompaña a un
/// <see cref="RunState"/> que es el de <b>antes</b> del partido: con ese estado, la semilla del nodo
/// (<c>RngStreams.MatchSeed(seed, nodeId)</c>) y estas decisiones, <see cref="RunEngine.EnterMatch"/>
/// reproduce el mismo partido hasta el mismo final (RT-013, RT-024). No es parte del estado de la run:
/// existe sólo entre que se empieza a ver un partido y se llega a su informe.
/// </summary>
/// <param name="NodeId">Nodo de partido que se estaba viendo.</param>
/// <param name="Decisions">Lo que el jugador había decidido dentro del partido hasta salir (ADR 0094, 0134, 0154).</param>
/// <param name="WatchedTick">
/// Tick más lejano que el jugador llegó a ver. Al reanudar, lo que ya vio no se puede volver a decidir:
/// una activación manual o un cambio de orden sólo valen desde este tick, porque decidir con el futuro ya
/// visto sería volver a tirar el partido (anti-abuso de RT-061).
/// </param>
public sealed record PendingMatch(int NodeId, MatchDecisions Decisions, int WatchedTick)
{
    /// <summary>
    /// Si una decisión en <paramref name="decisionTick"/> sigue permitida cuando el jugador ya vio hasta
    /// <paramref name="watchedTick"/>: sólo desde el tick siguiente al último visto. Es la única definición de la
    /// regla; <c>/Game</c> la usa para los controles en vivo, las respuestas a un punto de sustitución y la
    /// resolución automática de los puntos que quedan por debajo (ADR 0183).
    /// </summary>
    public static bool CanDecideAt(int watchedTick, int decisionTick) => decisionTick > watchedTick;

    /// <summary>
    /// El guardado que se escribe <b>antes de enseñar</b> el partido (ADR 0183): el peor caso, con
    /// <see cref="WatchedTick"/> igual al último tick del partido. Si el proceso muere sin un guardado limpio
    /// (cierre forzado, caída), la repetición sale con todo lo decidible ya bloqueado y el resultado no cambia;
    /// un guardado limpio lo baja después al tick que el jugador vio de verdad. Sin traza, <c>int.MaxValue</c>.
    /// </summary>
    public static PendingMatch BeforeShowing(int nodeId, MatchDecisions decisions, MatchPlayback playback)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(playback);
        int end = playback.Trace is { FrameCount: > 0 } trace ? trace.TickAt(trace.FrameCount - 1) : int.MaxValue;
        return new PendingMatch(nodeId, decisions, end);
    }
}
