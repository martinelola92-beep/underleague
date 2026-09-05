using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Perks;

/// <summary>Tipo de perk (RF-069): relleno, condicional o rompe-reglas. Distribución objetivo 60/30/10.</summary>
public enum PerkKind
{
    Filler,
    Conditional,
    RuleBreaker,
}

/// <summary>Catálogo cerrado de tipos de efecto (docs/fase1-diseno.md §2, fase1b-diseno.md §1.4).</summary>
public enum EffectType
{
    ModifyAttribute,
    ModifyLeash,
    ModifyBias,
    ModifyProbability,
    CancelEvent,
    AddCounter,
    SetState,

    /// <summary>Alarga (o acorta) el derribo que provocan las entradas del objetivo (ADR 0026, Orcos).</summary>
    ModifyKnockdownTicks,

    /// <summary>Concede una inmunidad al objetivo (ADR 0026, Enanos y No-muertos). Ver <see cref="ImmunityKind"/>.</summary>
    Immunity,

    /// <summary>Modifica la experiencia que gana el portador **fuera** del partido (ADR 0026, Humanos).</summary>
    ModifyExperience,
}

/// <summary>
/// Eje de activación de un perk (<c>docs/perks-ejes.md</c>): de qué depende que se active, ortogonal a
/// <see cref="PerkKind"/>, que mide potencia. El catálogo se vigila con la distribución objetivo de ese
/// documento.
/// </summary>
public enum PerkAxis
{
    Identity,
    Accumulation,
    Alignment,
    StartZone,
    Geometry,
    MatchState,
    Composition,
    Proximity,
}

/// <summary>
/// Relación direccional entre casillas-hogar (RF-044, ADR 0021), en coordenadas **relativas al sentido
/// de ataque**: "adelante" es hacia la portería rival e "izquierda"/"derecha" se toman desde un jugador
/// que mira hacia ella, así que el visitante refleja columnas y bandas.
/// </summary>
public enum LinkRelation
{
    /// <summary>Misma columna, fila contigua: pareja de centrales, doble pivote.</summary>
    Beside,

    /// <summary>Columna contigua en el sentido de ataque, fila igual o contigua.</summary>
    Ahead,

    /// <summary>Columna contigua en el sentido contrario, fila igual o contigua.</summary>
    Behind,

    /// <summary>Fila contigua hacia la banda izquierda: el compañero de tu banda.</summary>
    Left,

    /// <summary>Fila contigua hacia la banda derecha.</summary>
    Right,

    /// <summary>Columna y fila contiguas, hacia adelante.</summary>
    DiagonalAhead,

    /// <summary>Columna y fila contiguas, hacia atrás.</summary>
    DiagonalBehind,
}

/// <summary>
/// Inmunidad concedida por un efecto <see cref="EffectType.Immunity"/> (ADR 0026). Cada una la consume
/// un sistema distinto: <see cref="Push"/> el motor de cuerpos (ADR 0020), las otras dos la capa de
/// campaña entre partidos.
/// </summary>
public enum ImmunityKind
{
    /// <summary>No puede ser desplazado por la separación de cuerpos ni por el empuje de una entrada.</summary>
    Push,

    /// <summary>No entra en duelo cuando un vinculado muere, se vende o queda con lesión grave (RF-104).</summary>
    Mourning,

    /// <summary>La lesión leve no le penaliza los atributos entre partidos (RF-035).</summary>
    MinorInjuryPenalty,
}

/// <summary>
/// Estadística del partido en curso que expone la función de condición <c>stat</c> (perks-ejes.md). Son
/// las que el motor ya lleva para el informe post-partido (RF-119): un perk de acumulación no necesita
/// declarar su propio contador para leerlas.
/// </summary>
public enum MatchStat
{
    Goals,
    PassesCompleted,
    TacklesWon,
    Shots,
    Saves,
}

/// <summary>A quién se aplica un efecto (§2). Los objetivos colectivos se recorren por id ascendente.</summary>
public enum EffectTarget
{
    Actor,
    Target,
    Opponent,
    Owner,
    Adjacent,
    Team,
    OpposingTeam,
    WithTag,
    AdjacentWithTag,

    /// <summary>Los vinculados del portador en las relaciones que declara el perk (ADR 0021).</summary>
    Linked,

    /// <summary>Los vinculados del portador que además llevan una etiqueta concreta.</summary>
    LinkedWithTag,
}

/// <summary>Duración de un modificador (§2). En fase 1 <c>Run</c> se comporta como <c>Match</c> dentro del partido.</summary>
public enum EffectDuration
{
    Instant,
    Play,
    Match,
    Run,
}

/// <summary>Ámbito de un límite de activaciones (§2).</summary>
public enum LimitScope
{
    Play,
    Match,
    Mob,
    Run,
}

/// <summary>A quién debe corresponder el evento para que el perk del dueño se evalúe (RF-065, §2).</summary>
public enum PerkScope
{
    Actor,
    Target,
    Team,
    OpposingTeam,
    Any,
}

/// <summary>Resolución probabilística sobre la que actúa <c>modifyProbability</c> (§2).</summary>
public enum ProbabilityKind
{
    Foul,
    Card,
    Injury,
    Injure,
    SevereInjury,
    Pass,
    Intercept,
    Dribble,
    Tackle,
    ShotOnTarget,
    Save,

    /// <summary>Resistencia del conductor a que le roben el balón en una entrada (ADR 0026, Elfos).</summary>
    TackleEvasion,

    /// <summary>Resistencia del pasador a que le intercepten el pase (ADR 0026, Elfos).</summary>
    InterceptEvasion,
}

/// <summary>Límite de activaciones de un perk (§2): <c>times</c> veces por <c>per</c>.</summary>
public sealed record LimitDefinition(LimitScope Per, int Times);

/// <summary>
/// Un efecto de un perk (§2). Los campos que no aplican a un <see cref="Type"/> concreto quedan en su
/// valor por defecto; el cargador valida qué combinaciones son legales, así que el motor puede leerlos
/// sin comprobaciones adicionales.
/// </summary>
/// <param name="UsesCounter">
/// True si el valor se calcula como <c>ValuePerCounter * counter(Counter) / CounterDivisor</c> acotado a
/// <c>MaxValue</c>, en vez de con <see cref="Value"/>.
/// </param>
public sealed record EffectDefinition(
    EffectType Type,
    EffectTarget Target = EffectTarget.Owner,
    string TargetTag = "",
    AttributeKind Attribute = AttributeKind.Strength,
    int Value = 0,
    bool UsesCounter = false,
    int ValuePerCounter = 0,
    string Counter = "",
    int MaxValue = 0,
    int CounterDivisor = 1,
    ProbabilityKind Probability = ProbabilityKind.Foul,
    EffectDuration Duration = EffectDuration.Instant,
    PlayerState State = PlayerState.KnockedDown,
    int Ticks = 0,
    ImmunityKind Immunity = ImmunityKind.Push);

/// <summary>
/// Un perk cargado de <c>data/perks/&lt;id&gt;.json</c> (RT-033). Es un dato puro: no contiene código,
/// solo una condición NCalc ya compilada (RT-034) y una lista ordenada de efectos.
/// </summary>
/// <param name="ElseEffects">
/// Efectos aplicados cuando la condición es falsa (§7, antisinergias declaradas). Lista vacía = el perk
/// no hace nada si la condición no se cumple.
/// </param>
/// <param name="Axis">Eje de activación (docs/perks-ejes.md); se declara en el dato, no se deduce.</param>
/// <param name="Race">
/// null = perk universal; una raza = perk exclusivo (ADR 0023). Es restricción de **aparición** (qué
/// entra en el pool de una run), no de asignación.
/// </param>
/// <param name="Links">
/// Relaciones direccionales que el perk necesita (RF-044, ADR 0021). Se resuelven una sola vez al
/// construir el partido y habilitan los objetivos <c>linked</c> y <c>linkedWithTag:&lt;Tag&gt;</c>.
/// </param>
/// <param name="MinAct">
/// Acto nativo (ADR 0051): a partir de qué acto empieza a aparecer en el pool de recompensas y de
/// mercado. Por debajo solo sale <b>fuera de profundidad</b>, con un peso pequeño, y un maestro ni
/// siquiera eso.
/// </param>
/// <param name="Frequency">
/// El "commonness" de Angband (ADR 0051): cuánto sale este perk comparado con uno normal, en porcentaje.
/// Multiplica al peso por valor de la ADR 0038 y a la curva de profundidad; no sustituye a ninguno.
/// </param>
/// <param name="Family">Línea del catálogo a la que pertenece (ADR 0051); cadena vacía = perk suelto.</param>
/// <param name="Requires">Lo que exige para poder cobrarse; null = no es un maestro (ADR 0051).</param>
/// <param name="Blocks">Lo que cierra de forma permanente en la run al aceptarlo (ADR 0051).</param>
public sealed record PerkDefinition(
    string Id,
    LocalizedName Name,
    Rarity Rarity,
    PerkKind Kind,
    PerkAxis Axis,
    Underleague.Sim.Model.Race? Race,
    IReadOnlyList<LinkRelation> Links,
    EventType Trigger,
    PerkScope Scope,
    string Condition,
    CompiledCondition CompiledCondition,
    IReadOnlyList<EffectDefinition> Effects,
    IReadOnlyList<EffectDefinition> ElseEffects,
    LimitDefinition? Limit,
    bool AccumulatesAcrossMatches,
    bool Lethal,
    int LethalChance,
    Position? PositionOnly,
    IReadOnlyList<string> TagsRequired,
    IReadOnlyList<string> TagsForbidden,
    int MinAct,
    int Frequency,
    string Family,
    MasterRequirement? Requires,
    PerkBlock Blocks)
{
    /// <summary>
    /// True si es un perk <b>maestro</b> (ADR 0051): exige llevar ya varios perks de su línea y cierra
    /// otras de forma permanente. Son entre el 5% y el 10% del catálogo; si crecen más, el catálogo deja
    /// de ser un roguelite de piezas sueltas y se convierte en un árbol de talentos.
    /// </summary>
    public bool IsMaster => Requires is not null;

    /// <summary>True si el perk pertenece a alguna línea del catálogo (ADR 0051).</summary>
    public bool HasFamily => Family.Length > 0;
}
