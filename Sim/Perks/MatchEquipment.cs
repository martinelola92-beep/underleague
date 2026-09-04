using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// De dónde sale un efecto activo del partido (RT-043). El informe post-partido los lleva en listas
/// separadas para que <c>/Balance</c> pueda medir por separado qué aporta un perk y qué un objeto.
/// </summary>
public enum EffectSource
{
    /// <summary>Perk asignado al jugador (RF-071) o habilidad racial (ADR 0026).</summary>
    Perk,

    /// <summary>Objeto equipado por el jugador (RF-075..078). No ocupa slot de perk (RF-076).</summary>
    Item,

    /// <summary>Consumible equipado antes del partido (RF-080..085).</summary>
    Consumable,
}

/// <summary>
/// Un objeto de equipamiento tal y como entra en el partido (RF-075..078). Es la forma "de partido" de
/// <c>Sim.Run.Systems.Items.ItemDefinition</c>: solo lo que el motor necesita, sin nada de economía
/// (precio, fracción de venta) ni de plantilla (usos gastados). La conversión la hace
/// <c>Sim.Run.Systems.Items.RunEquipment</c>, que es quien conoce el catálogo.
///
/// <para><b>Arquetipos (RF-077)</b>, todos expresados con estos tres campos y sin código por arquetipo:
/// <list type="bullet">
/// <item><b>Maldito</b>: <see cref="DrawbackEffects"/> no vacío. La contrapartida se aplica siempre, a la
/// vez que <see cref="Effects"/> y sin condición que la evite: es permanente mientras el objeto esté
/// puesto.</item>
/// <item><b>Frágil</b>: dentro del partido no se distingue de uno normal; lo que lo define es que se
/// gasta o se rompe <b>entre</b> partidos, y de eso se ocupa
/// <c>Sim.Run.Systems.Equipment.EquipmentSystem.ProcessFragileItems</c> sobre el <c>RunState</c>.</item>
/// <item><b>Restringido</b>: <see cref="RequiredTag"/> no vacía. Si el portador no lleva esa etiqueta, el
/// objeto <b>no aporta nada</b> (ni siquiera su contrapartida) y así se registra en el informe.</item>
/// </list></para>
///
/// <para>Los efectos son los mismos <see cref="EffectDefinition"/> que los perks (RF-078), recortados a
/// los pasivos con objetivo el portador: <c>modifyAttribute</c>, <c>modifyProbability</c>,
/// <c>modifyLeash</c>, <c>modifyKnockdownTicks</c> e <c>immunity</c>. Un objeto no tiene disparador ni
/// condición: está activo desde el saque inicial hasta el final del partido.</para>
/// </summary>
public sealed record MatchItem(string Id, Rarity Rarity, IReadOnlyList<EffectDefinition> Effects)
{
    /// <summary>Contrapartida permanente de un objeto maldito (RF-077); vacía en los demás arquetipos.</summary>
    public IReadOnlyList<EffectDefinition> DrawbackEffects { get; init; } = Array.Empty<EffectDefinition>();

    /// <summary>Etiqueta que el portador debe llevar para que el objeto funcione (RF-077, restringido); vacía si no aplica.</summary>
    public string RequiredTag { get; init; } = string.Empty;

    /// <summary>True si el objeto surte efecto sobre este portador (RF-077, restringido).</summary>
    public bool AppliesTo(PlayerDefinition bearer)
    {
        ArgumentNullException.ThrowIfNull(bearer);
        return RequiredTag.Length == 0 || bearer.HasTag(RequiredTag);
    }
}

/// <summary>
/// Disparador de un consumible equipado (RF-081..083). <see cref="Manual"/> es el slot que el jugador
/// pulsa (RF-082) y se modela como parte del estado inicial
/// (<c>docs/arquitectura.md</c>, "Consumibles manuales durante el partido"):
/// <c>MatchConsumable.ManualTick</c> lleva el tick en el que se pulsó, así que volver a ejecutar el
/// partido con la activación dentro reproduce exactamente lo mismo (RT-013, RT-061).
/// </summary>
public enum ConsumableTrigger
{
    /// <summary>Lo activa el jugador (RF-082): se resuelve en <c>ManualTick</c>, y nunca si vale -1.</summary>
    Manual,

    /// <summary>Marcador por debajo.</summary>
    ScoreBehind,

    /// <summary>Marcador empatado.</summary>
    ScoreTied,

    /// <summary>Últimos <c>Threshold</c> segundos del tiempo reglamentario (20 por defecto).</summary>
    LastSeconds,

    /// <summary>Entrada en la turba (RF-055b).</summary>
    MobStart,

    /// <summary>Lesión propia: cualquier jugador del equipo lesionado en este partido.</summary>
    OwnInjury,

    /// <summary>Tarjeta roja propia: cualquier jugador del equipo expulsado.</summary>
    OwnRedCard,

    /// <summary><c>Threshold</c> goles encajados o más.</summary>
    GoalsConceded,

    /// <summary>Criterio del árbitro a favor del equipo por debajo de <c>Threshold</c> (RF-062).</summary>
    RefereeBiasBelow,
}

/// <summary>
/// Un consumible equipado para el partido (RF-080..085). Como el objeto, reutiliza el formato de efecto
/// de los perks; a diferencia del objeto, <b>no tiene portador</b>: lo usa el entrenador y sus efectos
/// alcanzan a todo su equipo sobre el campo en el instante en que se usa, hasta el final del partido.
///
/// <para>Se consume al usarse y no persiste entre partidos (RF-085): dentro del partido eso significa
/// que se resuelve <b>una sola vez</b>; fuera, que <c>Sim.Run.MatchResolution</c> lo retira de los
/// equipados al terminar.</para>
/// </summary>
/// <param name="Trigger">Disparador (RF-081..083); <see cref="ConsumableTrigger.Manual"/> para el slot manual (RF-082).</param>
public sealed record MatchConsumable(
    string Id,
    Rarity Rarity,
    IReadOnlyList<EffectDefinition> Effects,
    ConsumableTrigger Trigger)
{
    /// <summary>Segundos del tramo final, goles encajados o umbral de criterio, según el disparador.</summary>
    public int Threshold { get; init; }

    /// <summary>
    /// Tick en el que el jugador pulsó el consumible manual (RF-082); -1 si no lo pulsó. Solo lo lee
    /// <see cref="ConsumableTrigger.Manual"/>. En <c>/Balance</c> no hay quien lo pulse, así que vale
    /// siempre -1 y el canal queda abierto sin alterar ninguna medición.
    /// </summary>
    public int ManualTick { get; init; } = -1;
}
