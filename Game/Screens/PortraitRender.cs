using System.IO;
using Godot;
using Underleague.Game.Ui;
using Underleague.Sim.Model;

namespace Underleague.Game.Screens;

/// <summary>
/// <b>Herramienta de arte</b> (pase de arte, 9 oct 2026): renderiza el busto de cada raza con los mismos modelos del
/// partido, en el uniforme propio, para que la cara de la plantilla sea la del campo. Escribe PNG con fondo
/// transparente en <c>out/arte/retratos/</c>; <c>tools/arte/retratos.py</c> les pone el contorno de tinta y los deja en
/// <c>Game/Art/Portraits/</c>. No forma parte del juego.
///
/// <code>timeout 600 xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game --rendering-driver opengl3 --audio-driver Dummy --scene res://Scenes/RetratosRender.tscn</code>
/// </summary>
public partial class PortraitRender : Node3D
{
    /// <summary>Variantes por raza: el modelo cambia pelo, barba y tonos con la variante (ver <c>PlayerModel</c>).</summary>
    public const int Variants = 8;

    private static readonly Race[] Races = { Race.Human, Race.Elf, Race.Dwarf, Race.Orc, Race.Undead };

    private const float Height = 1.8f;

    public override async void _Ready()
    {
        GetViewport().TransparentBg = true;
        var camera = new Camera3D { Fov = 24f, Current = true };
        AddChild(camera);

        // Luz de retrato: principal de tres cuartos, relleno frío y contraluz cálido para despegar la silueta.
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-28f, -38f, 0f), LightEnergy = 0.95f, ShadowEnabled = true });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-10f, 60f, 0f), LightEnergy = 0.12f, LightColor = new Color("b9c8e8") });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20f, 170f, 0f), LightEnergy = 0.45f, LightColor = new Color("ffd9a0") });
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.7f, 0.68f, 0.66f),
                AmbientLightEnergy = 0.45f,
            },
        });

        string folder = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../out/arte/retratos";
        Directory.CreateDirectory(folder);
        for (int r = 0; r < Races.Length; r++)
        {
            for (int v = 0; v < Variants; v++)
            {
                var model = PlayerModel.TryCreate(Height, keeper: false, Races[r], v);
                if (model is null)
                {
                    GD.Print($"[retratos] sin modelo para {Races[r]}");
                    continue;
                }

                var holder = new Node3D { Position = new Vector3(0f, Height / 2f, 0f) };
                AddChild(holder);
                holder.AddChild(model);
                model.Paint(new StandardMaterial3D(), Style.TeamOwn.Darkened(0.35f));
                model.DebugPlayClip("idle", 1.0f + (v * 0.13f), 18f + ((v % 3) * 6f));

                // Busto: cabeza y hombros. La cabeza cae hacia el 92 % del alto (medido en las capturas de prueba).
                float headY = Height * 0.92f;
                var eye = new Vector3(0.28f, headY + 0.04f, 1.38f);
                camera.LookAtFromPosition(eye, new Vector3(0f, headY - 0.04f, 0f), Vector3.Up);

                // Mira a cámara, que es lo que hace un retrato (la espera mira al suelo).
                for (int i = 0; i < 8; i++)
                {
                    model.LookAt(eye, 1f, 0.1f);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var image = GetViewport().GetTexture().GetImage();
                int side = Mathf.Min(image.GetWidth(), image.GetHeight());
                var square = image.GetRegion(new Rect2I((image.GetWidth() - side) / 2, (image.GetHeight() - side) / 2, side, side));
                string name = $"{Races[r].ToString().ToLowerInvariant()}_{v}.png";
                square.SavePng(Path.Combine(folder, name));
                GD.Print($"retrato: {name}");
                holder.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        GetTree().Quit();
    }
}
