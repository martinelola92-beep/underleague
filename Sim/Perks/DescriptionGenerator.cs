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
    private const string LinksSection = "links";
    private const string ImmunitiesSection = "immunities";
    private const string StartZonesSection = "startZones";
    private const string StartFlanksSection = "startFlanks";
    private const string StatsSection = "stats";

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
        string finalSeparator = templates.Get(Layout, "effectFinalSeparator");

        string triggerNoun = templates.Get(EventsSection, EventTypeNames.ToUpperSnake(perk.Trigger));
        string links = DescribeLinks(perk.Links, templates);
        var effects = new StringBuilder();
        AppendEffects(effects, perk.Effects, templates, separator, finalSeparator, triggerNoun, links);
        if (perk.ElseEffects.Count > 0)
        {
            effects.Append(templates.Get(Layout, "elsePrefix"));
            AppendEffects(effects, perk.ElseEffects, templates, separator, finalSeparator, triggerNoun, links);
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
        text = Replace(text, "{limit}", limit);

        // RF-093 vía 2 y RF-012d: la letalidad es parte del dato del perk, así que va en la descripción
        // generada como todo lo demás (RT-035: no hay texto de efecto escrito a mano). El informe de ojeo
        // la destaca aparte (RF-013, Scouting.LethalPerks), pero quien lea la ficha del perk tiene que ver
        // lo peor que puede pasar sin salir de ella.
        if (perk.Lethal)
        {
            text += templates.Get(Layout, "lethalSuffix");
        }

        return CapitalizeFirst(text);
    }

    /// <summary>
    /// Una sola frase con mayúscula inicial (`docs/estilo-descripciones.md`): las plantillas se escriben en
    /// minúscula porque el disparador puede aparecer en mitad de una frase (por ejemplo tras "; si no, "),
    /// así que la mayúscula la pone el generador sobre el resultado ya compuesto, una vez.
    /// </summary>
    private static string CapitalizeFirst(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

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

    /// <summary>
    /// Compone los efectos visibles de la lista en la frase. <see cref="EffectType.AddCounter"/> se omite
    /// cuando acompaña a otro efecto: es contabilidad interna pura (incrementa el contador que ya narra el
    /// efecto emparejado con "por cada ..."), y decirlo aparte ("+1 al contador X") es exponer una variable
    /// interna (`docs/estilo-descripciones.md`, "nada de implementación"). Si un perk no tiene ningún otro
    /// efecto (un contador aislado, sin escalar nada visible; no ocurre en el catálogo de lanzamiento pero
    /// el motor lo admite), se describe él mismo en vez de dejar la frase vacía. El último efecto visible
    /// se une con <paramref name="finalSeparator"/> ("y"/"and") en vez de con la coma, para que la lista se
    /// lea como una frase y no como un volcado de datos.
    /// </summary>
    /// <summary>
    /// Descripción de una lista de efectos suelta, sin disparador ni condición (RT-035). La usan los
    /// <b>consumibles</b>, que son efectos sin perk que los envuelva: sin esto, el mercado tendría que
    /// escribir su texto a mano, que es justo lo que RT-035 prohíbe.
    /// </summary>
    public static string DescribeEffects(IReadOnlyList<EffectDefinition> effects, DescriptionTemplates templates)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(templates);

        var builder = new StringBuilder();
        AppendEffects(
            builder,
            effects,
            templates,
            templates.Get(Layout, "effectSeparator"),
            templates.Get(Layout, "effectFinalSeparator"),
            templates.Get(EventsSection, EventTypeNames.ToUpperSnake(EventType.MatchStart)),
            string.Empty);

        return CapitalizeFirst(builder.ToString());
    }

    private static void AppendEffects(
        StringBuilder builder,
        IReadOnlyList<EffectDefinition> effects,
        DescriptionTemplates templates,
        string separator,
        string finalSeparator,
        string triggerNoun,
        string links)
    {
        var visible = new List<EffectDefinition>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Type != EffectType.AddCounter)
            {
                visible.Add(effects[i]);
            }
        }

        IReadOnlyList<EffectDefinition> toRender = visible.Count > 0 ? visible : effects;
        for (int i = 0; i < toRender.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(i == toRender.Count - 1 ? finalSeparator : separator);
            }

            builder.Append(DescribeEffect(toRender[i], templates, triggerNoun, links));
        }
    }

    /// <summary>
    /// Nombre de las relaciones que el perk declara (ADR 0021), unidas por la conjunción de la plantilla.
    /// Es lo que convierte "el vinculado" en "el compañero de su columna" o "el de detrás".
    /// </summary>
    private static string DescribeLinks(IReadOnlyList<LinkRelation> links, DescriptionTemplates templates)
    {
        if (links.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        string separator = templates.Get(Layout, "linkSeparator");
        for (int i = 0; i < links.Count; i++)
        {
            if (i > 0)
            {
                text.Append(separator);
            }

            text.Append(templates.Get(LinksSection, ConditionCompiler.LinkNames[(int)links[i]]));
        }

        return text.ToString();
    }

    private static string DescribeEffect(
        EffectDefinition effect, DescriptionTemplates templates, string triggerNoun, string links)
    {
        string key = effect.Type switch
        {
            EffectType.ModifyAttribute when !effect.UsesCounter => "modifyAttribute",
            EffectType.ModifyAttribute => effect.CounterDivisor > 1
                ? "modifyAttributePerCounterDivided"
                : "modifyAttributePerCounter",
            EffectType.ModifyLeash when !effect.UsesCounter => "modifyLeash",
            EffectType.ModifyLeash => effect.CounterDivisor > 1
                ? "modifyLeashPerCounterDivided"
                : "modifyLeashPerCounter",
            EffectType.ModifyBias => "modifyBias",
            // El objetivo vinculado hace que el modificador sea **por par** (ADR 0021) SOLO en el pase, que
            // es la única resolución que enfrenta a dos compañeros; la misma condición que aplica
            // EffectEngine (§16, costura 4). Ahí la descripción tiene que decirlo —"hacia ese
            // compañero"— y en cualquier otro canal el bono es del compañero vinculado y se describe como
            // lo que es: "el compañero de delante suma +25% a su remate". Antes se describían todos como
            // pase y salía la frase sin sentido "probabilidad de tiro a puerta +25% hacia el compañero de
            // delante" (RT-035: la descripción sale del efecto, así que tiene que salir del efecto REAL).
            EffectType.ModifyProbability
                when effect.Target is EffectTarget.Linked or EffectTarget.LinkedWithTag
                    && effect.Probability == ProbabilityKind.Pass
                => "modifyProbabilityPaired",
            EffectType.ModifyProbability when effect.UsesCounter => effect.CounterDivisor > 1
                ? "modifyProbabilityPerCounterDivided"
                : "modifyProbabilityPerCounter",
            EffectType.ModifyProbability => "modifyProbability",
            EffectType.CancelEvent => "cancelEvent",
            EffectType.AddCounter => "addCounter",
            EffectType.SetState => "setState",

            // Los ticks son una unidad interna y no aparecen nunca en pantalla
            // (docs/estilo-descripciones.md): la descripción dice "más tiempo", que es lo que se ve.
            EffectType.ModifyKnockdownTicks => effect.Value >= 0 ? "modifyKnockdownTicks" : "modifyKnockdownTicksDown",
            EffectType.Immunity => "immunity",
            EffectType.ModifyExperience => effect.Value >= 0 ? "modifyExperience" : "modifyExperienceDown",
            _ => throw new InvalidOperationException($"tipo de efecto sin plantilla: {effect.Type}"),
        };

        string text = templates.Get(Effects, key);
        text = Replace(text, "{target}", DescribeTarget(effect, templates, links));
        text = Replace(text, "{immunity}", templates.Get(ImmunitiesSection, ImmunityKey(effect.Immunity)));
        text = Replace(text, "{attribute}", templates.Get(AttributesSection, ConditionCompiler.AttributeName(effect.Attribute)));
        text = Replace(text, "{duration}", templates.Get(Durations, DurationKey(effect.Duration)));
        text = Replace(text, "{probability}", templates.Get(Probabilities, ProbabilityKey(effect.Probability)));
        text = Replace(text, "{counter}", CounterName(effect.Counter, templates));
        text = Replace(text, "{value:+%}", Percent(effect.Value));
        text = Replace(text, "{value:+}", Signed(effect.Value));
        text = Replace(text, "{value:abs}", Math.Abs(effect.Value).ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{value}", effect.Value.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{valuePerCounter:+%}", Percent(effect.ValuePerCounter));
        text = Replace(text, "{valuePerCounter:+}", Signed(effect.ValuePerCounter));
        text = Replace(text, "{maxValue:%}", PlainPercent(effect.MaxValue));
        text = Replace(text, "{maxValue}", effect.MaxValue.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{counterDivisor}", effect.CounterDivisor.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "{ticks}", effect.Ticks.ToString(CultureInfo.InvariantCulture));
        return Replace(text, "{event}", triggerNoun);
    }

    private static string DescribeTarget(EffectDefinition effect, DescriptionTemplates templates, string links)
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
            EffectTarget.Linked => "linked",
            EffectTarget.LinkedWithTag => "linkedWithTag",
            _ => throw new InvalidOperationException($"objetivo sin plantilla: {effect.Target}"),
        };

        string text = Replace(templates.Get(TargetsSection, key), "{tag}", Tag(effect.TargetTag, templates));
        return Replace(text, "{link}", links);
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
                // Argumento entero literal: hoy solo el radio en casillas de nearAlly/nearOpponent.
                if (ConditionCompiler.TryReadInt(arguments[i]) is { } cells)
                {
                    template = Replace(template, "{cells}", cells.ToString(CultureInfo.InvariantCulture));
                }

                continue;
            }

            template = name switch
            {
                "attr" => Replace(template, "{attribute}", templates.Get(AttributesSection, text)),
                "counter" => Replace(template, "{counter}", CounterName(text, templates)),
                "startsIn" => Replace(template, "{startZone}", templates.Get(StartZonesSection, text)),
                "startsOn" => Replace(template, "{startFlank}", templates.Get(StartFlanksSection, text)),
                "linked" => Replace(template, "{link}", templates.Get(LinksSection, text)),
                "stat" => Replace(template, "{stat}", templates.Get(StatsSection, text)),
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

    private static string ImmunityKey(ImmunityKind kind) => kind switch
    {
        ImmunityKind.Push => "push",
        ImmunityKind.Mourning => "mourning",
        _ => "minorInjuryPenalty",
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
        ProbabilityKind.TackleEvasion => "tackleEvasion",
        ProbabilityKind.InterceptEvasion => "interceptEvasion",
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

    /// <summary>Puntos base 10000 como porcentaje sin signo: un tope no es un incremento.</summary>
    private static string PlainPercent(int value) =>
        (Math.Abs(value) / 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string Replace(string text, string token, string value) =>
        text.Contains(token, StringComparison.Ordinal) ? text.Replace(token, value, StringComparison.Ordinal) : text;
}
