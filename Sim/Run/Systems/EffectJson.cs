using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Lee una lista de efectos con el mismo formato de <c>data/perks/*.json</c> (<see cref="EffectDefinition"/>
/// de <c>Sim.Perks</c>), recortado a lo que un objeto o un consumible pasivo necesita: <c>type</c>,
/// <c>attribute</c>, <c>probability</c> y <c>value</c>. Sin disparador, sin condición, sin alcance: el
/// objetivo es el equipo propio (<see cref="EffectTarget.Team"/>) o, con <c>target: opposingTeam</c>, el rival.
///
/// <para><b>Duración</b>: <see cref="EffectDuration.Match"/>, no <see cref="EffectDuration.Run"/> (BA-H,
/// corrección de texto: <c>ApplyPassiveEffect</c> ignora este campo del todo —solo lo lee la plantilla de
/// descripción, RT-035— y un objeto o un consumible pasivo actúa desde que se activa hasta el final del
/// PARTIDO, nunca de la run entera; el valor viejo hacía que un consumible de un solo uso dijera «durante
/// la run» en su propia descripción, que es justo lo contrario de RF-085 «se consumen al usarse»).</para>
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
        if (type == "shout")
        {
            return ReadShout(node);
        }

        var effectType = type switch
        {
            "modifyAttribute" => EffectType.ModifyAttribute,
            "modifyProbability" => EffectType.ModifyProbability,
            _ => throw new DataException(node.File, node.Path + ".type", $"tipo de efecto no admitido en objetos/consumibles: '{type}' (solo modifyAttribute, modifyProbability y, en consumibles, shout)"),
        };

        int value = node.Int("value");

        // Un consumible no tiene portador: por defecto alcanza a su equipo entero, y `opposingTeam` lo dirige
        // al rival (el mismo nombre que un perk como marrow_thirst). Sin esto, `severeInjury` —canal de quien
        // SUFRE la lesión— sólo podía subirse a los propios.
        var target = node.OptionalStr("target", "team") switch
        {
            "team" => EffectTarget.Team,
            "opposingTeam" => EffectTarget.OpposingTeam,
            var other => throw new DataException(node.File, node.Path + ".target", $"objetivo no admitido en un consumible: '{other}' (solo team y opposingTeam)"),
        };

        if (effectType == EffectType.ModifyAttribute)
        {
            string attribute = node.Str("attribute");
            var kind = ParseAttribute(node, attribute);
            return new EffectDefinition(
                EffectType.ModifyAttribute,
                Target: target,
                Attribute: kind,
                Value: value,
                Duration: EffectDuration.Match);
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
            Target: target,
            Probability: probabilityKind,
            Value: Perks.ProbabilityScale.ToMultiplier(value),
            Duration: EffectDuration.Match);
    }

    /// <summary>Techo de un grito, en segundos de juego: un partido dura 60-90 s, así que más de 30 sería jugar con la orden gritada casi todo el partido (ADR 0166, provisional).</summary>
    public const int MaxShoutSeconds = 30;

    /// <summary>
    /// ADR 0166: <c>{ "type": "shout", "order": "Defensive"|"Offensive", "seconds": N }</c> o
    /// <c>{ "type": "shout", "press": true, "seconds": N }</c>. Exactamente uno de <c>order</c> y <c>press</c>;
    /// la duración va en <see cref="EffectDefinition.Value"/>. Un grito no tiene objetivo: es del equipo que
    /// lo usa.
    /// </summary>
    private static EffectDefinition ReadShout(Json node)
    {
        bool press = node.OptionalBool("press", false);
        bool hasOrder = node.TryProp("order") is not null;
        if (press == hasOrder)
        {
            throw new DataException(node.File, node.Path, "un grito lleva exactamente uno de 'order' (Defensive u Offensive) y 'press': true");
        }

        var kind = ShoutKind.Press;
        if (hasOrder)
        {
            string order = node.Str("order");
            kind = order switch
            {
                "Defensive" => ShoutKind.Defensive,
                "Offensive" => ShoutKind.Offensive,
                _ => throw new DataException(node.File, node.Path + ".order", $"orden de un grito no admitida: '{order}' (solo Defensive y Offensive; Neutral no es un grito)"),
            };
        }

        int seconds = node.Int("seconds");
        if (seconds < 1 || seconds > MaxShoutSeconds)
        {
            throw new DataException(node.File, node.Path + ".seconds", $"la duración de un grito son 1..{MaxShoutSeconds} segundos, no {seconds}");
        }

        return new EffectDefinition(
            EffectType.Shout,
            Target: EffectTarget.Team,
            Value: seconds,
            Duration: EffectDuration.Match,
            Shout: kind);
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
