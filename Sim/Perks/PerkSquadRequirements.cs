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
/// <para><b>Con portador o sin él.</b> Si se pasa <c>ownerId</c> —la ficha de un jugador concreto— la
/// lectura es la misma que hace el motor: <c>hasTag</c> mira a ESE jugador (0 o 1 de 1) y
/// <c>teammatesWithTag</c> cuenta a los demás, excluyéndolo. Sin portador —el Mercado, donde el perk
/// todavía no es de nadie— <c>teammatesWithTag</c> cuenta la plantilla entera, que es una cota superior, y
/// <c>hasTag</c> <b>no se reporta</b>: "necesita a alguien con la etiqueta X" no es un conteo, y decir
/// "8 de 1" no significaría nada. De eso ya informa el Mercado con sus portadores elegibles.</para>

/// <para>En partido <c>teammatesWithTag</c> cuenta solo TITULARES; aquí se cuenta la plantilla. Con una
/// plantilla mayor que siete, el número puede ser mayor que el que verá el motor: informativo, nunca una
/// promesa de activación.</para>
/// </summary>
public static class PerkSquadRequirements
{
    /// <summary>
    /// Conteos de etiqueta que pide la condición de <paramref name="perk"/>, medidos sobre
    /// <paramref name="squad"/>. Lista vacía si la condición no cuenta ninguna etiqueta.
    /// </summary>
    /// <param name="ownerId">
    /// Jugador que lleva (o llevaría) el perk, si se sabe. Con él la lectura coincide con la del motor;
    /// sin él se cuenta la plantilla entera y los <c>hasTag</c> se omiten.
    /// </param>
    public static IReadOnlyList<LineupPerkRequirement> For(
        PerkDefinition perk, IReadOnlyList<PlayerDefinition> squad, int? ownerId = null)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(squad);

        var found = new List<LineupPerkRequirement>();
        if (perk.CompiledCondition is { IsAlwaysTrue: false, Ast: { } ast })
        {
            Collect(ast, squad, ownerId, found, negated: false);
        }

        return found;
    }

    /// <summary>
    /// Recorre el árbol recogiendo solo lo contable. A diferencia del previsualizador esto no es
    /// todo-o-nada: un nodo que no se entiende se salta sin descartar el resto.
    /// </summary>
    private static void Collect(
        LogicalExpression node, IReadOnlyList<PlayerDefinition> squad, int? ownerId,
        List<LineupPerkRequirement> found, bool negated)
    {
        switch (node)
        {
            // `hasTag(owner,'Fine')`: solo tiene lectura si se sabe QUIÉN lo lleva. Sin portador, un
            // "tienes 8 de 1" no diría nada, así que se omite.
            case NCalc.Function function when function.Identifier.Name == "hasTag":
            {
                if (!negated && ownerId is { } owner && TagArgument(function) is { } tag)
                {
                    found.Add(new LineupPerkRequirement("hasTag", tag, HasTag(squad, owner, tag) ? 1 : 0, 1));
                }

                break;
            }

            case UnaryExpression unary when unary.Type == UnaryExpressionType.Not:
                // Dentro de una negación, "necesitas N" significaría lo contrario: se recorre por si hay
                // algo más abajo, pero no se recoge nada de dentro.
                Collect(unary.Expression, squad, ownerId, found, negated: true);
                break;

            case BinaryExpression binary when binary.Type is BinaryExpressionType.And or BinaryExpressionType.Or:
                Collect(binary.LeftExpression, squad, ownerId, found, negated);
                Collect(binary.RightExpression, squad, ownerId, found, negated);
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

                found.Add(new LineupPerkRequirement(counter.Identifier.Name, tag, Count(squad, ownerId, tag), required));
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

    /// <summary>Cuántos llevan la etiqueta, excluyendo al portador si se sabe quién es (como el motor).</summary>
    private static int Count(IReadOnlyList<PlayerDefinition> squad, int? ownerId, string tag)
    {
        int total = 0;
        for (int i = 0; i < squad.Count; i++)
        {
            if (squad[i].Id != ownerId && squad[i].HasTag(tag))
            {
                total++;
            }
        }

        return total;
    }

    private static bool HasTag(IReadOnlyList<PlayerDefinition> squad, int ownerId, string tag)
    {
        for (int i = 0; i < squad.Count; i++)
        {
            if (squad[i].Id == ownerId)
            {
                return squad[i].HasTag(tag);
            }
        }

        return false;
    }

    /// <summary>Segundo argumento de la función, si es un literal de texto (la etiqueta).</summary>
    private static string? TagArgument(NCalc.Function function) =>
        function.Parameters.Count > 1 && function.Parameters[1] is ValueExpression { Value: string text } ? text : null;
}
