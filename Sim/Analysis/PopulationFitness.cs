using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Analysis;

/// <summary>Papel del portador ante su propio disparador (§22.2: el actor de un INJURY es la VÍCTIMA, no quien entra).</summary>
public enum TriggerActorRole
{
    /// <summary>MATCH_START/PLAY_START/MATCH_END: la condición se evalúa sin que el portador haga nada.</summary>
    NoCarrierAction,

    /// <summary>El portador EJECUTA la acción que emite el evento (tirar, driblar, entrar).</summary>
    PerformsAction,

    /// <summary>El portador SUFRE el suceso (INJURY se emite sobre la víctima, MatchEngine.cs:2568).</summary>
    SuffersAction,

    /// <summary>Sin sitio de emisión leído todavía: no se adivina.</summary>
    Unknown,
}

/// <summary>Veredicto del eje de POBLACIÓN (raza/composición) — ordinal, sin ninguna constante nueva (§22.4).</summary>
public enum PopulationVerdict
{
    /// <summary>La condición no exige ninguna etiqueta de estilo: la raza no debería importar.</summary>
    NoStyleRequired,

    /// <summary>El propio perk fija su raza (<c>perk.Race</c>): el harness no elige.</summary>
    FixedByPerk,

    /// <summary>La raza usada ES la de peso máximo para el estilo exigido.</summary>
    Adequate,

    /// <summary>Existe otra raza donde ese estilo es más común que en la usada.</summary>
    WrongPopulation,

    /// <summary>La condición no se pudo analizar con las funciones NCalc ya mapeadas.</summary>
    Unknown,
}

/// <summary>Veredicto del eje de POSICIÓN (rol del portador frente a la acción del disparador).</summary>
public enum PositionVerdict
{
    /// <summary>El disparador no exige ninguna acción del portador.</summary>
    NoActionRequired,

    /// <summary>El propio perk fija la posición (<c>perk.PositionOnly</c>): el harness no elige.</summary>
    FixedByPerk,

    /// <summary>El rol usado ES el de peso base máximo para la acción exigida.</summary>
    Adequate,

    /// <summary>Otro rol ejecuta esa acción con más peso base que el usado.</summary>
    WrongPosition,

    /// <summary>Disparador sin mapear, o portador que SUFRE el suceso (no hay dato para decir qué rol lo sufre más).</summary>
    Unknown,
}

/// <summary>
/// Motivo reportado de una exposición POR DEBAJO del suelo (§22.3). Los cuatro son NO-PASS: esto explica,
/// nunca decide — ninguna combinación convierte una exposición baja en <c>SCREENING_PASS</c>.
/// </summary>
public enum ExposureDiagnosis
{
    WrongPopulation,
    WrongPosition,
    GenuinelyRare,
    LowExposure,
}

/// <summary>Resultado completo del análisis de adecuación, con los dos ejes por separado (§22.3).</summary>
public sealed record PopulationFitnessResult(
    string PerkId,
    StyleTag? RequiredStyle,
    Race? AffineRace,
    int AffineWeight,
    int WeightInTestedRace,
    TriggerActorRole ActorRole,
    PlayerAction? RequiredAction,
    PlayerAction? EffectAction,
    Position? RequiredRole,
    Position? TestedRole,
    PopulationVerdict PopulationCheck,
    PositionVerdict PositionCheck,
    string Note)
{
    /// <summary>
    /// Proyección a los cuatro motivos de §22.3, para usar SOLO cuando la exposición ya quedó bajo el
    /// suelo. Desempate documentado: el eje de población se comprueba primero porque puede hacer la
    /// condición IMPOSIBLE en esa plantilla (ningún compañero con la etiqueta), mientras que el de
    /// posición solo reduce la FRECUENCIA. Los dos veredictos siguen disponibles por separado en el
    /// propio registro: la proyección no pierde información, solo elige qué contar primero.
    /// </summary>
    public ExposureDiagnosis Diagnose()
    {
        if (PopulationCheck == PopulationVerdict.WrongPopulation)
        {
            return ExposureDiagnosis.WrongPopulation;
        }

        if (PositionCheck == PositionVerdict.WrongPosition)
        {
            return ExposureDiagnosis.WrongPosition;
        }

        if (PopulationCheck == PopulationVerdict.Unknown || PositionCheck == PositionVerdict.Unknown)
        {
            return ExposureDiagnosis.LowExposure;
        }

        return ExposureDiagnosis.GenuinelyRare;
    }
}

/// <summary>
/// Analizador de adecuación de la población experimental (§22, diseño aprobado el 19 sep 2026). PURO: no
/// simula ningún partido, no hace E/S, no llama a ningún modelo. Deriva TODO de fuentes ya validadas —
/// el conjunto cerrado de estilos (<see cref="StyleTag"/>, <c>data/tags/styles.json</c>),
/// <c>RaceDefinition.StyleTagWeights</c> (<c>data/races/*.json</c>), los sitios de emisión reales de cada
/// <see cref="EventType"/> en <c>MatchEngine</c>, y <c>AiWeights.Base(rol, acción)</c>
/// (<c>data/ai/weights.json</c>, la MISMA tabla que usa <c>Utility.Choose</c> para decidir).
///
/// <para><b>Cero constantes nuevas</b>: los dos veredictos son comparaciones ordinales (¿es la raza/el rol
/// el de peso máximo?), no cortes numéricos. <b>Cero decisiones</b>: nada de lo que devuelve esta clase
/// cambia un estado de Screening — solo explica un motivo (§22.5).</para>
/// </summary>
public static class PopulationFitness
{
    /// <summary>Las únicas funciones NCalc de las que se sabe leer una etiqueta (§22.2). Fuera de esta lista: Unknown.</summary>
    private static readonly string[] TagFunctions = { "hasTag", "nearAlly", "nearOpponent", "teammatesWithTag" };

    public static PopulationFitnessResult Analyze(PerkDefinition perk, Catalog catalog, Race testedRace, Position? testedRole)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(catalog);

        var (style, styleKnown) = ExtractRequiredStyle(perk.Condition);
        var (actorRole, action, directRole) = ClassifyTrigger(perk.Trigger);

        // --- Eje de población ---
        Race? affineRace = null;
        int affineWeight = 0;
        int testedWeight = 0;
        PopulationVerdict population;

        if (perk.Race is not null)
        {
            population = PopulationVerdict.FixedByPerk;
        }
        else if (!styleKnown)
        {
            population = PopulationVerdict.Unknown;
        }
        else if (style is not { } requiredStyle)
        {
            population = PopulationVerdict.NoStyleRequired;
        }
        else
        {
            (affineRace, affineWeight) = MostAffineRace(catalog, requiredStyle);
            testedWeight = StyleWeight(catalog, testedRace, requiredStyle);
            population = affineRace == testedRace ? PopulationVerdict.Adequate : PopulationVerdict.WrongPopulation;
        }

        // --- Eje de posición: DOS fuentes independientes (§22.4b) ---
        // (a) el DISPARADOR exige que el portador ejecute una acción -> afecta a la EXPOSICIÓN;
        // (b) el EFECTO modifica algo que solo consume una acción concreta -> afecta al DELTA, no a la
        //     exposición (cannon activa el 100% de los partidos y aun así es inerte en un Defensa).
        var (effectAction, effectDirectRole) = ClassifyEffectConsumer(perk);
        Position? roleFromTrigger = actorRole == TriggerActorRole.PerformsAction
            ? directRole ?? (action is { } triggerAction ? BestRoleFor(catalog, triggerAction) : null)
            : null;
        Position? roleFromEffect = effectDirectRole ?? (effectAction is { } consumed ? BestRoleFor(catalog, consumed) : null);

        Position? requiredRole = null;
        PositionVerdict positionCheck;

        if (perk.PositionOnly is not null)
        {
            positionCheck = PositionVerdict.FixedByPerk;
        }
        else if (roleFromTrigger is not null && roleFromEffect is not null && roleFromTrigger != roleFromEffect)
        {
            // Los dos ejes piden roles distintos: no se elige uno por conveniencia.
            positionCheck = PositionVerdict.Unknown;
        }
        else if ((roleFromTrigger ?? roleFromEffect) is not null)
        {
            requiredRole = roleFromTrigger ?? roleFromEffect;
            positionCheck = testedRole is null
                ? PositionVerdict.Unknown
                : requiredRole == testedRole ? PositionVerdict.Adequate : PositionVerdict.WrongPosition;
        }
        else if (actorRole == TriggerActorRole.NoCarrierAction)
        {
            // Ni el disparador ni el efecto dependen de una acción del portador.
            positionCheck = PositionVerdict.NoActionRequired;
        }
        else
        {
            // SuffersAction (el portador no ELIGE el suceso; base[rol][acción] dice quién la EJECUTA, no
            // quién la sufre) o disparador sin mapear. No se inventa: Unknown.
            positionCheck = PositionVerdict.Unknown;
        }

        return new PopulationFitnessResult(
            perk.Id, style, affineRace, affineWeight, testedWeight, actorRole, action, effectAction, requiredRole, testedRole,
            population, positionCheck, Describe(population, positionCheck, style, affineRace, affineWeight, testedWeight, action, effectAction, requiredRole, testedRole));
    }

    /// <summary>
    /// Estilo exigido por la condición. <c>styleKnown=false</c> significa "no se pudo analizar" (hay un
    /// literal de estilo sin ninguna función conocida alrededor, o hay más de un estilo distinto): en ese
    /// caso NO se devuelve un estilo adivinado.
    /// </summary>
    public static (StyleTag? Style, bool Known) ExtractRequiredStyle(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return (null, true);
        }

        var literals = QuotedLiterals(condition);
        var styles = literals
            .Where(l => Enum.TryParse<StyleTag>(l, ignoreCase: false, out _))
            .Select(l => Enum.Parse<StyleTag>(l, ignoreCase: false))
            .Distinct()
            .ToList();

        if (styles.Count == 0)
        {
            return (null, true); // condición sin etiqueta de estilo (zona, marcador, posición, vínculo...)
        }

        bool hasKnownFunction = TagFunctions.Any(f => condition.Contains(f, StringComparison.Ordinal));
        if (!hasKnownFunction || styles.Count > 1)
        {
            return (null, false); // hay estilo pero no se sabe atribuirlo: Unknown, no una suposición
        }

        return (styles[0], true);
    }

    /// <summary>
    /// Papel del portador y acción exigida por cada disparador, leídos de los sitios de emisión reales del
    /// motor (§22.2). Lo que no está verificado devuelve <see cref="TriggerActorRole.Unknown"/> — añadir
    /// un caso exige leer su sitio de emisión, nunca un comodín.
    /// </summary>
    public static (TriggerActorRole Role, PlayerAction? Action, Position? DirectRole) ClassifyTrigger(EventType trigger) => trigger switch
    {
        EventType.MatchStart or EventType.PlayStart or EventType.MatchEnd => (TriggerActorRole.NoCarrierAction, null, null),

        // MatchEngine.cs:1764/1801 — Shot se emite sobre el tirador.
        EventType.Shot => (TriggerActorRole.PerformsAction, PlayerAction.Shoot, null),

        // MatchEngine.cs:2116 — DribbleAttempted se emite sobre el conductor.
        EventType.DribbleAttempted => (TriggerActorRole.PerformsAction, PlayerAction.Dribble, null),

        // MatchEngine.cs:2172/2203 — Tackle se emite sobre quien entra.
        EventType.Tackle => (TriggerActorRole.PerformsAction, PlayerAction.Tackle, null),

        // MatchEngine.cs:2323 — Foul se emite sobre el TACKLER: es una consecuencia de entrar.
        EventType.Foul => (TriggerActorRole.PerformsAction, PlayerAction.Tackle, null),

        // MatchEngine.cs:2568 — Injury se emite sobre la VÍCTIMA (opponent: tackler). El portador no ejecuta nada.
        EventType.Injury => (TriggerActorRole.SuffersAction, null, null),

        // MatchEngine.cs:2024 — Save se emite sobre el portero: el rol sale del sitio de emisión, no de los pesos.
        EventType.Save => (TriggerActorRole.PerformsAction, null, Position.Goalkeeper),

        _ => (TriggerActorRole.Unknown, null, null),
    };

    /// <summary>
    /// Qué acción CONSUME el efecto principal del perk (§22.4b, segundo eje de posición). Distinto del
    /// disparador: <c>cannon</c> se activa en MATCH_START (exposición 100%) pero su
    /// <c>shootRangeBonusCells</c> solo lo lee la lógica de tiro (<c>Utility.cs:1016/1291</c>), así que en
    /// un Defensa el efecto es inerte sin que la exposición lo delate. Solo se mapea lo verificado; el
    /// resto devuelve <c>null</c> (que se proyecta a Unknown), nunca una suposición.
    /// </summary>
    public static (PlayerAction? Action, Position? DirectRole) ClassifyEffectConsumer(PerkDefinition perk)
    {
        ArgumentNullException.ThrowIfNull(perk);
        if (perk.Effects.Count == 0)
        {
            return (null, null);
        }

        var effect = PerkBalanceClassifier.GetPrimaryEffect(perk);
        return effect.Type switch
        {
            // El efecto NOMBRA la acción: no hay nada que derivar (C1/modifyUtility).
            EffectType.ModifyUtility => (effect.UtilityAction, null),

            // La tirada la hace quien ejecuta la acción correspondiente.
            EffectType.ModifyProbability => effect.Probability switch
            {
                ProbabilityKind.Tackle => (PlayerAction.Tackle, null),
                ProbabilityKind.Dribble => (PlayerAction.Dribble, null),
                ProbabilityKind.ShotOnTarget => (PlayerAction.Shoot, null),
                ProbabilityKind.Save => ((PlayerAction?)null, (Position?)Position.Goalkeeper),

                // Pass/Intercept/*Evasion: el rol de máximo peso base discrepa entre ShortPass/LongPass
                // (Portero) y ThroughPass (Medio), y las "evasion" las sufre el portador, no las ejecuta.
                // Foul/Card/Injury/Injure/SevereInjury: el sujeto de la tirada no es siempre el portador.
                _ => (null, null),
            },

            // Único escalar con sitio de consumo verificado (Utility.cs:1016/1291, lógica de tiro).
            EffectType.ModifyTraitScalar when effect.Scalar == TraitScalarKind.ShootRangeBonusCells => (PlayerAction.Shoot, null),

            _ => (null, null),
        };
    }

    /// <summary>Raza con el peso máximo para ese estilo, sobre las razas REALES del catálogo. Desempate: orden de <see cref="Race"/>.</summary>
    public static (Race Race, int Weight) MostAffineRace(Catalog catalog, StyleTag style)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Race best = default;
        int bestWeight = -1;
        foreach (var race in catalog.Races.OrderBy(r => (int)r.Id))
        {
            int weight = WeightOf(race, style);
            if (weight > bestWeight)
            {
                best = race.Id;
                bestWeight = weight;
            }
        }

        return (best, bestWeight);
    }

    /// <summary>Rol con el peso base máximo para esa acción (<c>data/ai/weights.json</c>). Desempate: orden de <see cref="Position"/>.</summary>
    public static Position BestRoleFor(Catalog catalog, PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var best = Position.Goalkeeper;
        int bestWeight = int.MinValue;
        foreach (var role in Enum.GetValues<Position>())
        {
            int weight = catalog.Ai.Base(role, action);
            if (weight > bestWeight)
            {
                best = role;
                bestWeight = weight;
            }
        }

        return best;
    }

    private static int StyleWeight(Catalog catalog, Race race, StyleTag style) => WeightOf(catalog.Race(race), style);

    private static int WeightOf(RaceDefinition race, StyleTag style)
    {
        foreach (var (candidate, weight) in race.StyleTagWeights)
        {
            if (candidate == style)
            {
                return weight;
            }
        }

        return 0;
    }

    private static List<string> QuotedLiterals(string condition)
    {
        var literals = new List<string>();
        int i = 0;
        while (i < condition.Length)
        {
            int open = condition.IndexOf('\'', i);
            if (open < 0)
            {
                break;
            }

            int close = condition.IndexOf('\'', open + 1);
            if (close < 0)
            {
                break;
            }

            literals.Add(condition[(open + 1)..close].Trim());
            i = close + 1;
        }

        return literals;
    }

    private static string Describe(
        PopulationVerdict population, PositionVerdict position, StyleTag? style, Race? affine, int affineWeight,
        int testedWeight, PlayerAction? action, PlayerAction? effectAction, Position? requiredRole, Position? testedRole)
    {
        string source = (action, effectAction) switch
        {
            (not null, not null) => $"disparador ({action}) y efecto ({effectAction})",
            (not null, _) => $"disparador ({action})",
            (_, not null) => $"efecto ({effectAction}; la exposición no lo delata)",
            _ => "ninguno",
        };

        string populationPart = population switch
        {
            PopulationVerdict.WrongPopulation => $"estilo {style} exigido: peso {testedWeight} en la raza medida frente a {affineWeight} en {affine}",
            PopulationVerdict.Adequate => $"estilo {style}: ya se mide con la raza de peso máximo ({affineWeight})",
            PopulationVerdict.NoStyleRequired => "la condición no exige ninguna etiqueta de estilo",
            PopulationVerdict.FixedByPerk => "el perk fija su propia raza",
            _ => "condición no analizable con las funciones ya mapeadas",
        };

        string positionPart = position switch
        {
            PositionVerdict.WrongPosition => $"acción exigida por {source}: la ejecuta sobre todo {requiredRole}, se mide sobre {testedRole}",
            PositionVerdict.Adequate => $"acción exigida por {source}: ya se mide sobre el rol de peso base máximo ({requiredRole})",
            PositionVerdict.NoActionRequired => "ni el disparador ni el efecto dependen de una acción del portador",
            PositionVerdict.FixedByPerk => "el perk fija su propia posición",
            _ => "disparador sin mapear, o el portador sufre el suceso en vez de ejecutarlo",
        };

        return $"población: {populationPart}. posición: {positionPart}.";
    }
}
