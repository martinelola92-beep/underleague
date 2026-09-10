using System;
using System.Collections.Generic;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Autoload;

/// <summary>
/// Lo que las pantallas de <b>partido, informe, recompensa y mercado</b> le piden al controlador.
/// <para>
/// Está en un fichero aparte por lo mismo que el resto de la clase existe: <b>ninguna pantalla llama a
/// <c>/Sim</c></b> ni calcula nada del juego (RT-014). Todo lo que se pinta en esas cuatro pantallas sale
/// de un método puro de <c>Sim.Run.View</c> —<c>MatchLogView</c>, <c>PostMatchView</c>,
/// <c>RewardView</c>, <c>MarketView</c>— y este fichero es el único sitio donde se llaman.
/// </para>
/// </summary>
public partial class RunController
{
    /// <summary>
    /// El último partido <b>reproducido</b>, con su secuencia de eventos (RF-121). El
    /// <see cref="Underleague.Sim.Engine.MatchReport"/> de <see cref="LastMatch"/> tiene los agregados
    /// pero no los eventos, y el log se compone de eventos: por eso el partido se reproduce desde su
    /// semilla antes de resolverlo (RF-120, RT-061). El partido que se enseña y el que se juega son el
    /// mismo, porque la semilla y el estado de partida son los mismos.
    /// </summary>
    public MatchPlayback? Playback { get; private set; }

    /// <summary>
    /// ADR 0094: las decisiones del jugador dentro del partido (sustituciones forzadas) viajan como estado
    /// inicial. El estado de ANTES del partido se guarda para volver a entrar con las decisiones nuevas:
    /// la reproducción y la run aplican exactamente el mismo partido (RT-024).
    /// </summary>
    public MatchDecisions Decisions { get; private set; } = MatchDecisions.None;

    private RunState? _stateBeforeMatch;
    private int _matchNodeId = -1;

    /// <summary>Primer punto de sustitución del jugador sin resolver en la reproducción actual, o <c>null</c>.</summary>
    public SubstitutionPoint? PendingSubstitution() =>
        Playback is null ? null : SubstitutionPoints.Pending(Playback.Setup, Playback.Result, Playback.PlayerTeam);

    /// <summary>
    /// El jugador eligió sustituto en la ventana: se vuelve a reproducir el partido con la decisión y la run
    /// vuelve a entrar en el nodo desde el estado previo, con las mismas decisiones.
    /// </summary>
    public void Substitute(Substitution substitution)
    {
        if (State is null || Catalog is null || _stateBeforeMatch is null || _matchNodeId < 0)
        {
            throw new InvalidOperationException("no hay ningún partido en reproducción");
        }

        var substitutions = new List<Substitution>(Decisions.Substitutions) { substitution };
        Decisions = Decisions with { Substitutions = substitutions };
        Playback = MatchPlaybacks.Of(_stateBeforeMatch, _matchNodeId, Catalog, Engine, trace: true, Decisions);
        State = _stateBeforeMatch;
        Enter(_matchNodeId);
    }

    /// <summary>
    /// Juega el partido de ese nodo: lo reproduce para poder narrarlo y después lo resuelve de verdad con
    /// <see cref="Enter"/>, que es quien avanza el estado, guarda y avisa a las pantallas.
    /// </summary>
    public void PlayMatch(int nodeId)
    {
        if (State is null || Catalog is null)
        {
            throw new InvalidOperationException("no hay ninguna run en curso: llama antes a NewRun o a Continue");
        }

        // trace: true — la pantalla de Partido reproduce el campo con la traza de posiciones tick a tick
        // (MatchTrace). Se pide aquí y solo aquí: el partido que /Sim resuelve de verdad en Enter y los
        // millones de partidos de /Balance siguen corriendo sin ella.
        Decisions = MatchDecisions.None;
        _stateBeforeMatch = State;
        _matchNodeId = nodeId;
        Playback = MatchPlaybacks.Of(State, nodeId, Catalog, Engine, trace: true, Decisions);
        Enter(nodeId);
    }

    /// <summary>Log de eventos del último partido (RF-121); vacío si todavía no se ha jugado ninguno.</summary>
    public IReadOnlyList<MatchLogLine> MatchLog() =>
        Playback is null || Catalog is null
            ? Array.Empty<MatchLogLine>()
            : MatchLogView.Build(Playback, Catalog.Tuning.RegulationTicks);

    /// <summary>
    /// Informe post-partido del último partido (RF-119): perks activados con su contribución, bajas,
    /// tarjetas, árbitro y desglose del oro. Null si todavía no se ha jugado ninguno.
    /// </summary>
    public PostMatchReport? PostMatch()
    {
        if (Playback is null || LastMatch is null || State is null || Catalog is null)
        {
            return null;
        }

        return PostMatchView.Build(
            Playback,
            State,
            LastMatch.Summary,
            Catalog,
            Systems?.Economy,
            Systems?.Items,
            Data.GameData.Language);
    }

    /// <summary>Elección de recompensa pendiente (RF-071, ADR 0049); null si no hay ninguna abierta.</summary>
    public RewardScreenView? Reward() =>
        State is null || Catalog is null || Systems is null
            ? null
            : RewardView.Build(State, Catalog, Systems.Economy, Systems.Items, Data.GameData.Language);

    /// <summary>Surtido del nodo de mercado abierto (RF-114); null si el nodo abierto no es un mercado.</summary>
    public MarketScreenView? Market() =>
        State is null || Catalog is null || Systems is null
            ? null
            : MarketView.Build(State, Catalog, Systems.Economy, Systems.Items, Systems.Consumables, Data.GameData.Language);
}
