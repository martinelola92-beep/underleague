using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Data;

/// <summary>Nombre localizado es/en. El texto visible por el jugador siempre sale de aquí o de data/l10n.</summary>
public sealed record LocalizedName(string Es, string En);

/// <summary>
/// Definición de una raza jugable (data/races/*.json, fase1b-diseno.md §1.1). <c>SpeciesTag</c> es la
/// etiqueta fija de especie (ADR 0024); <c>StyleTagWeights</c> es la distribución de estilo individual
/// que Sim.Generation.PlayerGenerator sortea por jugador. <c>BodyRadius</c> en centésimas de casilla
/// (ADR 0020); <c>Discipline</c> 0-100, cuánto tira de vuelta a la zona (ADR 0028); <c>Ability</c> es el
/// id del perk de habilidad racial (ADR 0026, en data/perks/, no validado aquí: lo carga otro paquete).
/// </summary>
public sealed record RaceDefinition(
    Race Id,
    LocalizedName Name,
    string SpeciesTag,
    IReadOnlyList<(StyleTag Style, int Weight)> StyleTagWeights,
    bool Launch,
    int CellsOccupied,
    int BodyRadius,
    int Discipline,
    Attributes AttributeBias,
    string Ability,
    LocalizedName Description,
    int IndividualDeviation,
    IReadOnlyList<(Trait Trait, int Weight)> TraitWeights,
    IReadOnlyList<string> FirstNames,
    IReadOnlyList<string> LastNames);

/// <summary>
/// Definición de una etiqueta de estilo (data/tags/styles.json, fase1b-diseno.md §1.2, ADR 0024).
/// <c>AttributeBias</c> es lo que hace que, por ejemplo, un elfo Brute sea de verdad más fuerte que un
/// elfo medio: Sim.Generation.PlayerGenerator lo suma al sesgo de raza al repartir el presupuesto.
/// </summary>
public sealed record StyleDefinition(
    StyleTag Id,
    LocalizedName Name,
    LocalizedName Description,
    Attributes AttributeBias);

/// <summary>Definición de un rasgo de jugador (data/traits/traits.json).</summary>
public sealed record TraitDefinition(
    Trait Id,
    LocalizedName Name,
    IReadOnlyList<(PlayerAction Action, int MultiplierPercent)> ActionMultipliers,
    int HardTackleBonus,
    int SpeedBonusPercent,
    int ShotQualityBonus,
    int ShootRangeBonusCells,
    int PassQualityBonus,
    int FoulChanceBonus,
    int InjuryChanceBonus,
    int FatigueResistancePercent,
    int InjuryResistanceBonus,
    int AdjacentTeammateBonusPercent,
    int SaveBonusClose,
    int SaveBonusFar,
    int LeashBonus,
    bool GoalkeeperOnly);

/// <summary>Desplazamiento de bloque objetivo para un estado táctico (data/ai/weights.json, blockShift).</summary>
public readonly record struct BlockShift(float Shift, int SpeedTicks);

/// <summary>Términos de contexto enteros de la IA de utilidad (data/ai/weights.json, context).</summary>
public sealed record AiContext(
    int ChaseBallDistancePenaltyPerCell,
    int ChaseBallLooseBonus,
    int ChaseBallNotNearestPenalty,
    int ChaseBallIncomingPassBonus,
    int MarkDistancePenaltyPerCell,
    int SupportAheadBonus,
    int SupportCrowdedPenalty,
    int CoverBetweenBallAndGoalBonus,
    int PassOpenReceiverBonus,
    int PassUnderPressureBonus,
    int PassNoReceiverPenalty,
    int DribbleOpenSpaceBonus,
    int DribbleOpponentAheadPenalty,
    int ShootBaseRangeCells,
    int ShootInRangeBonus,
    int ShootBeyondRangePenaltyPerCell,
    int ShootDistancePenaltyPerCell,
    int ShootAnglePenaltyPerRow,
    float TackleDistanceMaxCells,
    int TackleOutOfReachPenalty,
    int TackleBallCarrierBonus,
    int RetreatDistanceBonusPerCell,
    int RetreatAtHomePenalty,

    // Términos de FindSpace y PressCarrier (ADR 0022, §2.3). El paquete R los dejó como constantes de
    // Utility.cs porque añadir claves aquí exigía abrir Sim/Data, fuera de sus fronteras; era su única
    // deuda declarada (§4, decisión 20) y aquí queda saldada, con el mismo nombre que ya tenían.
    int FindSpaceOpponentDistanceBonusPerCell = 0,
    int FindSpaceAdvanceBonusPerCell = 0,
    int FindSpaceOpenLaneBonus = 0,
    int PressCarrierBonus = 0,
    int PressDistancePenaltyPerCell = 0,
    int PressGoalkeeperExitBonus = 0,

    // Acciones de ataque diferenciadas (ADR 0030 §1). Las dos bandas de pase son disjuntas y exhaustivas:
    // corto es "distancia <= ShortPassMaxCells" y largo, "> ShortPassMaxCells y <= LongPassMaxCells".
    // Las pendientes son puntos de utilidad por punto de atributo por encima de 50, con signo: el torpe
    // paga lo mismo que cobra el brillante. La del pase corto es deliberadamente la más suave de todas.
    float ShortPassMaxCells = 0f,
    float LongPassMaxCells = 0f,
    int ShortPassTechniqueSlope = 0,
    int LongPassTechniqueSlope = 0,
    int DribbleTechniqueSlope = 0,
    int DribbleSpeedSlope = 0,
    int ShootTechniqueSlope = 0,
    int ShootStrengthSlope = 0,

    // Bloqueo sin balón (ADR 0030 §2). La "jugada activa" de RF-057 son las dos primeras claves: un radio
    // alrededor del balón o un corredor entre el balón y la portería que ataca quien lo tiene.
    float BlockActiveRadiusCells = 0f,
    float BlockCorridorHalfWidthCells = 0f,
    float BlockReachMaxCells = 0f,
    int BlockTargetBonus = 0,
    int BlockDistancePenaltyPerCell = 0,
    int BlockAggressiveBonus = 0,
    int BlockBruteTagBonus = 0);

/// <summary>
/// Pesos de la IA de utilidad (RT-093..RT-098). Las tablas Base y Tactical se guardan como arrays
/// indexados por enum, nunca como Dictionary iterado.
/// </summary>
public sealed class AiWeights
{
    private readonly int[,] _base;
    private readonly int[,] _tactical;
    private readonly BlockShift[] _shift;

    internal AiWeights(int[,] baseTable, int[,] tacticalTable, AiContext context, BlockShift[] shift)
    {
        _base = baseTable;
        _tactical = tacticalTable;
        Context = context;
        _shift = shift;
    }

    /// <summary>Peso base de la acción a para la posición p.</summary>
    public int Base(Position p, PlayerAction a) => _base[(int)p, (int)a];

    /// <summary>Multiplicador táctico (porcentaje, 100 = neutro) de la acción a en el estado s.</summary>
    public int Tactical(TacticalState s, PlayerAction a) => _tactical[(int)s, (int)a];

    /// <summary>Términos de contexto compartidos por todas las posiciones.</summary>
    public AiContext Context { get; }

    /// <summary>Desplazamiento de bloque objetivo para el estado táctico s.</summary>
    public BlockShift Shift(TacticalState s) => _shift[(int)s];
}

/// <summary>
/// Porcentaje del presupuesto de generación (fase1b-diseno.md §1.3) asignado a cada atributo; misma
/// forma que <see cref="Attributes"/> (cinco campos con nombre) pero sin su semántica de rango 1..99:
/// aquí cada campo es un entero 0..100 y los cinco de una posición suman 100 (DataLoader lo valida).
/// </summary>
public readonly record struct AttributeShare(int Strength, int Speed, int Technique, int Stamina, int Leash)
{
    /// <summary>Lee el porcentaje del atributo indicado por kind.</summary>
    public int Get(AttributeKind kind) => kind switch
    {
        AttributeKind.Strength => Strength,
        AttributeKind.Speed => Speed,
        AttributeKind.Technique => Technique,
        AttributeKind.Stamina => Stamina,
        AttributeKind.Leash => Leash,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>tuning.generation.positionShare: reparto porcentual del presupuesto por posición (§1.3).</summary>
public sealed record PositionShareTable(AttributeShare Goalkeeper, AttributeShare Defender, AttributeShare Midfielder, AttributeShare Forward)
{
    /// <summary>Reparto de presupuesto de esa posición: qué atributos le importan y cuánto.</summary>
    public AttributeShare Of(Position position) => position switch
    {
        Position.Goalkeeper => Goalkeeper,
        Position.Defender => Defender,
        Position.Midfielder => Midfielder,
        Position.Forward => Forward,
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };
}

/// <summary>Mínimo y máximo de un atributo para una rareza (tuning.generation.rangeByRarity).</summary>
public sealed record AttributeRange(int Min, int Max);

/// <summary>tuning.generation.rangeByRarity: baremo por rareza, igual para los cinco atributos (§1.3).</summary>
public sealed record RarityRangeTable(AttributeRange Common, AttributeRange Uncommon, AttributeRange Rare, AttributeRange Legendary);

/// <summary>tuning.generation.budgetByRarity: presupuesto de atributos en nivel 1, por rareza (§1.3, ADR 0027).</summary>
public sealed record RarityBudgetTable(int Common, int Uncommon, int Rare, int Legendary)
{
    /// <summary>Presupuesto de la rareza indicada.</summary>
    public int Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => Common,
        Rarity.Uncommon => Uncommon,
        Rarity.Rare => Rare,
        Rarity.Legendary => Legendary,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };
}

/// <summary>
/// Suelo por atributo, adicional al de <c>rangeByRarity</c>, solo para los atributos que una posición
/// necesita garantizados (tuning.generation.positionFloors, §1.3): ausente = sin suelo adicional.
/// </summary>
public sealed record PositionFloorTable(
    IReadOnlyDictionary<AttributeKind, int> Goalkeeper,
    IReadOnlyDictionary<AttributeKind, int> Defender,
    IReadOnlyDictionary<AttributeKind, int> Midfielder,
    IReadOnlyDictionary<AttributeKind, int> Forward)
{
    /// <summary>Tabla de suelos de la posición indicada.</summary>
    public IReadOnlyDictionary<AttributeKind, int> Of(Position position) => position switch
    {
        Position.Goalkeeper => Goalkeeper,
        Position.Defender => Defender,
        Position.Midfielder => Midfielder,
        Position.Forward => Forward,
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };
}

/// <summary>
/// tuning.generation: modelo de presupuesto de la generación de jugadores (fase1b-diseno.md §1.3,
/// ADR 0025, ADR 0027). Ver Sim.Generation.PlayerGenerator para el algoritmo de reparto y renormalización.
/// </summary>
public sealed record GenerationTuning(
    RarityBudgetTable BudgetByRarity,
    int BudgetPerLevel,
    int AttributeFloor,
    int AttributeCap,
    RarityRangeTable RangeByRarity,
    PositionShareTable PositionShare,
    PositionFloorTable PositionFloors,
    IReadOnlyList<int> TraitCountWeights,
    int GoalkeeperTraitChance);

/// <summary>Forma de la zona de acción de una posición, en casillas relativas a la casilla-hogar efectiva; -1 = sin límite (ADR 0028).</summary>
public sealed record ZoneShape(int Forward, int Back, int Sides);

/// <summary>tuning.actionZone.shape: forma de la zona por posición (§1.3, ADR 0028).</summary>
public sealed record ActionZoneShapeTable(ZoneShape Goalkeeper, ZoneShape Defender, ZoneShape Midfielder, ZoneShape Forward);

/// <summary>tuning.actionZone.scaleFromLeashPercent: escala de la zona interpolada según el atributo Leash 1..99.</summary>
public sealed record LeashScalePercent(int At1, int At99);

/// <summary>
/// tuning.actionZone: zona de acción con forma que sustituye al radio de correa duro (ADR 0028,
/// fase1b-diseno.md §1.3, §2.2). No tiene todavía consumidor en /Sim (lo añade el paquete R); se carga
/// aquí para que exista, se valide y esté disponible.
/// </summary>
public sealed record ActionZoneTuning(
    ActionZoneShapeTable Shape,
    LeashScalePercent ScaleFromLeashPercent,
    int OuterLimitMultiplier,
    int OutsidePenaltyPerCell,
    int DisciplineWeightPercent,
    int RetreatBonusOutsidePerCell);

/// <summary>
/// tuning.bodies: colisión y empuje entre cuerpos (ADR 0020, fase1b-diseno.md §1.3, §2.1). Sin
/// consumidor todavía en /Sim (lo añade el paquete R); se carga aquí para que exista y se valide.
/// </summary>
public sealed record BodiesTuning(
    bool SeparationEnabled,
    int MaxPushPerTickMilli,
    int MassStrengthWeight,
    int MassRadiusWeight,
    int TacklePushMultiplier);

/// <summary>tuning.movement.</summary>
public sealed record MovementTuning(int BaseCellsPerTickMilli, int SpeedCellsPerTickMilliPer99, int DribbleSpeedPercent, int FatigueStartTick, int FatigueMaxSlowPercent);

/// <summary>tuning.ball.</summary>
public sealed record BallTuning(int PassSpeedCellsPerTickMilli, int ShotSpeedCellsPerTickMilli, int LooseBallFrictionPercent);

/// <summary>tuning.states: duraciones de los estados de jugador, en ticks.</summary>
public sealed record StatesTuning(int PassingTicks, int ShootingTicks, int TacklingTicks, int KnockedDownTicks, int CelebratingTicks, int DribbleDuelCooldownTicks, int TackleCooldownTicks);

/// <summary>tuning.pass.</summary>
public sealed record PassTuning(int BaseSuccess, int TechniqueFactor, int DistancePenaltyPerCell, int PressurePenalty, float InterceptRadiusCells, int InterceptBaseChance, int InterceptTechniqueFactor);

/// <summary>tuning.dribble.</summary>
public sealed record DribbleTuning(int BaseWin, int AttackerTechniqueFactor, int DefenderSpeedSharePercent, int LostKnockdownTicks);

/// <summary>tuning.shot.</summary>
public sealed record ShotTuning(int BaseQuality, int TechniqueFactor, int StrengthFactor, int DistancePenaltyPerCell, int PressurePenalty, int OffTargetBase, int OffTargetDistanceFactor, int PenaltyQualityBonus);

/// <summary>tuning.save.</summary>
public sealed record SaveTuning(int BasePercent, int CloseRangeCells, int AttributeWeightPercent, int ConsecutiveShotDecayPercent, int QualityWeight, int QualityPivot);

/// <summary>tuning.tackle.</summary>
public sealed record TackleTuning(int BaseWin, int PressureFactor, int StrengthSharePercent, int FoulBase, int FoulStrengthFactor, int HardTackleThreshold, int YellowCardBase, int RedCardBase, int HardTackleYellowBonus, int HardTackleRedBonus, bool SecondYellowIsRed);

/// <summary>tuning.injury.</summary>
/// <summary>
/// tuning.injury. <c>ActScalePercent</c> y <c>EliteScalePercent</c> son el desgaste creciente por acto de
/// la ADR 0043: multiplicadores en tanto por ciento sobre la probabilidad ya calculada, <b>sin tocar la
/// fórmula</b>. El motor no sabe en qué acto está, así que los aplica el bucle de run pasándolos en
/// <c>SimConfig.InjuryScalePercent</c> (<c>IRunSystems.MatchConfig</c>); un partido suelto usa el 100%.
/// </summary>
public sealed record InjuryTuning(
    int OnTackleBase,
    int OnFoulBase,
    int RelativeFactor,
    int SevereShare,
    IReadOnlyList<int> ActScalePercent,
    int EliteScalePercent)
{
    /// <summary>Multiplicador de desgaste del acto indicado (1..3), en tanto por ciento.</summary>
    public int ScaleForAct(int act) =>
        act >= 1 && act <= ActScalePercent.Count ? ActScalePercent[act - 1] : 100;
}

/// <summary>
/// tuning.referee: el criterio del árbitro (RF-062..RF-064, ADR 0030 §3). Los tres campos
/// <c>...ShiftPer10</c> son <b>efectos</b> del criterio sobre una tirada (puntos base 10.000 por cada 10
/// puntos de criterio); los campos <c>BiasShift...</c> son <b>desplazamientos</b> del propio criterio, en
/// puntos de la escala -100..+100, y son acumulativos por gravedad (RF-063).
/// </summary>
public sealed record RefereeTuning(
    int BiasFoulShiftPer10,
    int PenaltyOnFoulInArea,
    int BiasCardShiftPer10,
    int BiasPenaltyShiftPer10,
    int BiasShiftFoulSeen,
    int BiasShiftFoulUnseen,
    int BiasShiftHardExtra,
    int BiasShiftBlockExtra,
    int BiasShiftInjuryExtra,
    int BiasShiftYellowExtra,
    int BiasShiftRedExtra);

/// <summary>
/// tuning.block: resolución del bloqueo sin balón (ADR 0030 §2). La <b>decisión</b> de bloquear vive en
/// data/ai/weights.json como cualquier otra acción; aquí están las constantes de la <b>resolución</b>,
/// junto a las de la entrada y el regate, que es donde el motor las busca.
/// </summary>
public sealed record BlockTuning(
    int BlockingTicks,
    int CooldownTicks,
    int BaseWin,
    int StrengthFactor,
    int SpeedFactor,
    int KnockdownTicks,
    int FoulBase);

/// <summary>tuning.progression: experiencia, niveles y atributos por nivel (§6, RF-025, RF-027).</summary>
public sealed record ProgressionTuning(
    int MatchExperience,
    int BenchSharePercent,
    IReadOnlyList<int> ExperiencePerLevel,
    int AttributesPerLevel);

/// <summary>tuning.restart.</summary>
public sealed record RestartTuning(int ThrowInTicks, int GoalKickTicks, int CornerTicks, int KickoffTicks, int PenaltyTicks);

/// <summary>
/// Constantes de resolución del simulador (data/sim/tuning.json), un campo por clave, anidado por sección.
/// Decisión fuera de la especificación (revisión independiente, fase 0): las claves "ticksPerSecond" y
/// "pitch" de tuning.json no las leía nadie (la geometría del campo vive en Sim.Model.Pitch como
/// constantes de compilación, no en datos, y el reloj lógico de 15/s es RT-020, no un ajuste de balance).
/// Cablear Sim.Model.Pitch a estos datos habría tocado más de 20 sitios (const de compilación en
/// MatchEngine/Utility que dependen de Pitch.Columns/Rows en tiempo de compilación, más el propio Utility
/// como clase estática), muy por encima del umbral fijado para el arreglo; se retiran del esquema, del
/// catálogo y del parser en vez de cablearlas en silencio. Ver el informe del hito para el detalle.
/// </summary>
public sealed record Tuning(
    int RegulationTicks,
    int GoldenGoalMaxTicks,
    int DecisionIntervalTicks,
    int TransitionTicks,
    int AssistWindowTicks,
    MovementTuning Movement,
    BallTuning Ball,
    StatesTuning States,
    PassTuning Pass,
    DribbleTuning Dribble,
    ShotTuning Shot,
    SaveTuning Save,
    TackleTuning Tackle,
    InjuryTuning Injury,
    RefereeTuning Referee,
    BlockTuning Block,
    RestartTuning Restart,
    GenerationTuning Generation,
    BodiesTuning Bodies,
    ActionZoneTuning ActionZone,
    ProgressionTuning Progression,
    Underleague.Sim.Perks.ProbabilityScale Probability);

/// <summary>
/// Plantillas de descripción de un idioma (data/l10n/&lt;lang&gt;/templates.json, RT-035). Se guardan
/// aplanadas como "sección.clave" para que la búsqueda sea un único acceso y para poder comprobar de una
/// pasada que el catálogo entero es describible al cargar.
/// </summary>
public sealed class DescriptionTemplates
{
    private readonly Dictionary<string, string> _entries;

    internal DescriptionTemplates(string language, Dictionary<string, string> entries)
    {
        Language = language;
        _entries = entries;
    }

    /// <summary>Código de idioma ("es", "en").</summary>
    public string Language { get; }

    /// <summary>Texto de la clave, o null si la plantilla no la define.</summary>
    public string? Find(string section, string key) =>
        _entries.GetValueOrDefault(section + "." + key);

    /// <summary>Texto de la clave; lanza si falta (una descripción incompleta es un fallo de datos).</summary>
    public string Get(string section, string key) =>
        Find(section, key)
        ?? throw new InvalidOperationException(
            $"data/l10n/{Language}/templates.json no define '{section}.{key}'");
}

/// <summary>Plantillas de descripción por idioma, ordenadas por código de idioma ordinal (RT-073).</summary>
public sealed class Localization
{
    private readonly DescriptionTemplates[] _languages;

    internal Localization(IEnumerable<DescriptionTemplates> languages)
    {
        _languages = languages.OrderBy(l => l.Language, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Localización vacía: /Sim funciona sin plantillas mientras nadie pida una descripción.</summary>
    public static Localization Empty { get; } = new(Array.Empty<DescriptionTemplates>());

    /// <summary>Idiomas cargados, en orden ordinal.</summary>
    public IReadOnlyList<string> Languages => _languages.Select(l => l.Language).ToArray();

    /// <summary>Plantillas del idioma, o null si no está cargado.</summary>
    public DescriptionTemplates? Find(string language)
    {
        for (int i = 0; i < _languages.Length; i++)
        {
            if (string.Equals(_languages[i].Language, language, StringComparison.Ordinal))
            {
                return _languages[i];
            }
        }

        return null;
    }

    /// <summary>Plantillas del idioma; lanza si no está cargado.</summary>
    public DescriptionTemplates Get(string language) =>
        Find(language) ?? throw new InvalidOperationException($"idioma no cargado en data/l10n: {language}");

    /// <summary>Todas las plantillas cargadas, en orden ordinal de idioma.</summary>
    internal IReadOnlyList<DescriptionTemplates> All => _languages;
}

/// <summary>Conjunto de datos cargado de /data, listo para pasar a Simulator.Run.</summary>
public sealed record Catalog(
    IReadOnlyList<RaceDefinition> Races,
    IReadOnlyList<StyleDefinition> Styles,
    IReadOnlyList<TraitDefinition> Traits,
    AiWeights Ai,
    Tuning Tuning,
    PerkCatalog Perks,
    Localization Localization)
{
    /// <summary>Busca la definición de la raza id; lanza si no está en el catálogo.</summary>
    public RaceDefinition Race(Race id) =>
        Races.FirstOrDefault(r => r.Id == id) ?? throw new InvalidOperationException($"raza no encontrada en el catálogo: {id}");

    /// <summary>Busca la definición de la etiqueta de estilo id; lanza si no está en el catálogo.</summary>
    public StyleDefinition Style(StyleTag id) =>
        Styles.FirstOrDefault(s => s.Id == id) ?? throw new InvalidOperationException($"etiqueta de estilo no encontrada en el catálogo: {id}");

    /// <summary>Busca la definición del rasgo id; lanza si no está en el catálogo.</summary>
    public TraitDefinition Trait(Trait id) =>
        Traits.FirstOrDefault(t => t.Id == id) ?? throw new InvalidOperationException($"rasgo no encontrado en el catálogo: {id}");

    /// <summary>Tabla de progresión (§6); atajo a <c>Tuning.Progression</c>.</summary>
    public ProgressionTuning Progression => Tuning.Progression;
}
