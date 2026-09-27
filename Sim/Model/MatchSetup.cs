namespace Underleague.Sim.Model;

/// <summary>Configuración del árbitro de un partido concreto.</summary>
public sealed record RefereeSetup(string Name, RefereeTrait Trait, int InitialBias)
{
    /// <summary>
    /// Media banda del campo que no ve, solo relevante con <see cref="RefereeTrait.OneEyed"/> (ADR 0158).
    /// Propiedad fuera del constructor posicional para no romper las llamadas existentes.
    /// </summary>
    public RefereeSide BlindSide { get; init; } = RefereeSide.None;
}

/// <summary>Entrada completa para simular un partido: equipos y árbitro.</summary>
public sealed record MatchSetup(TeamSetup Home, TeamSetup Away, RefereeSetup Referee);
