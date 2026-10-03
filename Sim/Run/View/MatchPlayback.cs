using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Un partido reproducido desde su semilla (RF-120, RT-061): el <see cref="MatchSetup"/> con el que se
/// jugó y su <see cref="MatchResult"/> completo, con la secuencia ordenada de eventos.
///
/// <para>Existe porque <c>RunEngine.EnterMatch</c> devuelve el <see cref="MatchReport"/> pero no los
/// eventos, y el log de RF-121 se compone de eventos, no de agregados. Reproducir es determinista: la
/// semilla del partido es <c>RngStreams.MatchSeed(state.Seed, node.Id)</c> y no depende de nada que
/// cambie entre la reproducción y el partido de verdad, así que el partido que se enseña es
/// <b>exactamente</b> el que se jugó (RT-013).</para>
/// </summary>
public sealed record MatchPlayback(MapNode Node, MatchSetup Setup, MatchResult Result, ulong Seed)
{
    /// <summary>Traza de posiciones del partido, o null si se reprodujo sin ella (<c>trace: false</c>).</summary>
    public MatchTrace? Trace => Result.Trace;

    /// <summary>Goles del equipo del jugador (siempre el local, W-15).</summary>
    public int GoalsFor => Result.Report.Goals[PlayerTeam];

    /// <summary>Goles del rival.</summary>
    public int GoalsAgainst => Result.Report.Goals[1 - PlayerTeam];

    /// <summary>True si ganó el equipo del jugador.</summary>
    public bool Won => Result.Report.Winner == PlayerTeam;

    /// <summary>Índice del equipo del jugador dentro del <see cref="MatchSetup"/> (W-15: siempre local).</summary>
    public int PlayerTeam => 0;

    /// <summary>Nombre del equipo del jugador.</summary>
    public string OwnName => Setup.Home.Name;

    /// <summary>Nombre del rival.</summary>
    public string RivalName => Setup.Away.Name;
}

/// <summary>
/// Reproduce el partido de un nodo desde el estado <b>anterior</b> a jugarlo. Puro y determinista, sin
/// E/S ni reloj (RT-012, RT-013): es la misma llamada que hace <c>RunEngine</c> por dentro.
/// </summary>
public static class MatchPlaybacks
{
    /// <summary>
    /// Reproduce el partido del nodo indicado con el estado tal y como estaba <b>antes</b> de entrar en
    /// él. Pasar un estado posterior al partido devuelve otro partido: la plantilla ya no es la misma.
    /// </summary>
    /// <param name="trace">
    /// True para grabar además la <see cref="MatchTrace"/> del partido (posiciones y estados de los 20
    /// jugadores y del balón, tick a tick), que es lo que la pantalla de Partido reproduce sobre el
    /// campo. Es lo único que cambia respecto al partido de verdad, y no cambia el resultado: la traza
    /// solo <b>lee</b> el estado del motor. Por defecto <b>apagada</b>, para que reproducir un partido
    /// en una tanda de medición no cueste memoria.
    /// </param>
    public static MatchPlayback Of(
        RunState stateBeforeMatch, int nodeId, Catalog catalog, IRunSystems? systems = null, bool trace = false, MatchDecisions? decisions = null)
    {
        ArgumentNullException.ThrowIfNull(stateBeforeMatch);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        var node = stateBeforeMatch.GetNode(nodeId);
        decisions ??= MatchDecisions.None;
        var (built, seed, _) = RunEngine.BuildMatch(
            stateBeforeMatch, nodeId, catalog, systems, decisions.ManualActivations, decisions.Substitutions, decisions.PlayOns,
            decisions.OrderChanges);
        var config = systems.MatchConfig(stateBeforeMatch, node, catalog);
        // ADR 0094: el rival sustituye solo con la política por defecto; los puntos de decisión del jugador
        // (equipo 0) se quedan pendientes para que la pantalla abra la ventana (SubstitutionPoints.Pending).
        var (setup, result) = SubstitutionPoints.ResolveAutomatically(
            built, seed, catalog, trace ? config with { Trace = true } : config, static team => team == 1, decisions.Declines);
        return new MatchPlayback(node, setup, result, seed);
    }

    /// <summary>
    /// Como <see cref="Of"/>, pero los puntos de sustitución del jugador que caen por debajo de lo que ya vio
    /// (<paramref name="watchedTick"/>, <see cref="PendingMatch.CanDecideAt"/>) se resuelven aquí con la
    /// política por defecto, sin ventana: elegir con el futuro conocido sería volver a tirar el partido
    /// (ADR 0183). Es exactamente lo que <c>RunEngine.EnterMatch</c> hace con un punto sin respuesta, así que lo
    /// que se enseña y lo que se aplica son el mismo partido. <paramref name="resolved"/> son las decisiones con
    /// esas respuestas añadidas. Un punto sólo existe si hay candidatos, así que la política siempre elige a
    /// alguien y nunca se anota un rechazo que el motor no anotaría.
    /// </summary>
    public static MatchPlayback OfResolvingBlockedPoints(
        RunState stateBeforeMatch,
        int nodeId,
        Catalog catalog,
        IRunSystems? systems,
        bool trace,
        MatchDecisions decisions,
        int watchedTick,
        out MatchDecisions resolved)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        var playback = Of(stateBeforeMatch, nodeId, catalog, systems, trace, decisions);
        resolved = decisions;
        if (watchedTick <= 0)
        {
            return playback;
        }

        // Cota: un punto por suplente como mucho.
        for (int guard = 0; guard < 64; guard++)
        {
            var point = SubstitutionPoints.Pending(playback.Setup, playback.Result, playback.PlayerTeam, catalog, resolved.Declines);
            if (point is null || PendingMatch.CanDecideAt(watchedTick, point.Tick + 1) || point.DefaultCandidateId < 0)
            {
                return playback;
            }

            resolved = resolved with
            {
                Substitutions = new List<Substitution>(resolved.Substitutions)
                {
                    new(point.Tick, point.OutPlayerId, point.DefaultCandidateId),
                },
            };
            playback = Of(stateBeforeMatch, nodeId, catalog, systems, trace, resolved);
        }

        throw new InvalidOperationException("la resolución de los puntos bloqueados no converge (ADR 0183)");
    }

    /// <summary>
    /// BX-19, ADR 0191: la reproducción con traza (<see cref="OfResolvingBlockedPoints"/>) y la entrada en el nodo
    /// (<see cref="RunEngine.EnterMatch(RunState, int, Catalog, IRunSystems?, MatchDecisions?)"/>) con una sola
    /// simulación del partido cuando se puede, que es lo que hace <c>/Game</c> en cada decisión en vivo.
    ///
    /// <para>Se puede cuando la reproducción no deja ningún punto de sustitución del jugador pendiente: la única
    /// diferencia entre las dos resoluciones es que la reproducción deja los puntos del jugador sin responder
    /// (para abrir la ventana) y la entrada los responde con la política. Sin ninguno pendiente, las dos toman las
    /// mismas respuestas en el mismo orden y la traza sólo lee el motor, así que el partido es el mismo
    /// (comprobado byte a byte en <c>PlayAndEnterTests</c>). Con uno pendiente se resuelve aparte, como antes.</para>
    /// </summary>
    public static (MatchPlayback Playback, MatchEntry Entry) PlayAndEnter(
        RunState stateBeforeMatch,
        int nodeId,
        Catalog catalog,
        IRunSystems? systems,
        MatchDecisions decisions,
        int watchedTick,
        out MatchDecisions resolved)
    {
        systems ??= DefaultRunSystems.Instance;
        var playback = OfResolvingBlockedPoints(stateBeforeMatch, nodeId, catalog, systems, trace: true, decisions, watchedTick, out resolved);
        bool playerPointPending =
            SubstitutionPoints.Pending(playback.Setup, playback.Result, playback.PlayerTeam, catalog, resolved.Declines) is not null;
        var entry = playerPointPending
            ? RunEngine.EnterMatch(stateBeforeMatch, nodeId, catalog, systems, resolved)
            : RunEngine.EnterResolvedMatch(stateBeforeMatch, nodeId, catalog, systems, resolved, playback.Setup, playback.Result);
        return (playback, entry);
    }
}
