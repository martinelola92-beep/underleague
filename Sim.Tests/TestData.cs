using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests;

/// <summary>
/// Ayudante de tests para cargar los ficheros reales de /data desde disco (los tests sí hacen E/S;
/// /Sim no). Sube directorios desde AppContext.BaseDirectory hasta encontrar data/sim/tuning.json.
/// </summary>
internal static class TestData
{
    public static string DataDirectory { get; } = FindDataDirectory();

    /// <summary>
    /// Todos los JSON de /data salvo los esquemas, con la ruta relativa que espera DataLoader.FromJson.
    /// Se enumera el directorio en vez de listar los ficheros a mano (como hacía la fase 0) para que el
    /// catálogo de perks y las plantillas de l10n que escriba cualquier paquete entren en los tests sin
    /// tocar este ayudante.
    /// </summary>
    public static Dictionary<string, string> LoadAllFiles()
    {
        var files = new Dictionary<string, string>();
        string schemas = Path.Combine(DataDirectory, "schemas") + Path.DirectorySeparatorChar;

        foreach (var path in Directory.EnumerateFiles(DataDirectory, "*.json", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            if (path.StartsWith(schemas, StringComparison.Ordinal))
            {
                continue;
            }

            string relative = Path.GetRelativePath(DataDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
            files[relative] = File.ReadAllText(path);
        }

        return files;
    }

    public static Catalog LoadCatalog() => DataLoader.FromJson(LoadAllFiles());

    private static string FindDataDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "sim", "tuning.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"No se encontró data/sim/tuning.json subiendo directorios desde {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Tabla de ajuste de la entrada sin balón por puesto (ADR 0125 D2) para los <see cref="AiWeights"/>
    /// que un test construye a mano. El portero siempre 0: no tiene marca asignada. Los valores por
    /// defecto dejan la acción apagada en los tres puestos, así que un test que no la ejercita no la
    /// enciende sin querer.
    /// </summary>
    public static int[] OffBallTackleAdjust(int defender = 0, int midfielder = 0, int forward = 0)
    {
        var table = new int[Enum.GetValues<Position>().Length];
        table[(int)Position.Defender] = defender;
        table[(int)Position.Midfielder] = midfielder;
        table[(int)Position.Forward] = forward;
        return table;
    }
}
