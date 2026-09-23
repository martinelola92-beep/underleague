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
/// <para><b>Lo que sigue faltando</b>: no hay <b>celebración</b> en el pack (se queda en la espera) y la
/// <b>estirada del portero</b> no está enganchada porque depende de un evento —una parada— y no del estado
/// del jugador, que es lo único que esta clase mira.</para>
/// </summary>
public sealed partial class PlayerModel : Node3D
{
    /// <summary>La carpeta del material de fútbol: personaje y clips, todos del mismo esqueleto.</summary>
    private const string Folder = "res://models/soccer";

    /// <summary>El personaje con malla. Los clips vienen en ficheros aparte, sin malla.</summary>
    private const string CharacterPath = Folder + "/X Bot.fbx";

    /// <summary>Nombre de la biblioteca que se monta en cada reproductor; prefija a todas las claves.</summary>
    private const string Library = "soccer";

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
    private string _playing = string.Empty;
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
    public void Pose(Vector2 velocity, PlayerState state)
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

    /// <summary>Cambia de animación solo si no es la que ya suena (llamar cada fotograma es seguro).</summary>
    private void Switch(string key)
    {
        if (_playing == key)
        {
            return;
        }

        _playing = key;
        _anim!.SpeedScale = 1f;
        _anim.Play($"{Library}/{key}");
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
