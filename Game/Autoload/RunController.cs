using System;
using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;
using Underleague.Game.Data;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
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

        DeleteSave();
        Save();
        Changed();
    }

    /// <summary>
    /// Retoma el guardado ironman y lo <b>borra</b> del disco (RT-061: un único slot, que se borra al
    /// cargarse). La run se sigue jugando con la instantánea de <c>/data</c> que congeló al empezar, no
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

            var state = RunSave.Load(file.GetAsText());
            Catalog = RunSave.CatalogFromSnapshot(state);
            Systems = StandardRunSystems.FromJson(state.DataSnapshot);
            Bosses = BossCatalog.FromJson(state.DataSnapshot);
            _systems = new BossRunSystems(Bosses, Systems);
            State = state;
            SelectedNodeId = -1;
            LastMatch = null;
        }
        catch (Exception error)
        {
            GD.PushError($"no se pudo cargar el guardado: {error.Message}");
            return false;
        }

        DeleteSave();
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
        var (state, catalog) = Require();
        var node = state.GetNode(nodeId);
        if (node.IsMatch)
        {
            var entry = RunEngine.EnterMatch(state, nodeId, catalog, _systems);
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

    /// <summary>Aplica una decisión del jugador (alineación, compra, tratamiento, recompensa, salir del nodo).</summary>
    public void Apply(RunDecision decision)
    {
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

        if (Outcome().IsOver)
        {
            DeleteSave();
            return;
        }

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"no se pudo escribir el guardado en {SavePath}");
            return;
        }

        file.StoreString(RunSave.Save(State));
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
        DeleteSave();
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
    /// Cierre común de toda transición: guardar al completar un nodo (RT-061) y avisar a las pantallas.
    /// Se guarda cuando la run vuelve al mapa, que es exactamente "nodo completado"; con un nodo abierto
    /// no se guarda, porque las decisiones de dentro todavía no han terminado.
    /// </summary>
    private void AfterTransition()
    {
        if (State is not null && (State.Phase == RunPhase.OnMap || Outcome().IsOver))
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
