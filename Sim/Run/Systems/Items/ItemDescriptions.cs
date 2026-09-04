using System.Globalization;
using System.Text;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Genera la descripción de un objeto de equipamiento desde su dato (RT-035): el fichero no lleva ningún
/// campo <c>description</c>.
///
/// <para>Con la ADR 0036 esto es casi trivial —"una plantilla por arquetipo"—, que era justamente uno de
/// los argumentos de la decisión: un objeto es una lista de atributos, así que su texto es la lista y su
/// contrapartida. La probabilidad de rotura del frágil <b>siempre</b> aparece, porque RF-012d exige que
/// nada de lo que pase estuviera sin anunciar.</para>
/// </summary>
public static class ItemDescriptions
{
    /// <summary>Descripción completa del objeto en el idioma pedido ("es" o "en").</summary>
    public static string Describe(ItemDefinition item, string language)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool es = string.Equals(language, "es", StringComparison.Ordinal);

        var parts = new List<string>();
        foreach (var kind in item.Raised)
        {
            parts.Add(Modifier(item.Modifier.Get(kind), kind, es));
        }

        var builder = new StringBuilder();
        builder.Append(Join(parts, es));

        var lowered = item.Lowered;
        if (lowered.Count > 0)
        {
            var drawbacks = new List<string>(lowered.Count);
            foreach (var kind in lowered)
            {
                drawbacks.Add(Modifier(item.Modifier.Get(kind), kind, es));
            }

            builder.Append(es ? "; a cambio, " : "; in exchange, ");
            builder.Append(Join(drawbacks, es));
        }

        builder.Append('.');

        if (item.Archetype == ItemArchetype.Fragile)
        {
            string chance = item.BreakChancePercent.ToString(CultureInfo.InvariantCulture);
            builder.Append(es
                ? $" Frágil: {chance}% de romperse al terminar cada partido."
                : $" Fragile: {chance}% chance of breaking at the end of each match.");
        }

        if (item.Archetype == ItemArchetype.Restricted)
        {
            string race = item.RequiredTag;
            builder.Append(es
                ? $" Exclusivo de {race}: no aporta nada a un portador de otra raza."
                : $" {race} only: it does nothing on a wearer of another race.");
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

    private static string Modifier(int value, AttributeKind kind, bool es)
    {
        string sign = value >= 0 ? "+" : string.Empty;
        string attribute = AttributeName(kind, es);
        string text = value.ToString(CultureInfo.InvariantCulture);
        return es ? $"{sign}{text} de {attribute}" : $"{sign}{text} {attribute}";
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

    private static string CapitalizeFirst(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
