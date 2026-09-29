using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Bets;

/// <summary>
/// Lo que necesita una condición de apuesta para resolverse: el <see cref="MatchSetup"/> con el que se jugó,
/// el <see cref="MatchResult"/> (secuencia de eventos e informe) y dos datos de la run que el partido no
/// conoce. El equipo del jugador es SIEMPRE el 0 (W-15). Sin RNG, sin reloj: la incertidumbre de una
/// apuesta es el propio partido (ADR 0157, punto 3).
/// </summary>
/// <param name="Setup">Partido tal y como se jugó (tras <c>TransformMatch</c>): alineación y plantillas.</param>
/// <param name="Result">Eventos e informe del partido.</param>
/// <param name="IsYouth">
/// ¿Es canterano (RF-114c) el jugador propio con ese id? Es un dato de <c>RunPlayer</c>, no de
/// <c>PlayerDefinition</c>, así que lo aporta quien construye el contexto. Null = nadie lo es.
/// </param>
/// <param name="TargetPlayerId">Jugador rival nombrado por <see cref="BetKind.HuntTheStar"/>; -1 si no aplica.</param>
public sealed record BetContext(
    MatchSetup Setup,
    MatchResult Result,
    Func<int, bool>? IsYouth = null,
    int TargetPlayerId = -1);

/// <summary>
/// Evaluador puro de las once condiciones de apuesta (ADR 0157). Cada una se lee de los hechos del partido
/// —los eventos ordenados y el informe—, nunca de una tirada nueva. Convenciones comunes:
/// <list type="bullet">
/// <item>Un evento con <c>Detail</c> acabado en <c>:cancelled</c> (lo anuló un perk, §3 del motor) no ocurrió:
/// no cuenta para ninguna condición.</item>
/// <item>«Ganar» es <c>Report.Winner == 0</c>. Equipo propio = 0; rival = 1.</item>
/// <item>«Lesión» es un evento <c>INJURY</c> (leve o grave) de la víctima en <c>Actor</c>; el causante, si lo
/// hay, viaja en <c>Opponent</c>. Una lesión causada «por el jugador» es la de un rival cuyo <c>Opponent</c>
/// es un jugador propio.</item>
/// <item>«Tarjeta» es un evento <c>CARD</c>, amarilla o roja.</item>
/// <item>«Falta no señalada» es un <c>FOUL</c> de detalle <c>unseen</c> ANTES de <c>MOB_START</c>: en la
/// turba no hay árbitro y el motor las emite todas como no vistas, así que no cuentan.</item>
/// </list>
/// </summary>
public static class BetConditions
{
    private const string CancelledSuffix = ":cancelled";

    /// <summary>Titulares a partir de los cuales ya no se sale «con menos de siete» (RF-002d).</summary>
    private const int FullSide = 7;

    /// <summary>Goles de diferencia de <see cref="BetKind.Thrashing"/>.</summary>
    private const int ThrashingMargin = 3;

    /// <summary>Máximo de rivales en el campo al terminar de <see cref="BetKind.Thrashing"/>.</summary>
    private const int ThrashingMaxRivals = 6;

    /// <summary>Jugadores propios distintos que han de marcar en <see cref="BetKind.ThreeNames"/>.</summary>
    private const int ThreeNamesScorers = 3;

    /// <summary>Faltas propias no señaladas que pide <see cref="BetKind.RefereeBlind"/>.</summary>
    private const int UnseenFoulsNeeded = 3;

    /// <summary>¿Se cumple la condición <paramref name="kind"/> en este partido?</summary>
    public static bool Evaluate(BetKind kind, BetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return kind switch
        {
            BetKind.BloodBeforeGoals => BloodBeforeGoals(context),
            BetKind.HuntTheStar => HuntTheStar(context),
            BetKind.EyeForEye => EyeForEye(context),
            BetKind.Comeback => Comeback(context),
            BetKind.IntoTheMob => IntoTheMob(context),
            BetKind.CleanHands => CleanHands(context),
            BetKind.Thrashing => Thrashing(context),
            BetKind.ThreeNames => ThreeNames(context),
            BetKind.YouthDecides => YouthDecides(context),
            BetKind.ShortAndClean => ShortAndClean(context),
            BetKind.RefereeBlind => RefereeBlind(context),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "condición de apuesta desconocida"),
        };
    }

    /// <summary>
    /// <see cref="BetKind.BloodBeforeGoals"/>: la primera lesión del partido, de cualquiera, llega antes que
    /// el primer gol. Si no hubo goles, basta con que hubo alguna lesión. No exige ganar.
    /// </summary>
    private static bool BloodBeforeGoals(BetContext c)
    {
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!Live(e))
            {
                continue;
            }

            if (e.Type == EventType.Injury)
            {
                return true;
            }

            if (e.Type == EventType.Goal)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// <see cref="BetKind.HuntTheStar"/>: el jugador rival nombrado (<see cref="BetContext.TargetPlayerId"/>)
    /// sale del campo antes del final por lesión, muerte o expulsión (roja). No exige ganar: es la apuesta de
    /// la carnicería. Sin jugador nombrado (id &lt; 0) no se cumple.
    /// </summary>
    private static bool HuntTheStar(BetContext c)
    {
        if (c.TargetPlayerId < 0)
        {
            return false;
        }

        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!Live(e) || e.Actor != c.TargetPlayerId || e.Team != 1)
            {
                continue;
            }

            if (e.Type is EventType.Injury or EventType.Death
                || (e.Type == EventType.Card && e.Detail == "red"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <see cref="BetKind.EyeForEye"/>: hubo al menos una lesión propia, al menos una lesión de un rival
    /// causada por un jugador propio, y el equipo del jugador gana. Sin lesión propia no hay «ojo» que cobrar.
    /// </summary>
    private static bool EyeForEye(BetContext c) =>
        Won(c) && OwnInjuries(c) >= 1 && InjuriesCausedByUs(c) >= 1;

    /// <summary>
    /// <see cref="BetKind.Comeback"/>: en algún momento el marcador iba en contra del equipo propio (tras
    /// cualquier gol no anulado, rival &gt; propio) y el equipo gana.
    /// </summary>
    private static bool Comeback(BetContext c)
    {
        if (!Won(c))
        {
            return false;
        }

        int own = 0;
        int rival = 0;
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!Live(e) || e.Type != EventType.Goal)
            {
                continue;
            }

            if (e.Team == 0)
            {
                own++;
            }
            else
            {
                rival++;
            }

            if (rival > own)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <see cref="BetKind.IntoTheMob"/>: el partido llegó a la prórroga de gol de oro (turba, RF-055b) y lo
    /// gana el equipo del jugador. Cuenta también la victoria por desempate al agotarse el tiempo de la
    /// prórroga sin gol de oro (medido: la semilla 1 de 50 contra 50 termina así): quien lo gana ha «llegado
    /// a la turba y salido ganando», que es lo que se apuesta.
    /// </summary>
    private static bool IntoTheMob(BetContext c) => Won(c) && c.Result.Report.WentToGoldenGoal;

    /// <summary>
    /// <see cref="BetKind.CleanHands"/>: gana, causa al menos una lesión a un rival (atribuida a un jugador
    /// propio) y su equipo no ve ninguna tarjeta, amarilla ni roja.
    /// </summary>
    private static bool CleanHands(BetContext c) =>
        Won(c) && InjuriesCausedByUs(c) >= 1 && OwnCards(c) == 0;

    /// <summary>
    /// <see cref="BetKind.Thrashing"/>: gana por 3 goles o más y el rival termina con 6 o menos en el campo.
    /// «En el campo al terminar» = jugadores del rival con tiempo en el campo que no lo abandonaron
    /// (<c>LeftPitchTick &lt; 0</c>): cuenta a los suplentes que entraron y descuenta lesionados, muertos y
    /// expulsados sin reemplazo.
    /// </summary>
    private static bool Thrashing(BetContext c)
    {
        var report = c.Result.Report;
        if (!Won(c) || report.Goals[0] - report.Goals[1] < ThrashingMargin)
        {
            return false;
        }

        int rivalsOnPitch = 0;
        var players = report.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.Team == 1 && p.TicksOnPitch > 0 && p.LeftPitchTick < 0)
            {
                rivalsOnPitch++;
            }
        }

        return rivalsOnPitch <= ThrashingMaxRivals;
    }

    /// <summary>
    /// <see cref="BetKind.ThreeNames"/>: gana con goles no anulados de al menos tres jugadores propios
    /// distintos (el goleador es el <c>Actor</c> del evento; no hay goles en propia puerta en el motor).
    /// </summary>
    private static bool ThreeNames(BetContext c)
    {
        if (!Won(c))
        {
            return false;
        }

        var scorers = new List<int>();
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (Live(e) && e.Type == EventType.Goal && e.Team == 0 && !scorers.Contains(e.Actor))
            {
                scorers.Add(e.Actor);
            }
        }

        return scorers.Count >= ThreeNamesScorers;
    }

    /// <summary>
    /// <see cref="BetKind.YouthDecides"/>: gana y el gol que da la victoria es de un canterano. «El gol de la
    /// victoria» es el gol propio a partir del cual el equipo ya nunca deja de ir por delante en el marcador
    /// final: el (goles del rival + 1)-ésimo gol propio, contando solo goles no anulados. Con 3-1 es el
    /// segundo gol propio (el 2-1 ya no se remonta), no el tercero.
    /// </summary>
    private static bool YouthDecides(BetContext c)
    {
        if (!Won(c) || c.IsYouth is null)
        {
            return false;
        }

        int rivalFinal = c.Result.Report.Goals[1];
        int own = 0;
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!Live(e) || e.Type != EventType.Goal || e.Team != 0)
            {
                continue;
            }

            own++;
            if (own == rivalFinal + 1)
            {
                return c.IsYouth(e.Actor);
            }
        }

        return false;
    }

    /// <summary>
    /// <see cref="BetKind.ShortAndClean"/>: el equipo sale con menos de 7 titulares (casillas de la
    /// alineación con la que se jugó, RF-002d), gana y no encaja ningún gol.
    /// </summary>
    private static bool ShortAndClean(BetContext c) =>
        Won(c) && c.Setup.Home.Lineup.Slots.Count < FullSide && c.Result.Report.Goals[1] == 0;

    /// <summary>
    /// <see cref="BetKind.RefereeBlind"/>: gana con al menos 3 faltas propias no señaladas (<c>FOUL</c> de
    /// detalle <c>unseen</c>) cometidas con el árbitro en el campo, es decir, antes de <c>MOB_START</c>.
    /// </summary>
    private static bool RefereeBlind(BetContext c)
    {
        if (!Won(c))
        {
            return false;
        }

        int unseen = 0;
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Type == EventType.MobStart)
            {
                break;
            }

            if (Live(e) && e.Type == EventType.Foul && e.Team == 0 && e.Detail == "unseen")
            {
                unseen++;
            }
        }

        return unseen >= UnseenFoulsNeeded;
    }

    private static bool Won(BetContext c) => c.Result.Report.Winner == 0;

    private static bool Live(MatchEvent e) => !e.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal);

    private static int OwnInjuries(BetContext c)
    {
        int count = 0;
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            if (Live(events[i]) && events[i].Type == EventType.Injury && events[i].Team == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int InjuriesCausedByUs(BetContext c)
    {
        int count = 0;
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (Live(e) && e.Type == EventType.Injury && e.Team == 1 && IsOwn(c, e.Opponent))
            {
                count++;
            }
        }

        return count;
    }

    private static int OwnCards(BetContext c)
    {
        int count = 0;
        var events = c.Result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            if (Live(events[i]) && events[i].Type == EventType.Card && events[i].Team == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsOwn(BetContext c, int playerId)
    {
        if (playerId < 0)
        {
            return false;
        }

        var players = c.Setup.Home.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == playerId)
            {
                return true;
            }
        }

        return false;
    }
}
