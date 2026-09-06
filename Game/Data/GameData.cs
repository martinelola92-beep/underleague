using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Underleague.Game.Data;

/// <summary>
/// Lectura de <c>/data</c> desde el proyecto de Godot. La E/S vive aquí, en <c>/Game</c>, que es donde
/// puede vivir: <c>/Sim</c> no lee ficheros (RT-012) y recibe el contenido ya leído.
/// <para>
/// El diccionario que devuelve <see cref="Snapshot"/> es el mismo formato que consume
/// <c>DataLoader.FromJson</c> y el que la run congela como instantánea al empezar (RT-061b), así que una
/// run en curso sigue jugándose con los datos con los que empezó aunque el disco cambie.
/// </para>
/// </summary>
public static class GameData
{
    /// <summary>Idioma de la interfaz. En fase 4 lo elige el jugador (RT-073); hasta entonces, español.</summary>
    public const string Language = "es";

    private static IReadOnlyDictionary<string, string>? _snapshot;

    /// <summary>
    /// Todos los JSON de <c>/data</c> salvo los esquemas, con la ruta relativa que espera el cargador.
    /// Se lee una vez por ejecución: el disco no cambia a mitad de partida y releerlo sería E/S gratis.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Snapshot => _snapshot ??= ReadDataFiles(FindDataDirectory());

    private static Dictionary<string, string> ReadDataFiles(string dataDirectory)
    {
        var files = new Dictionary<string, string>();
        string schemas = Path.Combine(dataDirectory, "schemas") + Path.DirectorySeparatorChar;
        var paths = new List<string>(Directory.EnumerateFiles(dataDirectory, "*.json", SearchOption.AllDirectories));
        paths.Sort(StringComparer.Ordinal);

        foreach (string path in paths)
        {
            if (path.StartsWith(schemas, StringComparison.Ordinal))
            {
                continue;
            }

            files[Path.GetRelativePath(dataDirectory, path).Replace(Path.DirectorySeparatorChar, '/')] = File.ReadAllText(path);
        }

        return files;
    }

    /// <summary>Sube directorios desde el proyecto de Godot (o desde el ejecutable exportado) hasta encontrar <c>data/sim/tuning.json</c>.</summary>
    private static string FindDataDirectory()
    {
        // En el editor res:// es el directorio del proyecto; en una build exportada es el .pck y
        // GlobalizePath devuelve vacío, así que el /data va junto al ejecutable (docs/entorno.md).
        var candidates = new List<string>
        {
            ProjectSettings.GlobalizePath("res://"),
            OS.GetExecutablePath().GetBaseDir(),
            AppContext.BaseDirectory,
        };

        foreach (string start in candidates)
        {
            if (string.IsNullOrEmpty(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                string data = Path.Combine(dir.FullName, "data");
                if (File.Exists(Path.Combine(data, "sim", "tuning.json")))
                {
                    return data;
                }

                dir = dir.Parent;
            }
        }

        throw new DirectoryNotFoundException("no se encontró data/sim/tuning.json subiendo desde res:// ni desde el ensamblado");
    }
}
