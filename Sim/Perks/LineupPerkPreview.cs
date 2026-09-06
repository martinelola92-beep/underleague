using NCalc;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>Estado de un perk con la alineación actual: su condición se cumple o no se cumple.</summary>
public enum LineupPerkStatus
{
    /// <summary>La condición se cumple con esta colocación: el perk se activaría.</summary>
    Active,

    /// <summary>La condición no se cumple: el perk no se activaría (aplicaría sus <c>elseEffects</c>).</summary>
    Inactive,
}

/// <summary>Estado de un perk concreto de un titular concreto en la alineación previsualizada.</summary>
public sealed record LineupPerkPreview(int PlayerId, string PerkId, LineupPerkStatus Status);

/// <summary>
/// Previsualización de los perks que decide la <b>colocación</b> (RF-012d, RF-040..045): dice, antes del
/// partido y sin simular nada, qué perks de qué titulares se activan con la alineación que hay sobre la
/// mesa. Es lo que permite que la pantalla de Equipo avise de que mover a alguien enciende o apaga un
/// perk suyo o de un compañero, en vez de dejar que el jugador lo descubra en el informe post-partido.
/// <para>
/// Solo se pronuncia sobre las condiciones que la alineación <b>decide por completo</b>: las que se
/// componen de <c>startsIn</c>, <c>startsOn</c>, <c>linked</c>, <c>hasTag</c>, <c>teammatesWithTag</c> y
/// <c>adjacentCount</c> sobre <c>owner</c>, con <c>!</c>, <c>&amp;&amp;</c>, <c>||</c> y comparaciones con
/// un literal entero. Cualquier otra función —<c>zone</c>, <c>scoreDiff</c>, <c>stat</c>, <c>counter</c>,
/// <c>attr</c>, <c>nearAlly</c>...— depende de cómo vaya el partido, así que el perk se <b>omite</b>:
/// es preferible no decir nada que prometer una activación que el partido puede desmentir.
/// </para>
/// <para>
/// La geometría no se reimplementa: sale de <see cref="LinkGeometry"/>, la misma que evalúa el motor
/// durante el partido, de modo que la previsualización y el partido no pueden divergir. Todo es
/// aritmética entera (RT-023) y la salida va en orden determinista: id de jugador ascendente y, dentro,
/// id de perk ascendente (RT-041).
/// </para>
/// <para>
/// <b>Invariante de datos que esto da por hecho:</b> un perk cuya condición use <c>linked(...)</c> declara
/// también esas relaciones en su campo <c>links</c>. El motor solo construye la tabla de vínculos si algún
/// perk en campo declara relaciones (<c>EffectEngine.HasLink</c> devuelve false sin ella), así que un perk
/// que preguntara por un vínculo sin declararlo evaluaría siempre false en partido y aquí saldría activo.
/// Los seis perks del catálogo que usan <c>linked</c> lo declaran; el cargador todavía no lo exige.
/// </para>
/// </summary>
public static class LineupPerkPreviewer
{
    /// <summary>
    /// Estado de los perks decidibles de los titulares de <paramref name="lineup"/>. Las casillas se leen
    /// tal cual (coordenadas de colocación, equipo 0), que es como las evalúa el motor para el local.
    /// </summary>
    public static IReadOnlyList<LineupPerkPreview> Preview(
        Lineup lineup, IReadOnlyList<PlayerDefinition> players, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(catalog);

        var starters = Starters(lineup, players);
        if (starters.Count == 0)
        {
            return Array.Empty<LineupPerkPreview>();
        }

        var board = new Board(starters);
        var result = new List<LineupPerkPreview>();
        for (int i = 0; i < starters.Count; i++)
        {
            var definition = starters[i].Definition;
            var perkIds = new List<string>(definition.Perks);
            perkIds.Sort(StringComparer.Ordinal);

            foreach (string perkId in perkIds)
            {
                var condition = catalog.Perks.Find(perkId)?.CompiledCondition;
                if (condition is null || condition.IsAlwaysTrue || condition.Ast is not { } ast)
                {
                    continue;
                }

                if (!TryEvaluate(ast, board, i, out bool holds))
                {
                    continue;
                }

                result.Add(new LineupPerkPreview(
                    definition.Id, perkId, holds ? LineupPerkStatus.Active : LineupPerkStatus.Inactive));
            }
        }

        return result;
    }

    /// <summary>
    /// Titulares con definición conocida, en id ascendente. El orden no es cosmético: el desempate de
    /// <see cref="LinkGeometry.ResolveLink"/> se apoya en él, igual que el de <c>LinkTable</c> (RT-041).
    /// </summary>
    private static List<Starter> Starters(Lineup lineup, IReadOnlyList<PlayerDefinition> players)
    {
        var starters = new List<Starter>(lineup.Slots.Count);
        foreach (var slot in lineup.Slots)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == slot.PlayerId)
                {
                    starters.Add(new Starter(slot.PlayerId, slot.HomeCell, players[i]));
                    break;
                }
            }
        }

        starters.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return starters;
    }

    // ------------------------------------------------------------------ evaluación del AST

    /// <summary>
    /// Evalúa el nodo sobre la alineación. Devuelve false si el nodo no es decidible desde la colocación;
    /// entonces el perk entero se omite, sin resultado parcial.
    /// </summary>
    private static bool TryEvaluate(LogicalExpression node, Board board, int self, out bool value)
    {
        value = false;
        switch (node)
        {
            case NCalc.Function function:
                return TryBooleanFunction(function, board, self, out value);

            case UnaryExpression unary when unary.Type == UnaryExpressionType.Not:
            {
                if (!TryEvaluate(unary.Expression, board, self, out bool inner))
                {
                    return false;
                }

                value = !inner;
                return true;
            }

            case BinaryExpression binary when binary.Type is BinaryExpressionType.And or BinaryExpressionType.Or:
            {
                if (!TryEvaluate(binary.LeftExpression, board, self, out bool left)
                    || !TryEvaluate(binary.RightExpression, board, self, out bool right))
                {
                    return false;
                }

                value = binary.Type == BinaryExpressionType.And ? left && right : left || right;
                return true;
            }

            case BinaryExpression binary when ConditionCompiler.IsComparison(binary.Type)
                && binary.LeftExpression is NCalc.Function function:
            {
                if (ConditionCompiler.TryReadInt(binary.RightExpression) is not { } literal
                    || !TryIntegerFunction(function, board, self, out int left))
                {
                    return false;
                }

                value = Compare(binary.Type, left, literal);
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Funciones booleanas que la alineación decide por completo.</summary>
    private static bool TryBooleanFunction(NCalc.Function function, Board board, int self, out bool value)
    {
        value = false;
        if (!IsOwner(function) || TextArgument(function, 1) is not { } literal)
        {
            return false;
        }

        switch (function.Identifier.Name)
        {
            case "hasTag":
                value = board.Starters[self].Definition.HasTag(literal);
                return true;

            case "startsIn":
            {
                int index = ConditionCompiler.NameIndex(ConditionCompiler.StartZoneNames, literal);
                if (index < 0)
                {
                    return false;
                }

                value = LinkGeometry.ZoneOfHome(board.Homes[self], board.Teams[self]) == (StartZone)index;
                return true;
            }

            case "startsOn":
            {
                int index = ConditionCompiler.NameIndex(ConditionCompiler.StartFlankNames, literal);
                if (index < 0)
                {
                    return false;
                }

                value = LinkGeometry.FlankOfHome(board.Homes[self], board.Teams[self]) == (StartFlank)index;
                return true;
            }

            case "linked":
            {
                int index = ConditionCompiler.NameIndex(ConditionCompiler.LinkNames, literal);
                if (index < 0)
                {
                    return false;
                }

                value = LinkGeometry.ResolveLink(board.Homes, board.Teams, self, (LinkRelation)index) >= 0;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Funciones enteras decidibles (van siempre dentro de una comparación con un literal). Cuentan sobre
    /// los titulares, que es exactamente lo que cuenta el motor: sus equivalentes de <c>MatchEngine</c>
    /// solo miran a compañeros con <c>OnPitch</c>, y al empezar el partido eso son los alineados.
    /// </summary>
    private static bool TryIntegerFunction(NCalc.Function function, Board board, int self, out int value)
    {
        value = 0;
        if (!IsOwner(function) || TextArgument(function, 1) is not { } tag)
        {
            return false;
        }

        bool adjacentOnly;
        switch (function.Identifier.Name)
        {
            case "teammatesWithTag":
                adjacentOnly = false;
                break;

            case "adjacentCount":
                adjacentOnly = true;
                break;

            default:
                return false;
        }

        for (int i = 0; i < board.Starters.Count; i++)
        {
            if (i == self || board.Teams[i] != board.Teams[self] || !board.Starters[i].Definition.HasTag(tag))
            {
                continue;
            }

            if (!adjacentOnly || Pitch.AreAdjacent(board.Homes[self], board.Homes[i]))
            {
                value++;
            }
        }

        return true;
    }

    private static bool Compare(BinaryExpressionType type, int left, int right) => type switch
    {
        BinaryExpressionType.Lesser => left < right,
        BinaryExpressionType.LesserOrEqual => left <= right,
        BinaryExpressionType.Greater => left > right,
        BinaryExpressionType.GreaterOrEqual => left >= right,
        BinaryExpressionType.Equal => left == right,
        BinaryExpressionType.NotEqual => left != right,
        _ => false,
    };

    /// <summary>
    /// True si el primer argumento es <c>owner</c>. Solo el portador es decidible desde la alineación:
    /// <c>actor</c>, <c>target</c> y <c>opponent</c> los pone el evento del partido.
    /// </summary>
    private static bool IsOwner(NCalc.Function function) =>
        function.Parameters.Count >= 1 && function.Parameters[0] is Identifier { Name: "owner" };

    private static string? TextArgument(NCalc.Function function, int index) =>
        function.Parameters.Count > index && function.Parameters[index] is ValueExpression { Value: string text }
            ? text
            : null;

    /// <summary>Titular con su casilla-hogar y su definición.</summary>
    private readonly record struct Starter(int Id, Cell Home, PlayerDefinition Definition);

    /// <summary>
    /// Alineación normalizada: casillas y equipos en arrays paralelos a <see cref="Starters"/>, que es lo
    /// que consume <see cref="LinkGeometry.ResolveLink"/>. Todos los titulares son del mismo equipo, el 0,
    /// porque una alineación es de un solo equipo; el array de equipos existe para compartir la geometría
    /// con <c>LinkTable</c>, que sí mezcla los dos.
    /// </summary>
    private sealed class Board
    {
        public Board(List<Starter> starters)
        {
            Starters = starters;
            Homes = new Cell[starters.Count];
            Teams = new int[starters.Count];
            for (int i = 0; i < starters.Count; i++)
            {
                Homes[i] = starters[i].Home;
            }
        }

        public List<Starter> Starters { get; }

        public Cell[] Homes { get; }

        public int[] Teams { get; }
    }
}
