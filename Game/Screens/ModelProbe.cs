using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Screens;

/// <summary>
/// <b>Herramienta de desarrollo</b>: vuelca qué trae de verdad un modelo importado —árbol de nodos, alto
/// de la malla, huesos, animaciones y la <b>ruta de sus pistas</b>— y sale. No forma parte del juego.
///
/// <para>Existe porque cada pregunta que se responde suponiendo cuesta una ronda de capturas de diez
/// minutos. Ya pasó dos veces con el maniquí de Quaternius: el importador de glTF <b>quita el sufijo
/// <c>_Loop</c></b> de los nombres, y un modelo sin animación arrancada se queda en su pose de reposo con
/// los brazos en cruz. Las dos se habrían visto en el primer volcado.</para>
///
/// <para>La pregunta que motivó la sonda: los clips de Mixamo vienen en ficheros <b>aparte</b> del
/// personaje, así que una animación solo se le puede aplicar si las <b>rutas de sus pistas</b> coinciden
/// con la jerarquía del personaje (<c>Skeleton3D:mixamorig_Hips</c> y compañía). Si no coinciden, hay que
/// reescribirlas al cargar. Eso no se adivina: se mira.</para>
///
/// <code>timeout 300 godot --path Game --headless --scene res://Scenes/SondaModelo.tscn</code>
/// </summary>
public partial class ModelProbe : Node
{
    /// <summary>Carpeta a sondear. Todo lo que Godot haya importado como escena.</summary>
    private const string Folder = "res://models/soccer";

    public override void _Ready()
    {
        using var dir = DirAccess.Open(Folder);
        if (dir is null)
        {
            GD.Print($"[sonda] no existe {Folder}");
            GetTree().Quit(1);
            return;
        }

        var names = new List<string>();
        foreach (string file in dir.GetFiles())
        {
            string name = file.EndsWith(".import") ? file[..^".import".Length] : file;
            if (name.EndsWith(".fbx") || name.EndsWith(".glb") || name.EndsWith(".gltf"))
            {
                names.Add(name);
            }
        }

        names.Sort();
        foreach (string name in new HashSet<string>(names))
        {
            Probe($"{Folder}/{name}");
        }

        GetTree().Quit();
    }

    private static void Probe(string path)
    {
        if (ResourceLoader.Load(path) is not PackedScene packed)
        {
            GD.Print($"[sonda] {path}: NO es una escena importada");
            return;
        }

        var root = packed.Instantiate();
        GD.Print($"\n[sonda] {path}");
        GD.Print("  árbol: " + Tree(root, 0));

        foreach (var node in All(root))
        {
            switch (node)
            {
                case Skeleton3D skeleton:
                    GD.Print($"  huesos: {skeleton.GetBoneCount()} (primero '{skeleton.GetBoneName(0)}')");
                    break;

                case MeshInstance3D mesh:
                    GD.Print($"  malla '{mesh.Name}': alto {mesh.GetAabb().Size.Y:0.###}, escala global {mesh.Scale}");
                    break;

                case AnimationPlayer player:
                    foreach (string animation in player.GetAnimationList())
                    {
                        var clip = player.GetAnimation(animation);
                        string first = clip.GetTrackCount() > 0 ? clip.TrackGetPath(0).ToString() : "(sin pistas)";
                        GD.Print($"  animación '{animation}': {clip.GetTrackCount()} pistas, {clip.Length:0.##}s, bucle {clip.LoopMode}, pista[0] = {first}");
                    }

                    break;
            }
        }

        root.QueueFree();
    }

    private static string Tree(Node node, int depth)
    {
        string text = $"{node.Name}({node.GetType().Name})";
        var children = node.GetChildren();
        if (children.Count == 0 || depth > 3)
        {
            return text;
        }

        var parts = new List<string>();
        foreach (var child in children)
        {
            parts.Add(Tree(child, depth + 1));
        }

        return text + " [" + string.Join(", ", parts) + "]";
    }

    private static IEnumerable<Node> All(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren())
        {
            foreach (var deeper in All(child))
            {
                yield return deeper;
            }
        }
    }
}
