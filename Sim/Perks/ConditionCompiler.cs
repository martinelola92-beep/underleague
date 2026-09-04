using System.Globalization;
using NCalc;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>Tipo estático del resultado de un nodo de condición. Cerrado: no hay flotantes (RT-023).</summary>
internal enum ConditionValueKind
{
    Bool,
    Int,
    Str,
}

/// <summary>Forma del argumento de una función de condición (§2).</summary>
internal enum ConditionArgKind
{
    /// <summary>Identificador sin comillas: actor, target, opponent, owner.</summary>
    Who,

    /// <summary>Literal de cadena con una etiqueta (RF-068).</summary>
    Tag,

    /// <summary>Literal de cadena con el nombre de un atributo en minúsculas.</summary>
    Attribute,

    /// <summary>Literal de cadena con el nombre de un contador (RF-070).</summary>
    CounterName,

    /// <summary>Literal entero (por ejemplo, el radio en casillas de nearAlly/nearOpponent).</summary>
    Number,

    /// <summary>Literal de cadena con un tercio de inicio: OwnThird, Middle, AttackingThird.</summary>
    StartZone,

    /// <summary>Literal de cadena con una banda de inicio: LeftFlank, Center, RightFlank.</summary>
    StartFlank,

    /// <summary>Literal de cadena con una relación de vínculo direccional (ADR 0021).</summary>
    Link,

    /// <summary>Literal de cadena con una estadística del partido en curso (RF-119).</summary>
    Stat,
}

/// <summary>Firma de una función de condición: aridad, tipos de argumento y tipo de retorno (RT-034).</summary>
internal sealed record ConditionFunctionSignature(
    string Name,
    IReadOnlyList<ConditionArgKind> Arguments,
    ConditionValueKind Returns);

/// <summary>
/// Compila las condiciones NCalc de los perks una sola vez, al cargar /data (RT-034, ADR 0003).
/// <para>
/// La gramática aceptada es deliberadamente cerrada, por tres motivos que van juntos: (1) el tipo de cada
/// nodo se conoce estáticamente, así que ninguna condición puede devolver algo que no sea booleano en
/// partido; (2) toda la aritmética es entera (RT-023), porque no hay literales flotantes ni división
/// real; y (3) toda condición del catálogo es describible por construcción (RT-035, §4), porque cada
/// forma sintáctica admitida tiene su plantilla de descripción.
/// </para>
/// <code>
/// Cond  := Cond ('&amp;&amp;' | '||') Cond | '!' Cond | '(' Cond ')' | BoolFn | IntFn Cmp IntLit | StrFn ('=='|'!=') StrLit
/// Cmp   := '&lt;' | '&lt;=' | '&gt;' | '&gt;=' | '==' | '!='
/// </code>
/// Cualquier otra forma (aritmética suelta, comparación entre dos funciones, literal a la izquierda,
/// función o identificador desconocido, tipo incorrecto) es <see cref="DataException"/> al cargar.
/// </summary>
public static class ConditionCompiler
{
    /// <summary>Tabla cerrada de funciones (§2). Se recorre por índice, nunca se itera un diccionario.</summary>
    internal static readonly ConditionFunctionSignature[] Signatures =
    {
        new("hasTag", new[] { ConditionArgKind.Who, ConditionArgKind.Tag }, ConditionValueKind.Bool),
        new("attr", new[] { ConditionArgKind.Who, ConditionArgKind.Attribute }, ConditionValueKind.Int),
        new("level", new[] { ConditionArgKind.Who }, ConditionValueKind.Int),
        new("position", new[] { ConditionArgKind.Who }, ConditionValueKind.Str),
        new("isMob", Array.Empty<ConditionArgKind>(), ConditionValueKind.Bool),
        new("bias", Array.Empty<ConditionArgKind>(), ConditionValueKind.Int),
        new("zone", new[] { ConditionArgKind.Who }, ConditionValueKind.Str),
        new("adjacent", new[] { ConditionArgKind.Who, ConditionArgKind.Tag }, ConditionValueKind.Bool),
        new("adjacentCount", new[] { ConditionArgKind.Who, ConditionArgKind.Tag }, ConditionValueKind.Int),
        new("teammatesWithTag", new[] { ConditionArgKind.Who, ConditionArgKind.Tag }, ConditionValueKind.Int),
        new("distanceToGoal", new[] { ConditionArgKind.Who }, ConditionValueKind.Int),
        new("scoreDiff", Array.Empty<ConditionArgKind>(), ConditionValueKind.Int),
        new("tick", Array.Empty<ConditionArgKind>(), ConditionValueKind.Int),
        new("counter", new[] { ConditionArgKind.CounterName }, ConditionValueKind.Int),
        new("detail", Array.Empty<ConditionArgKind>(), ConditionValueKind.Str),

        // Funciones nuevas del rediseño espacial (fase1b-diseno.md §1.5, docs/perks-ejes.md). Cierran los
        // tres ejes que faltaban -alineación, zona de inicio y proximidad dinámica- y abaratan el eje de
        // acumulación, que hasta ahora obligaba a declarar un contador propio para leer algo que el motor
        // ya lleva para el informe post-partido (RF-119).
        new("startsIn", new[] { ConditionArgKind.Who, ConditionArgKind.StartZone }, ConditionValueKind.Bool),
        new("startsOn", new[] { ConditionArgKind.Who, ConditionArgKind.StartFlank }, ConditionValueKind.Bool),
        new("linked", new[] { ConditionArgKind.Who, ConditionArgKind.Link }, ConditionValueKind.Bool),
        new("nearAlly", new[] { ConditionArgKind.Who, ConditionArgKind.Tag, ConditionArgKind.Number }, ConditionValueKind.Bool),
        new("nearOpponent", new[] { ConditionArgKind.Who, ConditionArgKind.Tag, ConditionArgKind.Number }, ConditionValueKind.Bool),
        new("stat", new[] { ConditionArgKind.Who, ConditionArgKind.Stat }, ConditionValueKind.Int),
    };

    /// <summary>Nombres de los tercios de inicio admitidos por <c>startsIn</c>, en orden de StartZone.</summary>
    internal static readonly string[] StartZoneNames = { "OwnThird", "Middle", "AttackingThird" };

    /// <summary>Nombres de las bandas de inicio admitidas por <c>startsOn</c>, en orden de StartFlank.</summary>
    internal static readonly string[] StartFlankNames = { "LeftFlank", "Center", "RightFlank" };

    /// <summary>Nombres de las relaciones de vínculo, en orden de <see cref="LinkRelation"/>.</summary>
    internal static readonly string[] LinkNames =
    {
        "beside", "ahead", "behind", "left", "right", "diagonalAhead", "diagonalBehind",
    };

    /// <summary>Nombres de las estadísticas de <c>stat</c>, en orden de <see cref="MatchStat"/>.</summary>
    internal static readonly string[] StatNames = { "goals", "passesCompleted", "tacklesWon", "shots", "saves" };

    /// <summary>Radio máximo en casillas de nearAlly/nearOpponent: más allá cubre el campo entero.</summary>
    internal const int MaxProximityCells = 8;

    private static readonly string[] WhoNames = { "actor", "target", "opponent", "owner" };

    private static readonly string[] AttributeNames = { "strength", "speed", "technique", "stamina", "leash" };

    /// <summary>Nombres de los cuatro identificadores válidos en una condición, en orden de <see cref="WhoRef"/>.</summary>
    public static IReadOnlyList<string> WhoIdentifiers => WhoNames;

    /// <summary>Nombres de función admitidos en una condición, en el orden de la tabla de §2.</summary>
    public static IReadOnlyList<string> FunctionNames => Signatures.Select(s => s.Name).ToArray();

    /// <summary>
    /// Compila source. Una condición vacía es "siempre verdadera" y no crea ninguna expresión NCalc.
    /// Lanza <see cref="DataException"/> con fichero y ruta JSON ante cualquier problema.
    /// </summary>
    public static CompiledCondition Compile(string source, string file, string jsonPath)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Trim().Length == 0)
        {
            return CompiledCondition.AlwaysTrue;
        }

        LogicalExpression ast;
        try
        {
            var parser = new Expression(source, CultureInfo.InvariantCulture) { Options = ExpressionOptions.NoCache };
            ast = parser.GetLogicalExpression()
                ?? throw new DataException(file, jsonPath, $"condición no parseable: '{source}'");
        }
        catch (NCalc.Exceptions.NCalcException ex)
        {
            throw new DataException(file, jsonPath, $"condición no parseable: '{source}' ({ex.Message})");
        }

        var kind = Validate(ast, file, jsonPath, source);
        if (kind != ConditionValueKind.Bool)
        {
            throw new DataException(
                file, jsonPath, $"la condición '{source}' devuelve {kind}; una condición debe ser booleana");
        }

        var compiled = new CompiledCondition(source, ast);

        // Evaluación de prueba (encargo del paquete F): recorre el cableado real de NCalc con un contexto
        // neutro. La validación estática de arriba ya cubre todo el AST -y el && de NCalc cortocircuita,
        // así que esta pasada sola no bastaría-, pero atrapa cualquier desajuste entre la tabla de firmas
        // y los manejadores reales antes de que llegue a un partido.
        object? probe;
        try
        {
            probe = compiled.EvaluateProbe();
        }
        catch (Exception ex) when (ex is NCalc.Exceptions.NCalcException or InvalidOperationException)
        {
            throw new DataException(file, jsonPath, $"la condición '{source}' no se puede evaluar: {ex.Message}");
        }

        if (probe is not bool)
        {
            throw new DataException(
                file,
                jsonPath,
                $"la condición '{source}' evaluó a '{probe?.GetType().Name ?? "null"}'; debe ser booleana");
        }

        return compiled;
    }

    /// <summary>Índice de la función name en <see cref="Signatures"/>; -1 si no existe.</summary>
    internal static int FunctionIndex(string name)
    {
        for (int i = 0; i < Signatures.Length; i++)
        {
            if (string.Equals(Signatures[i].Name, name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Resuelve un identificador de condición (actor/target/opponent/owner); -1 si no existe.</summary>
    internal static int WhoIndex(string name)
    {
        for (int i = 0; i < WhoNames.Length; i++)
        {
            if (string.Equals(WhoNames[i], name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Resuelve el nombre en minúsculas de un atributo; null si no existe.</summary>
    internal static AttributeKind? Attribute(string name)
    {
        for (int i = 0; i < AttributeNames.Length; i++)
        {
            if (string.Equals(AttributeNames[i], name, StringComparison.Ordinal))
            {
                return (AttributeKind)i;
            }
        }

        return null;
    }

    /// <summary>Nombre en minúsculas del atributo, tal y como se escribe en /data.</summary>
    internal static string AttributeName(AttributeKind kind) => AttributeNames[(int)kind];

    /// <summary>Índice de text en names con comparación ordinal; -1 si no está.</summary>
    internal static int NameIndex(IReadOnlyList<string> names, string text)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], text, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Etiquetas (RF-068) que la condición nombra como literal. Lo usa <see cref="PerkLoader"/> para
    /// rechazar que un perk universal consulte la etiqueta de especie (ADR 0023): la comprobación se hace
    /// sobre el AST ya analizado, no sobre el texto, así que no se puede colar por espaciado ni comillas.
    /// </summary>
    internal static IReadOnlyList<string> TagLiterals(LogicalExpression? ast)
    {
        if (ast is null)
        {
            return Array.Empty<string>();
        }

        var tags = new List<string>();
        Collect(ast, tags);
        return tags;
    }

    private static void Collect(LogicalExpression node, List<string> tags)
    {
        switch (node)
        {
            case NCalc.Function function:
            {
                int index = FunctionIndex(function.Identifier.Name);
                if (index < 0)
                {
                    return;
                }

                var signature = Signatures[index];
                for (int i = 0; i < function.Parameters.Count && i < signature.Arguments.Count; i++)
                {
                    if (signature.Arguments[i] == ConditionArgKind.Tag
                        && function.Parameters[i] is ValueExpression { Value: string text })
                    {
                        tags.Add(text);
                    }
                }

                return;
            }

            case UnaryExpression unary:
                Collect(unary.Expression, tags);
                return;

            case BinaryExpression binary:
                Collect(binary.LeftExpression, tags);
                Collect(binary.RightExpression, tags);
                return;

            default:
                return;
        }
    }

    /// <summary>Comprueba el nodo y devuelve su tipo estático; lanza DataException si no es admisible.</summary>
    private static ConditionValueKind Validate(LogicalExpression node, string file, string path, string source)
    {
        switch (node)
        {
            case NCalc.Function function:
                return ValidateFunction(function, file, path, source);

            case UnaryExpression unary when unary.Type == UnaryExpressionType.Not:
                Require(
                    Validate(unary.Expression, file, path, source) == ConditionValueKind.Bool,
                    file, path, $"'!' se aplica a algo que no es booleano en '{source}'");
                return ConditionValueKind.Bool;

            case BinaryExpression binary when binary.Type is BinaryExpressionType.And or BinaryExpressionType.Or:
                Require(
                    Validate(binary.LeftExpression, file, path, source) == ConditionValueKind.Bool
                        && Validate(binary.RightExpression, file, path, source) == ConditionValueKind.Bool,
                    file, path, $"'&&'/'||' exigen operandos booleanos en '{source}'");
                return ConditionValueKind.Bool;

            case BinaryExpression binary when IsComparison(binary.Type):
                ValidateComparison(binary, file, path, source);
                return ConditionValueKind.Bool;

            default:
                throw new DataException(
                    file,
                    path,
                    $"expresión no admitida en la condición '{source}': solo se aceptan funciones del catálogo, "
                        + "'&&', '||', '!' y comparaciones de una función con un literal");
        }
    }

    private static ConditionValueKind ValidateFunction(NCalc.Function function, string file, string path, string source)
    {
        string name = function.Identifier.Name;
        int index = FunctionIndex(name);
        if (index < 0)
        {
            throw new DataException(file, path, $"función desconocida '{name}' en la condición '{source}'");
        }

        var signature = Signatures[index];
        var arguments = function.Parameters;
        if (arguments.Count != signature.Arguments.Count)
        {
            throw new DataException(
                file,
                path,
                $"'{name}' espera {signature.Arguments.Count} argumento(s) y recibe {arguments.Count} en '{source}'");
        }

        for (int i = 0; i < arguments.Count; i++)
        {
            ValidateArgument(name, signature.Arguments[i], arguments[i], file, path, source);
        }

        return signature.Returns;
    }

    private static void ValidateArgument(
        string function, ConditionArgKind kind, LogicalExpression argument, string file, string path, string source)
    {
        if (kind == ConditionArgKind.Who)
        {
            if (argument is not Identifier identifier)
            {
                throw new DataException(
                    file, path, $"'{function}' espera un identificador (actor, target, opponent, owner) en '{source}'");
            }

            if (WhoIndex(identifier.Name) < 0)
            {
                throw new DataException(
                    file, path, $"identificador desconocido '{identifier.Name}' en la condición '{source}'");
            }

            return;
        }

        if (kind == ConditionArgKind.Number)
        {
            int? cells = TryReadInt(argument);
            if (cells is null)
            {
                throw new DataException(file, path, $"'{function}' espera un entero literal en '{source}'");
            }

            if (cells.Value < 1 || cells.Value > MaxProximityCells)
            {
                throw new DataException(
                    file, path, $"'{function}' admite un radio de 1 a {MaxProximityCells} casillas en '{source}'");
            }

            return;
        }

        if (argument is not ValueExpression value || value.Type != NCalc.ValueType.String || value.Value is not string text)
        {
            throw new DataException(file, path, $"'{function}' espera un literal de cadena en '{source}'");
        }

        if (text.Length == 0)
        {
            throw new DataException(file, path, $"'{function}' recibe una cadena vacía en '{source}'");
        }

        if (kind == ConditionArgKind.Attribute && Attribute(text) is null)
        {
            throw new DataException(file, path, $"atributo desconocido '{text}' en la condición '{source}'");
        }

        var (names, what) = kind switch
        {
            ConditionArgKind.StartZone => (StartZoneNames, "tercio de inicio"),
            ConditionArgKind.StartFlank => (StartFlankNames, "banda de inicio"),
            ConditionArgKind.Link => (LinkNames, "relación de vínculo"),
            ConditionArgKind.Stat => (StatNames, "estadística"),
            _ => (Array.Empty<string>(), string.Empty),
        };

        if (names.Length > 0 && NameIndex(names, text) < 0)
        {
            throw new DataException(file, path, $"{what} desconocida '{text}' en la condición '{source}'");
        }
    }

    private static void ValidateComparison(BinaryExpression binary, string file, string path, string source)
    {
        if (binary.LeftExpression is not NCalc.Function function)
        {
            throw new DataException(
                file,
                path,
                $"en '{source}' una comparación debe tener una función a la izquierda y un literal a la derecha");
        }

        var left = ValidateFunction(function, file, path, source);
        if (left == ConditionValueKind.Bool)
        {
            throw new DataException(
                file, path, $"'{function.Identifier.Name}' ya es booleana; no se compara con nada en '{source}'");
        }

        if (left == ConditionValueKind.Int)
        {
            if (TryReadInt(binary.RightExpression) is null)
            {
                throw new DataException(
                    file, path, $"'{function.Identifier.Name}' se compara con algo que no es un entero literal en '{source}'");
            }

            return;
        }

        if (binary.Type is not (BinaryExpressionType.Equal or BinaryExpressionType.NotEqual))
        {
            throw new DataException(
                file, path, $"'{function.Identifier.Name}' devuelve texto: solo admite '==' y '!=' en '{source}'");
        }

        if (binary.RightExpression is not ValueExpression value
            || value.Type != NCalc.ValueType.String
            || value.Value is not string)
        {
            throw new DataException(
                file, path, $"'{function.Identifier.Name}' se compara con algo que no es un literal de cadena en '{source}'");
        }
    }

    /// <summary>True si el operador es una de las seis comparaciones admitidas.</summary>
    internal static bool IsComparison(BinaryExpressionType type) => type
        is BinaryExpressionType.Lesser
        or BinaryExpressionType.LesserOrEqual
        or BinaryExpressionType.Greater
        or BinaryExpressionType.GreaterOrEqual
        or BinaryExpressionType.Equal
        or BinaryExpressionType.NotEqual;

    /// <summary>
    /// Lee un literal entero, con o sin signo. NCalc analiza "-3" como <c>Negate(3)</c>, así que el signo
    /// llega como nodo unario y no como parte del literal.
    /// </summary>
    internal static int? TryReadInt(LogicalExpression node)
    {
        if (node is UnaryExpression unary)
        {
            int? inner = TryReadInt(unary.Expression);
            if (inner is null)
            {
                return null;
            }

            return unary.Type switch
            {
                UnaryExpressionType.Negate => -inner.Value,
                UnaryExpressionType.Positive => inner.Value,
                _ => null,
            };
        }

        if (node is ValueExpression value && value.Type == NCalc.ValueType.Integer)
        {
            return value.Value switch
            {
                int i => i,
                long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
                _ => null,
            };
        }

        return null;
    }

    private static void Require(bool condition, string file, string path, string message)
    {
        if (!condition)
        {
            throw new DataException(file, path, message);
        }
    }
}

/// <summary>
/// Condición de un perk ya compilada (RT-034). Se construye al cargar /data y se reutiliza durante todo
/// el partido: en tiempo de partido solo se evalúa un AST ya analizado, sin reflexión, sin
/// <c>dynamic</c> y sin compilar nada.
/// <para>
/// El contexto de la evaluación en curso se guarda en un campo de la propia instancia porque los
/// manejadores de NCalc reciben solo <c>(nombre, argumentos)</c>. Es seguro porque /Sim es estrictamente
/// síncrono y de un solo hilo (RT-021 prohíbe <c>Parallel</c>): no hay dos evaluaciones vivas a la vez.
/// </para>
/// </summary>
public sealed class CompiledCondition
{
    /// <summary>Valor neutro de attr() cuando el jugador nombrado no existe en el evento (§2).</summary>
    private const int NeutralAttribute = 50;

    private static readonly string[] ZoneNames = { "Own", "Middle", "Opposing" };
    private static readonly string[] PositionNames = { "Goalkeeper", "Defender", "Midfielder", "Forward" };

    private readonly NCalc.Expression? _expression;
    private ConditionContext _context;
    private bool _probe;

    internal CompiledCondition(string source, LogicalExpression ast)
    {
        Source = source;
        Ast = ast;
        _expression = new NCalc.Expression(source, CultureInfo.InvariantCulture)
        {
            Options = ExpressionOptions.NoCache,
        };
        _expression.EvaluateParameter += OnParameter;
        _expression.EvaluateFunction += OnFunction;
    }

    private CompiledCondition()
    {
        Source = string.Empty;
        Ast = null;
    }

    /// <summary>Condición vacía: siempre verdadera, sin expresión NCalc detrás (§2).</summary>
    public static CompiledCondition AlwaysTrue { get; } = new();

    /// <summary>Texto original de la condición, tal cual está en /data.</summary>
    public string Source { get; }

    /// <summary>True si la condición está vacía y por tanto siempre se cumple.</summary>
    public bool IsAlwaysTrue => _expression is null;

    /// <summary>AST analizado por NCalc; lo recorre el generador de descripciones (RT-035).</summary>
    internal LogicalExpression? Ast { get; }

    /// <summary>
    /// Evalúa la condición sobre el contexto del evento. Por construcción (validación de carga) no puede
    /// devolver algo que no sea booleano ni encontrar una función o identificador desconocidos; si aun
    /// así ocurriera, es un fallo del motor y no del dato, y se convierte en InvalidOperationException.
    /// </summary>
    internal bool Evaluate(in ConditionContext context)
    {
        if (_expression is null)
        {
            return true;
        }

        _context = context;
        _probe = false;
        object? result = _expression.Evaluate();
        if (result is bool value)
        {
            return value;
        }

        throw new InvalidOperationException(
            $"la condición '{Source}' devolvió '{result?.GetType().Name ?? "null"}' en partido; debía ser booleana");
    }

    /// <summary>Evaluación de prueba con valores neutros, solo durante la carga (ver ConditionCompiler).</summary>
    internal object? EvaluateProbe()
    {
        if (_expression is null)
        {
            return true;
        }

        _probe = true;
        try
        {
            return _expression.Evaluate();
        }
        finally
        {
            _probe = false;
        }
    }

    private void OnParameter(string name, NCalc.Handlers.ParameterEventArgs args)
    {
        int index = ConditionCompiler.WhoIndex(name);
        if (index < 0)
        {
            throw new InvalidOperationException($"identificador desconocido '{name}' en la condición '{Source}'");
        }

        args.Result = index;
    }

    private void OnFunction(string name, NCalc.Handlers.FunctionEventArgs args)
    {
        switch (name)
        {
            case "hasTag":
            {
                var who = Player(args, 0);
                args.Result = who is not null && who.Definition.HasTag(Text(args, 1));
                break;
            }

            case "attr":
            {
                var who = Player(args, 0);
                var kind = ConditionCompiler.Attribute(Text(args, 1))!.Value;
                args.Result = who is null ? NeutralAttribute : who.Effective(kind);
                break;
            }

            case "level":
            {
                var who = Player(args, 0);
                args.Result = who is null ? 1 : who.Definition.Level;
                break;
            }

            case "position":
            {
                var who = Player(args, 0);
                args.Result = who is null ? PositionNames[0] : PositionNames[(int)who.Role];
                break;
            }

            case "isMob":
                args.Result = !_probe && _context.World.IsMob;
                break;

            case "bias":
                args.Result = _probe ? 0 : _context.World.BiasFor(_context.Owner.Team);
                break;

            case "zone":
            {
                var who = Player(args, 0);
                args.Result = who is null ? ZoneNames[0] : ZoneNames[(int)_context.World.ZoneOf(who)];
                break;
            }

            case "adjacent":
            {
                var who = Player(args, 0);
                args.Result = who is not null && _context.World.AdjacentCount(who, Text(args, 1)) > 0;
                break;
            }

            case "adjacentCount":
            {
                var who = Player(args, 0);
                args.Result = who is null ? 0 : _context.World.AdjacentCount(who, Text(args, 1));
                break;
            }

            case "teammatesWithTag":
            {
                var who = Player(args, 0);
                args.Result = who is null ? 0 : _context.World.TeammatesWithTag(who, Text(args, 1));
                break;
            }

            case "distanceToGoal":
            {
                // Toma un jugador explícito, como el resto de funciones que hablan de alguien: el implícito
                // era siempre el actor y no dejaba preguntar por el portador ni por el rival (paquete S).
                var who = Player(args, 0);
                args.Result = who is null ? 0 : _context.World.DistanceToGoalCells(who);
                break;
            }

            case "scoreDiff":
                args.Result = _probe ? 0 : _context.World.ScoreDiff(_context.Owner.Team);
                break;

            case "tick":
                args.Result = _probe ? 0 : _context.World.Tick;
                break;

            case "counter":
                args.Result = _probe ? 0 : _context.World.Counter(_context.Owner, Text(args, 0));
                break;

            case "detail":
                args.Result = _probe ? string.Empty : _context.Detail;
                break;

            case "startsIn":
            {
                var who = Player(args, 0);
                int index = ConditionCompiler.NameIndex(ConditionCompiler.StartZoneNames, Text(args, 1));
                args.Result = who is not null
                    && LinkGeometry.ZoneOfHome(who.HomeCell, who.Team) == (StartZone)index;
                break;
            }

            case "startsOn":
            {
                var who = Player(args, 0);
                int index = ConditionCompiler.NameIndex(ConditionCompiler.StartFlankNames, Text(args, 1));
                args.Result = who is not null
                    && LinkGeometry.FlankOfHome(who.HomeCell, who.Team) == (StartFlank)index;
                break;
            }

            case "linked":
            {
                var who = Player(args, 0);
                int index = ConditionCompiler.NameIndex(ConditionCompiler.LinkNames, Text(args, 1));
                args.Result = who is not null && _context.Perks.HasLink(who, (LinkRelation)index);
                break;
            }

            case "nearAlly":
            {
                var who = Player(args, 0);
                args.Result = who is not null && _context.Perks.NearAlly(who, Text(args, 1), Number(args, 2));
                break;
            }

            case "nearOpponent":
            {
                var who = Player(args, 0);
                args.Result = who is not null && _context.Perks.NearOpponent(who, Text(args, 1), Number(args, 2));
                break;
            }

            case "stat":
            {
                var who = Player(args, 0);
                int index = ConditionCompiler.NameIndex(ConditionCompiler.StatNames, Text(args, 1));
                args.Result = who is null ? 0 : _context.Perks.Stat(who, (MatchStat)index);
                break;
            }

            default:
                throw new InvalidOperationException($"función desconocida '{name}' en la condición '{Source}'");
        }
    }

    /// <summary>
    /// Jugador nombrado por el argumento index; null si el evento no lo trae (target/opponent ausentes,
    /// §2) o si es la evaluación de prueba de la carga.
    /// </summary>
    private Engine.MatchPlayer? Player(NCalc.Handlers.FunctionEventArgs args, int index)
    {
        if (_probe)
        {
            return null;
        }

        return _context.Who((WhoRef)(int)args.Parameters.Evaluate(index)!);
    }

    private static string Text(NCalc.Handlers.FunctionEventArgs args, int index) =>
        (string)args.Parameters.Evaluate(index)!;

    /// <summary>Literal entero de un argumento (radio en casillas); la carga ya comprobó que lo es.</summary>
    private static int Number(NCalc.Handlers.FunctionEventArgs args, int index) =>
        Convert.ToInt32(args.Parameters.Evaluate(index)!, CultureInfo.InvariantCulture);
}
