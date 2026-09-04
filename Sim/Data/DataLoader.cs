using System.Text.Json;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Data;

/// <summary>Error de carga de datos: fichero y ruta JSON (estilo "$.a.b[2]") donde ocurrió.</summary>
public sealed class DataException : Exception
{
    public string File { get; }

    public string JsonPath { get; }

    public DataException(string file, string jsonPath, string message)
        : base($"{file} {jsonPath}: {message}")
    {
        File = file;
        JsonPath = jsonPath;
    }
}

/// <summary>
/// Carga un <see cref="Catalog"/> a partir del contenido en texto de los ficheros de /data. Sin E/S
/// (recibe el contenido ya leído). System.Text.Json, sin librerías externas. Las claves "_doc" se
/// ignoran a cualquier nivel; las claves desconocidas dentro de "context", de cada sección de
/// "tuning" y de cada rasgo son DataException con fichero y ruta JSON (RT-032).
/// </summary>
public static class DataLoader
{
    /// <summary>files: ruta relativa a /data (p. ej. "races/human.json") -> contenido del fichero.</summary>
    public static Catalog FromJson(IReadOnlyDictionary<string, string> files)
    {
        var races = new List<RaceDefinition>();
        var perkFiles = new List<(string Path, string Content)>();
        var localizationFiles = new List<(string Path, string Content)>();
        string? traitsPath = null;
        string? traitsContent = null;
        string? weightsPath = null;
        string? weightsContent = null;
        string? tuningPath = null;
        string? tuningContent = null;

        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            string content = files[path];
            if (path.StartsWith("races/", StringComparison.Ordinal))
            {
                races.Add(ParseRace(path, content));
            }
            else if (path == "traits/traits.json")
            {
                traitsPath = path;
                traitsContent = content;
            }
            else if (path == "ai/weights.json")
            {
                weightsPath = path;
                weightsContent = content;
            }
            else if (path == "sim/tuning.json")
            {
                tuningPath = path;
                tuningContent = content;
            }
            else if (path.StartsWith("perks/", StringComparison.Ordinal))
            {
                perkFiles.Add((path, content));
            }
            else if (path.StartsWith("l10n/", StringComparison.Ordinal)
                && path.EndsWith("/templates.json", StringComparison.Ordinal))
            {
                localizationFiles.Add((path, content));
            }
        }

        if (traitsContent is null)
        {
            throw new DataException("traits/traits.json", "$", "fichero requerido ausente");
        }

        if (weightsContent is null)
        {
            throw new DataException("ai/weights.json", "$", "fichero requerido ausente");
        }

        if (tuningContent is null)
        {
            throw new DataException("sim/tuning.json", "$", "fichero requerido ausente");
        }

        var traits = ParseTraits(traitsPath!, traitsContent);
        var ai = ParseAiWeights(weightsPath!, weightsContent);
        var tuning = ParseTuning(tuningPath!, tuningContent);

        var localization = new Localization(localizationFiles.Select(f => ParseTemplates(f.Path, f.Content)));
        var perks = new List<PerkDefinition>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, content) in perkFiles)
        {
            var perk = ParsePerk(path, content);
            if (!seenIds.Add(perk.Id))
            {
                throw new DataException(path, "$.id", $"id de perk repetido en el catálogo: '{perk.Id}'");
            }

            // Toda condición del catálogo tiene que ser describible en todos los idiomas cargados
            // (RT-035): un texto que falta es error de carga, nunca una descripción a medias en pantalla.
            DescriptionGenerator.EnsureDescribable(perk, localization, path, "$");
            perks.Add(perk);
        }

        return new Catalog(races, traits, ai, tuning, new PerkCatalog(perks), localization);
    }

    // ---- perks/*.json (RT-033, docs/fase1-diseno.md §2) ----

    private static readonly string[] PerkKnownKeys =
    {
        "id", "name", "rarity", "kind", "trigger", "scope", "condition", "effects", "elseEffects",
        "limit", "accumulatesAcrossMatches", "lethal", "positionOnly", "tagsRequired", "tagsForbidden",
    };

    private static readonly string[] EffectKnownKeys =
    {
        "type", "target", "attribute", "value", "valuePerCounter", "counter", "maxValue",
        "counterDivisor", "probability", "duration", "state", "ticks",
    };

    private static PerkDefinition ParsePerk(string file, string content)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(file, "$", $"JSON inválido: {ex.Message}");
        }

        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys(PerkKnownKeys);

        string id = root.Prop("id").AsString();
        var name = ParseLocalizedName(root.Prop("name"));
        var rarity = ParseEnum<Rarity>(root.Prop("rarity"), "rareza");
        var kind = ParseEnum<PerkKind>(root.Prop("kind"), "tipo de perk");
        var trigger = ParseTrigger(root.Prop("trigger"));
        var scope = root.TryProp("scope") is { } scopeNode
            ? ParseEnum<PerkScope>(scopeNode, "alcance")
            : PerkScope.Actor;

        string conditionSource = root.TryProp("condition") is { } conditionNode ? conditionNode.AsString() : string.Empty;
        var condition = ConditionCompiler.Compile(conditionSource, file, "$.condition");

        var effects = ParseEffects(root.Prop("effects"), file, trigger);
        var elseEffects = root.TryProp("elseEffects") is { } elseNode
            ? ParseEffects(elseNode, file, trigger)
            : Array.Empty<EffectDefinition>();
        if (effects.Count == 0 && elseEffects.Count == 0)
        {
            throw new DataException(file, "$.effects", "un perk debe tener al menos un efecto");
        }

        LimitDefinition? limit = null;
        if (root.TryProp("limit") is { } limitNode)
        {
            limitNode.EnsureKnownKeys("per", "times");
            int times = limitNode.Prop("times").AsInt();
            if (times < 1)
            {
                throw new DataException(file, limitNode.Path + ".times", "el límite debe ser al menos 1");
            }

            limit = new LimitDefinition(ParseEnum<LimitScope>(limitNode.Prop("per"), "ámbito de límite"), times);
        }

        bool accumulates = root.TryProp("accumulatesAcrossMatches") is { } accNode && accNode.AsBool();
        bool lethal = root.TryProp("lethal") is { } lethalNode && lethalNode.AsBool();
        if (lethal)
        {
            // RF-093: en fase 1 no hay muertes, así que ningún efecto puede producir DEATH y un perk
            // letal sería una promesa que el motor no cumple.
            throw new DataException(file, "$.lethal", "en fase 1 no hay muertes: lethal debe ser false");
        }

        Position? positionOnly = null;
        if (root.TryProp("positionOnly") is { } positionNode && !positionNode.IsNull)
        {
            positionOnly = ParseEnum<Position>(positionNode, "posición");
        }

        var tagsRequired = ParseTags(root.TryProp("tagsRequired"));
        var tagsForbidden = ParseTags(root.TryProp("tagsForbidden"));

        return new PerkDefinition(
            id, name, rarity, kind, trigger, scope, conditionSource, condition,
            effects, elseEffects, limit, accumulates, lethal, positionOnly, tagsRequired, tagsForbidden);
    }

    private static IReadOnlyList<string> ParseTags(Json? node) =>
        node is { } tags ? tags.EnumerateArray().Select(j => j.AsString()).ToArray() : Array.Empty<string>();

    private static EventType ParseTrigger(Json node)
    {
        string text = node.AsString();
        foreach (var candidate in Enum.GetValues<EventType>())
        {
            if (string.Equals(EventTypeNames.ToUpperSnake(candidate), text, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new DataException(node.File, node.Path, $"disparador desconocido '{text}'");
    }

    private static T ParseEnum<T>(Json node, string what)
        where T : struct, Enum
    {
        string text = node.AsString();
        string pascal = text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
        if (Enum.TryParse<T>(pascal, out var value) && Enum.IsDefined(value))
        {
            return value;
        }

        throw new DataException(node.File, node.Path, $"{what} desconocido '{text}'");
    }

    private static IReadOnlyList<EffectDefinition> ParseEffects(Json node, string file, EventType trigger)
    {
        var effects = new List<EffectDefinition>();
        foreach (var item in node.EnumerateArray())
        {
            effects.Add(ParseEffect(item, file, trigger));
        }

        return effects;
    }

    private static EffectDefinition ParseEffect(Json node, string file, EventType trigger)
    {
        node.EnsureKnownKeys(EffectKnownKeys);
        var type = ParseEnum<EffectType>(node.Prop("type"), "tipo de efecto");

        var (target, targetTag) = node.TryProp("target") is { } targetNode
            ? ParseTarget(targetNode)
            : (EffectTarget.Owner, string.Empty);

        var duration = node.TryProp("duration") is { } durationNode
            ? ParseEnum<EffectDuration>(durationNode, "duración")
            : EffectDuration.Instant;

        int value = node.TryProp("value") is { } valueNode ? valueNode.AsInt() : 0;
        bool usesCounter = node.TryProp("valuePerCounter") is not null;
        int valuePerCounter = usesCounter ? node.Prop("valuePerCounter").AsInt() : 0;
        string counter = node.TryProp("counter") is { } counterNode ? counterNode.AsString() : string.Empty;
        int maxValue = node.TryProp("maxValue") is { } maxNode ? maxNode.AsInt() : 0;
        int counterDivisor = node.TryProp("counterDivisor") is { } divisorNode ? divisorNode.AsInt() : 1;
        int ticks = node.TryProp("ticks") is { } ticksNode ? ticksNode.AsInt() : 0;

        var attribute = AttributeKind.Strength;
        if (node.TryProp("attribute") is { } attributeNode)
        {
            attribute = ConditionCompiler.Attribute(attributeNode.AsString())
                ?? throw new DataException(file, attributeNode.Path, $"atributo desconocido '{attributeNode.AsString()}'");
        }

        var probability = ProbabilityKind.Foul;
        if (node.TryProp("probability") is { } probabilityNode)
        {
            probability = ParseEnum<ProbabilityKind>(probabilityNode, "probabilidad");
        }

        var state = PlayerState.KnockedDown;
        if (node.TryProp("state") is { } stateNode)
        {
            state = ParseEnum<PlayerState>(stateNode, "estado");
        }

        ValidateEffect(node, file, trigger, type, target, duration, usesCounter, counter, counterDivisor, state);

        return new EffectDefinition(
            type, target, targetTag, attribute, value, usesCounter, valuePerCounter, counter,
            maxValue, counterDivisor, probability, duration, state, ticks);
    }

    private static void ValidateEffect(
        Json node,
        string file,
        EventType trigger,
        EffectType type,
        EffectTarget target,
        EffectDuration duration,
        bool usesCounter,
        string counter,
        int counterDivisor,
        PlayerState state)
    {
        bool instantOnly = type is EffectType.AddCounter or EffectType.ModifyBias or EffectType.SetState or EffectType.CancelEvent;
        if (instantOnly && duration != EffectDuration.Instant)
        {
            throw new DataException(file, node.Path, $"'{type}' solo admite duration 'instant'");
        }

        if (!instantOnly && duration == EffectDuration.Instant)
        {
            throw new DataException(file, node.Path, $"'{type}' necesita una duración ('play', 'match' o 'run')");
        }

        if (type == EffectType.CancelEvent && trigger is not (EventType.Card or EventType.Injury or EventType.Foul))
        {
            throw new DataException(
                file, node.Path, "cancelEvent solo es válido con trigger CARD, INJURY o FOUL");
        }

        if (type == EffectType.SetState)
        {
            if (state != PlayerState.KnockedDown)
            {
                throw new DataException(file, node.Path, "setState solo admite el estado 'KnockedDown'");
            }

            if (target is not (EffectTarget.Target or EffectTarget.Opponent or EffectTarget.OpposingTeam))
            {
                throw new DataException(
                    file, node.Path, "setState solo puede derribar a objetivos rivales (target, opponent, opposingTeam)");
            }
        }

        if (type == EffectType.AddCounter && counter.Length == 0)
        {
            throw new DataException(file, node.Path, "addCounter necesita el nombre del contador");
        }

        if (usesCounter)
        {
            if (type != EffectType.ModifyAttribute)
            {
                throw new DataException(file, node.Path, "valuePerCounter solo es válido en modifyAttribute");
            }

            if (counter.Length == 0)
            {
                throw new DataException(file, node.Path, "valuePerCounter necesita el contador de referencia");
            }

            if (counterDivisor < 1)
            {
                throw new DataException(file, node.Path, "counterDivisor debe ser al menos 1");
            }
        }
    }

    private static (EffectTarget Target, string Tag) ParseTarget(Json node)
    {
        string text = node.AsString();
        int separator = text.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return (ParseEnum<EffectTarget>(node, "objetivo"), string.Empty);
        }

        string prefix = text[..separator];
        string tag = text[(separator + 1)..];
        if (tag.Length == 0)
        {
            throw new DataException(node.File, node.Path, $"objetivo '{text}' sin etiqueta");
        }

        var target = prefix switch
        {
            "withTag" => EffectTarget.WithTag,
            "adjacentWithTag" => EffectTarget.AdjacentWithTag,
            _ => throw new DataException(node.File, node.Path, $"objetivo desconocido '{text}'"),
        };

        return (target, tag);
    }

    // ---- l10n/<lang>/templates.json (RT-035, RT-073) ----

    private static readonly string[] TemplateSections =
    {
        "layout", "effects", "triggers", "events", "conditions", "targets", "durations", "limits",
        "attributes", "probabilities", "tags", "positions", "zones", "details", "counters",
    };

    private static DescriptionTemplates ParseTemplates(string file, string content)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(file, "$", $"JSON inválido: {ex.Message}");
        }

        // "l10n/es/templates.json" -> "es"
        string[] parts = file.Split('/');
        if (parts.Length != 3)
        {
            throw new DataException(file, "$", "las plantillas deben estar en l10n/<lang>/templates.json");
        }

        string language = parts[1];
        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys(TemplateSections);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (section, sectionNode) in root.EnumerateObjectEntries())
        {
            foreach (var (key, value) in sectionNode.EnumerateObjectEntries())
            {
                entries[section + "." + key] = value.AsString();
            }
        }

        return new DescriptionTemplates(language, entries);
    }

    // ---- races/*.json ----

    private static RaceDefinition ParseRace(string file, string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys("id", "name", "tag", "launch", "cellsOccupied", "attributeBias", "individualDeviation", "traitWeights", "names");

        var idNode = root.Prop("id");
        string idText = idNode.AsString();
        if (!Enum.TryParse<Race>(idText, out var raceId))
        {
            throw new DataException(file, idNode.Path, $"raza desconocida '{idText}'");
        }

        var name = ParseLocalizedName(root.Prop("name"));
        string tag = root.Prop("tag").AsString();
        bool launch = root.Prop("launch").AsBool();
        int cellsOccupied = root.Prop("cellsOccupied").AsInt();

        var biasNode = root.Prop("attributeBias");
        biasNode.EnsureKnownKeys("strength", "speed", "technique", "stamina", "leash");
        var attributeBias = new Attributes(
            biasNode.Prop("strength").AsInt(),
            biasNode.Prop("speed").AsInt(),
            biasNode.Prop("technique").AsInt(),
            biasNode.Prop("stamina").AsInt(),
            biasNode.Prop("leash").AsInt());

        int individualDeviation = root.Prop("individualDeviation").AsInt();

        var traitWeights = new List<(Trait Trait, int Weight)>();
        foreach (var (key, value) in root.Prop("traitWeights").EnumerateObjectEntries())
        {
            if (!Enum.TryParse<Trait>(key, out var trait))
            {
                throw new DataException(file, value.Path, $"rasgo desconocido '{key}'");
            }

            traitWeights.Add((trait, value.AsInt()));
        }

        var namesNode = root.Prop("names");
        namesNode.EnsureKnownKeys("first", "last");
        var firstNames = namesNode.Prop("first").EnumerateArray().Select(j => j.AsString()).ToList();
        var lastNames = namesNode.Prop("last").EnumerateArray().Select(j => j.AsString()).ToList();

        return new RaceDefinition(raceId, name, tag, launch, cellsOccupied, attributeBias, individualDeviation, traitWeights, firstNames, lastNames);
    }

    private static LocalizedName ParseLocalizedName(Json node)
    {
        node.EnsureKnownKeys("es", "en");
        return new LocalizedName(node.Prop("es").AsString(), node.Prop("en").AsString());
    }

    // ---- traits/traits.json ----

    private static readonly string[] TraitKnownKeys =
    {
        "name", "actionMultipliers", "hardTackleBonus", "speedBonusPercent", "shotQualityBonus",
        "shootRangeBonusCells", "passQualityBonus", "foulChanceBonus", "injuryChanceBonus",
        "fatigueResistancePercent", "injuryResistanceBonus", "adjacentTeammateBonusPercent",
        "saveBonusClose", "saveBonusFar", "leashBonus",
    };

    private static List<TraitDefinition> ParseTraits(string file, string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys("traits", "goalkeeperTraits");

        var result = new List<TraitDefinition>();
        foreach (var (key, node) in root.Prop("traits").EnumerateObjectEntries())
        {
            result.Add(ParseTraitEntry(file, key, node, goalkeeperOnly: false));
        }

        foreach (var (key, node) in root.Prop("goalkeeperTraits").EnumerateObjectEntries())
        {
            result.Add(ParseTraitEntry(file, key, node, goalkeeperOnly: true));
        }

        return result;
    }

    private static TraitDefinition ParseTraitEntry(string file, string key, Json node, bool goalkeeperOnly)
    {
        node.EnsureKnownKeys(TraitKnownKeys);

        if (!Enum.TryParse<Trait>(key, out var traitId))
        {
            throw new DataException(file, node.Path, $"rasgo desconocido '{key}'");
        }

        var name = ParseLocalizedName(node.Prop("name"));

        var multipliers = new List<(PlayerAction Action, int MultiplierPercent)>();
        var multipliersNode = node.TryProp("actionMultipliers");
        if (multipliersNode is { } mn)
        {
            foreach (var (actionKey, value) in mn.EnumerateObjectEntries())
            {
                if (!Enum.TryParse<PlayerAction>(actionKey, out var action))
                {
                    throw new DataException(file, value.Path, $"acción desconocida '{actionKey}'");
                }

                multipliers.Add((action, value.AsInt()));
            }
        }

        return new TraitDefinition(
            traitId,
            name,
            multipliers,
            HardTackleBonus: OptionalInt(node, "hardTackleBonus"),
            SpeedBonusPercent: OptionalInt(node, "speedBonusPercent"),
            ShotQualityBonus: OptionalInt(node, "shotQualityBonus"),
            ShootRangeBonusCells: OptionalInt(node, "shootRangeBonusCells"),
            PassQualityBonus: OptionalInt(node, "passQualityBonus"),
            FoulChanceBonus: OptionalInt(node, "foulChanceBonus"),
            InjuryChanceBonus: OptionalInt(node, "injuryChanceBonus"),
            FatigueResistancePercent: OptionalInt(node, "fatigueResistancePercent"),
            InjuryResistanceBonus: OptionalInt(node, "injuryResistanceBonus"),
            AdjacentTeammateBonusPercent: OptionalInt(node, "adjacentTeammateBonusPercent"),
            SaveBonusClose: OptionalInt(node, "saveBonusClose"),
            SaveBonusFar: OptionalInt(node, "saveBonusFar"),
            LeashBonus: OptionalInt(node, "leashBonus"),
            GoalkeeperOnly: goalkeeperOnly);
    }

    private static int OptionalInt(Json node, string property) => node.TryProp(property) is { } value ? value.AsInt() : 0;

    // ---- ai/weights.json ----

    private static readonly string[] AiContextKnownKeys =
    {
        "chaseBallDistancePenaltyPerCell", "chaseBallLooseBonus", "chaseBallNotNearestPenalty", "chaseBallIncomingPassBonus",
        "markDistancePenaltyPerCell", "supportAheadBonus", "supportCrowdedPenalty",
        "coverBetweenBallAndGoalBonus", "passOpenReceiverBonus", "passUnderPressureBonus", "passNoReceiverPenalty",
        "dribbleOpenSpaceBonus", "dribbleOpponentAheadPenalty",
        "shootBaseRangeCells", "shootInRangeBonus", "shootOutOfRangePenalty", "shootDistancePenaltyPerCell", "shootAnglePenaltyPerRow",
        "tackleDistanceMaxCells", "tackleOutOfReachPenalty", "tackleBallCarrierBonus",
        "retreatDistanceBonusPerCell", "retreatAtHomePenalty",
    };

    private static AiWeights ParseAiWeights(string file, string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys("base", "tactical", "context", "blockShift");

        int positionCount = Enum.GetValues<Position>().Length;
        int tacticalCount = Enum.GetValues<TacticalState>().Length;
        int actionCount = Enum.GetValues<PlayerAction>().Length;

        var baseTable = new int[positionCount, actionCount];
        var baseSet = new bool[positionCount, actionCount];
        foreach (var (posKey, posNode) in root.Prop("base").EnumerateObjectEntries())
        {
            if (!Enum.TryParse<Position>(posKey, out var position))
            {
                throw new DataException(file, posNode.Path, $"posición desconocida '{posKey}'");
            }

            foreach (var (actionKey, value) in posNode.EnumerateObjectEntries())
            {
                if (!Enum.TryParse<PlayerAction>(actionKey, out var action))
                {
                    throw new DataException(file, value.Path, $"acción desconocida '{actionKey}'");
                }

                baseTable[(int)position, (int)action] = value.AsInt();
                baseSet[(int)position, (int)action] = true;
            }
        }

        EnsureComplete(file, root.Prop("base").Path, baseSet, positionCount, actionCount);

        var tacticalTable = new int[tacticalCount, actionCount];
        var tacticalSet = new bool[tacticalCount, actionCount];
        foreach (var (stateKey, stateNode) in root.Prop("tactical").EnumerateObjectEntries())
        {
            if (!Enum.TryParse<TacticalState>(stateKey, out var state))
            {
                throw new DataException(file, stateNode.Path, $"estado táctico desconocido '{stateKey}'");
            }

            foreach (var (actionKey, value) in stateNode.EnumerateObjectEntries())
            {
                if (!Enum.TryParse<PlayerAction>(actionKey, out var action))
                {
                    throw new DataException(file, value.Path, $"acción desconocida '{actionKey}'");
                }

                tacticalTable[(int)state, (int)action] = value.AsInt();
                tacticalSet[(int)state, (int)action] = true;
            }
        }

        EnsureComplete(file, root.Prop("tactical").Path, tacticalSet, tacticalCount, actionCount);

        var contextNode = root.Prop("context");
        contextNode.EnsureKnownKeys(AiContextKnownKeys);
        var context = new AiContext(
            contextNode.Prop("chaseBallDistancePenaltyPerCell").AsInt(),
            contextNode.Prop("chaseBallLooseBonus").AsInt(),
            contextNode.Prop("chaseBallNotNearestPenalty").AsInt(),
            contextNode.Prop("chaseBallIncomingPassBonus").AsInt(),
            contextNode.Prop("markDistancePenaltyPerCell").AsInt(),
            contextNode.Prop("supportAheadBonus").AsInt(),
            contextNode.Prop("supportCrowdedPenalty").AsInt(),
            contextNode.Prop("coverBetweenBallAndGoalBonus").AsInt(),
            contextNode.Prop("passOpenReceiverBonus").AsInt(),
            contextNode.Prop("passUnderPressureBonus").AsInt(),
            contextNode.Prop("passNoReceiverPenalty").AsInt(),
            contextNode.Prop("dribbleOpenSpaceBonus").AsInt(),
            contextNode.Prop("dribbleOpponentAheadPenalty").AsInt(),
            contextNode.Prop("shootBaseRangeCells").AsInt(),
            contextNode.Prop("shootInRangeBonus").AsInt(),
            contextNode.Prop("shootOutOfRangePenalty").AsInt(),
            contextNode.Prop("shootDistancePenaltyPerCell").AsInt(),
            contextNode.Prop("shootAnglePenaltyPerRow").AsInt(),
            contextNode.Prop("tackleDistanceMaxCells").AsFloat(),
            contextNode.Prop("tackleOutOfReachPenalty").AsInt(),
            contextNode.Prop("tackleBallCarrierBonus").AsInt(),
            contextNode.Prop("retreatDistanceBonusPerCell").AsInt(),
            contextNode.Prop("retreatAtHomePenalty").AsInt());

        var shiftArray = new BlockShift[tacticalCount];
        var shiftSet = new bool[tacticalCount];
        var blockShiftNode = root.Prop("blockShift");
        foreach (var (stateKey, stateNode) in blockShiftNode.EnumerateObjectEntries())
        {
            if (!Enum.TryParse<TacticalState>(stateKey, out var state))
            {
                throw new DataException(file, stateNode.Path, $"estado táctico desconocido '{stateKey}'");
            }

            stateNode.EnsureKnownKeys("shift", "speedTicks");
            shiftArray[(int)state] = new BlockShift(stateNode.Prop("shift").AsFloat(), stateNode.Prop("speedTicks").AsInt());
            shiftSet[(int)state] = true;
        }

        for (int i = 0; i < tacticalCount; i++)
        {
            if (!shiftSet[i])
            {
                throw new DataException(file, blockShiftNode.Path, $"falta blockShift para el estado táctico '{(TacticalState)i}'");
            }
        }

        return new AiWeights(baseTable, tacticalTable, context, shiftArray);
    }

    private static void EnsureComplete(string file, string path, bool[,] set, int dim0, int dim1)
    {
        for (int i = 0; i < dim0; i++)
        {
            for (int j = 0; j < dim1; j++)
            {
                if (!set[i, j])
                {
                    throw new DataException(file, path, $"falta la combinación [{i},{j}]");
                }
            }
        }
    }

    // ---- sim/tuning.json ----

    private static Tuning ParseTuning(string file, string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = new Json(doc.RootElement, file, "$");
        // "ticksPerSecond" y "pitch" se retiraron de aquí, del esquema y de Tuning (ver el comentario de
        // Tuning en Catalog.cs): no tenían ningún consumidor y cablearlas habría sido más invasivo que el
        // resto de este arreglo (revisión independiente, fase 0).
        root.EnsureKnownKeys(
            "regulationTicks", "goldenGoalMaxTicks", "decisionIntervalTicks", "transitionTicks",
            "assistWindowTicks",
            "movement", "ball", "states", "pass", "dribble", "shot", "save", "tackle", "injury", "referee",
            "restart", "generation", "leash", "progression");

        return new Tuning(
            root.Prop("regulationTicks").AsInt(),
            root.Prop("goldenGoalMaxTicks").AsInt(),
            root.Prop("decisionIntervalTicks").AsInt(),
            root.Prop("transitionTicks").AsInt(),
            root.Prop("assistWindowTicks").AsInt(),
            ParseMovement(root.Prop("movement")),
            ParseBall(root.Prop("ball")),
            ParseStates(root.Prop("states")),
            ParsePass(root.Prop("pass")),
            ParseDribble(root.Prop("dribble")),
            ParseShot(root.Prop("shot")),
            ParseSave(root.Prop("save")),
            ParseTackle(root.Prop("tackle")),
            ParseInjury(root.Prop("injury")),
            ParseReferee(root.Prop("referee")),
            ParseRestart(root.Prop("restart")),
            ParseGeneration(root.Prop("generation")),
            ParseLeash(root.Prop("leash")),
            ParseProgression(root.Prop("progression")));
    }

    private static MovementTuning ParseMovement(Json node)
    {
        node.EnsureKnownKeys("baseCellsPerTickMilli", "speedCellsPerTickMilliPer99", "dribbleSpeedPercent", "fatigueStartTick", "fatigueMaxSlowPercent");
        return new MovementTuning(
            node.Prop("baseCellsPerTickMilli").AsInt(),
            node.Prop("speedCellsPerTickMilliPer99").AsInt(),
            node.Prop("dribbleSpeedPercent").AsInt(),
            node.Prop("fatigueStartTick").AsInt(),
            node.Prop("fatigueMaxSlowPercent").AsInt());
    }

    private static BallTuning ParseBall(Json node)
    {
        node.EnsureKnownKeys("passSpeedCellsPerTickMilli", "shotSpeedCellsPerTickMilli", "looseBallFrictionPercent");
        return new BallTuning(
            node.Prop("passSpeedCellsPerTickMilli").AsInt(),
            node.Prop("shotSpeedCellsPerTickMilli").AsInt(),
            node.Prop("looseBallFrictionPercent").AsInt());
    }

    private static StatesTuning ParseStates(Json node)
    {
        node.EnsureKnownKeys("PassingTicks", "ShootingTicks", "TacklingTicks", "KnockedDownTicks", "CelebratingTicks", "DribbleDuelCooldownTicks", "TackleCooldownTicks");
        return new StatesTuning(
            node.Prop("PassingTicks").AsInt(),
            node.Prop("ShootingTicks").AsInt(),
            node.Prop("TacklingTicks").AsInt(),
            node.Prop("KnockedDownTicks").AsInt(),
            node.Prop("CelebratingTicks").AsInt(),
            node.Prop("DribbleDuelCooldownTicks").AsInt(),
            node.Prop("TackleCooldownTicks").AsInt());
    }

    private static PassTuning ParsePass(Json node)
    {
        node.EnsureKnownKeys("baseSuccess", "techniqueFactor", "distancePenaltyPerCell", "pressurePenalty", "interceptRadiusCells", "interceptBaseChance", "interceptTechniqueFactor");
        return new PassTuning(
            node.Prop("baseSuccess").AsInt(),
            node.Prop("techniqueFactor").AsInt(),
            node.Prop("distancePenaltyPerCell").AsInt(),
            node.Prop("pressurePenalty").AsInt(),
            node.Prop("interceptRadiusCells").AsFloat(),
            node.Prop("interceptBaseChance").AsInt(),
            node.Prop("interceptTechniqueFactor").AsInt());
    }

    private static DribbleTuning ParseDribble(Json node)
    {
        node.EnsureKnownKeys("baseWin", "attackerTechniqueFactor", "defenderSpeedFactor", "defenderStrengthFactor", "lostKnockdownTicks");
        return new DribbleTuning(
            node.Prop("baseWin").AsInt(),
            node.Prop("attackerTechniqueFactor").AsInt(),
            node.Prop("defenderSpeedFactor").AsInt(),
            node.Prop("defenderStrengthFactor").AsInt(),
            node.Prop("lostKnockdownTicks").AsInt());
    }

    private static ShotTuning ParseShot(Json node)
    {
        node.EnsureKnownKeys("baseQuality", "techniqueFactor", "strengthFactor", "distancePenaltyPerCell", "pressurePenalty", "offTargetBase", "offTargetDistanceFactor", "penaltyQualityBonus");
        return new ShotTuning(
            node.Prop("baseQuality").AsInt(),
            node.Prop("techniqueFactor").AsInt(),
            node.Prop("strengthFactor").AsInt(),
            node.Prop("distancePenaltyPerCell").AsInt(),
            node.Prop("pressurePenalty").AsInt(),
            node.Prop("offTargetBase").AsInt(),
            node.Prop("offTargetDistanceFactor").AsInt(),
            node.Prop("penaltyQualityBonus").AsInt());
    }

    private static SaveTuning ParseSave(Json node)
    {
        node.EnsureKnownKeys("basePercent", "closeRangeCells", "attributeWeightPercent", "consecutiveShotDecayPercent", "qualityWeight");
        return new SaveTuning(
            node.Prop("basePercent").AsInt(),
            node.Prop("closeRangeCells").AsInt(),
            node.Prop("attributeWeightPercent").AsInt(),
            node.Prop("consecutiveShotDecayPercent").AsInt(),
            node.Prop("qualityWeight").AsInt());
    }

    private static TackleTuning ParseTackle(Json node)
    {
        node.EnsureKnownKeys("baseWin", "strengthFactor", "speedFactor", "carrierTechniqueFactor", "foulBase", "foulStrengthFactor", "hardTackleThreshold", "yellowCardBase", "redCardBase", "hardTackleYellowBonus", "hardTackleRedBonus", "secondYellowIsRed");
        return new TackleTuning(
            node.Prop("baseWin").AsInt(),
            node.Prop("strengthFactor").AsInt(),
            node.Prop("speedFactor").AsInt(),
            node.Prop("carrierTechniqueFactor").AsInt(),
            node.Prop("foulBase").AsInt(),
            node.Prop("foulStrengthFactor").AsInt(),
            node.Prop("hardTackleThreshold").AsInt(),
            node.Prop("yellowCardBase").AsInt(),
            node.Prop("redCardBase").AsInt(),
            node.Prop("hardTackleYellowBonus").AsInt(),
            node.Prop("hardTackleRedBonus").AsInt(),
            node.Prop("secondYellowIsRed").AsBool());
    }

    private static InjuryTuning ParseInjury(Json node)
    {
        node.EnsureKnownKeys("onTackleBase", "onFoulBase", "attackerStrengthFactor", "victimStaminaResistFactor", "severeShare");
        return new InjuryTuning(
            node.Prop("onTackleBase").AsInt(),
            node.Prop("onFoulBase").AsInt(),
            node.Prop("attackerStrengthFactor").AsInt(),
            node.Prop("victimStaminaResistFactor").AsInt(),
            node.Prop("severeShare").AsInt());
    }

    private static RefereeTuning ParseReferee(Json node)
    {
        node.EnsureKnownKeys("biasFoulShiftPer10", "penaltyOnFoulInArea");
        return new RefereeTuning(
            node.Prop("biasFoulShiftPer10").AsInt(),
            node.Prop("penaltyOnFoulInArea").AsInt());
    }

    private static RestartTuning ParseRestart(Json node)
    {
        node.EnsureKnownKeys("throwInTicks", "goalKickTicks", "cornerTicks", "kickoffTicks", "penaltyTicks");
        return new RestartTuning(
            node.Prop("throwInTicks").AsInt(),
            node.Prop("goalKickTicks").AsInt(),
            node.Prop("cornerTicks").AsInt(),
            node.Prop("kickoffTicks").AsInt(),
            node.Prop("penaltyTicks").AsInt());
    }

    private static GenerationTuning ParseGeneration(Json node)
    {
        node.EnsureKnownKeys("positionBias", "leashBase", "traitCountWeights", "goalkeeperTraitChance");

        var biasNode = node.Prop("positionBias");
        biasNode.EnsureKnownKeys("Goalkeeper", "Defender", "Midfielder", "Forward");
        var positionBias = new PositionBiasTable(
            ParsePositionBiasEntry(biasNode.Prop("Goalkeeper")),
            ParsePositionBiasEntry(biasNode.Prop("Defender")),
            ParsePositionBiasEntry(biasNode.Prop("Midfielder")),
            ParsePositionBiasEntry(biasNode.Prop("Forward")));

        var traitCountWeights = node.Prop("traitCountWeights").EnumerateArray().Select(j => j.AsInt()).ToList();

        return new GenerationTuning(positionBias, node.Prop("leashBase").AsInt(), traitCountWeights, node.Prop("goalkeeperTraitChance").AsInt());
    }

    private static Attributes ParsePositionBiasEntry(Json node)
    {
        node.EnsureKnownKeys("strength", "speed", "technique", "stamina", "leash");
        return new Attributes(
            node.Prop("strength").AsInt(),
            node.Prop("speed").AsInt(),
            node.Prop("technique").AsInt(),
            node.Prop("stamina").AsInt(),
            node.Prop("leash").AsInt());
    }

    private static ProgressionTuning ParseProgression(Json node)
    {
        node.EnsureKnownKeys("matchExperience", "benchSharePercent", "experiencePerLevel", "attributesPerLevel");
        var table = node.Prop("experiencePerLevel").EnumerateArray().Select(j => j.AsInt()).ToList();
        if (table.Count != ProgressionRules.MaxLevel)
        {
            throw new DataException(
                node.File,
                node.Path + ".experiencePerLevel",
                $"la tabla de niveles debe tener {ProgressionRules.MaxLevel} entradas y tiene {table.Count}");
        }

        return new ProgressionTuning(
            node.Prop("matchExperience").AsInt(),
            node.Prop("benchSharePercent").AsInt(),
            table,
            node.Prop("attributesPerLevel").AsInt());
    }

    private static LeashTuning ParseLeash(Json node)
    {
        node.EnsureKnownKeys("minCells", "cellsPer99");
        return new LeashTuning(node.Prop("minCells").AsInt(), node.Prop("cellsPer99").AsInt());
    }

    /// <summary>
    /// Cursor de lectura sobre un JsonElement que arrastra el fichero y la ruta JSON recorrida, para
    /// poder lanzar DataException con contexto preciso. "_doc" se ignora en EnumerateObjectEntries.
    /// </summary>
    private readonly struct Json
    {
        private readonly JsonElement _element;
        private readonly string _file;
        private readonly string _path;

        public Json(JsonElement element, string file, string path)
        {
            _element = element;
            _file = file;
            _path = path;
        }

        public string Path => _path;

        /// <summary>Fichero al que pertenece este nodo, para componer DataException fuera de la struct.</summary>
        public string File => _file;

        /// <summary>True si el nodo es el literal JSON null.</summary>
        public bool IsNull => _element.ValueKind == JsonValueKind.Null;

        public Json Prop(string name)
        {
            if (_element.ValueKind != JsonValueKind.Object)
            {
                throw new DataException(_file, _path, $"se esperaba un objeto con la propiedad '{name}'");
            }

            if (!_element.TryGetProperty(name, out var value))
            {
                throw new DataException(_file, _path, $"falta la propiedad requerida '{name}'");
            }

            return new Json(value, _file, _path + "." + name);
        }

        public Json? TryProp(string name)
        {
            if (_element.ValueKind == JsonValueKind.Object && _element.TryGetProperty(name, out var value))
            {
                return new Json(value, _file, _path + "." + name);
            }

            return null;
        }

        public string AsString()
        {
            if (_element.ValueKind != JsonValueKind.String)
            {
                throw new DataException(_file, _path, "se esperaba una cadena");
            }

            return _element.GetString()!;
        }

        public int AsInt()
        {
            if (_element.ValueKind != JsonValueKind.Number || !_element.TryGetInt32(out int value))
            {
                throw new DataException(_file, _path, "se esperaba un entero");
            }

            return value;
        }

        public float AsFloat()
        {
            if (_element.ValueKind != JsonValueKind.Number)
            {
                throw new DataException(_file, _path, "se esperaba un número");
            }

            return _element.GetSingle();
        }

        public bool AsBool()
        {
            if (_element.ValueKind != JsonValueKind.True && _element.ValueKind != JsonValueKind.False)
            {
                throw new DataException(_file, _path, "se esperaba un booleano");
            }

            return _element.GetBoolean();
        }

        public IEnumerable<Json> EnumerateArray()
        {
            if (_element.ValueKind != JsonValueKind.Array)
            {
                throw new DataException(_file, _path, "se esperaba un array");
            }

            int i = 0;
            foreach (var item in _element.EnumerateArray())
            {
                yield return new Json(item, _file, _path + $"[{i}]");
                i++;
            }
        }

        public IEnumerable<(string Name, Json Value)> EnumerateObjectEntries()
        {
            if (_element.ValueKind != JsonValueKind.Object)
            {
                throw new DataException(_file, _path, "se esperaba un objeto");
            }

            foreach (var property in _element.EnumerateObject())
            {
                if (property.Name == "_doc")
                {
                    continue;
                }

                yield return (property.Name, new Json(property.Value, _file, _path + "." + property.Name));
            }
        }

        public void EnsureKnownKeys(params IReadOnlyList<string> known)
        {
            if (_element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in _element.EnumerateObject())
            {
                if (property.Name == "_doc")
                {
                    continue;
                }

                bool isKnown = false;
                for (int i = 0; i < known.Count; i++)
                {
                    if (known[i] == property.Name)
                    {
                        isKnown = true;
                        break;
                    }
                }

                if (!isKnown)
                {
                    throw new DataException(_file, _path, $"clave desconocida '{property.Name}'");
                }
            }
        }
    }
}
