using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Data;

/// <summary>Nombre localizado es/en. El texto visible por el jugador siempre sale de aquí o de data/l10n.</summary>
public sealed record LocalizedName(string Es, string En);

/// <summary>
/// Lista de nombres de un generador de raza (data/races/*.json.names.first/last, RF-020b), con la
/// misma cantidad de entradas en los dos idiomas: <see cref="Sim.Generation.NameGenerator"/> sortea un
/// <b>índice</b>, nunca un idioma, así que ese índice tiene que señalar al mismo jugador en <see cref="Es"/>
/// y en <see cref="En"/> (RT-073, docs/estilo-descripciones.md "Nombres propios"). Un nombre de pila de
/// fantasía repite la misma lista en los dos idiomas; un apellido parlante lleva el equivalente del mismo
/// registro, nunca una traducción literal. DataLoader rechaza cualquier fichero donde <c>Es.Count</c> y
/// <c>En.Count</c> difieran.
/// </summary>
public sealed record LocalizedNameList(IReadOnlyList<string> Es, IReadOnlyList<string> En)
{
    /// <summary>Número de entradas, igual en los dos idiomas (invariante comprobado por DataLoader).</summary>
    public int Count => Es.Count;
}

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
    LocalizedNameList FirstNames,
    LocalizedNameList LastNames);

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
    int BlockBruteTagBonus = 0,

    // AW-D (docs/pendientes.md, cambio 1 de 2): hasta ahora PassOpenReceiverBonus se cobraba entero con
    // cualquier receptor legal, sin mirar si estaba delante o detrás del pasador. Penaliza solo el avance
    // negativo del receptor elegido, por casilla; un pase lateral o hacia delante no lo paga.
    int PassBackwardPenaltyPerCell = 0,

    // AW-E (docs/pendientes.md, cambio 2 de 2): FindSpace solo premiaba alejarse del rival, sin mirar si
    // la casilla candidata ya tenía compañeros — mismo radio de aglomeración que SupportCrowdedPenalty.
    int FindSpaceCrowdedPenalty = 0,

    // AW-Q (docs/pendientes.md): la línea defensiva no tenía techo. Los dos márgenes en casillas son la
    // holgura sobre la línea defensiva (Utility.DefensiveLineColumn) de sus dos usos independientes: el
    // techo de la casilla-hogar del propio defensa en MatchEngine.UpdateBlockShift, y el recorte de la
    // casilla candidata del desmarque en Utility.EvaluateFindSpace contra la línea RIVAL.
    float BlockShiftLineMarginCells = 0f,
    float FindSpaceLineMarginCells = 0f,
    float PassLaneRadiusCells = 0.6f,
    int PassBlockedLanePenalty = 0,
    int PassBlockedLaneRankPenalty = 0,
    int ShootBlockedLanePenalty = 0,
    int ThroughPassLateTicks = 0,
    int ThroughPassMarginTicks = 0,
    int ThroughPassBase = 0,
    int ThroughPassTechniqueSlope = 0,
    // ADR 0091 (ajuste del revisor): las casillas candidatas del pase en profundidad van de Min a Max por
    // delante del corredor, y a menos de FreeZone casillas de la portería rival no se recortan por la línea
    // defensiva: el pase a la espalda de la defensa es exactamente lo que la acción es.
    int ThroughPassMinCells = 2,
    int ThroughPassMaxCells = 4,
    float ThroughPassFreeZoneCells = 0f,
    // ADR 0136 (centrar). El alcance y la zona de remate son las dos precondiciones DURAS de la acción
    // -sin compañero al alcance y cerca del área no hay centro-; el resto puntúa, nunca descarta, que es
    // la forma que el repositorio ya usa desde el paso 3 de la ADR 0091.
    float CrossMinCells = 0f,
    float CrossMaxCells = 0f,
    float CrossTargetGoalDistanceCells = 0f,
    int CrossBase = 0,
    int CrossApertureGainPerCenti = 0,
    int CrossBlockedLanePenalty = 0,
    // Que el rematador esté marcado PUNTÚA, no descarta (ADR 0136, enmienda): un centro es el balón que se
    // pone CUANDO el área está poblada. La primera versión copió del pase raso la precondición «receptor
    // libre» y dejó el centro en 0,54 por partido.
    int CrossMarkedTargetPenalty = 0,
    int CrossTechniqueSlope = 0,

    // Gameplay AI Foundations Pass, P2 — PROTEGER. La acción solo existe cuando hay alguien apretando: sin
    // presión no hay nada de lo que proteger el balón, así que ShieldMinPressure es precondición DURA (la
    // misma forma que CrossTargetGoalDistanceCells, no una penalización grande). Lo demás puntúa: cuanto
    // más te aprietan más vale aguantar, y la fuerza es el atributo que lo hace viable.
    int ShieldBase = 0,
    int ShieldMinPressure = 0,
    int ShieldPressureBonusPerCenti = 0,
    int ShieldStrengthSlope = 0,

    // Gameplay AI Foundations Pass, P4 — DESPEJAR. Simétrico al anterior: un despeje sin peligro es
    // regalar el balón, así que ClearMinDanger es la precondición dura. El peligro y la presión puntúan;
    // la fuerza NO entra en la decisión (entra en la distancia, que es donde se nota).
    int ClearBase = 0,
    int ClearMinDanger = 0,
    int ClearDangerBonusPerCenti = 0,
    int ClearPressureBonusPerCenti = 0,

    // Gameplay AI Foundations Pass, P3 — ARRANQUE COORDINADO. Cuando un compañero está armando un pase al
    // espacio dirigido A MÍ, las casillas candidatas de mi desmarque que caen cerca de ese espacio valen
    // más. No es una orden: es un sumando más en la misma comparación, así que un desmarque claramente
    // mejor sigue ganando. El radio dice hasta dónde "cerca" significa algo.
    int FindSpaceIntentBonus = 0,
    float FindSpaceIntentRadiusCells = 0f,

    // ADR 0140 — URGENCIA. Cuánto pesa cada gol de diferencia en la urgencia de un equipo, en tanto por
    // ciento. Con 60, ir uno abajo vale 60 y ir dos o más satura en 100: la urgencia crece con el
    // marcador, pero deja de crecer en algún punto porque un equipo no puede atacar «más que con todo».
    int UrgencyPerGoalPercent = 0,

    // ADR 0141 — EL RECEPTOR IMPORTA. Hasta aquí un compañero con un rival encima quedaba DESCARTADO como
    // receptor, sin más: el pase era binario —libre o inexistente— y los atributos del que recibe no
    // entraban en la decisión en ningún sitio. Ahora estar presionado PUNTÚA en contra, y lo que lo
    // compensa es la capacidad del receptor de aguantar el balón.
    int PassReceiverPressureRankPenalty = 0,
    int PassReceiverPressurePenalty = 0,
    int PassReceiverHoldSlope = 0,

    // ADR 0142 — EL CANSADO DEJA DE PRESIONAR. El cansancio ya llega a casi toda la utilidad por las
    // pendientes de atributo (un cansado regatea, pasa y tira peor porque sus atributos son peores), pero
    // las acciones de ESFUERZO —perseguir, presionar, pegarse— no tienen pendiente de atributo y se
    // quedarían fuera justo donde el cansancio se nota más en un campo de verdad.
    int TiredEffortPenalty = 0,

    // ADR 0144 — LA DESCARGA. OfferSupport llevaba muerta desde que la ADR 0022 creó FindSpace «para
    // sustituir su punto fijo» y nadie la retiró: iba a un punto fijo dos casillas por delante del
    // portador, competía contra dieciséis candidatos evaluados y perdía SIEMPRE (medido: 0,01 elecciones
    // por mil decisiones, y un hueco medio de 803 puntos cuando llegaba a competir). Ahora representa una
    // situación futbolística propia que FindSpace no cubre: venir CORTO a dar salida a un compañero al que
    // están apretando, aunque eso signifique ir hacia atrás.
    int SupportBase = 0,
    int SupportMinCarrierPressure = 0,
    int SupportPressedBonusPerCenti = 0,

    // ADR 0145 — REPRESALIA. Lo que vale más entrarle o cargarle al que acaba de romperte a un compañero
    // delante de ti. Es un SUMANDO, no una orden: el jugador sigue comparando, y si entrar no era buena
    // idea sigue sin serlo. El encargo lo pide así de forma explícita.
    int GrudgeBonus = 0);

/// <summary>
/// Pesos de la IA de utilidad (RT-093..RT-098). Las tablas Base y Tactical se guardan como arrays
/// indexados por enum, nunca como Dictionary iterado.
/// </summary>
public sealed class AiWeights
{
    private readonly int[,] _base;
    private readonly int[,] _tactical;
    private readonly int[,] _mentality;
    private readonly int[] _offBallTackle;
    private readonly BlockShift[] _shift;

    internal AiWeights(int[,] baseTable, int[,] tacticalTable, int[,] mentalityTable, int[] offBallTackle, AiContext context, BlockShift[] shift)
    {
        _base = baseTable;
        _tactical = tacticalTable;
        _mentality = mentalityTable;
        _offBallTackle = offBallTackle;
        Context = context;
        _shift = shift;
    }

    /// <summary>
    /// Los mismos pesos con otro <see cref="Context"/>. Existe para que una prueba pueda mover <b>un</b>
    /// término de contexto sobre los pesos reales, en vez de reconstruir a mano las dos tablas enteras
    /// —que es lo que se venía haciendo y lo que hace que un test mida un juego que no es el publicado—.
    /// Comparte los arrays a propósito: nadie los muta después de cargar.
    /// </summary>
    internal AiWeights WithContext(AiContext context) =>
        new(_base, _tactical, _mentality, _offBallTackle, context, _shift);

    /// <summary>Peso base de la acción a para la posición p.</summary>
    public int Base(Position p, PlayerAction a) => _base[(int)p, (int)a];

    /// <summary>Multiplicador táctico (porcentaje, 100 = neutro) de la acción a en el estado s.</summary>
    public int Tactical(TacticalState s, PlayerAction a) => _tactical[(int)s, (int)a];

    /// <summary>
    /// Multiplicador de mentalidad (porcentaje, 100 = neutro) de la acción a con la mentalidad m (ADR
    /// 0140). Es un eje <b>aparte</b> del estado táctico y se multiplica sobre él: el contraste
    /// posesión/no posesión sigue siendo el mismo, y encima se aplica cuánto riesgo quiere el equipo.
    /// </summary>
    public int Mentality(Mentality m, PlayerAction a) => _mentality[(int)m, (int)a];

    /// <summary>
    /// Ajuste de utilidad de la ENTRADA SIN BALÓN al marcado, <b>por puesto</b> (ADR 0125 D2; antes, desde
    /// la ADR 0105, era un entero plano igual para todos). Es un ajuste con <b>signo</b>, no un bono: sin
    /// balón, la entrada de un delantero ya gana a sus propias alternativas (<c>Tackle</c> 211 contra
    /// <c>MarkOpponent</c> 180 tras el multiplicador táctico), mientras que la de un defensa pierde por 180
    /// contra la suya, así que igualar los puestos por arriba invierte el orden que pide la ADR.
    ///
    /// <para><b>0 significa que ese puesto no DECIDE entrar nunca</b>, y se comprueba explícitamente en
    /// <c>Utility.EvaluateTackle</c>: no sale solo del dato. Está medido (22 sep 2026) que con el término
    /// en 0 seguían ocurriendo 0,98 entradas sin balón por partido, porque la acción puede ganar la
    /// comparación sin ningún ajuste.</para>
    ///
    /// <para><b>Alcance exacto de ese "nunca": la decisión, no el motor entero.</b>
    /// <c>MatchEngine.RepeatTackle</c> —el efecto <c>extraAction</c> de "Embestida"/"Arrollador"— no
    /// decide: ejecuta, y marca la entrada como sin balón sin consultar este mapa, así que un puesto con 0
    /// todavía puede producir entradas sin balón por esa vía. Mecanismo real, <b>sin evidencia de
    /// activación</b>: ningún no-defensa lleva esos perks en <c>/data</c>. Fijado por
    /// <c>TandaTwoPrimitivesTests.RepeatTackleIgnoresThePerPositionMapAndIsTheKnownGapOfTheZeroRule</c> y
    /// abierto en <c>docs/pendientes/BE-A.md</c>.</para>
    ///
    /// <para>El invariante de la ADR 0105 §3 sigue vigente para cada puesto: el ajuste nunca llega a
    /// <see cref="AiContext.TackleBallCarrierBonus"/> —quitar el balón siempre puntúa más que pegarle a
    /// quien no lo lleva—. Lo valida <c>DataLoader</c>.</para>
    /// </summary>
    public int OffBallTackleAdjust(Position p) => _offBallTackle[(int)p];

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

/// <summary>
/// Suelo y techo únicos de toda probabilidad de <b>resolución del balón</b> (ADR 0050 P4), en base
/// 10.000. Sustituyen a los límites ad hoc por canal: el 500-9800 del pase, el 5-95% de la parada y la
/// ausencia de límite en regate, entrada, intercepción, bloqueo y tiro a puerta. No alcanzan a los
/// sucesos raros —falta, tarjeta, penalti, lesión y muerte—: ver el <c>_doc</c> del dato.
/// </summary>
public sealed record ResolutionTuning(int ProbabilityFloor, int ProbabilityCeiling);

/// <summary>tuning.movement.</summary>
public sealed record MovementTuning(int BaseCellsPerTickMilli, int SpeedCellsPerTickMilliPer99, int DribbleSpeedPercent);

/// <summary>tuning.ball.</summary>
public sealed record BallTuning(
    int PassSpeedCellsPerTickMilli,
    int ShotSpeedCellsPerTickMilli,
    int LooseBallFrictionPercent,
    int GravityCellsPerTickSqMilli,
    int BounceRestitutionPercent,

    // Gameplay AI Foundations Pass, P4 — LA ALTURA EMPIEZA A SIGNIFICAR ALGO. Hasta aquí un balón podía
    // recogerse estuviera a la altura que estuviera: la ADR 0135 le dio altura al balón y nadie la
    // consultaba al recogerlo. Estas tres cifras parten la recogida en tres casos que son tres jugadas
    // distintas: controlar (raso), cabecear (a la altura de un salto) y nada (por encima de todos).
    float ControlHeightCells = 0f,
    float AerialReachHeightCells = 0f,
    int HeaderSpeedCellsPerTickMilli = 0,
    int HeaderDropCellsPerTickMilli = 0);

/// <summary>tuning.states: duraciones de los estados de jugador, en ticks.</summary>
public sealed record StatesTuning(int PassingTicks, int ShootingTicks, int TacklingTicks, int KnockedDownTicks, int CelebratingTicks, int DribbleDuelCooldownTicks, int TackleCooldownTicks, int OffBallTackleCooldownTicks, int ShieldingTicks = 0, int AerialCooldownTicks = 0, int GrudgeTicks = 0);

/// <summary>tuning.pass.</summary>
public sealed record PassTuning(int BaseSuccess, int TechniqueFactor, int DistancePenaltyPerCell, int PressurePenalty, float InterceptRadiusCells, int InterceptBaseChance, int InterceptTechniqueFactor, float MaxLeadCells, int InterceptContactPercent, int LoftedPeakHeightCellsMilli = 0);

/// <summary>tuning.dribble.</summary>
public sealed record DribbleTuning(int BaseWin, int AttackerTechniqueFactor, int DefenderSpeedSharePercent, int LostKnockdownTicks, int DriveTicks, int DriveTicksTechniqueSharePercent);

/// <summary>tuning.shot.</summary>
public sealed record ShotTuning(
    int BaseQuality,
    int TechniqueFactor,
    int StrengthFactor,
    int DistancePenaltyPerCell,
    int PressurePenalty,
    int OffTargetBase,
    int OffTargetDistanceFactor,
    int PenaltyQualityBonus,
    int BlockChancePercent,
    int GoalHalfWidthCellsMilli,
    int GoalHeightCellsMilli,
    int ArcCellsPerCellMilli,
    int PostThicknessCellsMilli,
    int MinAimApertureCenti,
    int OffTargetAperturePenalty);

/// <summary>tuning.save.</summary>
/// <summary>
/// Centro y remate (ADR 0136). <b>La comba es lo que hace que el centro exista</b>: el balón pasa por
/// encima del radio de intercepción a mitad de vuelo, que es la única diferencia física entre un centro y
/// un pase largo.
/// <para>
/// <c>peakHeightCellsMilli</c> es la altura del vuelo <b>en absoluto</b>, no por casilla de distancia. Se
/// probó lo segundo primero y un test lo tumbó antes de medir nada: un centro corto desde el cordel (3,6
/// casillas) hacía pico <b>0,897</b> contra un radio de 0,9, así que la mitad de los centros se
/// interceptaban igual que un pase raso y la acción no habría hecho nada. Una altura fija además es lo
/// que hace un centro de verdad —se levanta para salvar a los defensas, no en proporción a lo lejos que
/// se esté— y deja la invariante «un centro SIEMPRE despega por encima del radio» comprobable con un solo
/// número.
/// </para>
/// <para>
/// El remate invierte los factores del tiro a propósito (<c>ShotTuning</c>: técnica 14, fuerza 4): el tiro
/// es <b>colocar</b> y el remate es <b>llegar y empujarla</b>. Y paga
/// <c>volleyOffTargetPenalty</c> de puntería por no controlar el balón antes de golpearlo.
/// </para>
/// </summary>
public sealed record CrossTuning(
    int PeakHeightCellsMilli,
    int VolleyBaseQuality,
    int VolleyTechniqueFactor,
    int VolleyStrengthFactor,
    int VolleyOffTargetPenalty);

public sealed record SaveTuning(
    int BasePercent,
    int CloseRangeCells,
    int AttributeWeightPercent,
    int ConsecutiveShotDecayPercent,
    int QualityWeight,
    int QualityPivot,
    float ReachCells,
    float DiveReachCells,
    int DivePenaltyPercent,

    // ADR 0141 — QUÉ PASA DESPUÉS DE PARARLA. Hasta aquí el portero que ganaba el duelo ATRAPABA SIEMPRE,
    // así que una parada cerraba la jugada y no existía el rechace. Estas cinco cifras deciden si la
    // blocó, la rechazó o la mandó a córner, que son tres jugadas distintas para el jugador.
    int CatchBasePercent = 0,
    int CatchAttributeWeightPercent = 0,
    int CatchQualityWeight = 0,
    int CatchDivePenaltyPercent = 0,
    float CornerOffCentreCells = 0f,
    int ParrySpeedCellsPerTickMilli = 0,
    int ParryLiftCellsPerTickMilli = 0);

/// <summary>tuning.tackle.</summary>
public sealed record TackleTuning(int BaseWin, int PressureFactor, int StrengthSharePercent, int FoulBase, int OffBallFoulBase, int FoulStrengthFactor, int HardTackleThreshold, int YellowCardBase, int RedCardBase, int HardTackleYellowBonus, int HardTackleRedBonus, bool SecondYellowIsRed, int ShieldResistance = 0);

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
    int EliteScalePercent,
    Underleague.Sim.Perks.LethalityTuning Lethality)
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
    int BiasShiftRedExtra,
    int WhistlePercent);

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
/// <summary>
/// tuning.goalkeeper — cuándo y cuánto puede salirse del área el portero (ADR 0141). Bloque propio y no
/// una clave más de <c>save</c>: la parada y la salida son dos cosas distintas, y meterlas juntas invitaría
/// a calibrarlas como si fueran una.
/// </summary>
/// <param name="ExitCells">Casillas que se ensancha el área cuando se le permite salir. Nunca deja de estar acotado: se le mueve el límite.</param>
/// <param name="ExitUrgencyPercent">Urgencia mínima (ADR 0140) para que el portero suba por necesidad de gol. Alta a propósito: salir no puede ser común.</param>
public sealed record GoalkeeperTuning(float ExitCells, int ExitUrgencyPercent);

/// <summary>
/// tuning.fatigue — el cansancio como <b>recurso</b> (ADR 0142). Sustituye a la rampa global del reloj que
/// había en <c>movement</c>, que vaciaba a todo el mundo por igual desde un minuto fijo y hacía que el
/// aguante fuera casi decorativo.
/// </summary>
/// <param name="RunCostPerTick">Coste de correr un tick a tope, en milésimas de energía. Proporcional a lo que se corre de verdad.</param>
/// <param name="CarryCostPerTick">Coste extra por tick de llevar el balón (conducir o proteger): cuesta más que correr suelto.</param>
/// <param name="ContactCost">Coste de un esfuerzo puntual: una entrada, una carga, un salto.</param>
/// <param name="RecoverPerTick">Energía que se recupera por tick sin esforzarse, antes de aplicar el aguante.</param>
/// <param name="MaxPenaltyPoints">Puntos de atributo que quita el cansancio con la energía a cero.</param>
public sealed record FatigueTuning(
    int RunCostPerTick,
    int CarryCostPerTick,
    int ContactCost,
    int RecoverPerTick,
    int MaxPenaltyPoints);

public sealed record RestartTuning(
    int ThrowInTicks,
    int GoalKickTicks,
    int CornerTicks,
    int KickoffTicks,
    int PenaltyTicks,
    int FreeKickTicks,
    float RestartClearanceCells,

    // BC-A: el saque de centro no arranca hasta que el equipo ha vuelto andando a su formación. Tope en
    // ticks para que un derribado o un caso raro no congelen el partido, y tolerancia en casillas para no
    // exigir el punto exacto —a un jugador que anda le sobra con estar EN su sitio, no clavado en él—.
    int KickoffMaxWaitTicks = 0,
    float InPlaceCells = 0f);

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
/// <summary>
/// tuning.clear — el despeje (Gameplay AI Foundations Pass, P4). Un despeje no es un pase largo sin
/// receptor: es un balón que sale <b>lejos y alto</b> para que el peligro se aleje de la portería propia,
/// y que al caer <b>no es de nadie</b>. La distancia depende de la fuerza porque despejar es golpear, no
/// colocar —los factores son los del remate invertidos, igual que hizo la ADR 0136 con el centro—.
/// </summary>
/// <param name="BaseDistanceCells">Casillas que recorre el despeje de un jugador de fuerza 50.</param>
/// <param name="StrengthDistanceMilliPerPoint">Milésimas de casilla más por punto de fuerza sobre 50.</param>
/// <param name="PeakHeightCellsMilli">Altura del pico de la parábola, en milésimas de casilla.</param>
/// <param name="SpreadRows">Dispersión lateral en filas: un despeje no elige destino, lo aproxima.</param>
public sealed record ClearTuning(
    float BaseDistanceCells,
    int StrengthDistanceMilliPerPoint,
    int PeakHeightCellsMilli,
    int SpreadRows,

    // BI-F: casillas de alcance que se pierden cuando al que despeja lo tienen encima. Un despeje apurado
    // no llega donde uno golpeado con tiempo, y sin esta distinción el saque de puerta —que se golpea
    // SOLO y con el balón parado— llegaba tan corto como un despeje angustiado y caía entre los rivales.
    float PressurePenaltyCells = 0f);

public sealed record Tuning(
    int RegulationTicks,
    int GoldenGoalMaxTicks,
    int DecisionIntervalTicks,
    int TransitionTicks,
    int AssistWindowTicks,
    ResolutionTuning Resolution,
    MovementTuning Movement,
    BallTuning Ball,
    StatesTuning States,
    PassTuning Pass,
    DribbleTuning Dribble,
    ShotTuning Shot,
    CrossTuning Cross,
    ClearTuning Clear,
    SaveTuning Save,
    GoalkeeperTuning Goalkeeper,
    FatigueTuning Fatigue,
    TackleTuning Tackle,
    InjuryTuning Injury,
    RefereeTuning Referee,
    BlockTuning Block,
    RestartTuning Restart,
    GenerationTuning Generation,
    BodiesTuning Bodies,
    ActionZoneTuning ActionZone,
    ProgressionTuning Progression);

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
