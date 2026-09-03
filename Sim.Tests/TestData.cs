using Underleague.Sim.Data;

namespace Underleague.Sim.Tests;

/// <summary>
/// Ayudante de tests para cargar los ficheros reales de /data desde disco (los tests sí hacen E/S;
/// /Sim no). Sube directorios desde AppContext.BaseDirectory hasta encontrar data/sim/tuning.json.
/// </summary>
internal static class TestData
{
    public static string DataDirectory { get; } = FindDataDirectory();

    public static Dictionary<string, string> LoadAllFiles()
    {
        var files = new Dictionary<string, string>();

        string racesDir = Path.Combine(DataDirectory, "races");
        foreach (var racePath in Directory.GetFiles(racesDir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            files["races/" + Path.GetFileName(racePath)] = File.ReadAllText(racePath);
        }

        files["traits/traits.json"] = File.ReadAllText(Path.Combine(DataDirectory, "traits", "traits.json"));
        files["ai/weights.json"] = File.ReadAllText(Path.Combine(DataDirectory, "ai", "weights.json"));
        files["sim/tuning.json"] = File.ReadAllText(Path.Combine(DataDirectory, "sim", "tuning.json"));
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
}
