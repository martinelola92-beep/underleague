using Underleague.Sim.Run.View;

namespace Underleague.Game.Match;

/// <summary>
/// Qué pool de sonido le corresponde a cada <see cref="MomentKind"/>. Es la <b>única</b> tabla que traduce
/// el vocabulario de la simulación al de audio: el resto del juego pide un pool por nombre y el
/// <c>AudioManager</c> elige la variante, así que añadir <c>heavy_hit_06.wav</c> no toca nada de aquí.
///
/// <para><b>Qué se oye es una decisión de presentación</b> (ADR 0119: la simulación produce momentos, el
/// director decide cómo se presentan). Por eso la tabla vive en <c>/Game</c> y no en <c>/Sim</c>, y por eso
/// un momento puede no tener sonido: el silencio también se elige.</para>
///
/// <para><b>Lo que falta y dónde irá</b>: los pools de <c>combat/</c> —golpe ligero, medio, fuerte,
/// aplastamiento, entrada, caída— no cuelgan de un momento sino de un <b>evento</b> suelto del partido
/// (TACKLE, INJURY, el derribo). Esa capa necesita que la pantalla recorra los eventos de cada fotograma,
/// que hoy no hace; el árbol de carpetas ya está puesto para cuando se haga, y el gesto será el mismo:
/// <c>AudioManager.PlayRandomSfx("combat/heavy_hit")</c>.</para>
/// </summary>
public static class MomentSounds
{
    /// <summary>
    /// El pool del momento, o <c>null</c> si ese momento no suena. Un pool que todavía no tiene ficheros
    /// no es un problema: el <c>AudioManager</c> lo ignora en silencio.
    /// </summary>
    public static string? PoolFor(MomentKind kind) => kind switch
    {
        // El silbato abre el partido y corta la falta: es el mismo sonido y el mismo gesto.
        MomentKind.Kickoff => "referee/whistle",
        MomentKind.Foul => "referee/whistle",

        // La tarjeta no suena a silbato —ya sonó—, suena a grada. Es la reacción, que es lo que el jugador
        // está mirando mientras el árbitro la saca.
        MomentKind.Yellow => "crowd/boo",
        MomentKind.Red => "crowd/boo",

        MomentKind.Goal => "football/goal",

        // Las dos lesiones comparten pool a propósito: lo que cambia entre una leve y una grave no es el
        // sonido del jugador, es todo lo demás (el congelado, el estandarte, la bandeja).
        MomentKind.MinorInjury => "players/pain",
        MomentKind.SevereInjury => "players/pain",

        // La muerte es un vacío, no un estruendo: la grada toma aire.
        MomentKind.Death => "crowd/gasp",

        // La turba entra al campo y el árbitro se va: bocina para el final del reglamentario y para el
        // pitido final, abucheo para la turba.
        MomentKind.Mob => "crowd/boo",
        MomentKind.RefereeLeaves => "referee/horn",
        MomentKind.FullTime => "referee/horn",

        // Sin sonido a propósito: son decisiones de gestión, no sucesos del campo. Ponerles un efecto
        // convertiría en espectáculo algo que el jugador está leyendo.
        MomentKind.Consumable => null,
        MomentKind.Substitution => null,
        _ => null,
    };
}
