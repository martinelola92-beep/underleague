using System.Text.Json;
using System.Text.Json.Serialization;

namespace Underleague.Sim.Analysis;

// RT-012: /Sim no hace E/S. Esta clase solo serializa/deserializa texto (puro, sin tocar disco); leer y
// escribir el fichero es responsabilidad de quien orquesta (Sim.Tests o /Balance), igual que
// Sim.Data.DataLoader.FromJson recibe los ficheros ya leídos.

/// <summary>Un candidato de valor ya medido, con sus métricas por brazo (§10).</summary>
public sealed record BalanceCandidateRecord(
    double Value,
    IReadOnlyDictionary<string, double> ArmedMetrics,
    IReadOnlyDictionary<string, double> ControlMetrics,
    BalanceState ResultingState);

/// <summary>
/// Registro por perk (§10 de docs/analisis/protocolo-balanceo-automatizado.md): suficiente para que otro
/// agente/sesión retome el proceso sin reconstruir el historial a mano — soporta el checkpoint/
/// reanudación de §9.1 (el lote nunca depende de mantener el estado solo en memoria).
/// </summary>
/// <param name="Timestamp">
/// Marca de tiempo en formato ISO-8601 ("o", round-trip), como texto: RT-012 prohíbe que /Sim consulte
/// el reloj o incluso referencie <c>DateTime</c>/<c>DateTimeOffset</c>
/// (Sim.Tests/Engine/ArchitectureTests.cs), así que quien orquesta la escribe ya resuelta.
/// </param>
public sealed record BalanceRegistryEntry(
    string PerkId,
    string ProtocolVersion,
    string Timestamp,
    BalanceState State,
    string Category,
    string Readiness,
    string PrimaryMetric,
    IReadOnlyList<double> ValuesTried,
    IReadOnlyList<BalanceCandidateRecord> Candidates,
    string? ReasonForChange = null,
    string? ReasonForOutcome = null);

/// <summary>Serialización del registro — texto puro (RT-012: ningún fichero se toca desde aquí).</summary>
public static class BalanceRegistry
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(BalanceRegistryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return JsonSerializer.Serialize(entry, Options);
    }

    public static BalanceRegistryEntry? Deserialize(string json) =>
        JsonSerializer.Deserialize<BalanceRegistryEntry>(json, Options);
}
