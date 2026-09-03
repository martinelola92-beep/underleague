using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Data;

/// <summary>Nombre localizado es/en. El texto visible por el jugador siempre sale de aquí o de data/l10n.</summary>
public sealed record LocalizedName(string Es, string En);

/// <summary>Definición de una raza jugable (data/races/*.json).</summary>
public sealed record RaceDefinition(
    Race Id,
    LocalizedName Name,
    string Tag,
    bool Launch,
    int CellsOccupied,
    Attributes AttributeBias,
    int IndividualDeviation,
    IReadOnlyList<(Trait Trait, int Weight)> TraitWeights,
    IReadOnlyList<string> FirstNames,
    IReadOnlyList<string> LastNames);

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
    int ShootOutOfRangePenalty,
    int ShootDistancePenaltyPerCell,
    int ShootAnglePenaltyPerRow,
    float TackleDistanceMaxCells,
    int TackleOutOfReachPenalty,
    int TackleBallCarrierBonus,
    int RetreatDistanceBonusPerCell,
    int RetreatAtHomePenalty);

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

/// <summary>Sesgo de atributos por posición usado en la generación de jugadores (tuning.generation.positionBias).</summary>
public sealed record PositionBiasTable(Attributes Goalkeeper, Attributes Defender, Attributes Midfielder, Attributes Forward);

/// <summary>tuning.generation: parámetros de generación de jugadores (§2.6).</summary>
public sealed record GenerationTuning(PositionBiasTable PositionBias, int LeashBase, IReadOnlyList<int> TraitCountWeights, int GoalkeeperTraitChance);

/// <summary>tuning.leash: conversión de atributo Leash a radio de correa en casillas (§2.6).</summary>
public sealed record LeashTuning(int MinCells, int CellsPer99);

/// <summary>tuning.movement.</summary>
public sealed record MovementTuning(int BaseCellsPerTickMilli, int SpeedCellsPerTickMilliPer99, int DribbleSpeedPercent, int FatigueStartTick, int FatigueMaxSlowPercent);

/// <summary>tuning.ball.</summary>
public sealed record BallTuning(int PassSpeedCellsPerTickMilli, int ShotSpeedCellsPerTickMilli, int LooseBallFrictionPercent);

/// <summary>tuning.states: duraciones de los estados de jugador, en ticks.</summary>
public sealed record StatesTuning(int PassingTicks, int ShootingTicks, int TacklingTicks, int KnockedDownTicks, int CelebratingTicks, int DribbleDuelCooldownTicks, int TackleCooldownTicks);

/// <summary>tuning.pass.</summary>
public sealed record PassTuning(int BaseSuccess, int TechniqueFactor, int DistancePenaltyPerCell, int PressurePenalty, float InterceptRadiusCells, int InterceptBaseChance, int InterceptTechniqueFactor);

/// <summary>tuning.dribble.</summary>
public sealed record DribbleTuning(int BaseWin, int AttackerTechniqueFactor, int DefenderSpeedFactor, int DefenderStrengthFactor, int LostKnockdownTicks);

/// <summary>tuning.shot.</summary>
public sealed record ShotTuning(int BaseQuality, int TechniqueFactor, int StrengthFactor, int DistancePenaltyPerCell, int PressurePenalty, int OffTargetBase, int OffTargetDistanceFactor, int PenaltyQualityBonus);

/// <summary>tuning.save.</summary>
public sealed record SaveTuning(int BasePercent, int CloseRangeCells, int AttributeWeightPercent, int ConsecutiveShotDecayPercent, int QualityWeight);

/// <summary>tuning.tackle.</summary>
public sealed record TackleTuning(int BaseWin, int StrengthFactor, int SpeedFactor, int CarrierTechniqueFactor, int FoulBase, int FoulStrengthFactor, int HardTackleThreshold, int YellowCardBase, int RedCardBase, int HardTackleYellowBonus, int HardTackleRedBonus, bool SecondYellowIsRed);

/// <summary>tuning.injury.</summary>
public sealed record InjuryTuning(int OnTackleBase, int OnFoulBase, int AttackerStrengthFactor, int VictimStaminaResistFactor, int SevereShare);

/// <summary>tuning.referee.</summary>
public sealed record RefereeTuning(int BiasFoulShiftPer10, int PenaltyOnFoulInArea);

/// <summary>tuning.restart.</summary>
public sealed record RestartTuning(int ThrowInTicks, int GoalKickTicks, int CornerTicks, int KickoffTicks, int PenaltyTicks);

/// <summary>tuning.pitch.</summary>
public sealed record PitchTuning(int Columns, int Rows, int AreaColumns, int AreaRows, int GoalRows);

/// <summary>Constantes de resolución del simulador (data/sim/tuning.json), un campo por clave, anidado por sección.</summary>
public sealed record Tuning(
    int TicksPerSecond,
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
    RestartTuning Restart,
    PitchTuning Pitch,
    GenerationTuning Generation,
    LeashTuning Leash);

/// <summary>Conjunto de datos cargado de /data, listo para pasar a Simulator.Run.</summary>
public sealed record Catalog(IReadOnlyList<RaceDefinition> Races, IReadOnlyList<TraitDefinition> Traits, AiWeights Ai, Tuning Tuning)
{
    /// <summary>Busca la definición de la raza id; lanza si no está en el catálogo.</summary>
    public RaceDefinition Race(Race id) =>
        Races.FirstOrDefault(r => r.Id == id) ?? throw new InvalidOperationException($"raza no encontrada en el catálogo: {id}");

    /// <summary>Busca la definición del rasgo id; lanza si no está en el catálogo.</summary>
    public TraitDefinition Trait(Trait id) =>
        Traits.FirstOrDefault(t => t.Id == id) ?? throw new InvalidOperationException($"rasgo no encontrado en el catálogo: {id}");
}
