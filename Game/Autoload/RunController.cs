using System;
using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;
using Underleague.Game.Data;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using System.Linq;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;

namespace Underleague.Game.Autoload;

/// <summary>
/// El único nodo que habla con <c>/Sim</c> (<c>docs/ui-run-minima.md</c>). Envuelve
/// <see cref="RunEngine"/> y los sistemas de la run, guarda el estado y avisa a las pantallas cuando
/// cambia.
/// <para>
/// <b>Regla que evita el error clásico</b>: la interfaz nunca decide (RT-014). Ninguna pantalla llama a
/// <c>/Sim</c> directamente y ninguna calcula nada del juego; si una pantalla necesita un dato que
/// <see cref="RunState"/> no expone, se expone en <c>/Sim</c> como método puro —como
/// <c>Sim.Run.RunSummary</c> o <c>Sim.Perks.Scouting</c>— y no se calcula en la escena.
/// </para>
/// <para>
/// Toda la E/S vive aquí y en <see cref="GameData"/>: leer <c>/data</c>, escribir el guardado. <c>/Sim</c>
/// no lee ficheros ni consulta el reloj (RT-012) y recibe el contenido ya leído.
/// </para>
/// </summary>
public partial class RunController : Node
{
    /// <summary>Guardado ironman: un único slot por run (RT-061).</summary>
    public const string SavePath = "user://run.json";

    private IRunSystems _systems = DefaultRunSystems.Instance;

    /// <summary>La instancia del autoload. Null solo si una escena se ejecuta suelta, sin el proyecto.</summary>
    public static RunController? Instance { get; private set; }

    /// <summary>Estado de la run en curso; null si no hay ninguna.</summary>
    public RunState? State { get; private set; }

    /// <summary>Catálogo con el que se juega la run: el de su instantánea de <c>/data</c> (RT-061b).</summary>
    public Catalog? Catalog { get; private set; }

    /// <summary>Sistemas de la run (economía, mercado, recompensas, clínica): los que consultan las pantallas.</summary>
    public StandardRunSystems? Systems { get; private set; }

    /// <summary>Catálogo de jefes de la run (RF-001b/c).</summary>
    public BossCatalog? Bosses { get; private set; }

    /// <summary>
    /// Los sistemas <b>compuestos</b> con los que se llama al motor: los jefes envuelven a los estándar
    /// (<c>BossRunSystems</c>), igual que en <c>/Balance --full-runs</c>. Es lo que hay que pasarle a
    /// cualquier consulta de <c>/Sim</c> que reciba <c>IRunSystems</c>, para que el partido que se ojea y
    /// el que se juega sean el mismo (RF-012b, RF-012d).
    /// </summary>
    public IRunSystems Engine => _systems;

    /// <summary>True si hay una run cargada en memoria.</summary>
    public bool HasRun => State is not null && Catalog is not null;

    /// <summary>
    /// Nodo que el jugador ha elegido en el mapa y todavía <b>no</b> ha entrado: el que ojea y el que va
    /// a jugar. -1 si no hay ninguno. Lo pone el mapa y lo consume la pantalla de partido.
    /// </summary>
    public int SelectedNodeId { get; set; } = -1;

    /// <summary>
    /// Último partido resuelto, con su resumen y su <c>MatchReport</c> (RF-119). Es lo que leen la
    /// pantalla de partido y la del informe post-partido; null antes del primer partido de la run.
    /// </summary>
    public MatchEntry? LastMatch { get; private set; }

    /// <summary>Cualquier pantalla se redibuja cuando el estado cambia.</summary>
    [Signal]
    public delegate void StateChangedEventHandler();

    /// <summary>Fase de la run (<see cref="RunPhase"/> como entero: la señal viaja por el bus de Godot).</summary>
    [Signal]
    public delegate void PhaseChangedEventHandler(int phase);

    /// <summary>True si hay un guardado ironman en disco (RT-061).</summary>
    public static bool SaveExists => FileAccess.FileExists(SavePath);

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>
    /// Empieza una run con ese club y esa semilla (RF-004): congela la instantánea de <c>/data</c>
    /// (RT-061b), genera plantilla y mapas y deja la run en la entrada del acto 1. Toda la aleatoriedad
    /// posterior sale de esta semilla (RT-021).
    /// </summary>
    /// <param name="clubId">Id del club elegido en <see cref="Screens.StartScreen"/> (RF-004, <c>data/clubs/</c>).</param>
    /// <param name="clubRace">Raza de ese club: todos los jugadores iniciales son de ella (RF-004).</param>
    /// <param name="seed">Semilla de la run (RT-021).</param>
    /// <summary>
    /// <b>Solo para el arnés de capturas</b> (<c>BroadcastCapture</c>), como <c>SeekTo</c> o
    /// <c>Pitch3D</c>: le pone un perk a todos los titulares de campo para poder <b>escenificar</b> un
    /// suceso que la run todavía no regala. Un perk se gana jugando, así que el primer partido del acto 1
    /// no tiene ninguno de los interesantes y una captura que dependa de la suerte no es un experimento.
    /// No se usa jugando y no toca el guardado.
    /// </summary>
    public void ArmStartersForCapture(string perkId)
    {
        if (State is null || Catalog?.Perks.Find(perkId) is null)
        {
            return;
        }

        var roster = State.Roster.ToList();
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i].Position != Position.Goalkeeper)
            {
                roster[i] = PerkPool.WithPerk(roster[i], perkId);
            }
        }

        State = State with { Roster = roster };
    }

    public void NewRun(string clubId, Race clubRace, ulong seed)
    {
        var files = GameData.Snapshot;
        Catalog = DataLoader.FromJson(files);
        Systems = StandardRunSystems.FromJson(files);
        Bosses = BossCatalog.FromJson(files);

        var bossSystems = new BossRunSystems(Bosses, Systems);
        _systems = bossSystems;

        var setup = Systems.NewRunSetup(clubId, clubRace, files);
        State = bossSystems.AssignBosses(RunEngine.Start(setup, seed, Catalog, bossSystems));
        SelectedNodeId = -1;
        LastMatch = null;
        ForgetMatch();

        DeleteSave();
        Save();
        Changed();
    }

    /// <summary>
    /// Aviso que el mapa enseña una vez tras cargar un guardado (ADR 0172): los consumibles de un guardado
    /// anterior que no cupieron en los dos huecos. Vacío si no hay nada que decir; el mapa lo consume.
    /// </summary>
    public string LoadNotice { get; set; } = string.Empty;

    private string LostConsumablesNotice(IReadOnlyList<string> lost)
    {
        if (lost.Count == 0)
        {
            return string.Empty;
        }

        var names = new List<string>(lost.Count);
        foreach (string id in lost)
        {
            names.Add(ConsumableName(id));
        }

        return Ui.UiText.Get("ui.load.lostConsumables", string.Join(", ", names));
    }

    /// <summary>
    /// Retoma el guardado ironman <b>sin borrarlo</b> (RT-061, ADR 0183 enmienda del 3 oct): el slot se
    /// sobrescribe en cada punto de control y sólo se borra al terminar la run. La run se sigue jugando con la instantánea de <c>/data</c> que congeló al
    /// empezar, no
    /// con la del disco (RT-061b). Devuelve false si no había guardado o si no se pudo leer.
    /// </summary>
    public bool Continue()
    {
        if (!SaveExists)
        {
            return false;
        }

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file is null)
            {
                return false;
            }

            // Un guardado de una versión anterior puede no traer los catálogos añadibles que el juego
            // actual exige (apodos, Gaceta, prótesis): se completan con los de /data actual y sólo esos
            // (SnapshotCompletion, RT-061b). Las reglas de la run siguen siendo las de su instantánea.
            var loaded = RunSave.Load(file.GetAsText(), out var lostConsumables, out var pendingMatch);
            var state = SnapshotCompletion.Complete(loaded, GameData.Snapshot);
            Catalog = RunSave.CatalogFromSnapshot(state);
            Systems = StandardRunSystems.FromJson(state.DataSnapshot, fromRunSnapshot: true);
            Bosses = BossCatalog.FromJson(state.DataSnapshot);
            _systems = new BossRunSystems(Bosses, Systems);
            State = state;
            LoadNotice = LostConsumablesNotice(lostConsumables);
            SelectedNodeId = -1;
            LastMatch = null;
            ForgetMatch();

            // BR-A, RT-061, ADR 0183: el guardado era de un partido a medias, y `state` es el de ANTES de ese
            // partido. Se deja el nodo seleccionado y las decisiones listas: la retransmisión lo reproduce
            // (PlayMatch las recoge), no se salta al informe.
            if (pendingMatch is not null)
            {
                _resume = pendingMatch;
                SelectedNodeId = pendingMatch.NodeId;
            }
        }
        catch (Exception error)
        {
            GD.PushError($"no se pudo cargar el guardado: {error.Message}");
            return false;
        }

        Changed();
        return true;
    }

    /// <summary>Nodos a los que se puede entrar ahora (RF-010: sin retroceso). Vacía si hay un nodo abierto.</summary>
    public IReadOnlyList<MapNode> Available() =>
        State is null || Catalog is null ? Array.Empty<MapNode>() : RunEngine.AvailableNodes(State);

    /// <summary>
    /// Entra en un nodo. Si es de partido lo resuelve entero y deja el resumen en
    /// <see cref="LastMatch"/> (RF-119); si no, lo abre y la fase pasa a <see cref="RunPhase.NodeOpen"/>
    /// cuando el nodo pide decisiones.
    /// </summary>
    public void Enter(int nodeId)
    {
        CloseStaleMatch();
        var (state, catalog) = Require();
        var node = state.GetNode(nodeId);
        if (node.IsMatch)
        {
            var entry = RunEngine.EnterMatch(state, nodeId, catalog, _systems, Decisions);
            LastMatch = entry;
            State = entry.State;
        }
        else
        {
            State = RunEngine.Enter(state, nodeId, catalog, _systems);
        }

        SelectedNodeId = -1;
        AfterTransition();
    }

    /// <summary>
    /// Salta a la entrada de otro acto sin jugar los nodos intermedios (RT-062, vía
    /// <see cref="RunStateBuilder"/>). Es modo de depuración: lo usa el recorrido de capturas del mapa
    /// para enseñar los tres actos. No guarda.
    /// </summary>
    public void JumpToAct(int act)
    {
        CloseStaleMatch();
        var (state, _) = Require();
        State = RunStateBuilder.From(state).AtAct(act).Build();
        SelectedNodeId = -1;
        LastMatch = null;
        Changed();
    }

    /// <summary>Coloca la run en un nodo del acto actual sin jugar los anteriores (RT-062). Depuración y capturas.</summary>
    public void JumpToNode(int nodeId)
    {
        CloseStaleMatch();
        var (state, _) = Require();
        State = RunStateBuilder.From(state).AtNode(nodeId).Build();
        SelectedNodeId = -1;
        LastMatch = null;
        Changed();
    }

    /// <summary>
    /// Edita el estado de la run sin pasar por una decisión. <b>Solo para el recorrido de capturas</b>
    /// (<c>--tour-clinic</c>, ADR 0164): un mapa recién generado no tiene lesionados que enseñar en la clínica.
    /// </summary>
    public void SeedForCapture(Func<RunState, RunState> edit)
    {
        CloseStaleMatch();
        var (state, _) = Require();
        State = edit(state);
        Changed();
    }

    /// <summary>Aplica una decisión del jugador (alineación, compra, tratamiento, recompensa, salir del nodo).</summary>
    public void Apply(RunDecision decision)
    {
        CloseStaleMatch();
        var (state, catalog) = Require();
        State = RunEngine.Apply(state, decision, catalog, _systems);
        AfterTransition();
    }

    /// <summary>Desenlace de la run: en curso, victoria, o derrota con su causa (RF-002, RF-002b).</summary>
    public RunOutcome Outcome() => State is null ? RunOutcome.InProgress : RunEngine.Outcome(State);

    /// <summary>
    /// Guarda la run (RT-061). Se llama al completar cada nodo y al cerrar el juego; si la run ha
    /// terminado, en vez de guardar se borra el slot.
    /// </summary>
    public void Save()
    {
        if (State is null)
        {
            return;
        }

        // BR-A, RT-061, ADR 0183: con un partido a medias, lo que hay en memoria ya es el estado de DESPUÉS
        // (RunEngine.EnterMatch lo resolvió entero antes de enseñarlo). Se guarda el de ANTES, con las
        // decisiones tomadas y hasta dónde llegó a ver el jugador; el guardado de después se escribe al
        // cerrar el partido (CommitMatch), no antes.
        // Un Save() limpio (pausa, salir al menú, cerrar la ventana) escribe el tick que el jugador vio de
        // verdad; el guardado del peor caso lo escriben PlayMatch y cada decisión (WriteCheckpoint).
        if (_matchOpen && _stateBeforeMatch is not null)
        {
            WriteCheckpoint(pessimistic: false);
            return;
        }

        if (Outcome().IsOver)
        {
            DeleteSave();
            return;
        }

        WriteSave(RunSave.Save(State));
    }

    /// <summary>
    /// Escritura atómica (ADR 0183, enmienda del 3 oct): el JSON entero va a un temporal del mismo directorio
    /// y sólo cuando está cerrado se renombra sobre el guardado. Un cierre forzado a mitad de escritura deja
    /// el guardado anterior intacto, nunca uno truncado.
    /// </summary>
    private static void WriteSave(string json)
    {
        string temporary = SavePath + ".tmp";
        using (var file = FileAccess.Open(temporary, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushError($"no se pudo escribir el guardado temporal {temporary}");
                return;
            }

            file.StoreString(json);
        }

        if (DirAccess.RenameAbsolute(temporary, SavePath) != Error.Ok)
        {
            GD.PushError($"no se pudo renombrar {temporary} a {SavePath}");
        }
    }

    /// <summary>Borra el guardado ironman.</summary>
    public static void DeleteSave()
    {
        if (SaveExists)
        {
            DirAccess.RemoveAbsolute(SavePath);
        }
    }

    /// <summary>Abandona la run en curso y vuelve al inicio sin guardado (RF-007).</summary>
    public void Abandon()
    {
        State = null;
        LastMatch = null;
        SelectedNodeId = -1;
        ForgetMatch();
        DeleteSave();
        Changed();
    }

    /// <summary>
    /// Sale al menú principal <b>sin</b> perder la run: la guarda como al cerrar la ventana y la descarga
    /// de memoria, así que en el inicio aparece «Continuar la run guardada» y retomarla pasa por
    /// <see cref="Continue"/>, que deja el slot donde está (RT-061). Distinto de <see cref="Abandon"/>,
    /// que sí la pierde (RF-007).
    /// </summary>
    public void LeaveToMenu()
    {
        Save();
        State = null;
        LastMatch = null;
        SelectedNodeId = -1;
        ForgetMatch();
        Changed();
    }

    /// <summary>Cerrar la ventana a mitad de run no pierde la run (RT-061).</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            Save();
        }
    }

    private (RunState State, Catalog Catalog) Require() =>
        State is null || Catalog is null
            ? throw new InvalidOperationException("no hay ninguna run en curso: llama antes a NewRun o a Continue")
            : (State, Catalog);

    /// <summary>
    /// Cierre común de toda transición: guardar (RT-061) y avisar a las pantallas. Se guarda en <b>cada</b>
    /// transición, también con un nodo abierto (ADR 0183, enmienda del 3 oct): el guardado ya no se borra al
    /// cargar, así que lo decidido dentro de un nodo (compra, tratamiento, opción de un evento) debe estar en
    /// disco antes de que el jugador vea su consecuencia; si no, cerrar a la fuerza y volver permitiría
    /// deshacerlo. Un nodo abierto es un estado que el guardado ya sabe cargar (recompensa pendiente).
    /// </summary>
    private void AfterTransition()
    {
        // BR-A, ADR 0183: con un partido abierto, toda transición (entrar, cada decisión) escribe el guardado de
        // ANTES con el suelo del peor caso, sea derrota o victoria: si el proceso muere sin un guardado limpio,
        // la repetición sale con lo decidible bloqueado y el resultado no cambia.
        if (_matchOpen && _stateBeforeMatch is not null)
        {
            WriteCheckpoint(pessimistic: true);
            Changed();
            return;
        }

        if (State is not null)
        {
            Save();
        }

        Changed();
    }

    private void Changed()
    {
        EmitSignal(SignalName.StateChanged);
        EmitSignal(SignalName.PhaseChanged, (int)(State?.Phase ?? RunPhase.OnMap));
    }
}
