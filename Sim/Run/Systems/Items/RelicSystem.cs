namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Qué reliquia deja un jugador propio al morir (ADR 0161 §2): un objeto de <c>data/items/</c> marcado
/// como reliquia, elegido según lo que ese jugador hizo en la run (<see cref="RunCareer"/>) hasta el
/// final del partido en el que murió —<c>MatchResolution</c> actualiza la carrera ANTES de que
/// <c>StandardRunSystems.AfterMatch</c> llame aquí, así que ese último partido cuenta—.
///
/// <para><b>Determinista, sin tirada</b> (ADR 0161 §2): la misma carrera produce siempre la misma clase.
/// Prioridad fija goleador &gt; carnicero &gt; muro cuando más de un umbral se cumple a la vez —mismo
/// estilo que el resto del motor resuelve empates, aunque aquí no hay empate real: es una regla de
/// contenido, no de <c>RT-041</c>—, y genérica si ninguno se cumple.</para>
///
/// <para><b>Los tres umbrales son PROVISIONALES, SIN MEDIR</b> (Regla H, <c>CLAUDE.md</c>): no hay todavía
/// un lote de <c>/Balance</c> que diga con cuántos goles, lesiones o entradas un jugador "destaca" de
/// verdad en una run de fase 2. Se fijan aquí, bajos a propósito para que la reliquia con nombre no sea un
/// caso raro, y quedan pendientes de remedirse cuando haya datos de runs completas.</para>
/// </summary>
public static class RelicSystem
{
    /// <summary>Goles desde los que un jugador "destaca" como goleador (provisional, sin medir).</summary>
    public const int ScorerGoalsThreshold = 3;

    /// <summary>Lesiones causadas desde las que un jugador "destaca" como carnicero (provisional, sin medir).</summary>
    public const int ButcherInjuriesThreshold = 2;

    /// <summary>Entradas ganadas desde las que un jugador "destaca" como muro (provisional, sin medir).</summary>
    public const int WallTacklesThreshold = 5;

    /// <summary>Clase de reliquia para esa carrera (ADR 0161 §2). Nunca lanza: el caso por defecto es genérica.</summary>
    public static RelicKind Classify(RunCareer career)
    {
        ArgumentNullException.ThrowIfNull(career);

        if (career.Goals >= ScorerGoalsThreshold)
        {
            return RelicKind.Scorer;
        }

        if (career.InjuriesCaused >= ButcherInjuriesThreshold)
        {
            return RelicKind.Butcher;
        }

        if (career.TacklesWon >= WallTacklesThreshold)
        {
            return RelicKind.Wall;
        }

        return RelicKind.Generic;
    }
}
