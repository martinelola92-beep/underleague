namespace Underleague.Sim.Model;

/// <summary>Lo que hace un tipo de turba (ADR 0167). Cada efecto sale de <c>data/mobs/*.json</c>.</summary>
public enum MobEffectKind
{
    /// <summary>Un jugador de campo en el campo, al azar entre los dos equipos, sale con lesión leve. Nunca mata.</summary>
    Injure,

    /// <summary>Los dos equipos presionan al portador (la consigna <c>Press</c> de la ADR 0166).</summary>
    PressBoth,

    /// <summary>El rival (equipo visitante, su grada) juega en orden ofensiva (la orden de la ADR 0166).</summary>
    TheirOffensive,
}

/// <summary>
/// El tipo de turba de un partido (ADR 0167), ya resuelto: el motor no consulta ningún catálogo. Se deriva del
/// nodo como el árbitro (<c>IRunSystems.MobFor</c>) y viaja en <see cref="MatchSetup.Mob"/>. Sin efectos (tipo
/// <c>plain</c>) la turba es la de siempre: gol de oro sin árbitro.
/// </summary>
/// <param name="Id">Id de datos del tipo (<c>plain</c>, <c>invader</c>…).</param>
/// <param name="Effects">Lo que pasa al entrar la turba, y ahora mismo si se provoca.</param>
public sealed record MobSetup(string Id, IReadOnlyList<MobEffectKind> Effects);
