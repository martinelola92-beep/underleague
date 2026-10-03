using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Items;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Run;

/// <summary>División de la liga (RF-128). La fase 2 juega siempre en <see cref="Third"/>.</summary>
public enum Division
{
    Third,
    Second,
    First,
    Continental,
    World,
}

/// <summary>Tipo de vínculo entre dos jugadores (RF-101, RF-102). No hay vínculos negativos (I-3, v0.9.1).</summary>
public enum BondKind
{
    Partnership,
    BloodDebt,
    Stonewall,
}

/// <summary>Vínculo de un jugador con otro. Sin signo: no existen vínculos negativos en el lanzamiento (I-3).</summary>
public sealed record RunBond(int OtherPlayerId, BondKind Kind);

/// <summary>
/// Historial de carrera de un jugador de la run (RF-122, ADR 0124): vocabulario cerrado de hechos
/// acumulados partido a partido, que <b>nunca topa ni se reinicia</b> -a diferencia de
/// <see cref="RunPlayer.Counters"/>, gobernado por la semántica de perk (RF-070, topes <c>maxValue</c>).
/// Mezclar los dos ciclos de vida en la misma bolsa habría dejado que una regla futura de perk truncara
/// el historial en silencio (ADR 0124, decisión 1). Aritmética entera (RT-023).
/// <see cref="Revenges"/> (ADR 0165, versión 8 del esquema): veces que este jugador ha lesionado o matado a
/// un némesis del rival; alimenta el apodo «el Vengador» (ADR 0163).
/// </summary>
public sealed record RunCareer(
    int Matches,
    int Goals,
    int Assists,
    int Tackles,
    int TacklesWon,
    int Fouls,
    int Cards,
    int InjuriesCaused,
    int DeathsCaused,
    int InjuriesSuffered,
    int TicksOnPitch,
    int Revenges = 0)
{
    /// <summary>Instancia vacía compartida: el caso normal es no haber jugado ningún partido todavía.</summary>
    public static RunCareer None { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>Prótesis instalada en un jugador (RF-095). Fase 3; el campo existe desde la versión 1 del esquema.</summary>
public sealed record RunProsthesis(string Slot, string Effect);

/// <summary>Modo de uso de un consumible equipado (RF-080..082).</summary>
public enum ConsumableMode
{
    Manual,
    Conditional,
}

/// <summary>
/// Consumible que la run lleva en uno de sus <see cref="RunRules.ConsumableSlots"/> huecos (RF-080..082, ADR
/// 0172). El hueco <b>es</b> la posesión: no hay inventario de consumibles sueltos. Entra <see cref="ConsumableMode.Manual"/>
/// (un clic en el tablero de la retransmisión) y el jugador puede pasarlo a condicional en Equipo.
/// </summary>
public sealed record EquippedConsumable(string Id, ConsumableMode Mode, string Trigger);

/// <summary>Árbitro de la run (RF-061, RF-064c). <c>BribesReceived</c> es progresión de fase 3.</summary>
public sealed record RunReferee(int Id, string Name, RefereeTrait Trait, int BribesReceived)
{
    /// <summary>
    /// Id de la ficha en <c>data/referees/referees.json</c> (ADR 0158): permite releer nombre y muletilla
    /// en el idioma pedido (<c>Sim.Run.View.RefereeView</c>) sin volver a guardar texto bilingüe aquí.
    /// Cadena vacía cuando el árbitro no viene de datos (<c>DefaultRunSystems</c>, paquete W).
    /// </summary>
    public string DefinitionId { get; init; } = string.Empty;

    /// <summary>
    /// Memoria del árbitro contra el jugador (ADR 0158, RF-061, revisión independiente): acotada a
    /// <c>tuning.referee.memory.memoryCap</c> (±40 hoy), positivo a favor del jugador. Refleja SOLO la
    /// conducta propia: al terminar un partido que pitó decae en proporción a lo que el árbitro desplazó
    /// EN CONTRA del jugador mientras hubo árbitro (<c>MatchReport.BiasShiftedAgainst[0]</c>, nunca lo que
    /// hizo el rival, ni el −20 de arranque del casero, ni la turba, que no tiene árbitro), y sube un bono
    /// fijo si el partido terminó limpio. El siguiente partido con este árbitro empieza con esta memoria
    /// (<see cref="RunSystems.RefereeFor"/>). Se llamó <c>Grudge</c> hasta que ese nombre se reservó para
    /// la represalia de la ADR 0145.
    /// </summary>
    public int Memory { get; init; }

    /// <summary>
    /// Media banda que no ve si es <see cref="RefereeTrait.OneEyed"/> (ADR 0158); <see cref="RefereeSide.None"/>
    /// en cualquier otro rasgo. Copiado de la ficha de datos al elegir el plantel de la run.
    /// </summary>
    public RefereeSide BlindSide { get; init; } = RefereeSide.None;
}

/// <summary>Resultado con el que se cerró un nodo del historial.</summary>
public enum NodeResult
{
    /// <summary>Nodo no de partido, resuelto sin ganar ni perder.</summary>
    Completed,

    /// <summary>Partido ganado.</summary>
    Won,

    /// <summary>Partido perdido.</summary>
    Lost,
}

/// <summary>Entrada del historial de nodos (RT-030).</summary>
public sealed record NodeHistoryEntry(int NodeId, NodeKind Kind, NodeResult Result);

/// <summary>Dónde está la run: en el mapa eligiendo nodo, dentro de un nodo interactivo, o terminada.</summary>
public enum RunPhase
{
    /// <summary>En el mapa: <see cref="RunEngine.AvailableNodes"/> devuelve los nodos elegibles.</summary>
    OnMap,

    /// <summary>Dentro de un nodo interactivo (mercado, clínica, entrenamiento, evento, recompensa).</summary>
    NodeOpen,

    /// <summary>La run ha terminado, en victoria o en derrota.</summary>
    Finished,
}

/// <summary>Estado de la run (RF-002).</summary>
public enum RunOutcomeKind
{
    InProgress,
    Victory,
    Defeat,
}

/// <summary>
/// Causa de derrota. Solo hay dos (RF-002b) y este enum no crece sin cambiar esa regla del juego.
/// </summary>
public enum DefeatCause
{
    /// <summary>La run no ha terminado en derrota.</summary>
    None,

    /// <summary>Se perdió un partido de jefe (RF-002b).</summary>
    BossMatchLost,

    /// <summary>Los jugadores disponibles bajaron de 5 (RF-002b), dentro o fuera de un partido.</summary>
    NotEnoughPlayers,
}

/// <summary>
/// Desenlace de la run.
/// </summary>
/// <param name="Kind">En curso, victoria o derrota (RF-002, RF-002b).</param>
/// <param name="Cause">Causa de la derrota; <see cref="DefeatCause.None"/> si no la hay.</param>
/// <param name="NodeId">Nodo en el que se decidió; -1 si no aplica.</param>
/// <param name="Tick">
/// Tick del partido en el que se decidió, o -1 si ocurrió fuera de un partido. Con
/// <see cref="DefeatCause.NotEnoughPlayers"/> es el tick exacto de la baja que terminó la run: RF-002b
/// dice "al instante", así que el render puede cortar la reproducción justo ahí.
/// </param>
public sealed record RunOutcome(RunOutcomeKind Kind, DefeatCause Cause = DefeatCause.None, int NodeId = -1, int Tick = -1)
{
    /// <summary>Run en curso.</summary>
    public static RunOutcome InProgress { get; } = new(RunOutcomeKind.InProgress);

    /// <summary>True si la run ha terminado, con victoria o con derrota.</summary>
    public bool IsOver => Kind != RunOutcomeKind.InProgress;
}

/// <summary>
/// Un jugador de la plantilla de la run (RT-030, <c>modelo-datos.md</c>). Un objeto por jugador, con
/// los cinco atributos (I-1), un único objeto equipado (I-2) y vínculos sin signo (I-3).
/// Inmutable: se modifica con <c>with</c> o con los métodos <c>With*</c>.
/// </summary>
public sealed record RunPlayer(
    int Id,
    string Name,
    Race Race,
    Position Position,
    Rarity Rarity,
    int Level,
    int Experience,
    Attributes Attributes,
    IReadOnlyList<Trait> Traits,
    IReadOnlyList<string> Tags,
    PhysicalState PhysicalState)
{
    /// <summary>Diccionario entero vacío compartido: el caso normal es no tener contadores.</summary>
    internal static readonly IReadOnlyDictionary<string, int> NoCounters =
        new SortedDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Dorsal del jugador, <b>fijo toda la run</b> (BX-4): lo recibe al entrar en la plantilla
    /// (<see cref="RunState.WithNewPlayer"/>, <see cref="RunState.WithRoster"/>) y no cambia por puesto, por
    /// partido ni porque entre o salga alguien. Es el primero libre ≥ 1 de la plantilla (los muertos siguen en ella,
    /// así que su número no se reasigna). 0 = sin asignar todavía, un estado transitorio que ningún embudo deja salir.
    /// </summary>
    public int ShirtNumber { get; init; }

    /// <summary>Etiqueta de especie, fija por raza (ADR 0024).</summary>
    public string SpeciesTag { get; init; } = string.Empty;

    /// <summary>Etiqueta de estilo individual, sorteada al generar el jugador (ADR 0024).</summary>
    public StyleTag StyleTag { get; init; } = StyleTag.Neutral;

    /// <summary>Perks asignados, por id. El máximo depende de la rareza (RF-023, Progression.PerkSlots).</summary>
    public IReadOnlyList<string> Perks { get; init; } = Array.Empty<string>();

    /// <summary>Objeto equipado, o null. Un único objeto por jugador (RF-076, I-2).</summary>
    public string? Item { get; init; }

    /// <summary>Lesiones leves acumuladas (RF-091). Cada una resta <see cref="RunRules.MinorInjuryPenaltyPercent"/>% a los atributos.</summary>
    public int MinorInjuries { get; init; }

    /// <summary>Prótesis instaladas (RF-095). Fase 3.</summary>
    public IReadOnlyList<RunProsthesis> Prostheses { get; init; } = Array.Empty<RunProsthesis>();

    /// <summary>Salario por partido; 0 salvo mercenarios (RF-111).</summary>
    public int Wage { get; init; }

    /// <summary>True si es mercenario (RF-110..113): otra raza, no forma vínculos, cuenta como Stranger.</summary>
    public bool IsMercenary { get; init; }

    /// <summary>True si entró como canterano (RF-114b/c): +33% de experiencia.</summary>
    public bool IsYouth { get; init; }

    /// <summary>Partidos seguidos sin jugar. Los mercenarios abandonan tras 3 (RF-111).</summary>
    public int MatchesBenched { get; init; }

    /// <summary>Vínculos, máximo 2 (RF-101, RF-102).</summary>
    public IReadOnlyList<RunBond> Bonds { get; init; } = Array.Empty<RunBond>();

    /// <summary>Partidos que le quedan de duelo, 0 si no aplica (RF-104).</summary>
    public int Mourning { get; init; }

    /// <summary>Contadores de perks acumulados entre partidos (RF-070). Ordenado por clave ordinal.</summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = NoCounters;

    /// <summary>Contadores parciales de progreso de vínculo (asistencias A-&gt;B, etc.). Ordenado por clave ordinal.</summary>
    public IReadOnlyDictionary<string, int> BondProgress { get; init; } = NoCounters;

    /// <summary>
    /// Historial de carrera acumulado (RF-122, ADR 0124): vocabulario cerrado, vive lo que vive el
    /// jugador y no se topa nunca -a diferencia de <see cref="Counters"/>. Alimenta el futuro obituario
    /// (RF-122); esta ADR solo lo acumula, no lo presenta.
    /// </summary>
    public RunCareer Career { get; init; } = RunCareer.None;

    /// <summary>
    /// True si el jugador puede alinearse: sano o con lesión leve. La lesión grave impide jugar hasta
    /// recibir tratamiento (RF-092) y el muerto no vuelve (RF-093). Es el predicado que cuenta para el
    /// mínimo de 5 de RF-002b/RF-002e.
    /// </summary>
    public bool IsAvailable => PhysicalState is PhysicalState.Healthy or PhysicalState.MinorInjury;

    /// <summary>
    /// Lisiado (ADR 0187, decisión del revisor del 3 oct 2026): un jugador con <see cref="RunRules.MaxProstheses"/>
    /// prótesis que sufre una lesión grave ya no tiene cura —ni médico, ni matasanos, ni herrero, ni tarifa
    /// plana— y no puede alinearse, ni siquiera arriesgándose (RF-093 vía 1). Sigue en la plantilla: vínculos,
    /// venta a precio de lesión grave. <b>Derivado</b>, sin campo propio: es grave con el tope de prótesis, de
    /// modo que no puede desincronizarse del estado y un guardado anterior lo calcula al cargar.
    /// </summary>
    public bool IsCrippled => PhysicalState == PhysicalState.SevereInjury && Prostheses.Count >= RunRules.MaxProstheses;

    /// <summary>Copia con otro estado físico.</summary>
    public RunPlayer WithPhysicalState(PhysicalState state) => this with { PhysicalState = state };

    /// <summary>Copia con la experiencia indicada.</summary>
    public RunPlayer WithExperience(int experience) => this with { Experience = experience };

    /// <summary>Copia con los contadores indicados, ordenados por clave ordinal.</summary>
    public RunPlayer WithCounters(IEnumerable<KeyValuePair<string, int>> counters)
    {
        ArgumentNullException.ThrowIfNull(counters);
        var sorted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in counters)
        {
            sorted[name] = value;
        }

        return this with { Counters = sorted };
    }

    /// <summary>Copia con el progreso de vínculo indicado, ordenado por clave ordinal.</summary>
    public RunPlayer WithBondProgress(IEnumerable<KeyValuePair<string, int>> progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var sorted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in progress)
        {
            sorted[name] = value;
        }

        return this with { BondProgress = sorted };
    }

    /// <summary>
    /// Copia sumando al historial de carrera las estadísticas de un partido recién jugado (RF-122, ADR
    /// 0124). Aritmética entera (RT-023); <see cref="RunCareer.Matches"/> solo sube si el jugador llegó a
    /// pisar el campo (<see cref="PlayerMatchStats.TicksOnPitch"/> &gt; 0).
    /// </summary>
    public RunPlayer WithCareerFrom(PlayerMatchStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var career = Career;
        return this with
        {
            Career = career with
            {
                Matches = career.Matches + (stats.TicksOnPitch > 0 ? 1 : 0),
                Goals = career.Goals + stats.Goals,
                Assists = career.Assists + stats.Assists,
                // ADR 0125 D1 separa la MÉTRICA, no la memoria: la carrera sigue contando las dos
                // entradas juntas, que es lo que contaba antes. Estrechar lo que una run recuerda de un
                // jugador sería una decisión de diseño sobre RF-122 —y pasa por game-design-review—, no un
                // efecto colateral de arreglar un instrumento de medida.
                Tackles = career.Tackles + stats.Tackles + stats.OffBallTackles,
                TacklesWon = career.TacklesWon + stats.TacklesWon,
                Fouls = career.Fouls + stats.Fouls,
                Cards = career.Cards + stats.Cards,
                InjuriesCaused = career.InjuriesCaused + stats.InjuriesCaused,
                DeathsCaused = career.DeathsCaused + stats.DeathsCaused,
                InjuriesSuffered = career.InjuriesSuffered + (stats.Injured ? 1 : 0),
                TicksOnPitch = career.TicksOnPitch + stats.TicksOnPitch,
            },
        };
    }

    /// <summary>
    /// Convierte el jugador de la run en la definición que consume <c>Simulator.Run</c>, aplicando la
    /// penalización de las lesiones leves (RF-091: -15% a todos los atributos, acumulable) salvo que el
    /// jugador sea inmune a ella (efecto <c>immunity</c>, ADR 0026: los no-muertos, RF-035). Los
    /// atributos se acotan a 1..99 con <see cref="Attributes.Clamp"/>.
    /// <para><paramref name="applyMinorInjuryPenalty"/> a false devuelve los atributos de la plantilla
    /// sin tocar: es lo que necesita la progresión de después del partido, que no debe guardar la
    /// penalización dentro de los atributos permanentes del jugador.</para>
    /// </summary>
    public PlayerDefinition ToDefinition(Catalog catalog, bool applyMinorInjuryPenalty = true)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var definition = new PlayerDefinition(
            Id, Name, Race, Position, Rarity, Level, Attributes, Traits, Tags, PhysicalState)
        {
            SpeciesTag = SpeciesTag,
            StyleTag = StyleTag,
            Perks = Perks,
            Counters = Counters,
            ShirtNumber = ShirtNumber,
        };

        if (!applyMinorInjuryPenalty
            || MinorInjuries <= 0
            || ProgressionRules.HasImmunity(definition, catalog, Underleague.Sim.Perks.ImmunityKind.MinorInjuryPenalty))
        {
            return definition;
        }

        int percent = 100 - (RunRules.MinorInjuryPenaltyPercent * MinorInjuries);
        var a = Attributes;
        return definition with
        {
            Attributes = Attributes.Clamp(new Attributes(
                a.Strength * percent / 100,
                a.Speed * percent / 100,
                a.Technique * percent / 100,
                a.Stamina * percent / 100,
                a.Leash)),
        };
    }

    /// <summary>
    /// Construye un jugador de run a partir de una definición generada (<c>TeamGenerator</c>,
    /// <c>PlayerGenerator</c>). Es el puente que usan el arranque de la run y el mercado del paquete X.
    /// </summary>
    public static RunPlayer From(PlayerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new RunPlayer(
            definition.Id,
            definition.Name,
            definition.Race,
            definition.Position,
            definition.Rarity,
            definition.Level,
            0,
            definition.Attributes,
            definition.Traits,
            definition.Tags,
            definition.PhysicalState)
        {
            SpeciesTag = definition.SpeciesTag,
            StyleTag = definition.StyleTag,
            Perks = definition.Perks,
            Counters = definition.Counters,
            ShirtNumber = definition.ShirtNumber,
        };
    }
}

/// <summary>
/// Constantes de regla del bucle de run que no viven en <c>/data</c> porque no son ajustes de balance
/// sino reglas de <c>docs/requisitos.md</c>. Las que sí son de balance (oro, precios, objetivos de
/// partido excelente) las trae el paquete X en <c>data/economy/</c>.
/// </summary>
public static class RunRules
{
    /// <summary>Jugadores disponibles por debajo de los cuales la run termina en derrota (RF-002b).</summary>
    public const int MinimumAvailablePlayers = 5;

    /// <summary>
    /// Huecos de consumible (RF-080, ADR 0172; antes «hasta 3»): lo que la run lleva encima, y por tanto lo
    /// que llega al partido. Comprar o recibir un consumible exige un hueco libre y lo deja ya en él. Lo
    /// valida <c>RunEngine.Apply(SetConsumables)</c>; la constante existe para que el mercado, los eventos,
    /// la política automática y la pantalla de Equipo no repitan el número a mano.
    /// </summary>
    public const int ConsumableSlots = 2;

    /// <summary>
    /// Capacidad base (RF-020, ADR 0046): un hueco más que la plantilla inicial de 9 (RF-005), para poder
    /// fichar en el primer mercado. No es un mínimo ni un objetivo: es el <b>techo de partida</b>. Crecer
    /// por encima exige un hueco, y el único que los vende es el nodo de inscripción.
    /// </summary>
    public const int BaseRosterSize = 10;

    /// <summary>
    /// Plantilla máxima (RF-020, ADR 0046): 12. Con el mínimo de 5 de RF-002b, el margen de una run
    /// entera son cinco bajas —siete si se compran los dos huecos—, que es lo que hace del desgaste un
    /// recurso y no un contador que nunca llega a cero (ADR 0045).
    /// </summary>
    public const int MaxRosterSize = 12;

    /// <summary>Huecos de inscripción comprables en una run: los que van de la plantilla base al techo.</summary>
    public const int MaxEnrollmentSlots = MaxRosterSize - BaseRosterSize;

    /// <summary>Titulares máximos de un equipo (RF-059; el simulador exige entre 5 y 7).</summary>
    public const int MaxStarters = 7;

    /// <summary>Actos por run (RF-001).</summary>
    public const int Acts = 3;

    /// <summary>
    /// Penalización de atributos por cada lesión leve acumulada, en porcentaje (RF-091). Es una regla
    /// del documento de requisitos, no un dial de balance; si algún día hay que ajustarla, se mueve a
    /// <c>data/</c> con un ADR, no se cambia aquí en silencio (RT-057).
    /// </summary>
    public const int MinorInjuryPenaltyPercent = 15;

    /// <summary>
    /// Tope de prótesis por jugador (ADR 0187, decisión del revisor): la tercera lo vuelve <c>Automaton</c>
    /// (RF-095c) y a partir de ahí una lesión grave lo deja lisiado (<see cref="RunPlayer.IsCrippled"/>).
    /// </summary>
    public const int MaxProstheses = 3;

    /// <summary>Experiencia extra de un canterano, en porcentaje (RF-114c).</summary>
    public const int YouthExperienceBonusPercent = 33;

    /// <summary>
    /// Rasgos máximos por jugador (RF-022c). El efecto <c>grantTrait</c> de eventos (ADR 0159) es el
    /// primer sitio fuera de la generación de jugadores que puede toparlo, así que sube aquí de constante
    /// implícita de la generación a regla nombrada del bucle de run.
    /// </summary>
    public const int MaxTraits = 3;
}

/// <summary>
/// Estado versionado de una run (RT-030), según <c>docs/modelo-datos.md</c>. Inmutable: cada cambio
/// devuelve una copia con los métodos <c>With*</c>. Se serializa con <see cref="Save.RunSave"/>.
///
/// <para>Todo lo que afecta al resultado se guarda en listas o diccionarios ordenados: la plantilla va
/// ordenada por id ascendente y los diccionarios son <c>SortedDictionary</c> ordinal, para que dos
/// estados equivalentes se serialicen byte a byte igual (RT-021, orden determinista).</para>
/// </summary>
public sealed record RunState
{
    /// <summary>
    /// Versión del esquema del estado de la run (RT-030, RT-060). Sube con cualquier cambio de forma;
    /// una run guardada con otra versión no se migra en silencio (<c>modelo-datos.md</c>, "Versionado").
    /// Versión 1: primera con código (la 0 era el borrador sin implementar).
    /// </summary>
    // 2 (ADR 0097): desaparece NodeKind.Enrollment, que se serializaba por nombre.
    // 3 (ADR 0103): el campo pasa de 5 a 6 filas; las casillas de la alineación (0..4) pasan a 0..5.
    // 4 (sucesora de la ADR 0103): el campo pasa de 6 a 7 filas; las casillas de la alineación (0..5)
    // pasan a 0..6.
    // 5 (ADR 0124): cada jugador gana un objeto "career" con su historial de carrera acumulado.
    // 6 (ADR 0158): cada árbitro gana definitionId, grudge y blindSide.
    // 7 (ADR 0157): el estado gana la apuesta tomada del nodo pendiente (bet, null si no hay).
    // 8 (ADR 0165): el estado gana la memoria de los clanes rivales (rivalMemory: vacantes y némesis) y la
    // carrera de cada jugador gana "revenges".
    // 9 (ADR 0183): el guardado puede llevar un "pendingMatch" (partido a medias y sus decisiones) junto al estado
    // de antes de ese partido. Sólo añade un campo opcional: la 8 sigue leyéndose (RunSave.MinimumReadableVersion).
    // 10 (BX-4): cada jugador gana "shirtNumber", su dorsal fijo de la run. Los guardados de la 8 y la 9 no lo traen y
    // se numeran al cargar por id ascendente (RunState.WithRoster): migración explícita, sin pérdida.
    public const int CurrentSchemaVersion = 10;

    /// <summary>Versión de esquema con la que se creó este estado.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Semilla de la run: de ella salen todos los flujos de RNG (RT-022).</summary>
    public ulong Seed { get; init; }

    /// <summary>División en la que se juega la run (RF-128).</summary>
    public Division Division { get; init; } = Division.Third;

    /// <summary>Id del club inicial (RF-004).</summary>
    public string ClubId { get; init; } = string.Empty;

    /// <summary>Raza del club inicial (RF-004): todos los jugadores del club son de ella.</summary>
    public Race ClubRace { get; init; } = Race.Human;

    /// <summary>Acto actual, 1..3 (RF-001).</summary>
    public int Act { get; init; } = 1;

    /// <summary>Nodo en el que está el jugador; -1 mientras no ha entrado en ninguno del acto.</summary>
    public int CurrentNodeId { get; init; } = -1;

    /// <summary>
    /// Nodo interactivo abierto (mercado, clínica, recompensa...); -1 si no hay ninguno. Es lo único que
    /// necesita el paquete X para recomponer el surtido: el contenido del nodo se deriva de
    /// <c>RngStreams.Rewards(Seed, PendingNodeId)</c> y de <see cref="NodeRerolls"/>, así que no hace
    /// falta serializarlo y salir a mitad de un mercado no permite volver a tirar el surtido.
    /// </summary>
    public int PendingNodeId { get; init; } = -1;

    /// <summary>Fase del bucle: en el mapa, dentro de un nodo, o terminada.</summary>
    public RunPhase Phase { get; init; } = RunPhase.OnMap;

    /// <summary>Oro disponible (RF-114g..k).</summary>
    public int Gold { get; init; }

    /// <summary>
    /// Apuesta del vestuario tomada y pagada para un nodo de partido aún sin jugar, o null (ADR 0157). Es lo
    /// único de la apuesta que se guarda: la ofrecida se deriva de (semilla, nodo) y no ocupa estado.
    /// </summary>
    public Systems.Bets.AcceptedBet? Bet { get; init; }

    /// <summary>Rerolls usados en toda la run: su coste es creciente (RF-071b).</summary>
    public int RerollsUsed { get; init; }

    /// <summary>Rerolls usados en el nodo abierto. Uno por nodo (RF-071b).</summary>
    public int NodeRerolls { get; init; }

    /// <summary>Desenlace registrado. Ver también <see cref="RunEngine.Outcome"/>, que además vigila el mínimo de plantilla.</summary>
    public RunOutcome Result { get; init; } = RunOutcome.InProgress;

    /// <summary>Historial de nodos completados, en orden (RT-030).</summary>
    public IReadOnlyList<NodeHistoryEntry> NodeHistory { get; init; } = Array.Empty<NodeHistoryEntry>();

    /// <summary>Mapas de los tres actos, en orden. Se generan todos al empezar la run (flujo <c>RngStreams.Map</c>).</summary>
    public IReadOnlyList<ActMap> Maps { get; init; } = Array.Empty<ActMap>();

    /// <summary>Árbitros de la run, 6-8 (RF-061b, RF-064c).</summary>
    public IReadOnlyList<RunReferee> Referees { get; init; } = Array.Empty<RunReferee>();

    /// <summary>Plantilla completa, ordenada por id ascendente. Incluye lesionados graves y muertos.</summary>
    public IReadOnlyList<RunPlayer> Roster { get; init; } = Array.Empty<RunPlayer>();

    /// <summary>Alineación elegida (RF-041). Puede quedar obsoleta tras una baja: <see cref="RunLineup"/> la repara al entrar en un partido.</summary>
    public Lineup Lineup { get; init; } = new(Array.Empty<LineupSlot>());

    /// <summary>
    /// Consumibles que la run lleva, como mucho <see cref="RunRules.ConsumableSlots"/> (RF-080, ADR 0172).
    /// Persisten entre partidos: sólo se va el que se usa (RF-085) o el que el jugador descarta.
    /// </summary>
    public IReadOnlyList<EquippedConsumable> Consumables { get; init; } = Array.Empty<EquippedConsumable>();

    /// <summary>
    /// Siguiente id libre de jugador. Los ids se asignan en orden de creación dentro de la run
    /// (<c>determinismo.md</c>, "Orden") y no se reutilizan nunca, ni siquiera tras una muerte: un id
    /// reutilizado rompería el historial y los vínculos.
    /// </summary>
    public int NextPlayerId { get; init; }

    /// <summary>
    /// Contadores enteros de los sistemas de los paquetes X e Y (coste actual del reroll, oro gastado
    /// por sumidero, derrotas seguidas de un mercenario...). Existe para que añadir un sistema no
    /// obligue a subir la versión del esquema. Ordenado por clave ordinal.
    /// </summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = RunPlayer.NoCounters;

    /// <summary>
    /// Memoria de los clanes rivales (ADR 0165, versión 8 del esquema): qué puestos ya no ocupa su jugador
    /// de datos y qué rivales son o fueron némesis. Vacía al empezar la run.
    /// </summary>
    public Systems.Rivals.RivalMemory RivalMemory { get; init; } = Systems.Rivals.RivalMemory.Empty;

    /// <summary>Progreso de logros de desbloqueo (RF-125b). Ordenado por clave ordinal.</summary>
    public IReadOnlyDictionary<string, int> Achievements { get; init; } = RunPlayer.NoCounters;

    /// <summary>
    /// Instantánea de <c>/data</c> congelada al empezar la run (RT-061b): ruta relativa -&gt; contenido.
    /// Cargar la run usa esta copia, nunca el <c>/data</c> del disco. Ordenada por ruta ordinal.
    /// </summary>
    public IReadOnlyDictionary<string, string> DataSnapshot { get; init; } =
        new SortedDictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Catálogos de objetos y consumibles de <b>esta</b> run, derivados de <see cref="DataSnapshot"/> por
    /// <see cref="WithDataSnapshot"/> (RT-061b). No se serializa: es dato derivado, siempre coherente con
    /// la instantánea, y se reconstruye solo al cargar. Es lo que permite que el equipamiento llegue al
    /// partido sin que <c>Catalog</c> gane un campo ni <c>IRunSystems</c> un método
    /// (<c>Sim.Run.Systems.Items.RunEquipment</c> explica por qué). Un estado sin instantánea —el modo de
    /// depuración de RT-062— juega sin equipamiento.
    /// </summary>
    public RunEquipment Equipment { get; init; } = RunEquipment.None;

    /// <summary>Mapa del acto actual.</summary>
    public ActMap CurrentMap => MapOf(Act);

    /// <summary>Mapa del acto indicado.</summary>
    public ActMap MapOf(int act)
    {
        for (int i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].Act == act)
            {
                return Maps[i];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(act), act, "la run no tiene mapa para ese acto");
    }

    /// <summary>
    /// Contador de run con los huecos de inscripción ya comprados (ADR 0046). Vive en
    /// <see cref="Counters"/> a propósito: es exactamente el caso para el que ese diccionario existe
    /// —añadir un sistema sin subir la versión del esquema— y así una run guardada antes del nodo de
    /// inscripción se carga con cero huecos, que es la lectura correcta.
    /// </summary>
    public const string EnrollmentSlotsCounter = "enrollmentSlots";

    /// <summary>
    /// Prefijo del <b>almacén de objetos</b> (ADR 0048, condición 4): <c>itemStock:&lt;idObjeto&gt;</c> con
    /// cuántas copias sueltas tiene el club. Existe por una única razón: <b>el objeto del jugador muerto
    /// vuelve al inventario</b> en vez de perderse con él, que es la mitad "se puede rehacer" de lo que
    /// sostiene que un sano pueda morir. Vive en <see cref="Counters"/> como los huecos de inscripción
    /// —mismo motivo: añadir un sistema sin subir la versión del esquema (RT-030)— y una run guardada
    /// antes de esto se carga con el almacén vacío, que es la lectura correcta.
    /// </summary>
    public const string ItemStockPrefix = "itemStock:";

    /// <summary>
    /// Prefijo del contador de tiradas del matasanos por nodo (ADR 0099): <c>clinicRolls:&lt;idNodo&gt;</c>.
    /// El flujo de RNG de la clínica es función del nodo, así que sin esto dos tratamientos arriesgados en
    /// el mismo nodo sacarían el mismo número; el contador dice cuántas tiradas hay que saltar. Vive en
    /// <see cref="Counters"/> por el mismo motivo que los demás: añadir un sistema sin subir la versión.
    /// </summary>
    public const string ClinicRollsPrefix = "clinicRolls:";

    /// <summary>Objetos recuperados de un muerto en toda la run, para el informe y para /Balance.</summary>
    public const string ItemsRecoveredCounter = "itemsRecovered";

    /// <summary>
    /// Marca de "el nodo abierto ya se resolvió" (ADR 0159, ADR 0160, revisión independiente): vale el id
    /// del nodo <b>más uno</b> (0 = ninguno) mientras el nodo de evento o de entrenamiento abierto ya tuvo
    /// su elección. Esos dos nodos se resuelven <b>una sola vez</b>: sin la marca, <c>ChooseEventOption</c>
    /// y <c>ChooseTrainingSession</c> se podían repetir hasta salir con <c>LeaveNode</c> (tres pachangas
    /// seguidas = 3x40 de experiencia). Vive en <see cref="Counters"/> por el mismo motivo que los demás
    /// (sin subir la versión del guardado) y <c>RunEngine.Apply(LeaveNode)</c> la borra al cerrar. La clínica,
    /// el mercado y la inscripción no la usan: cobran cada servicio, así que repetir es legítimo.
    /// </summary>
    public const string NodeResolvedCounter = "nodeResolved";

    /// <summary>
    /// Oro que el corredor devolvió al entrar en el último nodo por una apuesta tomada para otro (ADR 0157):
    /// 0 si no hubo devolución. Se reescribe en cada entrada y se borra al tomar o retirar una apuesta; es lo
    /// que las pantallas del nodo y el informe enseñan («el corredor te devuelve N»).
    /// </summary>
    public const string BetRefundedCounter = "betRefunded";

    /// <summary>Copias sueltas de ese objeto en el almacén.</summary>
    public int StockOf(string itemId)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);
        return Counter(ItemStockPrefix + itemId);
    }

    /// <summary>
    /// Objetos del almacén, por id ascendente y una entrada por copia (RT-041): es lo que la pantalla de
    /// equipo enseña y lo que una política automática recorre.
    /// </summary>
    public IReadOnlyList<string> StoredItems
    {
        get
        {
            var ids = new List<string>();
            foreach (var (key, count) in Counters)
            {
                if (count <= 0 || !key.StartsWith(ItemStockPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                for (int i = 0; i < count; i++)
                {
                    ids.Add(key[ItemStockPrefix.Length..]);
                }
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }
    }

    /// <summary>
    /// Prefijo del contador del <b>inventario de consumibles sueltos</b> que existió hasta la ADR 0172 (paquete
    /// X, X-9: el mercado lo subía al comprar y el partido lo bajaba al gastarse). Ya no lo escribe nadie:
    /// desde la ADR 0172 el hueco es la posesión. Sólo lo lee <see cref="FoldLegacyConsumables"/>, que
    /// convierte lo que un guardado viejo trajera en ese inventario.
    /// </summary>
    public const string LegacyConsumableOwnedPrefix = "consumable_owned:";

    /// <summary>True si queda algún hueco de consumible libre (RF-080, ADR 0172).</summary>
    public bool HasFreeConsumableSlot => Consumables.Count < RunRules.ConsumableSlots;

    /// <summary>True si la run ya lleva ese consumible en un hueco (no se lleva el mismo dos veces, ADR 0172).</summary>
    public bool CarriesConsumable(string consumableId)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumableId);
        for (int i = 0; i < Consumables.Count; i++)
        {
            if (string.Equals(Consumables[i].Id, consumableId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Si ese consumible puede entrar ahora: hay un hueco libre y no se lleva ya (ADR 0172). Es lo que
    /// comparten el mercado, los eventos y la política antes de comprar o recibir uno.
    /// </summary>
    public bool CanTakeConsumable(string consumableId) => HasFreeConsumableSlot && !CarriesConsumable(consumableId);

    /// <summary>
    /// Copia con ese consumible en el primer hueco libre, <see cref="ConsumableMode.Manual"/> y sin
    /// disparador: sale «ya equipado» para usarlo con un clic (ADR 0172). Lanza si no cabe.
    /// </summary>
    public RunState WithTakenConsumable(string consumableId)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumableId);
        if (!HasFreeConsumableSlot)
        {
            throw new InvalidOperationException(
                $"no hay ningún hueco de consumible libre ({Consumables.Count} de {RunRules.ConsumableSlots}, RF-080)");
        }

        if (CarriesConsumable(consumableId))
        {
            throw new InvalidOperationException($"la run ya lleva el consumible '{consumableId}' (ADR 0172)");
        }

        var next = new List<EquippedConsumable>(Consumables)
        {
            new(consumableId, ConsumableMode.Manual, string.Empty),
        };
        return this with { Consumables = next };
    }

    /// <summary>
    /// Migración explícita de los guardados anteriores a la ADR 0172 (no cambia la forma del guardado, sólo
    /// lo que significa): lo que hubiera en el inventario suelto (<see cref="LegacyConsumableOwnedPrefix"/>) y
    /// lo que estuviera equipado pasa a los huecos, por este orden y <b>sin repetir</b>: primero lo que ya iba
    /// equipado (hasta <see cref="RunRules.ConsumableSlots"/>, en su orden y con su modo; de un id repetido
    /// gana la primera copia), luego las copias sueltas por id ascendente (RT-041), en modo manual. Los
    /// contadores viejos se borran.
    ///
    /// <para><b>Lo que no cabe no se pierde en silencio</b>: <paramref name="lost"/> lleva un id por cada
    /// copia que se queda fuera (por id ascendente), y quien carga el guardado se lo dice al jugador. Las
    /// copias de un id son el mayor entre su contador y lo que hubiera equipado (el equipado también
    /// contaba en el inventario). Sin contadores viejos, sin repetidos y con como mucho dos equipados,
    /// devuelve el mismo estado y <paramref name="lost"/> vacío.</para>
    /// </summary>
    public RunState FoldLegacyConsumables(out IReadOnlyList<string> lost)
    {
        var loose = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var kept = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in Counters)
        {
            if (key.StartsWith(LegacyConsumableOwnedPrefix, StringComparison.Ordinal))
            {
                if (value > 0)
                {
                    loose[key[LegacyConsumableOwnedPrefix.Length..]] = value;
                }

                continue;
            }

            kept[key] = value;
        }

        var slots = new List<EquippedConsumable>(RunRules.ConsumableSlots);
        var equippedCopies = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < Consumables.Count; i++)
        {
            string id = Consumables[i].Id;
            equippedCopies[id] = equippedCopies.GetValueOrDefault(id) + 1;
            if (slots.Count < RunRules.ConsumableSlots && !slots.Exists(c => string.Equals(c.Id, id, StringComparison.Ordinal)))
            {
                slots.Add(Consumables[i]);
            }
        }

        foreach (var (id, _) in loose)
        {
            if (slots.Count < RunRules.ConsumableSlots && !slots.Exists(c => string.Equals(c.Id, id, StringComparison.Ordinal)))
            {
                slots.Add(new EquippedConsumable(id, ConsumableMode.Manual, string.Empty));
            }
        }

        var lostCopies = new List<string>();
        var ids = new SortedSet<string>(loose.Keys, StringComparer.Ordinal);
        ids.UnionWith(equippedCopies.Keys);
        foreach (string id in ids)
        {
            int total = Math.Max(loose.GetValueOrDefault(id), equippedCopies.GetValueOrDefault(id));
            int remaining = total - (slots.Exists(c => string.Equals(c.Id, id, StringComparison.Ordinal)) ? 1 : 0);
            for (int i = 0; i < remaining; i++)
            {
                lostCopies.Add(id);
            }
        }

        lost = lostCopies;
        bool dirty = kept.Count != Counters.Count || Consumables.Count != slots.Count || lostCopies.Count > 0;
        return dirty ? this with { Consumables = slots, Counters = kept } : this;
    }

    /// <summary>Copia con una copia más (o menos) de ese objeto en el almacén.</summary>
    public RunState WithStockedItem(string itemId, int delta)
    {
        ArgumentException.ThrowIfNullOrEmpty(itemId);
        int next = StockOf(itemId) + delta;
        if (next < 0)
        {
            throw new InvalidOperationException($"el almacén no tiene ninguna copia de '{itemId}' que sacar");
        }

        return WithCounter(ItemStockPrefix + itemId, next);
    }

    /// <summary>
    /// Prefijo de la memoria de "quién knaveó a quién" (BE-B, enmienda de la ADR 0124, corrigiendo su
    /// tabla «Dónde vive cada memoria»: citaba esta clave como si ya existiera, y era un plan, no código).
    /// Vive en <see cref="Counters"/> por el mismo motivo que <see cref="ItemStockPrefix"/> y
    /// <see cref="ItemStockPrefix"/> -clave libre para no subir de versión cada vez que entra un
    /// sistema-, y es el gemelo de vocabulario <b>abierto</b> de <see cref="RunCareer"/> (vocabulario
    /// <b>cerrado</b>, por jugador propio): un rival no vive lo que vive la run entera con nombre propio en
    /// <c>RunState</c>, así que no puede tener una propiedad tipada.
    ///
    /// <para><b>Formato de la clave</b> (cuatro campos tras el prefijo, separados por <c>:</c>):</para>
    /// <code>
    /// rivalCredit:&lt;opponentId&gt;:&lt;rivalIndex&gt;:&lt;ownPlayerId&gt;:&lt;causedInjury|causedDeath|sufferedInjury|sufferedDeath&gt;
    /// </code>
    /// <list type="bullet">
    /// <item><description><c>opponentId</c> es <see cref="MapNode.OpponentId"/>: el id del clan
    /// rival (<c>data/rivals/</c>, RF-015). Sin catálogo de rivales (nodo procedural, cadena vacía) no hay
    /// identidad de clan estable y no se registra nada -mismo guardia que ya usa
    /// <see cref="Systems.Rivals.RivalHistory"/> para el mismo motivo-.</description></item>
    /// <item><description><c>rivalIndex</c> es el índice del jugador rival dentro de la lista de ese
    /// clan en el fichero JSON: <c>id de jugador - <see cref="Systems.Rivals.RivalTeamBuilder.OpponentFirstPlayerId"/></c>
    /// (verificado: así asigna <see cref="Systems.Rivals.RivalTeamBuilder.Build"/> los ids). Junto con
    /// <c>opponentId</c> identifica de forma estable y determinista a <b>ese</b> individuo rival, no solo
    /// a su clan.</description></item>
    /// <item><description><c>ownPlayerId</c> es el id, dentro de <c>RunState</c>, del jugador de la
    /// plantilla propia del par -la víctima si la dirección es <c>suffered</c>, el causante si es
    /// <c>caused</c>-.</description></item>
    /// <item><description>El último campo junta dirección y hecho en una sola palabra para no dejar
    /// ambigüedad en el número de segmentos: <c>caused*</c> es un jugador <b>propio</b> lesionando o
    /// matando a este rival; <c>suffered*</c> es este rival lesionando o matando a un jugador
    /// <b>propio</b>. Las dos direcciones producen historia (RF-125 cuenta lesiones causadas; el bando de
    /// muerte de F2 querrá poder decir "a manos de quién").</description></item>
    /// </list>
    /// <para>BS-A: si el puesto no lo ocupaba el jugador de datos sino un fichaje o un némesis, la clave lleva un
    /// quinto campo, el código de <c>Systems.Rivals.RivalKiller</c>: <c>…:&lt;hecho&gt;:&lt;ocupante&gt;</c>. Sin él (0, y todo
    /// guardado anterior) es el jugador de datos. Así cada ocupante lleva su propia cuenta.</para>
    /// <para>El valor del contador es cuántas veces ha pasado ese hecho concreto, acumulado a lo largo de
    /// toda la run (nunca se reinicia, igual que <see cref="ItemStockPrefix"/>). Lo escribe
    /// <see cref="MatchResolution"/>, en una pasada que excluye los eventos anulados (mismo criterio que
    /// la atribución de <see cref="RunCareer"/>) y no cambia ninguna tirada: es contabilidad pura sobre la
    /// secuencia de eventos que el motor ya produjo.</para>
    /// </summary>
    public const string RivalCreditPrefix = "rivalCredit:";

    /// <summary>
    /// Contador de run (ADR 0165): venganzas cobradas en toda la run (un jugador propio lesiona o mata a un
    /// némesis). Sólo contabilidad para el censo de <c>/Balance</c>; el dato de juego es la carrera de cada jugador.
    /// </summary>
    public const string RevengesCounter = "nemesis:revenges";

    /// <summary>
    /// Contador de run (ADR 0165): asesinos rivales que NO se convirtieron en némesis porque ya había el tope
    /// vivos («se anota», ADR 0165 punto 3).
    /// </summary>
    public const string NemesisCappedCounter = "nemesis:capped";

    /// <summary>
    /// Prefijo de los contadores que guardan <b>cómo murió</b> cada jugador propio (ADR 0163, RF-122):
    /// <c>deathCause:&lt;playerId&gt;</c> = <see cref="PlayerDeathCause"/> como entero. Lo escriben las tres
    /// vías de muerte (partido, sacrificio del evento y matasanos) y lo lee la Gaceta; sin él una esquela
    /// sólo puede decir «cayó en el campo» de quien murió en la clínica.
    /// </summary>
    public const string DeathCausePrefix = "deathCause:";

    /// <summary>
    /// BS-A: prefijo de los contadores que guardan <b>quién ocupaba</b> el puesto del rival que mató a un jugador
    /// propio, en el momento del partido: <c>deathKiller:&lt;playerId&gt;</c> = código de
    /// <c>Systems.Rivals.RivalKiller</c>. Clave libre, como <see cref="DeathCausePrefix"/>: no sube la versión.
    /// </summary>
    public const string DeathKillerPrefix = "deathKiller:";

    /// <summary>
    /// Jugadores que <b>ocupan plantilla</b> (RF-020): todos menos los muertos. El muerto se queda en
    /// <see cref="Roster"/> para el memorial (RF-122) pero deja su sitio libre: morir cuesta un jugador,
    /// no un jugador y su hueco.
    /// </summary>
    public int RosterSize
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].PhysicalState != PhysicalState.Dead)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Tamaño máximo de plantilla ahora mismo (RF-020, ADR 0046): la base de 10 más los huecos de
    /// inscripción comprados, con el techo duro de 12.
    /// </summary>
    public int RosterCapacity =>
        Math.Min(RunRules.BaseRosterSize + Counter(EnrollmentSlotsCounter), RunRules.MaxRosterSize);

    /// <summary>Huecos de inscripción que todavía se pueden comprar (0, 1 o 2).</summary>
    public int EnrollmentSlotsLeft => RunRules.MaxEnrollmentSlots - Counter(EnrollmentSlotsCounter);

    /// <summary>True si cabe un jugador más sin vender ni descartar a nadie (RF-020).</summary>
    public bool HasRosterSpace => RosterSize < RosterCapacity;

    /// <summary>
    /// Jugadores que pueden alinearse ahora mismo (RF-002e). Es consultable en todo momento y es la
    /// cifra que se compara con <see cref="RunRules.MinimumAvailablePlayers"/>.
    /// </summary>
    public int AvailablePlayerCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].IsAvailable)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>True si la plantilla ha bajado del mínimo de 5 (RF-002b).</summary>
    public bool IsBelowMinimum => AvailablePlayerCount < RunRules.MinimumAvailablePlayers;

    /// <summary>Jugadores alineables, en orden de id ascendente.</summary>
    public IReadOnlyList<RunPlayer> AvailablePlayers
    {
        get
        {
            var players = new List<RunPlayer>(Roster.Count);
            for (int i = 0; i < Roster.Count; i++)
            {
                if (Roster[i].IsAvailable)
                {
                    players.Add(Roster[i]);
                }
            }

            return players;
        }
    }

    /// <summary>Jugador con ese id, o null.</summary>
    public RunPlayer? FindPlayer(int id)
    {
        for (int i = 0; i < Roster.Count; i++)
        {
            if (Roster[i].Id == id)
            {
                return Roster[i];
            }
        }

        return null;
    }

    /// <summary>Jugador con ese id; lanza si no está en la plantilla.</summary>
    public RunPlayer GetPlayer(int id) =>
        FindPlayer(id) ?? throw new ArgumentOutOfRangeException(nameof(id), id, "no hay ningún jugador con ese id en la plantilla");

    /// <summary>Nodo con ese id en cualquiera de los tres actos; null si no existe.</summary>
    public MapNode? FindNode(int nodeId)
    {
        for (int i = 0; i < Maps.Count; i++)
        {
            var node = Maps[i].Find(nodeId);
            if (node is not null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>Nodo con ese id; lanza si no existe.</summary>
    public MapNode GetNode(int nodeId) =>
        FindNode(nodeId) ?? throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "la run no tiene ningún nodo con ese id");

    // ------------------------------------------------------------------ With*

    /// <summary>Copia con el oro indicado. Nunca baja de 0.</summary>
    public RunState WithGold(int gold) => this with { Gold = gold < 0 ? 0 : gold };

    /// <summary>Copia con la memoria de los clanes rivales indicada (ADR 0165).</summary>
    public RunState WithRivalMemory(Systems.Rivals.RivalMemory memory) =>
        this with { RivalMemory = memory ?? throw new ArgumentNullException(nameof(memory)) };

    /// <summary>Copia con la apuesta tomada indicada (null la cierra), ADR 0157.</summary>
    public RunState WithBet(Systems.Bets.AcceptedBet? bet) => this with { Bet = bet };

    /// <summary>Copia sumando (o restando, con valor negativo) oro. Nunca baja de 0.</summary>
    public RunState AddGold(int delta) => WithGold(Gold + delta);

    /// <summary>Copia en el acto indicado, 1..3, colocada en la entrada del acto.</summary>
    public RunState WithAct(int act)
    {
        if (act < 1 || act > RunRules.Acts)
        {
            throw new ArgumentOutOfRangeException(nameof(act), act, $"el acto debe estar entre 1 y {RunRules.Acts}");
        }

        return this with { Act = act, CurrentNodeId = -1, PendingNodeId = -1, Phase = RunPhase.OnMap };
    }

    /// <summary>Copia situada en ese nodo del mapa.</summary>
    public RunState WithCurrentNode(int nodeId) => this with { CurrentNodeId = nodeId };

    /// <summary>Copia con ese nodo interactivo abierto (o -1 para cerrarlo) y la fase coherente.</summary>
    public RunState WithPendingNode(int nodeId) => this with
    {
        PendingNodeId = nodeId,
        Phase = nodeId < 0 ? RunPhase.OnMap : RunPhase.NodeOpen,
        NodeRerolls = nodeId < 0 ? 0 : NodeRerolls,
    };

    /// <summary>Copia en la fase indicada.</summary>
    public RunState WithPhase(RunPhase phase) => this with { Phase = phase };

    /// <summary>Copia con el desenlace indicado; si termina la run, la fase pasa a <see cref="RunPhase.Finished"/>.</summary>
    public RunState WithOutcome(RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return this with { Result = outcome, Phase = outcome.IsOver ? RunPhase.Finished : Phase };
    }

    /// <summary>Copia con la plantilla indicada, reordenada por id ascendente.</summary>
    public RunState WithRoster(IEnumerable<RunPlayer> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var players = new List<RunPlayer>(roster);
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        AssignShirtNumbers(players);

        int next = NextPlayerId;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id >= next)
            {
                next = players[i].Id + 1;
            }
        }

        return this with { Roster = players, NextPlayerId = next };
    }

    /// <summary>Copia sustituyendo a un jugador de la plantilla por otro con el mismo id.</summary>
    public RunState WithPlayer(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var players = new List<RunPlayer>(Roster.Count);
        bool found = false;
        for (int i = 0; i < Roster.Count; i++)
        {
            if (Roster[i].Id == player.Id)
            {
                players.Add(player);
                found = true;
            }
            else
            {
                players.Add(Roster[i]);
            }
        }

        if (!found)
        {
            throw new ArgumentOutOfRangeException(nameof(player), player.Id, "no hay ningún jugador con ese id en la plantilla");
        }

        return this with { Roster = players };
    }

    /// <summary>
    /// Copia con un jugador más. Le asigna <see cref="NextPlayerId"/> si su id es negativo.
    ///
    /// <para><b>Respeta el tamaño de plantilla</b> (RF-020, ADR 0046): con la plantilla llena lanza
    /// <see cref="InvalidOperationException"/>. La comprobación está aquí, en el único embudo por el que
    /// entra un jugador nuevo, y no repartida por los sistemas: así ningún camino —mercado, canterano,
    /// mercenario, recompensa, o el que se añada mañana— puede ampliar la plantilla en silencio, que es
    /// exactamente lo que hacía que el desgaste no mordiera (ADR 0045).</para>
    /// </summary>
    public RunState WithNewPlayer(RunPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!HasRosterSpace)
        {
            throw new InvalidOperationException(
                $"la plantilla está llena: {RosterSize} de {RosterCapacity} (RF-020). Hay que vender o "
                    + "descartar a alguien, o comprar un hueco en un nodo de inscripción (ADR 0046)");
        }

        var added = player.Id < 0 ? player with { Id = NextPlayerId } : player;
        if (FindPlayer(added.Id) is not null)
        {
            throw new ArgumentException($"ya hay un jugador con el id {added.Id} en la plantilla", nameof(player));
        }

        // BX-4: el dorsal se fija aquí, al entrar. Si el que trae está ocupado (o no trae), el primero libre.
        if (added.ShirtNumber <= 0 || IsShirtNumberTaken(Roster, added.ShirtNumber))
        {
            added = added with { ShirtNumber = FirstFreeShirtNumber(Roster) };
        }

        var players = new List<RunPlayer>(Roster) { added };
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return this with { Roster = players, NextPlayerId = Math.Max(NextPlayerId, added.Id + 1) };
    }

    /// <summary>
    /// Da dorsal a quien no lo tiene o lo tiene repetido, en orden de id (BX-4): conserva el de cada jugador si es
    /// válido y único y reparte el primero libre al resto. Es la migración de un guardado anterior y el arranque de la run.
    /// </summary>
    private static void AssignShirtNumbers(List<RunPlayer> players)
    {
        var taken = new HashSet<int>();
        var pending = new List<int>();
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].ShirtNumber > 0 && taken.Add(players[i].ShirtNumber))
            {
                continue;
            }

            pending.Add(i);
        }

        foreach (int index in pending)
        {
            int number = 1;
            while (taken.Contains(number))
            {
                number++;
            }

            taken.Add(number);
            players[index] = players[index] with { ShirtNumber = number };
        }
    }

    private static bool IsShirtNumberTaken(IReadOnlyList<RunPlayer> roster, int number)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i].ShirtNumber == number)
            {
                return true;
            }
        }

        return false;
    }

    private static int FirstFreeShirtNumber(IReadOnlyList<RunPlayer> roster)
    {
        int number = 1;
        while (IsShirtNumberTaken(roster, number))
        {
            number++;
        }

        return number;
    }

    /// <summary>Copia sin el jugador indicado (venta, RF-114f). No reutiliza su id.</summary>
    public RunState WithoutPlayer(int playerId)
    {
        var players = new List<RunPlayer>(Roster.Count);
        for (int i = 0; i < Roster.Count; i++)
        {
            if (Roster[i].Id != playerId)
            {
                players.Add(Roster[i]);
            }
        }

        return this with { Roster = players };
    }

    /// <summary>Copia con esa alineación (RF-041).</summary>
    public RunState WithLineup(Lineup lineup)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        return this with { Lineup = lineup };
    }

    /// <summary>Copia con esos consumibles equipados (RF-080..082).</summary>
    public RunState WithConsumables(IEnumerable<EquippedConsumable> consumables)
    {
        ArgumentNullException.ThrowIfNull(consumables);
        return this with { Consumables = new List<EquippedConsumable>(consumables) };
    }

    /// <summary>Copia con esos árbitros (RF-061b).</summary>
    public RunState WithReferees(IEnumerable<RunReferee> referees)
    {
        ArgumentNullException.ThrowIfNull(referees);
        return this with { Referees = new List<RunReferee>(referees) };
    }

    /// <summary>Copia sustituyendo el mapa de su acto.</summary>
    public RunState WithMap(ActMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var maps = new List<ActMap>(Maps.Count);
        bool found = false;
        for (int i = 0; i < Maps.Count; i++)
        {
            if (Maps[i].Act == map.Act)
            {
                maps.Add(map);
                found = true;
            }
            else
            {
                maps.Add(Maps[i]);
            }
        }

        if (!found)
        {
            maps.Add(map);
            maps.Sort(static (a, b) => a.Act.CompareTo(b.Act));
        }

        return this with { Maps = maps };
    }

    /// <summary>Copia con una entrada más en el historial de nodos.</summary>
    public RunState WithNodeCompleted(int nodeId, NodeKind kind, NodeResult result)
    {
        var history = new List<NodeHistoryEntry>(NodeHistory) { new(nodeId, kind, result) };
        return this with { NodeHistory = history };
    }

    /// <summary>Copia con ese contador de run fijado al valor indicado.</summary>
    public RunState WithCounter(string name, int value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var counters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, current) in Counters)
        {
            counters[key] = current;
        }

        counters[name] = value;
        return this with { Counters = counters };
    }

    /// <summary>Copia con la causa de muerte de ese jugador anotada (<see cref="DeathCausePrefix"/>).</summary>
    public RunState WithDeathCause(int playerId, PlayerDeathCause cause) =>
        WithCounter(DeathCausePrefix + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture), (int)cause);

    /// <summary>Código del ocupante que mató a ese jugador (<see cref="DeathKillerPrefix"/>); 0 si no consta.</summary>
    public int DeathKillerOf(int playerId) =>
        Counter(DeathKillerPrefix + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Cómo murió ese jugador; <see cref="PlayerDeathCause.Unknown"/> si no consta.</summary>
    public PlayerDeathCause DeathCauseOf(int playerId) =>
        (PlayerDeathCause)Counter(DeathCausePrefix + playerId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Valor de un contador de run, 0 si no está.</summary>
    public int Counter(string name) => Counters.TryGetValue(name, out int value) ? value : 0;

    /// <summary>Copia con ese contador de logro fijado al valor indicado (RF-125b).</summary>
    public RunState WithAchievement(string name, int value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var achievements = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, current) in Achievements)
        {
            achievements[key] = current;
        }

        achievements[name] = value;
        return this with { Achievements = achievements };
    }

    /// <summary>Copia con los rerolls indicados (RF-071b).</summary>
    public RunState WithRerolls(int rerollsUsed, int nodeRerolls) =>
        this with { RerollsUsed = rerollsUsed, NodeRerolls = nodeRerolls };

    /// <summary>Copia con la instantánea de <c>/data</c> indicada (RT-061b), ordenada por ruta ordinal.</summary>
    public RunState WithDataSnapshot(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, content) in files)
        {
            sorted[path] = content;
        }

        return this with { DataSnapshot = sorted, Equipment = RunEquipment.FromSnapshot(sorted) };
    }
}
