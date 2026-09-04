using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems.Consumables;

/// <summary>
/// Traduce el disparador de un consumible equipado (<see cref="EquippedConsumable.Trigger"/>, una cadena
/// del estado de la run) al <see cref="ConsumableTrigger"/> que entiende el motor, con su umbral.
///
/// <para><b>Formato</b>: <c>nombre</c> o <c>nombre:umbral</c>, donde el umbral es un entero. Los ocho
/// disparadores mínimos de RF-083:</para>
/// <list type="table">
/// <item><term><c>scoreBehind</c></term><description>marcador por debajo</description></item>
/// <item><term><c>scoreTied</c></term><description>marcador empatado (con al menos un gol en el partido)</description></item>
/// <item><term><c>lastSeconds</c> / <c>lastSeconds:N</c></term><description>últimos 20 segundos, o los últimos N</description></item>
/// <item><term><c>mobStart</c></term><description>entrada en la turba</description></item>
/// <item><term><c>ownInjury</c></term><description>lesión propia</description></item>
/// <item><term><c>ownRedCard</c></term><description>tarjeta roja propia</description></item>
/// <item><term><c>goalsConceded:N</c></term><description>N goles encajados</description></item>
/// <item><term><c>refereeBiasBelow:N</c></term><description>criterio del árbitro por debajo de N</description></item>
/// </list>
///
/// <para>El slot manual (RF-082) no lleva disparador: su cadena es vacía o <c>manual</c>, y lo que lo
/// resuelve es el tick de activación que el jugador deja en el estado inicial del partido
/// (<c>docs/arquitectura.md</c>).</para>
/// </summary>
public static class ConsumableTriggers
{
    /// <summary>Segundos del tramo final si el disparador no dice otra cosa (RF-083).</summary>
    public const int DefaultLastSeconds = 20;

    /// <summary>
    /// Parsea la cadena del estado. Lanza <see cref="ArgumentException"/> con el texto ofensivo si el
    /// disparador no existe o si le falta el umbral: un disparador mal escrito es un error explícito,
    /// nunca un consumible que no se dispara nunca en silencio (RT-032, mismo criterio que <c>/data</c>).
    /// </summary>
    public static (ConsumableTrigger Trigger, int Threshold) Parse(string trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        string name = trigger;
        int threshold = 0;
        bool hasThreshold = false;
        int colon = trigger.IndexOf(':');
        if (colon >= 0)
        {
            name = trigger[..colon];
            string value = trigger[(colon + 1)..];
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out threshold))
            {
                throw new ArgumentException(
                    $"el umbral del disparador '{trigger}' no es un entero (RF-083)", nameof(trigger));
            }

            hasThreshold = true;
        }

        return name switch
        {
            "" or "manual" => (ConsumableTrigger.Manual, 0),
            "scoreBehind" => (ConsumableTrigger.ScoreBehind, 0),
            "scoreTied" => (ConsumableTrigger.ScoreTied, 0),
            "lastSeconds" => (ConsumableTrigger.LastSeconds, hasThreshold ? Positive(trigger, threshold) : DefaultLastSeconds),
            "mobStart" => (ConsumableTrigger.MobStart, 0),
            "ownInjury" => (ConsumableTrigger.OwnInjury, 0),
            "ownRedCard" => (ConsumableTrigger.OwnRedCard, 0),
            "goalsConceded" => hasThreshold
                ? (ConsumableTrigger.GoalsConceded, Positive(trigger, threshold))
                : throw new ArgumentException($"el disparador '{trigger}' necesita cuántos goles encajados (RF-083)", nameof(trigger)),
            "refereeBiasBelow" => hasThreshold
                ? (ConsumableTrigger.RefereeBiasBelow, threshold)
                : throw new ArgumentException($"el disparador '{trigger}' necesita el umbral de criterio (RF-083)", nameof(trigger)),
            _ => throw new ArgumentException(
                $"disparador de consumible desconocido: '{trigger}' (RF-083)", nameof(trigger)),
        };
    }

    private static int Positive(string trigger, int threshold) => threshold > 0
        ? threshold
        : throw new ArgumentException($"el umbral del disparador '{trigger}' debe ser positivo (RF-083)", nameof(trigger));
}

/// <summary>
/// Activación de un consumible manual (RF-082), tal y como la modela <c>docs/arquitectura.md</c>: no es
/// una llamada en mitad del partido, es un dato del <b>estado inicial</b>. Al pulsar el consumible en el
/// tick T, <c>/Game</c> vuelve a ejecutar el partido con esta activación dentro, de modo que la
/// repetición y el guardado ironman reproducen exactamente lo mismo (RT-013, RT-061).
/// </summary>
public sealed record ManualActivation(string ConsumableId, int Tick);
