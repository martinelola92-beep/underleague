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
        string? stylesPath = null;
        string? stylesContent = null;

        foreach (var path in files.Keys.OrderBy(p => p, StringComparer.Ordinal))
        {
            string content = files[path];
            if (path.StartsWith("races/", StringComparison.Ordinal))
            {
                races.Add(ParseRace(path, content));
            }
            else if (path == "tags/styles.json")
            {
                stylesPath = path;
                stylesContent = content;
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

        if (stylesContent is null)
        {
            throw new DataException("tags/styles.json", "$", "fichero requerido ausente");
        }

        var traits = ParseTraits(traitsPath!, traitsContent);
        var ai = ParseAiWeights(weightsPath!, weightsContent);
        var tuning = ParseTuning(tuningPath!, tuningContent);
        var styles = ParseStyles(stylesPath!, stylesContent);

        var localization = new Localization(localizationFiles.Select(f => ParseTemplates(f.Path, f.Content)));
        var perks = new List<PerkDefinition>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, content) in perkFiles)
        {
            var perk = PerkLoader.Parse(path, content, tuning.Probability);
            if (!seenIds.Add(perk.Id))
            {
                throw new DataException(path, "$.id", $"id de perk repetido en el catálogo: '{perk.Id}'");
            }

            // Toda condición del catálogo tiene que ser describible en todos los idiomas cargados
            // (RT-035): un texto que falta es error de carga, nunca una descripción a medias en pantalla.
            DescriptionGenerator.EnsureDescribable(perk, localization, path, "$");
            perks.Add(perk);
        }

        return new Catalog(races, styles, traits, ai, tuning, new PerkCatalog(perks), localization);
    }

    // ---- perks/*.json ----

    // El formato de perk (fase1b-diseno.md §1.4) y su validación viven en Sim.Perks.PerkLoader: es el
    // mismo paquete que el motor de efectos, las condiciones y el generador de descripciones, así que un
    // tipo de efecto, un objetivo o una función nuevos se añaden en un solo sitio (paquete S).

    // ---- l10n/<lang>/templates.json (RT-035, RT-073) ----

    private static readonly string[] TemplateSections =
    {
        "layout", "effects", "triggers", "events", "conditions", "targets", "durations", "limits",
        "attributes", "probabilities", "tags", "positions", "zones", "details", "counters",

        // Secciones del rediseño espacial (paquete S): relaciones de vínculo (ADR 0021), inmunidades y
        // estadísticas de las funciones de condición nuevas (fase1b-diseno.md §1.5).
        "links", "immunities", "startZones", "startFlanks", "stats",
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

    private static readonly string[] RaceKnownKeys =
    {
        "id", "name", "speciesTag", "styleTagWeights", "launch", "cellsOccupied", "bodyRadius", "discipline",
        "attributeBias", "ability", "description", "individualDeviation", "traitWeights", "names",
    };

    private static RaceDefinition ParseRace(string file, string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys(RaceKnownKeys);

        var idNode = root.Prop("id");
        string idText = idNode.AsString();
        if (!Enum.TryParse<Race>(idText, out var raceId))
        {
            throw new DataException(file, idNode.Path, $"raza desconocida '{idText}'");
        }

        var name = ParseLocalizedName(root.Prop("name"));

        var speciesTagNode = root.Prop("speciesTag");
        string speciesTag = speciesTagNode.AsString();
        if (!Enum.TryParse<Race>(speciesTag, out _))
        {
            throw new DataException(file, speciesTagNode.Path, $"especie desconocida '{speciesTag}'");
        }

        if (!string.Equals(speciesTag, idText, StringComparison.Ordinal))
        {
            // ADR 0024: speciesTag es la etiqueta fija de especie, la misma raza que "id". Un desajuste
            // solo puede ser un error de copia/pega en el dato (RT-032: error explícito, nunca silencioso).
            throw new DataException(file, speciesTagNode.Path, $"speciesTag '{speciesTag}' no coincide con id '{idText}'");
        }

        var styleTagWeights = ParseStyleTagWeights(file, root.Prop("styleTagWeights"));

        bool launch = root.Prop("launch").AsBool();
        int cellsOccupied = root.Prop("cellsOccupied").AsInt();
        int bodyRadius = root.Prop("bodyRadius").AsInt();
        int discipline = root.Prop("discipline").AsInt();

        var biasNode = root.Prop("attributeBias");
        biasNode.EnsureKnownKeys("strength", "speed", "technique", "stamina", "leash");
        var attributeBias = new Attributes(
            biasNode.Prop("strength").AsInt(),
            biasNode.Prop("speed").AsInt(),
            biasNode.Prop("technique").AsInt(),
            biasNode.Prop("stamina").AsInt(),
            biasNode.Prop("leash").AsInt());

        string ability = root.Prop("ability").AsString();
        var description = ParseLocalizedName(root.Prop("description"));

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

        return new RaceDefinition(
            raceId, name, speciesTag, styleTagWeights, launch, cellsOccupied, bodyRadius, discipline,
            attributeBias, ability, description, individualDeviation, traitWeights, firstNames, lastNames);
    }

    /// <summary>
    /// data/races/&lt;id&gt;.json.styleTagWeights (fase1b-diseno.md §1.1, ADR 0024): pesos enteros que
    /// deben sumar 100. La suma se comprueba aquí (dato inválido = error explícito, RT-032); la regla de
    /// diseño de que la dominante esté entre 60 y 85 y de que haya al menos una etiqueta opuesta a la
    /// identidad de la raza no es mecánicamente verificable y queda en manos de quien autora el dato.
    /// </summary>
    private static IReadOnlyList<(StyleTag Style, int Weight)> ParseStyleTagWeights(string file, Json node)
    {
        var weights = new List<(StyleTag Style, int Weight)>();
        int total = 0;
        foreach (var (key, value) in node.EnumerateObjectEntries())
        {
            if (!Enum.TryParse<StyleTag>(key, out var style))
            {
                throw new DataException(file, value.Path, $"etiqueta de estilo desconocida '{key}'");
            }

            int weight = value.AsInt();
            weights.Add((style, weight));
            total += weight;
        }

        if (weights.Count == 0)
        {
            throw new DataException(file, node.Path, "styleTagWeights no puede estar vacío");
        }

        if (total != 100)
        {
            throw new DataException(file, node.Path, $"styleTagWeights debe sumar 100 y suma {total}");
        }

        return weights;
    }

    private static LocalizedName ParseLocalizedName(Json node)
    {
        node.EnsureKnownKeys("es", "en");
        return new LocalizedName(node.Prop("es").AsString(), node.Prop("en").AsString());
    }

    // ---- tags/styles.json ----

    private static readonly string[] StyleTagNames = Enum.GetNames<StyleTag>();

    private static List<StyleDefinition> ParseStyles(string file, string content)
    {
        var doc = JsonDocument.Parse(content);
        var root = new Json(doc.RootElement, file, "$");
        root.EnsureKnownKeys(StyleTagNames);

        var result = new List<StyleDefinition>();
        var seen = new HashSet<StyleTag>();
        foreach (var (key, node) in root.EnumerateObjectEntries())
        {
            if (!Enum.TryParse<StyleTag>(key, out var style))
            {
                throw new DataException(file, node.Path, $"etiqueta de estilo desconocida '{key}'");
            }

            node.EnsureKnownKeys("name", "description", "attributeBias");
            var name = ParseLocalizedName(node.Prop("name"));
            var description = ParseLocalizedName(node.Prop("description"));

            var biasNode = node.Prop("attributeBias");
            biasNode.EnsureKnownKeys("strength", "speed", "technique", "stamina", "leash");
            var attributeBias = new Attributes(
                biasNode.Prop("strength").AsInt(),
                biasNode.Prop("speed").AsInt(),
                biasNode.Prop("technique").AsInt(),
                biasNode.Prop("stamina").AsInt(),
                biasNode.Prop("leash").AsInt());

            seen.Add(style);
            result.Add(new StyleDefinition(style, name, description, attributeBias));
        }

        foreach (var styleName in StyleTagNames)
        {
            if (!seen.Contains(Enum.Parse<StyleTag>(styleName)))
            {
                throw new DataException(file, root.Path, $"falta la etiqueta de estilo '{styleName}'");
            }
        }

        return result;
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
        "shootBaseRangeCells", "shootInRangeBonus", "shootBeyondRangePenaltyPerCell", "shootDistancePenaltyPerCell", "shootAnglePenaltyPerRow",
        "tackleDistanceMaxCells", "tackleOutOfReachPenalty", "tackleBallCarrierBonus",
        "retreatDistanceBonusPerCell", "retreatAtHomePenalty",
        "findSpaceOpponentDistanceBonusPerCell", "findSpaceAdvanceBonusPerCell", "findSpaceOpenLaneBonus",
        "pressCarrierBonus", "pressDistancePenaltyPerCell", "pressGoalkeeperExitBonus",
        "shortPassMaxCells", "longPassMaxCells", "shortPassTechniqueSlope", "longPassTechniqueSlope",
        "dribbleTechniqueSlope", "dribbleSpeedSlope", "shootTechniqueSlope", "shootStrengthSlope",
        "blockActiveRadiusCells", "blockCorridorHalfWidthCells", "blockReachMaxCells",
        "blockTargetBonus", "blockDistancePenaltyPerCell", "blockAggressiveBonus", "blockBruteTagBonus",
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
            contextNode.Prop("shootBeyondRangePenaltyPerCell").AsInt(),
            contextNode.Prop("shootDistancePenaltyPerCell").AsInt(),
            contextNode.Prop("shootAnglePenaltyPerRow").AsInt(),
            contextNode.Prop("tackleDistanceMaxCells").AsFloat(),
            contextNode.Prop("tackleOutOfReachPenalty").AsInt(),
            contextNode.Prop("tackleBallCarrierBonus").AsInt(),
            contextNode.Prop("retreatDistanceBonusPerCell").AsInt(),
            contextNode.Prop("retreatAtHomePenalty").AsInt(),
            FindSpaceOpponentDistanceBonusPerCell: contextNode.Prop("findSpaceOpponentDistanceBonusPerCell").AsInt(),
            FindSpaceAdvanceBonusPerCell: contextNode.Prop("findSpaceAdvanceBonusPerCell").AsInt(),
            FindSpaceOpenLaneBonus: contextNode.Prop("findSpaceOpenLaneBonus").AsInt(),
            PressCarrierBonus: contextNode.Prop("pressCarrierBonus").AsInt(),
            PressDistancePenaltyPerCell: contextNode.Prop("pressDistancePenaltyPerCell").AsInt(),
            PressGoalkeeperExitBonus: contextNode.Prop("pressGoalkeeperExitBonus").AsInt(),
            ShortPassMaxCells: contextNode.Prop("shortPassMaxCells").AsFloat(),
            LongPassMaxCells: contextNode.Prop("longPassMaxCells").AsFloat(),
            ShortPassTechniqueSlope: contextNode.Prop("shortPassTechniqueSlope").AsInt(),
            LongPassTechniqueSlope: contextNode.Prop("longPassTechniqueSlope").AsInt(),
            DribbleTechniqueSlope: contextNode.Prop("dribbleTechniqueSlope").AsInt(),
            DribbleSpeedSlope: contextNode.Prop("dribbleSpeedSlope").AsInt(),
            ShootTechniqueSlope: contextNode.Prop("shootTechniqueSlope").AsInt(),
            ShootStrengthSlope: contextNode.Prop("shootStrengthSlope").AsInt(),
            BlockActiveRadiusCells: contextNode.Prop("blockActiveRadiusCells").AsFloat(),
            BlockCorridorHalfWidthCells: contextNode.Prop("blockCorridorHalfWidthCells").AsFloat(),
            BlockReachMaxCells: contextNode.Prop("blockReachMaxCells").AsFloat(),
            BlockTargetBonus: contextNode.Prop("blockTargetBonus").AsInt(),
            BlockDistancePenaltyPerCell: contextNode.Prop("blockDistancePenaltyPerCell").AsInt(),
            BlockAggressiveBonus: contextNode.Prop("blockAggressiveBonus").AsInt(),
            BlockBruteTagBonus: contextNode.Prop("blockBruteTagBonus").AsInt());

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
            "block", "restart", "generation", "bodies", "actionZone", "progression",
            "probabilityChannels");

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
            ParseBlock(root.Prop("block")),
            ParseRestart(root.Prop("restart")),
            ParseGeneration(root.Prop("generation")),
            ParseBodies(root.Prop("bodies")),
            ParseActionZone(root.Prop("actionZone")),
            ParseProgression(root.Prop("progression")),
            ParseProbabilityChannels(root.Prop("probabilityChannels")));
    }

    /// <summary>
    /// tuning.probabilityChannels: el escalón de cada canal de probabilidad (ADR 0035). Están los trece
    /// de <see cref="ProbabilityKind"/> y ninguno más: un canal sin escalón sería un canal sin escala, y
    /// un canal de más sería una errata que pasaría desapercibida.
    /// </summary>
    private static ProbabilityScale ParseProbabilityChannels(Json node)
    {
        var kinds = Enum.GetValues<ProbabilityKind>();
        var names = new string[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            names[i] = ProbabilityScale.Name(kinds[i]);
        }

        node.EnsureKnownKeys(names);
        var steps = new int[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            var channel = node.Prop(names[i]);
            channel.EnsureKnownKeys("step");
            steps[i] = channel.Prop("step").AsInt();
        }

        return new ProbabilityScale(steps);
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
        node.EnsureKnownKeys("baseWin", "attackerTechniqueFactor", "defenderSpeedSharePercent", "lostKnockdownTicks");
        return new DribbleTuning(
            node.Prop("baseWin").AsInt(),
            node.Prop("attackerTechniqueFactor").AsInt(),
            node.Prop("defenderSpeedSharePercent").AsInt(),
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
        node.EnsureKnownKeys("basePercent", "closeRangeCells", "attributeWeightPercent", "consecutiveShotDecayPercent", "qualityWeight", "qualityPivot");
        return new SaveTuning(
            node.Prop("basePercent").AsInt(),
            node.Prop("closeRangeCells").AsInt(),
            node.Prop("attributeWeightPercent").AsInt(),
            node.Prop("consecutiveShotDecayPercent").AsInt(),
            node.Prop("qualityWeight").AsInt(),
            node.Prop("qualityPivot").AsInt());
    }

    private static TackleTuning ParseTackle(Json node)
    {
        node.EnsureKnownKeys("baseWin", "pressureFactor", "strengthSharePercent", "foulBase", "foulStrengthFactor", "hardTackleThreshold", "yellowCardBase", "redCardBase", "hardTackleYellowBonus", "hardTackleRedBonus", "secondYellowIsRed");
        return new TackleTuning(
            node.Prop("baseWin").AsInt(),
            node.Prop("pressureFactor").AsInt(),
            node.Prop("strengthSharePercent").AsInt(),
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
        node.EnsureKnownKeys(
            "onTackleBase", "onFoulBase", "relativeFactor", "severeShare", "actScalePercent", "eliteScalePercent");

        // ADR 0043: un multiplicador por acto, en datos, sobre la probabilidad ya calculada.
        var actScale = new List<int>(3);
        foreach (var item in node.Prop("actScalePercent").EnumerateArray())
        {
            actScale.Add(item.AsInt());
        }

        if (actScale.Count != 3)
        {
            throw new DataException(node.File, node.Path + ".actScalePercent", "debe tener exactamente 3 valores, uno por acto (RF-001)");
        }

        return new InjuryTuning(
            node.Prop("onTackleBase").AsInt(),
            node.Prop("onFoulBase").AsInt(),
            node.Prop("relativeFactor").AsInt(),
            node.Prop("severeShare").AsInt(),
            actScale,
            node.Prop("eliteScalePercent").AsInt());
    }

    private static RefereeTuning ParseReferee(Json node)
    {
        node.EnsureKnownKeys(
            "biasFoulShiftPer10", "penaltyOnFoulInArea", "biasCardShiftPer10", "biasPenaltyShiftPer10",
            "biasShiftFoulSeen", "biasShiftFoulUnseen", "biasShiftHardExtra", "biasShiftBlockExtra",
            "biasShiftInjuryExtra", "biasShiftYellowExtra", "biasShiftRedExtra");
        return new RefereeTuning(
            node.Prop("biasFoulShiftPer10").AsInt(),
            node.Prop("penaltyOnFoulInArea").AsInt(),
            node.Prop("biasCardShiftPer10").AsInt(),
            node.Prop("biasPenaltyShiftPer10").AsInt(),
            node.Prop("biasShiftFoulSeen").AsInt(),
            node.Prop("biasShiftFoulUnseen").AsInt(),
            node.Prop("biasShiftHardExtra").AsInt(),
            node.Prop("biasShiftBlockExtra").AsInt(),
            node.Prop("biasShiftInjuryExtra").AsInt(),
            node.Prop("biasShiftYellowExtra").AsInt(),
            node.Prop("biasShiftRedExtra").AsInt());
    }

    /// <summary>tuning.block: resolución del bloqueo sin balón (ADR 0030 §2).</summary>
    private static BlockTuning ParseBlock(Json node)
    {
        node.EnsureKnownKeys(
            "blockingTicks", "cooldownTicks", "baseWin", "strengthFactor", "speedFactor",
            "knockdownTicks", "foulBase");
        return new BlockTuning(
            node.Prop("blockingTicks").AsInt(),
            node.Prop("cooldownTicks").AsInt(),
            node.Prop("baseWin").AsInt(),
            node.Prop("strengthFactor").AsInt(),
            node.Prop("speedFactor").AsInt(),
            node.Prop("knockdownTicks").AsInt(),
            node.Prop("foulBase").AsInt());
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

    /// <summary>tuning.generation (fase1b-diseno.md §1.3, ADR 0025, ADR 0027): modelo de presupuesto de atributos.</summary>
    private static GenerationTuning ParseGeneration(Json node)
    {
        node.EnsureKnownKeys(
            "budgetByRarity", "budgetPerLevel", "attributeFloor", "attributeCap", "rangeByRarity",
            "positionShare", "positionFloors", "traitCountWeights", "goalkeeperTraitChance");

        var budgetNode = node.Prop("budgetByRarity");
        budgetNode.EnsureKnownKeys("common", "uncommon", "rare", "legendary");
        var budgetByRarity = new RarityBudgetTable(
            budgetNode.Prop("common").AsInt(),
            budgetNode.Prop("uncommon").AsInt(),
            budgetNode.Prop("rare").AsInt(),
            budgetNode.Prop("legendary").AsInt());

        int budgetPerLevel = node.Prop("budgetPerLevel").AsInt();
        int attributeFloor = node.Prop("attributeFloor").AsInt();
        int attributeCap = node.Prop("attributeCap").AsInt();

        var rangeNode = node.Prop("rangeByRarity");
        rangeNode.EnsureKnownKeys("common", "uncommon", "rare", "legendary");
        var rangeByRarity = new RarityRangeTable(
            ParseAttributeRange(rangeNode.Prop("common")),
            ParseAttributeRange(rangeNode.Prop("uncommon")),
            ParseAttributeRange(rangeNode.Prop("rare")),
            ParseAttributeRange(rangeNode.Prop("legendary")));

        var shareNode = node.Prop("positionShare");
        shareNode.EnsureKnownKeys("Goalkeeper", "Defender", "Midfielder", "Forward");
        var positionShare = new PositionShareTable(
            ParseAttributeShareSummingTo100(shareNode.Prop("Goalkeeper")),
            ParseAttributeShareSummingTo100(shareNode.Prop("Defender")),
            ParseAttributeShareSummingTo100(shareNode.Prop("Midfielder")),
            ParseAttributeShareSummingTo100(shareNode.Prop("Forward")));

        var floorsNode = node.Prop("positionFloors");
        floorsNode.EnsureKnownKeys("Goalkeeper", "Defender", "Midfielder", "Forward");
        var positionFloors = new PositionFloorTable(
            ParsePositionFloorEntry(floorsNode.Prop("Goalkeeper")),
            ParsePositionFloorEntry(floorsNode.Prop("Defender")),
            ParsePositionFloorEntry(floorsNode.Prop("Midfielder")),
            ParsePositionFloorEntry(floorsNode.Prop("Forward")));

        var traitCountWeights = node.Prop("traitCountWeights").EnumerateArray().Select(j => j.AsInt()).ToList();

        return new GenerationTuning(
            budgetByRarity, budgetPerLevel, attributeFloor, attributeCap, rangeByRarity, positionShare,
            positionFloors, traitCountWeights, node.Prop("goalkeeperTraitChance").AsInt());
    }

    private static AttributeRange ParseAttributeRange(Json node)
    {
        node.EnsureKnownKeys("min", "max");
        return new AttributeRange(node.Prop("min").AsInt(), node.Prop("max").AsInt());
    }

    /// <summary>tuning.generation.positionShare.&lt;position&gt;: los cinco porcentajes deben sumar 100 (RT-032).</summary>
    private static AttributeShare ParseAttributeShareSummingTo100(Json node)
    {
        node.EnsureKnownKeys("strength", "speed", "technique", "stamina", "leash");
        var share = new AttributeShare(
            node.Prop("strength").AsInt(),
            node.Prop("speed").AsInt(),
            node.Prop("technique").AsInt(),
            node.Prop("stamina").AsInt(),
            node.Prop("leash").AsInt());

        int total = share.Strength + share.Speed + share.Technique + share.Stamina + share.Leash;
        if (total != 100)
        {
            throw new DataException(node.File, node.Path, $"positionShare debe sumar 100 y suma {total}");
        }

        return share;
    }

    private static readonly string[] AttributeFloorKeys = { "strength", "speed", "technique", "stamina", "leash" };

    private static IReadOnlyDictionary<AttributeKind, int> ParsePositionFloorEntry(Json node)
    {
        node.EnsureKnownKeys(AttributeFloorKeys);
        var floors = new Dictionary<AttributeKind, int>();
        foreach (var (key, value) in node.EnumerateObjectEntries())
        {
            var attribute = key switch
            {
                "strength" => AttributeKind.Strength,
                "speed" => AttributeKind.Speed,
                "technique" => AttributeKind.Technique,
                "stamina" => AttributeKind.Stamina,
                "leash" => AttributeKind.Leash,
                _ => throw new DataException(value.File, value.Path, $"atributo desconocido '{key}'"),
            };
            floors[attribute] = value.AsInt();
        }

        return floors;
    }

    /// <summary>tuning.bodies (fase1b-diseno.md §1.3, ADR 0020). Sin consumidor todavía en /Sim (paquete R).</summary>
    private static BodiesTuning ParseBodies(Json node)
    {
        node.EnsureKnownKeys("separationEnabled", "maxPushPerTickMilli", "massStrengthWeight", "massRadiusWeight", "tacklePushMultiplier");
        return new BodiesTuning(
            node.Prop("separationEnabled").AsBool(),
            node.Prop("maxPushPerTickMilli").AsInt(),
            node.Prop("massStrengthWeight").AsInt(),
            node.Prop("massRadiusWeight").AsInt(),
            node.Prop("tacklePushMultiplier").AsInt());
    }

    /// <summary>tuning.actionZone (fase1b-diseno.md §1.3, ADR 0028). Sin consumidor todavía en /Sim (paquete R).</summary>
    private static ActionZoneTuning ParseActionZone(Json node)
    {
        node.EnsureKnownKeys(
            "shape", "scaleFromLeashPercent", "outerLimitMultiplier", "outsidePenaltyPerCell",
            "disciplineWeightPercent", "retreatBonusOutsidePerCell");

        var shapeNode = node.Prop("shape");
        shapeNode.EnsureKnownKeys("Goalkeeper", "Defender", "Midfielder", "Forward");
        var shape = new ActionZoneShapeTable(
            ParseZoneShape(shapeNode.Prop("Goalkeeper")),
            ParseZoneShape(shapeNode.Prop("Defender")),
            ParseZoneShape(shapeNode.Prop("Midfielder")),
            ParseZoneShape(shapeNode.Prop("Forward")));

        var scaleNode = node.Prop("scaleFromLeashPercent");
        scaleNode.EnsureKnownKeys("at1", "at99");
        var scale = new LeashScalePercent(scaleNode.Prop("at1").AsInt(), scaleNode.Prop("at99").AsInt());

        return new ActionZoneTuning(
            shape,
            scale,
            node.Prop("outerLimitMultiplier").AsInt(),
            node.Prop("outsidePenaltyPerCell").AsInt(),
            node.Prop("disciplineWeightPercent").AsInt(),
            node.Prop("retreatBonusOutsidePerCell").AsInt());
    }

    private static ZoneShape ParseZoneShape(Json node)
    {
        node.EnsureKnownKeys("forward", "back", "sides");
        return new ZoneShape(node.Prop("forward").AsInt(), node.Prop("back").AsInt(), node.Prop("sides").AsInt());
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
