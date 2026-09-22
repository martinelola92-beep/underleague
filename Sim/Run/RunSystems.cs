using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run;

/// <summary>
/// Resumen de un partido de la run, tal y como lo ve el bucle de run: sin eventos ni tablas de
/// utilidad, solo lo que necesitan la economía (RF-114g..k) y las recompensas (RF-071).
/// </summary>
/// <param name="NodeId">Nodo en el que se jugó.</param>
/// <param name="Kind">Tipo de nodo: liga, élite o jefe.</param>
/// <param name="Won">True si ganó el equipo del jugador.</param>
/// <param name="GoalsFor">Goles del equipo del jugador.</param>
/// <param name="GoalsAgainst">Goles del rival.</param>
/// <param name="Ticks">Duración del partido en ticks lógicos.</param>
/// <param name="WentToGoldenGoal">True si hubo prórroga de turba (RF-055b).</param>
/// <param name="PlayedPlayerIds">Titulares alineados, en orden de id ascendente.</param>
/// <param name="BenchedPlayerIds">Disponibles que no jugaron, en orden de id ascendente.</param>
/// <param name="OwnInjuries">Lesiones propias sufridas (leves y graves).</param>
/// <param name="OwnDeaths">Muertes propias (RF-093).</param>
/// <param name="Report">Informe completo del partido, por si el sistema quiere más detalle (RF-119).</param>
public sealed record RunMatchSummary(
    int NodeId,
    NodeKind Kind,
    bool Won,
    int GoalsFor,
    int GoalsAgainst,
    int Ticks,
    bool WentToGoldenGoal,
    IReadOnlyList<int> PlayedPlayerIds,
    IReadOnlyList<int> BenchedPlayerIds,
    int OwnInjuries,
    int OwnDeaths,
    MatchReport Report)
{
    /// <summary>
    /// Contadores que perks con <c>accumulatesAcrossMatches: true</c> han sumado en ESTE partido
    /// (<c>MatchResult.CounterDeltas</c>, RF-070 §6), de cualquiera de los dos equipos y ordenados por id
    /// de jugador y luego por nombre de contador (RT-041, heredado de <c>EffectEngine.CounterDeltas</c>).
    /// Es lo que <see cref="Economy.GoldCalculator"/> convierte en oro cuando el contador tiene tarifa
    /// (paquete Z, primitiva D): un contador de partido que paga oro. Propiedad añadida fuera del
    /// constructor primario, como <c>EconomyConfig.ClinicMinorCost</c>, para no romper la construcción
    /// posicional de los tests existentes.
    /// </summary>
    public IReadOnlyList<PlayerCounterDelta> CounterDeltas { get; init; } = Array.Empty<PlayerCounterDelta>();

    /// <summary>
    /// Detalle de cada jugador PROPIO que ha muerto en este partido, en el orden del propio evento (RT-041,
    /// que ya es determinista). Es la primitiva "run-level: oro y atributos al salir de la plantilla"
    /// (paquete BB, <c>docs/analisis/perks-catalogo-unificado.md</c> §3.2): de aquí salen tanto
    /// <see cref="Economy.GoldCalculator.DeathGold"/> (Seguro de vida) como
    /// <see cref="Economy.InheritanceSystem"/> (Herencia). Propiedad añadida fuera del constructor
    /// primario, como <see cref="CounterDeltas"/>, para no romper la construcción posicional de los tests
    /// existentes. Vacía si nadie ha muerto.
    /// </summary>
    public IReadOnlyList<PlayerDeathDetail> DeathDetails { get; init; } = Array.Empty<PlayerDeathDetail>();
}

/// <summary>
/// Detalle de un jugador propio que ha muerto en un partido (paquete BB, §3.2). Lo construye
/// <c>MatchResolution</c> en el momento del evento <c>DEATH</c>, con los perks del jugador tal y como
/// estaban en ese instante (no cambian al morir) y su compañero vinculado ya resuelto.
/// </summary>
/// <param name="PlayerId">Jugador que ha muerto.</param>
/// <param name="Perks">
/// Perks que llevaba, en orden ascendente (<see cref="Model.RunPlayer.Perks"/> ya viene ordenado así,
/// RT-041). De aquí lee su tarifa <see cref="Economy.DeathGoldTable"/> y su traspaso
/// <see cref="Economy.InheritanceTable"/>.
/// </param>
/// <param name="LinkedPlayerId">
/// Compañero vinculado en la alineación INICIAL de este partido (<see cref="MatchLineup.Lineup"/>), o -1
/// si ninguno de los perks del muerto declara relaciones de vínculo (<c>PerkDefinition.Links</c>) o
/// ninguna relación resuelve candidato. Solo lo resuelve el <b>primer</b> perk con vínculo declarado, en
/// orden ascendente: es una decisión de diseño del paquete BB, no una limitación del motor.
/// <see cref="Economy.InheritanceSystem"/> se encarga de comprobar si sigue vivo al aplicar el traspaso.
/// </param>
/// <param name="KillerPlayerId">
/// Id del jugador que causó la muerte (RF-122, ADR 0124, enmienda R3), leído del <c>Opponent</c> del
/// evento DEATH (<see cref="Engine.MatchEngine.Kill"/>), o -1 si no hubo matador. Puede ser un jugador
/// del <b>rival</b> —el caso que da nombre al juego— o un compañero, si la vía letal fue un perk propio.
/// Solo dato: qué se enseña con esto es F2 (RF-122, obituario), no esta ADR.
/// </param>
public sealed record PlayerDeathDetail(
    int PlayerId, IReadOnlyList<string> Perks, int LinkedPlayerId, int KillerPlayerId = -1);

/// <summary>
/// Los huecos que el paquete W deja abiertos para los paquetes X (economía, mercado, plantilla) e Y
/// (jefe y cierre de run). <see cref="RunEngine"/> resuelve el mapa, los partidos y las condiciones de
/// derrota; todo lo demás pasa por aquí.
///
/// <para><b>Contrato</b>: toda implementación debe ser <b>pura y determinista</b>, igual que
/// <c>Simulator.Run</c>. Nada de E/S, nada de reloj, nada de RNG que no salga de
/// <see cref="RngStreams"/> con la semilla de la run (RT-012, RT-021, RT-022). Las que sortean algo
/// deben usar el flujo de recompensas <c>RngStreams.Rewards(state.Seed, node.Id)</c>, no el del
/// partido: cambiar una recompensa no puede alterar un partido con la misma semilla.</para>
/// </summary>
public interface IRunSystems
{
    /// <summary>
    /// Árbitros de la run (RF-061b): 6-8, creados al empezar. Se sortean con
    /// <c>RngStreams.Generation</c>, no con el flujo del mapa ni con el del partido.
    /// </summary>
    IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog);

    /// <summary>
    /// Equipo rival del nodo de partido (RF-015). Los ids de sus jugadores <b>no pueden solaparse</b>
    /// con los de la plantilla del jugador: <c>Simulator.Run</c> lo rechaza.
    /// </summary>
    TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog);

    /// <summary>Árbitro que dirige el partido de ese nodo (RF-061).</summary>
    RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog);

    /// <summary>
    /// Configuración del simulador para ese partido (log, profundidad de recursión y el desgaste del
    /// nodo, ADR 0043). Recibe el catálogo porque el desgaste por acto es un dato de
    /// <c>tuning.injury</c> y el motor no puede saber en qué acto se juega.
    /// </summary>
    SimConfig MatchConfig(RunState state, MapNode node, Catalog catalog);

    /// <summary>
    /// Abre un nodo no de partido (mercado, clínica, entrenamiento, evento) y devuelve el estado
    /// resultante. Dos contratos posibles:
    /// <list type="bullet">
    /// <item>si el nodo requiere decisiones, devolver <c>state.WithPendingNode(node.Id)</c> y esperar a
    /// <see cref="ApplyDecision"/>;</item>
    /// <item>si se resuelve solo, devolver el estado ya resuelto sin nodo pendiente.</item>
    /// </list>
    /// En ambos casos <see cref="RunEngine"/> se encarga después de mover al jugador al nodo y de
    /// anotarlo en el historial.
    /// </summary>
    RunState OpenNode(RunState state, MapNode node, Catalog catalog);

    /// <summary>
    /// Se llama tras resolver un partido que <b>no</b> ha terminado la run: aquí van el oro
    /// (RF-114g..k) y las recompensas de RF-071 (que pueden dejar el nodo pendiente de decisión, igual
    /// que <see cref="OpenNode"/>). El estado que recibe ya tiene las lesiones, la experiencia y los
    /// contadores del partido aplicados.
    /// </summary>
    RunState AfterMatch(RunState state, MapNode node, RunMatchSummary summary, Catalog catalog);

    /// <summary>
    /// Aplica una decisión que el paquete W no conoce (compras, ventas, tratamientos, recompensas).
    /// Debe lanzar <see cref="NotSupportedException"/> ante una decisión que no sepa resolver.
    /// </summary>
    RunState ApplyDecision(RunState state, RunDecision decision, Catalog catalog);

    /// <summary>
    /// Modificadores de regla del jefe del acto (RF-001b/c): uno en los actos 1 y 2, dos en el 3.
    /// Devuelve ids de <c>data/bosses/</c>; lista vacía si no hay ninguno todavía. Paquete Y.
    /// </summary>
    IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog);

    /// <summary>
    /// Última oportunidad de transformar el partido antes de simularlo. Es el gancho por el que entran
    /// los modificadores de regla del jefe (RF-001b/c): un modificador es, por construcción, una
    /// transformación del <see cref="MatchSetup"/> —quita perks, mueve casillas-hogar— y el motor no
    /// puede saber que existe un jefe (RT-011).
    ///
    /// <para>Lo llama <see cref="RunEngine.BuildMatch"/>, así que <b>lo que se juega es exactamente lo
    /// que el informe de ojeo enseña</b> (RF-012b, RF-012d): las dos cosas salen de la misma llamada.
    /// Debe ser pura, determinista e <b>idempotente en la práctica</b> no hace falta: solo se aplica una
    /// vez por partido, en <c>BuildMatch</c>.</para>
    /// </summary>
    /// <param name="playerTeamIndex">Equipo del jugador en el <see cref="MatchSetup"/>: 0 local, 1 visitante (W-15: siempre 0 hoy).</param>
    MatchSetup TransformMatch(RunState state, MapNode node, MatchSetup setup, int playerTeamIndex, Catalog catalog);
}

/// <summary>
/// Implementación de arranque de <see cref="IRunSystems"/>: la que permite que el paquete W juegue una
/// run completa por sí solo, sin economía ni mercado.
///
/// <para><b>Qué hace de verdad</b>: genera rivales procedurales con <c>TeamGenerator</c> (calidad por
/// acto y tipo de nodo) y reparte los árbitros de la run entre los partidos. Es todo lo que hace falta
/// para que los nodos de partido se resuelvan y para que las dos vías de derrota de RF-002b se puedan
/// probar.</para>
///
/// <para><b>Qué es un hueco</b>: <see cref="OpenNode"/> y <see cref="AfterMatch"/> no hacen nada (el
/// nodo se marca como completado y no se cobra oro ni se dan recompensas) y
/// <see cref="ApplyDecision"/> lanza <see cref="NotSupportedException"/> con el nombre del paquete que
/// le toca. El paquete X sustituye esta clase por la suya; no hay que tocar
/// <see cref="RunEngine"/> para ello.</para>
/// </summary>
public sealed class DefaultRunSystems : IRunSystems
{
    /// <summary>
    /// Primer id de jugador de los equipos rivales. Muy por encima de cualquier id que la plantilla del
    /// jugador pueda alcanzar en una run (empieza en 0 y crece de uno en uno), de modo que
    /// <c>Simulator.Run</c> nunca vea ids repetidos entre los dos equipos.
    /// </summary>
    public const int OpponentFirstPlayerId = 1_000_000;

    /// <summary>Calidad del rival de liga del acto 1; cada acto suma <see cref="QualityPerAct"/>.</summary>
    public const int BaseQuality = 45;

    /// <summary>Puntos de calidad que suma cada acto al rival.</summary>
    public const int QualityPerAct = 8;

    /// <summary>Puntos de calidad extra de un partido de élite (RF-011).</summary>
    public const int EliteQualityBonus = 7;

    /// <summary>Puntos de calidad extra de un jefe (RF-001b/c).</summary>
    public const int BossQualityBonus = 12;

    /// <summary>Instancia compartida: la clase no tiene estado.</summary>
    public static DefaultRunSystems Instance { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<RunReferee> CreateReferees(ulong seed, int count, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var referees = new List<RunReferee>(count);
        for (int i = 0; i < count; i++)
        {
            // Rasgo neutro a propósito: los rasgos de árbitro y los sobornos son de fase 3 (RF-061,
            // RF-064) y el balance de la fase 1 se midió con árbitro neutro. El paquete Y los traerá
            // de data/referees/ sin cambiar la forma del estado.
            referees.Add(new RunReferee(i, $"referee_{i}", RefereeTrait.Neutral, 0));
        }

        return referees;
    }

    /// <inheritdoc />
    public TeamSetup OpponentFor(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(catalog);

        var rng = RngStreams.Generation(state.Seed, node.Id);
        int quality = BaseQuality + ((node.Act - 1) * QualityPerAct) + node.Kind switch
        {
            NodeKind.EliteMatch => EliteQualityBonus,
            NodeKind.Boss => BossQualityBonus,
            _ => 0,
        };

        var race = OpponentRace(catalog, node);
        string id = node.OpponentId.Length > 0 ? node.OpponentId : $"rival_{node.Id}";
        return TeamGenerator.Generate(
            ref rng, catalog, id, race, quality, OpponentFirstPlayerId, level: node.Act);
    }

    /// <summary>
    /// Raza del rival procedural: una de las jugables al lanzamiento, elegida por el id del nodo (no
    /// por sorteo, para que dos nodos distintos del mismo acto no salgan siempre iguales).
    /// </summary>
    private static Race OpponentRace(Catalog catalog, MapNode node)
    {
        var races = new List<Race>();
        for (int i = 0; i < catalog.Races.Count; i++)
        {
            if (catalog.Races[i].Launch)
            {
                races.Add(catalog.Races[i].Id);
            }
        }

        if (races.Count == 0)
        {
            for (int i = 0; i < catalog.Races.Count; i++)
            {
                races.Add(catalog.Races[i].Id);
            }
        }

        if (races.Count == 0)
        {
            throw new InvalidOperationException("el catálogo no tiene ninguna raza: no se puede generar un rival");
        }

        races.Sort();
        return races[node.Id % races.Count];
    }

    /// <inheritdoc />
    public RefereeSetup RefereeFor(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        if (state.Referees.Count == 0)
        {
            return new RefereeSetup("referee_0", RefereeTrait.Neutral, 0);
        }

        var referee = state.Referees[node.Id % state.Referees.Count];
        return new RefereeSetup(referee.Name, referee.Trait, 0);
    }

    /// <inheritdoc />
    public SimConfig MatchConfig(RunState state, MapNode node, Catalog catalog) => SimConfig.Default;

    /// <inheritdoc />
    public RunState OpenNode(RunState state, MapNode node, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Hueco del paquete X: el nodo se visita y no pasa nada. No se lanza porque el paquete W tiene
        // que poder jugar una run entera de principio a fin para probar las condiciones de derrota.
        return state;
    }

    /// <inheritdoc />
    public RunState AfterMatch(RunState state, MapNode node, RunMatchSummary summary, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Hueco del paquete X: sin oro (RF-114g..k) y sin recompensas (RF-071).
        return state;
    }

    /// <inheritdoc />
    public RunState ApplyDecision(RunState state, RunDecision decision, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(decision);
        throw new NotSupportedException(
            $"la decisión {decision.GetType().Name} no la resuelve el paquete W: el mercado, la clínica, "
                + "el equipamiento y las recompensas los implementa el paquete X, y el jefe el paquete Y, "
                + "sustituyendo DefaultRunSystems por su propia implementación de IRunSystems.");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> BossRuleModifiers(RunState state, MapNode node, Catalog catalog) =>
        Array.Empty<string>();

    /// <inheritdoc />
    public MatchSetup TransformMatch(RunState state, MapNode node, MatchSetup setup, int playerTeamIndex, Catalog catalog) =>
        setup;
}
