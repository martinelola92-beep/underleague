using System.Text.Json;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

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

        return new Catalog(races, traits, ai, tuning);
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
        "chaseBallDistancePenaltyPerCell", "chaseBallLooseBonus", "chaseBallNotNearestPenalty",
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
        root.EnsureKnownKeys(
            "ticksPerSecond", "regulationTicks", "goldenGoalMaxTicks", "decisionIntervalTicks", "transitionTicks",
            "movement", "ball", "states", "pass", "dribble", "shot", "save", "tackle", "injury", "referee",
            "restart", "pitch", "generation", "leash");

        return new Tuning(
            root.Prop("ticksPerSecond").AsInt(),
            root.Prop("regulationTicks").AsInt(),
            root.Prop("goldenGoalMaxTicks").AsInt(),
            root.Prop("decisionIntervalTicks").AsInt(),
            root.Prop("transitionTicks").AsInt(),
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
            ParsePitch(root.Prop("pitch")),
            ParseGeneration(root.Prop("generation")),
            ParseLeash(root.Prop("leash")));
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
        node.EnsureKnownKeys("PassingTicks", "ShootingTicks", "TacklingTicks", "KnockedDownTicks", "CelebratingTicks", "DribbleDuelCooldownTicks");
        return new StatesTuning(
            node.Prop("PassingTicks").AsInt(),
            node.Prop("ShootingTicks").AsInt(),
            node.Prop("TacklingTicks").AsInt(),
            node.Prop("KnockedDownTicks").AsInt(),
            node.Prop("CelebratingTicks").AsInt(),
            node.Prop("DribbleDuelCooldownTicks").AsInt());
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
        node.EnsureKnownKeys("baseWin", "attackerTechniqueFactor", "defenderSpeedFactor", "defenderStrengthFactor");
        return new DribbleTuning(
            node.Prop("baseWin").AsInt(),
            node.Prop("attackerTechniqueFactor").AsInt(),
            node.Prop("defenderSpeedFactor").AsInt(),
            node.Prop("defenderStrengthFactor").AsInt());
    }

    private static ShotTuning ParseShot(Json node)
    {
        node.EnsureKnownKeys("baseQuality", "techniqueFactor", "strengthFactor", "distancePenaltyPerCell", "pressurePenalty", "offTargetBase", "offTargetDistanceFactor");
        return new ShotTuning(
            node.Prop("baseQuality").AsInt(),
            node.Prop("techniqueFactor").AsInt(),
            node.Prop("strengthFactor").AsInt(),
            node.Prop("distancePenaltyPerCell").AsInt(),
            node.Prop("pressurePenalty").AsInt(),
            node.Prop("offTargetBase").AsInt(),
            node.Prop("offTargetDistanceFactor").AsInt());
    }

    private static SaveTuning ParseSave(Json node)
    {
        node.EnsureKnownKeys("basePercent", "closeRangeCells", "attributeWeightPercent", "consecutiveShotDecayPercent");
        return new SaveTuning(
            node.Prop("basePercent").AsInt(),
            node.Prop("closeRangeCells").AsInt(),
            node.Prop("attributeWeightPercent").AsInt(),
            node.Prop("consecutiveShotDecayPercent").AsInt());
    }

    private static TackleTuning ParseTackle(Json node)
    {
        node.EnsureKnownKeys("baseWin", "strengthFactor", "speedFactor", "carrierTechniqueFactor", "foulBase", "foulStrengthFactor", "hardTackleThreshold", "yellowCardBase", "redCardBase", "secondYellowIsRed");
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

    private static PitchTuning ParsePitch(Json node)
    {
        node.EnsureKnownKeys("columns", "rows", "areaColumns", "areaRows", "goalRows");
        return new PitchTuning(
            node.Prop("columns").AsInt(),
            node.Prop("rows").AsInt(),
            node.Prop("areaColumns").AsInt(),
            node.Prop("areaRows").AsInt(),
            node.Prop("goalRows").AsInt());
    }

    private static GenerationTuning ParseGeneration(Json node)
    {
        node.EnsureKnownKeys("positionBias", "traitCountWeights", "goalkeeperTraitChance");

        var biasNode = node.Prop("positionBias");
        biasNode.EnsureKnownKeys("Goalkeeper", "Defender", "Midfielder", "Forward");
        var positionBias = new PositionBiasTable(
            ParsePositionBiasEntry(biasNode.Prop("Goalkeeper")),
            ParsePositionBiasEntry(biasNode.Prop("Defender")),
            ParsePositionBiasEntry(biasNode.Prop("Midfielder")),
            ParsePositionBiasEntry(biasNode.Prop("Forward")));

        var traitCountWeights = node.Prop("traitCountWeights").EnumerateArray().Select(j => j.AsInt()).ToList();

        return new GenerationTuning(positionBias, traitCountWeights, node.Prop("goalkeeperTraitChance").AsInt());
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
