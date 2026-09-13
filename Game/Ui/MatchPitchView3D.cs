using System.Collections.Generic;
using Godot;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui;

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
/// <b>Escala: 1 casilla = 1 unidad de mundo.</b> El campo son 16x5 unidades en el plano XZ: la columna del
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

    private readonly List<MeshInstance3D> _bodies = new();
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

    /// <summary>Elevación de la cámara en grados sobre el césped. Es <c>[Export]</c> para poder barrerla en las capturas.</summary>
    [Export]
    public float Elevation { get; set; } = 45f;

    /// <summary>
    /// Alto del encuadre ortográfico en unidades de mundo (Godot mide el <c>Size</c> ortográfico en
    /// vertical). Con el rectángulo del campo de la pantalla —1120x350, relación 3,2— 5,3 de alto son 17,0
    /// de ancho: las 16 columnas con media casilla de margen a cada lado, la misma en los tres ángulos. Es
    /// el ancho el que manda: 5 filas en tres cuartos nunca llenan un rectángulo tan apaisado.
    /// </summary>
    [Export]
    public float OrthoSize { get; set; } = 5.3f;

    /// <summary>Cápsulas negras planas sobre suelo blanco, sin color de equipo: la prueba literal de RA-002.</summary>
    [Export]
    public bool SilhouetteMode { get; set; }

    /// <summary>Traza del partido; null mientras no haya partido reproducido.</summary>
    public MatchTrace? Trace { get; private set; }

    /// <summary>Fotograma que se está pintando (índice, no tick). Lo pone la pantalla, igual que en 2D.</summary>
    public int Frame { get; set; }

    /// <summary>Fracción 0..1 hacia el fotograma siguiente. Solo suaviza el dibujo (RT-020).</summary>
    public float Alpha { get; set; }

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

        ApplyCamera();
        ApplyPalette();
        ApplyTrace();
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

            _bodies[i].Mesh = new CapsuleMesh { Radius = radius, Height = height, RadialSegments = 28, Rings = 12 };
            _rings[i].Scale = new Vector3(radius, 1f, radius);
            _heights[i] = height;
            _radii[i] = radius;
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

        _appliedSilhouette = !SilhouetteMode;
        ApplyCamera();
        ApplyPalette();
    }

    private static Godot.Environment BuildEnvironment(bool silhouette) => new()
    {
        BackgroundMode = Godot.Environment.BGMode.Color,
        BackgroundColor = silhouette ? new Color(1f, 1f, 1f) : Style.Background,
        AmbientLightSource = Godot.Environment.AmbientSource.Color,
        AmbientLightColor = silhouette ? new Color(1f, 1f, 1f) : new Color(0.62f, 0.68f, 0.78f),

        // En silueta el suelo es blanco y la luz tiene que quedarse muy por debajo de quemarlo: si se
        // satura desaparecen las líneas del campo y con ellas la referencia de casilla, que es la mitad de
        // lo que hay que juzgar.
        AmbientLightEnergy = silhouette ? 0.34f : 0.45f,
        TonemapMode = Godot.Environment.ToneMapper.Linear,
    };

    /// <summary>
    /// El césped: un cuadrilátero de 16x5 en el plano XZ con los vértices y las UV puestos a mano. Se
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
        float elevation = Mathf.DegToRad(Mathf.Clamp(Elevation, 5f, 89f));
        var center = new Vector3(Pitch.Columns / 2f, 0.35f, Pitch.Rows / 2f);

        // La cámara mira desde el lado +Z: así el eje X del campo cae a la derecha de la pantalla y las
        // filas crecen hacia abajo, exactamente como en la vista 2D.
        var from = center + (new Vector3(0f, Mathf.Sin(elevation), Mathf.Cos(elevation)) * CameraDistance);
        _camera.LookAtFromPosition(from, center, Vector3.Up);
        _camera.Size = Mathf.Max(OrthoSize, 0.5f);
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
            _grassTexture ??= BuildGroundTexture(false);
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
            _bodies[i].MaterialOverride = BodyMaterial(Trace.Players[i].Team);
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

            // La cápsula proyecta sombra en el suelo (RA-008) pero no la recibe. Una superficie tan curva
            // y tan pequeña frente al texel del mapa de sombras se auto-sombrea con un moiré o con un
            // escalón recto, según el sesgo; y su volumen ya lo cuenta el difuso. Además va en la
            // dirección del toon del ADR 0102: la forma la dice la rampa de luz, no la sombra.
            DisableReceiveShadows = true,
        };
    }

    private void ApplyTrace()
    {
        if (Trace is not { FrameCount: > 0 } trace || _bodies.Count == 0)
        {
            _ball.Visible = false;
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
            if (Style.IsDown(trace.StateAt(frame, i)))
            {
                body.Transform = new Transform3D(
                    new Basis(new Vector3(0f, 0f, 1f), Mathf.Pi / 2f),
                    new Vector3(at.X, _radii[i], at.Y));
            }
            else
            {
                body.Transform = new Transform3D(Basis.Identity, new Vector3(at.X, _heights[i] / 2f, at.Y));
            }
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

        _ball.Visible = true;
        _ball.Position = new Vector3(ball.X + offset, BallRadius, ball.Y);
    }

    /// <summary>Misma interpolación que <see cref="MatchPitchView"/>: solo dibujo, la traza no se toca (RT-020).</summary>
    private Vec2 Interpolate(MatchTrace trace, int frame, int player)
    {
        var here = trace.PositionAt(frame, player);
        if (Alpha <= 0f || frame + 1 >= trace.FrameCount)
        {
            return here;
        }

        var next = trace.PositionAt(frame + 1, player);
        return new Vec2(Mathf.Lerp(here.X, next.X, Alpha), Mathf.Lerp(here.Y, next.Y, Alpha));
    }

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

    // ------------------------------------------------------------------ textura del suelo

    /// <summary>
    /// El campo pintado en una textura: mitades, cuadrícula, medio campo, círculo central, las dos áreas
    /// (2x3, <see cref="Pitch.AreaColumns"/>) y las porterías, con la misma paleta que la vista 2D. En
    /// silueta la misma geometría en blanco y gris, para no perder la referencia de casilla.
    /// </summary>
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

        Outline(image, 0f, 1f, Pitch.AreaColumns, 1f + Pitch.AreaRows, 0.05f, line);
        Outline(image, Pitch.Columns - Pitch.AreaColumns, 1f, Pitch.Columns, 1f + Pitch.AreaRows, 0.05f, line);
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
