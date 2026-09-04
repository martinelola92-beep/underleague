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

/// <summary>Catálogo cerrado de tipos de efecto de fase 1 (docs/fase1-diseno.md §2).</summary>
public enum EffectType
{
    ModifyAttribute,
    ModifyLeash,
    ModifyBias,
    ModifyProbability,
    CancelEvent,
    AddCounter,
    SetState,
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
    int Ticks = 0);

/// <summary>
/// Un perk cargado de <c>data/perks/&lt;id&gt;.json</c> (RT-033). Es un dato puro: no contiene código,
/// solo una condición NCalc ya compilada (RT-034) y una lista ordenada de efectos.
/// </summary>
/// <param name="ElseEffects">
/// Efectos aplicados cuando la condición es falsa (§7, antisinergias declaradas). Lista vacía = el perk
/// no hace nada si la condición no se cumple.
/// </param>
public sealed record PerkDefinition(
    string Id,
    LocalizedName Name,
    Rarity Rarity,
    PerkKind Kind,
    EventType Trigger,
    PerkScope Scope,
    string Condition,
    CompiledCondition CompiledCondition,
    IReadOnlyList<EffectDefinition> Effects,
    IReadOnlyList<EffectDefinition> ElseEffects,
    LimitDefinition? Limit,
    bool AccumulatesAcrossMatches,
    bool Lethal,
    Position? PositionOnly,
    IReadOnlyList<string> TagsRequired,
    IReadOnlyList<string> TagsForbidden);
