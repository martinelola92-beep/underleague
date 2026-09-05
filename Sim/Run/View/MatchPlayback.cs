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
    public static MatchPlayback Of(RunState stateBeforeMatch, int nodeId, Catalog catalog, IRunSystems? systems = null)
    {
        ArgumentNullException.ThrowIfNull(stateBeforeMatch);
        ArgumentNullException.ThrowIfNull(catalog);
        systems ??= DefaultRunSystems.Instance;

        var node = stateBeforeMatch.GetNode(nodeId);
        var (setup, seed, _) = RunEngine.BuildMatch(stateBeforeMatch, nodeId, catalog, systems);
        var result = Simulator.Run(setup, seed, catalog, systems.MatchConfig(stateBeforeMatch, node, catalog));
        return new MatchPlayback(node, setup, result, seed);
    }
}
