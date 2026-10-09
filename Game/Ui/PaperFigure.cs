using System.Collections.Generic;
using Godot;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Game.Ui;

/// <summary>
/// <b>Prototipo</b> (9 oct 2026; el revisor: «esos modelos 3D siguen siendo una mierda; ¿y si haces 2D con 8
/// direcciones?»): el jugador como figura de papel ilustrada, a la manera de Cult of the Lamb — un recorte dibujado que
/// siempre mira a cámara y se anima por código: bota y se aplasta al correr, se balancea a cada zancada, se inclina hacia
/// donde va, se lanza en plancha en la entrada y se tumba al caer. Sin esqueleto ni clips.
/// <para>
/// Vistas: frente, perfil y espalda de las hojas de modelo (<c>tools/arte/figuras_papel.py</c> →
/// <c>Game/Art/Figures/</c>); el perfil reflejado da la otra mano. Con las tres salen cuatro direcciones; las vistas de
/// tres cuartos (para ocho) son las que faltan por dibujar. El rival se distingue tiñendo de rojo el azul de la camiseta
/// (<c>paper_figure.gdshader</c>).
/// </para>
/// <para>Sólo mira lo que ya escribió la traza (RT-014). Se cuelga del mismo cuerpo que el modelo 3D, que lo coloca.</para>
/// </summary>
public sealed partial class PaperFigure : Node3D
{
    private const string ShaderPath = "res://Art/Shaders/paper_figure.gdshader";

    private static readonly Dictionary<string, Texture2D?> Views = new();
    private static Shader? _shader;

    private Sprite3D _sprite = null!;
    private ShaderMaterial _material = null!;
    private string _race = "human";
    private float _height = 1f;
    private float _phase;
    private float _seed;
    private float _lean;
    private float _tilt;
    private string _view = string.Empty;
    private bool _mirror;
    private Vector2 _lastFacing = new(1f, 0f);

    /// <summary>¿Hay figura dibujada para esta raza? Si no, la vista se queda con el modelo 3D.</summary>
    public static bool Available(Race race) => View(Key(race), "front") is not null;

    private static string Key(Race race) => race switch
    {
        Race.Orc => "orc",
        Race.Dwarf => "dwarf",
        Race.Elf => "elf",
        Race.Undead => "undead",
        _ => "human",
    };

    private static Texture2D? View(string race, string view)
    {
        string path = $"res://Art/Figures/{race}_{view}.png";
        if (!Views.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Views[path] = texture;
        }

        return texture;
    }

    /// <summary>Crea la figura de una raza con el alto que manda la vista (en casillas) y el color de su equipo.</summary>
    public static PaperFigure? TryCreate(Race race, float height, int team, int seed)
    {
        if (!Available(race))
        {
            return null;
        }

        _shader ??= ResourceLoader.Exists(ShaderPath) ? GD.Load<Shader>(ShaderPath) : null;
        var figure = new PaperFigure { _race = Key(race), _height = height, _seed = seed * 0.37f, _phase = seed * 1.7f };
        figure._material = new ShaderMaterial { Shader = _shader };
        figure._material.SetShaderParameter("rival", team != 0);
        figure._sprite = new Sprite3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            Centered = true,
            Offset = Vector2.Zero,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            MaterialOverride = figure._material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        figure.AddChild(figure._sprite);
        figure.SetView("front", false);
        return figure;
    }

    private void SetView(string view, bool mirror)
    {
        if (view == _view && mirror == _mirror)
        {
            return;
        }

        _view = view;
        _mirror = mirror;
        var texture = View(_race, view) ?? View(_race, "front");
        _sprite.Texture = texture;
        _material.SetShaderParameter("tex", texture);
        _sprite.FlipH = mirror;
        _material.SetShaderParameter("flip", mirror);
        if (texture is not null)
        {
            // Pies en el suelo: el sprite se centra en su mitad, así que se sube medio alto.
            _sprite.PixelSize = _height / texture.GetHeight();
            _sprite.Position = new Vector3(0f, _height / 2f, 0f);
        }
    }

    /// <summary>
    /// La postura de este fotograma. <paramref name="velocity"/> y <paramref name="facing"/> en casillas por segundo y
    /// en el plano del campo (x columnas, y filas); <paramref name="camera"/> es la cámara, para elegir la vista.
    /// </summary>
    public void Pose(Vector2 velocity, Vector2 facing, PlayerState state, Camera3D camera, float delta)
    {
        if (delta <= 0f)
        {
            return;
        }

        float speed = velocity.Length();
        if (facing.LengthSquared() > 0.01f)
        {
            _lastFacing = facing.Normalized();
        }

        // Vista según hacia dónde mira respecto a la cámara (que mira hacia -Z del campo, filas crecientes hacia ella).
        var toCamera = new Vector2(camera.GlobalPosition.X - GlobalPosition.X, camera.GlobalPosition.Z - GlobalPosition.Z).Normalized();
        float along = _lastFacing.Dot(toCamera);
        float across = _lastFacing.X * toCamera.Y - _lastFacing.Y * toCamera.X;
        if (along > 0.5f)
        {
            SetView("front", false);
        }
        else if (along < -0.5f)
        {
            SetView("back", false);
        }
        else
        {
            SetView("side", across > 0f);
        }

        bool down = state is PlayerState.KnockedDown or PlayerState.Injured;
        bool tackling = state == PlayerState.Tackling;
        _phase += delta * Mathf.Clamp(speed * 3.2f, 0f, 14f);

        // Zancada: bote y aplastamiento a dos golpes por ciclo, balanceo lateral a uno.
        float stride = Mathf.Clamp(speed / 3f, 0f, 1f);
        float bounce = Mathf.Abs(Mathf.Sin(_phase)) * 0.09f * stride * _height;
        float squash = 1f - (0.06f * stride * (1f - Mathf.Abs(Mathf.Sin(_phase))));
        float sway = Mathf.Sin(_phase) * 7f * stride;

        // Inclinación hacia donde corre (en pantalla, hacia la izquierda o la derecha según el perfil).
        float lateral = velocity.X * toCamera.Y - velocity.Y * toCamera.X;
        float targetLean = Mathf.Clamp(-lateral * 3.5f, -14f, 14f);
        float targetTilt = down ? 90f * (_mirror ? -1f : 1f) : tackling ? 55f * (_mirror ? 1f : -1f) : 0f;
        float k = 1f - Mathf.Exp(-12f * delta);
        _lean = Mathf.Lerp(_lean, targetLean, k);
        _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-(down ? 9f : 14f) * delta));

        // Respiración en reposo: un aplastamiento lentísimo, distinto por jugador.
        float breathe = 1f + (Mathf.Sin((Time.GetTicksMsec() / 1000f * 2.2f) + _seed) * 0.015f * (1f - stride));

        _sprite.Position = new Vector3(0f, (_height / 2f * squash * breathe) + bounce - (down ? _height * 0.35f : 0f), 0f);
        _sprite.Scale = new Vector3(1f / Mathf.Sqrt(squash), squash * breathe, 1f);
        RotationDegrees = new Vector3(0f, 0f, 0f);
        _sprite.RotationDegrees = new Vector3(0f, 0f, sway + _lean + _tilt);
    }

    /// <summary>Atenuación de la cortinilla de teletransporte (BA-K).</summary>
    public void SetOpacity(float opacity) => _material.SetShaderParameter("opacity", opacity);

    /// <summary>Modo silueta (RA-002): la figura en negro.</summary>
    public void SetSilhouette(bool silhouette) => _material.SetShaderParameter("silhouette", silhouette);
}
