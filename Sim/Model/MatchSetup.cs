namespace Underleague.Sim.Model;

/// <summary>Configuración del árbitro de un partido concreto.</summary>
public sealed record RefereeSetup(string Name, RefereeTrait Trait, int InitialBias)
{
    /// <summary>
    /// Media banda del campo que no ve, solo relevante con <see cref="RefereeTrait.OneEyed"/> (ADR 0158).
    /// Propiedad fuera del constructor posicional para no romper las llamadas existentes.
    /// </summary>
    public RefereeSide BlindSide { get; init; } = RefereeSide.None;

    /// <summary>
    /// Id del <c>RunReferee</c> de la run que pita este partido (ADR 0158, revisión independiente): -1 si
    /// no hay ninguno identificable (partido suelto sin plantel de árbitros, por ejemplo en pruebas). Es
    /// la clave con la que <c>MatchResolution.ApplyRefereeMemory</c> y <c>RefereeView</c> encuentran de
    /// vuelta al mismo árbitro dentro de <c>RunState.Referees</c> -por id, no por nombre: dos árbitros
    /// nunca comparten nombre en el mismo idioma (RefereeLoader lo valida), pero el nombre es un dato de
    /// presentación y el id es la identidad de verdad-.
    /// </summary>
    public int RefereeId { get; init; } = -1;
}

/// <summary>Entrada completa para simular un partido: equipos y árbitro.</summary>
public sealed record MatchSetup(TeamSetup Home, TeamSetup Away, RefereeSetup Referee)
{
    /// <summary>
    /// Tipo de turba del partido (ADR 0167): qué pasa si hay empate y la turba entra, y qué hace el consumible
    /// que la provoca. Null = sin tipo, la turba de siempre (partidos sueltos, pruebas, <c>/Balance</c> sin run).
    /// </summary>
    public MobSetup? Mob { get; init; }
}
