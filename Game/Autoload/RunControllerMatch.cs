using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
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

    // BR-A, RT-061, ADR 0183. `_matchOpen`: hay un partido que el jugador todavía no ha terminado de ver, y
    // por tanto el guardado es el de ANTES (Save). `_watchedTick`: el tick más lejano que ha llegado a ver.
    // `_floorTick`: el que ya había visto al guardar la vez anterior; antes de él no se puede volver a decidir.
    // `_resume`: el partido a medias que trajo el guardado, hasta que PlayMatch lo recoge.
    private bool _matchOpen;
    private int _watchedTick;
    private int _floorTick;
    private PendingMatch? _resume;

    // True mientras PlayMatch o una decisión aplican el partido (ApplyReplay): es la entrada «legítima» al partido abierto. Cualquier otra
    // entrada o decisión con un partido abierto significa que el jugador ya salió de él sin pasar por el informe.
    private bool _replaying;

    /// <summary>Hasta qué tick vio el jugador este partido en una salida anterior; antes de él no se decide (0 si no hay).</summary>
    public int ReplayFloorTick => _floorTick;

    /// <summary>
    /// True si la run ha terminado <b>y</b> el jugador ya lo ha visto. Mientras el partido decisivo se está
    /// viendo, el estado en memoria ya es el de después (derrota o victoria final), pero la run sigue viva a
    /// efectos de guardar y de salir: el guardado es el de antes del partido (BR-A, ADR 0183).
    /// </summary>
    public bool IsOverNow => !_matchOpen && Outcome().IsOver;

    /// <summary>True si el guardado cargado era de un partido a medias y todavía no se ha retomado (BR-A).</summary>
    public bool HasMatchToResume => _resume is not null;

    /// <summary>
    /// La retransmisión avisa del tick que enseña. Sólo importa el más lejano: es lo que el jugador ya sabe del
    /// partido, y lo que <see cref="CanDecideAt"/> protege al reanudar (ADR 0183).
    /// </summary>
    public void NoteWatched(int tick)
    {
        if (_matchOpen && tick > _watchedTick)
        {
            _watchedTick = tick;
        }
    }

    /// <summary>
    /// Si una decisión en vivo (consumible manual, orden táctica) puede entrar en <paramref name="tick"/>.
    /// Tras volver de un partido a medias no vale antes de lo que el jugador ya había visto: decidir con el
    /// futuro conocido sería volver a tirar el partido (RT-061, ADR 0183).
    /// </summary>
    public bool CanDecideAt(int tick) => PendingMatch.CanDecideAt(_floorTick, tick);

    /// <summary>
    /// Cierra un partido que se quedó abierto sin que nadie llegara al informe (la vista de depuración, un
    /// arnés de capturas): cualquier entrada o decisión nueva demuestra que el jugador ya salió de él, y dejarlo
    /// abierto haría que cada guardado posterior reescribiera en silencio el estado viejo (ADR 0183).
    /// </summary>
    private void CloseStaleMatch()
    {
        if (_matchOpen && !_replaying)
        {
            CommitMatch();
        }
    }

    /// <summary>
    /// La reproducción con las decisiones actuales; los puntos de sustitución por debajo de lo ya visto se
    /// resuelven con la política por defecto en <c>/Sim</c> (ADR 0183) y quedan anotados en
    /// <see cref="Decisions"/>, para que lo que se enseña y lo que se aplica sean el mismo partido.
    /// </summary>
    // BX-19, ADR 0191: cada decisión en vivo vuelve a simular el partido, y eso se hace FUERA del hilo principal para
    // que la retransmisión no se congele. `_decision` es la simulación en marcha; `_decisionConsumable`, el manual
    // pulsado (si no llega a dispararse, la pulsación se retira). `/Sim` no es reentrante: el hilo de fondo juega con
    // su PROPIO catálogo (`_decisionCatalog`, cargado de la misma fuente que `Catalog`), nunca con el de la pantalla.
    private Task<DecisionReplay>? _decision;
    private string? _decisionConsumable;
    private Func<Catalog>? _catalogFactory;
    private Task<Catalog>? _decisionCatalog;

    private sealed record DecisionReplay(MatchPlayback Playback, MatchEntry Entry, MatchDecisions Resolved);

    /// <summary>Qué pasó con una decisión en vivo al consultarla (<see cref="TryCompleteDecision"/>).</summary>
    public enum DecisionProgress
    {
        /// <summary>No había ninguna decisión simulándose.</summary>
        None,

        /// <summary>Sigue simulándose: la pantalla mantiene el fotograma de la decisión.</summary>
        Pending,

        /// <summary>Terminada y aplicada (o retirada, si era un manual que no llegó a dispararse).</summary>
        Applied,
    }

    /// <summary>True mientras una decisión en vivo se está simulando en segundo plano (ADR 0191).</summary>
    public bool DecisionPending => _decision is not null;

    /// <summary>
    /// El catálogo propio del hilo de fondo: se empieza a cargar al abrir el partido, en segundo plano, para que la
    /// primera decisión no lo pague. Mismos ficheros que <see cref="Catalog"/>, así que el partido es el mismo.
    /// </summary>
    private Task<Catalog> DecisionCatalog()
    {
        var factory = _catalogFactory ?? throw new InvalidOperationException("no hay catálogo de la run");
        return _decisionCatalog ??= Task.Run(factory);
    }

    /// <summary>
    /// La reproducción y la entrada en el nodo con las decisiones dadas, con UNA simulación cuando se puede
    /// (<see cref="MatchPlaybacks.PlayAndEnter"/>, ADR 0191). Los puntos de sustitución por debajo de lo ya visto se
    /// resuelven con la política por defecto en <c>/Sim</c> (ADR 0183), y van en <c>Resolved</c>.
    /// </summary>
    private static DecisionReplay Replay(
        RunState before, int nodeId, Catalog catalog, IRunSystems systems, MatchDecisions decisions, int floorTick)
    {
        var (playback, entry) = MatchPlaybacks.PlayAndEnter(before, nodeId, catalog, systems, decisions, floorTick, out var resolved);
        return new DecisionReplay(playback, entry, resolved);
    }

    /// <summary>
    /// Deja el partido como la simulación dice: la reproducción, las decisiones resueltas y el estado de después, y
    /// avisa y guarda como una entrada (<see cref="AfterTransition"/>: el guardado del peor caso, ADR 0183).
    /// </summary>
    private void ApplyReplay(DecisionReplay replay)
    {
        Decisions = replay.Resolved;
        Playback = replay.Playback;
        LastMatch = replay.Entry;
        State = replay.Entry.State;
        SelectedNodeId = -1;
        _replaying = true;
        try
        {
            AfterTransition();
        }
        finally
        {
            _replaying = false;
        }
    }

    /// <summary>
    /// Para la pantalla, una vez por fotograma mientras <see cref="DecisionPending"/>: si la simulación de fondo ya
    /// terminó, la aplica (en el hilo principal) y devuelve <see cref="DecisionProgress.Applied"/>.
    /// </summary>
    public DecisionProgress TryCompleteDecision()
    {
        if (_decision is null)
        {
            return DecisionProgress.None;
        }

        if (!_decision.IsCompleted)
        {
            return DecisionProgress.Pending;
        }

        FinishDecision();
        return DecisionProgress.Applied;
    }

    /// <summary>
    /// Espera a la decisión en marcha y la aplica. Para quien necesita el partido nuevo YA (la vista de depuración, el
    /// arnés de capturas) y para todo lo que cierra o guarda el partido: una decisión no se pierde por salir.
    /// </summary>
    public void CompleteDecision()
    {
        if (_decision is not null)
        {
            FinishDecision();
        }
    }

    private void FinishDecision()
    {
        var task = _decision!;
        string? consumable = _decisionConsumable;
        _decision = null;
        _decisionConsumable = null;

        // GetResult relanza aquí, en el hilo principal, cualquier excepción de /Sim: una decisión que el motor
        // rechaza falla igual que antes, no se pierde en el hilo de fondo.
        var replay = task.GetAwaiter().GetResult();

        // Un manual sólo cuenta como usado si el partido re-simulado lo disparó de verdad (CONSUMABLE_USED de nuestro
        // equipo): si el tick queda fuera del partido, o el partido cambió y acabó antes, la pulsación se retira y el
        // botón vuelve a estar vivo. Lo mismo hace /Sim al gastar (solo sale del hueco el que se activó, RF-085). El
        // partido de antes sigue en Playback/State, así que retirarla es no aplicar nada.
        if (consumable is not null && !Fired(replay.Playback, consumable))
        {
            return;
        }

        ApplyReplay(replay);
    }

    /// <summary>
    /// Sólo para el arnés de capturas (BX-19): el partido actual simulado otra vez en el hilo principal y con el
    /// catálogo de la pantalla, para comprobar que el del hilo de fondo es el mismo.
    /// </summary>
    public (MatchPlayback Playback, string StateAfter) ReplayOnMainThreadForCheck()
    {
        var replay = Replay(_stateBeforeMatch!, _matchNodeId, Catalog!, _systems, Decisions, _floorTick);
        return (replay.Playback, RunSave.Save(replay.Entry.State));
    }

    /// <summary>Sólo para el arnés (BX-19): el estado de después del partido actual, serializado.</summary>
    public string StateAfterForCheck() => RunSave.Save(State!);

    private static bool Fired(MatchPlayback playback, string consumableId)
    {
        foreach (var e in playback.Result.Events)
        {
            if (e.Type == EventType.ConsumableUsed && e.Team == 0 && string.Equals(e.Detail, consumableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void WriteCheckpoint(bool pessimistic)
    {
        var pending = pessimistic && Playback is not null
            ? PendingMatch.BeforeShowing(_matchNodeId, Decisions, Playback, _floorTick)
            : new PendingMatch(_matchNodeId, Decisions, Math.Max(_watchedTick, _floorTick));
        WriteSave(RunSave.Save(_stateBeforeMatch!, pending));
    }

    /// <summary>
    /// El jugador ha llegado al informe: el partido deja de estar a medias y el guardado pasa a ser el de
    /// <b>después</b> (en una derrota definitiva, el slot se borra). Idempotente.
    /// </summary>
    public void CommitMatch()
    {
        CompleteDecision();
        if (!_matchOpen)
        {
            return;
        }

        _matchOpen = false;
        Save();
    }

    private void ForgetMatch()
    {
        // Una run nueva o cargada: la decisión que se estuviera simulando era de otro partido, se tira.
        _decision = null;
        _decisionConsumable = null;
        _matchOpen = false;
        _watchedTick = 0;
        _floorTick = 0;
        _resume = null;
    }

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
        CompleteDecision(); // ADR 0191: la decisión se construye sobre el partido que dejó la anterior
        if (!CanDecideAt(substitution.Tick + 1))
        {
            return;
        }

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
        CompleteDecision(); // ADR 0191: la decisión se construye sobre el partido que dejó la anterior
        ArgumentNullException.ThrowIfNull(point);
        if (!CanDecideAt(point.Tick + 1))
        {
            return;
        }

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
        CompleteDecision(); // ADR 0191: la decisión se construye sobre el partido que dejó la anterior
        ArgumentNullException.ThrowIfNull(point);
        if (State is null || Catalog is null || _stateBeforeMatch is null)
        {
            throw new InvalidOperationException("no hay ningún partido en reproducción");
        }

        if (!CanDecideAt(point.Tick + 1))
        {
            return;
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
        CompleteDecision(); // ADR 0191: la decisión se construye sobre el partido que dejó la anterior
        if (!CanDecideAt(tick))
        {
            return;
        }

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
    /// Nombre de un consumible de la run en el idioma del juego, o el propio id si la run no trae su
    /// catálogo. Las pantallas no leen <c>Name.Es</c> a mano: el idioma lo decide <c>GameData.Language</c>.
    /// </summary>
    public string ConsumableName(string id) =>
        State?.Equipment.Consumables?.Find(id) is { } definition ? Ui.UiText.Name(definition.Name) : id;

    /// <summary>
    /// Descripción generada de lo que hace un consumible (RT-035), en el idioma del juego; vacía si la run
    /// no lo conoce. Es el único punto por el que las pantallas piden ese texto a <c>/Sim</c> (RT-014).
    /// </summary>
    public string ConsumableDescription(string id) =>
        State?.Equipment.Consumables?.Find(id) is { } definition && Catalog is not null
            ? Underleague.Sim.Perks.DescriptionGenerator.DescribeEffects(
                definition.Effects, Catalog.Localization.Get(Data.GameData.Language))
            : string.Empty;

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
        CompleteDecision(); // ADR 0191: la decisión se construye sobre el partido que dejó la anterior
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (!CanDecideAt(tick))
        {
            return;
        }

        var activations = new List<ManualActivation>();
        foreach (var activation in Decisions.ManualActivations)
        {
            if (!string.Equals(activation.ConsumableId, id, StringComparison.Ordinal))
            {
                activations.Add(activation);
            }
        }

        activations.Add(new ManualActivation(id, tick));
        Answer(Decisions with { ManualActivations = activations }, consumableId: id);
    }

    /// <summary>
    /// Lo que comparten las tres respuestas: se vuelve a reproducir el partido con la decisión dentro y la
    /// run vuelve a entrar en el nodo desde el estado previo, así que la reproducción y lo que se aplica de
    /// verdad son el mismo partido (ADR 0094, RT-024).
    /// </summary>
    /// <para>BX-19, ADR 0191: la simulación va en segundo plano y se aplica en <see cref="TryCompleteDecision"/> o
    /// <see cref="CompleteDecision"/>; mientras tanto <see cref="Playback"/> y el estado siguen siendo los de antes. El
    /// partido que sale es el mismo que con la simulación en el hilo principal: mismo estado previo, mismas
    /// decisiones, mismo suelo y un catálogo cargado de los mismos ficheros.</para>
    /// </summary>
    private void Answer(MatchDecisions decisions, string? consumableId = null)
    {
        if (State is null || Catalog is null || _stateBeforeMatch is null || _matchNodeId < 0)
        {
            throw new InvalidOperationException("no hay ningún partido en reproducción");
        }

        // Una decisión a la vez: la siguiente se toma sobre el partido que deja la anterior.
        CompleteDecision();

        var before = _stateBeforeMatch;
        int nodeId = _matchNodeId;
        int floorTick = _floorTick;
        var systems = _systems;
        var catalog = DecisionCatalog();
        _decisionConsumable = consumableId;
        _decision = Task.Run(() => Replay(before, nodeId, catalog.GetAwaiter().GetResult(), systems, decisions, floorTick));
    }

    /// <summary>
    /// Juega el partido de ese nodo: lo reproduce para poder narrarlo y después lo resuelve de verdad con
    /// <see cref="Enter"/>, que es quien avanza el estado, guarda y avisa a las pantallas.
    /// </summary>
    public void PlayMatch(int nodeId)
    {
        CloseStaleMatch();
        if (State is null || Catalog is null)
        {
            throw new InvalidOperationException("no hay ninguna run en curso: llama antes a NewRun o a Continue");
        }

        // trace: true — la pantalla de Partido reproduce el campo con la traza de posiciones tick a tick
        // (MatchTrace). Se pide aquí y solo aquí: el partido que /Sim resuelve de verdad en Enter y los
        // millones de partidos de /Balance siguen corriendo sin ella.
        // BR-A: si viene de un guardado a medias, se retoman sus decisiones y lo que ya había visto.
        var resume = _resume is { } r && r.NodeId == nodeId ? r : null;
        _resume = null;
        Decisions = resume?.Decisions ?? MatchDecisions.None;
        _watchedTick = resume?.WatchedTick ?? 0;
        _floorTick = _watchedTick;
        _stateBeforeMatch = State;
        _matchNodeId = nodeId;
        _matchOpen = true;

        // ADR 0191: el catálogo de las decisiones en vivo se carga ya, en segundo plano, mientras se ve el partido.
        DecisionCatalog();
        ApplyReplay(Replay(State, nodeId, Catalog, _systems, Decisions, _floorTick));
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
            Data.GameData.Language,
            Systems?.Nicknames,
            Systems?.Nemesis);
    }

    /// <summary>Elección de recompensa pendiente (RF-071, ADR 0049); null si no hay ninguna abierta.</summary>
    public RewardScreenView? Reward() =>
        State is null || Catalog is null || Systems is null
            ? null
            : RewardView.Build(State, Catalog, Systems.Economy, Systems.Items, Data.GameData.Language);

    /// <summary>
    /// A quién se puede soltar a cambio de un fichaje con la plantilla llena (BX-5), con lo que le pasará (vender o
    /// descartar). <paramref name="market"/> true en el mercado (vende si el veto de la ADR 0108 lo permite) y false en
    /// una recompensa (siempre descarta). Vacío con la plantilla con hueco.
    /// </summary>
    public IReadOnlyList<SwapCandidate> SwapCandidates(bool market) =>
        State is null || Systems is null
            ? Array.Empty<SwapCandidate>()
            : RosterSwapView.Candidates(State, market ? Systems.Economy : null);

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

    /// <summary>
    /// Cómo se presenta el rival de <paramref name="node"/> (BH-B): el jefe con el nombre de <c>data/bosses/</c>, el
    /// partido de liga o élite con el de su clan, y null en lo que no se juega. Las pantallas no leen
    /// <c>MapNode.OpponentId</c> para presentar a nadie: el del nodo de jefe es un fantasma que resuelve a un clan
    /// de la liga (BE-F). Null sin run cargada.
    /// </summary>
    public OpponentCard? Opponent(MapNode node) =>
        Bosses is null || Systems is null
            ? null
            : OpponentView.For(node, Bosses, Systems.Rivals, Data.GameData.Language);
}
