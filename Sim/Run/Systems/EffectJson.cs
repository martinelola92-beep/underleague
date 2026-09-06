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
    public static IReadOnlyList<EffectDefinition> ReadList(Json? node, Rarity rarity)
    {
        if (node is not { } value)
        {
            return Array.Empty<EffectDefinition>();
        }

        var effects = new List<EffectDefinition>();
        foreach (var item in value.EnumerateArray())
        {
            effects.Add(Read(item, rarity));
        }

        return effects;
    }

    public static EffectDefinition Read(Json node, Rarity rarity)
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

        // ADR 0050 P1: igual que en data/perks, el valor es un multiplicador de CUOTA escrito como
        // porcentaje con signo, no puntos base 10.000. La escala es la misma para objetos, consumibles y
        // perks: un efecto no vale distinto por venir de una tienda. Y desde la ADR 0058 el TECHO también
        // es el mismo, es decir el de su rareza: un consumible común no puede mover la cuota más que un
        // perk común.
        int ceiling = Perks.ProbabilityScale.CeilingFor(rarity);
        if (!Perks.ProbabilityScale.IsLegalUpTo(value, ceiling))
        {
            throw new DataException(
                node.File,
                node.Path + ".value",
                Perks.ProbabilityScale.IsLegal(value)
                    ? $"un consumible {rarity.ToString().ToLowerInvariant()} no puede llevar '{value}': el "
                        + $"techo de esa rareza es {ceiling} (ADR 0058) y los valores a su alcance son "
                        + $"{Perks.ProbabilityScale.AllowedUpTo(ceiling)}"
                    : $"'{value}' no es un valor legal de modifyProbability: multiplica la CUOTA del canal y la "
                        + $"escala es {Perks.ProbabilityScale.Allowed} (ADR 0050 P1), con el techo de su "
                        + $"rareza ({rarity.ToString().ToLowerInvariant()}: {ceiling}, ADR 0058)");
        }

        return new EffectDefinition(
            EffectType.ModifyProbability,
            Target: EffectTarget.Owner,
            Probability: probabilityKind,
            Value: Perks.ProbabilityScale.ToMultiplier(value),
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
