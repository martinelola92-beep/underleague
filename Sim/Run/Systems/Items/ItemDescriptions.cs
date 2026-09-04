using System.Globalization;
using System.Text;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Genera la descripción de un objeto de equipamiento desde su efecto (RT-035): el dato no lleva ningún
/// campo <c>description</c>. <c>Sim.Perks.DescriptionGenerator</c> hace exactamente esto para los perks,
/// pero lee las plantillas de <c>data/l10n/&lt;lang&gt;/templates.json</c>, que es territorio de otro
/// agente en paralelo (fase2-diseno.md, encargo del paquete X: "no toques ... data/l10n"). Por eso este
/// generador usa sus propias plantillas, en código, con la misma idea: el texto sale del mismo dato que
/// describe, nunca al revés.
/// </summary>
public static class ItemDescriptions
{
    /// <summary>Descripción completa del objeto en el idioma pedido ("es" o "en").</summary>
    public static string Describe(ItemDefinition item, string language)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool es = string.Equals(language, "es", StringComparison.Ordinal);

        var parts = new List<string>();
        foreach (var effect in item.Effects)
        {
            parts.Add(DescribeEffect(effect, es, positive: true));
        }

        var builder = new StringBuilder();
        builder.Append(Join(parts, es));

        if (item.Archetype == ItemArchetype.Cursed)
        {
            var drawbacks = new List<string>();
            foreach (var effect in item.DrawbackEffects)
            {
                drawbacks.Add(DescribeEffect(effect, es, positive: false));
            }

            builder.Append(es ? "; a cambio, " : "; in exchange, ");
            builder.Append(Join(drawbacks, es));
            builder.Append('.');
        }
        else
        {
            builder.Append('.');
        }

        if (item.Archetype == ItemArchetype.Fragile)
        {
            builder.Append(' ');
            builder.Append(es
                ? $"Frágil: se rompe tras {item.UsesLimit.ToString(CultureInfo.InvariantCulture)} partidos jugados con él, o si el portador se lesiona."
                : $"Fragile: breaks after {item.UsesLimit.ToString(CultureInfo.InvariantCulture)} matches played with it, or if the wearer is injured.");
        }

        if (item.Archetype == ItemArchetype.Restricted)
        {
            builder.Append(' ');
            builder.Append(es
                ? $"Restringido: solo tiene efecto en portadores con la etiqueta {item.RequiredTag}."
                : $"Restricted: only works on wearers with the {item.RequiredTag} tag.");
        }

        return CapitalizeFirst(builder.ToString());
    }

    private static string Join(IReadOnlyList<string> parts, bool es)
    {
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var builder = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(i == parts.Count - 1 ? (es ? " y " : " and ") : ", ");
            }

            builder.Append(parts[i]);
        }

        return builder.ToString();
    }

    private static string DescribeEffect(EffectDefinition effect, bool es, bool positive)
    {
        string sign = effect.Value >= 0 ? "+" : string.Empty;
        int abs = effect.Value;

        if (effect.Type == EffectType.ModifyAttribute)
        {
            string attribute = AttributeName(effect.Attribute, es);
            return es
                ? $"{sign}{abs} de {attribute}"
                : $"{sign}{abs} {attribute}";
        }

        // modifyProbability: valor en base 10.000 (RT-023), se muestra como puntos porcentuales.
        int percent = abs / 100;
        string probability = ProbabilityName(effect.Probability, es);
        return es
            ? $"{sign}{percent}% de probabilidad de {probability}"
            : $"{sign}{percent}% {probability} probability";
    }

    private static string AttributeName(AttributeKind kind, bool es) => (kind, es) switch
    {
        (AttributeKind.Strength, true) => "fuerza",
        (AttributeKind.Strength, false) => "strength",
        (AttributeKind.Speed, true) => "velocidad",
        (AttributeKind.Speed, false) => "speed",
        (AttributeKind.Technique, true) => "técnica",
        (AttributeKind.Technique, false) => "technique",
        (AttributeKind.Stamina, true) => "resistencia",
        (AttributeKind.Stamina, false) => "stamina",
        (AttributeKind.Leash, true) => "correa",
        (AttributeKind.Leash, false) => "leash",
        _ => kind.ToString(),
    };

    private static string ProbabilityName(ProbabilityKind kind, bool es) => (kind, es) switch
    {
        (ProbabilityKind.Injure, true) => "lesión",
        (ProbabilityKind.Injure, false) => "injury",
        (ProbabilityKind.SevereInjury, true) => "lesión grave",
        (ProbabilityKind.SevereInjury, false) => "severe injury",
        (ProbabilityKind.Injury, true) => "lesión",
        (ProbabilityKind.Injury, false) => "injury",
        (ProbabilityKind.Foul, true) => "falta",
        (ProbabilityKind.Foul, false) => "foul",
        (ProbabilityKind.Card, true) => "tarjeta",
        (ProbabilityKind.Card, false) => "card",
        _ => kind.ToString(),
    };

    private static string CapitalizeFirst(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
