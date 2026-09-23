using System;
using Underleague.Sim.Run.View;

namespace Underleague.Game.Match;

/// <summary>
/// Qué suena en cada <see cref="MomentKind"/>: la <b>capa de retransmisión</b>, la que subraya lo que el
/// pregón está contando. La otra capa, la del campo, es <see cref="MatchEventSounds"/> y cuelga de los
/// eventos crudos.
///
/// <para><b>Un momento puede sonar en varias capas a la vez</b> y por eso devuelve una lista: un gol es la
/// red <i>y</i> la grada, una muerte es el jadeo <i>y</i> el cuerno. Son sucesos distintos que ocurren
/// juntos, no un sonido con dos mitades — y separados, cada uno se puede cambiar solo.</para>
///
/// <para><b>Qué se oye es una decisión de presentación</b> (ADR 0119: la simulación produce momentos, el
/// director decide cómo se presentan). Por eso la tabla vive en <c>/Game</c> y no en <c>/Sim</c>, y por eso
/// un momento puede no tener sonido: el silencio también se elige.</para>
///
/// <para>Ningún nombre de fichero aparece aquí: se pide un pool y el <c>AudioManager</c> reparte las
/// variantes, así que añadir <c>Crowd_Gasp_04.wav</c> no toca esta tabla. Un pool que todavía no tiene
/// ficheros tampoco es un problema: la llamada es un no-op anotado una vez.</para>
/// </summary>
public static class MomentSounds
{
    private static readonly string[] None = Array.Empty<string>();

    // El silbato abre el partido y corta la falta: es el mismo sonido y el mismo gesto.
    private static readonly string[] Whistle = { "referee/whistle" };

    // La tarjeta no suena a silbato —ya sonó—, suena a grada. Es la reacción, que es lo que el jugador
    // está mirando mientras el árbitro la saca.
    private static readonly string[] Boo = { "crowd/boo" };

    // El gol son dos cosas a la vez y ninguna sobra: el balón entrando y la grada. Pero la grada **es la
    // tuya**: el marcador, el estandarte y el «¡Viva Altos Hornos FC!» ya cuentan el partido desde tu lado,
    // y una grada reventando de alegría cuando te marcan daba la información al revés en el momento más
    // cargado del partido (lo encontró la revisión independiente). El balón suena igual en los dos casos
    // —la red no toma partido—; lo que cambia es quién grita.
    private static readonly string[] GoalFor = { "football/goal", "crowd/goal" };
    private static readonly string[] GoalAgainst = { "football/goal", "crowd/boo" };

    // Las dos lesiones comparten pool a propósito: lo que cambia entre una leve y una grave no es el
    // sonido del jugador, es todo lo demás (el congelado, el estandarte, la bandeja). El impacto —el
    // hueso— lo pone la capa de campo, que suena antes: primero el golpe, después el grito.
    private static readonly string[] Pain = { "players/pain" };

    // La muerte es un vacío y un anuncio: la grada toma aire y suena el cuerno. No hay estruendo aquí —el
    // crujido ya sonó en la capa de campo, en la casilla, medio segundo antes.
    private static readonly string[] Death = { "crowd/gasp", "death" };

    // El árbitro se va: el cuerno del final anticipado y la bronca. Mientras no haya cuerno, la bronca
    // sostiene el momento ella sola.
    private static readonly string[] RefereeLeaves = { "referee/horn", "crowd/boo" };

    // Pitido final: el silbato, y nada más. Llevaba `crowd/cheer` y la revisión independiente tenía razón
    // en tumbarlo: **la grada aplaudía una derrota**. La reacción correcta depende del resultado, que este
    // nivel no conoce —lo conoce la pantalla, que ya enseña el acta—, así que se elige el silencio antes
    // que una emoción equivocada. Anotado en `docs/pendientes/BI-A.md`.
    private static readonly string[] FullTime = { "referee/whistle" };

    /// <summary>
    /// Los pools que suenan en ese momento, en orden. Lista vacía si el momento no suena.
    /// </summary>
    /// <param name="kind">El momento.</param>
    /// <param name="team">De quién es el momento: 0 tu equipo, 1 el rival. Solo lo mira el gol.</param>
    public static string[] PoolsFor(MomentKind kind, int team) => kind switch
    {
        MomentKind.Kickoff => Whistle,
        MomentKind.Foul => Whistle,

        // La tarjeta se abuchea sea de quien sea: lo que la grada abuchea es al árbitro, no al castigado.
        MomentKind.Yellow => Boo,
        MomentKind.Red => Boo,

        MomentKind.Goal => team == 0 ? GoalFor : GoalAgainst,
        MomentKind.MinorInjury => Pain,
        MomentKind.SevereInjury => Pain,
        MomentKind.Death => Death,
        MomentKind.Mob => Boo,
        MomentKind.RefereeLeaves => RefereeLeaves,
        MomentKind.FullTime => FullTime,

        // Sin sonido a propósito: son decisiones de gestión, no sucesos del campo. Ponerles un efecto
        // convertiría en espectáculo algo que el jugador está leyendo.
        MomentKind.Consumable => None,
        MomentKind.Substitution => None,
        _ => None,
    };
}
