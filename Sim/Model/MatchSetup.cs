namespace Underleague.Sim.Model;

/// <summary>Configuración del árbitro de un partido concreto.</summary>
public sealed record RefereeSetup(string Name, RefereeTrait Trait, int InitialBias);

/// <summary>Entrada completa para simular un partido: equipos y árbitro.</summary>
public sealed record MatchSetup(TeamSetup Home, TeamSetup Away, RefereeSetup Referee);
