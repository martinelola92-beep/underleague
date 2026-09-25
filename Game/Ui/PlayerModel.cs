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

    /// <summary>Por debajo de esto se considera quieto (casillas por segundo).</summary>
    private const float MovingThreshold = 0.15f;

    /// <summary>A partir de esta velocidad se corre en vez de trotar (casillas por segundo).</summary>
    private const float RunThreshold = 1.9f;

    /// <summary>Velocidad a la que el trote y la carrera van a su ritmo natural (<c>SpeedScale</c> 1).</summary>
    private const float JogReferenceSpeed = 1.4f;
    private const float RunReferenceSpeed = 2.8f;

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

    /// <summary>
    /// Índices de hueso resueltos una vez por modelo. <see cref="Skeleton3D.FindBone"/> recorre los 65
    /// huesos por nombre, y esto se consultaría catorce veces por fotograma.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<ContactPart, (int Left, int Right)> _contactBones = new();
    private string _playing = string.Empty;

    /// <summary>
    /// El gesto de contacto que se está reproduciendo y hay que <b>dejar terminar</b>, o vacío. Existe
    /// porque un gesto nacido de un evento dura <b>un tick</b> —el cabezazo, la parada— y sin esto el
    /// fotograma siguiente lo cortaba con la postura de estado: se lanzaba el remate y al instante volvía
    /// a correr. El disparo y el pase no lo sufren porque <c>Passing</c>/<c>Shooting</c> duran cinco ticks.
    /// Es el mismo patrón que ya encadena <c>trip</c> con <c>fallen</c>: lo manda el reloj de la propia
    /// animación, no un temporizador aparte.
    /// </summary>
    private string _holdingCue = string.Empty;
    private bool _keeper;

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
        model.Switch(keeper ? "gk_idle" : "idle");
        return model;
    }

    /// <summary>
    /// Pinta el modelo del color del equipo. Se hace con <c>MaterialOverride</c> sobre las mallas: lo que
    /// tiene que leerse de un vistazo es de qué bando es cada uno (RA-002).
    /// </summary>
    public void Paint(Material material) => Paint(this, material);

    /// <summary>
    /// La postura de este fotograma. <paramref name="velocity"/> viene de la diferencia entre dos
    /// fotogramas de la traza, en casillas por segundo, y <paramref name="state"/> es el estado que la
    /// simulación ya publica por jugador (<c>MatchTrace.StateAt</c>): <b>el modelo no decide nada</b>, solo
    /// mira lo que está escrito (RT-014).
    /// </summary>
    public void Pose(Vector2 velocity, PlayerState state, ContactCue cue = ContactCue.None)
    {
        if (_anim is null)
        {
            return;
        }

        float speed = velocity.Length();
        if (speed > MovingThreshold)
        {
            // Mirar hacia donde se va. Solo con movimiento de verdad: parado, el ruido de la interpolación
            // le haría girar sobre sí mismo. Antes de elegir postura, para que la entrada y el trompicón
            // también salgan orientados.
            Rotation = new Vector3(0f, Mathf.Atan2(velocity.X, velocity.Y) + FacingOffset, 0f);
        }

        // El gesto de contacto va ANTES que el estado, pero después del suelo: un rematador de cabeza
        // sigue estando en Positioning para /Sim, y un portero que para sigue estando donde estaba. Lo que
        // no puede es tapar un derribo —el que cae, cae— así que esos dos casos se miran primero.
        bool floored = state is PlayerState.KnockedDown or PlayerState.Injured;

        // Un gesto en curso se deja terminar. Solo lo interrumpe irse al suelo: el que cae, cae.
        if (_holdingCue.Length > 0)
        {
            if (!floored && _playing == _holdingCue && _anim.IsPlaying())
            {
                return;
            }

            _holdingCue = string.Empty;
        }

        if (cue != ContactCue.None && !floored)
        {
            string? clip = cue switch
            {
                ContactCue.Receive => "receive",
                ContactCue.Header => "header",
                ContactCue.ThrowIn => "throwin",
                ContactCue.Save => "gk_save",
                ContactCue.Catch => "gk_catch",
                ContactCue.Penalty => "penalty",
                _ => null,
            };

            if (clip is not null)
            {
                Once(clip);
                _holdingCue = clip;
                return;
            }
        }

        switch (state)
        {
            case PlayerState.KnockedDown:
            case PlayerState.Injured:
                // Dos tiempos, encadenados por el reloj de la propia animación y no por un temporizador
                // aparte: se va al suelo (trip) y se queda ahí (fallen, en bucle).
                if (_playing == "trip")
                {
                    if (!_anim.IsPlaying())
                    {
                        Switch("fallen");
                    }
                }
                else if (_playing != "fallen")
                {
                    Switch("trip");
                }

                return;

            case PlayerState.Tackling:
                Once("tackle");
                return;

            case PlayerState.Shooting:
                Once("kick");
                return;

            case PlayerState.Passing:
                // El pase es un golpeo más corto; comparte clip con el tiro y se distingue por lo que hace
                // el balón, que es lo que el jugador mira.
                Once("kick");
                return;

            case PlayerState.Celebrating:
                // El pack no trae celebración. Se queda en su espera antes que inventarse un gesto que
                // signifique otra cosa.
                Switch(_keeper ? "gk_idle" : "idle");
                return;
        }

        if (speed <= MovingThreshold)
        {
            Switch(_keeper ? "gk_idle" : "idle");
            _anim.SpeedScale = 1f;
            return;
        }

        bool running = speed > RunThreshold;
        Switch(running ? "run" : "jog");
        _anim.SpeedScale = Mathf.Clamp(speed / (running ? RunReferenceSpeed : JogReferenceSpeed), 0.6f, 1.8f);
    }

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

    /// <summary>Cambia de animación solo si no es la que ya suena (llamar cada fotograma es seguro).</summary>
    private void Switch(string key)
    {
        if (_playing == key)
        {
            return;
        }

        _playing = key;
        _anim!.SpeedScale = 1f;
        _anim.Play($"{Library}/{key}", BlendSeconds);
    }

    /// <summary>Un golpe seco que se deja terminar: mientras dure, ninguna otra postura lo interrumpe.</summary>
    private void Once(string key)
    {
        if (_playing == key && _anim!.IsPlaying())
        {
            return;
        }

        Switch(key);
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
