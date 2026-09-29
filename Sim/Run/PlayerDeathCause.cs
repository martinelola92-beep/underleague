namespace Underleague.Sim.Run;

/// <summary>
/// Cómo murió un jugador propio (ADR 0163, RF-122): lo que la esquela de la Gaceta cuenta. Vive en
/// <see cref="RunState.Counters"/> con la clave <see cref="RunState.DeathCausePrefix"/> + id de jugador
/// (contabilidad de run de clave libre, como <see cref="RunState.RivalCreditPrefix"/>: no sube la versión
/// del esquema). El valor numérico se guarda, así que <b>no se renumera</b>.
/// </summary>
public enum PlayerDeathCause
{
    /// <summary>No consta (guardados de antes de este registro): la esquela no afirma nada.</summary>
    Unknown = 0,

    /// <summary>En un partido, a manos de un jugador rival concreto (el matador viene en el evento DEATH).</summary>
    MatchByOpponent = 1,

    /// <summary>En un partido sin matador (RF-093 vía 1 sin autor identificable).</summary>
    MatchNoAuthor = 2,

    /// <summary>Sacrificado en un evento del donante para pasar su perk a un compañero (ADR 0159).</summary>
    Sacrifice = 3,

    /// <summary>En la mesa del matasanos de la clínica (ADR 0099).</summary>
    Quack = 4,
}
