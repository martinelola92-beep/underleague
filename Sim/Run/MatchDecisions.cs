using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Sim.Run;

/// <summary>
/// Decisiones del jugador <b>dentro</b> de un partido, que viajan como estado inicial (docs/arquitectura.md,
/// ADR 0094): la activación manual de un consumible (RF-082) y las sustituciones forzadas (AZ-F). Vacías por
/// defecto: <c>/Balance</c> y los tests no traen ninguna y <see cref="RunEngine.EnterMatch"/> resuelve los
/// puntos de decisión con la política por defecto.
/// </summary>
public sealed record MatchDecisions(
    IReadOnlyList<ManualActivation> ManualActivations,
    IReadOnlyList<Substitution> Substitutions)
{
    public static MatchDecisions None { get; } = new(Array.Empty<ManualActivation>(), Array.Empty<Substitution>());
}
