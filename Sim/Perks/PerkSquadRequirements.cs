using NCalc;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>
/// Requisitos contables de un perk frente a una <b>plantilla</b>, no a una colocación (RF-012d, BB-J).
///
/// <para>Es la pregunta del Mercado, donde el perk todavía no tiene portador: <i>"si compro esto, ¿me
/// sirve con el equipo que tengo?"</i>. <see cref="LineupPerkPreviewer"/> no vale ahí — necesita
/// titulares colocados para poder decidir si el perk se enciende.</para>
///
/// <para><b>Qué NO promete.</b> No dice si el perk se activará: eso depende de quién lo lleve y de dónde
/// se coloque, y en el Mercado ninguna de las dos cosas está decidida. Dice solo cuántos jugadores de la
/// plantilla llevan la etiqueta que el perk cuenta. Por eso, a diferencia del previsualizador, aquí no se
/// evalúa la condición ni se devuelve estado: solo se recogen los conteos que aparecen en ella, y las
/// partes que dependen de la colocación o del partido se ignoran en silencio.</para>
///
/// <para>El conteo es sobre la plantilla ENTERA, sin excluir a nadie: en el Mercado no se sabe quién será
/// el portador. En partido, <c>teammatesWithTag</c> excluye al portador y cuenta solo titulares, así que
/// este número es una cota superior — informativa, nunca una promesa de activación.</para>
/// </summary>
public static class PerkSquadRequirements
{
    /// <summary>
    /// Conteos de etiqueta que pide la condición de <paramref name="perk"/>, medidos sobre
    /// <paramref name="squad"/>. Lista vacía si la condición no cuenta ninguna etiqueta.
    /// </summary>
    public static IReadOnlyList<LineupPerkRequirement> For(PerkDefinition perk, IReadOnlyList<PlayerDefinition> squad)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(squad);

        var found = new List<LineupPerkRequirement>();
        if (perk.CompiledCondition is { IsAlwaysTrue: false, Ast: { } ast })
        {
            Collect(ast, squad, found, negated: false);
        }

        return found;
    }

    /// <summary>
    /// Recorre el árbol recogiendo solo lo contable. A diferencia del previsualizador esto no es
    /// todo-o-nada: un nodo que no se entiende se salta sin descartar el resto.
    /// </summary>
    private static void Collect(
        LogicalExpression node, IReadOnlyList<PlayerDefinition> squad, List<LineupPerkRequirement> found, bool negated)
    {
        switch (node)
        {
            // `hasTag(x,'Fine')` suelto: hace falta alguien que la lleve.
            case NCalc.Function function when function.Identifier.Name == "hasTag":
            {
                if (!negated && TagArgument(function) is { } tag)
                {
                    found.Add(new LineupPerkRequirement("hasTag", tag, Count(squad, tag), 1));
                }

                break;
            }

            case UnaryExpression unary when unary.Type == UnaryExpressionType.Not:
                // Dentro de una negación, "necesitas N" significaría lo contrario: se recorre por si hay
                // algo más abajo, pero no se recoge nada de dentro.
                Collect(unary.Expression, squad, found, negated: true);
                break;

            case BinaryExpression binary when binary.Type is BinaryExpressionType.And or BinaryExpressionType.Or:
                Collect(binary.LeftExpression, squad, found, negated);
                Collect(binary.RightExpression, squad, found, negated);
                break;

            // `teammatesWithTag(owner,'Fine') > 1` y `adjacentCount(owner,'Brute') >= 2`.
            case BinaryExpression binary when ConditionCompiler.IsComparison(binary.Type)
                && binary.LeftExpression is NCalc.Function counter
                && counter.Identifier.Name is "teammatesWithTag" or "adjacentCount":
            {
                if (negated
                    || ConditionCompiler.TryReadInt(binary.RightExpression) is not { } literal
                    || TagArgument(counter) is not { } tag
                    || RequiredCount(binary.Type, literal) is not { } required)
                {
                    break;
                }

                found.Add(new LineupPerkRequirement(counter.Identifier.Name, tag, Count(squad, tag), required));
                break;
            }

            default:
                break;
        }
    }

    /// <summary>Igual que en el previsualizador: <c>&gt; 1</c> son dos, <c>&gt;= 2</c> son dos, el resto no se traduce.</summary>
    private static int? RequiredCount(BinaryExpressionType comparison, int literal) => comparison switch
    {
        BinaryExpressionType.Greater => literal + 1,
        BinaryExpressionType.GreaterOrEqual => literal,
        _ => null,
    };

    private static int Count(IReadOnlyList<PlayerDefinition> squad, string tag)
    {
        int total = 0;
        for (int i = 0; i < squad.Count; i++)
        {
            if (squad[i].HasTag(tag))
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>Segundo argumento de la función, si es un literal de texto (la etiqueta).</summary>
    private static string? TagArgument(NCalc.Function function) =>
        function.Parameters.Count > 1 && function.Parameters[1] is ValueExpression { Value: string text } ? text : null;
}
