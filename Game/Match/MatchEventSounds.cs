using System;
using Underleague.Sim.Events;

namespace Underleague.Game.Match;

/// <summary>
/// Qué suena en cada <b>evento crudo</b> del partido: la <b>capa de campo</b>, la que hace que el césped
/// tenga cuerpo —el balón, el cuero, los huesos— por debajo de la capa de retransmisión
/// (<see cref="MomentSounds"/>), que es la que subraya lo que el pregón cuenta.
///
/// <para><b>Por qué dos capas y no una.</b> Un momento es una <i>unidad narrativa</i> agrupada por el
/// director (ADR 0119): llega cuando se presenta, no cuando ocurre. Un pase, una entrada o un tiro no son
/// momentos —nadie los cuenta— y sin embargo son casi todo lo que pasa en un partido. Si el audio colgara
/// solo de los momentos, el campo sonaría a ocho cosas por partido y estaría mudo el resto del tiempo.</para>
///
/// <para><b>Las dos capas se apoyan, no se pisan.</b> Donde las dos tienen algo que decir, cada una dice
/// lo suyo y el desfase natural entre el suceso y su presentación las ordena bien: el hueso cruje en la
/// casilla (aquí) y el grito llega con el sello (allí). Por eso la lesión no repite el grito y la muerte no
/// repite el jadeo.</para>
///
/// <para><b>Vive en <c>/Game</c></b> como toda decisión de presentación: <c>/Sim</c> emite el evento y no
/// sabe —ni puede saber— si suena (RT-011/RT-014).</para>
///
/// <para><b>Pools sin ficheros todavía</b>: <c>combat/tackle</c>, <c>combat/heavy_hit</c>,
/// <c>combat/medium_hit</c>, <c>combat/light_hit</c> y <c>football/net</c> están vacíos, así que la entrada
/// se apoya de momento en el esfuerzo del jugador (<c>players/grunt</c>). Cuando haya ficheros de entrada,
/// el cambio es <b>una línea de esta tabla</b>, no un cambio de diseño.</para>
/// </summary>
public static class MatchEventSounds
{
    private static readonly string[] None = Array.Empty<string>();

    // El golpeo: tiro y centro golpean el balón igual de fuerte. Lo que los distingue es a dónde va, y eso
    // se ve, no se oye.
    private static readonly string[] Kick = { "football/kick" };

    // El cuero blando: el pase que sale y el balón que se detiene en unas manos o en un cuerpo.
    private static readonly string[] BallSoft = { "football/ball_hit" };

    // El palo: el golpe seco de la madera y el «uuuh» de la grada. Es el único evento del campo que ya
    // trae su propia reacción, porque un tiro al palo es de las pocas cosas que se cuentan solas.
    private static readonly string[] Post = { "football/ball_hit", "crowd/woah" };

    // La entrada: hoy el esfuerzo del que entra (combat/tackle está vacío, ver el resumen de la clase).
    private static readonly string[] Tackle = { "players/grunt" };

    // La falta: alguien acaba en el suelo. El silbato no se pone aquí —lo pone el momento— para que la
    // secuencia sea la de verdad: primero cae, después pitan.
    private static readonly string[] Fall = { "combat/fall" };

    // El hueso. Solo la lesión grave y la muerte: la leve no cruje, se cojea.
    private static readonly string[] Crush = { "combat/crush" };

    // El regate ganado: el grito del que se va. Es el único sonido «de gesto» de la capa, y está porque
    // salir de una presión es lo más parecido a una buena noticia que tiene un partido sin gol.
    private static readonly string[] Shout = { "players/shout" };

    /// <summary>
    /// Los pools que suenan en ese evento, en orden. Lista vacía —lo normal— si el evento no suena: de los
    /// 29 tipos de <see cref="EventType"/>, la mayoría son contabilidad (los 19 <c>PerkTriggered</c> de un
    /// partido, los arranques y cierres de jugada) y meterlos sería ruido, no presencia.
    /// </summary>
    public static string[] PoolsFor(MatchEvent e) => e.Type switch
    {
        EventType.Shot => Kick,
        EventType.Cross => Kick,
        EventType.PassAttempted => BallSoft,
        EventType.Save => BallSoft,
        EventType.ShotBlocked => BallSoft,
        EventType.ShotPost => Post,
        EventType.Tackle => Tackle,
        EventType.Foul => Fall,
        EventType.DribbleWon => Shout,

        // La grave cruje, la leve no. El detalle es el mismo texto estable que usa la pantalla para las
        // manchas de sangre (RA-027), no una cadena nueva.
        EventType.Injury => e.Detail == "severe" ? Crush : None,
        EventType.Death => Crush,

        // El gol, la tarjeta, la turba y el final los pone la capa de retransmisión: son momentos, y ahí
        // suenan una vez y a tiempo con lo que se está enseñando.
        _ => None,
    };
}
