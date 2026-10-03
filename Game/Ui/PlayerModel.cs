using System;
using Godot;
using Underleague.Sim.Engine;

namespace Underleague.Game.Ui;

/// <summary>
/// <b>Maqueta</b> (23 sep 2026): un modelo humanoide animado en el sitio de la cápsula, solo para los
/// humanos, para ver qué cambia. No es el sistema de personajes del juego y no pretende serlo — la regla 10
/// del proyecto dice que no se produce arte hasta cerrar el diseño de la fase 2, así que esto es material
/// provisional puesto para <b>decidir con una imagen delante</b>, no para quedarse.
///
/// <para><b>Material</b>: personaje y animaciones de Mixamo (<c>Game/models/soccer/</c>) — el «Soccer Game
/// Pack», que es de fútbol de verdad: correr, chutar, remate de cabeza, entrada, trompicón, portero. La
/// primera maqueta usó una biblioteca genérica de aventura (Quaternius, CC0, en <c>Game/models/</c>) y se
/// veía lo que era: los jugadores andaban <i>encorvados</i> porque su única animación de andar era «andar
/// cargando algo». Ese pack se conserva sin usar porque trae un juego <c>Zombie_*</c> completo que le vendrá
/// bien a los no-muertos.</para>
///
/// <para><b>Cuelga de la cápsula, no la sustituye.</b> El nodo se añade como hijo del mismo
/// <c>MeshInstance3D</c> que ya movía <c>ApplyTrace</c>, así que hereda posición sin que nada del resto de
/// la vista —anillo, dorsal, sangre, cortinilla de teletransporte, cámara— tenga que enterarse. A la
/// cápsula se le quita la malla y se queda como el <i>hueso</i> que la transforma.</para>
///
/// <para><b>Por qué los clips se aplican sin reorientar nada</b> (medido con la sonda
/// <c>Scenes/SondaModelo.tscn</c>, no supuesto): el personaje y los 15 clips vienen de Mixamo, tienen los
/// <b>mismos 65 huesos</b> (<c>mixamorig_*</c>) y las pistas apuntan a <c>Skeleton3D:mixamorig_…</c>, que es
/// exactamente la jerarquía del personaje. Se cargan una vez en una
/// <see cref="AnimationLibrary"/> compartida y cada jugador la monta en su reproductor. Si los esqueletos
/// no coincidieran haría falta reescribir las rutas o reorientar con <c>SkeletonProfileHumanoid</c>.</para>
///
/// <para><b>Un mensaje de cierre que NO es un fallo</b>: al salir, Godot avisa de «3 resources still in use
/// at exit». Son el personaje, la biblioteca y su animación, que viven en cachés <c>static</c> a propósito
/// —se cargan una vez para los catorce jugadores— y por tanto aún están referenciadas cuando el proceso
/// termina. Queda anotado aquí para que nadie lo persiga: no hay fuga en tiempo de juego.</para>
///
/// <para><b>Lo que sigue faltando</b>: no hay <b>celebración</b> en el pack (se queda en la espera) y la
/// <b>estirada del portero</b> no está enganchada porque depende de un evento —una parada— y no del estado
/// del jugador, que es lo único que esta clase mira.</para>
/// </summary>
/// <summary>
/// El gesto que toca este fotograma porque el jugador está tocando el balón, cuando el
/// <see cref="PlayerState"/> no basta para saberlo (BI-H). <c>/Sim</c> no tiene estados de recibir, rematar
/// ni parar —el contacto vive en los <b>eventos</b>, no en el estado—, así que lo deduce la vista de lo
/// que ya lee de la traza y se lo pasa al modelo. <see cref="None"/> es «nada especial, decide por estado».
/// </summary>
public enum ContactCue
{
    None,
    Receive,
    Header,
    ThrowIn,
    Save,
    Catch,
    Penalty,
}

/// <summary>
/// La parte del cuerpo con la que se está jugando el balón, para anclar el <b>dibujo</b> de la pelota
/// (BI-H). No es física ni geometría exacta: es el punto del esqueleto animado del que debe parecer que
/// sale el balón.
/// </summary>
public enum ContactPart
{
    Feet,
    Head,
    Hands,
    Chest,
}

/// <summary>
/// El golpeo que viene (BV-A, H7): en qué tick suelta el balón este jugador según la traza ya calculada
/// (<see cref="Release"/>, −1 si no lo suelta pronto) y cuántos segundos de partido faltan para ese instante.
/// </summary>
public readonly record struct KickCue(int Release, float SecondsToContact)
{
    public static KickCue None { get; } = new(-1, -1f);
}

public sealed partial class PlayerModel : Node3D
{
    /// <summary>La carpeta del material de fútbol: personaje y clips, todos del mismo esqueleto.</summary>
    private const string Folder = "res://models/soccer";

    /// <summary>El personaje con malla. Los clips vienen en ficheros aparte, sin malla.</summary>
    private const string CharacterPath = Folder + "/X Bot.fbx";

    /// <summary>Nombre de la biblioteca que se monta en cada reproductor; prefija a todas las claves.</summary>
    private const string Library = "soccer";

    /// <summary>
    /// El hueso raíz del esqueleto de Mixamo, el que lleva el desplazamiento del clip. Es el que se fija
    /// para dejar la animación <b>en el sitio</b> (ver <see cref="PinInPlace"/>). Con otro pack cambia.
    /// </summary>
    private const string RootBone = ":mixamorig_Hips";

    /// <summary>
    /// De qué hueso sale el balón según con qué se esté jugando (BI-H). Los nombres están <b>verificados
    /// en el propio FBX</b> —el personaje trae los 65 huesos <c>mixamorig:*</c> y el importador de Godot
    /// sustituye el <c>:</c> por <c>_</c>—, no supuestos. Cada parte lleva dos huesos, izquierdo y derecho,
    /// y se elige el que esté <b>más cerca del balón</b>: es lo único que hace falta para que parezca que
    /// golpea con la pierna que toca, y cuesta una comparación de distancias en vez de un sistema de IK.
    /// Con otro pack de animación cambia esta tabla y nada más.
    /// </summary>
    private static readonly (ContactPart Part, string Left, string Right)[] ContactBones =
    {
        (ContactPart.Feet, "mixamorig_LeftToeBase", "mixamorig_RightToeBase"),
        (ContactPart.Head, "mixamorig_Head", "mixamorig_Head"),
        (ContactPart.Hands, "mixamorig_LeftHand", "mixamorig_RightHand"),
        (ContactPart.Chest, "mixamorig_Spine2", "mixamorig_Spine2"),
    };

    /// <summary>
    /// Los clips, con la clave por la que los pide el código, el fichero del que salen y si van en bucle.
    /// <b>La clave no es el nombre del fichero a propósito</b>: cambiar de pack no debe tocar
    /// <see cref="Pose"/>, solo esta tabla. Todos los ficheros de Mixamo llaman a su animación
    /// <c>mixamo_com</c>, así que el nombre de dentro no sirve para nada.
    /// </summary>
    private static readonly (string Key, string File, bool Loop)[] Clips =
    {
        ("idle", "offensive idle", true),
        ("jog", "jog forward", true),
        ("run", "Running", true),
        ("kick", "kick soccerball", false),
        ("header", "header soccerball", false),
        ("tackle", "soccer tackle", false),
        ("trip", "soccer trip", false),
        ("fallen", "fallen idle", true),
        ("standup", "standing up", false),
        ("receive", "receive soccerball", false),
        ("throwin", "throw in", false),
        ("penalty", "soccer penalty kick", false),
        ("gk_idle", "goalkeeper idle", true),
        ("gk_save", "goalkeeper diving save", false),
        ("gk_catch", "goalkeeper catch", false),
    };

    /// <summary>
    /// Fundido entre posturas. Sin él, pasar de correr a chutar es un corte seco —la pose salta en un
    /// fotograma— y a veinte jugadores a la vez se nota como un tirón. 0,15 s es el rango que la práctica
    /// da por bueno para algo que tiene que seguir leyéndose como respuesta inmediata (0,1-0,2 s).
    /// </summary>
    private const float BlendSeconds = 0.15f;

    /// <summary>
    /// Constante de tiempo del filtro de la mezcla de locomoción, en segundos de partido (BV-A). La
    /// velocidad que llega ya viene promediada sobre una ventana de la traza; esto sólo quita el escalón que
    /// queda al cambiar de ventana. <b>Provisional, sin medir</b>: del orden del intervalo de decisión de
    /// <c>/Sim</c> (<c>decisionIntervalTicks: 2</c> = 0,13 s), para que un cambio de destino no se vea antes de
    /// que el motor lo haya sostenido.
    /// </summary>
    private const float LocomotionSmoothingSeconds = 0.12f;

    /// <summary>
    /// Velocidad máxima de giro del cuerpo, en grados por segundo de partido (BV-A, H2). <b>Provisional, sin
    /// medir</b>: media vuelta en 0,25 s, el orden de un futbolista que se da la vuelta corriendo. Antes no había
    /// límite: el modelo saltaba hasta 180° entre dos fotogramas.
    /// </summary>
    private const float TurnDegreesPerSecond = 720f;

    /// <summary>
    /// Segundo del clip <c>kick</c> en que el pie golpea (BV-A, H7). <b>Medido</b> con
    /// <see cref="DebugFootProfile"/>: la puntera izquierda barre hacia delante de 0,05 a 0,30 s con el pico de
    /// velocidad a 0,20–0,23 s y a la altura del balón; después vuelve. La vista lanza el golpeo para que este
    /// instante caiga en el tick en que la traza suelta el balón.
    /// </summary>
    public const float KickContactSeconds = 0.20f;

    /// <summary>
    /// El tramo útil del clip <c>receive</c> (5,13 s, casi todo pasitos con el balón): de 0,85 s a 1,5 s, donde
    /// el perfil de los pies (<see cref="DebugFootProfile"/>) enseña el pie levantado a ~0,3 m y lento, que es el
    /// control. <b>Medido</b> el sitio; la longitud es <b>provisional</b>: lo justo para leerse como «la
    /// controla» sin que el cuerpo se deslice con un gesto parado (antes, 5,2 s y el 77 % deslizándose).
    /// </summary>
    private const float ReceiveStartSeconds = 0.85f;
    private const float ReceiveLengthSeconds = 0.65f;

    /// <summary>
    /// Velocidad del cuerpo, en casillas por segundo de partido, por encima de la cual un gesto en el sitio se
    /// suelta. <b>Provisional</b>: es el mismo corte con que el instrumento de BV-A cuenta un gesto «deslizándose».
    /// </summary>
    private const float GestureReleaseCellsPerSecond = 1.0f;

    /// <summary>
    /// Lo mínimo que se adelanta el golpeo al contacto cuando el cuerpo llega corriendo (BV-A, carrerilla). Parado
    /// se lanza entero, <see cref="KickContactSeconds"/> antes; a la velocidad de la carrera, sólo esto: el pie de
    /// apoyo sigue la zancada hasta casi el contacto en vez de clavarse mientras <c>/Sim</c> sigue moviendo el
    /// cuerpo (medido antes: el golpeo se deslizaba el 35-56 % de sus fotogramas). <b>Provisional, sin medir</b>:
    /// algo más de un tick (1/15 s).
    /// </summary>
    private const float KickMinLeadSeconds = 0.08f;

    /// <summary>
    /// Cuánto rueda el balón por delante del pie entre dos toques al conducir, en casillas (BV-A, punto 4). Se toca una
    /// vez por ciclo de zancada (dos pasos), con la puntera izquierda adelantada. <b>Provisional, sin medir</b>: ~20 cm,
    /// conducción pegada; una conducción larga lo pondría más lejos.
    /// </summary>
    private const float DribbleRollCells = 0.10f;

    /// <summary>
    /// Velocidad del cuerpo (casillas/s) por debajo de la cual el jugador se vuelve a mirar el balón, y por encima de la
    /// cual vuelve a mirar hacia donde corre (histéresis). Corriendo se mira adelante; parado o casi, al juego (BV-A,
    /// H9). <b>Provisionales, sin medir</b>: entre el trote (1,3 c/s medido) y la carrera (2,19 c/s medido).
    /// </summary>
    private const float FaceBallEnterCellsPerSecond = 0.9f;
    private const float FaceBallExitCellsPerSecond = 1.3f;

    /// <summary>Lo mismo para el que va a recibir un pase en vuelo: espera mirando al pasador aunque se acomode trotando. Provisional.</summary>
    private const float FaceBallReceiverExitCellsPerSecond = 1.9f;

    /// <summary>Por debajo de esto (c/s) se considera parado y mira al balón aunque se mueva de lado. Provisional: medio trote.</summary>
    private const float StandStillCellsPerSecond = 0.35f;

    /// <summary>Coseno del ángulo entre la marcha y el balón por debajo del cual el paso es lateral (70°–110°): ahí no hay clip creíble y mira a donde va. Provisional.</summary>
    private const float LateralCos = 0.34f;

    /// <summary>A menos de esto (casillas) el balón está encima y su dirección no dice nada. Provisional.</summary>
    private const float BallLookMinCells = 0.3f;

    /// <summary>
    /// Remate del golpeo que se deja ver tras el contacto antes de poder soltarlo. <b>Medido</b> (BV-A, segunda pasada):
    /// el pasador está QUIETO durante <c>Passing</c> y <c>/Sim</c> lo echa a correr en el mismo tick en que sale el balón,
    /// así que el patinaje del golpeo no estaba en la carrerilla sino en el remate (0,20-0,30 s del clip, cuerpo a 2,2 c/s).
    /// Si arranca, el golpeo se suelta en el contacto.
    /// </summary>
    private const float KickFollowThroughSeconds = 0f;

    /// <summary>Fundido de salida de un gesto que se suelta porque el cuerpo echa a correr. <b>Provisional, sin medir</b>: algo más de un tick.</summary>
    private const float GestureRunOutSeconds = 0.08f;

    private float _outFade = BlendSeconds;

    /// <summary>Lo mínimo que se deja ver la recepción antes de poder soltarla. <b>Provisional, sin medir</b> (~4 ticks).</summary>
    private const float ReceiveMinSeconds = 0.25f;

    /// <summary>
    /// Los gestos que no son locomoción, montados como entradas de una transición del árbol (BV-A). Los
    /// clips en bucle (<c>fallen</c>) no terminan; el resto se acaba con su propia duración.
    /// </summary>
    private static readonly string[] GestureKeys =
    {
        "kick", "header", "tackle", "trip", "fallen", "receive", "throwin", "penalty", "gk_save", "gk_catch",
    };

    /// <summary>
    /// Corrección de orientación, en radianes. glTF y FBX dan el frente en <b>+Z</b> y Godot lo considera
    /// <b>−Z</b>; con este personaje 0 es lo correcto. <b>Si los jugadores corren de espaldas, esta es la
    /// palanca</b> (poner <c>Mathf.Pi</c>).
    /// </summary>
    private const float FacingOffset = 0f;

    private static PackedScene? _character;
    private static AnimationLibrary? _library;
    private static bool _missing;
    private static bool _announced;

    private AnimationPlayer? _anim;
    private Skeleton3D? _skeleton;
    private Node3D? _instance;

    /// <summary>
    /// Fase del ciclo de locomoción, en ciclos [0, 1): 0 es la puntera izquierda en su punto más adelantado, en el
    /// trote y en la carrera por igual (BV-A, punto 2). Avanza con el desplazamiento DIBUJADO del cuerpo dividido por
    /// la zancada de un ciclo, así que el pie de apoyo no patina por construcción; hacia atrás, retrocede.
    /// </summary>
    private float _phase;
    private Vector3 _lastGlobal;
    private bool _hasLast;
    private bool _faceBall;
    private float _gestureFade = BlendSeconds;
    private float _debugFeetScale;
    private float _debugFeetNatural;

    /// <summary>
    /// Medidas del ciclo hechas una vez sobre los propios clips (no supuestas): en qué segundo de cada clip va la
    /// puntera izquierda más adelantada, y dónde quedan las punteras —en el espacio del personaje importado— en la
    /// zancada, en la espera y en el contacto del golpeo. De ahí salen la alineación trote/carrera y el balón al pie.
    /// </summary>
    private static bool _cyclesMeasured;
    private static float _jogOffset, _runOffset;
    private static Vector3 _reachRun, _reachIdle, _reachKick;

    /// <summary>
    /// Índices de hueso resueltos una vez por modelo. <see cref="Skeleton3D.FindBone"/> recorre los 65
    /// huesos por nombre, y esto se consultaría catorce veces por fotograma.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<ContactPart, (int Left, int Right)> _contactBones = new();
    private bool _keeper;

    /// <summary>
    /// El árbol que mezcla la locomoción con los gestos (BV-A). Sustituye a tocar el
    /// <see cref="AnimationPlayer"/> con <c>Play</c>, que sólo sabe ir de un clip a otro y arrancaba el
    /// nuevo desde 0: con la velocidad cruzando los umbrales cada tick, el muñeco reiniciaba la zancada
    /// dos veces por segundo.
    /// </summary>
    private AnimationTree? _tree;

    /// <summary>Posición de la mezcla de locomoción ahora mismo: 0 quieto, <see cref="_jogBlend"/> trote, 1 carrera.</summary>
    private float _blend;

    /// <summary>Dónde cae el trote en la mezcla: zancada natural del <c>jog</c> / la del <c>run</c>, medida en los clips.</summary>
    private static float _jogBlend = 0.5f;

    /// <summary>Zancada natural de la carrera de ESTE modelo, en casillas por segundo de partido; 0 hasta medirla.</summary>
    private float _runCells;

    /// <summary>
    /// El gesto que suena por encima de la locomoción, o vacío. Su reloj es el de la reproducción, no el de
    /// un estado: un gesto nacido de un evento dura un tick y sin esto el siguiente fotograma lo cortaba.
    /// </summary>
    private string _gesture = string.Empty;
    private float _gestureTime;
    private float _gestureLength;

    /// <summary>
    /// Si el gesto termina entero aunque el estado que lo pidió ya se haya ido (los de evento: recepción,
    /// cabezazo, parada, saques, el suelo), o vive mientras dure su estado (entrada, golpeo).
    /// </summary>
    private bool _gestureHeld;

    /// <summary>Peso del gesto en la mezcla final, 0..1; sube y baja en <see cref="BlendSeconds"/>.</summary>
    private float _gestureWeight;

    /// <summary>
    /// Un modelo escalado a esa altura de cuerpo, o <c>null</c> si el material no está (la vista sigue con
    /// su cápsula y no se rompe nada: la misma tolerancia que el audio tiene con los pools vacíos).
    /// </summary>
    /// <param name="bodyHeight">Altura de la cápsula que sustituye, en casillas (1 casilla = 1 unidad).</param>
    /// <param name="keeper">Si es el portero: espera en postura de portero en vez de la de campo.</param>
    public static PlayerModel? TryCreate(float bodyHeight, bool keeper)
    {
        if (_missing)
        {
            return null;
        }

        if (_character is null)
        {
            _character = ResourceLoader.Load<PackedScene>(CharacterPath);
            if (_character is null)
            {
                _missing = true;
                GD.Print($"[modelos] no está {CharacterPath}: los humanos siguen siendo cápsulas");
                return null;
            }

            _library = BuildLibrary();
        }

        var instance = _character.Instantiate<Node3D>();
        var model = new PlayerModel { Name = "Model", _keeper = keeper };
        model.AddChild(instance);

        // La altura real del personaje se mide, no se supone: si mañana se cambia de modelo, el tamaño en
        // el campo sigue siendo el que manda la raza y no hay una constante mágica que corregir.
        float natural = MeasureHeight(instance);
        float scale = natural > 0.01f ? bodyHeight / natural : 1f;
        instance.Scale = new Vector3(scale, scale, scale);

        // El personaje tiene el origen en los pies y la cápsula está centrada en su mitad.
        model.Position = new Vector3(0f, -bodyHeight / 2f, 0f);

        model._instance = instance;
        model._anim = FindAnimationPlayer(instance);
        model._skeleton = FindSkeleton(instance);
        model.ResolveContactBones();
        if (model._anim is not null && _library is not null && !model._anim.HasAnimationLibrary(Library))
        {
            model._anim.AddAnimationLibrary(Library, _library);
        }

        if (!_announced)
        {
            _announced = true;
            GD.Print($"[modelos] {CharacterPath}: alto natural {natural:0.###}, "
                + (model._anim is null
                    ? "SIN AnimationPlayer (se queda en su pose de reposo)"
                    : $"{model._anim.GetAnimationList().Length} animaciones montadas"));
        }

        // Arranca en su espera ya, sin esperar al primer Pose(): si no, el personaje se queda en la pose de
        // reposo del fichero —los brazos en cruz de la T— y cualquier captura de una pantalla pausada sale
        // con un espantapájaros. Costó una ronda de capturas averiguarlo con el pack anterior.
        model.BuildTree();
        return model;
    }

    /// <summary>
    /// Pinta el modelo del color del equipo. Se hace con <c>MaterialOverride</c> sobre las mallas: lo que
    /// tiene que leerse de un vistazo es de qué bando es cada uno (RA-002).
    /// </summary>
    public void Paint(Material material) => Paint(this, material);

    /// <summary>
    /// La postura de este fotograma. <paramref name="velocity"/> es la velocidad de PRESENTACIÓN en casillas
    /// por segundo de partido —la vista la promedia sobre una ventana de la traza, no es el paso de un tick—,
    /// <paramref name="state"/> el estado que la simulación publica (<c>MatchTrace.StateAt</c>) y
    /// <paramref name="delta"/> los segundos reales de este fotograma (0 = colocar sin animar, para las
    /// capturas fijas). <paramref name="rate"/> es a cuántos ticks por tick real avanza la reproducción que
    /// se está viendo: 1 a x1, 4 a x4, 0,5 en la cámara lenta y 0 congelada o en pausa. Todos los relojes
    /// del muñeco —zancada, gestos, fundidos— van con él: congelada la imagen, congelado el muñeco (H6).
    /// <b>El modelo no decide nada</b>: sólo mira lo que está escrito (RT-014).
    /// </summary>
    public void Pose(Vector2 velocity, Vector2 facing, Vector2 toBall, bool receiving, PlayerState state, ContactCue cue, KickCue kick, float rate, float delta)
    {
        if (_tree is null)
        {
            return;
        }

        MeasureStride();
        MeasureCycles();
        float speed = velocity.Length();

        // Segundos de PARTIDO que han pasado en este fotograma: lo que mueve todos los relojes del muñeco.
        float simDelta = delta * rate;
        bool down = state is PlayerState.KnockedDown or PlayerState.Injured;
        Turn(ChooseFacing(velocity, facing, toBall, receiving, down), simDelta, snap: delta <= 0f);
        DebugInputSpeed = speed;
        AdvanceGesture(simDelta);

        // Un gesto en el sitio no puede llevarse al cuerpo deslizándose (H7): el golpeo, pasado el contacto y
        // un poco de remate, y la recepción, pasado su primer cuarto de segundo, se funden en cuanto el cuerpo
        // echa a correr. Medido antes: el golpeo retenido hasta el final se deslizaba el 37-77 % de su tiempo.
        if (speed > GestureReleaseCellsPerSecond
            && ((_gesture == "kick" && _gestureTime > KickContactSeconds + KickFollowThroughSeconds)
                || (_gesture == "receive" && _gestureTime > ReceiveMinSeconds)))
        {
            StopGesture();
            _outFade = GestureRunOutSeconds;
        }
        ChooseGesture(state, cue, kick, speed);

        // Locomoción continua: una posición de mezcla idle→trote→carrera en lugar de tres clips con umbral.
        float target = _runCells > 0.01f ? Mathf.Clamp(speed / _runCells, 0f, 1f) : 0f;
        _blend = delta <= 0f ? target : Mathf.Lerp(_blend, target, 1f - Mathf.Exp(-simDelta / LocomotionSmoothingSeconds));

        // Pesos: espera→marcha hasta el punto del trote, y trote→carrera por encima (la misma mezcla lineal que hacía
        // el BlendSpace1D, pero con trote y carrera en la MISMA fase, para que la mezcla no cruce dos zancadas).
        float move = _jogBlend > 0f ? Mathf.Clamp(_blend / _jogBlend, 0f, 1f) : 1f;
        float jogToRun = _blend <= _jogBlend ? 0f : Mathf.Clamp((_blend - _jogBlend) / (1f - _jogBlend), 0f, 1f);
        AdvancePhase(jogToRun, delta);
        _tree.Set("parameters/loco/blend_amount", move);
        _tree.Set("parameters/jr/blend_amount", jogToRun);
        _tree.Set("parameters/jog_seek/seek_request", ClipTimeAtPhase("jog", _jogOffset));
        _tree.Set("parameters/run_seek/seek_request", ClipTimeAtPhase("run", _runOffset));
        _tree.Set("parameters/idle_scale/scale", rate);
        _tree.Set("parameters/gesture_scale/scale", rate);

        float weightTarget = _gesture.Length > 0 ? 1f : 0f;
        float fade = weightTarget > _gestureWeight ? _gestureFade : _outFade;
        _gestureWeight = delta <= 0f ? weightTarget : Mathf.MoveToward(_gestureWeight, weightTarget, simDelta / Mathf.Max(0.01f, fade));
        _tree.Set("parameters/mix/blend_amount", _gestureWeight);
    }

    /// <summary>
    /// Hacia dónde debe mirar (BV-A, punto 3). Corriendo, hacia donde va (<paramref name="facing"/>, que la vista
    /// saca mirando adelante en la traza). Parado o casi, al balón; y si se mueve despacio hacia delante o hacia atrás
    /// sigue mirándolo (hacia atrás es un retroceso: la zancada se reproduce al revés, ver <see cref="AdvancePhase"/>).
    /// De lado no hay clip que lo haga creíble, así que mira a donde va. El que va a recibir un pase aguanta mirando al
    /// pasador hasta una velocidad mayor. Derribado no gira. Vector nulo = sigue como estaba.
    /// </summary>
    private Vector2 ChooseFacing(Vector2 velocity, Vector2 facing, Vector2 toBall, bool receiving, bool down)
    {
        if (down)
        {
            return Vector2.Zero;
        }

        float speed = velocity.Length();
        bool canBall = toBall.LengthSquared() > BallLookMinCells * BallLookMinCells;
        float exit = receiving ? FaceBallReceiverExitCellsPerSecond : FaceBallExitCellsPerSecond;
        _faceBall = _faceBall
            ? canBall && speed <= exit
            : canBall && speed < (receiving ? exit : FaceBallEnterCellsPerSecond);

        if (!_faceBall)
        {
            return facing;
        }

        if (speed > StandStillCellsPerSecond && Mathf.Abs(velocity.Normalized().Dot(toBall.Normalized())) < LateralCos)
        {
            return facing;
        }

        return toBall;
    }

    /// <summary>
    /// Avanza la fase con lo que el cuerpo se ha movido DE VERDAD en pantalla este fotograma: a la velocidad que sea, a
    /// x4 o en cámara lenta, los pies dan exactamente los pasos que el cuerpo recorre (H5) y, congelada la imagen, no
    /// se mueven (H6). Un desplazamiento mayoritariamente hacia atrás retrocede la fase (retroceso de cara al balón).
    /// </summary>
    private void AdvancePhase(float jogToRun, float delta)
    {
        var here = GlobalPosition;
        if (delta <= 0f || !_hasLast)
        {
            _lastGlobal = here;
            _hasLast = true;
            _debugFeetScale = 0f;
            return;
        }

        var step = new Vector2(here.X - _lastGlobal.X, here.Z - _lastGlobal.Z);
        _lastGlobal = here;
        float distance = step.Length();
        float cycle = CycleCells(jogToRun);
        if (distance > 0.6f || cycle <= 0.001f)
        {
            return;
        }

        var forward = new Vector2(Mathf.Sin(_yaw), Mathf.Cos(_yaw));
        // Hacia atrás sólo cuando reculaba mirando al balón: en un giro a la carrera el cuerpo va un instante «de
        // espaldas» al muñeco que aún está girando, y ahí la zancada no debe invertirse (visto en la hoja del giro).
        // «Hacia atrás» = componente hacia atrás de más de un cuarto del paso (provisional, sin medir). El tope de 0,6
        // casillas por fotograma es el umbral de teletransporte de la vista (BA-K): eso no es una zancada.
        float signed = _faceBall && step.Dot(forward) < -0.25f * distance ? -distance : distance;
        _phase = Mathf.PosMod(_phase + (signed / cycle), 1f);

        float length = Mathf.Lerp(ClipLength("jog"), ClipLength("run"), jogToRun);
        _debugFeetScale = signed / cycle * length / delta;
        _debugFeetNatural = cycle / Mathf.Max(0.001f, length);
    }

    /// <summary>Zancada de un ciclo completo (dos pasos), en casillas, a esta mezcla trote→carrera: desplazamiento horneado del clip por su duración.</summary>
    private float CycleCells(float jogToRun)
    {
        if (_skeleton is null || !_skeleton.IsInsideTree())
        {
            return 0f;
        }

        float scale = _skeleton.GlobalTransform.Basis.Scale.X;
        NaturalSkeletonSpeed.TryGetValue("jog", out float jog);
        NaturalSkeletonSpeed.TryGetValue("run", out float run);
        return Mathf.Lerp(jog * ClipLength("jog"), run * ClipLength("run"), jogToRun) * scale;
    }

    private static float ClipLength(string key) => _library is not null && _library.HasAnimation(key) ? _library.GetAnimation(key).Length : 1f;

    private float ClipTimeAtPhase(string key, float offset)
    {
        float length = ClipLength(key);
        return Mathf.PosMod(offset + (_phase * length), length);
    }

    /// <summary>
    /// Dónde va el balón de quien lo conduce, respecto al centro de su cuerpo y en casillas del mundo (BV-A, punto 4):
    /// delante, en la dirección en que MIRA el muñeco, tocado con la puntera izquierda en la fase 0 de cada ciclo y
    /// rodando <see cref="DribbleRollCells"/> por delante hasta el toque siguiente. Parado, quieto delante del pie; en el
    /// golpeo, donde la puntera llega en el contacto. Las distancias al pie son las de los clips, medidas al cargar.
    /// Sustituye a anclarlo a la puntera más cercana, que cambiaba de pie a cada paso (H11). <c>false</c> si aún no hay
    /// medidas o no hay esqueleto.
    /// </summary>
    public bool TryBallAtFeet(float ballRadius, out Vector2 offset)
    {
        offset = Vector2.Zero;
        if (!_cyclesMeasured || _instance is null || !_instance.IsInsideTree())
        {
            return false;
        }

        float k = _instance.GlobalTransform.Basis.Scale.X;
        float move = _jogBlend > 0f ? Mathf.Clamp(_blend / _jogBlend, 0f, 1f) : 1f;
        float reach = Mathf.Lerp(_reachIdle.Z, _reachRun.Z, move) * k;
        float dribble = reach + ballRadius + (DribbleRollCells * move * Mathf.Sin(Mathf.Pi * _phase));
        float distance = _gesture == "kick" ? Mathf.Lerp(dribble, (_reachKick.Z * k) + ballRadius, _gestureWeight) : dribble;
        offset = new Vector2(Mathf.Sin(_yaw), Mathf.Cos(_yaw)) * distance;
        return true;
    }

    /// <summary>
    /// Mide, una vez y sobre un personaje de prueba colgado de este modelo, la fase de cada clip de marcha (instante con
    /// la puntera izquierda más adelantada respecto a la derecha) y dónde caen las punteras en la zancada, en la espera
    /// y en el contacto del golpeo. Mismo método que <see cref="DebugFootProfile"/>, que ya midió el contacto del golpeo.
    /// </summary>
    private void MeasureCycles()
    {
        if (_cyclesMeasured || _character is null || _library is null || !IsInsideTree())
        {
            return;
        }

        _cyclesMeasured = true;
        var probe = _character.Instantiate<Node3D>();
        AddChild(probe);
        var anim = FindAnimationPlayer(probe);
        var skeleton = FindSkeleton(probe);
        if (anim is not null && skeleton is not null)
        {
            if (!anim.HasAnimationLibrary(Library))
            {
                anim.AddAnimationLibrary(Library, _library);
            }

            int left = skeleton.FindBone("mixamorig_LeftToeBase");
            int right = skeleton.FindBone("mixamorig_RightToeBase");
            if (left >= 0 && right >= 0)
            {
                (Vector3 L, Vector3 R) Toes(string key, float t)
                {
                    anim.Play($"{Library}/{key}");
                    anim.Seek(t, true);
                    var toRoot = probe.GlobalTransform.AffineInverse() * skeleton.GlobalTransform;
                    return (toRoot * skeleton.GetBoneGlobalPose(left).Origin, toRoot * skeleton.GetBoneGlobalPose(right).Origin);
                }

                (float T, Vector3 L) LeftForward(string key)
                {
                    float length = ClipLength(key);
                    (float T, Vector3 L, float D) best = (0f, Vector3.Zero, float.MinValue);
                    for (int i = 0; i < 60; i++)
                    {
                        float t = i * length / 60f;
                        var (l, r) = Toes(key, t);
                        if (l.Z - r.Z > best.D)
                        {
                            best = (t, l, l.Z - r.Z);
                        }
                    }

                    return (best.T, best.L);
                }

                (_jogOffset, _) = LeftForward("jog");
                (_runOffset, _reachRun) = LeftForward("run");
                var (il, ir) = Toes("idle", 0f);
                _reachIdle = il.Z >= ir.Z ? il : ir;
                var (kl, kr) = Toes("kick", KickContactSeconds);
                _reachKick = kl.Z >= kr.Z ? kl : kr;
                GD.Print($"[modelos] ciclo medido: fase 0 del trote {_jogOffset:0.###} s, de la carrera {_runOffset:0.###} s; "
                    + $"puntera adelantada {_reachRun.Z:0.###} (carrera), {_reachIdle.Z:0.###} (espera), {_reachKick.Z:0.###} (golpeo) en unidades del personaje");
            }
        }

        RemoveChild(probe);
        probe.Free();
    }

    /// <summary>
    /// Gira hacia <paramref name="facing"/> como mucho <see cref="TurnDegreesPerSecond"/> (H2). Un
    /// <paramref name="facing"/> nulo es «sigue mirando a donde mirabas»: parado, o con una ida y vuelta que
    /// la traza deshace enseguida (lo decide la vista mirando adelante en la traza ya calculada).
    /// </summary>
    private void Turn(Vector2 facing, float simDelta, bool snap)
    {
        if (facing.LengthSquared() > 0.000001f)
        {
            float want = Mathf.Atan2(facing.X, facing.Y) + FacingOffset;
            if (!_hasYaw || snap)
            {
                _yaw = want;
            }
            else
            {
                float diff = Mathf.Wrap(want - _yaw, -Mathf.Pi, Mathf.Pi);
                float max = Mathf.DegToRad(TurnDegreesPerSecond) * simDelta;
                _yaw = Mathf.Wrap(_yaw + Mathf.Clamp(diff, -max, max), -Mathf.Pi, Mathf.Pi);
            }

            _hasYaw = true;
        }

        Rotation = new Vector3(0f, _yaw, 0f);
    }

    private float _yaw;
    private bool _hasYaw;

    /// <summary>
    /// Qué gesto toca encima de la locomoción. El suelo manda (el que cae, cae); después el gesto de evento
    /// —que se deja terminar—, y por último el de estado, que vive mientras dura su estado y se funde al salir.
    /// </summary>
    private void ChooseGesture(PlayerState state, ContactCue cue, KickCue kick, float speed)
    {
        if (state is PlayerState.KnockedDown or PlayerState.Injured)
        {
            if (_gesture == "trip" && _gestureTime >= _gestureLength)
            {
                StartGesture("fallen", held: true);
            }
            else if (_gesture is not ("trip" or "fallen"))
            {
                StartGesture("trip", held: true);
            }

            return;
        }

        if (_gesture is "trip" or "fallen")
        {
            StopGesture();
        }

        // El golpeo, ALINEADO con la traza (H7): la vista avisa de cuántos segundos de partido faltan para que
        // este jugador suelte el balón; el clip arranca para que su pie llegue justo entonces, y se deja
        // terminar. Si el aviso llega tarde, el clip entra ya avanzado lo que corresponda.
        bool handsOrSetPiece = cue is ContactCue.ThrowIn or ContactCue.Penalty || _gesture is "throwin" or "penalty";
        // Llegando corriendo, el golpeo se lanza más tarde y ya avanzado (carrerilla): hasta entonces manda la zancada.
        float lead = _runCells > 0.01f
            ? Mathf.Lerp(KickContactSeconds, KickMinLeadSeconds, Mathf.Clamp(speed / _runCells, 0f, 1f))
            : KickContactSeconds;
        if (!handsOrSetPiece && kick.Release >= 0 && kick.Release != _lastKickRelease && kick.SecondsToContact <= lead)
        {
            _lastKickRelease = kick.Release;
            StartGesture("kick", held: true, KickContactSeconds - Mathf.Max(0f, kick.SecondsToContact));
            _gestureFade = Mathf.Clamp(kick.SecondsToContact * 0.75f, 0.03f, BlendSeconds);
            return;
        }

        string? cueClip = cue switch
        {
            ContactCue.Receive => "receive",
            ContactCue.Header => "header",
            ContactCue.ThrowIn => "throwin",
            ContactCue.Save => "gk_save",
            ContactCue.Catch => "gk_catch",
            ContactCue.Penalty => "penalty",
            _ => null,
        };

        if (cueClip is not null)
        {
            if (!(_gesture == cueClip && _gestureHeld))
            {
                StartGesture(cueClip, held: true);
            }

            return;
        }

        if (_gestureHeld)
        {
            return;
        }

        // El golpeo por estado sólo queda de respaldo, para un pase o tiro cuya salida la traza no enseñe.
        string? stateClip = state switch
        {
            PlayerState.Tackling => "tackle",
            PlayerState.Shooting or PlayerState.Passing when kick.Release < 0 => "kick",
            _ => null,
        };

        if (stateClip is null)
        {
            StopGesture();
        }
        else if (_gesture != stateClip)
        {
            StartGesture(stateClip, held: false);
        }
    }

    /// <summary>Lanza un gesto desde su principio (la transición admite volver a sí misma: dos pases seguidos son dos golpeos).</summary>
    private void StartGesture(string key, bool held, float offset = 0f)
    {
        if (_tree is null || _library is null || !_library.HasAnimation(key))
        {
            return;
        }

        _tree.Set("parameters/gesture/transition_request", key);
        if (offset > 0f)
        {
            _tree.Set("parameters/gesture_seek/seek_request", offset);
        }

        _gesture = key;
        _gestureFade = BlendSeconds;
        _outFade = BlendSeconds;
        _gestureHeld = held;
        _gestureTime = offset;
        var clip = _library.GetAnimation(key);
        _gestureLength = key == "receive"
            ? ReceiveLengthSeconds
            : clip.LoopMode == Animation.LoopModeEnum.None ? clip.Length : float.MaxValue;
    }

    /// <summary>La salida de balón para la que ya se lanzó un golpeo, para no lanzarlo dos veces.</summary>
    private int _lastKickRelease = -1;

    private void StopGesture()
    {
        _gesture = string.Empty;
        _gestureHeld = false;
    }

    /// <summary>El reloj del gesto. Uno que terminó deja paso a la locomoción, salvo el trompicón, que encadena con el suelo.</summary>
    private void AdvanceGesture(float delta)
    {
        if (_gesture.Length == 0)
        {
            return;
        }

        _gestureTime += delta;
        if (_gestureTime >= _gestureLength && _gesture != "trip")
        {
            StopGesture();
        }
    }

    /// <summary>
    /// La zancada natural de la carrera de este modelo, en casillas por segundo: el desplazamiento horneado del
    /// clip (medido al cargarlo, antes de fijarlo) por la escala real del esqueleto. Hace falta estar en el
    /// árbol para conocer esa escala, así que se mide la primera vez que se posa.
    /// </summary>
    private void MeasureStride()
    {
        if (_runCells > 0f || _skeleton is null || !_skeleton.IsInsideTree())
        {
            return;
        }

        if (NaturalSkeletonSpeed.TryGetValue("run", out float run) && run > 0f)
        {
            _runCells = run * _skeleton.GlobalTransform.Basis.Scale.X;
        }
    }

    /// <summary>
    /// Monta el árbol de mezcla (BV-A; skill <c>animation-system</c>: <c>AnimationTree</c> cuando hay que
    /// mezclar). Raíz <see cref="AnimationNodeBlendTree"/>: <c>loco</c> (<see cref="AnimationNodeBlendSpace1D"/>
    /// idle→jog→run con <c>Sync</c>, para que los tres clips sigan andando y cruzar un punto no reinicie
    /// ninguno) → <c>loco_scale</c> → <c>mix</c>[0]; <c>gesture</c> (<see cref="AnimationNodeTransition"/>,
    /// un clip por gesto) → <c>gesture_scale</c> → <c>mix</c>[1]. El peso de <c>mix</c> es el del gesto.
    /// Los puntos de la mezcla están en la zancada natural de cada clip, relativa a la de la carrera: así, a
    /// una velocidad dada, la zancada mezclada es esa misma velocidad y los pies no patinan.
    /// </summary>
    private void BuildTree()
    {
        if (_anim is null || _library is null)
        {
            return;
        }

        var root = new AnimationNodeBlendTree();

        // La espera va con su reloj (al ritmo de la reproducción); trote y carrera se COLOCAN cada fotograma en la
        // misma fase (BV-A, punto 2): mezclar dos zancadas desfasadas daba piernas a medio camino de dos pasos.
        root.AddNode("idle", Clip(_keeper ? "gk_idle" : "idle"));
        root.AddNode("idle_scale", new AnimationNodeTimeScale());
        root.ConnectNode("idle_scale", 0, "idle");
        foreach (var key in new[] { "jog", "run" })
        {
            root.AddNode(key, Clip(key));
            root.AddNode(key + "_seek", new AnimationNodeTimeSeek());
            root.ConnectNode(key + "_seek", 0, key);
        }

        root.AddNode("jr", new AnimationNodeBlend2());
        root.ConnectNode("jr", 0, "jog_seek");
        root.ConnectNode("jr", 1, "run_seek");
        root.AddNode("loco", new AnimationNodeBlend2());
        root.ConnectNode("loco", 0, "idle_scale");
        root.ConnectNode("loco", 1, "jr");

        var gestures = new AnimationNodeTransition { XfadeTime = 0.1f, AllowTransitionToSelf = true, InputCount = GestureKeys.Length };
        root.AddNode("gesture", gestures);
        for (int i = 0; i < GestureKeys.Length; i++)
        {
            gestures.SetInputName(i, GestureKeys[i]);
            gestures.SetInputReset(i, true);
            var node = Clip(GestureKeys[i]);
            if (GestureKeys[i] == "receive")
            {
                // Sólo el control, no los cinco segundos de pasitos (H7).
                node.UseCustomTimeline = true;
                node.StretchTimeScale = false;
                node.StartOffset = ReceiveStartSeconds;
                node.TimelineLength = ReceiveLengthSeconds;
                node.LoopMode = Animation.LoopModeEnum.None;
            }

            root.AddNode("g_" + GestureKeys[i], node);
            root.ConnectNode("gesture", i, "g_" + GestureKeys[i]);
        }

        root.AddNode("gesture_seek", new AnimationNodeTimeSeek());
        root.ConnectNode("gesture_seek", 0, "gesture");
        root.AddNode("gesture_scale", new AnimationNodeTimeScale());
        root.ConnectNode("gesture_scale", 0, "gesture_seek");
        root.AddNode("mix", new AnimationNodeBlend2());
        root.ConnectNode("mix", 0, "loco");
        root.ConnectNode("mix", 1, "gesture_scale");
        root.ConnectNode("output", 0, "mix");

        _anim.Stop();
        _tree = new AnimationTree { Name = "Tree", TreeRoot = root, RootNode = _anim.RootNode };
        _tree.AddAnimationLibrary(Library, _library);
        _anim.GetParent().AddChild(_tree);
        _tree.Active = true;
    }

    private static AnimationNodeAnimation Clip(string key) => new() { Animation = $"{Library}/{key}" };

    // ------------------------------------------------------------------ diagnóstico (BV-A), solo lectura

    /// <summary>Velocidad (casillas/s) que <see cref="Pose"/> recibió en su última llamada. Solo para el instrumento de BV-A.</summary>
    public float DebugInputSpeed { get; private set; }

    /// <summary>Clave del clip que se está reproduciendo (<c>run</c>, <c>jog</c>, <c>idle</c>...). Solo para el instrumento de BV-A.</summary>
    public string DebugClip => _gestureWeight > 0.5f && _gesture.Length > 0
        ? _gesture
        : _blend < _jogBlend / 2f ? (_keeper ? "gk_idle" : "idle") : _blend < (_jogBlend + 1f) / 2f ? "jog" : "run";

    /// <summary>Segundo del clip en curso, o -1 si el reproductor ya no está sonando (un golpe que terminó). BV-A.</summary>
    public float DebugClipTime => _gestureWeight > 0.5f && _gesture.Length > 0 ? _gestureTime : _blend;

    /// <summary>Multiplicador de ritmo del reproductor ahora mismo. BV-A.</summary>
    public float DebugSpeedScale => _debugFeetScale;

    /// <summary>
    /// A qué velocidad, en casillas por segundo, avanzaría el cuerpo si el clip conservara su desplazamiento
    /// horneado (el que <see cref="PinInPlace"/> le quita), con este modelo a su escala. Es la velocidad a
    /// la que los pies NO patinan con <c>SpeedScale</c> 1. 0 si el clip no se desplaza. Solo BV-A.
    /// </summary>
    public float DebugNaturalCellsPerSecond(string key)
    {
        // La locomoción es una mezcla: con los puntos en la zancada de cada clip, la de la mezcla es
        // posición × zancada de la carrera.
        if (key is "idle" or "gk_idle" or "jog" or "run")
        {
            // Con la fase atada al desplazamiento, los pies avanzan lo que el cuerpo por construcción: esta cifra deja de
            // medir nada (cuerpo/pies = 1). La medida honesta es el pie de apoyo en el mundo (instrumento, Regla J).
            return _debugFeetNatural;
        }

        if (_skeleton is null || !NaturalSkeletonSpeed.TryGetValue(key, out float perSecond))
        {
            return 0f;
        }

        return perSecond * _skeleton.GlobalTransform.Basis.Scale.X;
    }

    /// <summary>Las dos punteras en coordenadas del mundo, ya animadas; ceros si el modelo no tiene esqueleto. Solo BV-A.</summary>
    public void DebugToes(out Vector3 left, out Vector3 right)
    {
        left = right = Vector3.Zero;
        if (_skeleton is null || !_contactBones.TryGetValue(ContactPart.Feet, out var bones) || bones.Left < 0 || bones.Right < 0)
        {
            return;
        }

        var toWorld = _skeleton.GlobalTransform;
        left = toWorld * _skeleton.GetBoneGlobalPose(bones.Left).Origin;
        right = toWorld * _skeleton.GetBoneGlobalPose(bones.Right).Origin;
    }

    /// <summary>Una línea CSV por clip montado: clave, duración en segundos, bucle y desplazamiento horneado por segundo (unidades de esqueleto). BV-A.</summary>
    public static string DebugDescribeClips()
    {
        var sb = new System.Text.StringBuilder("clip,length,loop,naturalSkelPerSecond\n");
        foreach (var (key, _, loop) in Clips)
        {
            if (_library is null || !_library.HasAnimation(key))
            {
                continue;
            }

            float length = _library.GetAnimation(key).Length;
            NaturalSkeletonSpeed.TryGetValue(key, out float natural);
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"{key},{length:0.###},{(loop ? 1 : 0)},{natural:0.####}\n");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Perfil de los dos pies a lo largo de un clip, muestreado a 60 Hz en un personaje de prueba colgado de
    /// <paramref name="parent"/>: <c>t, pie izquierdo xyz, pie derecho xyz</c> en espacio del esqueleto. De ahí
    /// sale cuándo golpea el pie (pico de velocidad de la puntera), que es lo que hay que alinear con el tick
    /// en que sale el balón. Solo para el instrumento de BV-A.
    /// </summary>
    public static string DebugFootProfile(Node parent, string key)
    {
        var sb = new System.Text.StringBuilder();
        if (_character is null || _library is null || !_library.HasAnimation(key))
        {
            return sb.ToString();
        }

        var probe = _character.Instantiate<Node3D>();
        parent.AddChild(probe);
        var anim = FindAnimationPlayer(probe);
        var skeleton = FindSkeleton(probe);
        if (anim is not null && skeleton is not null)
        {
            if (!anim.HasAnimationLibrary(Library))
            {
                anim.AddAnimationLibrary(Library, _library);
            }

            int left = skeleton.FindBone("mixamorig_LeftToeBase");
            int right = skeleton.FindBone("mixamorig_RightToeBase");
            float length = _library.GetAnimation(key).Length;
            anim.Play($"{Library}/{key}");
            for (float t = 0f; t <= length; t += 1f / 60f)
            {
                anim.Seek(t, true);
                var l = skeleton.GetBoneGlobalPose(left).Origin;
                var r = skeleton.GetBoneGlobalPose(right).Origin;
                sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $"{key},{t:0.####},{l.X:0.###},{l.Y:0.###},{l.Z:0.###},{r.X:0.###},{r.Y:0.###},{r.Z:0.###}\n");
            }
        }

        parent.RemoveChild(probe);
        probe.Free();
        return sb.ToString();
    }

    /// <summary>Desplazamiento horizontal neto del hueso raíz por segundo de clip, en unidades del esqueleto, por clave. BV-A.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, float> NaturalSkeletonSpeed = new();

    /// <summary>
    /// Dónde está, en el mundo, la parte del cuerpo con la que se está jugando el balón (BI-H). Devuelve
    /// <c>false</c> si este modelo no tiene esqueleto o el hueso no existe en el pack, y entonces la vista
    /// se queda con lo que hacía antes — igual que se queda con la cápsula cuando no hay modelo.
    ///
    /// <para>Se lee del <see cref="Skeleton3D"/> <b>ya animado</b>: la postura de este fotograma la ha
    /// calculado el <see cref="AnimationPlayer"/>, así que el punto es coherente con el clip que se está
    /// reproduciendo sin anotar nada en los clips ni añadir marcadores. De los dos huesos de la parte se
    /// devuelve el más cercano a <paramref name="towards"/>, que es lo que hace que un pase salga del pie
    /// que está del lado del balón.</para>
    /// </summary>
    public bool TryContactPoint(ContactPart part, Vector3 towards, out Vector3 world)
    {
        world = Vector3.Zero;
        if (_skeleton is null || !_contactBones.TryGetValue(part, out var bones))
        {
            return false;
        }

        var skeletonToWorld = _skeleton.GlobalTransform;
        var left = bones.Left >= 0 ? skeletonToWorld * _skeleton.GetBoneGlobalPose(bones.Left).Origin : (Vector3?)null;
        var right = bones.Right >= 0 ? skeletonToWorld * _skeleton.GetBoneGlobalPose(bones.Right).Origin : (Vector3?)null;

        if (left is null && right is null)
        {
            return false;
        }

        if (left is null || right is null)
        {
            world = left ?? right!.Value;
            return true;
        }

        world = left.Value.DistanceSquaredTo(towards) <= right.Value.DistanceSquaredTo(towards)
            ? left.Value
            : right.Value;
        return true;
    }

    /// <summary>Los índices de la tabla, una vez por modelo. Un hueso que no exista queda en -1.</summary>
    private void ResolveContactBones()
    {
        if (_skeleton is null)
        {
            return;
        }

        foreach (var (part, leftName, rightName) in ContactBones)
        {
            _contactBones[part] = (_skeleton.FindBone(leftName), _skeleton.FindBone(rightName));
        }
    }

    /// <summary>El <see cref="Skeleton3D"/> esté donde esté, por el mismo motivo que el reproductor.</summary>
    private static Skeleton3D? FindSkeleton(Node node)
    {
        if (node is Skeleton3D found)
        {
            return found;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindSkeleton(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>
    /// Carga los clips una vez en una biblioteca compartida. Cada fichero de Mixamo trae su animación
    /// dentro de una escena propia, así que hay que instanciarla para sacarla; el recurso de animación
    /// sobrevive a liberar la escena porque la biblioteca se queda con la referencia.
    /// </summary>
    private static AnimationLibrary BuildLibrary()
    {
        var library = new AnimationLibrary();
        foreach (var (key, file, loop) in Clips)
        {
            if (ResourceLoader.Load($"{Folder}/{file}.fbx") is not PackedScene packed)
            {
                GD.Print($"[modelos] falta el clip '{file}.fbx': '{key}' se queda sin animación");
                continue;
            }

            var probe = packed.Instantiate();
            var player = FindAnimationPlayer(probe);
            var names = player?.GetAnimationList() ?? Array.Empty<string>();
            if (player is not null && names.Length > 0)
            {
                var clip = player.GetAnimation(names[0]);
                clip.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                NaturalSkeletonSpeed[key] = NetRootTravel(clip) / Math.Max(clip.Length, 0.001f);
                float drift = PinInPlace(clip);
                if (drift > 0.01f)
                {
                    GD.Print($"[modelos] '{key}': deriva horizontal {drift:0.##} m quitada del hueso raíz");
                }

                library.AddAnimation(key, clip);
            }
            else
            {
                GD.Print($"[modelos] '{file}.fbx' no trae ninguna animación: '{key}' se queda sin ella");
            }

            probe.Free();
        }

        // Dónde cae el trote en la mezcla de locomoción: su zancada relativa a la de la carrera (BV-A).
        if (NaturalSkeletonSpeed.TryGetValue("jog", out float jog) && NaturalSkeletonSpeed.TryGetValue("run", out float run) && run > 0f)
        {
            _jogBlend = Math.Clamp(jog / run, 0.1f, 0.9f);
        }

        return library;
    }

    /// <summary>
    /// Deja el clip <b>en el sitio</b>: fija a su valor inicial el desplazamiento horizontal del hueso
    /// raíz y conserva el vertical. Devuelve cuánto se desplazaba, en metros del modelo, para poder
    /// decirlo en consola.
    ///
    /// <para><b>Por qué hay que hacerlo, y por qué no es un parche.</b> Estos clips vienen con el
    /// desplazamiento horneado en la raíz (<i>root motion</i>): al correr, la animación se lleva al modelo
    /// hacia delante y lo saca de su anillo, y al cambiar de postura vuelve de golpe a su sitio — lo
    /// reportó el revisor jugando. En Underleague eso está mal <b>por construcción</b>: la posición de cada
    /// jugador la manda <c>/Sim</c> tick a tick y el render no mueve a nadie (RT-014). Un clip que mueve al
    /// modelo es el render decidiendo dónde está un jugador, que es justo la frontera que el proyecto no
    /// cruza. Equivale a la casilla «In Place» de Mixamo, aplicada al cargar para no depender de cómo se
    /// descargó cada fichero.</para>
    ///
    /// <para>El vertical <b>se conserva</b> a propósito: es el balanceo del cuerpo al correr y el bajar al
    /// suelo del trompicón. Quitarlo dejaría un muñeco deslizándose.</para>
    /// </summary>
    private static float PinInPlace(Animation clip)
    {
        float drift = 0f;
        for (int track = 0; track < clip.GetTrackCount(); track++)
        {
            if (clip.TrackGetType(track) != Animation.TrackType.Position3D)
            {
                continue;
            }

            // Solo la raíz: el resto de huesos SÍ deben moverse, son el cuerpo.
            if (!clip.TrackGetPath(track).ToString().EndsWith(RootBone, StringComparison.Ordinal))
            {
                continue;
            }

            int keys = clip.TrackGetKeyCount(track);
            if (keys == 0)
            {
                continue;
            }

            var origin = (Vector3)clip.TrackGetKeyValue(track, 0);
            for (int key = 0; key < keys; key++)
            {
                var value = (Vector3)clip.TrackGetKeyValue(track, key);
                drift = Math.Max(drift, new Vector2(value.X - origin.X, value.Z - origin.Z).Length());
                clip.TrackSetKeyValue(track, key, new Vector3(origin.X, value.Y, origin.Z));
            }
        }

        return drift;
    }

    /// <summary>
    /// Desplazamiento horizontal neto (primera clave a última) del hueso raíz, antes de fijarlo. Para un
    /// ciclo de carrera es la zancada del clip entero; de ahí sale a qué velocidad los pies no patinan (BV-A).
    /// </summary>
    private static float NetRootTravel(Animation clip)
    {
        for (int track = 0; track < clip.GetTrackCount(); track++)
        {
            if (clip.TrackGetType(track) != Animation.TrackType.Position3D
                || !clip.TrackGetPath(track).ToString().EndsWith(RootBone, StringComparison.Ordinal))
            {
                continue;
            }

            int keys = clip.TrackGetKeyCount(track);
            if (keys < 2)
            {
                return 0f;
            }

            var first = (Vector3)clip.TrackGetKeyValue(track, 0);
            var last = (Vector3)clip.TrackGetKeyValue(track, keys - 1);
            return new Vector2(last.X - first.X, last.Z - first.Z).Length();
        }

        return 0f;
    }

    /// <summary>El <see cref="AnimationPlayer"/> esté donde esté en el árbol importado: el importador no garantiza dónde lo cuelga.</summary>
    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer found)
        {
            return found;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindAnimationPlayer(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static void Paint(Node node, Material material)
    {
        if (node is MeshInstance3D mesh)
        {
            mesh.MaterialOverride = material;
        }

        foreach (var child in node.GetChildren())
        {
            Paint(child, material);
        }
    }

    /// <summary>Alto del modelo en su pose de reposo, medido sobre la caja de la malla más alta que tenga.</summary>
    private static float MeasureHeight(Node node)
    {
        float best = 0f;
        if (node is MeshInstance3D mesh)
        {
            best = mesh.GetAabb().Size.Y;
        }

        foreach (var child in node.GetChildren())
        {
            best = Math.Max(best, MeasureHeight(child));
        }

        return best;
    }
}
