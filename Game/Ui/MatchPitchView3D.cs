using System.Collections.Generic;
using Godot;
using Underleague.Game.Ui.Broadcast;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Ui;

/// <summary>
/// Una mancha de sangre persistente (RA-027, marcador de posición procedural): visible desde
/// <see cref="MatchPitchView3D.Frame"/> igual o mayor que <paramref name="Frame"/>, así que retroceder o
/// saltar la quita o la pone sin que <see cref="MatchPitchView3D"/> lleve ningún estado de "qué manchas ya
/// pinté" — se lee de la lista entera cada fotograma, como cualquier otro residuo persistente (principio 5
/// de <c>docs/ui/README.md</c>). <paramref name="Column"/>/<paramref name="Row"/> son coordenadas
/// continuas de casilla (centro de la casilla del suceso, <c>Pitch.CellCenter</c>), <paramref name="Size"/>
/// el radio en casillas de la calcomanía irregular.
/// </summary>
public readonly record struct BloodMark(int Frame, float Column, float Row, float Size);

/// <summary>
/// El mismo partido que <see cref="MatchPitchView"/>, pero en <b>3D visto por una cámara ortográfica fija
/// en tres cuartos</b> (ADR 0102). Todavía sin toon ni modelos: <b>cápsulas grises</b> a las proporciones
/// de RA-002 y con el radio de <c>bodyRadius</c>, que es el volumen que de verdad simula
/// <c>Sim.Engine.BodySeparation</c> (ADR 0020). Es el paso 1 de «cómo se acepta» del ADR 0102: probar la
/// geometría antes de encargar nada de arte.
///
/// <para>
/// <b>No calcula nada</b> (RT-014) y no se sale de la frontera (RT-011): lee la misma
/// <see cref="MatchTrace"/> que la vista 2D, con el mismo criterio de interpolación entre dos ticks
/// (RT-020). La única altura que hay aquí —la de las cápsulas y la del balón sobre la hierba— es
/// <b>decorado que nunca se lee de vuelta</b>, como exige el ADR 0102: el balón sigue siendo un punto en
/// el plano para el motor.
/// </para>
///
/// <para>
/// <b>Escala: 1 casilla = 1 unidad de mundo.</b> El campo son 16x7 unidades en el plano XZ (sucesora de la ADR 0103): la columna del
/// campo es X y la fila es Z, así que con la cámara al lado +Z el campo sale con la misma orientación que
/// en 2D (columna 0 a la izquierda, fila 0 arriba).
/// </para>
///
/// <para>
/// <b>Modo silueta</b> (<see cref="SilhouetteMode"/>): cápsulas negras planas sobre suelo blanco, sin color
/// de equipo. Es RA-002 comprobado al pie de la letra —«toda raza debe ser reconocible en blanco y
/// negro»— antes de que exista ni un modelo.
/// </para>
/// </summary>
public partial class MatchPitchView3D : SubViewportContainer
{
    /// <summary>Píxeles de textura por casilla del suelo. 64 da 1024x320: nítido y barato de generar.</summary>
    private const int GroundPixels = 64;

    /// <summary>Radio del balón en casillas. El mismo que la vista 2D, para que se lean igual de grandes.</summary>
    private const float BallRadius = 0.13f;

    /// <summary>
    /// Distancia de la cámara al centro del campo. Con proyección ortográfica <b>no cambia el encuadre</b>,
    /// pero sí la sombra: el mapa de sombras direccional se reparte sobre
    /// <c>DirectionalShadowMaxDistance</c> unidades contadas desde la cámara, así que alejarla desperdicia
    /// resolución y la sombra sale a cuadros. 11 unidades es lo justo para que quepa el campo entero
    /// delante del plano cercano.
    /// </summary>
    private const float CameraDistance = 11f;

    /// <summary>
    /// Proporción ancho x alto de cada raza según <b>RA-002</b> (§5.1 de <c>docs/requisitos.md</c>). Esto
    /// es <b>arte</b>, no dato de simulación, y por eso vive en <c>/Game</c> y no en <c>/data</c>: el motor
    /// no sabe ni quiere saber cuánto mide de alto un orco.
    /// <para>
    /// La altura de la cápsula sale de aquí y del radio de <c>/data</c>:
    /// <c>altura = (altoRA / anchoRA) x diámetro</c>, con <c>diámetro = 2 x bodyRadius / 100</c>. Da
    /// enano 0,738 · elfo 1,091 · humano 0,907 · orco 0,855 · no-muerto 0,865.
    /// </para>
    /// </summary>
    private static readonly Dictionary<Race, (float Width, float Height)> RaceProportions = new()
    {
        [Race.Dwarf] = (13f, 16f),
        [Race.Elf] = (11f, 20f),
        [Race.Human] = (12f, 17f),
        [Race.Orc] = (16f, 18f),
        [Race.Undead] = (11f, 17f),
    };

    /// <summary>Las cinco razas de lanzamiento, en el orden en el que las recorre la captura de siluetas.</summary>
    private static readonly Race[] LaunchRaces = { Race.Dwarf, Race.Elf, Race.Human, Race.Orc, Race.Undead };

    private SubViewport _world = null!;
    private Camera3D _camera = null!;
    private DirectionalLight3D _sun = null!;
    private WorldEnvironment _environment = null!;
    private MeshInstance3D _ground = null!;
    private MeshInstance3D _ball = null!;

    /// <summary>Disco en el césped bajo el balón en vuelo: sin él, subir el balón no se lee como altura.</summary>
    private MeshInstance3D? _ballShadow;

    private readonly List<MeshInstance3D> _bodies = new();

    /// <summary>
    /// <b>Maqueta</b> (23 sep 2026): el modelo humanoide de los humanos, <c>null</c> para el resto. Cuelga
    /// de <see cref="_bodies"/>, así que se mueve y se libera con la cápsula sin tocar nada más. Ver
    /// <see cref="PlayerModel"/> para qué es y qué no es.
    /// </summary>
    private readonly List<PlayerModel?> _models = new();

    private readonly List<MeshInstance3D> _rings = new();
    private readonly List<Label3D> _numbers = new();
    private readonly List<float> _heights = new();
    private readonly List<float> _radii = new();

    private ArrayMesh? _ringMesh;

    private ImageTexture? _grassTexture;
    private ImageTexture? _whiteTexture;
    private StandardMaterial3D _groundMaterial = null!;
    private StandardMaterial3D _ballMaterial = null!;

    private bool _built;
    private bool _appliedSilhouette;

    // ------------------------------------------------------------------ gestos de cámara (docs/ui/README §4)

    /// <summary>Sacudida en curso: envolvente lineal de 1 a 0 durante <see cref="_shakeDuration"/>, nunca <c>System.Random</c> (ver <see cref="ShakeOffset2D"/>).</summary>
    private bool _shakeActive;
    private float _shakeAmplitude;
    private float _shakeDuration;
    private float _shakeElapsed;

    /// <summary>Acercamiento en curso: entrada/hueco/salida con suavizado (<see cref="Ease"/>), encima de la cámara ya encajada — nunca vuelve a llamar a <c>SolvePerspectiveFit</c>.</summary>
    private bool _punchActive;
    private Vector3 _punchTarget;
    private float _punchZoom = 1f;
    private float _punchInSeconds;
    private float _punchHoldSeconds;
    private float _punchOutSeconds;
    private float _punchElapsed;

    /// <summary>True desde que <see cref="ReleasePunch"/> corta el hueco a mano: la salida se mide desde <see cref="_punchReleasedAmount"/>, no desde 1.</summary>
    private bool _punchReleased;
    private float _punchReleasedAmount;
    private float _punchReleaseElapsed;

    // ------------------------------------------------------------------ sangre persistente (RA-027)

    private readonly List<BloodMark> _bloodMarks = new();
    private readonly List<MeshInstance3D> _bloodDecals = new();

    /// <summary>Elevación de la cámara en grados sobre el césped. Es <c>[Export]</c> para poder barrerla en las capturas.</summary>
    [Export]
    public float Elevation { get; set; } = 60f;

    /// <summary>
    /// Alto del encuadre ortográfico en unidades de mundo (Godot mide el <c>Size</c> ortográfico en
    /// vertical). Con el rectángulo del campo de la pantalla —1120x350, relación 3,2— 5,3 de alto son 17,0
    /// de ancho: las 16 columnas con media casilla de margen a cada lado, la misma en los tres ángulos. Es
    /// el ancho el que manda: 5 filas en tres cuartos nunca llenan un rectángulo tan apaisado.
    /// </summary>
    [Export]
    public float OrthoSize { get; set; } = 5.3f;

    /// <summary>
    /// Desplaza el centro de la cámara por su propio eje "arriba" en pantalla, en unidades de mundo
    /// (revisión visual del orquestador, 19 sep 2026, bloque <c>MatchPitchView3D.cs</c> del parche de
    /// prototipo): con la retransmisión a campo entero, el césped queda centrado verticalmente si no se
    /// corrige, y la composición validada (<c>docs/ui/capturas/base-1280x800.jpg</c>) lo quiere bajado —
    /// el tablero se lleva más margen arriba que las tiras abajo. Positivo baja el campo en pantalla.
    /// </summary>
    [Export]
    public float PanUp { get; set; }

    /// <summary>
    /// Color plano fuera del césped (revisión visual, 19 sep 2026): por defecto el mismo
    /// <see cref="Style.Background"/> oscuro del modo depuración (ADR 0102, cápsulas grises de prueba),
    /// pero la retransmisión a campo entero lo sustituye por un verde de alrededores plano — sin gradas ni
    /// vallas (regla 10 de <c>CLAUDE.md</c>) — para que fuera del rectángulo de 16x7 no se vea negro.
    /// </summary>
    [Export]
    public Color SurroundColor { get; set; } = Style.Background;

    /// <summary>Cápsulas negras planas sobre suelo blanco, sin color de equipo: la prueba literal de RA-002.</summary>
    [Export]
    public bool SilhouetteMode { get; set; }

    /// <summary>
    /// Proyección en perspectiva en vez de ortográfica (revisión del orquestador, 19 sep 2026: «el campo
    /// debe tener más 3D, más profundidad»). Por defecto <c>false</c>: el modo depuración
    /// (<see cref="Screens.MatchScreen"/>) no toca esta propiedad y se queda exactamente como en el ADR
    /// 0102 (ortográfico fijo en tres cuartos). Solo <see cref="Screens.BroadcastScreen"/> la activa.
    /// </summary>
    [Export]
    public bool Perspective { get; set; }

    /// <summary>
    /// Campo de visión <b>vertical</b> en grados (la cámara mantiene <c>KeepAspectEnum.Height</c>), solo
    /// leído cuando <see cref="Perspective"/> está activo. Con <see cref="Perspective"/> apagado no hace
    /// nada: el ortográfico no tiene FOV.
    /// </summary>
    [Export]
    public float Fov { get; set; } = 35f;

    /// <summary>
    /// Pistas de profundidad alrededor del campo — césped gastado, vallas con patrocinadores de parodia y
    /// grada con público — todo marcador de posición procedural (regla 10 de <c>CLAUDE.md</c>: nada de
    /// texturas ni modelos importados). Por defecto <c>false</c>: el modo depuración se queda con el
    /// suelo y el fondo plano del ADR 0102, sin nada alrededor.
    /// </summary>
    [Export]
    public bool Stadium { get; set; }

    /// <summary>Traza del partido; null mientras no haya partido reproducido.</summary>
    public MatchTrace? Trace { get; private set; }

    /// <summary>Fotograma que se está pintando (índice, no tick). Lo pone la pantalla, igual que en 2D.</summary>
    public int Frame { get; set; }

    /// <summary>Fracción 0..1 hacia el fotograma siguiente. Solo suaviza el dibujo (RT-020).</summary>
    public float Alpha { get; set; }

    /// <summary>
    /// Marcas de perk del partido (<see cref="MatchFlashView"/>, ubicadas por
    /// <see cref="Sim.Run.View.MatchMomentView"/>): las carteles de pergamino que <see cref="DrawMarks"/>
    /// ancla sobre el jugador. No pasan por el director de presentación (ADR 0119, regla 5) — el 3D las
    /// pinta directamente igual que <see cref="MatchPitchView.Flashes"/> las pinta en 2D.
    /// </summary>
    public IReadOnlyList<MomentMark> Marks { get; set; } = System.Array.Empty<MomentMark>();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
        EnsureBuilt();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        AdvanceGestures((float)delta);
        ApplyCamera();
        ApplyPalette();
        ApplyTrace();
        ApplyBloodMarks();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawMarks();
    }

    /// <summary>
    /// Empareja la traza con el plantel del partido para saber <b>de qué raza</b> es cada ficha: la traza
    /// no la lleva, así que sale de <c>Playback.Setup</c> por id dentro de cada equipo, que es el mismo
    /// recorrido que hace la ventana de sustitución. El radio sale de <c>/data</c> (<c>bodyRadius</c>) y la
    /// altura de la proporción de RA-002.
    /// </summary>
    public void Bind(MatchTrace? trace, MatchSetup? setup, Catalog? catalog)
    {
        EnsureBuilt();
        Trace = trace;

        foreach (var body in _bodies)
        {
            body.QueueFree();
        }

        foreach (var ring in _rings)
        {
            ring.QueueFree();
        }

        foreach (var number in _numbers)
        {
            number.QueueFree();
        }

        _bodies.Clear();
        _models.Clear();
        _rings.Clear();
        _numbers.Clear();
        _heights.Clear();
        _radii.Clear();

        if (trace is null || setup is null || catalog is null)
        {
            return;
        }

        for (int i = 0; i < trace.Players.Count; i++)
        {
            var player = trace.Players[i];
            var race = RaceOf(setup, player);
            float radius = catalog.Race(race).BodyRadius / 100f;
            var proportion = RaceProportions.TryGetValue(race, out var found) ? found : RaceProportions[Race.Human];
            float height = proportion.Height / proportion.Width * (radius * 2f);

            var body = new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = radius, Height = height, RadialSegments = 28, Rings = 12 },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            };
            _world.AddChild(body);
            _bodies.Add(body);
            _heights.Add(height);
            _radii.Add(radius);

            // MAQUETA (23 sep 2026, encargo del revisor): solo los humanos llevan modelo, para poder
            // comparar las dos cosas en la misma imagen. Si el modelo no está, TryCreate devuelve null y
            // el jugador se queda con su cápsula: la maqueta no puede romper la vista.
            PlayerModel? model = null;
            if (race == Race.Human)
            {
                model = PlayerModel.TryCreate(height);
                if (model is not null)
                {
                    // La cápsula se queda sin malla y pasa a ser solo el hueso que transforma al modelo:
                    // posición, altura y postura las sigue mandando ApplyTrace, sin enterarse de nada.
                    body.Mesh = null;
                    body.AddChild(model);
                }
            }

            _models.Add(model);

            var ring = new MeshInstance3D
            {
                Mesh = _ringMesh,
                MaterialOverride = RingMaterial(player.Team),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Scale = new Vector3(radius, 1f, radius),
            };
            _world.AddChild(ring);
            _rings.Add(ring);
            _world.AddChild(NewNumber(player, radius, out var label));
            _numbers.Add(label);
        }

        _appliedSilhouette = !SilhouetteMode;
        ApplyPalette();
    }

    /// <summary>
    /// <b>Solo para la secuencia de capturas</b>: reparte las cinco razas de lanzamiento entre las fichas
    /// del campo para poder mirarlas juntas en una sola imagen. Es maquillaje de la vista y nada más —la
    /// traza, el motor y el resultado del partido no se enteran—, igual que <c>ForceTestConsumables</c>
    /// en <c>TeamState</c>.
    /// </summary>
    public void ForceRaceParade(Catalog catalog)
    {
        EnsureBuilt();
        for (int i = 0; i < _bodies.Count; i++)
        {
            var race = LaunchRaces[i % LaunchRaces.Length];
            float radius = catalog.Race(race).BodyRadius / 100f;
            var proportion = RaceProportions[race];
            float height = proportion.Height / proportion.Width * (radius * 2f);

            // Con la maqueta puesta, la ficha que reciba "humano" enseña el modelo y las otras cuatro su
            // cápsula: si se le devolviera la malla a una ficha con modelo se verían las dos cosas a la vez.
            // El desfile REPARTE razas que el partido no tiene —aquí no juega ningún humano—, así que si a
            // esta ficha le toca humano y no traía modelo, se le crea ahora: sin esto la maqueta no se ve
            // en la única captura donde se pueden comparar las cinco razas juntas.
            if (race == Race.Human && _models[i] is null)
            {
                var created = PlayerModel.TryCreate(height);
                if (created is not null)
                {
                    _bodies[i].AddChild(created);
                    _models[i] = created;
                    if (_bodies[i].MaterialOverride is { } current)
                    {
                        created.Paint(current);
                    }
                }
            }

            if (_models[i] is { } humanoid)
            {
                humanoid.Visible = race == Race.Human;
            }

            _bodies[i].Mesh = _models[i] is not null && race == Race.Human
                ? null
                : new CapsuleMesh { Radius = radius, Height = height, RadialSegments = 28, Rings = 12 };
            _rings[i].Scale = new Vector3(radius, 1f, radius);
            _heights[i] = height;
            _radii[i] = radius;
        }
    }

    // ------------------------------------------------------------------ gestos de cámara (docs/ui/README §4)

    /// <summary>
    /// Sacudida de cámara determinista, encima del encuadre ya calculado (nunca vuelve a encajar): un
    /// desplazamiento lateral/vertical con envolvente lineal de 1 a 0 durante <paramref name="seconds"/>
    /// reales de <see cref="_Process"/>, no ticks lógicos — es adorno de presentación (RT-014), la traza
    /// no se entera. <paramref name="amplitude"/> en unidades de mundo (casillas). El patrón es
    /// determinista por construcción (suma de senos de frecuencia fija, ver <see cref="ShakeOffset2D"/>):
    /// nunca <c>System.Random</c>.
    /// </summary>
    public void Shake(float amplitude, float seconds)
    {
        _shakeActive = amplitude > 0f && seconds > 0f;
        _shakeAmplitude = amplitude;
        _shakeDuration = Mathf.Max(seconds, 0.0001f);
        _shakeElapsed = 0f;
    }

    /// <summary>
    /// Acercamiento de cámara: mueve el centro de mira y encoge la distancia (o el <c>Size</c> ortográfico)
    /// hacia <paramref name="target"/> en un factor <paramref name="zoom"/>, con suavizado ease-in/ease-out
    /// —nunca lineal—, encima de la cámara ya encajada por <c>SolvePerspectiveFit</c>/el ortográfico fijo,
    /// sin volver a calcular ninguno de los dos. Entra en <paramref name="inSeconds"/>, se mantiene a fondo
    /// <paramref name="holdSeconds"/> y sale en <paramref name="outSeconds"/> si nadie llama antes a
    /// <see cref="ReleasePunch"/> — que es como se suelta de verdad en la política de gestos
    /// (<c>BroadcastScreen</c>): <paramref name="holdSeconds"/> es solo la red de seguridad.
    /// </summary>
    public void PunchIn(Vector3 target, float zoom, float inSeconds, float holdSeconds, float outSeconds)
    {
        _punchActive = true;
        _punchTarget = target;
        _punchZoom = Mathf.Max(zoom, 1f);
        _punchInSeconds = Mathf.Max(inSeconds, 0.0001f);
        _punchHoldSeconds = Mathf.Max(holdSeconds, 0f);
        _punchOutSeconds = Mathf.Max(outSeconds, 0.0001f);
        _punchElapsed = 0f;
        _punchReleased = false;
        _punchReleaseElapsed = 0f;
    }

    /// <summary>Corta el hueco de <see cref="PunchIn"/> a mano y empieza la salida ya mismo, desde el punto en que estuviera (no necesariamente a fondo). Sin efecto si no hay ningún acercamiento activo.</summary>
    public void ReleasePunch()
    {
        if (!_punchActive || _punchReleased)
        {
            return;
        }

        _punchReleased = true;
        _punchReleasedAmount = PunchAmount();
        _punchReleaseElapsed = 0f;
    }

    /// <summary>Cancela sacudida y acercamiento al instante, sin salida suave: para un <c>SeekTo</c> o un cambio de velocidad (docs/ui/README §4), no para el fin natural de un gesto.</summary>
    public void ResetGestures()
    {
        _shakeActive = false;
        _punchActive = false;
        _punchReleased = false;
    }

    /// <summary>
    /// Sustituye las manchas de sangre persistentes (RA-027): se reconstruyen enteras porque, tras una
    /// sustitución, la reproducción cambia y las manchas de antes ya no valen. La forma irregular de cada
    /// una es determinista por su índice en la lista (<see cref="BuildBloodMesh"/>), nunca por
    /// <c>System.Random</c>.
    /// </summary>
    public void SetBloodMarks(IReadOnlyList<BloodMark> marks)
    {
        EnsureBuilt();

        foreach (var decal in _bloodDecals)
        {
            decal.QueueFree();
        }

        _bloodDecals.Clear();
        _bloodMarks.Clear();
        _bloodMarks.AddRange(marks);

        var material = BloodMaterial();
        for (int i = 0; i < _bloodMarks.Count; i++)
        {
            var mark = _bloodMarks[i];
            var decal = new MeshInstance3D
            {
                Mesh = BuildBloodMesh(i),
                MaterialOverride = material,
                Position = new Vector3(mark.Column, 0.006f, mark.Row),
                Scale = new Vector3(Mathf.Max(mark.Size, 0.05f), 1f, Mathf.Max(mark.Size, 0.05f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            _world.AddChild(decal);
            _bloodDecals.Add(decal);
        }
    }

    private static Race RaceOf(MatchSetup setup, TracePlayer player)
    {
        var squad = player.Team == 0 ? setup.Home.Players : setup.Away.Players;
        foreach (var definition in squad)
        {
            if (definition.Id == player.Id)
            {
                return definition.Race;
            }
        }

        return player.Team == 0 ? setup.Home.Race : setup.Away.Race;
    }

    // ------------------------------------------------------------------ escena

    private void EnsureBuilt()
    {
        if (_built)
        {
            return;
        }

        _built = true;

        _world = new SubViewport
        {
            OwnWorld3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(_world);

        _environment = new WorldEnvironment { Environment = BuildEnvironment(false) };
        _world.AddChild(_environment);

        _camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Near = 0.05f,
            Far = 120f,
            Current = true,
        };
        _world.AddChild(_camera);

        // RA-005: la luz viene de arriba a la izquierda y no se mueve. La sombra en el suelo es lo que
        // dice en qué casilla está cada jugador (RA-008), así que es parte del entregable.
        _sun = new DirectionalLight3D
        {
            ShadowEnabled = true,
            LightEnergy = 0.85f,

            // Sesgos por encima de lo normal. Bajo el renderizador de COMPATIBILIDAD (opengl3, el de las
            // capturas por Xvfb) una cápsula de 0,3 de radio se auto-sombrea con un escalón recto en el
            // ecuador: ni `DisableReceiveShadows` del material ni subir el sesgo lo quitan —comprobado—, y
            // solo desaparece apagando la sombra entera, que se llevaría por delante RA-008. Queda como
            // artefacto conocido del renderizador de las capturas; el proyecto va en Forward+ y encima el
            // toon del ADR 0102 sustituye esta iluminación. Lo que importa aquí es la sombra en el SUELO.
            ShadowBias = 0.12f,
            ShadowNormalBias = 3f,
            ShadowBlur = 1.0f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 18f,
        };
        _world.AddChild(_sun);
        _sun.LookAtFromPosition(new Vector3(1f, 11f, -1.5f), new Vector3(8f, 0f, 4f), Vector3.Up);

        _groundMaterial = new StandardMaterial3D
        {
            Roughness = 1f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };

        _ground = new MeshInstance3D
        {
            Mesh = BuildGroundMesh(),
            MaterialOverride = _groundMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _world.AddChild(_ground);

        if (Stadium)
        {
            BuildStadium();
        }

        _ringMesh = BuildRingMesh();

        _ballMaterial = new StandardMaterial3D
        {
            Roughness = 0.4f,
            Metallic = 0f,
            DisableReceiveShadows = true,
        };
        _ball = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = BallRadius, Height = BallRadius * 2f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = _ballMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        _world.AddChild(_ball);

        // La sombra del balón en vuelo: un disco plano pegado al césped, oscuro y semitransparente. No es
        // una sombra de motor (el balón ya proyecta la suya, pero con una luz en tres cuartos cae lejos y
        // no dice la altura): es un marcador de POSICIÓN en el suelo, que es lo que falta para leer un
        // arco. Invisible mientras el balón va raso.
        _ballShadow = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = BallRadius * 0.95f, Height = 0.01f, RadialSegments = 14, Rings = 2 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0f, 0f, 0f, 0.35f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        _world.AddChild(_ballShadow);

        _appliedSilhouette = !SilhouetteMode;
        ApplyCamera();
        ApplyPalette();
    }

    private Godot.Environment BuildEnvironment(bool silhouette) => new()
    {
        BackgroundMode = Godot.Environment.BGMode.Color,
        BackgroundColor = silhouette ? new Color(1f, 1f, 1f) : SurroundColor,
        AmbientLightSource = Godot.Environment.AmbientSource.Color,
        AmbientLightColor = silhouette ? new Color(1f, 1f, 1f) : new Color(0.62f, 0.68f, 0.78f),

        // En silueta el suelo es blanco y la luz tiene que quedarse muy por debajo de quemarlo: si se
        // satura desaparecen las líneas del campo y con ellas la referencia de casilla, que es la mitad de
        // lo que hay que juzgar.
        AmbientLightEnergy = silhouette ? 0.34f : 0.45f,
        TonemapMode = Godot.Environment.ToneMapper.Linear,
    };

    /// <summary>
    /// El césped: un cuadrilátero de 16x7 en el plano XZ (sucesora de la ADR 0103) con los vértices y las UV puestos a mano. Se
    /// construye así y no con un <c>PlaneMesh</c> para que no haya ninguna duda sobre en qué esquina de la
    /// textura cae la casilla (0,0): la columna es X, la fila es Z y la textura se lee igual que la imagen.
    /// </summary>
    private static ArrayMesh BuildGroundMesh()
    {
        var vertices = new Vector3[]
        {
            new(0f, 0f, 0f),
            new(Pitch.Columns, 0f, 0f),
            new(Pitch.Columns, 0f, Pitch.Rows),
            new(0f, 0f, Pitch.Rows),
        };
        var uvs = new Vector2[] { new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f) };
        var normals = new Vector3[] { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
        var indices = new int[] { 0, 1, 2, 0, 2, 3 };

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>
    /// La corona plana del anillo de suelo, de radio 1: se escala por jugador al radio de su cuerpo, así
    /// que <b>el anillo es la huella real</b> que usa <c>BodySeparation</c> y no una decoración de tamaño
    /// libre. Va en el plano XZ, sin espesor y sin proyectar sombra: lo que tiene que verse debajo es la
    /// sombra de la cápsula (RA-008), no otra sombra más.
    /// </summary>
    private static ArrayMesh BuildRingMesh()
    {
        const int Steps = 48;
        const float Inner = 0.82f;

        var vertices = new Vector3[Steps * 2];
        var normals = new Vector3[Steps * 2];
        for (int i = 0; i < Steps; i++)
        {
            float angle = Mathf.Tau * i / Steps;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = direction * Inner;
            vertices[(i * 2) + 1] = direction;
            normals[i * 2] = Vector3.Up;
            normals[(i * 2) + 1] = Vector3.Up;
        }

        var indices = new int[Steps * 6];
        for (int i = 0; i < Steps; i++)
        {
            int a = i * 2;
            int b = ((i + 1) % Steps) * 2;
            indices[i * 6] = a;
            indices[(i * 6) + 1] = a + 1;
            indices[(i * 6) + 2] = b + 1;
            indices[(i * 6) + 3] = a;
            indices[(i * 6) + 4] = b + 1;
            indices[(i * 6) + 5] = b;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // ------------------------------------------------------------------ sangre persistente (RA-027)

    /// <summary>
    /// Disco de radio 1 (se escala por <c>BloodMark.Size</c> al usarlo) con el borde irregular — una
    /// calcomanía de sangre, no un círculo perfecto — determinista por <paramref name="index"/>: el mismo
    /// índice da siempre el mismo contorno, con <see cref="Hash01"/> en vez de <c>System.Random</c>.
    /// </summary>
    /// <summary>
    /// Relleno de la mancha: <c>Pregon.Blood</c> (b3121b, el rojo vivo de la UI) oscurecido ~25% — rojo
    /// oscuro SATURADO, no negro (revisión del orquestador, 19 sep 2026: la primera versión, un albedo
    /// plano sin más, salía casi negra bajo el renderizador de las capturas). El borde va más oscuro
    /// todavía, por vértice (degradado Gouraud, sin textura): así se lee como una mancha con cuerpo, no
    /// un disco de un solo tono.
    /// </summary>
    private static readonly Color BloodFill = new("860d14");
    private static readonly Color BloodEdge = new("52080d");

    private static ArrayMesh BuildBloodMesh(int index)
    {
        const int Steps = 11;

        var vertices = new Vector3[Steps + 1];
        var normals = new Vector3[Steps + 1];
        var colors = new Color[Steps + 1];
        vertices[0] = Vector3.Zero;
        normals[0] = Vector3.Up;
        colors[0] = BloodFill;
        for (int i = 0; i < Steps; i++)
        {
            float angle = Mathf.Tau * i / Steps;
            float wobble = 0.55f + (0.45f * Hash01(index, i));
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * wobble;
            normals[i + 1] = Vector3.Up;
            colors[i + 1] = BloodEdge;
        }

        var indices = new int[Steps * 3];
        for (int i = 0; i < Steps; i++)
        {
            indices[i * 3] = 0;
            indices[(i * 3) + 1] = i + 1;
            indices[(i * 3) + 2] = ((i + 1) % Steps) + 1;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Hash determinista de dos enteros a [0,1) (clásico truco de seno·escala, sin estado ni <c>System.Random</c>): la misma pareja da siempre el mismo número.</summary>
    private static float Hash01(int index, int i)
    {
        float v = Mathf.Sin((index * 12.9898f) + (i * 78.233f)) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    /// <summary>Sin sombra propia (se lee como parte del suelo, no como un objeto sobre él); el color viene del vértice (<see cref="BuildBloodMesh"/>), no de un albedo plano.</summary>
    private static StandardMaterial3D BloodMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>
    /// Anillo y dorsal van <b>sin prueba de profundidad</b>. No es un capricho: la cápsula tiene
    /// exactamente el radio del anillo, así que desde tres cuartos se come todo el anillo menos una uña de
    /// medio radio por delante, y el dorsal entero. Con un modelo de verdad —más estrecho que un cilindro
    /// de radio completo, sobre todo por las piernas— el anillo se vería solo y esto se puede quitar. El
    /// precio, mientras tanto: el anillo de quien está detrás se pinta encima de la cápsula de quien está
    /// justo delante en la misma columna.
    /// </summary>
    private static StandardMaterial3D RingMaterial(int team) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = new Color(team == 0 ? Style.TeamOwn : Style.TeamRival, 0.92f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        NoDepthTest = true,
    };

    /// <summary>
    /// El dorsal tumbado en el suelo, dentro del anillo (<c>Label3D</c> girado -90° sobre X: su cara pasa a
    /// mirar hacia arriba y su vertical hacia el fondo del campo, que es arriba en pantalla). Se dimensiona
    /// contra el radio del cuerpo, no en píxeles, para que un no-muerto y un orco lo lleven proporcionado.
    /// </summary>
    private static Label3D NewNumber(TracePlayer player, float radius, out Label3D label)
    {
        const int FontPixels = 64;
        var tint = (player.Team == 0 ? Style.TeamOwn : Style.TeamRival).Lightened(0.62f);

        label = new Label3D
        {
            Text = player.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FontSize = FontPixels,

            // Alto del texto ~1,4 radios: dos cifras caben de sobra dentro del anillo (2 radios de diámetro)
            // y siguen siendo legibles con la compresión vertical de los tres cuartos.
            PixelSize = radius * 1.4f / FontPixels,
            Modulate = tint,
            OutlineSize = 14,
            OutlineModulate = new Color(Style.Background, 0.95f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            NoDepthTest = true,
            DoubleSided = true,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
        };
        return label;
    }

    // ------------------------------------------------------------------ sincronización con el tick

    private void ApplyCamera()
    {
        // El modo depuración (Perspective apagado por defecto) no pasa por aquí en absoluto más que para
        // fijar el tipo de proyección: con Perspective=false esta línea deja la cámara exactamente como
        // antes de este cambio (ADR 0102), y ApplyOrthographicCamera es el cuerpo íntegro de la vieja
        // ApplyCamera, sin tocar.
        _camera.Projection = Perspective ? Camera3D.ProjectionType.Perspective : Camera3D.ProjectionType.Orthogonal;

        if (Perspective)
        {
            ApplyPerspectiveCamera();
        }
        else
        {
            ApplyOrthographicCamera();
        }
    }

    private void ApplyOrthographicCamera()
    {
        float elevation = Mathf.DegToRad(Mathf.Clamp(Elevation, 5f, 89f));
        var elevationUp = new Vector3(0f, Mathf.Cos(elevation), -Mathf.Sin(elevation));
        var center = new Vector3(Pitch.Columns / 2f, 0.35f, Pitch.Rows / 2f);

        // Desplazamiento vertical (revisión visual, 19 sep 2026): mover cámara y centro juntos por el eje
        // "arriba" real de la pantalla —(0, cos(elevación), -sen(elevación)), no el eje Y del mundo— baja
        // el campo en pantalla sin cambiar el encuadre ortográfico ni la elevación.
        center += elevationUp * PanUp;

        // Gestos (docs/ui/README §4), encima del encuadre de siempre: el modo depuración nunca los activa
        // (Shake/PunchIn no los llama nadie ahí), así que con los dos en reposo esto da exactamente lo
        // mismo que antes (zoomFactor=1, gestureCenter=center, shake=Vector3.Zero).
        var (gestureCenter, zoomFactor) = ApplyPunch(center);
        DebugCenter = gestureCenter;
        DebugDistance = CameraDistance;
        DebugZoomFactor = zoomFactor;

        // La cámara mira desde el lado +Z: así el eje X del campo cae a la derecha de la pantalla y las
        // filas crecen hacia abajo, exactamente como en la vista 2D.
        var from = gestureCenter + (new Vector3(0f, Mathf.Sin(elevation), Mathf.Cos(elevation)) * CameraDistance);

        var shake = ShakeWorldOffset(elevationUp);
        from += shake;
        gestureCenter += shake;

        _camera.LookAtFromPosition(from, gestureCenter, Vector3.Up);

        // El ortográfico no tiene distancia que encoger (no cambia el encuadre): el acercamiento aquí es
        // el Size, al revés que en perspectiva.
        _camera.Size = Mathf.Max(OrthoSize / zoomFactor, 0.5f);
    }

    /// <summary>
    /// Encaje automático en perspectiva (revisión del orquestador, 19 sep 2026): en vez de una fórmula
    /// cerrada —la proyección de un rectángulo inclinado no tiene una despejable simple para FOV y
    /// elevación arbitrarios—, se mide la posición en pantalla de las cuatro esquinas del césped con la
    /// propia cámara (<see cref="Camera3D.UnprojectPosition"/>) y se ajustan por bisección la distancia
    /// (ancho/alto) y el desplazamiento vertical (posición dentro del hueco), en <see cref="SolvePerspectiveFit"/>.
    /// El resultado se cachea en <see cref="_fitDistance"/>/<see cref="_fitPan"/> y solo se recalcula si
    /// cambian <see cref="Elevation"/>, <see cref="Fov"/> o el tamaño del viewport.
    /// </summary>
    private void ApplyPerspectiveCamera()
    {
        EnsurePerspectiveFit();

        float elevation = Mathf.DegToRad(Mathf.Clamp(Elevation, 5f, 89f));
        var elevationUp = new Vector3(0f, Mathf.Cos(elevation), -Mathf.Sin(elevation));
        var center = new Vector3(Pitch.Columns / 2f, 0f, Pitch.Rows / 2f) + (elevationUp * _fitPan);

        // Gestos (docs/ui/README §4), encima del encaje ya calculado por EnsurePerspectiveFit — nunca
        // vuelve a llamar a SolvePerspectiveFit: el acercamiento solo mueve el centro y encoge la
        // distancia ya encajada.
        var (gestureCenter, zoomFactor) = ApplyPunch(center);
        float distance = _fitDistance / zoomFactor;
        DebugCenter = gestureCenter;
        DebugDistance = distance;
        DebugZoomFactor = zoomFactor;
        var from = gestureCenter + (new Vector3(0f, Mathf.Sin(elevation), Mathf.Cos(elevation)) * distance);

        var shake = ShakeWorldOffset(elevationUp);
        from += shake;
        gestureCenter += shake;

        _camera.Fov = Mathf.Clamp(Fov, 1f, 179f);
        _camera.LookAtFromPosition(from, gestureCenter, Vector3.Up);
    }

    // ------------------------------------------------------------------ diagnóstico (BroadcastCapture)

    /// <summary>Centro de mira efectivo del último fotograma (con el gesto ya mezclado, si había alguno): para medir un acercamiento en vez de suponerlo.</summary>
    public Vector3 DebugCenter { get; private set; }

    /// <summary>Distancia de cámara efectiva del último fotograma (perspectiva) — en ortográfico es <see cref="CameraDistance"/> sin más, el zoom ahí va por <see cref="Camera3D.Size"/>.</summary>
    public float DebugDistance { get; private set; }

    /// <summary>Factor de zoom del acercamiento en el último fotograma: 1 = sin acercamiento.</summary>
    public float DebugZoomFactor { get; private set; }

    /// <summary>Las cuatro esquinas del césped proyectadas con la cámara TAL COMO ESTÁ AHORA (tras el último <c>ApplyCamera</c>): orden (0,0,0)/(Columnas,0,0)/(0,0,Filas)/(Columnas,0,Filas).</summary>
    public Vector2[] DebugPitchCorners() => new[]
    {
        _camera.UnprojectPosition(new Vector3(0f, 0f, 0f)),
        _camera.UnprojectPosition(new Vector3(Pitch.Columns, 0f, 0f)),
        _camera.UnprojectPosition(new Vector3(0f, 0f, Pitch.Rows)),
        _camera.UnprojectPosition(new Vector3(Pitch.Columns, 0f, Pitch.Rows)),
    };

    /// <summary>Las dos esquinas de la boca de la portería del equipo <paramref name="team"/> (suelo, y=0) proyectadas con la cámara actual — para comprobar que caen sobre la línea de fondo pintada, no a un lado.</summary>
    public Vector2[] DebugGoalMouth(int team)
    {
        float goalLineX = team == 0 ? 0f : Pitch.Columns;
        float mid = Pitch.Rows / 2f;
        return new[]
        {
            _camera.UnprojectPosition(new Vector3(goalLineX, 0f, mid - 0.9f)),
            _camera.UnprojectPosition(new Vector3(goalLineX, 0f, mid + 0.9f)),
        };
    }

    /// <summary>El centro/distancia (u <see cref="OrthoSize"/>) que habría SIN ningún gesto, para comparar contra <see cref="DebugCenter"/>/<see cref="DebugDistance"/>/<see cref="DebugZoomFactor"/> sin tener que capturar otro fotograma aparte.</summary>
    public (Vector3 Center, float Distance) DebugUnpunchedRig()
    {
        float elevation = Mathf.DegToRad(Mathf.Clamp(Elevation, 5f, 89f));
        var elevationUp = new Vector3(0f, Mathf.Cos(elevation), -Mathf.Sin(elevation));
        return Perspective
            ? (new Vector3(Pitch.Columns / 2f, 0f, Pitch.Rows / 2f) + (elevationUp * _fitPan), _fitDistance)
            : (new Vector3(Pitch.Columns / 2f, 0.35f, Pitch.Rows / 2f) + (elevationUp * PanUp), CameraDistance);
    }

    /// <summary>La proyección de <paramref name="world"/> con la cámara TAL COMO ESTÁ AHORA (gesto incluido, si hay uno activo).</summary>
    public Vector2 DebugProject(Vector3 world) => _camera.UnprojectPosition(world);

    /// <summary>
    /// La proyección de <paramref name="world"/> con el encuadre de <see cref="DebugUnpunchedRig"/> —sin
    /// ningún gesto—, para comparar contra <see cref="DebugProject"/> sin tener que capturar otro
    /// fotograma aparte. Cambia la transformada de la cámara para medir y la repone antes de devolver el
    /// control: no deja rastro en lo que se dibuje después.
    /// </summary>
    public Vector2 DebugProjectUnpunched(Vector3 world)
    {
        var (center, distance) = DebugUnpunchedRig();
        float elevation = Mathf.DegToRad(Mathf.Clamp(Elevation, 5f, 89f));
        var from = center + (new Vector3(0f, Mathf.Sin(elevation), Mathf.Cos(elevation)) * distance);

        var savedTransform = _camera.GlobalTransform;
        _camera.LookAtFromPosition(from, center, Vector3.Up);
        var projected = _camera.UnprojectPosition(world);
        _camera.GlobalTransform = savedTransform;
        return projected;
    }

    // ------------------------------------------------------------------ matemática de los gestos

    /// <summary>Avanza los relojes reales de sacudida y acercamiento (RT-020 no aplica: esto es <c>/Game</c>, tiempo real de presentación, no ticks lógicos).</summary>
    private void AdvanceGestures(float delta)
    {
        if (_shakeActive)
        {
            _shakeElapsed += delta;
            if (_shakeElapsed >= _shakeDuration)
            {
                _shakeActive = false;
            }
        }

        if (!_punchActive)
        {
            return;
        }

        if (_punchReleased)
        {
            _punchReleaseElapsed += delta;
            if (_punchReleaseElapsed >= _punchOutSeconds)
            {
                _punchActive = false;
            }

            return;
        }

        _punchElapsed += delta;
        if (_punchElapsed >= _punchInSeconds + _punchHoldSeconds + _punchOutSeconds)
        {
            // Nadie llamó a ReleasePunch a tiempo: se apaga solo por la red de seguridad del propio hueco.
            _punchActive = false;
        }
    }

    /// <summary>0 (sin acercar) .. 1 (a fondo), con <see cref="Ease"/> en la entrada y en la salida.</summary>
    private float PunchAmount()
    {
        if (!_punchActive)
        {
            return 0f;
        }

        if (_punchReleased)
        {
            float t = Mathf.Clamp(_punchReleaseElapsed / _punchOutSeconds, 0f, 1f);
            return _punchReleasedAmount * (1f - Ease(t));
        }

        if (_punchElapsed < _punchInSeconds)
        {
            return Ease(_punchElapsed / _punchInSeconds);
        }

        float afterIn = _punchElapsed - _punchInSeconds;
        if (afterIn < _punchHoldSeconds)
        {
            return 1f;
        }

        float outT = Mathf.Clamp((afterIn - _punchHoldSeconds) / _punchOutSeconds, 0f, 1f);
        return 1f - Ease(outT);
    }

    /// <summary>Suavizado cúbico (smoothstep), ease-in/ease-out simétrico: 0 y 1 con derivada nula, nunca lineal.</summary>
    private static float Ease(float t) => t <= 0f ? 0f : (t >= 1f ? 1f : t * t * (3f - (2f * t)));

    /// <summary>El centro y el factor de zoom ya mezclados con el acercamiento en curso (1 = sin acercamiento).</summary>
    /// <summary>
    /// Amplía ALREDEDOR de <see cref="_punchTarget"/>, no recentra la escena en él (revisión del
    /// orquestador, 19 sep 2026: recentrar deja hueco vacío al lado contrario y mueve todo lo que no es
    /// el objetivo). La fórmula clásica de "zoom hacia un punto" —<c>center' = lerp(center, target, 1 -
    /// 1/zoom)</c>, con la distancia (o el <c>Size</c> ortográfico) dividida por el mismo <c>zoom</c>— deja
    /// el píxel proyectado de <c>target</c> fijo: el nuevo ojo cae exactamente sobre el rayo ojo-objetivo
    /// de antes (demostrable por álgebra: <c>from' = target - (target-from)/zoom</c>), así que la
    /// proyección de <c>target</c> no depende de la distancia, solo de la dirección, que no cambia.
    /// </summary>
    private (Vector3 Center, float ZoomFactor) ApplyPunch(Vector3 center)
    {
        float amount = PunchAmount();
        if (amount <= 0f)
        {
            return (center, 1f);
        }

        float zoomFactor = Mathf.Lerp(1f, _punchZoom, amount);
        float towardTarget = 1f - (1f / zoomFactor);
        return (center.Lerp(_punchTarget, towardTarget), zoomFactor);
    }

    /// <summary>
    /// Desplazamiento 2D de la sacudida en este instante: suma de senos de frecuencia FIJA (nunca
    /// <c>System.Random</c>, determinista por construcción — la misma entrada da siempre la misma salida)
    /// con una envolvente que decae linealmente de 1 a 0 durante toda la duración: un gesto de cámara, no
    /// un temblor aleatorio.
    /// </summary>
    private Vector2 ShakeOffset2D()
    {
        if (!_shakeActive)
        {
            return Vector2.Zero;
        }

        float envelope = 1f - Mathf.Clamp(_shakeElapsed / _shakeDuration, 0f, 1f);
        float t = _shakeElapsed;
        float x = (Mathf.Sin(t * 37.1f) + (0.5f * Mathf.Sin((t * 61.7f) + 2.1f))) / 1.5f;
        float y = (Mathf.Sin((t * 29.3f) + 1.7f) + (0.5f * Mathf.Sin((t * 53.9f) + 0.4f))) / 1.5f;
        return new Vector2(x, y) * _shakeAmplitude * envelope;
    }

    /// <summary>La sacudida en unidades de mundo, sobre el eje X del campo y el eje "arriba en pantalla" (<paramref name="elevationUp"/>) — nunca el eje Y del mundo, igual que <see cref="PanUp"/>.</summary>
    private Vector3 ShakeWorldOffset(Vector3 elevationUp)
    {
        var offset = ShakeOffset2D();
        return offset == Vector2.Zero ? Vector3.Zero : (Vector3.Right * offset.X) + (elevationUp * offset.Y);
    }

    /// <summary>Último ajuste calculado por <see cref="SolvePerspectiveFit"/>, para no repetir la bisección cada fotograma.</summary>
    private float _fitElevation = float.NaN;
    private float _fitFov = float.NaN;
    private Vector2I _fitViewportSize;
    private float _fitDistance = CameraDistance;
    private float _fitPan;

    private void EnsurePerspectiveFit()
    {
        var size = _world.Size;
        if (size.X <= 0 || size.Y <= 0)
        {
            // El SubViewport todavía no ha recibido su tamaño final del contenedor (primer fotograma tras
            // construirse): se deja el último ajuste conocido para este fotograma y se reintenta en el
            // siguiente, en vez de encajar contra un tamaño de 0x0.
            return;
        }

        float elevation = Mathf.Clamp(Elevation, 5f, 89f);
        float fov = Mathf.Clamp(Fov, 1f, 179f);
        if (Mathf.IsEqualApprox(_fitElevation, elevation) && Mathf.IsEqualApprox(_fitFov, fov) && _fitViewportSize == size)
        {
            return;
        }

        (_fitDistance, _fitPan) = SolvePerspectiveFit(Mathf.DegToRad(elevation), fov, size);
        _fitElevation = elevation;
        _fitFov = fov;
        _fitViewportSize = size;
    }

    /// <summary>
    /// Rectángulo de pantalla destinado al campo, escalado al tamaño real del viewport de esta vista:
    /// ancho 45-1235 (sigue mandando en la bisección de distancia), <see cref="FieldRect"/> alto de
    /// referencia 200 (solo para acotar cuánto puede crecer el hueco vertical, ver
    /// <see cref="SolvePerspectiveFit"/>) y el <b>ancla</b> del borde cercano en 690 — no 665 — con el
    /// límite duro de las tiras en 735 (revisión del orquestador, 19 sep 2026, variante D elegida: a 602
    /// sobraba hueco antes de las tiras; 690 deja 45px de margen contra las tiras y sube la grada, que
    /// gana alto por arriba). Es el objetivo que persigue <see cref="SolvePerspectiveFit"/>.
    /// </summary>
    private static (float XMin, float XMax, float YMin, float YNearTarget, float YHardMax) FieldRect(Vector2I viewportSize)
    {
        float scaleX = viewportSize.X / 1280f;
        float scaleY = viewportSize.Y / 800f;
        return (45f * scaleX, 1235f * scaleX, 200f * scaleY, 690f * scaleY, 735f * scaleY);
    }

    /// <summary>
    /// Bisección en dos fases, sin fórmula cerrada (ver <see cref="ApplyPerspectiveCamera"/>): primero la
    /// <b>distancia</b> de la cámara —cuanto menos, más grande y más "3D" sale el campo— hasta la menor
    /// que sigue cabiendo en ancho y en alto contra el <b>límite duro</b> (nunca las tiras); luego el
    /// <b>desplazamiento vertical</b> —mismo eje "arriba en pantalla" que <see cref="PanUp"/>, aplicado a
    /// cámara y centro por igual— para anclar el borde <b>cercano</b> (el más abajo en pantalla, el que
    /// puede comerse las tiras) exactamente en <see cref="FieldRect"/>.YNearTarget, no para centrar el
    /// hueco: la revisión del 19 sep 2026 pidió bajar el campo y subir la grada, así que el ancla manda
    /// aunque sobre margen por arriba. Ninguna de las dos bisecciones usa <c>System.Random</c> (RT-021 no
    /// aplica aquí —esto es <c>/Game</c>, no <c>/Sim</c>— pero tampoco hace falta: es determinista de
    /// por sí, la misma entrada da siempre la misma salida).
    /// </summary>
    private (float Distance, float Pan) SolvePerspectiveFit(float elevationRad, float fovDeg, Vector2I viewportSize)
    {
        var (xMin, xMax, yMin, yNearTarget, yHardMax) = FieldRect(viewportSize);
        float widthBudget = xMax - xMin;
        float heightHardBudget = yHardMax - yMin;

        var corners = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(Pitch.Columns, 0f, 0f),
            new Vector3(0f, 0f, Pitch.Rows),
            new Vector3(Pitch.Columns, 0f, Pitch.Rows),
        };

        _camera.Fov = fovDeg;

        (float MinX, float MaxX, float MinY, float MaxY) Extent(float distance, float pan)
        {
            var elevationUp = new Vector3(0f, Mathf.Cos(elevationRad), -Mathf.Sin(elevationRad));
            var center = new Vector3(Pitch.Columns / 2f, 0f, Pitch.Rows / 2f) + (elevationUp * pan);
            var from = center + (new Vector3(0f, Mathf.Sin(elevationRad), Mathf.Cos(elevationRad)) * distance);
            _camera.LookAtFromPosition(from, center, Vector3.Up);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (var corner in corners)
            {
                var screen = _camera.UnprojectPosition(corner);
                minX = Mathf.Min(minX, screen.X);
                maxX = Mathf.Max(maxX, screen.X);
                minY = Mathf.Min(minY, screen.Y);
                maxY = Mathf.Max(maxY, screen.Y);
            }

            return (minX, maxX, minY, maxY);
        }

        bool FitsAt(float distance)
        {
            var e = Extent(distance, 0f);
            return (e.MaxX - e.MinX) <= widthBudget && (e.MaxY - e.MinY) <= heightHardBudget;
        }

        // Cota superior defensiva: si con 80 unidades todavía no cabe (FOV muy cerrado o elevación muy
        // rasante), se duplica hasta que quepa. No debería hacer falta con los FOV/elevaciones de las
        // variantes de captura, pero la propiedad es de uso general.
        float hi = 80f;
        while (!FitsAt(hi) && hi < 5000f)
        {
            hi *= 2f;
        }

        float lo = 1f;
        for (int i = 0; i < 40; i++)
        {
            float mid = (lo + hi) / 2f;
            if (FitsAt(mid))
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        float distance = hi;

        // El desplazamiento baja el borde CERCANO de la imagen (MaxY) cuanto más pan positivo se aplique
        // (misma convención que PanUp: positivo baja el campo en pantalla), así que se busca por bisección
        // en vez de despejar — no es lineal por la perspectiva. Se ancla el borde cercano, no se centra el
        // hueco: da igual cuánto sobre por arriba, ahí es donde tiene que crecer la grada.
        float panLo = -20f;
        float panHi = 20f;
        for (int i = 0; i < 40; i++)
        {
            float mid = (panLo + panHi) / 2f;
            float bottom = Extent(distance, mid).MaxY;
            if (bottom < yNearTarget)
            {
                panLo = mid;
            }
            else
            {
                panHi = mid;
            }
        }

        return (distance, (panLo + panHi) / 2f);
    }

    private void ApplyPalette()
    {
        if (_appliedSilhouette == SilhouetteMode)
        {
            return;
        }

        _appliedSilhouette = SilhouetteMode;
        _environment.Environment = BuildEnvironment(SilhouetteMode);
        _sun.LightEnergy = SilhouetteMode ? 0.5f : 0.85f;

        if (SilhouetteMode)
        {
            _whiteTexture ??= BuildGroundTexture(true);
            _groundMaterial.AlbedoTexture = _whiteTexture;
            _groundMaterial.AlbedoColor = new Color(1f, 1f, 1f);
        }
        else
        {
            // Stadium (revisión del orquestador, «más 3D, más profundidad»): césped gastado con franjas de
            // siega y calvas en vez del césped liso de RA-002 — la misma pista de profundidad del
            // prototipo (docs/ui/prototipo/prototipo-ui.patch, BuildWornGrass).
            _grassTexture ??= Stadium ? BuildWornGrass() : BuildGroundTexture(false);
            _groundMaterial.AlbedoTexture = _grassTexture;
            _groundMaterial.AlbedoColor = new Color(1f, 1f, 1f);
        }

        // En silueta el balón también es negro: forma parte de lo que hay que reconocer sin color, y sobre
        // el suelo blanco se sigue igual de bien que la ficha blanca de la vista 2D sobre la hierba.
        _ballMaterial.ShadingMode = SilhouetteMode
            ? BaseMaterial3D.ShadingModeEnum.Unshaded
            : BaseMaterial3D.ShadingModeEnum.PerPixel;
        _ballMaterial.AlbedoColor = SilhouetteMode ? new Color(0.05f, 0.05f, 0.05f) : Style.Ball;

        if (Trace is null)
        {
            return;
        }

        for (int i = 0; i < _bodies.Count && i < Trace.Players.Count; i++)
        {
            var material = BodyMaterial(Trace.Players[i].Team);
            _bodies[i].MaterialOverride = material;

            // El modelo comparte el MISMO material que su cápsula, no una copia: así la atenuación del
            // corte de teletransporte (BA-K), que ApplyTrace escribe sobre el material de la cápsula, le
            // llega también al modelo sin que ApplyTrace tenga que saber que existe.
            _models[i]?.Paint(material);
        }
    }

    /// <summary>
    /// Gris, con el tono del equipo apenas insinuado. Gris porque lo que se está probando es la
    /// <b>geometría</b>, y el color de equipo solo lo justo para no perder de vista quién es quién
    /// (los mismos dos tonos que usa la vista 2D).
    /// </summary>
    private StandardMaterial3D BodyMaterial(int team)
    {
        if (SilhouetteMode)
        {
            return new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.04f, 0.04f, 0.05f),
                // BA-K: transparencia siempre activa (ver ApplyTrace/TeleportCutOpacity) para poder atenuar
                // la cápsula en el corte de un teletransporte sin recrear el material cada vez; con alfa 1
                // constante en el resto de los casos no cambia nada frente a opaco.
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
        }

        // El color de equipo va entero, el mismo que la leyenda y las fichas de la vista 2D: las dos
        // vistas van a convivir y el jugador no puede tener que traducir de una paleta a otra. El gris de
        // «cápsulas grises» se lo queda la luz, que es la que da el volumen.
        return new StandardMaterial3D
        {
            AlbedoColor = team == 0 ? Style.TeamOwn : Style.TeamRival,
            Roughness = 0.85f,
            Metallic = 0f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,

            // La cápsula proyecta sombra en el suelo (RA-008) pero no la recibe. Una superficie tan curva
            // y tan pequeña frente al texel del mapa de sombras se auto-sombrea con un moiré o con un
            // escalón recto, según el sesgo; y su volumen ya lo cuenta el difuso. Además va en la
            // dirección del toon del ADR 0102: la forma la dice la rampa de luz, no la sombra.
            DisableReceiveShadows = true,
        };
    }

    /// <summary>
    /// Visibilidad de las manchas de sangre (RA-027): cada una se ve desde su propio fotograma en
    /// adelante, sin ningún estado propio de "qué se pintó ya" — retroceder o saltar con la barra
    /// simplemente vuelve a evaluar la misma condición contra el <see cref="Frame"/> que toque.
    /// </summary>
    private void ApplyBloodMarks()
    {
        for (int i = 0; i < _bloodDecals.Count; i++)
        {
            _bloodDecals[i].Visible = _bloodMarks[i].Frame <= Frame;
        }
    }

    private void ApplyTrace()
    {
        if (Trace is not { FrameCount: > 0 } trace || _bodies.Count == 0)
        {
            _ball.Visible = false;
            if (_ballShadow is not null)
            {
                _ballShadow.Visible = false;
            }

            return;
        }

        int frame = Mathf.Clamp(Frame, 0, trace.FrameCount - 1);
        for (int i = 0; i < _bodies.Count && i < trace.Players.Count; i++)
        {
            var body = _bodies[i];
            if (!trace.OnPitchAt(frame, i))
            {
                body.Visible = false;
                _rings[i].Visible = false;
                _numbers[i].Visible = false;
                continue;
            }

            body.Visible = true;
            var at = Interpolate(trace, frame, i);

            // BA-K: en el tick de un teletransporte, el modelo se atenúa hasta casi desaparecer justo en
            // el instante del corte (Alpha=0,5) y recupera opacidad hacia los dos bordes del tick — la
            // "cortinilla" que avisa de que el salto fue intencional, no un fallo de interpolación. Fuera
            // de ese tick, alfa 1 constante: no cambia nada de lo que ya se veía. Si el jugador acaba de
            // entrar al campo este mismo tick (sustitución, expulsión revertida...), aparece con una
            // pequeña rampa de aparición en vez de un `Visible = true` seco — mismo aviso, sentido inverso.
            if (body.MaterialOverride is StandardMaterial3D material)
            {
                float opacity;
                if (IsTeleportCut(trace, frame, i))
                {
                    opacity = TeleportCutOpacity(Alpha);
                }
                else if (frame > 0 && !trace.OnPitchAt(frame - 1, i))
                {
                    opacity = Mathf.Clamp(Alpha, 0.15f, 1f);
                }
                else
                {
                    opacity = 1f;
                }

                var color = material.AlbedoColor;
                color.A = opacity;
                material.AlbedoColor = color;
            }

            // En silueta no hay anillo ni dorsal: esa vista existe para comprobar RA-002 en blanco y negro
            // y cualquier cosa que se le añada deja de ser la prueba que es.
            _rings[i].Visible = !SilhouetteMode;
            _numbers[i].Visible = !SilhouetteMode;

            // Dos alturas distintas y mínimas: el anillo pegado al césped y el dorsal un pelo por encima,
            // para que no peleen entre sí por el mismo plano.
            _rings[i].Position = new Vector3(at.X, 0.012f, at.Y);
            _numbers[i].Position = new Vector3(at.X, 0.024f, at.Y);

            // Quien está fuera de la jugada (derribado, lesionado, expulsado) se tumba: con veinte cápsulas
            // grises la postura es lo único que dice de un vistazo quién sigue jugando (UI-002 en 3D).
            bool down = Style.IsDown(trace.StateAt(frame, i));
            var model = _models[i];

            if (model is null)
            {
                body.Transform = down
                    ? new Transform3D(new Basis(new Vector3(0f, 0f, 1f), Mathf.Pi / 2f), new Vector3(at.X, _radii[i], at.Y))
                    : new Transform3D(Basis.Identity, new Vector3(at.X, _heights[i] / 2f, at.Y));
                continue;
            }

            // MAQUETA: al modelo no se le tumba girando el hueso noventa grados —eso deja un cuerpo tieso
            // como una tabla—, se le pone la postura de estar en el suelo. El hueso se queda siempre
            // derecho y el modelo gira hacia donde va.
            body.Transform = new Transform3D(Basis.Identity, new Vector3(at.X, _heights[i] / 2f, at.Y));

            // La velocidad sale de los dos fotogramas que la interpolación ya usa, convertida a casillas
            // por segundo (ticks lógicos a 15/s, RT-020). El modelo solo MIRA lo que la traza escribió: no
            // decide nada del partido (RT-014).
            var here = trace.PositionAt(frame, i);
            var next = frame + 1 < trace.FrameCount && trace.OnPitchAt(frame + 1, i)
                ? trace.PositionAt(frame + 1, i)
                : here;
            var step = new Vector2(next.X - here.X, next.Y - here.Y);
            if (step.Length() > TeleportThresholdCells)
            {
                step = Vector2.Zero;
            }

            model.Pose(step * TicksPerSecond, down);
        }

        var ball = InterpolateBall(trace, frame);
        int carrier = trace.BallOwnerAt(frame);
        float offset = 0f;
        if (carrier >= 0 && carrier < _radii.Count)
        {
            // Igual que en 2D: con dueño el balón está exactamente encima de él, así que se aparta hacia la
            // portería que ataca para que se le vea a los pies y no dentro de la cápsula.
            offset = (trace.Players[carrier].Team == 0 ? 1f : -1f) * (_radii[carrier] + BallRadius + 0.06f);
        }

        // La ALTURA del balón (ADR 0135 pasos 1-2). Hasta el 23 sep 2026 esta vista lo dibujaba a altura
        // constante: la simulación calculaba el vuelo, la traza lo guardaba en `BallHeightAt` y **nadie lo
        // leía** —la métrica tenía cero consumidores en todo el repositorio—, así que el balón se veía
        // raso siempre y el tiro alto, el centro y el palo no se distinguían de un pase al pie. Lo reportó
        // el revisor jugando; la causa se encontró con un grep, sin tocar código.
        //
        // Se interpola entre los dos ticks igual que la posición, con el mismo Alpha, para que el arco sea
        // una curva y no una escalera de 15 escalones por segundo.
        float height = trace.BallHeightAt(frame);
        if (Alpha > 0f && frame + 1 < trace.FrameCount)
        {
            height = Mathf.Lerp(height, trace.BallHeightAt(frame + 1), Alpha);
        }

        _ball.Visible = true;
        _ball.Position = new Vector3(ball.X + offset, BallRadius + Mathf.Max(0f, height), ball.Y);

        // La sombra en el suelo, que es lo que convierte "una pelota más arriba en la pantalla" en "una
        // pelota por el aire": sin una referencia fija en el césped, subir el balón en una cámara en tres
        // cuartos es indistinguible de alejarlo. Se encoge con la altura, como una sombra de verdad.
        if (_ballShadow is not null)
        {
            bool airborne = height > 0.02f;
            _ballShadow.Visible = airborne;
            if (airborne)
            {
                float shrink = 1f / (1f + height * 0.7f);
                _ballShadow.Scale = new Vector3(shrink, 1f, shrink);
                _ballShadow.Position = new Vector3(ball.X + offset, 0.008f, ball.Y);
            }
        }
    }

    /// <summary>
    /// BA-K: por encima de esta distancia entre dos ticks consecutivos, ya no es una zancada (una zancada
    /// real mide 0,13-0,21 casillas/tick, medido en Sim.Tests) sino un teletransporte de <c>/Sim</c> —el
    /// saque de centro reforma diecinueve jugadores de golpe (BB-A), o alguien deja el campo hacia
    /// <c>(-1,-1)</c> (BB-L)—. Misma cota que <c>Sim.Tests.Engine.MatchRulesTests.MaxNormalStepCells</c> y
    /// <c>GoalCelebrationPositionTests.MaxNormalStepCells</c>: no se inventa un número nuevo para el
    /// mismo umbral.
    /// </summary>
    private const float TeleportThresholdCells = 0.6f;

    /// <summary>Ticks lógicos por segundo (RT-020): convierte el paso entre fotogramas en velocidad.</summary>
    private const float TicksPerSecond = 15f;

    /// <summary>
    /// Misma interpolación que <see cref="MatchPitchView"/>: solo dibujo, la traza no se toca (RT-020).
    /// BA-K: cuando el siguiente tick es un teletransporte (ver <see cref="TeleportThresholdCells"/>) o el
    /// jugador va a dejar el campo, deslizar hacia él con <c>Lerp</c> lo enseña cruzando el campo a toda
    /// velocidad (o volando hacia <c>(-1,-1)</c> antes de desaparecer, BB-L) en vez de un corte. Se corta
    /// en el punto medio del tick en vez de deslizar; <see cref="IsTeleportCut"/> usa la misma condición
    /// para atenuar el modelo justo en ese instante (ApplyTrace).
    /// </summary>
    private Vec2 Interpolate(MatchTrace trace, int frame, int player)
    {
        var here = trace.PositionAt(frame, player);
        if (Alpha <= 0f || frame + 1 >= trace.FrameCount || !trace.OnPitchAt(frame + 1, player))
        {
            return here;
        }

        var next = trace.PositionAt(frame + 1, player);
        if (Vec2.Distance(here, next) > TeleportThresholdCells)
        {
            return Alpha < 0.5f ? here : next;
        }

        return new Vec2(Mathf.Lerp(here.X, next.X, Alpha), Mathf.Lerp(here.Y, next.Y, Alpha));
    }

    /// <summary>
    /// Mismo criterio que el corte de <see cref="Interpolate"/>: dice si este fotograma cae dentro del
    /// tick en que un jugador se teletransporta, para que <c>ApplyTrace</c> atenúe el modelo en vez de
    /// dejar que aparezca/desaparezca de golpe sin ningún aviso (BA-K, "cortinilla o transición").
    /// </summary>
    private bool IsTeleportCut(MatchTrace trace, int frame, int player)
    {
        if (Alpha <= 0f || frame + 1 >= trace.FrameCount || !trace.OnPitchAt(frame + 1, player))
        {
            return false;
        }

        return Vec2.Distance(trace.PositionAt(frame, player), trace.PositionAt(frame + 1, player)) > TeleportThresholdCells;
    }

    /// <summary>
    /// Curva de opacidad de la cortinilla: 1 en los dos bordes del tick (0 y 1), mínimo 0,15 justo en el
    /// punto medio (Alpha=0,5) donde <see cref="Interpolate"/> corta de golpe. No baja a 0 del todo para
    /// que la ficha nunca desaparezca por completo -seguiría existiendo si alguien la mira fijamente-, solo
    /// se lee como un parpadeo intencional.
    /// </summary>
    private static float TeleportCutOpacity(float alpha) => Mathf.Clamp(Mathf.Abs(alpha - 0.5f) * 2f, 0.15f, 1f);

    private Vec2 InterpolateBall(MatchTrace trace, int frame)
    {
        var here = trace.BallAt(frame);
        if (Alpha <= 0f || frame + 1 >= trace.FrameCount)
        {
            return here;
        }

        var next = trace.BallAt(frame + 1);
        return new Vec2(Mathf.Lerp(here.X, next.X, Alpha), Mathf.Lerp(here.Y, next.Y, Alpha));
    }

    // ------------------------------------------------------------------ marcas de perk (regla 5, ADR 0119)

    /// <summary>
    /// Proyección 3D→pantalla de un cartel sobre la cabeza del jugador (coordenadas locales del propio
    /// contenedor, las mismas en las que se dibuja <see cref="DrawMarks"/>), o null si no está en el
    /// campo en el fotograma que se pinta.
    /// </summary>
    private Vector2? MarkScreenPosition(int traceIndex)
    {
        if (Trace is not { FrameCount: > 0 } trace || traceIndex < 0 || traceIndex >= trace.Players.Count)
        {
            return null;
        }

        int frame = Mathf.Clamp(Frame, 0, trace.FrameCount - 1);
        if (!trace.OnPitchAt(frame, traceIndex))
        {
            return null;
        }

        var at = trace.PositionAt(frame, traceIndex);
        float height = traceIndex < _heights.Count ? _heights[traceIndex] : 1f;
        var world = new Vector3(at.X, height + 0.35f, at.Y);
        return _camera.UnprojectPosition(world);
    }

    /// <summary>
    /// Carteles de pergamino de <see cref="Marks"/> vivos en el fotograma que se pinta: tamaño fijo en
    /// pantalla, 1 s de vida (<see cref="MatchFlashView.DurationFrames"/>), pequeños si el aviso quedó
    /// absorbido por un momento del director (<see cref="MomentMark.MomentIndex"/> &gt;= 0).
    /// </summary>
    private void DrawMarks()
    {
        if (Trace is not { FrameCount: > 0 } trace || Marks.Count == 0)
        {
            return;
        }

        int frame = Mathf.Clamp(Frame, 0, trace.FrameCount - 1);
        for (int i = 0; i < Marks.Count; i++)
        {
            var flash = Marks[i].Flash;
            int age = frame - flash.Frame;
            if (age < 0 || age >= MatchFlashView.DurationFrames)
            {
                continue;
            }

            var screen = MarkScreenPosition(flash.Player);
            if (screen is null)
            {
                continue;
            }

            DrawPlacard(screen.Value, flash.Name, small: Marks[i].MomentIndex >= 0, seed: (flash.Player * 97) + flash.Frame);
        }
    }

    /// <summary>Un pergamino corto con el nombre del perk, centrado sobre el punto de anclaje.</summary>
    private void DrawPlacard(Vector2 at, string text, bool small, int seed)
    {
        float w = small ? 96f : 156f;
        float h = small ? 24f : 34f;
        int size = small ? 14 : 18;
        var topLeft = at - new Vector2(w / 2f, h + 12f);
        Pregon.DrawParchment(this, topLeft, w, h, Pregon.Vellum, Pregon.VellumEdge, seed, amplitude: 1.2f, edgeWidth: 1.2f);
        Style.DrawText(this, Pregon.DataSemiBold, topLeft + new Vector2(6f, 4f), text, size, Pregon.InkBrown, maxWidth: w - 12f);
    }

    // ------------------------------------------------------------------ textura del suelo

    /// <summary>
    /// El campo pintado en una textura: mitades, cuadrícula, medio campo, círculo central, las dos áreas
    /// (2x4, <see cref="Pitch.AreaColumns"/>) y las porterías, con la misma paleta que la vista 2D. En
    /// silueta la misma geometría en blanco y gris, para no perder la referencia de casilla.
    /// </summary>
    // ------------------------------------------------------------------ estadio (pistas de profundidad, Stadium)

    /// <summary>
    /// Alrededores del campo, marcador de posición procedural (regla 10 de <c>CLAUDE.md</c>): explanada
    /// de tierra más oscura que el césped, vallas bajas con placas de patrocinadores de parodia (RA-025,
    /// «carnicería administrada» en los rótulos, no fútbol de cristal) y una grada escalonada con público
    /// de cápsulas de colores en el lado lejano. Adaptado de
    /// <c>docs/ui/prototipo/prototipo-ui.patch</c> (bloque <c>MatchPitchView3D.cs</c>, <c>BuildStadium</c>):
    /// las medidas ya encajaban con <c>Pitch.Columns</c>/<c>Pitch.Rows</c> (16x7) porque el prototipo se
    /// escribió contra el mismo campo. Determinista: el reparto de color del público usa
    /// <c>RandomNumberGenerator</c> con semilla fija, nunca <c>System.Random</c>.
    /// </summary>
    private void BuildStadium()
    {
        // Explanada: tierra apisonada con hierba rala alrededor del rectángulo de juego, y una franja algo
        // más clara justo delante de la cámara (la "boca" del estadio en la tele).
        AddBox(new Vector3(8f, -0.03f, 3.5f), new Vector3(24f, 0.04f, 13f), new Color("4b5a36"), shadow: false);
        AddBox(new Vector3(8f, -0.02f, -0.45f), new Vector3(18.4f, 0.03f, 0.8f), new Color("6b5a3e"), shadow: false);

        // Vallas de publicidad en la banda del fondo (lado lejano de la cámara): la banda cercana queda
        // libre, como en una retransmisión de verdad, para no tapar nunca al jugador que mira la cámara.
        float segment = 17.6f / SponsorBoards.Length;
        for (int i = 0; i < SponsorBoards.Length; i++)
        {
            float cx = -0.8f + (segment * (i + 0.5f));
            AddBox(new Vector3(cx, 0.24f, -0.42f), new Vector3(segment - 0.06f, 0.48f, 0.08f), SponsorBoards[i].Fill);
            _world.AddChild(NewSponsorLabel(SponsorBoards[i], new Vector3(cx, 0.24f, -0.37f), segment, 0f));
        }

        // Vallas detrás de las porterías: DESCARTADAS (revisión del revisor, 20 sep 2026, tercera pasada).
        // Una valla "paralela a la línea de fondo" tiene su eje largo en Z, el mismo eje por el que mira
        // esta cámara — así que, se vea o no desde aquí, siempre se proyecta en diagonal/escorzada, nunca
        // como un tablón de frente (a 1,2 casillas se leía como "una pila de tablas de canto"; a 1,6, más
        // baja y sin carteles, sencillamente no se distingue del fondo). El revisor autorizó explícitamente
        // dejarlas solo en el lado lejano si esto pasaba (§ese encargo): los patrocinadores siguen ahí, en
        // la valla de más abajo, que sí es frontal a la cámara.

        // Porterías con volumen (postes, larguero y red procedural) en las dos líneas de fondo (columna 0
        // y columna 16), en el color de quien la defiende — sustituyen a la barra plana que antes pintaba
        // BuildWornGrass en la textura del césped.
        BuildGoal(0f, 0);
        BuildGoal(Pitch.Columns, 1);

        // Grada escalonada de madera y piedra con público: cápsulas simples con los colores heráldicos de
        // los dos equipos, más algo de tierra/piedra para no leerse como uniforme. Semilla fija (RT-021 no
        // rige en /Game, pero determinismo por costumbre: la misma grada siempre pinta lo mismo).
        var rng = new RandomNumberGenerator { Seed = 1234 };
        var crowdColors = new[]
        {
            new Color("2f6fd6"), new Color("1d4590"), new Color("d63a2f"), new Color("8e231c"),
            new Color("c9b48a"), new Color("6b5a3e"), new Color("e8dcc0"),
        };
        var body = new CapsuleMesh { Radius = 0.12f, Height = 0.42f, RadialSegments = 8, Rings = 2 };
        var crowdMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };

        for (int tier = 0; tier < 5; tier++)
        {
            float z = -1.05f - (tier * 0.62f);
            float y = 0.18f + (tier * 0.36f);
            AddBox(new Vector3(8f, y / 2f, z), new Vector3(19.5f, y, 0.62f), tier % 2 == 0 ? new Color("5c4632") : new Color("6e5640"));

            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = body, InstanceCount = 62 };
            for (int k = 0; k < multiMesh.InstanceCount; k++)
            {
                float x = -1.4f + (k * 0.305f) + rng.RandfRange(-0.06f, 0.06f);
                float h = rng.RandfRange(0.9f, 1.15f);
                var transform = new Transform3D(
                    Basis.Identity.Scaled(new Vector3(1f, h, 1f)),
                    new Vector3(x, y + (0.2f * h), z + rng.RandfRange(-0.12f, 0.12f)));
                multiMesh.SetInstanceTransform(k, transform);
                multiMesh.SetInstanceColor(k, crowdColors[rng.RandiRange(0, crowdColors.Length - 1)]);
            }

            _world.AddChild(new MultiMeshInstance3D { Multimesh = multiMesh, MaterialOverride = crowdMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        // Remate de la grada: una viga con banderolas alternando los dos equipos.
        AddBox(new Vector3(8f, 2.05f, -4.2f), new Vector3(19.5f, 0.12f, 0.12f), new Color("3a2a1a"));
        for (int f = 0; f < 12; f++)
        {
            var flagColor = f % 2 == 0 ? new Color("2f6fd6") : new Color("d63a2f");
            AddBox(new Vector3(-0.8f + (f * 1.6f), 1.8f, -4.15f), new Vector3(0.5f, 0.45f, 0.03f), flagColor);
        }
    }

    /// <summary>Los mismos rótulos de parodia para las dos vallas (la del fondo lejano y las de las dos porterías): «reutiliza las que ya existen» (revisión del revisor, 20 sep 2026), un solo sitio, no dos catálogos.</summary>
    private static readonly (string Text, Color Fill, Color Ink)[] SponsorBoards =
    {
        ("FUNERARIA EL ÚLTIMO SAQUE", new Color("1c1a1a"), new Color("f1e4c3")),
        ("HIDROMIEL TRAGÓN", new Color("c9982f"), new Color("1c1a1a")),
        ("PRÓTESIS DE ROBLE MAESE TOCÓN", new Color("f1e4c3"), new Color("3a2a1a")),
        ("CARNICERÍA HNOS. TAJO", new Color("8f1d1d"), new Color("f1e4c3")),
        ("UNGÜENTOS LA PATA COJA", new Color("2d5a3a"), new Color("f1e4c3")),
        ("SEGUROS AY MADRE", new Color("1e3a6e"), new Color("c9982f")),
    };

    /// <summary>Un rótulo de valla, girado <paramref name="rotationYDeg"/> grados en Y (0 para la única valla que queda, la del fondo lejano — ver <see cref="BuildStadium"/> sobre por qué no hay vallas en las porterías).</summary>
    private static Label3D NewSponsorLabel((string Text, Color Fill, Color Ink) sponsor, Vector3 position, float segment, float rotationYDeg) => new()
    {
        Text = sponsor.Text,
        Font = Pregon.DataBold,
        FontSize = 96,
        PixelSize = 0.0028f,
        Modulate = sponsor.Ink,
        OutlineSize = 0,
        Position = position,
        RotationDegrees = new Vector3(0f, rotationYDeg, 0f),
        Width = (segment - 0.2f) / 0.0028f,
        AutowrapMode = TextServer.AutowrapMode.Off,
        HorizontalAlignment = HorizontalAlignment.Center,
        Shaded = false,
        DoubleSided = false,
    };

    /// <summary>
    /// Portería con volumen (revisión del revisor, 20 sep 2026, TERCERA pasada: «se ve como un alambre» —
    /// la red de líneas de 1 px no se lee a tamaño real): postes y larguero MÁS GRUESOS
    /// (<c>Post</c> 0,12, antes 0,08) y una red de barras finas de verdad (<see cref="AddNetGrid"/>, cajas
    /// con grosor, no líneas), más densa, blanca y con una sombra suave y plana en el suelo
    /// (<see cref="AddGoalGroundShadow"/>) — se lee como portería a 1920x1080 sin ampliar. Plantada
    /// exactamente sobre la línea de fondo (<paramref name="goalLineX"/> = 0 o
    /// <see cref="Pitch.Columns"/>), y=0, red hacia fuera del campo — comprobado por proyección con
    /// <c>Camera3D.UnprojectPosition</c>, no a ojo. Sustituye a la barra plana que pintaba
    /// <see cref="BuildWornGrass"/> en la textura del césped.
    /// </summary>
    private void BuildGoal(float goalLineX, int team)
    {
        const float HalfWidth = 0.9f;
        const float Height = 0.9f;
        const float Depth = 0.55f;
        const float Post = 0.12f;
        float mid = Pitch.Rows / 2f;

        // Team 0 defiende en X=0 y su red se abre hacia X negativo (fuera del campo); team 1 en X=Columnas
        // hacia X positivo — siempre hacia afuera, nunca invadiendo el rectángulo de juego.
        float dir = team == 0 ? -1f : 1f;
        float backX = goalLineX + (dir * Depth);
        float xMin = Mathf.Min(goalLineX, backX);
        var color = team == 0 ? Style.TeamOwn : Style.TeamRival;

        AddBox(new Vector3(goalLineX, Height / 2f, mid - HalfWidth), new Vector3(Post, Height, Post), color);
        AddBox(new Vector3(goalLineX, Height / 2f, mid + HalfWidth), new Vector3(Post, Height, Post), color);
        AddBox(new Vector3(goalLineX, Height, mid), new Vector3(Post, Post, (HalfWidth * 2f) + Post), color);
        AddGoalGroundShadow(goalLineX, backX, mid - HalfWidth - 0.15f, mid + HalfWidth + 0.15f);

        var net = new Color(0.97f, 0.97f, 0.98f, 0.88f);
        var xAxis = new Vector3(1f, 0f, 0f);
        var yAxis = new Vector3(0f, 1f, 0f);
        var zAxis = new Vector3(0f, 0f, 1f);

        // Fondo: red vertical al final del volumen, de poste a poste y de suelo a larguero.
        AddNetGrid(new Vector3(backX, 0f, mid - HalfWidth), zAxis, yAxis, HalfWidth * 2f, Height, 9, 6, net);

        // Techo: de la boca al fondo, a la altura del larguero.
        AddNetGrid(new Vector3(xMin, Height, mid - HalfWidth), xAxis, zAxis, Depth, HalfWidth * 2f, 4, 9, net);

        // Los dos lados: de la boca al fondo, en cada poste.
        AddNetGrid(new Vector3(xMin, 0f, mid - HalfWidth), xAxis, yAxis, Depth, Height, 4, 6, net);
        AddNetGrid(new Vector3(xMin, 0f, mid + HalfWidth), xAxis, yAxis, Depth, Height, 4, 6, net);
    }

    /// <summary>
    /// Mancha plana y semitransparente en el suelo, bajo la portería (revisión del revisor, 20 sep 2026):
    /// una sombra suave garantizada, sin depender de cómo el renderizador de las capturas sombree postes
    /// finos (ya documentado más arriba como poco fiable para geometría delgada).
    /// </summary>
    private void AddGoalGroundShadow(float x0, float x1, float z0, float z1)
    {
        float xMin = Mathf.Min(x0, x1);
        float xMax = Mathf.Max(x0, x1);
        var vertices = new[]
        {
            new Vector3(xMin, 0.004f, z0), new Vector3(xMax, 0.004f, z0),
            new Vector3(xMax, 0.004f, z1), new Vector3(xMin, 0.004f, z1),
        };
        var normals = new[] { Vector3.Up, Vector3.Up, Vector3.Up, Vector3.Up };
        var indices = new[] { 0, 1, 2, 0, 2, 3 };

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        _world.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0f, 0f, 0f, 0.22f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>
    /// Rejilla de la red de una portería (<see cref="BuildGoal"/>), sobre un rectángulo plano definido por
    /// <paramref name="origin"/> y los dos ejes <paramref name="uAxis"/>/<paramref name="vAxis"/>
    /// (unitarios): barras finas CON GROSOR de verdad —cajas, no <c>Mesh.PrimitiveType.Lines</c> (revisión
    /// del revisor, 20 sep 2026: una línea de 1 px de motor se ve como alambre a cualquier distancia, no
    /// como cuerda) — así que a 1920x1080 sin ampliar se lee como red. Marcador de posición procedural:
    /// nunca una textura ni un modelo importado (regla 10 de <c>CLAUDE.md</c>).
    /// </summary>
    private void AddNetGrid(Vector3 origin, Vector3 uAxis, Vector3 vAxis, float uLen, float vLen, int cellsU, int cellsV, Color color)
    {
        const float Thickness = 0.02f;
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // Hilos "verticales" (a lo largo de v, uno por cada paso de u): grosor en u y en la normal del
        // plano (uAxis x vAxis), largo vLen en v.
        var thickAcrossV = (Vector3.One - vAxis) * Thickness;
        for (int i = 0; i <= cellsU; i++)
        {
            float u = uLen * i / cellsU;
            var center = origin + (uAxis * u) + (vAxis * (vLen / 2f));
            AddNetStrand(center, (vAxis * vLen) + thickAcrossV, material);
        }

        // Hilos "horizontales" (a lo largo de u, uno por cada paso de v): grosor en v y en la normal,
        // largo uLen en u.
        var thickAcrossU = (Vector3.One - uAxis) * Thickness;
        for (int j = 0; j <= cellsV; j++)
        {
            float v = vLen * j / cellsV;
            var center = origin + (vAxis * v) + (uAxis * (uLen / 2f));
            AddNetStrand(center, (uAxis * uLen) + thickAcrossU, material);
        }
    }

    private void AddNetStrand(Vector3 center, Vector3 size, StandardMaterial3D material)
    {
        _world.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            Position = center,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Caja opaca, sin transparencia ni textura: el bloque de construcción de todo <see cref="BuildStadium"/>.</summary>
    private void AddBox(Vector3 center, Vector3 size, Color color, bool shadow = true)
    {
        _world.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled },
            Position = center,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>
    /// Césped de fútbol gastado (Stadium): franjas de siega por columna, calvas de tierra delante de cada
    /// portería y en el círculo central, hierba seca junto a las bandas y alguna quemadura — la misma
    /// pista de profundidad del prototipo (<c>BuildWornGrass</c>), sobre las líneas de <see cref="Box"/>/
    /// <see cref="Outline"/>/<see cref="Ring"/> que ya dibujan el campo liso.
    /// </summary>
    private static ImageTexture BuildWornGrass()
    {
        int width = Pitch.Columns * GroundPixels;
        int height = Pitch.Rows * GroundPixels;
        var image = Image.CreateEmpty(width, height, true, Image.Format.Rgba8);

        var noise = new FastNoiseLite { Seed = 11, Frequency = 0.006f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, FractalOctaves = 3 };
        var fine = new FastNoiseLite { Seed = 29, Frequency = 0.09f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var burn = new FastNoiseLite { Seed = 5, Frequency = 0.02f, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular };
        var lush = new Color("3b6631");
        var lushAlt = new Color("4a7d3b");
        var dry = new Color("8a8a45");
        var dirt = new Color("7a5f3e");
        var burnt = new Color("2e2a1c");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float cx = x / (float)GroundPixels;
                float cy = y / (float)GroundPixels;
                var c = (int)cx % 2 == 0 ? lush : lushAlt;

                // Desgaste: más en las dos áreas y en el círculo central.
                float wear = 0f;
                wear = Mathf.Max(wear, 1f - (Mathf.Abs(cx - 1.2f) / 1.6f + Mathf.Abs(cy - 3.5f) / 2.2f));
                wear = Mathf.Max(wear, 1f - (Mathf.Abs(cx - 14.8f) / 1.6f + Mathf.Abs(cy - 3.5f) / 2.2f));
                wear = Mathf.Max(wear, 1f - (new Vector2(cx - 8f, cy - 3.5f).Length() / 1.6f));
                wear = Mathf.Clamp(wear, 0f, 1f);

                float n = (noise.GetNoise2D(x, y) * 0.5f) + 0.5f;
                float f = (fine.GetNoise2D(x, y) * 0.5f) + 0.5f;
                float edge = Mathf.Min(Mathf.Min(cy, Pitch.Rows - cy), 1.2f) / 1.2f;
                if (n > 0.66f + (edge * 0.08f))
                {
                    c = c.Lerp(dry, Mathf.Clamp((n - 0.66f) / 0.2f, 0f, 1f) * 0.7f);
                }

                float bald = (wear * 0.9f) + ((n - 0.5f) * 0.6f) + ((f - 0.5f) * 0.25f);
                if (bald > 0.55f)
                {
                    c = c.Lerp(dirt, Mathf.Clamp((bald - 0.55f) / 0.12f, 0f, 1f));
                }

                if (burn.GetNoise2D(x, y) > 0.86f)
                {
                    c = c.Lerp(burnt, 0.65f);
                }

                c = c.Lerp(Colors.Black, (f - 0.5f) * 0.08f);
                image.SetPixel(x, y, c);
            }
        }

        var chalk = new Color(0.93f, 0.92f, 0.86f);
        var faint = new Color(1f, 1f, 1f, 0.07f);
        for (int column = 1; column < Pitch.Columns; column++)
        {
            Blend(image, column - 0.01f, 0f, column + 0.01f, Pitch.Rows, faint);
        }

        for (int row = 1; row < Pitch.Rows; row++)
        {
            Blend(image, 0f, row - 0.01f, Pitch.Columns, row + 0.01f, faint);
        }

        Box(image, (Pitch.Columns / 2f) - 0.03f, 0f, (Pitch.Columns / 2f) + 0.03f, Pitch.Rows, chalk);
        Ring(image, Pitch.Columns / 2f, Pitch.Rows / 2f, 0.9f, 0.05f, chalk);
        Outline(image, 0f, Pitch.AreaTop, Pitch.AreaColumns, Pitch.AreaBottom, 0.045f, chalk);
        Outline(image, Pitch.Columns - Pitch.AreaColumns, Pitch.AreaTop, Pitch.Columns, Pitch.AreaBottom, 0.045f, chalk);
        Outline(image, 0f, 0f, Pitch.Columns, Pitch.Rows, 0.05f, chalk);

        // La barra plana de portería que había aquí (revisión del revisor, 20 sep 2026) la sustituye
        // BuildGoal en BuildStadium: una portería con volumen (postes, larguero y red) de verdad, no un
        // rectángulo pintado en el césped.
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Mezcla alfa sobre lo que ya hay en la imagen, en coordenadas de casilla: para líneas semitransparentes sobre el césped gastado.</summary>
    private static void Blend(Image image, float x0, float y0, float x1, float y1, Color color)
    {
        int left = Mathf.Max(0, Mathf.RoundToInt(x0 * GroundPixels));
        int right = Mathf.Min(image.GetWidth(), Mathf.RoundToInt(x1 * GroundPixels));
        int top = Mathf.Max(0, Mathf.RoundToInt(y0 * GroundPixels));
        int bottom = Mathf.Min(image.GetHeight(), Mathf.RoundToInt(y1 * GroundPixels));

        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                image.SetPixel(x, y, image.GetPixel(x, y).Lerp(new Color(color.R, color.G, color.B), color.A));
            }
        }
    }

    private static ImageTexture BuildGroundTexture(bool silhouette)
    {
        int width = Pitch.Columns * GroundPixels;
        int height = Pitch.Rows * GroundPixels;
        var image = Image.CreateEmpty(width, height, true, Image.Format.Rgba8);

        var grass = silhouette ? new Color(1f, 1f, 1f) : Style.Grass;
        var own = silhouette ? new Color(0.94f, 0.94f, 0.95f) : Style.GrassOwn;
        var line = silhouette ? new Color(0.52f, 0.52f, 0.55f) : Style.GrassLine;
        var faint = silhouette ? new Color(0.76f, 0.76f, 0.78f) : Style.GrassLine.Lerp(Style.Grass, 0.65f);

        image.Fill(grass);
        Box(image, 0f, 0f, Pitch.Columns / 2f, Pitch.Rows, own);

        for (int column = 1; column < Pitch.Columns; column++)
        {
            Box(image, column - 0.012f, 0f, column + 0.012f, Pitch.Rows, faint);
        }

        for (int row = 1; row < Pitch.Rows; row++)
        {
            Box(image, 0f, row - 0.012f, Pitch.Columns, row + 0.012f, faint);
        }

        Box(image, (Pitch.Columns / 2f) - 0.03f, 0f, (Pitch.Columns / 2f) + 0.03f, Pitch.Rows, line);
        Ring(image, Pitch.Columns / 2f, Pitch.Rows / 2f, 0.9f, 0.06f, line);

        Outline(image, 0f, Pitch.AreaTop, Pitch.AreaColumns, Pitch.AreaBottom, 0.05f, line);
        Outline(image, Pitch.Columns - Pitch.AreaColumns, Pitch.AreaTop, Pitch.Columns, Pitch.AreaBottom, 0.05f, line);
        Outline(image, 0f, 0f, Pitch.Columns, Pitch.Rows, 0.05f, line);

        // Las porterías en el color del equipo que las defiende, como en 2D: es lo que dice hacia dónde
        // ataca cada uno sin escribirlo en ninguna parte.
        var ownGoal = silhouette ? new Color(0.20f, 0.20f, 0.22f) : Style.TeamOwn;
        var rivalGoal = silhouette ? new Color(0.20f, 0.20f, 0.22f) : Style.TeamRival;
        float mid = Pitch.Rows / 2f;
        Box(image, 0f, mid - 0.9f, 0.09f, mid + 0.9f, ownGoal);
        Box(image, Pitch.Columns - 0.09f, mid - 0.9f, Pitch.Columns, mid + 0.9f, rivalGoal);

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Rectángulo relleno, en coordenadas de casilla.</summary>
    private static void Box(Image image, float x0, float y0, float x1, float y1, Color color)
    {
        int left = Mathf.Clamp(Mathf.RoundToInt(x0 * GroundPixels), 0, image.GetWidth());
        int right = Mathf.Clamp(Mathf.RoundToInt(x1 * GroundPixels), 0, image.GetWidth());
        int top = Mathf.Clamp(Mathf.RoundToInt(y0 * GroundPixels), 0, image.GetHeight());
        int bottom = Mathf.Clamp(Mathf.RoundToInt(y1 * GroundPixels), 0, image.GetHeight());

        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                image.SetPixel(x, y, color);
            }
        }
    }

    /// <summary>Rectángulo hueco de grosor <paramref name="thickness"/> casillas, hacia dentro.</summary>
    private static void Outline(Image image, float x0, float y0, float x1, float y1, float thickness, Color color)
    {
        Box(image, x0, y0, x1, y0 + thickness, color);
        Box(image, x0, y1 - thickness, x1, y1, color);
        Box(image, x0, y0, x0 + thickness, y1, color);
        Box(image, x1 - thickness, y0, x1, y1, color);
    }

    private static void Ring(Image image, float cx, float cy, float radius, float thickness, Color color)
    {
        const int Steps = 900;
        float half = thickness / 2f;
        for (int i = 0; i < Steps; i++)
        {
            float angle = Mathf.Tau * i / Steps;
            float x = cx + (Mathf.Cos(angle) * radius);
            float y = cy + (Mathf.Sin(angle) * radius);
            Box(image, x - half, y - half, x + half, y + half, color);
        }
    }
}
