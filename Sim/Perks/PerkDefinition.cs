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

    /// <summary>
    /// Provoca una lesión sobre el objetivo por el camino normal del motor (paquete AY-cuatro-primitivas,
    /// "Juego sucio"): la misma fórmula, el mismo flujo de dados y la misma escala por acto que una
    /// entrada (<c>MatchEngine.ResolveInjury</c>). Puede acabar en muerte por la vía 1 de RF-093 (una
    /// lesión grave sin tratar que se repite), así que el cargador le impone la misma regla que a
    /// <see cref="PerkDefinition.Lethal"/>: nunca en <c>MATCH_START</c>/<c>PLAY_START</c> (paquete AY,
    /// ADR 0048).
    /// </summary>
    Injure,

    /// <summary>
    /// Mueve al portador a un punto simbólico derivado del estado del partido (paquete
    /// AY-cuatro-primitivas, "Último hombre"): un perk no conoce coordenadas, así que el dato declara una
    /// referencia (<see cref="RelocationPoint"/>) y el motor la resuelve en el instante del efecto.
    /// </summary>
    Relocate,

    /// <summary>
    /// C4 (docs/analisis/perks-catalogo-unificado.md §3.2): escribe sobre uno de los trece escalares de
    /// rasgo de <see cref="Underleague.Sim.Engine.MatchPlayer"/> (Cañón, Kamikaze, Pagar el hierro). Los
    /// trece escalares ya existían —los escriben los rasgos en el constructor—, lo que no existía era el
    /// efecto de perk que los escribe. Vocabulario cerrado en <see cref="TraitScalarKind"/> (RT-032): un
    /// nombre que no esté ahí es un error de carga, no un escalar que el motor ignora en silencio.
    /// </summary>
    ModifyTraitScalar,

    /// <summary>
    /// C8 (docs/analisis/perks-catalogo-unificado.md §3.2): desplaza la casilla-hogar efectiva del
    /// portador (<see cref="Underleague.Sim.Engine.MatchPlayer.EffectiveHome"/>) hacia delante o hacia
    /// atrás respecto al sentido de ataque de su equipo (Línea adelantada, Pivote hondo, Desmarque
    /// profundo). Se suma al desplazamiento de <b>bloque</b> táctico que ya existía; no lo sustituye. El
    /// valor son casillas enteras con signo (positivo = hacia la portería rival, RT-023).
    /// </summary>
    ShiftHome,

    /// <summary>
    /// C8 (docs/analisis/perks-catalogo-unificado.md §3.2): cambia la forma de la zona de acción del
    /// portador (ADR 0028/0029) en una sola dirección (<see cref="ZoneDimension"/>), sin tocar las otras
    /// dos: a diferencia de <c>modifyLeash</c> —que ensancha las tres direcciones por igual— este efecto
    /// deja a un perk decir "más profundidad, sin más anchura" o al revés (Sombra). El valor son casillas
    /// enteras con signo, en la misma escala que <c>modifyLeash</c>.
    /// </summary>
    ModifyZoneShape,

    /// <summary>
    /// C5 (docs/analisis/perks-catalogo-unificado.md §3.2): añade un término a la función de coste del
    /// reparto de marcas (<see cref="Underleague.Sim.Engine.Marking"/>, ADR 0022), que ya tenía una
    /// preferencia de rol. Tres variantes cerradas en <see cref="MarkBiasKind"/>: preferir una etiqueta
    /// concreta de rival (Perro de presa), proteger a un vinculado marcando a quien esté cerca de él
    /// (Guardaespaldas) y encarecer el propio marcaje para CUALQUIER marcador rival (Hombre libre, el
    /// único de los tres que toca el emparejamiento del equipo CONTRARIO).
    /// </summary>
    ModifyMarkBias,

    /// <summary>
    /// C7 (docs/analisis/perks-catalogo-unificado.md §3.2): sesga el criterio con el que un jugador elige
    /// a quién entrar sin balón (ADR 0105), que hoy es siempre el marcado asignado. Dos variantes cerradas
    /// en <see cref="TackleBiasKind"/>: preferir al rival ya derribado (Olfato de sangre) y recordar quién
    /// cometió la última falta para ir a por él (Rabia). No cambia la prioridad del poseedor rival, que
    /// sigue siendo siempre la primera opción (ADR 0105): solo sustituye AL MARCADO cuando no hay balón al
    /// alcance.
    /// </summary>
    ModifyTackleBias,

    /// <summary>
    /// Repite, dentro del MISMO tick, la acción que acaba de disparar este efecto (Doble disparo,
    /// Embestida, Arrollador): solo válido con disparador <c>SHOT</c> o <c>TACKLE</c> (RT-032), que son
    /// las dos únicas resoluciones que sabe repetir <c>MatchEngine.RepeatShot</c>/<c>RepeatTackle</c>. La
    /// repetición vuelve a pasar por <c>EffectEngine.PublishAtDepth</c> con la profundidad ya
    /// incrementada, así que una cadena de acciones extra usa el mismo <c>_maxDepth</c>/<c>RecursionCuts</c>
    /// que cualquier otro evento anidado (RT-042): el corte es el de siempre, y es observable en el
    /// informe.
    /// </summary>
    ExtraAction,
}

/// <summary>
/// Vocabulario cerrado de <see cref="EffectType.ModifyTraitScalar"/> (C4, RT-032): los trece escalares de
/// rasgo de <see cref="Underleague.Sim.Engine.MatchPlayer"/>, que hasta ahora solo escribían los rasgos en
/// el constructor. Un nombre que no esté aquí es un error de carga.
/// </summary>
public enum TraitScalarKind
{
    HardTackleBonus,
    SpeedBonusPercent,
    ShotQualityBonus,
    ShootRangeBonusCells,
    PassQualityBonus,
    FoulChanceBonus,
    InjuryChanceBonus,
    FatigueResistancePercent,
    InjuryResistanceBonus,
    AdjacentTeammateBonusPercent,
    SaveBonusClose,
    SaveBonusFar,
    LeashBonus,
}

/// <summary>
/// Dirección de <see cref="EffectType.ModifyZoneShape"/> (C8), en el mismo marco local que
/// <see cref="Underleague.Sim.Engine.ActionZone"/>: adelante (hacia la portería rival), atrás (hacia la
/// propia) o a los lados.
/// </summary>
public enum ZoneDimension
{
    Forward,
    Back,
    Sides,
}

/// <summary>
/// Variante de <see cref="EffectType.ModifyMarkBias"/> (C5, docs/analisis/perks-catalogo-unificado.md
/// §3.2): sobre qué término de la función de coste de <see cref="Underleague.Sim.Engine.Marking"/> actúa.
/// </summary>
public enum MarkBiasKind
{
    /// <summary>Descuento de coste cuando el candidato lleva la etiqueta declarada (Perro de presa).</summary>
    PreferTag,

    /// <summary>Descuento de coste cuando el candidato está cerca del vinculado que protege (Guardaespaldas).</summary>
    ProtectLinked,

    /// <summary>
    /// Recargo de coste que paga CUALQUIER marcador rival al considerar al portador como candidato
    /// (Hombre libre). Es la única variante que actúa sobre el emparejamiento del equipo CONTRARIO.
    /// </summary>
    Avoided,
}

/// <summary>
/// Variante de <see cref="EffectType.ModifyTackleBias"/> (C7, docs/analisis/perks-catalogo-unificado.md
/// §3.2): qué sustituye al marcado como objetivo de una entrada sin balón (ADR 0105).
/// </summary>
public enum TackleBiasKind
{
    /// <summary>Prefiere entrar al rival que ya está derribado (Olfato de sangre).</summary>
    KnockedDown,

    /// <summary>Recuerda quién cometió la última falta y va a por él (Rabia).</summary>
    Fouled,
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

    /// <summary>No paga la factura de la clínica por una lesión leve propia (RF-094, ADR 0099).</summary>
    MinorInjuryClinicCost,
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

    /// <summary>
    /// 1 si el jugador ha terminado el partido <b>de baja</b> —lesionado o muerto— y 0 si sigue entero.
    /// Es lo único que distingue "sobrevivir" de "jugar", y sin ello un perk que premia terminar de pie
    /// premiaría en realidad salir en la alineación.
    /// </summary>
    Down,
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

    /// <summary>
    /// Los rivales dentro de un radio real de una casilla del portador, en el instante del efecto
    /// (paquete AY-cuatro-primitivas, "Terremoto"). A diferencia de <see cref="Adjacent"/> y
    /// <see cref="AdjacentWithTag"/> —que miran la casilla-hogar fija de la alineación, familia estática
    /// de la ADR 0021— este objetivo mira la <c>Position</c> real en el momento del suceso, la misma
    /// familia dinámica que usa <c>IPerkLinks.NearOpponent</c>. Solo alcanza a rivales.
    /// </summary>
    AdjacentOpponents,

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

    /// <summary>
    /// El dueño es el <b>rival implicado</b> del evento (RF-067), no su actor. Es lo que distingue "lo
    /// que YO le hago al rival" de "lo que le pasa al rival": en un <c>INJURY</c> el actor es la víctima
    /// y el causante viaja en <c>Opponent</c>, así que sin este alcance un perk que cuenta la carne que
    /// reparte cobraría también por la que reparten sus compañeros.
    /// </summary>
    Opponent,

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

/// <summary>
/// Punto simbólico al que reubica un efecto <see cref="EffectType.Relocate"/> (paquete
/// AY-cuatro-primitivas, "Último hombre"): un perk de <c>/data</c> no conoce coordenadas del campo (regla
/// 5 de CLAUDE.md), así que declara una referencia al estado del partido y el motor la resuelve en el
/// instante del efecto. Vocabulario cerrado: cualquier otro valor es un error de carga (RT-032).
/// </summary>
public enum RelocationPoint
{
    /// <summary>
    /// Sobre quien tiene el balón ahora mismo; si nadie lo tiene, el efecto no mueve al portador (un
    /// balón suelto no da un rival al que marcar).
    /// </summary>
    OnBallCarrier,

    /// <summary>A medio camino entre el balón y la portería propia del portador, cortando la línea.</summary>
    BetweenBallAndOwnGoal,
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
    ImmunityKind Immunity = ImmunityKind.Push,
    RelocationPoint RelocationPoint = RelocationPoint.OnBallCarrier,

    /// <summary>Escalar de rasgo que escribe un efecto <see cref="EffectType.ModifyTraitScalar"/> (C4).</summary>
    TraitScalarKind Scalar = TraitScalarKind.HardTackleBonus,

    /// <summary>Dirección que cambia un efecto <see cref="EffectType.ModifyZoneShape"/> (C8).</summary>
    ZoneDimension ZoneDimension = ZoneDimension.Forward,

    /// <summary>Variante de un efecto <see cref="EffectType.ModifyMarkBias"/> (C5).</summary>
    MarkBiasKind MarkBias = MarkBiasKind.PreferTag,

    /// <summary>Etiqueta de rival preferida por <see cref="MarkBiasKind.PreferTag"/> (C5).</summary>
    string MarkTag = "",

    /// <summary>Variante de un efecto <see cref="EffectType.ModifyTackleBias"/> (C7).</summary>
    TackleBiasKind TackleBias = TackleBiasKind.KnockedDown);

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

    /// <summary>
    /// True si el perk se cuelga de un evento de <b>contacto</b>: la entrada (que incluye el bloqueo,
    /// publicado también como <c>TACKLE</c>), la falta y la lesión. Es la propiedad que decide a quién
    /// puede matar un perk letal —solo al rival que está en la jugada, ver <c>EffectEngine</c>— y por
    /// tanto también qué dice de él la descripción generada (RT-035). Paquete AY: desde él, el cargador
    /// prohíbe que un letal se cuelgue de <c>MATCH_START</c> o <c>PLAY_START</c>.
    /// </summary>
    public static bool IsContactTrigger(EventType trigger) =>
        trigger is EventType.Tackle or EventType.Foul or EventType.Injury;

    /// <summary>True si este perk mata, y solo puede matar, al rival implicado en la jugada.</summary>
    public bool IsContactLethal => Lethal && IsContactTrigger(Trigger);
}
