using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Si una población de destinatarios se pudo resolver antes de jugar, o por qué no (§16, punto 3 del
/// encargo del 18 sep 2026: "si un caso concreto necesita una semántica de gameplay que el tooling no
/// puede inferir, déjalo explícitamente bloqueado" — en vez de fingir que se resolvió).
/// </summary>
public enum PopulationResolution
{
    /// <summary>Población fija, resoluble antes de jugar con los datos ya generados de la plantilla.</summary>
    Resolved,

    /// <summary>Depende de estado del partido en vivo (posición, disparador dinámico) — no hay una población fija que resolver de antemano.</summary>
    RequiresLiveMatchState,

    /// <summary>Depende de <c>Links</c> (ADR 0021, <c>LinkTable</c>), resueltos al construir el partido — este resolver no los reproduce todavía.</summary>
    RequiresLinkResolution,
}

/// <summary>Resultado de resolver la población de un efecto sobre una plantilla concreta.</summary>
public readonly record struct PopulationResolutionResult(
    PopulationResolution Status,
    IReadOnlyList<int> AffectedPlayerIndexes,
    string Note)
{
    /// <summary>
    /// Cuántos jugadores DISTINTOS del portador recibe el efecto — la comprobación explícita contra la
    /// atribución falsa que pedía el encargo ("effect on N actors ≈ effect on owner"): si esto es 0 para
    /// un efecto que no es <c>Owner</c>/<c>Actor</c>, algo va mal en la resolución, no en el perk.
    /// </summary>
    public int AffectedCount => AffectedPlayerIndexes.Count;
}

/// <summary>
/// Resuelve la población de destinatarios de un <see cref="EffectTarget"/> sobre una plantilla ya
/// generada — estructural, por forma de destinatario (§3.2 punto 7), no una tabla de casos por perk.
/// Reutiliza <see cref="PlayerDefinition.HasTag"/>, el mismo primitivo que ya usa
/// <c>EffectEngine.ResolveTargets</c> en el motor real — no se duplica la lógica de emparejamiento
/// dinámico (pairwise, adyacencia en vivo), solo la parte que es determinable de la plantilla sola.
/// </summary>
public static class EffectPopulationResolver
{
    /// <summary>
    /// Resuelve los índices (dentro de <paramref name="ownerTeam"/> u <paramref name="opposingTeam"/>,
    /// según corresponda) afectados por un efecto con destinatario <paramref name="target"/>.
    /// <paramref name="ownerIndex"/> es la posición del portador en <paramref name="ownerTeam"/>.
    /// </summary>
    public static PopulationResolutionResult Resolve(
        EffectTarget target, string targetTag, TeamSetup ownerTeam, TeamSetup opposingTeam, int ownerIndex)
    {
        ArgumentNullException.ThrowIfNull(ownerTeam);
        ArgumentNullException.ThrowIfNull(opposingTeam);

        switch (target)
        {
            case EffectTarget.Owner:
            case EffectTarget.Actor:
                return new(PopulationResolution.Resolved, new[] { ownerIndex }, "un único destinatario: el propio portador");

            case EffectTarget.Team:
                return new(
                    PopulationResolution.Resolved,
                    Enumerable.Range(0, ownerTeam.Players.Count).ToList(),
                    "equipo completo del portador");

            case EffectTarget.OpposingTeam:
                return new(
                    PopulationResolution.Resolved,
                    Enumerable.Range(0, opposingTeam.Players.Count).ToList(),
                    "equipo rival completo");

            case EffectTarget.WithTag:
                {
                    var indexes = new List<int>();
                    for (int i = 0; i < ownerTeam.Players.Count; i++)
                    {
                        if (ownerTeam.Players[i].HasTag(targetTag))
                        {
                            indexes.Add(i);
                        }
                    }

                    return new(
                        PopulationResolution.Resolved, indexes,
                        $"jugadores del equipo del portador con la etiqueta '{targetTag}' ({indexes.Count} de {ownerTeam.Players.Count}, incluido el portador si él mismo la lleva)");
                }

            case EffectTarget.Adjacent:
            case EffectTarget.AdjacentWithTag:
                return new(
                    PopulationResolution.RequiresLiveMatchState, Array.Empty<int>(),
                    $"{target}: adyacencia ESTÁTICA de casilla-hogar (ADR 0021) — el dato (Lineup) ya está en TeamSetup, pero este resolver no reproduce todavía Pitch.AreAdjacent; no se inventa una aproximación");

            case EffectTarget.AdjacentOpponents:
                return new(
                    PopulationResolution.RequiresLiveMatchState, Array.Empty<int>(),
                    "adyacencia DINÁMICA (posición real en el instante del efecto, paquete AY-cuatro-primitivas) — no es una población fija resoluble antes de jugar");

            case EffectTarget.Target:
            case EffectTarget.Opponent:
                return new(
                    PopulationResolution.RequiresLiveMatchState, Array.Empty<int>(),
                    $"{target}: destinatario dinámico por disparo (el rival concreto de cada evento, p. ej. a quién se entra) — cambia cada vez, no es una población fija de la plantilla");

            case EffectTarget.Linked:
            case EffectTarget.LinkedWithTag:
                return new(
                    PopulationResolution.RequiresLinkResolution, Array.Empty<int>(),
                    $"{target}: depende de Links (ADR 0021, LinkTable), resueltos al construir el partido — este resolver no los reproduce todavía, no se adivina quién está vinculado");

            default:
                return new(PopulationResolution.RequiresLiveMatchState, Array.Empty<int>(), $"{target}: sin resolución estática definida");
        }
    }
}
