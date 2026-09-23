using System;
using Godot;

namespace Underleague.Game.Ui;

/// <summary>
/// <b>Maqueta</b> (23 sep 2026): un modelo humanoide animado en el sitio de la cápsula, solo para los
/// humanos, para ver qué cambia. No es el sistema de personajes del juego y no pretende serlo — la regla 10
/// del proyecto dice que no se produce arte hasta cerrar el diseño de la fase 2, así que esto es material
/// provisional (maniquí de Quaternius, CC0, <c>Game/models/LICENSE-quaternius.txt</c>) puesto para
/// <b>decidir con una imagen delante</b>, no para quedarse.
///
/// <para><b>Cuelga de la cápsula, no la sustituye.</b> El nodo se añade como hijo del mismo
/// <c>MeshInstance3D</c> que ya movía <c>ApplyTrace</c>, así que hereda posición sin que nada del resto de
/// la vista —anillo, dorsal, sangre, cortinilla de teletransporte, cámara— tenga que enterarse. A la
/// cápsula se le quita la malla y se queda como el <i>hueso</i> que la transforma.</para>
///
/// <para><b>Lo que la maqueta NO hace</b>, y conviene saberlo antes de mirarla: no hay animación de golpeo
/// ni de entrada —la biblioteca es genérica y no trae fútbol—, el color de equipo se pinta encima del
/// maniquí (pierde su propia textura, que es gris de todos modos) y por eso <b>no le llega la atenuación
/// del corte de teletransporte</b>, que sigue viviendo en el material de la cápsula.</para>
/// </summary>
public sealed partial class PlayerModel : Node3D
{
    /// <summary>El maniquí con sus 43 animaciones. Se carga una vez y se instancia por jugador.</summary>
    private const string ScenePath = "res://models/UAL2_Standard.glb";

    /// <summary>
    /// Quieto. De los once <c>Idle_*</c> del pack es el más neutro de pie.
    /// <para><b>Los nombres NO son los del fichero.</b> El importador de glTF de Godot reconoce el sufijo
    /// <c>_Loop</c>, lo usa para marcar el bucle y <b>lo quita del nombre</b>: en el <c>.glb</c> está
    /// <c>Idle_FoldArms_Loop</c> y en el juego se llama <c>Idle_FoldArms</c>. Pedir el nombre del fichero
    /// da <c>Animation not found</c> y el modelo se queda en su pose de reposo —los brazos en cruz del
    /// maniquí—, que es exactamente lo que salió en la primera captura. Los nombres vigentes los imprime
    /// el volcado <c>[modelos]</c> al crear el primer modelo.</para>
    /// </summary>
    private const string IdleAnimation = "Idle_FoldArms";

    /// <summary>En movimiento. El pack «Standard» no trae carrera, así que se acelera el paso.</summary>
    private const string WalkAnimation = "Walk_Carry";

    /// <summary>
    /// Derribado. Se congela en su <b>primer</b> fotograma, que es el cuerpo en el suelo: la animación va
    /// de tumbado a de pie, así que su inicio es exactamente la postura que hace falta y no hay que
    /// adivinar dónde acaba un derribo.
    /// </summary>
    private const string DownAnimation = "LayToIdle";

    /// <summary>Por debajo de esto se considera quieto (casillas por segundo).</summary>
    private const float MovingThreshold = 0.15f;

    /// <summary>Velocidad de paso que corresponde a <c>SpeedScale</c> 1. Ajustado a ojo sobre una captura.</summary>
    private const float WalkReferenceSpeed = 1.6f;

    /// <summary>
    /// Corrección de orientación del modelo, en radianes. glTF dice que el frente de un modelo mira a
    /// <b>+Z</b> y Godot considera el frente <b>−Z</b>, y el importador no lo voltea: con este maniquí, 0
    /// es lo correcto. <b>Si los jugadores corren de espaldas, esta es la palanca</b> (poner
    /// <c>Mathf.Pi</c>) — queda como constante con nombre justo para no tener que redescubrir por qué.
    /// </summary>
    private const float FacingOffset = 0f;

    private static PackedScene? _scene;
    private static bool _missing;
    private static bool _announced;

    private AnimationPlayer? _anim;
    private string _playing = string.Empty;
    private float _facing;

    /// <summary>
    /// Un modelo escalado a esa altura de cuerpo, o <c>null</c> si el fichero no está (la vista sigue con
    /// su cápsula y no se rompe nada: es la misma tolerancia que el audio tiene con los pools vacíos).
    /// </summary>
    /// <param name="bodyHeight">Altura de la cápsula que sustituye, en casillas (1 casilla = 1 unidad).</param>
    public static PlayerModel? TryCreate(float bodyHeight)
    {
        if (_missing)
        {
            return null;
        }

        if (_scene is null)
        {
            _scene = ResourceLoader.Load<PackedScene>(ScenePath);
            if (_scene is null)
            {
                _missing = true;
                GD.Print($"[modelos] no está {ScenePath}: los humanos siguen siendo cápsulas");
                return null;
            }
        }

        var instance = _scene.Instantiate<Node3D>();
        var model = new PlayerModel { Name = "Model" };
        model.AddChild(instance);

        // La altura real del maniquí se mide, no se supone: si mañana se cambia de modelo, el tamaño en el
        // campo sigue siendo el que manda la raza y no hay una constante mágica que corregir.
        float natural = MeasureHeight(instance);
        float scale = natural > 0.01f ? bodyHeight / natural : 1f;
        instance.Scale = new Vector3(scale, scale, scale);

        // El maniquí tiene el origen en los pies y la cápsula está centrada en su mitad.
        model.Position = new Vector3(0f, -bodyHeight / 2f, 0f);

        model._anim = instance.GetNodeOrNull<AnimationPlayer>("AnimationPlayer")
            ?? FindAnimationPlayer(instance);

        // Una línea, una sola vez por proceso: qué modelo se cargó, cuánto mide y si trae animaciones. Es
        // la diferencia entre «el maniquí sale en T» y saber POR QUÉ sale en T, que fue exactamente la
        // duda de la primera captura.
        if (!_announced)
        {
            _announced = true;
            GD.Print($"[modelos] {ScenePath}: alto natural {MeasureHeight(instance):0.###}, animaciones "
                + (model._anim is null
                    ? "NINGUNA (sin AnimationPlayer: se queda en su pose de reposo)"
                    : model._anim.GetAnimationList().Length + " → " + string.Join(" · ", model._anim.GetAnimationList())));
        }

        if (model._anim is not null)
        {
            Loop(model._anim, IdleAnimation);
            Loop(model._anim, WalkAnimation);

            // Arranca quieto ya, sin esperar al primer Pose(). Si no, el modelo se queda en su pose de
            // REPOSO —brazos en cruz, la T del maniquí— y cualquier captura de una pantalla pausada, o el
            // desfile de razas, sale con un espantapájaros. Costó una ronda de capturas averiguarlo.
            model._playing = IdleAnimation;
            model._anim.Play(IdleAnimation);
        }

        return model;
    }

    /// <summary>
    /// Pinta el modelo del color del equipo. Se hace con <c>MaterialOverride</c> sobre las mallas: el
    /// maniquí es gris y lo que tiene que leerse de un vistazo es de qué bando es cada uno (RA-002).
    /// </summary>
    public void Paint(Material material)
    {
        Paint(this, material);
    }

    /// <summary>
    /// La postura de este fotograma. <paramref name="velocity"/> viene de la diferencia entre dos
    /// fotogramas de la traza, en casillas por segundo: <b>el modelo no decide nada</b>, solo mira lo que
    /// la simulación ya escribió (RT-014).
    /// </summary>
    public void Pose(Vector2 velocity, bool down)
    {
        if (_anim is null)
        {
            return;
        }

        if (down)
        {
            if (_playing != DownAnimation)
            {
                _playing = DownAnimation;
                _anim.Play(DownAnimation);
                _anim.Seek(0d, update: true);
                _anim.Pause();
            }

            return;
        }

        float speed = velocity.Length();
        if (speed > MovingThreshold)
        {
            // Mirar hacia donde se va. Solo cuando hay movimiento de verdad: con el jugador parado, el
            // ruido de la interpolación le haría girar sobre sí mismo.
            _facing = Mathf.Atan2(velocity.X, velocity.Y) + FacingOffset;
            Rotation = new Vector3(0f, _facing, 0f);
        }

        string wanted = speed > MovingThreshold ? WalkAnimation : IdleAnimation;
        if (_playing != wanted)
        {
            _playing = wanted;
            _anim.Play(wanted);
        }

        _anim.SpeedScale = wanted == WalkAnimation
            ? Mathf.Clamp(speed / WalkReferenceSpeed, 0.5f, 2.5f)
            : 1f;
    }

    /// <summary>El <see cref="AnimationPlayer"/> esté donde esté en el árbol importado: el importador de glTF no garantiza dónde lo cuelga.</summary>
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

    private static void Loop(AnimationPlayer player, string name)
    {
        // El importador ya las marca en bucle al reconocer el sufijo `_Loop` (por eso se lo quita del
        // nombre), así que esto es un cinturón por si mañana se cambia de pack y llegan sin ciclo. El
        // recurso es compartido entre instancias: se marca una vez y vale para las catorce.
        if (player.HasAnimation(name))
        {
            player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }
        else
        {
            GD.Print($"[modelos] el pack no trae '{name}': el modelo se quedará quieto en esa postura");
        }
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

    /// <summary>Alto del modelo en su pose de reposo, medido sobre la caja de la primera malla que tenga.</summary>
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
