using System.IO;
using System.Threading.Tasks;
using Godot;
using Underleague.Game.Ui;
using Underleague.Sim.Model;

namespace Underleague.Game.Screens;

/// <summary>
/// <b>Herramienta de desarrollo</b>: las cinco razas, con los dos colores de equipo, colocadas de cerca en varios
/// instantes de los clips de fútbol, una imagen por clip (<c>Game/screenshots/razas/</c>). Existe porque la cámara de la
/// retransmisión las enseña a 60 px de alto: para ver si el retargeting de Mixamo al esqueleto UAL de Quaternius dobla
/// bien rodillas, brazos y cabeza hace falta acercarse. No forma parte del juego.
///
/// <code>timeout 600 xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game --rendering-driver opengl3 --audio-driver Dummy --scene res://Scenes/SondaRazas.tscn</code>
/// </summary>
public partial class RaceProbe : Node3D
{
    private static readonly Race[] Races = { Race.Human, Race.Elf, Race.Dwarf, Race.Orc, Race.Undead };

    /// <summary>Alto relativo de cada raza: las proporciones de RA de la vista (alto/ancho × un radio común), solo orientativo.</summary>
    private static readonly float[] Heights = { 1.0f, 1.05f, 0.85f, 0.95f, 0.95f };

    private static readonly (string Name, string Clip, float Seconds, float Yaw)[] Shots =
    {
        ("espera", "idle", 1.0f, 20f),
        ("carrera", "run", 0.2f, 70f),
        ("trote", "jog", 0.3f, 70f),
        ("golpeo", "kick", 0.2f, 70f),
        ("cabezazo", "header", 0.8f, 40f),
        ("entrada", "tackle", 1.0f, 70f),
        ("caida", "trip", 0.68f, 70f),
        ("suelo", "fallen", 0.5f, 70f),
        ("levanta", "standup", 1.0f, 70f),
        ("portero", "gk_idle", 1.0f, 20f),
        ("estirada", "gk_save", 0.9f, 20f),
        ("saque", "throwin", 1.0f, 20f),
    };

    public override async void _Ready()
    {
        var camera = new Camera3D { Fov = 30f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0f, 1.9f, 6.2f), new Vector3(0f, 0.6f, 0f), Vector3.Up);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50f, 30f, 0f), ShadowEnabled = true, LightEnergy = 1.1f });
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("6f8f5a"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.75f, 0.8f),
                AmbientLightEnergy = 0.6f,
            },
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(30f, 30f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("4f7a3a") },
        });

        var models = new System.Collections.Generic.List<PlayerModel>();
        for (int i = 0; i < Races.Length * 2; i++)
        {
            int race = i % Races.Length;
            int team = i / Races.Length;
            var model = PlayerModel.TryCreate(Heights[race], keeper: false, Races[race], i);
            if (model is null)
            {
                GD.Print($"[razas] sin modelo para {Races[race]}");
                continue;
            }

            var holder = new Node3D { Position = new Vector3((race - 2) * 1.25f + (team == 0 ? -0.3f : 0.3f), Heights[race] / 2f, team == 0 ? 0.6f : -0.9f) };
            AddChild(holder);
            holder.AddChild(model);
            model.Paint(new StandardMaterial3D(), team == 0 ? Style.TeamOwn : Style.TeamRival);
            models.Add(model);
        }

        string directory = ProjectSettings.GlobalizePath("res://screenshots/razas");
        Directory.CreateDirectory(directory);
        foreach (var (name, clip, seconds, yaw) in Shots)
        {
            foreach (var model in models)
            {
                model.DebugPlayClip(clip, seconds, yaw);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name + ".png"));
            GD.Print($"[razas] {name}.png");
        }

        // La mirada (LookAtModifier3D sobre Head/UpperChest): con el eje del perfil mal elegido, la cabeza se retuerce.
        foreach (var model in models)
        {
            model.DebugPlayClip("idle", 1.0f, 0f);
        }

        for (int frame = 0; frame < 30; frame++)
        {
            foreach (var model in models)
            {
                model.LookAt(new Vector3(-6f, 1.5f, 3f), 1f, 1f / 30f);
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "mirada-izquierda.png"));
        GD.Print("[razas] mirada-izquierda.png");
        GetTree().Quit();
    }
}
