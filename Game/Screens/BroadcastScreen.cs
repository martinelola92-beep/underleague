using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Underleague.Game.Autoload;
using Underleague.Game.Match;
using Underleague.Game.Ui;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Screens;

/// <summary>
/// Pantalla de <b>Partido</b> definitiva (ADR 0119/0120, RF-050/115/116): la retransmisión con voz de
/// pregón — campo 3D a campo entero, tablero de madera, tiras de papel y presentaciones de momento
/// conducidas por <see cref="Match.PresentationDirector"/>. <see cref="MatchScreen"/> pasa a ser el modo
/// de depuración (vista 2D, tick a tick, log): la tecla <b>F3</b> alterna entre las dos sobre la misma
/// <see cref="RunController.Playback"/> (ADR 0119 «En <c>/Game</c>»).
/// <para>
/// <b>No calcula nada del partido</b> (RT-014): el director agrupa momentos ya clasificados por
/// <c>Sim.Run.View.MatchMomentView</c> y aquí solo se decide el ritmo real (segundos, cola, animación) y
/// se lee la traza/el log para el residuo (marcador, tiras, bandeja) en el fotograma que el director
/// entrega. Jugar el partido al entrar es <b>idempotente</b>: se reutiliza la misma comprobación que
/// <see cref="MatchScreen"/> (<c>RunController.SelectedNodeId</c> vuelve a -1 tras <c>Enter</c>), así que
/// alternar con F3 no vuelve a jugar el partido.
/// </para>
/// </summary>
public partial class BroadcastScreen : Control
{
    /// <summary>
    /// Configuración del campo 3D para una variante de captura de profundidad
    /// (<see cref="BroadcastCapture"/>, revisión del orquestador: «el campo debe tener más 3D, más
    /// profundidad»). <see cref="CaptureVariant"/> es <c>null</c> en cualquier partido de verdad —la
    /// interfaz normal no cambia— y solo lo pone el arnés de capturas antes de instanciar esta escena.
    /// </summary>
    public readonly record struct PitchVariant(string Label, bool Perspective, float Elevation, float Fov, bool Stadium);

    /// <summary>
    /// Override de <see cref="PitchVariant"/> para la próxima instancia que llame a <see cref="Build"/>,
    /// o <c>null</c> para el campo de la retransmisión de siempre (<see cref="DefaultVariant"/>). Solo lo
    /// toca <see cref="BroadcastCapture"/>; se lee una vez en <see cref="Build"/> y no se conserva entre
    /// instancias.
    /// </summary>
    public static PitchVariant? CaptureVariant { get; set; }

    /// <summary>
    /// La variante elegida tras comparar A-E con capturas (revisión del orquestador, 19 sep 2026):
    /// perspectiva FOV 30°/elevación 45°, con estadio — más profundidad que el ortográfico sin el
    /// ensanchamiento de esquinas de un FOV más abierto. Es lo que ve cualquier partido de verdad; el modo
    /// depuración (<see cref="MatchScreen"/>) no la usa, sigue con los valores por defecto de
    /// <see cref="Ui.MatchPitchView3D"/> (ortográfico, sin estadio).
    /// </summary>
    private static readonly PitchVariant DefaultVariant = new("D", Perspective: true, Elevation: 45f, Fov: 30f, Stadium: true);

    private const float CanvasWidth = 1920f;

    /// <summary>Margen entre el borde inferior de las tiras de jugador y el borde del lienzo.</summary>
    private const float StripBottomMargin = 23f;

    /// <summary>
    /// Alto del lienzo lógico. <b>No</b> es una constante (RA-027, tarea 16:9): sale de
    /// <see cref="CanvasWidth"/> por el aspecto real del área lógica (<c>GetViewport().GetVisibleRect()</c>)
    /// en cada <see cref="Build"/> — 1200 a 16:10 (el de siempre, sin cambios), 1080 a 16:9. Todo lo
    /// anclado al fondo del lienzo (tiras, bandeja) se calcula relativo a este valor, nunca a un número
    /// fijo, para que la composición no se corte cuando el aspecto cambia.
    /// </summary>
    private float _canvasHeight = 1200f;

    /// <summary>Y de las tiras de jugador y la banqueta, pegadas al borde inferior del lienzo real.</summary>
    private float _stripY;

    /// <summary>Ticks lógicos por segundo (RT-020).</summary>
    private const float TicksPerSecond = 15f;

    private static readonly int[] Speeds = { 1, 4, 16 };

    private RunController _run = null!;
    private Catalog _catalog = null!;
    private MatchPlayback _playback = null!;
    private MatchTrace? _trace;
    private MatchMoments _moments = null!;
    private PresentationDirector _director = null!;

    private BroadcastBoard _board = null!;
    private readonly List<PlayerStrip> _strips = new();
    private BenchPlaque _bench = null!;
    private Stamp _stamp = null!;
    private HeraldBanner _banner = null!;
    private ProclamationBand _band = null!;
    private Edict _edict = null!;
    private MatchRecord _record = null!;
    private DecisionTray _tray = null!;
    private MatchPitchView3D _pitch3d = null!;

    private int _frame;
    private double _carry;
    private int _speedIndex;
    private bool _manualPaused;
    private bool _frozenLastFrame;
    private bool _matchEnded;
    private int _synced = -1;

    private MatchMoment? _lastStampMoment;
    private MatchMoment? _lastVoiceMoment;
    private SubstitutionPoint? _pendingPoint;

    // ------------------------------------------------------------------ gestos de cámara (docs/ui/README §4)

    /// <summary>
    /// Un tiro para el gesto de cámara: TODOS los tiros lo reciben, cualquiera que sea su resultado —eso
    /// es lo que hace que el acercamiento no delate el gol (última regla de §4: «no zoom = gol»)— con la
    /// misma duración mínima de mantenimiento real (<see cref="ShotPunchHoldSeconds"/>), independiente de
    /// cuándo o cómo resuelva el tiro (revisión del orquestador, 19 sep 2026: un tiro a bocajarro que
    /// resuelve en el mismo tick tenía antes ventana cero y por tanto ningún gesto — eso SÍ correlaciona
    /// con el resultado). Si el tiro acaba en gol, el acercamiento se suelta antes, al empezar el
    /// estandarte (<see cref="ShowGoalBanner"/>), con su salida normal — la reproducción se congela ahí y
    /// no tiene sentido seguir acercando a un tiro que ya se ha convertido en otra cosa.
    /// </summary>
    private readonly record struct ShotGesture(int StartFrame, Cell Cell);

    // Acercamiento del tiro (docs/ui/README §4, valores provisionales — marcador de posición procedural,
    // regla 10 de CLAUDE.md): ×1,15, entrada 0,25 s, mantenimiento mínimo 0,35 s reales, salida 0,4 s.
    private const float ShotPunchZoom = 1.15f;
    private const float ShotPunchInSeconds = 0.25f;
    private const float ShotPunchHoldSeconds = 0.35f;
    private const float ShotPunchOutSeconds = 0.4f;

    private readonly List<ShotGesture> _shotGestures = new();
    private int _nextShotGestureIndex;

    // ------------------------------------------------------------------ capa de campo (audio)

    /// <summary>
    /// Un sonido del campo listo para sonar: el fotograma del evento y los pools que le tocan
    /// (<see cref="MatchEventSounds"/>). Se precalcula en <see cref="BindPlayback"/> por la misma razón que
    /// los gestos de tiro —recorrer 3.000 eventos en cada fotograma para encontrar los de <i>este</i> es
    /// trabajo que se hace una vez— y se consume con un puntero, así que un evento suena <b>una sola vez</b>
    /// aunque el director congele la reproducción encima de él.
    /// </summary>
    private readonly record struct EventSound(int StartFrame, string[] Pools);

    private readonly List<EventSound> _eventSounds = new();
    private int _nextEventSoundIndex;

    // Sacudida de gol/roja/lesión grave (docs/ui/README §4: gol es "suave", roja y lesión grave "más
    // cortas"): valores provisionales, marcador de posición procedural hasta que haya arte.
    private const float GoalShakeAmplitude = 0.05f;
    private const float GoalShakeSeconds = 0.35f;
    private const float CardOrInjuryShakeAmplitude = 0.08f;
    private const float CardOrInjuryShakeSeconds = 0.18f;

    // Muerte, dos tiempos (docs/ui/README §4): el acercamiento entra en 0,4 s —más pausado que el del
    // tiro, tono de la escena— y se suelta A MANO (ver StartDeathTwoStage/ReleasePunch), nunca por su
    // propio hueco: HoldSeconds es solo la red de seguridad si algo impidiera llamar a ReleasePunch.
    private const float DeathPunchZoom = 1.35f;
    private const float DeathPunchInSeconds = 0.4f;
    private const float DeathPunchHoldSeconds = 20f;
    private const float DeathPunchOutSeconds = 0.5f;

    /// <summary>El bando de una muerte espera este tiempo real desde que el campo empieza a contarla (docs/ui/README §4: «dos tiempos»): el campo cuenta la muerte primero.</summary>
    private const float DeathEdictDelaySeconds = 1.2f;

    private MatchEvent? _pendingDeathEvent;
    private float _deathEdictDelay;
    private bool _deathTrayPending;

    // Tamaño de la mancha de sangre en casillas, por gravedad (RA-027, marcador de posición procedural):
    // leve pequeña, grave mediana, muerte grande.
    private const float BloodSizeMinorInjury = 0.32f;
    private const float BloodSizeSevereInjury = 0.5f;
    private const float BloodSizeDeath = 0.8f;

    /// <summary>
    /// El momento que tiene la reproducción congelada, o null si no está congelada (revisión visual del
    /// orquestador, principio 5: mientras se proclama un suceso, su residuo — tablero, tiras, residuo del
    /// rival — ya tiene que reflejarlo, aunque el campo siga congelado un tick por detrás). Lo usa
    /// <see cref="Sync"/> para leer <c>moment.LastFrame</c> en vez de <c>_frame</c>.
    /// </summary>
    private MatchMoment? _residueMoment;
    private MatchMoment? _lastResidueMoment;

    public override void _Ready()
    {
        var run = RunController.Instance;
        if (run is null || !run.HasRun)
        {
            Nav.Route(this);
            return;
        }

        _run = run;

        // Idempotencia (ADR 0119): igual que MatchScreen, solo se juega si SelectedNodeId sigue apuntando
        // al nodo — RunController.Enter lo pone a -1 al terminar, así que volver a entrar (F3, o esta
        // misma pantalla tras el informe) no vuelve a jugar el partido.
        if (_run.SelectedNodeId >= 0 && _run.State!.GetNode(_run.SelectedNodeId).IsMatch)
        {
            _run.PlayMatch(_run.SelectedNodeId);
        }

        if (_run.Playback is null)
        {
            Nav.Route(this);
            return;
        }

        Build();
    }

    public override void _Process(double delta)
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        if (_manualPaused || _matchEnded)
        {
            return;
        }

        int candidate = _frame;
        if (!_frozenLastFrame)
        {
            _carry += delta * TicksPerSecond * Speeds[_speedIndex];
            int advance = (int)_carry;
            if (advance > 0)
            {
                _carry -= advance;
                candidate = Mathf.Clamp(_frame + advance, 0, trace.FrameCount - 1);
            }
        }

        var result = _director.Advance(candidate, delta, Speeds[_speedIndex]);
        _frozenLastFrame = result.Frozen;
        _frame = Mathf.Clamp(result.DisplayFrame, 0, trace.FrameCount - 1);
        _residueMoment = result.Frozen ? result.Voice : null;

        ApplyPresentation(result);

        // Muerte, dos tiempos (docs/ui/README §4): si el bando quedó pendiente de su retardo, la bandeja
        // se abre cuando toque (UpdateDeathEdict más abajo), no aquí — el campo cuenta la muerte primero.
        if (result.AwaitingDecision && !_deathTrayPending)
        {
            OpenDecision();
        }

        Sync();

        // Gestos SOLO a x1 (docs/ui/README §4: «la velocidad degrada la presentación, nunca la
        // información»): a x4/x16 ningún tiro dispara el acercamiento, pero el bando/la bandeja de una
        // muerte siguen su propio reloj igual — son información, no adorno de cámara.
        UpdateShotGesture(Speeds[_speedIndex] == 1);
        UpdateEventSounds(Speeds[_speedIndex] == 1);
        UpdateDeathEdict((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.F3 })
        {
            Nav.Go(this, Nav.MatchDebug);
            return;
        }

        if (_matchEnded && (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_cancel")))
        {
            GoToReport();
        }
    }

    // ------------------------------------------------------------------ construcción

    private void Build()
    {
        _catalog = _run.Catalog!;

        // Alto real del lienzo (RA-027, tarea 16:9): 1920 de ancho por el aspecto del área lógica —1200
        // a 16:10, 1080 a 16:9—, nunca un número fijo. Se lee antes de nada que dependa de ella.
        var viewport = GetViewport().GetVisibleRect().Size;
        _canvasHeight = viewport is { X: > 0f, Y: > 0f } ? CanvasWidth * (viewport.Y / viewport.X) : 1200f;
        _stripY = _canvasHeight - PlayerStrip.DesignHeight - StripBottomMargin;

        Size = new Vector2(CanvasWidth, _canvasHeight);
        Position = Vector2.Zero;
        Theme = Pregon.BuildTheme();
        MouseFilter = MouseFilterEnum.Stop;

        float scale = viewport.X > 0f ? viewport.X / CanvasWidth : 1f;
        Scale = new Vector2(scale, scale);

        Widgets.Panel(this, new Rect2(Vector2.Zero, new Vector2(CanvasWidth, _canvasHeight)), new Color("2a2418"));

        BindPlayback();

        // Variante de captura de profundidad (BroadcastCapture), o el campo de siempre si nadie la pidió
        // (CaptureVariant vuelve a null justo debajo: no debe sobrevivir a esta instancia).
        var variant = CaptureVariant;
        CaptureVariant = null;

        _pitch3d = new MatchPitchView3D
        {
            // Campo entero de lado a lado (revisión visual del orquestador, 19 sep 2026, contra
            // docs/ui/capturas/base-1280x800.jpg): el 3D cubre TODO el lienzo, detrás del tablero y las
            // tiras, no una franja recortada — la que probé antes dejaba negro alrededor y no es lo que
            // pide la composición validada.
            Position = Vector2.Zero,
            Size = new Vector2(CanvasWidth, _canvasHeight),

            // docs/ui/README.md §7: Size 10,75 a 16:10 (9,68 a 16:9) — ancho del campo (16) + 0,6 casillas
            // por lado, para el lienzo entero: OrthoSize (alto) = (Columnas + 1,2) / aspecto. Solo se lee
            // en ortográfico (variantes A/B o el campo de siempre); en perspectiva el encaje automático de
            // MatchPitchView3D decide la distancia y el desplazamiento por su cuenta.
            OrthoSize = (Pitch.Columns + 1.2f) / (CanvasWidth / _canvasHeight),

            // Bajado, no centrado (composición validada: el tablero se lleva más margen arriba que las
            // tiras abajo). 0,27 unidades de mundo: medido por píxel contra
            // docs/ui/capturas/base-1280x800.jpg (césped x45-1235, y207-658) decodificando el PNG propio
            // — la horquilla horizontal ya salía exacta con solo el ancho del OrthoSize; la vertical
            // necesitó este ajuste. No hay una fórmula en la documentación para este valor. Solo se lee en
            // ortográfico, por la misma razón que OrthoSize.
            PanUp = 0.27f,

            // Verde de alrededores plano, no el fondo casi negro del modo depuración (ADR 0102): sin
            // gradas ni vallas (regla 10), pero tampoco negro fuera del rectángulo de 16x7. Con Stadium
            // activo (variantes de profundidad) queda tapado por la explanada de tierra del propio
            // estadio; se deja igual para no bifurcar el color por variante.
            SurroundColor = new Color("33452c"),

            Elevation = (variant ?? DefaultVariant).Elevation,
            Perspective = (variant ?? DefaultVariant).Perspective,
            Fov = (variant ?? DefaultVariant).Fov,
            Stadium = (variant ?? DefaultVariant).Stadium,
        };
        AddChild(_pitch3d);
        _pitch3d.Bind(_trace, _playback.Setup, _catalog);
        _pitch3d.Marks = _moments.Marks;
        BuildBloodMarks();

        _board = new BroadcastBoard();
        AddChild(_board);
        _board.Position = new Vector2(0f, 8f);
        _board.Size = new Vector2(CanvasWidth, BroadcastBoard.DesignHeight);
        _board.SpeedChosen += OnSpeedChosen;
        _board.PauseToggled += OnPauseToggled;

        float x0 = (CanvasWidth - ((7 * 232f) + (6 * 12f) + 24f + 150f)) / 2f;
        for (int i = 0; i < 7; i++)
        {
            var strip = new PlayerStrip();
            AddChild(strip);
            strip.Position = new Vector2(x0 + (i * 244f), _stripY);
            strip.Size = new Vector2(PlayerStrip.DesignWidth, PlayerStrip.DesignHeight);
            _strips.Add(strip);
        }

        _bench = new BenchPlaque();
        AddChild(_bench);
        _bench.Position = new Vector2(x0 + (7 * 244f) + 12f, _stripY);
        _bench.Size = new Vector2(BenchPlaque.DesignWidth, BenchPlaque.DesignHeight);

        _stamp = new Stamp();
        AddChild(_stamp);
        _stamp.Position = new Vector2(980f, 165f);

        _banner = new HeraldBanner();
        AddChild(_banner);
        _banner.Position = new Vector2(0f, 150f);
        _banner.Size = new Vector2(HeraldBanner.DesignWidth, HeraldBanner.DesignHeight);

        _band = new ProclamationBand();
        AddChild(_band);
        _band.Position = new Vector2(0f, 150f);
        _band.Size = new Vector2(CanvasWidth, ProclamationBand.DesignHeight);

        _edict = new Edict();
        AddChild(_edict);
        _edict.Position = new Vector2(0f, 150f);
        _edict.Size = new Vector2(Edict.DesignWidth, Edict.DesignHeight);

        _record = new MatchRecord();
        AddChild(_record);
        _record.Position = Vector2.Zero;
        _record.Size = new Vector2(CanvasWidth, _canvasHeight);

        _tray = new DecisionTray();
        AddChild(_tray);
        _tray.Position = new Vector2(0f, _canvasHeight - 12f - DecisionTray.DesignHeight);
        _tray.Size = new Vector2(CanvasWidth, DecisionTray.DesignHeight);
        _tray.Chosen += OnSubstituteChosen;
        _tray.OptionChosen += OnOptionChosen;

        Sync();
    }

    /// <summary>
    /// Ata la pantalla al partido reproducido actual: traza, momentos (<c>MatchMomentView.Build</c>) y un
    /// director nuevo. Se llama al construir la pantalla y tras resolver una decisión (ADR 0094): el
    /// partido con la sustitución ya está en <see cref="RunController.Playback"/>.
    /// </summary>
    private void BindPlayback()
    {
        _playback = _run.Playback!;
        _trace = _playback.Trace;
        if (_trace is null)
        {
            return;
        }

        _moments = MatchMomentView.Build(_playback.Setup, _playback.Result, _catalog, 0, _run.Decisions.Declines);
        _director = new PresentationDirector(_moments.Moments, DirectorTimings.Default);
        BuildShotGestures();
        BuildEventSounds();
    }

    // ------------------------------------------------------------------ decisión (ADR 0094)

    private void OpenDecision()
    {
        if (_tray.Visible)
        {
            return;
        }

        var point = _run.PendingSubstitution();
        if (point is null)
        {
            // El director pidió decisión pero ya no hay ninguna pendiente (no debería pasar): se libera
            // para no dejar la pantalla congelada sin remedio.
            _director.Resolve();
            return;
        }

        var outPlayer = FindDefinition(_playback.Setup.Home, point.OutPlayerId);
        if (outPlayer is null)
        {
            _director.Resolve();
            return;
        }

        _pendingPoint = point;

        foreach (var strip in _strips)
        {
            strip.Visible = false;
        }

        _bench.Visible = false;

        // ADR 0134: la casilla y el puesto los trae ya el punto de decisión, calculados en /Sim; la pantalla
        // no los deduce. El estado distingue leve de grave porque de eso depende que quepa seguir jugando.
        string square = SquareLabel(point.OutCell);
        string outState = point.Detail == "death"
            ? UiText.Get("ui.pregon.tray.outState.death")
            : UiText.Get(point.CanPlayOn ? "ui.pregon.tray.outState.injuryMinor" : "ui.pregon.tray.outState.injurySevere");

        _tray.SetOutgoing(new OutgoingModel(point.OutPlayerId, outPlayer.Name, UiText.Get("ui.pos." + outPlayer.Position), square, outState));

        var candidates = new List<CandidateModel>(point.Candidates.Count);
        for (int i = 0; i < point.Candidates.Count; i++)
        {
            var candidate = point.Candidates[i];
            int risk = i < point.CandidateRisks.Count ? point.CandidateRisks[i] : 0;
            candidates.Add(new CandidateModel(
                candidate.Id,
                candidate.Name,
                UiText.Get("ui.pos." + candidate.Position),
                UiText.Get("ui.state." + candidate.PhysicalState),
                candidate.Id == point.DefaultCandidateId,
                risk > 0 ? UiText.Get("ui.pregon.tray.candidateRisk", RiskLabel(risk)) : string.Empty));
        }

        // Las otras dos respuestas al mismo punto (ADR 0134 D y E). «Que siga jugando» solo cuando la lesión
        // fue leve: lo decide /Sim, no esta pantalla.
        var options = new List<TrayOption>(2)
        {
            new("decline", UiText.Get("ui.pregon.tray.decline"), UiText.Get("ui.pregon.tray.declineSub")),
        };
        if (point.CanPlayOn)
        {
            // El inmune (ADR 0026) no paga el −15 %, así que no se le anuncia; el riesgo letal sí lo paga,
            // y va aparte porque es el coste dominante de quedarse (ADR 0134 E).
            options.Add(new TrayOption(
                "playOn",
                UiText.Get("ui.pregon.tray.playOn"),
                UiText.Get(point.PlayOnImmune ? "ui.pregon.tray.playOnSubImmune" : "ui.pregon.tray.playOnSub"),
                point.PlayOnRisk > 0 ? UiText.Get("ui.pregon.tray.candidateRisk", RiskLabel(point.PlayOnRisk)) : string.Empty));
        }

        _tray.SetCandidates(candidates, options);
    }

    private void OnSubstituteChosen(int playerId)
    {
        if (_pendingPoint is null)
        {
            return;
        }

        var point = _pendingPoint;
        _pendingPoint = null;
        _run.Substitute(new Substitution(point.Tick, point.OutPlayerId, playerId));
        AfterDecision(point);
    }

    /// <summary>
    /// Las otras dos respuestas al punto de decisión (ADR 0134): dejar el hueco o que el lesionado leve siga
    /// jugando. Acaban en el mismo sitio que la sustitución —se vuelve a reproducir el partido con la
    /// decisión dentro— porque para la pantalla las tres son lo mismo: una decisión que cambia el partido
    /// desde ese tick.
    /// </summary>
    private void OnOptionChosen(string kind)
    {
        if (_pendingPoint is null)
        {
            return;
        }

        var point = _pendingPoint;
        _pendingPoint = null;
        if (kind == "playOn")
        {
            _run.PlayOn(point);
        }
        else
        {
            _run.Decline(point);
        }

        AfterDecision(point);
    }

    /// <summary>Lo que hay que rehacer en la pantalla después de cualquiera de las tres respuestas.</summary>
    private void AfterDecision(SubstitutionPoint point)
    {
        // Al resolver la decisión se suelta cualquier acercamiento en curso (docs/ui/README §4: el de
        // muerte, si lo había, se soltaba "al terminar la voz o al resolver la decisión" — esto es lo
        // segundo). Sin efecto si no había ninguno.
        _pitch3d.ReleasePunch();
        _pendingDeathEvent = null;
        _deathTrayPending = false;

        _tray.Visible = false;
        foreach (var strip in _strips)
        {
            strip.Visible = true;
        }

        _bench.Visible = true;

        BindPlayback();
        if (_trace is null)
        {
            return;
        }

        int decisionFrame = _trace.FrameOfTick(point.Tick);
        _director.Seek(decisionFrame + 1);
        _director.Resolve();
        _pitch3d.Bind(_trace, _playback.Setup, _catalog);
        _pitch3d.Marks = _moments.Marks;
        BuildBloodMarks();

        _frame = decisionFrame;
        _carry = 0d;
        _frozenLastFrame = false;
        _residueMoment = null;
        _lastStampMoment = null;
        _lastVoiceMoment = null;
        _synced = -1;

        // La reproducción cambia con la sustitución (docs/ui/README §4/RA-027): BindPlayback ya reconstruyó
        // la lista de tiros, y aquí se resincroniza el puntero al fotograma de la decisión para no repetir
        // un tiro que ya quedó atrás ni perder uno que caiga justo después.
        ResyncShotGestures(_frame);
        ResyncEventSounds(_frame);
        _pitch3d.ResetGestures();

        Sync();
    }

    private static LineupSlot? FindSlot(TeamSetup side, int playerId)
    {
        var slots = side.Lineup.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].PlayerId == playerId)
            {
                return slots[i];
            }
        }

        return null;
    }

    /// <summary>El riesgo en base 10.000 como porcentaje con un decimal, igual que lo escribe el Ojeo.</summary>
    private static string RiskLabel(int risk) => UiText.Get("ui.risk.percent", risk / 100, (risk % 100) / 10);

    private static string SquareLabel(Cell cell) =>
        ((char)('A' + Mathf.Clamp(cell.Column, 0, 7))).ToString(CultureInfo.InvariantCulture)
        + (cell.Row + 1).ToString(CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ presentaciones (director)

    /// <summary>
    /// El sonido del momento, cuando el momento <b>empieza</b> a presentarse. Se llama desde los dos
    /// canales —sello y voz alta— justo donde cada uno detecta que ha cambiado de momento, así que suena
    /// una vez por momento y no una vez por fotograma.
    ///
    /// <para>La pantalla no elige qué fichero suena ni sabe cuántos hay: pide un pool
    /// (<see cref="MomentSounds"/>) y el <c>AudioManager</c> reparte las variantes. Si el pool aún no tiene
    /// sonidos, la llamada es un no-op.</para>
    /// </summary>
    private static void PlayMomentSound(MatchMoment moment)
    {
        foreach (string pool in MomentSounds.PoolsFor(moment.Kind))
        {
            AudioManager.Instance?.PlayRandomSfx(pool);
        }
    }

    private void ApplyPresentation(DirectorFrame result)
    {
        if (result.Stamp != _lastStampMoment)
        {
            _lastStampMoment = result.Stamp;
            if (result.Stamp is null)
            {
                _stamp.Visible = false;
            }
            else
            {
                PresentStamp(result.Stamp);
                PlayMomentSound(result.Stamp);
            }
        }

        // Una vez enseñada el acta final se queda: no la puede tapar ningún residuo posterior del
        // director (el partido ya ha terminado) y solo se cierra por la acción del jugador.
        if (_matchEnded || result.Voice == _lastVoiceMoment)
        {
            return;
        }

        // Muerte, dos tiempos (docs/ui/README §4): «se suelta cuando termina la voz o al resolver la
        // decisión» — esto es lo primero. Si la que se va es una muerte SIN decisión (con decisión, el
        // director la deja fija como voz hasta Resolve() y nunca llega aquí mientras se espera), se suelta
        // el acercamiento y se limpia cualquier bando pendiente que no le hubiera dado tiempo a salir.
        if (_lastVoiceMoment?.Kind == MomentKind.Death && result.Voice != _lastVoiceMoment)
        {
            _pitch3d.ReleasePunch();
            _pendingDeathEvent = null;
            _deathTrayPending = false;
        }

        _lastVoiceMoment = result.Voice;
        if (result.Voice is not null)
        {
            PlayMomentSound(result.Voice);
        }

        _banner.Visible = false;
        _band.Visible = false;
        _edict.Visible = false;

        // El acta se queda pegada (arriba) mientras el partido no haya terminado de verdad: si se llega
        // aquí es porque _matchEnded acaba de reponerse a false (SeekTo de una captura, o un director
        // nuevo tras una decisión), y el acta de una presentación ANTERIOR en este mismo proceso no debe
        // seguir tapando la pantalla.
        _record.Visible = false;
        if (result.Voice is not null)
        {
            PresentVoice(result.Voice);
        }
    }

    private void PresentStamp(MatchMoment moment)
    {
        if (moment.Cancelled)
        {
            _stamp.Show(UiText.Get("ui.pregon.stamp.cancelled"), StampTone.Cancelled, large: false);
            return;
        }

        switch (moment.Kind)
        {
            case MomentKind.Foul:
            {
                var head = FindHeadEvent(moment);
                bool unseen = head is not null && head.Detail == "unseen";
                _stamp.Show(
                    UiText.Get(unseen ? "ui.pregon.stamp.unseen" : "ui.pregon.stamp.foul"),
                    unseen ? StampTone.Unseen : StampTone.Foul,
                    large: false);
                break;
            }

            case MomentKind.Yellow:
                _stamp.Show(UiText.Get("ui.pregon.stamp.yellow"), StampTone.Yellow, large: false);
                break;

            case MomentKind.MinorInjury:
                _stamp.Show(UiText.Get("ui.pregon.stamp.minorInjury"), StampTone.MinorInjury, large: false);
                break;

            case MomentKind.Consumable:
                _stamp.Show(UiText.Get("ui.pregon.stamp.consumable"), StampTone.Consumable, large: false);
                break;

            case MomentKind.Substitution:
                _stamp.Show(UiText.Get("ui.pregon.stamp.substitution"), StampTone.Unseen, large: false);
                break;

            default:
                _stamp.Show(UiText.Get("ui.pregon.stamp.foul"), StampTone.Foul, large: false);
                break;
        }
    }

    private void PresentVoice(MatchMoment moment)
    {
        // N4 manda siempre, aunque el momento contenga también un gol (gol de oro + final, ADR 0119
        // cadena fija): la acta y el bando son el suceso más alto, no el estandarte del gol que lo trajo.
        if (moment.Kind == MomentKind.Death)
        {
            var death = FindHeadEvent(moment);
            if (death is not null)
            {
                StartDeathTwoStage(moment, death);
            }

            return;
        }

        if (moment.Kind == MomentKind.FullTime)
        {
            ShowRecord(moment);
            return;
        }

        // HasGoal, no Kind == Goal (aviso del revisor): un gol seguido de una tarjeta del mismo nivel
        // puede dejar el momento encabezado por la tarjeta (regla 3, empate a nivel gana el último
        // suceso), pero sigue siendo, ante todo, un gol.
        if (moment.HasGoal)
        {
            var goal = FindGoalEvent(moment) ?? FindHeadEvent(moment);
            if (goal is not null)
            {
                ShowGoalBanner(goal);
                return;
            }
        }

        switch (moment.Kind)
        {
            case MomentKind.Kickoff:
                ShowKickoffBanner();
                break;

            case MomentKind.Red:
            {
                var head = FindHeadEvent(moment);
                if (head is not null)
                {
                    ShowRedBanner(head);
                }

                break;
            }

            case MomentKind.SevereInjury:
            {
                var head = FindHeadEvent(moment);
                if (head is not null)
                {
                    ShowInjuryBanner(head);
                }

                break;
            }

            case MomentKind.Mob:
            case MomentKind.RefereeLeaves:
                ShowMobBand();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// N3 desde el borde contrario al protagonista (docs/ui/README §4/§7, revisión del orquestador): si el
    /// suceso cae en la mitad izquierda del campo el estandarte sale por la derecha, y viceversa, para no
    /// tapar la portería donde ocurrió. El bando (N4, muerte) y el acta no se mueven — solo el estandarte.
    /// </summary>
    private void PositionBanner(Cell cell)
    {
        bool leftHalf = cell.Column < Pitch.Columns / 2f;
        _banner.Position = new Vector2(leftHalf ? CanvasWidth - HeraldBanner.DesignWidth : 0f, 150f);
    }

    private void ShowKickoffBanner()
    {
        _banner.Position = new Vector2(0f, 150f);
        _banner.Show(
            true,
            UiText.Get("ui.pregon.banner.said"),
            UiText.Get("ui.pregon.banner.kickoffTitle"),
            UiText.Get("ui.pregon.banner.kickoffBody", _playback.OwnName, _playback.RivalName),
            string.Empty);
    }

    private void ShowGoalBanner(MatchEvent goal)
    {
        bool ours = goal.Team == 0;
        string scorer = NameOf(goal.Actor);
        int minute = MatchLogView.Minute(goal.Tick, _catalog.Tuning.RegulationTicks);
        string team = ours ? _playback.OwnName : _playback.RivalName;
        PositionBanner(goal.Cell);
        _banner.Show(
            ours,
            UiText.Get("ui.pregon.banner.said"),
            UiText.Get("ui.pregon.banner.goalTitle"),
            UiText.Get("ui.pregon.banner.goalBody", scorer, minute),
            UiText.Get("ui.pregon.banner.goalFooter", team));

        // El tiro que trajo este gol puede seguir con su acercamiento activo (mantenimiento mínimo real,
        // UpdateShotGesture): se suelta aquí, al empezar el estandarte, con su salida normal — no tiene
        // sentido seguir acercando a un tiro que ya se ha convertido en el gol (revisión del orquestador,
        // 19 sep 2026). Sin efecto si ya se había soltado solo.
        _pitch3d.ReleasePunch();

        // Sacudida suave al empezar la presentación (docs/ui/README §4), SOLO a x1 — a x4/x16 la
        // velocidad degrada la presentación, nunca la información (principio 10).
        if (Speeds[_speedIndex] == 1)
        {
            _pitch3d.Shake(GoalShakeAmplitude, GoalShakeSeconds);
        }
    }

    private void ShowRedBanner(MatchEvent card)
    {
        bool ours = card.Team == 0;
        string name = NameOf(card.Actor);
        string team = ours ? _playback.OwnName : _playback.RivalName;
        int remaining = CountOnPitch(card.Team);
        PositionBanner(card.Cell);
        _banner.Show(
            ours,
            UiText.Get("ui.pregon.banner.said"),
            UiText.Get("ui.pregon.banner.redTitle"),
            UiText.Get("ui.pregon.banner.redBody", name, team),
            UiText.Get("ui.pregon.banner.redFooter", remaining));

        // Sacudida más corta que la del gol (docs/ui/README §4), SOLO a x1.
        if (Speeds[_speedIndex] == 1)
        {
            _pitch3d.Shake(CardOrInjuryShakeAmplitude, CardOrInjuryShakeSeconds);
        }
    }

    private void ShowInjuryBanner(MatchEvent injury)
    {
        bool ours = injury.Team == 0;
        string name = NameOf(injury.Actor);
        string position = UiText.Get("ui.pos." + PositionOf(injury.Actor));
        PositionBanner(injury.Cell);
        _banner.Show(
            ours,
            UiText.Get("ui.pregon.banner.said"),
            UiText.Get("ui.pregon.banner.injuryTitle"),
            UiText.Get("ui.pregon.banner.injuryBody", name, position),
            UiText.Get("ui.pregon.banner.injuryFooter"));

        // Sacudida más corta que la del gol (docs/ui/README §4), SOLO a x1.
        if (Speeds[_speedIndex] == 1)
        {
            _pitch3d.Shake(CardOrInjuryShakeAmplitude, CardOrInjuryShakeSeconds);
        }
    }

    private void ShowMobBand() =>
        _band.Show(UiText.Get("ui.pregon.turba.header"), UiText.Get("ui.pregon.turba.body"));

    /// <summary>
    /// Primer tiempo de la muerte (docs/ui/README §4): el campo la cuenta con un acercamiento hacia su
    /// casilla, con la reproducción ya congelada por el director; el bando (segundo tiempo) espera
    /// <see cref="DeathEdictDelaySeconds"/> reales, contados por <see cref="UpdateDeathEdict"/>. Si el
    /// momento también trae una decisión (<paramref name="moment"/>.Decision), la bandeja espera lo mismo
    /// —aparecen juntos, per §4— en vez de abrirse ya (el director la deja congelada de todas formas: el
    /// director no distingue "voz" de "decisión" para el reloj, este retardo es puro residuo visual).
    /// </summary>
    private void StartDeathTwoStage(MatchMoment moment, MatchEvent death)
    {
        var center = Pitch.CellCenter(death.Cell);
        _pitch3d.PunchIn(new Vector3(center.X, 0f, center.Y), DeathPunchZoom, DeathPunchInSeconds, DeathPunchHoldSeconds, DeathPunchOutSeconds);
        _pendingDeathEvent = death;
        _deathEdictDelay = DeathEdictDelaySeconds;
        _deathTrayPending = moment.Decision;
    }

    /// <summary>Cuenta atrás real del bando de una muerte pendiente; no hace nada si no hay ninguna.</summary>
    private void UpdateDeathEdict(float delta)
    {
        if (_pendingDeathEvent is null)
        {
            return;
        }

        _deathEdictDelay -= delta;
        if (_deathEdictDelay > 0f)
        {
            return;
        }

        var death = _pendingDeathEvent;
        _pendingDeathEvent = null;
        ShowDeathEdict(death);

        if (_deathTrayPending)
        {
            _deathTrayPending = false;
            OpenDecision();
        }
    }

    private void ShowDeathEdict(MatchEvent death)
    {
        string name = NameOf(death.Actor);
        string position = UiText.Get("ui.pos." + PositionOf(death.Actor));
        int minute = MatchLogView.Minute(death.Tick, _catalog.Tuning.RegulationTicks);
        string perk = PerkNameFromDetail(death.Detail);
        string body = perk.Length > 0
            ? UiText.Get("ui.pregon.edict.bodyWithCause", position, minute, perk)
            : UiText.Get("ui.pregon.edict.body", position, minute);
        _edict.Show(name, body);
    }

    private void ShowRecord(MatchMoment moment)
    {
        // moment.LastFrame, no el fotograma que se está mostrando (principio 5, revisión del
        // orquestador): la acta se presenta congelada en el anterior al suceso (C.2), un tick por detrás
        // de un gol de oro que termine el partido en el mismo instante — el marcador del acta tiene que
        // ser el de verdad, no el de un tick antes.
        int frame = Mathf.Clamp(moment.LastFrame, 0, _trace!.FrameCount - 1);
        var (own, rival) = ScoreAt(_trace.TickAt(frame));
        _record.Show(_playback.OwnName, _playback.RivalName, own, rival, UiText.Get("ui.pregon.record.hint"));
        _matchEnded = true;
    }

    // ------------------------------------------------------------------ residuo (traza + log)

    private void Sync()
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        _frame = Mathf.Clamp(_frame, 0, trace.FrameCount - 1);
        _pitch3d.Frame = _frame;
        _pitch3d.Alpha = _frozenLastFrame ? 0f : (float)Mathf.Clamp(_carry, 0d, 1d);
        _pitch3d.QueueRedraw();

        // Principio 5 (revisión visual del orquestador): mientras el director congela en FreezeFrame, el
        // suceso que se proclama YA ha dejado su residuo — tablero, residuo del rival, tiras leen
        // moment.LastFrame, no el fotograma (congelado, uno por detrás) que enseña el campo. Fuera del
        // congelado, el residuo es el del fotograma que se muestra, como siempre.
        int residueFrame = _residueMoment is not null
            ? Mathf.Clamp(_residueMoment.LastFrame, 0, trace.FrameCount - 1)
            : _frame;

        if (_frame == _synced && _residueMoment == _lastResidueMoment)
        {
            return;
        }

        _synced = _frame;
        _lastResidueMoment = _residueMoment;
        int tick = trace.TickAt(residueFrame);
        UpdateBoard(tick);
        UpdateStrips(tick, residueFrame);
    }

    private void UpdateBoard(int tick)
    {
        var (own, rival) = ScoreAt(tick);
        var (cards, casualties) = RivalResidueAt(tick);
        _board.SetTeams(_playback.OwnName, _playback.RivalName);
        _board.SetScore(own, rival);
        int regulation = Math.Max(1, _trace!.RegulationTicks);
        _board.SetProgress(Mathf.Clamp((float)tick / regulation, 0f, 1f));
        _board.SetRivalResidue(UiText.Get("ui.pregon.board.residue", cards, casualties));
        _board.SetSpeedIndex(_speedIndex);
        _board.SetPaused(_manualPaused);
    }

    private void UpdateStrips(int tick, int residueFrame)
    {
        var home = _playback.Setup.Home;
        var slots = home.Lineup.Slots;
        for (int i = 0; i < _strips.Count; i++)
        {
            if (i >= slots.Count)
            {
                _strips[i].Visible = false;
                continue;
            }

            var slot = slots[i];

            // El sustituto ocupa la casilla del titular que salió, no al revés (revisión visual del
            // orquestador: «la tira del que entra aparece»): la tira sigue la CASILLA, no el jugador que
            // la abrió, igual que en una retransmisión de verdad.
            int occupant = CurrentOccupant(slot.PlayerId, tick);
            var definition = FindDefinition(home, occupant);
            int traceIndex = FindTraceIndex(occupant);
            bool off = traceIndex < 0 || !_trace!.OnPitchAt(residueFrame, traceIndex);
            int number = traceIndex >= 0 ? _trace!.Players[traceIndex].Number : i + 1;
            var state = PhysicalStateAt(occupant, tick);
            string subtitle = definition is null
                ? string.Empty
                : UiText.Get("ui.pregon.strip.subtitle", UiText.Get("ui.pos." + definition.Position), _catalog.Race(definition.Race).Name.Es);
            string name = definition?.Name.ToUpperInvariant() ?? "?";
            int perkCount = definition?.Perks.Count ?? 0;
            _strips[i].SetModel(new StripModel(number, name, subtitle, perkCount, state, off));
        }

        _bench.SetCount(Math.Max(0, home.Players.Count - slots.Count));
    }

    private (int Own, int Rival) ScoreAt(int tick)
    {
        int own = 0;
        int rival = 0;
        var events = _playback.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Type != EventType.Goal || e.Tick > tick || IsCancelled(e))
            {
                continue;
            }

            if (e.Team == 0)
            {
                own++;
            }
            else if (e.Team == 1)
            {
                rival++;
            }
        }

        return (own, rival);
    }

    private (int Cards, int Casualties) RivalResidueAt(int tick)
    {
        int cards = 0;
        int casualties = 0;
        var events = _playback.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Team != 1 || e.Tick > tick || IsCancelled(e))
            {
                continue;
            }

            if (e.Type == EventType.Card)
            {
                cards++;
            }
            else if (e.Type == EventType.Death || (e.Type == EventType.Injury && e.Detail == "severe"))
            {
                casualties++;
            }
        }

        return (cards, casualties);
    }

    // ------------------------------------------------------------------ gestos derivados de eventos crudos

    /// <summary>
    /// Reconstruye la lista de ventanas de tiro (<see cref="BindPlayback"/>: al construir y tras cada
    /// sustitución, porque la reproducción cambia). El tiro no es un <see cref="MatchMoment"/> —N0, sin
    /// agrupador— así que se lee directamente de <c>Result.Events</c>, no del director.
    /// </summary>
    private void BuildShotGestures()
    {
        _shotGestures.Clear();
        _nextShotGestureIndex = 0;

        if (_trace is null)
        {
            return;
        }

        var events = _playback.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type == EventType.Shot)
            {
                _shotGestures.Add(new ShotGesture(_trace.FrameOfTick(events[i].Tick), events[i].Cell));
            }
        }
    }

    /// <summary>Recoloca el puntero de tiros pendientes al fotograma indicado: para <see cref="SeekTo"/> y tras una sustitución, igual que <c>PresentationDirector.Seek</c> hace con los momentos.</summary>
    private void ResyncShotGestures(int frame)
    {
        _nextShotGestureIndex = 0;
        while (_nextShotGestureIndex < _shotGestures.Count && _shotGestures[_nextShotGestureIndex].StartFrame < frame)
        {
            _nextShotGestureIndex++;
        }
    }

    /// <summary>
    /// Dispara el acercamiento de CUALQUIER tiro cuyo fotograma se alcance, con la misma duración mínima
    /// de mantenimiento real (<see cref="ShotPunchHoldSeconds"/>) siempre — <c>MatchPitchView3D.PunchIn</c>
    /// se suelta solo cuando pasa esa duración, o antes si <see cref="ShowGoalBanner"/> lo suelta a mano
    /// porque el tiro se convirtió en gol. <paramref name="gesturesAllowed"/> es SOLO a x1 (docs/ui/README
    /// §4): a otra velocidad el puntero sigue avanzando igual —para no acumular tiros atrasados que se
    /// disparasen todos de golpe al volver a x1— pero no se llama a <c>PunchIn</c>.
    /// </summary>
    private void UpdateShotGesture(bool gesturesAllowed)
    {
        while (_nextShotGestureIndex < _shotGestures.Count && _shotGestures[_nextShotGestureIndex].StartFrame <= _frame)
        {
            var shot = _shotGestures[_nextShotGestureIndex];
            _nextShotGestureIndex++;

            if (gesturesAllowed)
            {
                var center = Pitch.CellCenter(shot.Cell);
                _pitch3d.PunchIn(new Vector3(center.X, 0f, center.Y), ShotPunchZoom, ShotPunchInSeconds, ShotPunchHoldSeconds, ShotPunchOutSeconds);
            }
        }
    }

    /// <summary>
    /// Reconstruye la <b>capa de campo</b> del audio (<see cref="MatchEventSounds"/>) desde los eventos
    /// crudos, igual que <see cref="BuildShotGestures"/> y por el mismo motivo: el balón, la entrada y el
    /// hueso no son momentos —nadie los cuenta— así que no llegan por el director.
    ///
    /// <para>Un evento <b>anulado</b> no suena: si la jugada no cuenta, tampoco hizo ruido (mismo criterio
    /// que el marcador y las manchas de sangre, <see cref="IsCancelled"/>).</para>
    /// </summary>
    private void BuildEventSounds()
    {
        _eventSounds.Clear();
        _nextEventSoundIndex = 0;

        if (_trace is null)
        {
            return;
        }

        var events = _playback.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (IsCancelled(e))
            {
                continue;
            }

            var pools = MatchEventSounds.PoolsFor(e);
            if (pools.Length > 0)
            {
                _eventSounds.Add(new EventSound(_trace.FrameOfTick(e.Tick), pools));
            }
        }
    }

    /// <summary>Recoloca el puntero de la capa de campo, igual que <see cref="ResyncShotGestures"/>: tras un salto no se sueltan de golpe todos los sonidos que quedaron atrás.</summary>
    private void ResyncEventSounds(int frame)
    {
        _nextEventSoundIndex = 0;
        while (_nextEventSoundIndex < _eventSounds.Count && _eventSounds[_nextEventSoundIndex].StartFrame < frame)
        {
            _nextEventSoundIndex++;
        }
    }

    /// <summary>
    /// Suelta los sonidos de campo cuyo fotograma se haya alcanzado. <paramref name="allowed"/> es SOLO a
    /// ×1, misma regla que los gestos (docs/ui/README §4: «la velocidad degrada la presentación, nunca la
    /// información»): a ×16 esto serían dieciséis golpes por segundo, que no es un partido, es un
    /// zumbido. El puntero avanza igual —si no, al volver a ×1 sonaría de golpe todo lo saltado— y los
    /// momentos siguen sonando a cualquier velocidad, porque son lo que se está contando.
    /// </summary>
    private void UpdateEventSounds(bool allowed)
    {
        while (_nextEventSoundIndex < _eventSounds.Count && _eventSounds[_nextEventSoundIndex].StartFrame <= _frame)
        {
            var sound = _eventSounds[_nextEventSoundIndex];
            _nextEventSoundIndex++;

            if (!allowed)
            {
                continue;
            }

            foreach (string pool in sound.Pools)
            {
                AudioManager.Instance?.PlayRandomSfx(pool);
            }
        }
    }

    /// <summary>
    /// Reconstruye las manchas de sangre persistentes (RA-027) de <see cref="Sim.Events.EventType.Injury"/>
    /// no anulada y <see cref="Sim.Events.EventType.Death"/>, en la casilla del evento: leve pequeña, grave
    /// mediana, muerte grande. Se llama al construir y tras cada sustitución (la reproducción cambia).
    /// </summary>
    private void BuildBloodMarks()
    {
        var marks = new List<BloodMark>();
        if (_trace is not null)
        {
            var events = _playback.Result.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (IsCancelled(e))
                {
                    continue;
                }

                float size;
                if (e.Type == EventType.Death)
                {
                    size = BloodSizeDeath;
                }
                else if (e.Type == EventType.Injury && e.Detail == "severe")
                {
                    size = BloodSizeSevereInjury;
                }
                else if (e.Type == EventType.Injury && e.Detail == "minor")
                {
                    size = BloodSizeMinorInjury;
                }
                else
                {
                    continue;
                }

                var center = Pitch.CellCenter(e.Cell);
                marks.Add(new BloodMark(_trace.FrameOfTick(e.Tick), center.X, center.Y, size));
            }
        }

        _pitch3d.SetBloodMarks(marks);
    }

    private int CountOnPitch(int team)
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return 0;
        }

        int frame = Mathf.Clamp(_frame, 0, trace.FrameCount - 1);
        int count = 0;
        for (int i = 0; i < trace.Players.Count; i++)
        {
            if (trace.Players[i].Team == team && trace.OnPitchAt(frame, i))
            {
                count++;
            }
        }

        return count;
    }

    private int FindTraceIndex(int playerId)
    {
        if (_trace is null)
        {
            return -1;
        }

        for (int i = 0; i < _trace.Players.Count; i++)
        {
            if (_trace.Players[i].Id == playerId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Quién ocupa de verdad la casilla que abrió <paramref name="startingPlayerId"/>, en el tick
    /// indicado: sigue la cadena de sustituciones (<c>EventType.Substitution</c>, <c>Actor</c> = quien
    /// entra, <c>Target</c> = quien sale) hasta el sustituto más reciente con <c>Tick &lt;= tick</c>, o el
    /// propio titular si todavía no ha salido. El tope de vueltas es defensivo: no hay más sustitutos que
    /// suplentes en la plantilla.
    /// </summary>
    private int CurrentOccupant(int startingPlayerId, int tick)
    {
        int occupant = startingPlayerId;
        var events = _playback.Result.Events;
        for (int guard = 0; guard < 5; guard++)
        {
            int next = -1;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type == EventType.Substitution && e.Target == occupant && e.Tick <= tick && !IsCancelled(e))
                {
                    next = e.Actor;
                    break;
                }
            }

            if (next < 0)
            {
                break;
            }

            occupant = next;
        }

        return occupant;
    }

    /// <summary>
    /// Estado físico del jugador en el tick indicado, leído de los eventos hasta ahí (no calcula nada del
    /// partido: solo mira lo que ya pasó, igual que <c>MatchScreen.SyncLog</c> revela líneas por tick).
    /// </summary>
    private PhysicalState PhysicalStateAt(int playerId, int tick)
    {
        var state = PhysicalState.Healthy;
        var events = _playback.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Actor != playerId || e.Tick > tick || IsCancelled(e))
            {
                continue;
            }

            if (e.Type == EventType.Death)
            {
                state = PhysicalState.Dead;
            }
            else if (e.Type == EventType.Injury && e.Detail == "severe" && state != PhysicalState.Dead)
            {
                state = PhysicalState.SevereInjury;
            }
            else if (e.Type == EventType.Injury && e.Detail == "minor" && state == PhysicalState.Healthy)
            {
                state = PhysicalState.MinorInjury;
            }
        }

        return state;
    }

    private static bool IsCancelled(MatchEvent e) => e.Detail.EndsWith(":cancelled", StringComparison.Ordinal);

    private PlayerDefinition? FindDefinition(TeamSetup side, int playerId)
    {
        var players = side.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == playerId)
            {
                return players[i];
            }
        }

        return null;
    }

    private string NameOf(int playerId) =>
        FindDefinition(_playback.Setup.Home, playerId)?.Name
        ?? FindDefinition(_playback.Setup.Away, playerId)?.Name
        ?? playerId.ToString(CultureInfo.InvariantCulture);

    private Sim.Model.Position PositionOf(int playerId) =>
        FindDefinition(_playback.Setup.Home, playerId)?.Position
        ?? FindDefinition(_playback.Setup.Away, playerId)?.Position
        ?? Sim.Model.Position.Midfielder;

    /// <summary>
    /// El evento que encabeza el momento (regla 3 de <c>MatchMomentView</c>: el de mayor nivel, el último
    /// en empate). Se busca desde el final porque, con fusión, un evento posterior de nivel menor puede
    /// quedar último en <c>EventIndices</c> sin ser el que puso <c>Kind</c>/<c>LeadPlayerId</c>.
    /// </summary>
    private MatchEvent? FindHeadEvent(MatchMoment moment)
    {
        var events = _playback.Result.Events;
        var indices = moment.EventIndices;
        for (int i = indices.Count - 1; i >= 0; i--)
        {
            var e = events[indices[i]];
            if (e.Team == moment.Team && e.Actor == moment.LeadPlayerId && MapsToKind(e.Type, moment.Kind))
            {
                return e;
            }
        }

        return indices.Count > 0 ? events[indices[^1]] : null;
    }

    /// <summary>El gol no anulado del momento (<see cref="MatchMoment.HasGoal"/>), aunque no sea quien lo encabece.</summary>
    private MatchEvent? FindGoalEvent(MatchMoment moment)
    {
        var events = _playback.Result.Events;
        var indices = moment.EventIndices;
        for (int i = 0; i < indices.Count; i++)
        {
            var e = events[indices[i]];
            if (e.Type == EventType.Goal && !IsCancelled(e))
            {
                return e;
            }
        }

        return null;
    }

    private static bool MapsToKind(EventType type, MomentKind kind) => kind switch
    {
        MomentKind.Kickoff => type == EventType.MatchStart,
        MomentKind.Foul => type == EventType.Foul,
        MomentKind.Consumable => type == EventType.ConsumableUsed,
        MomentKind.Substitution => type == EventType.Substitution,
        MomentKind.Yellow or MomentKind.Red => type == EventType.Card,
        MomentKind.MinorInjury or MomentKind.SevereInjury => type == EventType.Injury,
        MomentKind.Goal => type == EventType.Goal,
        MomentKind.Mob => type == EventType.MobStart,
        MomentKind.RefereeLeaves => type == EventType.RefereeLeaves,
        MomentKind.Death => type == EventType.Death,
        MomentKind.FullTime => type == EventType.MatchEnd,
        _ => false,
    };

    /// <summary>El nombre del perk causante si <paramref name="detail"/> viene como <c>perk:&lt;id&gt;</c> (RF-013); vacío si no.</summary>
    private string PerkNameFromDetail(string detail)
    {
        int separator = detail.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0 || detail[..separator] != "perk")
        {
            return string.Empty;
        }

        string id = detail[(separator + 1)..];
        return _catalog.Perks.Find(id)?.Name.Es ?? id;
    }

    private void OnSpeedChosen(int index)
    {
        _speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
        _board.SetSpeedIndex(_speedIndex);

        // Un cambio de velocidad cancela los gestos en curso (docs/ui/README §4): solo el adorno de
        // cámara — el bando/la bandeja de una muerte pendiente siguen su reloj real igual, son
        // información, no gesto (principio 10).
        _pitch3d.ResetGestures();
    }

    private void OnPauseToggled()
    {
        _manualPaused = !_manualPaused;
        _board.SetPaused(_manualPaused);
    }

    private void GoToReport() => Nav.Go(this, Nav.Report);

    // ------------------------------------------------------------------ API pública (capturas, F3)

    /// <summary>Momentos del partido en reproducción; usado por <see cref="BroadcastCapture"/> para encontrar uno de cada tipo.</summary>
    public MatchMoments Moments => _moments;

    /// <summary>Traza del partido en reproducción, o null si todavía no hay ninguna.</summary>
    public MatchTrace? Trace => _trace;

    /// <summary>
    /// El campo 3D, público solo para el arnés de capturas (<see cref="BroadcastCapture"/>): las capturas
    /// de gesto avanzan el reloj real a mano, llamando a <c>_Process</c> con deltas fijos en vez de
    /// esperar fotogramas del motor (deterministas, docs/ui/README §4) — y como <c>_Process</c> de este
    /// campo es un nodo hijo aparte, el motor lo seguiría llamando por su cuenta con un delta real sin
    /// control mientras se espera a que se dibuje, así que también hay que congelarlo y avanzarlo a mano.
    /// </summary>
    public MatchPitchView3D Pitch3D => _pitch3d;

    /// <summary>
    /// Lleva la pantalla a ese fotograma y deja que el director lo presente: usado por
    /// <see cref="BroadcastCapture"/> para llegar a un momento concreto sin depender de dejar correr el
    /// reloj. <c>Seek</c> del director descarta lo pendiente anterior y <c>Advance</c> con el mismo
    /// fotograma dispara la presentación de lo que empiece justo ahí.
    /// </summary>
    public void SeekTo(int frame)
    {
        if (_trace is not { FrameCount: > 0 } trace)
        {
            return;
        }

        _frame = Mathf.Clamp(frame, 0, trace.FrameCount - 1);
        _carry = 0d;
        _matchEnded = false;
        _lastStampMoment = null;
        _lastVoiceMoment = null;

        // Con _lastVoiceMoment a null, un fotograma sin voz no se distingue de «sin cambios» en
        // ApplyPresentation: lo que quedara a la vista de antes del salto se oculta aquí.
        _banner.Visible = false;
        _band.Visible = false;
        _edict.Visible = false;
        _record.Visible = false;
        _stamp.Visible = false;

        // Una captura anterior en el mismo proceso puede haber dejado la bandeja abierta (otro momento de
        // decisión) o las tiras ocultas: se repone el estado de juego corriente antes de sembrar el nuevo.
        _pendingPoint = null;
        _tray.Visible = false;
        foreach (var strip in _strips)
        {
            strip.Visible = true;
        }

        _bench.Visible = true;

        // Gestos (docs/ui/README §4): un SeekTo cancela cualquiera en curso y resincroniza el puntero de
        // tiros con el nuevo fotograma, igual que el director descarta lo pendiente en su propio Seek.
        _pitch3d.ResetGestures();
        ResyncShotGestures(_frame);
        ResyncEventSounds(_frame);
        _pendingDeathEvent = null;
        _deathTrayPending = false;
        _deathEdictDelay = 0f;

        _director.Seek(_frame);

        var result = _director.Advance(_frame, 0d, Speeds[_speedIndex]);
        _frozenLastFrame = result.Frozen;
        _frame = Mathf.Clamp(result.DisplayFrame, 0, trace.FrameCount - 1);
        _residueMoment = result.Frozen ? result.Voice : null;
        ApplyPresentation(result);
        if (result.AwaitingDecision && !_deathTrayPending)
        {
            OpenDecision();
        }

        _synced = -1;
        Sync();
    }

    /// <summary>
    /// Solo para <see cref="BroadcastCapture"/> (revisión del orquestador, 19 sep 2026): elige el
    /// candidato recomendado de la bandeja abierta, como si se hubiera pulsado «Confirmar», para poder
    /// capturar la reproducción justo después de una decisión de verdad. False si no hay ninguna decisión
    /// pendiente.
    /// </summary>
    public bool ChooseRecommendedForCapture()
    {
        if (_pendingPoint is null)
        {
            return false;
        }

        var outPlayer = FindDefinition(_playback.Setup.Home, _pendingPoint.OutPlayerId);
        if (outPlayer is null)
        {
            return false;
        }

        var recommended = SubstitutionPolicy.Default(_pendingPoint, outPlayer);
        OnSubstituteChosen(recommended.Id);
        return true;
    }
}
