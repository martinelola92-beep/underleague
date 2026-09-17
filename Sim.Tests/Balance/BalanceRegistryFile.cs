using Underleague.Sim.Analysis;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Envoltorio de E/S sobre <see cref="BalanceRegistry"/> (que solo serializa texto, RT-012: /Sim no
/// hace E/S). Vive en Sim.Tests porque hoy es un instrumento de prueba; si el CLI de §12 (punto 5) se
/// implementa, esta misma responsabilidad se traslada a <c>/Balance</c>, no a <c>/Sim</c>.
/// </summary>
public static class BalanceRegistryFile
{
    public static void Save(string path, BalanceRegistryEntry entry)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, BalanceRegistry.Serialize(entry));
    }

    public static BalanceRegistryEntry? Load(string path) =>
        File.Exists(path) ? BalanceRegistry.Deserialize(File.ReadAllText(path)) : null;
}
