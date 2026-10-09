using System.IO;
using Godot;
using Underleague.Game.Ui;
using Underleague.Sim.Model;

namespace Underleague.Game.Screens;

/// <summary>
/// <b>Herramienta de arte</b> (pase de arte, 9 oct 2026; el revisor: «quiero que el público sean renders y no
/// pastillas»): fotografía figuras de grada con los mismos modelos de raza, en tres colores (afición propia, rival y
/// neutral) y tres posturas (de pie y dos con los brazos en alto). <c>tools/arte/publico.py</c> les pone tinta y las
/// junta en un atlas que la grada dibuja como impostores (<c>MatchPitchView3D.BuildStadium</c>). No forma parte del
/// juego.
///
/// <code>timeout 600 xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game --rendering-driver opengl3 --audio-driver Dummy --scene res://Scenes/PublicoRender.tscn</code>
/// </summary>
public partial class CrowdRender : Node3D
{
    public const int VariantsPerRace = 2;

    private static readonly Race[] Races = { Race.Human, Race.Elf, Race.Dwarf, Race.Orc, Race.Undead };

    private static readonly (string Name, Color Shirt)[] Colors =
    {
        ("own", new Color("2f6fd6")),
        ("rival", new Color("d63a2f")),
        ("neutral", new Color("c9b48a")),
    };

    // Postura: clip y segundo. El saque de banda a 1,1 s lleva los brazos por encima de la cabeza y el cabezazo a 0,55 s los abre: la grada celebrando (barrido de clips del 9 oct).
    private static readonly (string Clip, float Seconds)[] Poses = { ("idle", 1.0f), ("throwin", 1.1f), ("header", 0.55f) };

    private const float Height = 1.8f;

    public override async void _Ready()
    {
        GetViewport().TransparentBg = true;
        var camera = new Camera3D { Fov = 22f, Current = true };
        AddChild(camera);

        // La grada se ve desde el campo con la cámara de la retransmisión (45°): se fotografía desde arriba también.
        camera.LookAtFromPosition(new Vector3(0f, Height * 0.5f + 3.1f, 4.9f), new Vector3(0f, Height * 0.5f, 0f), Vector3.Up);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45f, -30f, 0f), LightEnergy = 1.0f });
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.72f, 0.72f, 0.75f),
                AmbientLightEnergy = 0.5f,
            },
        });

        string folder = ProjectSettings.GlobalizePath("res://").TrimEnd('/') + "/../out/arte/publico";
        Directory.CreateDirectory(folder);
        foreach (var race in Races)
        {
            for (int v = 0; v < VariantsPerRace; v++)
            {
                foreach (var (colorName, shirt) in Colors)
                {
                    for (int p = 0; p < Poses.Length; p++)
                    {
                        var model = PlayerModel.TryCreate(Height, keeper: false, race, (v * 3) + 1);
                        if (model is null)
                        {
                            continue;
                        }

                        var holder = new Node3D { Position = new Vector3(0f, Height / 2f, 0f) };
                        AddChild(holder);
                        holder.AddChild(model);
                        model.Paint(new StandardMaterial3D(), shirt);
                        model.DebugPlayClip(Poses[p].Clip, Poses[p].Seconds, 0f);
                        for (int i = 0; i < 6; i++)
                        {
                            model.LookAt(camera.GlobalPosition, 0.8f, 0.1f);
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        }

                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        var image = GetViewport().GetTexture().GetImage();
                        string name = $"{race.ToString().ToLowerInvariant()}_{v}_{colorName}_{p}.png";
                        image.SavePng(Path.Combine(folder, name));
                        GD.Print($"publico: {name}");
                        holder.QueueFree();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                }
            }
        }

        GetTree().Quit();
    }
}
