using System.Globalization;
using System.Text;
using NCalc;
using Underleague.Sim.Data;
using Underleague.Sim.Events;

namespace Underleague.Sim.Perks;

/// <summary>
/// Genera la descripción de un perk **desde su efecto** (RT-035): no existe texto escrito a mano, solo
/// plantillas localizadas en <c>data/l10n/&lt;lang&gt;/templates.json</c>. Es imposible por construcción
/// que la descripción y el efecto diverjan, porque la descripción se compone del mismo dato que ejecuta
/// el motor.
/// <para>
/// La condición se traduce recorriendo el AST de NCalc. Como la gramática admitida por
/// <see cref="ConditionCompiler"/> es cerrada, cada forma sintáctica tiene su clave de plantilla y
/// cualquier condición del catálogo es describible; si falta una clave, la carga falla (no se emite un
/// texto degradado).
/// </para>
/// </summary>
public static class DescriptionGenerator
{
    private const string Layout = "layout";
    private const string Effects = "effects";
    private const string Triggers = "triggers";
    private const string Conditions = "conditions";
    private const string TargetsSection = "targets";
    private const string Durations = "durations";
    private const string Limits = "limits";
    private const string AttributesSection = "attributes";
    private const string Probabilities = "probabilities";
    private const string TagsSection = "tags";
    private const string PositionsSection = "positions";
    private const string ZonesSection = "zones";
    private const string DetailsSection = "details";
    private const string EventsSection = "events";
    private const string CountersSection = "counters";

    /// <summary>Descripción completa del perk en el idioma pedido (RT-035).</summary>
    public static string Describe(PerkDefinition perk, string language, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Describe(perk, catalog.Localization.Get(language));
    }

    /// <summary>Descripción completa del perk con unas plantillas ya resueltas.</summary>
    public static string Describe(PerkDefinition perk, DescriptionTemplates templates)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(templates);

        string trigger = templates.Get(Triggers, EventTypeNames.ToUpperSnake(perk.Trigger));
        string condition = perk.CompiledCondition.Ast is { } ast ? DescribeCondition(ast, templates) : string.Empty;
        string separator = templates.Get(Layout, "effectSeparator");

        string triggerNoun = templates.Get(EventsSection, EventTypeNames.ToUpperSnake(perk.Trigger));
        var effects = new StringBuilder();
        AppendEffects(effects, perk.Effects, templates, separator, triggerNoun);
        if (perk.ElseEffects.Count > 0)
        {
            effects.Append(templates.Get(Layout, "elsePrefix"));
            AppendEffects(effects, perk.ElseEffects, templates, separator, triggerNoun);
        }

        string limit = perk.Limit is { } l
            ? Replace(templates.Get(Limits, LimitScopeKey(l.Per)), "{times}", l.Times.ToString(CultureInfo.InvariantCulture))
            : string.Empty;

        string layoutKey = (condition.Length > 0, limit.Length > 0) switch
        {
            (true, true) => "withConditionAndLimit",
            (true, false) => "withCondition",
            (false, true) => "withLimit",
            _ => "plain",
        };

        string text = templates.Get(Layout, layoutKey);
        text = Replace(text, "{trigger}", trigger);
        text = Replace(text, "{condition}", condition);
        text = Replace(text, "{effects}", effects.ToString());
        return Replace(text, "{limit}", limit);
    }

    /// <summary>
    /// Comprueba que el perk es describible en todos los idiomas cargados. Lo llama el cargador de /data
    /// con cada perk: un perk con una clave de plantilla que falta es un error de carga, no un texto raro
    /// en pantalla (RT-035, RT-032).
    /// </summary>
    internal static void EnsureDescribable(PerkDefinition perk, Localization localization, string file, string jsonPath)
    {
        var languages = localization.All;
        for (int i = 0; i < languages.Count; i++)
        {
            try
            {
                _ = Describe(perk, languages[i]);
            }
            catch (InvalidOperationException ex)
            {
                throw new DataException(file, jsonPath, $"perk no describible: {ex.Message}");
            }
        }
    }

    private static void AppendEffects(
        StringBuilder builder,
        IReadOnlyList<EffectDefinition> effects,
        DescriptionTemplates templates,
        string separator,
        string triggerNoun)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(separator);
            }

            builder.Append(DescribeEffect(effects[i], templates, triggerNoun));
        }
    }

    private static string DescribeEffect(EffectDefinition effect, DescriptionTemplates templates, string triggerNoun)
    {
        string key = effect.Type switch
        {
            EffectType.ModifyAttribute when !effect.UsesCounter => "modifyAttribute",
            EffectType.ModifyAttribute => effect.CounterDivisor > 1
                ? "modifyAttributePerCounterDivided"
                : "modifyAttributePerCounter",
            EffectType.ModifyLeash => "modifyLeash",
            EffectType.ModifyBias => "modifyBias",
            EffectType.ModifyProbability => "modifyProbability",
            EffectType.CancelEvent => "cancelEvent",
            EffectType.AddCounter => "addCounter",
            EffectType.SetState => "setState",
            _ => throw new InvalidOperationException($"tipo de efecto sin plantilla: {effect.Type}"),
        };

        string text = templates.Get(Effects, key);
        text = Replace(text, "{target}", DescribeTarget(effect, templates));
        text = Replace(text, "{attribute}", templates.Get(AttributesSection, ConditionCompiler.AttributeName(effect.Attribute)));
        text = Replace(text, "{duration}", templates.Get(Durations, DurationKey(effect.Duration)));
        text = Replace(text, "{probability}", templates.Get(Probabilities, ProbabilityKey(effect.Probability)));
        text = Replace(text, "{counter}", CounterName(effect.Counter, templates));
        text = Replace(text, "{value:+%}", Percent(effect.Value));
        text = Replace(text, "{value:+}", Signed(effect.Value));
        text = Replace(text, "{value}", effect.Value.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{valuePerCounter:+}", Signed(effect.ValuePerCounter));
        text = Replace(text, "{maxValue}", effect.MaxValue.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{counterDivisor}", effect.CounterDivisor.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{ticks}", effect.Ticks.ToString(CultureInfo.InvariantCulture));
        return Replace(text, "{event}", triggerNoun);
    }

    private static string DescribeTarget(EffectDefinition effect, DescriptionTemplates templates)
    {
        string key = effect.Target switch
        {
            EffectTarget.Actor => "actor",
            EffectTarget.Target => "target",
            EffectTarget.Opponent => "opponent",
            EffectTarget.Owner => "owner",
            EffectTarget.Adjacent => "adjacent",
            EffectTarget.Team => "team",
            EffectTarget.OpposingTeam => "opposingTeam",
            EffectTarget.WithTag => "withTag",
            EffectTarget.AdjacentWithTag => "adjacentWithTag",
            _ => throw new InvalidOperationException($"objetivo sin plantilla: {effect.Target}"),
        };

        return Replace(templates.Get(TargetsSection, key), "{tag}", Tag(effect.TargetTag, templates));
    }

    // ------------------------------------------------------------------ condiciones (pretty-printer)

    private static string DescribeCondition(LogicalExpression node, DescriptionTemplates templates)
    {
        switch (node)
        {
            case NCalc.Function function:
                return DescribeFunctionCondition(function, templates, string.Empty, 0);

            case UnaryExpression unary when unary.Type == UnaryExpressionType.Not:
                return Replace(templates.Get(Conditions, "not"), "{a}", DescribeCondition(unary.Expression, templates));

            case BinaryExpression binary when binary.Type is BinaryExpressionType.And or BinaryExpressionType.Or:
            {
                string template = templates.Get(Conditions, binary.Type == BinaryExpressionType.And ? "and" : "or");
                template = Replace(template, "{a}", DescribeCondition(binary.LeftExpression, templates));
                return Replace(template, "{b}", DescribeCondition(binary.RightExpression, templates));
            }

            case BinaryExpression binary when ConditionCompiler.IsComparison(binary.Type)
                && binary.LeftExpression is NCalc.Function function:
            {
                string suffix = OperatorSuffix(binary.Type);
                int number = ConditionCompiler.TryReadInt(binary.RightExpression) ?? 0;
                string literal = binary.RightExpression is ValueExpression { Value: string text } ? text : string.Empty;
                return DescribeFunctionCondition(function, templates, suffix, number, literal);
            }

            default:
                throw new InvalidOperationException("condición no describible: forma sintáctica no admitida");
        }
    }

    private static string DescribeFunctionCondition(
        NCalc.Function function, DescriptionTemplates templates, string suffix, int number, string literal = "")
    {
        string name = function.Identifier.Name;
        string template = templates.Get(Conditions, name + suffix);
        var arguments = function.Parameters;

        for (int i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] is Identifier identifier)
            {
                template = Replace(template, "{who}", templates.Get(TargetsSection, identifier.Name));
                continue;
            }

            if (arguments[i] is not ValueExpression { Value: string text })
            {
                continue;
            }

            template = name switch
            {
                "attr" => Replace(template, "{attribute}", templates.Get(AttributesSection, text)),
                "counter" => Replace(template, "{counter}", CounterName(text, templates)),
                _ => Replace(template, "{tag}", Tag(text, templates)),
            };
        }

        template = Replace(template, "{n}", number.ToString(CultureInfo.InvariantCulture));
        if (literal.Length > 0)
        {
            template = Replace(template, "{position}", templates.Find(PositionsSection, literal) ?? literal);
            template = Replace(template, "{zone}", templates.Find(ZonesSection, literal) ?? literal);
            template = Replace(template, "{detail}", templates.Find(DetailsSection, literal) ?? literal);
            template = Replace(template, "{event}", templates.Find(EventsSection, literal) ?? literal);
        }

        return template;
    }

    // ------------------------------------------------------------------ utilidades de formato

    private static string OperatorSuffix(BinaryExpressionType type) => type switch
    {
        BinaryExpressionType.Lesser => "Lt",
        BinaryExpressionType.LesserOrEqual => "Le",
        BinaryExpressionType.Greater => "Gt",
        BinaryExpressionType.GreaterOrEqual => "Ge",
        BinaryExpressionType.Equal => "Eq",
        BinaryExpressionType.NotEqual => "Ne",
        _ => throw new InvalidOperationException($"comparación no describible: {type}"),
    };

    private static string DurationKey(EffectDuration duration) => duration switch
    {
        EffectDuration.Instant => "instant",
        EffectDuration.Play => "play",
        EffectDuration.Match => "match",
        _ => "run",
    };

    private static string LimitScopeKey(LimitScope scope) => scope switch
    {
        LimitScope.Play => "play",
        LimitScope.Match => "match",
        LimitScope.Mob => "mob",
        _ => "run",
    };

    private static string ProbabilityKey(ProbabilityKind kind) => kind switch
    {
        ProbabilityKind.Foul => "foul",
        ProbabilityKind.Card => "card",
        ProbabilityKind.Injury => "injury",
        ProbabilityKind.Injure => "injure",
        ProbabilityKind.SevereInjury => "severeInjury",
        ProbabilityKind.Pass => "pass",
        ProbabilityKind.Intercept => "intercept",
        ProbabilityKind.Dribble => "dribble",
        ProbabilityKind.Tackle => "tackle",
        ProbabilityKind.ShotOnTarget => "shotOnTarget",
        _ => "save",
    };

    private static string Tag(string tag, DescriptionTemplates templates) =>
        tag.Length == 0 ? string.Empty : templates.Get(TagsSection, tag);

    private static string CounterName(string counter, DescriptionTemplates templates) =>
        counter.Length == 0 ? string.Empty : templates.Find(CountersSection, counter) ?? counter;

    /// <summary>Entero con signo explícito: "+3", "-3", "0".</summary>
    private static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);

    /// <summary>
    /// Puntos base 10000 como porcentaje con signo: 300 -> "+3%", 1500 -> "+15%", 350 -> "+3.5%". Punto
    /// decimal invariante para que el texto no dependa de la cultura del proceso (RT-024).
    /// </summary>
    private static string Percent(int value) =>
        (value / 100m).ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture) + "%";

    private static string Replace(string text, string token, string value) =>
        text.Contains(token, StringComparison.Ordinal) ? text.Replace(token, value, StringComparison.Ordinal) : text;
}
