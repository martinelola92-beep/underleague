using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Lee una lista de efectos con el mismo formato de <c>data/perks/*.json</c> (<see cref="EffectDefinition"/>
/// de <c>Sim.Perks</c>), recortado a lo que un objeto o un consumible pasivo necesita: <c>type</c>,
/// <c>attribute</c>, <c>probability</c> y <c>value</c>. Sin disparador, sin condición, sin alcance: el
/// objetivo es siempre el portador (<see cref="EffectTarget.Owner"/>) y la duración
/// <see cref="EffectDuration.Run"/> (dura lo que dure el objeto equipado), porque ni <c>data/items</c> ni
/// <c>data/consumables</c> participan todavía en la resolución de un partido (ver
/// <c>Sim.Run.Systems.Items.ItemDefinition</c>).
/// </summary>
internal static class EffectJson
{
    public static IReadOnlyList<EffectDefinition> ReadList(Json? node)
    {
        if (node is not { } value)
        {
            return Array.Empty<EffectDefinition>();
        }

        var effects = new List<EffectDefinition>();
        foreach (var item in value.EnumerateArray())
        {
            effects.Add(Read(item));
        }

        return effects;
    }

    public static EffectDefinition Read(Json node)
    {
        string type = node.Str("type");
        var effectType = type switch
        {
            "modifyAttribute" => EffectType.ModifyAttribute,
            "modifyProbability" => EffectType.ModifyProbability,
            _ => throw new DataException(node.File, node.Path + ".type", $"tipo de efecto no admitido en objetos/consumibles: '{type}' (solo modifyAttribute y modifyProbability)"),
        };

        int value = node.Int("value");

        if (effectType == EffectType.ModifyAttribute)
        {
            string attribute = node.Str("attribute");
            var kind = ParseAttribute(node, attribute);
            return new EffectDefinition(
                EffectType.ModifyAttribute,
                Target: EffectTarget.Owner,
                Attribute: kind,
                Value: value,
                Duration: EffectDuration.Run);
        }

        string probability = node.Str("probability");
        var probabilityKind = ParseProbability(node, probability);
        return new EffectDefinition(
            EffectType.ModifyProbability,
            Target: EffectTarget.Owner,
            Probability: probabilityKind,
            Value: value,
            Duration: EffectDuration.Run);
    }

    private static AttributeKind ParseAttribute(Json node, string attribute) => attribute switch
    {
        "strength" => AttributeKind.Strength,
        "speed" => AttributeKind.Speed,
        "technique" => AttributeKind.Technique,
        "stamina" => AttributeKind.Stamina,
        "leash" => AttributeKind.Leash,
        _ => throw new DataException(node.File, node.Path + ".attribute", $"atributo desconocido: '{attribute}'"),
    };

    private static ProbabilityKind ParseProbability(Json node, string probability) => probability switch
    {
        "foul" => ProbabilityKind.Foul,
        "card" => ProbabilityKind.Card,
        "injury" => ProbabilityKind.Injury,
        "injure" => ProbabilityKind.Injure,
        "severeInjury" => ProbabilityKind.SevereInjury,
        "pass" => ProbabilityKind.Pass,
        "intercept" => ProbabilityKind.Intercept,
        "dribble" => ProbabilityKind.Dribble,
        "tackle" => ProbabilityKind.Tackle,
        "shotOnTarget" => ProbabilityKind.ShotOnTarget,
        "save" => ProbabilityKind.Save,
        "tackleEvasion" => ProbabilityKind.TackleEvasion,
        "interceptEvasion" => ProbabilityKind.InterceptEvasion,
        _ => throw new DataException(node.File, node.Path + ".probability", $"probabilidad desconocida: '{probability}'"),
    };
}
