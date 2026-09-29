using System;
using System.Collections.Generic;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Consumables;
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
        Playback is null || Catalog is null
            ? null
            : SubstitutionPoints.Pending(Playback.Setup, Playback.Result, Playback.PlayerTeam, Catalog, Decisions.Declines);

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
        Answer(Decisions with { Substitutions = substitutions });
    }

    /// <summary>
    /// «Que se quede el hueco» (ADR 0134 D): el jugador rechaza el sustituto y juega el resto del partido
    /// con una casilla vacía. Es la inferioridad voluntaria de RF-002d ejercida durante el partido, y la
    /// razón para tomarla es de desgaste, no táctica: el que entra también puede morir.
    /// </summary>
    public void Decline(SubstitutionPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        var declines = new List<DeclinedSubstitution>(Decisions.Declines)
        {
            new(point.Tick, point.OutPlayerId),
        };
        Answer(Decisions with { Declines = declines });
    }

    /// <summary>
    /// «Que siga jugando» (ADR 0134 E): el lesionado <b>leve</b> no deja el campo, a cambio de arrastrar ya
    /// la penalización de RF-091 el resto del partido.
    ///
    /// <para>El precio lo calcula <c>/Sim</c>, no esta pantalla: <c>AttributesWithExtraMinorInjuries</c>
    /// vuelve a construir el partido con una lesión leve más para este jugador, así que sale del mismo
    /// pipeline que sus atributos normales —equipo y reposición incluidos— y la inmunidad de los no-muertos
    /// (ADR 0026) se resuelve sola. El <c>extra</c> cuenta las veces que ya se ha quedado en este partido,
    /// para que dos lesiones compongan como componen dos entre partidos.</para>
    /// </summary>
    public void PlayOn(SubstitutionPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (State is null || Catalog is null || _stateBeforeMatch is null)
        {
            throw new InvalidOperationException("no hay ningún partido en reproducción");
        }

        if (!point.CanPlayOn)
        {
            throw new InvalidOperationException(
                $"el jugador {point.OutPlayerId} no puede seguir jugando: solo la lesión leve deja quedarse (ADR 0134 E)");
        }

        int extra = 1;
        for (int i = 0; i < Decisions.PlayOns.Count; i++)
        {
            if (Decisions.PlayOns[i].PlayerId == point.OutPlayerId)
            {
                extra++;
            }
        }

        var after = RunLineup.AttributesWithExtraMinorInjuries(_stateBeforeMatch, Catalog, point.OutPlayerId, extra);
        var playOns = new List<PlayOn>(Decisions.PlayOns) { new(point.Tick, point.OutPlayerId, after) };
        Answer(Decisions with { PlayOns = playOns });
    }

    /// <summary>
    /// ADR 0154: el jugador cambia la orden táctica de su equipo durante el partido, desde
    /// <paramref name="tick"/>. Como las demás decisiones, se vuelve a reproducir el partido con ella
    /// dentro; lo anterior a ese tick no cambia. Las órdenes posteriores que ya hubiera se descartan: ese
    /// futuro es justo el que se está reescribiendo.
    /// </summary>
    public void ChangeOrder(int tick, Mentality order)
    {
        var changes = new List<OrderChange>();
        foreach (var change in Decisions.OrderChanges)
        {
            if (change.Tick < tick)
            {
                changes.Add(change);
            }
        }

        changes.Add(new OrderChange(tick, order));
        Answer(Decisions with { OrderChanges = changes });
    }

    /// <summary>La orden táctica del jugador en ese tick (ADR 0154): la última que eligió antes, o neutra.</summary>
    public Mentality OrderAt(int tick)
    {
        var order = Mentality.Neutral;
        foreach (var change in Decisions.OrderChanges)
        {
            if (change.Tick <= tick)
            {
                order = change.Order;
            }
        }

        return order;
    }

    /// <summary>
    /// BA-H, RF-082: el jugador pulsa un consumible manual en el tick <paramref name="tick"/>. Mismo
    /// patrón que <see cref="ChangeOrder"/> (ADR 0154): se vuelve a reproducir el partido con la
    /// activación dentro del estado inicial (<c>docs/arquitectura.md</c>, "Consumibles manuales durante
    /// el partido"), así que lo anterior al tick no cambia y la reproducción y lo que se aplica de verdad
    /// son el mismo partido (RT-013, RT-024, RT-061).
    ///
    /// <para>Si <paramref name="id"/> ya tenía una activación previa, la reemplaza en vez de añadir una
    /// segunda: un consumible se resuelve una sola vez por partido (RF-085) y "pulsarlo otra vez" solo
    /// tiene sentido como "quise decir ahora, no antes" — nunca como dos usos.</para>
    /// </summary>
    public void UseConsumable(string id, int tick)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var activations = new List<ManualActivation>();
        foreach (var activation in Decisions.ManualActivations)
        {
            if (!string.Equals(activation.ConsumableId, id, StringComparison.Ordinal))
            {
                activations.Add(activation);
            }
        }

        activations.Add(new ManualActivation(id, tick));
        Answer(Decisions with { ManualActivations = activations });
    }

    /// <summary>
    /// Lo que comparten las tres respuestas: se vuelve a reproducir el partido con la decisión dentro y la
    /// run vuelve a entrar en el nodo desde el estado previo, así que la reproducción y lo que se aplica de
    /// verdad son el mismo partido (ADR 0094, RT-024).
    /// </summary>
    private void Answer(MatchDecisions decisions)
    {
        if (State is null || Catalog is null || _stateBeforeMatch is null || _matchNodeId < 0)
        {
            throw new InvalidOperationException("no hay ningún partido en reproducción");
        }

        Decisions = decisions;
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
    /// <summary>La carta del nodo de evento abierto (ADR 0100), o null si no hay ninguno.</summary>
    public EventScreenView? Event() =>
        State is null || Catalog is null || Systems is null
            ? null
            : EventView.Build(State, Catalog, Systems.Events, Systems.Items, Systems.Consumables, Data.GameData.Language);

    /// <summary>La carta del nodo de entrenamiento abierto (ADR 0160), o null si no hay ninguno.</summary>
    public TrainingScreenView? Training() =>
        State is null || Catalog is null || Systems is null
            ? null
            : TrainingView.Build(State, Catalog, Systems.Economy, Data.GameData.Language);

    public MarketScreenView? Market() =>
        State is null || Catalog is null || Systems is null
            ? null
            : MarketView.Build(State, Catalog, Systems.Economy, Systems.Items, Systems.Consumables, Data.GameData.Language);

    /// <summary>
    /// Ficha del árbitro que pitaría <paramref name="node"/> (ADR 0158 §6, RF-061, RF-012b): nombre,
    /// rasgo, la línea que explica lo que hace y la memoria que tiene del jugador. Null sin run o catálogo
    /// cargados. <see cref="RefereeView.For"/> pide el <see cref="StandardRunSystems"/> concreto, no
    /// <see cref="IRunSystems"/> (<see cref="Engine"/>): <c>RefereeFor</c> solo está definido ahí, y
    /// <c>BossRunSystems</c> lo delega sin cambiarlo.
    /// </summary>
    public RefereeCardView? Referee(MapNode node) =>
        State is null || Catalog is null || Systems is null
            ? null
            : RefereeView.For(State, node, Systems, Catalog, Data.GameData.Language);
}
