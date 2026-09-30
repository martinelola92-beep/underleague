using System;
using Underleague.Sim.Engine;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Match;

/// <summary>
/// ¿Este momento <b>detiene el juego</b> de verdad? (BB-D, ADR 0173). Es la pregunta que decide si el director
/// hace una pausa breve antes de enseñarlo: una falta que el árbitro pita, una tarjeta, una lesión que para el
/// partido. Una falta que no ve, un consumible o una sustitución no paran nada, así que no se congela la imagen
/// por ellos.
///
/// <para><b>Se lee de la traza, no se adivina por el tipo de suceso</b>: el juego se ha parado si en los
/// fotogramas del momento el motor tiene pendiente el saque de falta o el penalti que ese suceso provoca (<see cref="MatchTrace.RestartAt"/>). Es el
/// mismo dato con el que <c>MatchPitchView3D</c> decide cómo se reanuda, y el motor lo escribe con sus propias
/// reglas (ADR 0143, 0147): si mañana una lesión deja de parar el juego, o una falta empieza a hacerlo, esto lo
/// sigue sin tocar nada aquí.</para>
///
/// <para><b>No decide nada del partido</b> (RT-014): el partido está simulado, esto sólo dice si la pantalla se
/// queda un instante en el fotograma anterior al suceso. Sin Godot, como el director.</para>
/// </summary>
public static class PlayStops
{
    /// <summary>
    /// Los sucesos que pueden parar el juego. El gol (tiene su congelado propio), la muerte, el final y la
    /// decisión (pausan por sí solos), el saque inicial, la turba y el árbitro que se va (banda sin congelar,
    /// docs/ui/README §4), y el consumible o la sustitución (no paran nada) quedan fuera por diseño.
    /// </summary>
    public static bool CanStopPlay(MomentKind kind) => kind is MomentKind.Foul or MomentKind.Yellow or MomentKind.Red
        or MomentKind.MinorInjury or MomentKind.SevereInjury;

    /// <summary>
    /// True si ese momento es de los que pueden parar el juego, no fue anulado por un perk (lo anulado no
    /// ocurrió) y el motor tiene una reanudación pendiente en sus fotogramas.
    /// </summary>
    public static bool Holds(MatchMoment moment, MatchTrace trace)
    {
        ArgumentNullException.ThrowIfNull(moment);
        ArgumentNullException.ThrowIfNull(trace);

        if (moment.Cancelled || !CanStopPlay(moment.Kind) || trace.FrameCount == 0)
        {
            return false;
        }

        // Los fotogramas del propio momento, sin margen: la falta resuelve la lesión de la entrada y abre el saque
        // en el mismo tick (OpenPendingFreeKick), y con dos fotogramas de margen entraban «faltas» que el árbitro
        // no vio (4 de 298 en trescientos partidos); sin margen, 0 de 298 y las 784 pitadas, todas.
        //
        // Y sólo cuenta la reanudación que PROVOCA el suceso: falta, tarjeta y lesión de una entrada terminan en
        // saque de falta o penalti. Un saque de banda, de puerta, de esquina o de centro que coincida en esos
        // fotogramas es otra cosa (el balón salió, el juego siguió), y pausar por él sería congelar sin que el
        // suceso hubiera parado nada.
        int last = Math.Min(moment.LastFrame, trace.FrameCount - 1);
        for (int frame = Math.Max(moment.Frame, 0); frame <= last; frame++)
        {
            if (trace.RestartAt(frame) is RestartKind.FreeKick or RestartKind.Penalty)
            {
                return true;
            }
        }

        return false;
    }
}
