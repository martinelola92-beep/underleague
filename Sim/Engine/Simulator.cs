using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>Configuración de una simulación de partido.</summary>
public sealed record SimConfig(bool CollectLog = true, (int PlayerId, int Tick)? DumpUtility = null, int? RegulationTicksOverride = null)
{
    /// <summary>Configuración por defecto: con log, sin volcado de utilidad, duración reglamentaria estándar.</summary>
    public static SimConfig Default { get; } = new();
}

/// <summary>Resultado de simular un partido completo: secuencia ordenada de eventos e informe final.</summary>
public sealed record MatchResult(IReadOnlyList<MatchEvent> Events, MatchReport Report);

/// <summary>Punto de entrada público del simulador de partidos (RT-013).</summary>
public static class Simulator
{
    /// <summary>
    /// Puro: sin efectos secundarios, sin E/S, sin reloj. Valida el setup (lanza ArgumentException con
    /// mensaje claro) y ejecuta el partido completo devolviendo eventos e informe.
    /// Implementación pendiente del motor (paquete B, docs/fase0-diseno.md §3).
    /// </summary>
    public static MatchResult Run(MatchSetup setup, ulong seed, Catalog catalog, SimConfig config)
    {
        throw new NotSupportedException("engine pending (package B)");
    }
}
