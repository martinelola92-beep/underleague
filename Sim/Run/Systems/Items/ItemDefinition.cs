using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Arquetipo de un objeto de equipamiento (RF-077). El catálogo de 12 objetos de esta fase incluye los
/// tres obligatorios además de objetos normales.
/// </summary>
public enum ItemArchetype
{
    /// <summary>Sin contrapartida ni condición: un modificador de atributo llano.</summary>
    Normal,

    /// <summary>Maldito: efecto potente con una contrapartida permanente mientras esté equipado (RF-077).</summary>
    Cursed,

    /// <summary>Frágil: se destruye tras <see cref="ItemDefinition.UsesLimit"/> partidos jugados con él equipado, o si el portador se lesiona (RF-077).</summary>
    Fragile,

    /// <summary>Restringido: solo tiene efecto sobre portadores con <see cref="ItemDefinition.RequiredTag"/> (RF-077).</summary>
    Restricted,
}

/// <summary>
/// Un objeto de equipamiento (RF-075..078), cargado de <c>data/items/&lt;id&gt;.json</c>. Reutiliza el
/// formato de efecto de los perks (<see cref="EffectDefinition"/>, mismo tipo de <c>Sim.Perks</c>: RF-078
/// pide "el mismo formato de efectos que los perks") recortado a lo que un objeto pasivo necesita: sin
/// disparador, sin condición NCalc, sin alcance. La descripción no es un campo del dato: se genera desde
/// el efecto (RT-035, <see cref="ItemDescriptions"/>).
///
/// <para>El objeto llega al partido dentro del <c>PlayerDefinition</c> de su portador, ya convertido a
/// <see cref="MatchItem"/> por <see cref="RunEquipment.ToMatchItem"/>, y sus efectos los aplica el mismo
/// motor de efectos que los de un perk (<c>EffectEngine.ApplyEquippedItems</c>). El camino es el mismo
/// desde el bucle de run y desde <c>/Balance</c>: una build de <c>data/balance/builds/</c> declara su
/// equipamiento en el campo <c>items</c> y lo resuelve con esta misma conversión, de modo que la curva de
/// puertas de la ADR 0033 mide el objeto que el jugador equipa y no una copia paralela
/// (<c>fase2-diseno.md</c> §16).</para>
/// </summary>
public sealed record ItemDefinition(
    string Id,
    LocalizedName Name,
    Rarity Rarity,
    ItemArchetype Archetype,
    IReadOnlyList<EffectDefinition> Effects,
    IReadOnlyList<EffectDefinition> DrawbackEffects,
    int UsesLimit,
    string RequiredTag);
