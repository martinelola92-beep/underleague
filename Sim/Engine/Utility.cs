using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Censo de decisiones de la IA (ADR 0144): por cada acción y cada decisión del partido, cuántas veces se
/// <b>eligió</b>, cuántas se <b>descartó</b> por precondición y cuántas <b>compitió y perdió</b>, más lo
/// lejos que se quedó del ganador cuando perdió.
///
/// <para><b>Por qué hace falta y por qué es permanente.</b> Las nueve auditorías de IA del proyecto
/// midieron esto con scripts de usar y tirar, y su propia conclusión fue que «descartada» y «pierde» piden
/// arreglos <b>opuestos</b>: a una acción descartada no la despierta ningún peso, y a una que pierde no la
/// arregla tocarle la precondición. Sin separarlo, cualquier intento de resucitar una acción muerta es a
/// ciegas. La auditoría dejó anotado que estas métricas «nunca se instalaron como permanentes»; ésta lo es.</para>
///
/// <para>Es <b>contabilidad pura</b>: no consume aleatoriedad, no decide nada y está apagado por defecto
/// (<c>SimConfig.Census</c> es null), así que no puede cambiar ningún partido. Los arrays van por índice de
/// <see cref="PlayerAction"/> y por puesto, sin ningún diccionario (RT-041).</para>
/// </summary>
public sealed class UtilityCensus
{
    private static readonly int Actions = Enum.GetValues<PlayerAction>().Length;
    private static readonly int Roles = Enum.GetValues<Position>().Length;

    /// <summary>Veces que cada acción fue la elegida, por puesto.</summary>
    public long[,] Chosen { get; } = new long[Roles, Actions];

    /// <summary>Veces que cada acción se descartó por precondición, por puesto.</summary>
    public long[,] Discarded { get; } = new long[Roles, Actions];

    /// <summary>Veces que cada acción compitió y perdió, por puesto.</summary>
    public long[,] Lost { get; } = new long[Roles, Actions];

    /// <summary>Suma de lo que le faltó a cada acción para ganar, cuando compitió y perdió.</summary>
    public long[,] LostByTotal { get; } = new long[Roles, Actions];

    /// <summary>Cuántas veces se decidió algo, por puesto: el denominador de todo lo demás.</summary>
    public long[] Decisions { get; } = new long[Roles];

    /// <summary>Veces que una acción fue elegida, sumando puestos.</summary>
    public long ChosenTotal(PlayerAction action) => Sum(Chosen, action);

    /// <summary>Veces que una acción se descartó, sumando puestos.</summary>
    public long DiscardedTotal(PlayerAction action) => Sum(Discarded, action);

    /// <summary>Veces que una acción compitió y perdió, sumando puestos.</summary>
    public long LostTotal(PlayerAction action) => Sum(Lost, action);

    /// <summary>Lo que de media le faltó para ganar cuando compitió y perdió; 0 si nunca compitió.</summary>
    public long AverageGap(PlayerAction action)
    {
        long lost = LostTotal(action);
        return lost == 0 ? 0 : Sum(LostByTotal, action) / lost;
    }

    /// <summary>Decisiones totales del censo.</summary>
    public long DecisionsTotal()
    {
        long total = 0;
        for (int r = 0; r < Roles; r++)
        {
            total += Decisions[r];
        }

        return total;
    }

    private static long Sum(long[,] table, PlayerAction action)
    {
        long total = 0;
        for (int r = 0; r < Roles; r++)
        {
            total += table[r, (int)action];
        }

        return total;
    }
}

/// <summary>
/// Vista del mundo que necesita la IA de utilidad (§3.5). El motor la rellena una vez por tick y la
/// reutiliza en todas las decisiones de ese tick: no se asigna nada por evaluación (RT-051).
/// </summary>
internal sealed class UtilityContext
{
    public UtilityContext(
        MatchPlayer[] players, Ball ball, AiWeights weights, ActionZoneTuning zone, float shootBlockRadiusCells, int passSpeedCellsPerTickMilli = 0)
    {
        Players = players;
        Ball = ball;
        Weights = weights;
        Zone = zone;
        ShootBlockRadiusCells = shootBlockRadiusCells;
        PassSpeedCellsPerTickMilli = passSpeedCellsPerTickMilli;

        // -1 y no el 0 por defecto del struct: el índice 0 es un jugador real, así que un array recién
        // creado diría que hay una oferta de pase en vigor desde el primer tick.
        Intent[0].PasserIndex = -1;
        Intent[1].PasserIndex = -1;

        Pressure = new int[players.Length];
        PressureCount = new int[players.Length];
        Openness = new int[players.Length];
        Marked = new bool[players.Length];
        AttackingDepth = new bool[players.Length];
    }

    /// <summary>Todos los jugadores del partido, ordenados por id ascendente (RT-041, RT-097).</summary>
    public MatchPlayer[] Players { get; }

    /// <summary>Balón del partido.</summary>
    public Ball Ball { get; }

    /// <summary>Pesos de IA cargados de data/ai/weights.json (RT-096).</summary>
    public AiWeights Weights { get; }

    /// <summary>Ajustes de la zona de acción, de data/sim/tuning.json (ADR 0028, §2.2).</summary>
    public ActionZoneTuning Zone { get; }

    /// <summary>
    /// Radio de bloqueo de un tiro (AZ-C, docs/plan-segunda-partida.md): el mismo con el que
    /// <c>MatchEngine.TryBlockShot</c> bloquea el tiro en vuelo (<c>data/sim/tuning.json</c>,
    /// <c>pass.interceptRadiusCells</c>). <see cref="EvaluatePass"/> lo reutiliza para decidir si el
    /// portador tiene línea de tiro despejada, sin duplicar el número.
    /// </summary>
    public float ShootBlockRadiusCells { get; }

    /// <summary>Velocidad del pase en milésimas de casilla por tick (tuning.ball), para la carrera del pase en profundidad.</summary>
    public int PassSpeedCellsPerTickMilli { get; }

    /// <summary>
    /// Tick actual del partido. Lo necesita la caducidad de las intenciones de pase (P3): una oferta vale
    /// mientras dure el armado del pase y un poco más, no para siempre.
    /// </summary>
    public int Tick { get; set; }

    /// <summary>Estado táctico por equipo (§3.4).</summary>
    public TacticalState[] TacticalStates { get; } = new TacticalState[2];

    /// <summary>Orden táctica con la que sale cada equipo (ADR 0140); la elige el jugador antes del partido.</summary>
    public Mentality[] Order { get; } = new Mentality[2];

    /// <summary>
    /// Cuántas casillas puede salirse del área el portero de cada equipo <b>en este tick</b> (ADR 0141).
    /// Cero —lo normal— es el portero acotado de siempre.
    /// </summary>
    public float[] KeeperExitCells { get; } = new float[2];

    /// <summary>
    /// Índice del jugador que está ejecutando una reanudación <b>en este mismo tick</b>, o -1 (ADR 0143).
    /// Mientras lo es, no tiene delante todas las acciones con balón: tiene las de <i>su</i> reanudación.
    /// </summary>
    public int RestartTakerIndex { get; set; } = -1;

    /// <summary>Tipo de la reanudación que se está ejecutando; sólo vale si <see cref="RestartTakerIndex"/> lo es.</summary>
    public MatchEngine.RestartKind RestartTakerKind { get; set; }

    /// <summary>Censo de decisiones, o null (el caso normal). Contabilidad pura: ver <see cref="UtilityCensus"/>.</summary>
    public UtilityCensus? Census { get; set; }

    /// <summary>
    /// Hacia qué mentalidad empuja el marcador a cada equipo (ADR 0140): <c>Offensive</c> al que va
    /// perdiendo, <c>Defensive</c> al que va ganando, su propia orden si están empatados.
    /// </summary>
    public Mentality[] UrgencyTarget { get; } = new Mentality[2];

    /// <summary>
    /// Cuánto empuja ese marcador, 0-100 (ADR 0140). Crece con la diferencia de goles <b>y con lo cerca
    /// que está el final</b>: el mismo 0-1 no pide lo mismo en el minuto 10 que en el 89.
    /// </summary>
    public int[] Urgency { get; } = new int[2];

    /// <summary>Compañero más cercano al balón por equipo (empate por id); el perseguidor designado de ChaseBall (AW-S).</summary>
    public MatchPlayer?[] NearestToBall { get; } = new MatchPlayer?[2];

    /// <summary>Equipo que sostiene el balón ahora mismo (dueño o vuelo); -1 si está suelto.</summary>
    public int HoldingTeam { get; set; } = -1;

    // ------------------------------------------------------------ P1: percepción compartida del equipo
    //
    // Gameplay AI Foundations Pass, D1 (docs/plan-gameplay-ai-foundations.md). UtilityContext ya ERA la
    // capa de percepción compartida del proyecto —«la vista del mundo que necesita la IA, rellenada una vez
    // por tick y reutilizada en todas las decisiones de ese tick», RT-051—, así que la percepción del
    // equipo se añade AQUÍ en vez de en un sistema paralelo.
    //
    // No es azúcar: hoy cada Evaluate* redescubre por candidato, por jugador y por tick lo mismo
    // (NearestOpponentDistance, TeammatesNear, quién lleva el balón). Cachearlo una vez QUITA trabajo
    // cuadrático repetido, que es la única justificación que el protocolo de architecture-review acepta
    // para una abstracción nueva.
    //
    // Todo entero y todo indexado por MatchPlayer.Index, rellenado en orden de id ascendente: ningún
    // Dictionary, ningún orden que dependa del tick (RT-041, RT-097, RT-023).

    /// <summary>Portador del balón de cada equipo, o null si ese equipo no lo tiene.</summary>
    public MatchPlayer?[] Carrier { get; } = new MatchPlayer?[2];

    /// <summary>
    /// Presión rival sobre cada jugador, 0-100: 100 pegado al cuerpo del rival más cercano, 0 a partir de
    /// <c>perceptionPressureRadiusCells</c>. Es "cuánto me aprietan", el término que el portador, el pase,
    /// el despeje y la fatiga necesitan y que hoy cada uno calcula por su cuenta.
    /// </summary>
    public int[] Pressure { get; }

    /// <summary>Cuántos rivales hay dentro del radio de presión de cada jugador.</summary>
    public int[] PressureCount { get; }

    /// <summary>
    /// Cuán libre está cada jugador para recibir, 0-100. Es el complemento de <see cref="Pressure"/>
    /// recortado por el marcaje: estar marcado nunca deja a nadie completamente libre.
    /// </summary>
    public int[] Openness { get; }

    /// <summary>True si algún rival tiene a este jugador como <c>MarkTarget</c>. <see cref="Marking"/> ya
    /// calcula el emparejamiento marcador → marcado; nadie leía la dirección contraria.</summary>
    public bool[] Marked { get; }

    /// <summary>
    /// True si este jugador está atacando el espacio a la espalda de la defensa rival. Lo escribe el
    /// arranque coordinado (P3) cuando un compañero le ofrece un pase en profundidad; en P1 es siempre
    /// false y nadie lo lee todavía.
    /// </summary>
    public bool[] AttackingDepth { get; }

    /// <summary>
    /// Amenaza sobre la portería que defiende cada equipo, 0-100: 100 con el balón en la línea de gol, 0 a
    /// partir de <c>perceptionDangerRadiusCells</c>. Es lo que distingue «despejar» de «jugarla».
    /// </summary>
    public int[] Danger { get; } = new int[2];

    /// <summary>True mientras el balón está aparcado para una reanudación (AW-R, docs/pendientes.md):
    /// saque de banda, córner, de puerta, de centro o penalti. Quita el bono de "balón suelto" de
    /// ChaseBall para que nadie converja sobre un balón muerto; el resto de acciones ya se autodescartan
    /// sin él (ver AW-R en docs/pendientes.md para el porqué completo).</summary>
    public bool BallDead { get; set; }

    /// <summary>
    /// Intención de pase en vigor de cada equipo (P3). Es <b>una oferta, no una orden</b>: el receptor
    /// puede ignorarla, y por eso vive en el contexto y no en un campo del receptor.
    /// </summary>
    public PassIntent[] Intent { get; } = new PassIntent[2];
}

/// <summary>
/// Una intención de pase publicada por un pasador durante el <b>armado</b> del pase (P3 del Gameplay AI
/// Foundations Pass).
///
/// <para><b>Por qué no hace falta ningún sistema nuevo.</b> El pase ya tenía una ventana: el pasador entra
/// en <c>Passing</c> y el balón no sale hasta <c>states.PassingTicks</c> ticks después. Esa ventana, que
/// ya existía y no hacía nada, <b>es</b> el canal del arranque coordinado: durante ella el receptor ve que
/// le están ofreciendo un balón a la espalda de la defensa y puede salir hacia allí, de modo que cuando el
/// pase se lanza —<c>Utility.PassTarget</c> ya adelanta el balón a donde el receptor va a estar— los dos
/// están de acuerdo. Un bus de mensajes o una cola de intenciones habría sido un sistema paralelo para
/// algo que el motor ya sabía hacer.</para>
///
/// <para>Una por equipo y con caducidad en ticks: si dos compañeros arman un pase en el mismo tick, gana
/// el último en el recorrido del bucle, que es determinista (RT-041).</para>
/// </summary>
internal struct PassIntent
{
    /// <summary>Índice del que ofrece el pase; -1 si no hay intención en vigor.</summary>
    public int PasserIndex;

    /// <summary>Índice del compañero al que va dirigida.</summary>
    public int ReceiverIndex;

    /// <summary>Casilla a la que se ofrece el balón.</summary>
    public Vec2 Target;

    /// <summary>Último tick en que la oferta sigue en pie.</summary>
    public int ExpiresTick;

    /// <summary>True si la oferta sigue viva en el tick indicado y va dirigida a ese jugador.</summary>
    public readonly bool OfferedTo(int playerIndex, int tick) =>
        PasserIndex >= 0 && ReceiverIndex == playerIndex && tick <= ExpiresTick;
}

/// <summary>
/// IA de utilidad (RT-090..RT-098). Puntúa las acciones legales del estado del jugador con
/// <c>Base * Tactical / 100 * TraitMult / 100 + Context</c> en aritmética entera y elige la mayor;
/// los empates se rompen por el orden del enum (y por tanto por id de jugador al iterar, RT-097).
/// </summary>
internal static class Utility
{
    /// <summary>Margen para que un punto acotado al área quede estrictamente dentro de ella.</summary>
    private const float AreaMargin = 0.05f;

    /// <summary>
    /// Distancia mínima que debe recorrer una acción de movimiento acotada al límite duro exterior para
    /// no ser descartada (§2.2). Es la única forma de descarte que deja la zona de acción: la zona
    /// blanda penaliza, el límite duro descarta.
    /// </summary>
    private const float OuterLimitMinAdvance = 0.25f;

    /// <summary>Radio de aglomeración alrededor del punto de apoyo (§3.5).</summary>
    private const float SupportCrowdRadius = 1.5f;

    /// <summary>
    /// A qué distancia del portador se ofrece la descarga (ADR 0144). No es balance: es la definición de
    /// «corto». Más lejos sería un desmarque, y para eso ya está <see cref="EvaluateFindSpace"/>.
    /// </summary>
    private const float OutletCells = 1.6f;

    /// <summary>Distancia por delante en la que un rival estorba al regate (§3.5).</summary>
    private const float DribbleAheadRadius = 2.0f;

    /// <summary>Radio en el que un rival tapa la línea de pase entre el poseedor y un hueco (§2.3).</summary>

    /// <summary>
    /// Atributo del jugador medio, pivote de las pendientes por atributo de la ADR 0030 §1. No es un
    /// valor de balance sino la definición de "medio" que usa el resto del motor (todas las fórmulas de
    /// <c>tuning.json</c> restan 50), así que vive en código y no en datos.
    /// </summary>
    private const int AttributePivot = 50;

    /// <summary>Etiqueta de estilo que empuja a cargar sin balón (ADR 0024, ADR 0030 §2).</summary>
    private const string BruteTag = "Brute";

    /// <summary>Tope de "espacio" que puntúa un candidato de FindSpace: más allá de 4 casillas da igual.</summary>
    private const int FindSpaceMaxSpaceCenti = 400;

    /// <summary>
    /// Las ocho direcciones de <c>FindSpace</c> (§2.3), de módulo 1. Las diagonales llevan el factor
    /// 0,70711 para que "a una casilla" signifique una casilla de distancia real en las ocho, y no 1,41
    /// en las diagonales. Orden fijo: el desempate entre candidatos es por índice ascendente.
    /// </summary>
    private static readonly Vec2[] SpaceDirections =
    {
        new(1f, 0f),
        new(0.70711f, 0.70711f),
        new(0f, 1f),
        new(-0.70711f, 0.70711f),
        new(-1f, 0f),
        new(-0.70711f, -0.70711f),
        new(0f, -1f),
        new(0.70711f, -0.70711f),
    };

    /// <summary>Las dos distancias a las que se prueba cada dirección de <c>FindSpace</c> (§2.3).</summary>
    private static readonly float[] SpaceDistances = { 1f, 2f };

    /// <summary>Resultado de evaluar una acción concreta; struct para no asignar por evaluación.</summary>
    private struct Eval
    {
        public int Context;
        public bool Discarded;
        public bool OutsideOuterLimit;
        public bool IgnoreOuterLimit;
        public int OutsideCentiCells;
        public Vec2 Target;
        public MatchPlayer? Receiver;
        public MatchPlayer? TackleTarget;
        public bool TackleOffBall;
        public MatchPlayer? BlockTarget;
    }

    /// <summary>
    /// Elige la acción de mayor utilidad para p y deja en el jugador el objetivo de movimiento ya
    /// acotado al límite duro exterior (y al área si es portero), el receptor de pase y el objetivo de
    /// entrada. Si rows no es null, añade una fila por acción evaluada (volcado RT-098).
    /// </summary>
    public static PlayerAction Choose(UtilityContext ctx, MatchPlayer p, List<UtilityRow>? rows)
    {
        var legal = StateMachine.LegalActions(p.State);

        // Con el censo encendido se piden las filas aunque nadie las haya pedido para el volcado: son el
        // mismo dato y duplicar el cálculo sería tener dos verdades sobre la misma decisión.
        if (ctx.Census is not null)
        {
            rows ??= CensusRows;
            rows.Clear();
        }

        int bestScore = 0;
        bool found = false;
        var best = PlayerAction.Retreat;
        Vec2 bestTarget = p.EffectiveHome;
        MatchPlayer? bestReceiver = null;
        MatchPlayer? bestTackleTarget = null;
        bool bestTackleOffBall = false;
        MatchPlayer? bestBlockTarget = null;

        for (int i = 0; i < legal.Count; i++)
        {
            var action = legal[i];

            // ADR 0143: una reanudación no es «el balón en los pies y a jugar». Un saque de banda no se
            // remata, de un córner no se chuta a puerta y de un saque de puerta no se sale regateando.
            // Filtrar aquí —y no corregir la acción después de elegirla— es lo que hace que la decisión
            // sea de verdad la de esa jugada: la utilidad compara sólo entre lo que se puede hacer.
            if (ctx.RestartTakerIndex == p.Index && !RestartAllows(ctx.RestartTakerKind, action))
            {
                continue;
            }

            var eval = Evaluate(ctx, p, action);
            int baseWeight = ctx.Weights.Base(p.Role, action);
            int tactical = ctx.Weights.Tactical(ctx.TacticalStates[p.Team], action);
            int mentality = EffectiveMentality(ctx, p.Team, action);
            // El bono de Leader de los compañeros con casilla-hogar contigua, y el de un efecto de perk
            // modifyUtility (C1, docs/analisis/c1-piloto-cazagoles-diseno.md) si lo hay, entran los dos en
            // el multiplicador de rasgos: la fórmula de §3.5 sigue siendo Base * Tactical / 100 * TraitMult
            // / 100 + Context. Utility.Choose no sabe qué perk es: PerkActionBonusPercent ya llega evaluado
            // (MatchEngine.UpdateContextCaches recalcula la zona cada tick, no aquí — RT-034).
            int traitMultiplier = p.ActionMultiplier(action)
                * (100 + p.LeaderBonusPercent + p.PerkActionBonusPercent(action)) / 100;
            int score = (baseWeight * tactical / 100 * mentality / 100 * traitMultiplier / 100) + eval.Context;

            bool rejected = eval.Discarded || eval.OutsideOuterLimit;
            // El volcado (RT-098) publica la mentalidad DENTRO del multiplicador táctico y no como una
            // columna nueva: lo que explica una decisión es el producto, y añadir una columna obligaría a
            // cambiar todos los consumidores del volcado para que la cuenta siguiera cuadrando.
            rows?.Add(new UtilityRow(
                action, score, baseWeight, tactical * mentality / 100, traitMultiplier, eval.Context,
                rejected, eval.OutsideCentiCells > 0, eval.OutsideCentiCells));

            if (rejected)
            {
                continue;
            }

            if (!found || score > bestScore)
            {
                found = true;
                bestScore = score;
                best = action;
                bestTarget = eval.Target;
                bestReceiver = eval.Receiver;
                bestTackleTarget = eval.TackleTarget;
                bestTackleOffBall = eval.TackleOffBall;
                bestBlockTarget = eval.BlockTarget;
            }
        }

        if (!found)
        {
            // Todas descartadas (solo posible con datos degenerados): replegar es siempre alcanzable.
            best = PlayerAction.Retreat;
            bestTarget = p.EffectiveHome;
        }

        RecordCensus(ctx, p, rows, best, bestScore, found);

        p.CurrentAction = best;
        p.TargetPoint = bestTarget;
        p.PassReceiver = bestReceiver;
        p.TackleTarget = bestTackleTarget;
        p.TackleOffBall = bestTackleOffBall;
        p.BlockTarget = bestBlockTarget;
        return best;
    }



    /// <summary>
    /// Qué puede hacer quien ejecuta cada reanudación (ADR 0143). No es una lista de balance sino las
    /// reglas del fútbol escritas una vez:
    /// <list type="bullet">
    /// <item>de un <b>saque de banda</b> sólo sale un pase —ni tiro, ni regate, ni centro: no se saca con
    /// el pie—;</item>
    /// <item>de un <b>córner</b> sale un pase corto o un balón al área, nunca un disparo directo;</item>
    /// <item>de un <b>saque de puerta</b>, un pase o un despeje: no se sale regateando desde la portería;</item>
    /// <item>de un <b>saque de centro</b>, un pase;</item>
    /// <item>de una <b>falta</b>, cualquier cosa — incluido el <b>tiro directo</b>, que es lo que la hace
    /// una jugada y no una reanudación más.</item>
    /// </list>
    /// </summary>
    private static bool RestartAllows(MatchEngine.RestartKind kind, PlayerAction action) => kind switch
    {
        MatchEngine.RestartKind.ThrowIn => action is PlayerAction.ShortPass or PlayerAction.LongPass,
        MatchEngine.RestartKind.Corner => action is PlayerAction.ShortPass or PlayerAction.LongPass or PlayerAction.Cross,
        MatchEngine.RestartKind.GoalKick => action is PlayerAction.ShortPass or PlayerAction.LongPass or PlayerAction.Clear,
        MatchEngine.RestartKind.Kickoff => action is PlayerAction.ShortPass or PlayerAction.LongPass,
        _ => true,
    };

    /// <summary>
    /// Multiplicador de mentalidad de <paramref name="team"/> para esta acción (ADR 0140), ya mezclado con
    /// la urgencia.
    ///
    /// <para>La orden del jugador fija el punto de partida y la urgencia lo <b>desplaza</b> hacia la
    /// mentalidad que pide el marcador, en proporción a lo urgente que sea: con urgencia 0 manda la orden
    /// y con urgencia 100 manda el marcador. Es una mezcla y no un salto porque un equipo no cambia de
    /// carácter de golpe en el minuto 80: empieza a estirarse antes.</para>
    ///
    /// <para>Aritmética entera (RT-023). Si la orden ya coincide con lo que pide el marcador —un equipo
    /// ofensivo que va perdiendo— la mezcla no hace nada, que es justo lo que debe pasar.</para>
    /// </summary>
    private static int EffectiveMentality(UtilityContext ctx, int team, PlayerAction action)
    {
        int ordered = ctx.Weights.Mentality(ctx.Order[team], action);
        int urgency = ctx.Urgency[team];
        if (urgency <= 0)
        {
            return ordered;
        }

        int target = ctx.Weights.Mentality(ctx.UrgencyTarget[team], action);
        return ordered + ((target - ordered) * urgency / 100);
    }


    /// <summary>
    /// Buffer reutilizable de filas para el censo: se vacía en cada decisión, así que no asigna por
    /// decisión (RT-051). No es compartido entre hilos porque <c>/Sim</c> no es reentrante y cada hilo del
    /// arnés juega con su propio catálogo y su propio motor.
    /// </summary>
    [ThreadStatic]
    private static List<UtilityRow>? _censusRows;

    private static List<UtilityRow> CensusRows => _censusRows ??= new List<UtilityRow>();

    /// <summary>
    /// Apunta en el censo qué pasó con cada acción en esta decisión (ADR 0144): elegida, descartada por
    /// precondición, o compitió y perdió — y por cuánto.
    /// </summary>
    private static void RecordCensus(
        UtilityContext ctx, MatchPlayer p, List<UtilityRow>? rows, PlayerAction best, int bestScore, bool found)
    {
        var census = ctx.Census;
        if (census is null || rows is null)
        {
            return;
        }

        int role = (int)p.Role;
        census.Decisions[role]++;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int action = (int)row.Action;

            if (row.Rejected)
            {
                census.Discarded[role, action]++;
                continue;
            }

            if (found && row.Action == best)
            {
                census.Chosen[role, action]++;
                continue;
            }

            census.Lost[role, action]++;
            census.LostByTotal[role, action] += bestScore - row.Score;
        }
    }

    /// <summary>
    /// Acota target al <b>límite duro exterior</b> de la zona de acción de p (§2.2). Es el único tope
    /// espacial que sigue siendo un muro: dentro de él la zona blanda solo penaliza.
    /// </summary>
    public static Vec2 ClampToZone(MatchPlayer p, Vec2 target) =>
        p.OuterZone.Clamp(target, p.EffectiveHome, Pitch.AttackDirection(p.Team));

    /// <summary>Distancia en casillas a la que un punto queda fuera de la zona blanda de p (0 si dentro).</summary>
    public static float DistanceOutsideZone(MatchPlayer p, Vec2 point) =>
        p.Zone.DistanceOutside(point, p.EffectiveHome, Pitch.AttackDirection(p.Team));

    /// <summary>Acota un punto al rectángulo del área que defiende team, con margen (RF-057b).</summary>
    /// <summary>
    /// Destino de un pase al pie (AZ-B paso 1): el balón se adelanta por lo que el receptor va a poder
    /// recorrer hacia donde <b>él</b> ha decidido ir (<c>TargetPoint</c>, elegido por su utilidad este mismo
    /// tick), nunca más allá de su intención ni de <paramref name="maxLeadCells"/>. Antes se extrapolaba
    /// <c>Velocity</c> —el paso del último tick, con el empuje de cuerpos dentro— durante todo el vuelo:
    /// 1,44 casillas de adelanto medio y un 14,3 % de pases sin nadie en el destino al llegar.
    /// </summary>
    /// <summary>
    /// Factor de proximidad de la intercepción (AZ-B paso 2), en tanto por ciento y aritmética entera:
    /// 100 en el borde del radio, <paramref name="contactPercent"/> cuando el balón pasa por dentro del
    /// cuerpo del rival, lineal entre medias. Antes la distancia no entraba en la tirada.
    /// </summary>
    /// <summary>
    /// Peligro del pasillo (AZ-B paso 3), entero 0..100: 0 si ningún rival está a menos de
    /// <paramref name="laneRadiusCells"/> del segmento, 100 si el segmento pasa por dentro del cuerpo de
    /// alguno (<c>bodyRadius</c> de su raza), lineal entre medias, y <b>el peor</b>, no la suma. Puntúa,
    /// nunca descarta: es la diferencia con el paso 4 descartado de <c>plan-intercepcion-disparo.md</c>.
    /// </summary>
    internal static int LaneDanger(MatchPlayer[] players, int team, Vec2 from, Vec2 to, float laneRadiusCells, bool ignoreGoalkeeper = false)
    {
        int worst = 0;
        int radiusCenti = Centi(laneRadiusCells);
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            // Para el pasillo del TIRO el portero no cuenta: el segmento al centro de la portería pasa
            // siempre por él y su parada ya tiene su propia tirada (save.reachCells). Con él dentro, el
            // paso 4 hundía los tiros de 11 a 4 por partido.
            if (other.Team == team || !other.OnPitch || (ignoreGoalkeeper && !other.IsOutfield))
            {
                continue;
            }

            float d = DistanceToSegment(from, to, other.Position);
            if (d >= laneRadiusCells)
            {
                continue;
            }

            int danger = LaneDangerPercent(Centi(d), other.BodyRadiusCentiCells, radiusCenti);
            if (danger > worst)
            {
                worst = danger;
            }
        }

        return worst;
    }

    internal static int LaneDangerPercent(int distanceCenti, int contactCenti, int radiusCenti)
    {
        if (distanceCenti <= contactCenti)
        {
            return 100;
        }

        if (distanceCenti >= radiusCenti || radiusCenti <= contactCenti)
        {
            return 0;
        }

        return 100 * (radiusCenti - distanceCenti) / (radiusCenti - contactCenti);
    }

    internal static int ProximityFactorPercent(int distanceCenti, int contactCenti, int radiusCenti, int contactPercent)
    {
        if (distanceCenti <= contactCenti)
        {
            return contactPercent;
        }

        if (distanceCenti >= radiusCenti || radiusCenti <= contactCenti)
        {
            return 100;
        }

        return 100 + ((contactPercent - 100) * (radiusCenti - distanceCenti) / (radiusCenti - contactCenti));
    }

    internal static Vec2 PassTarget(Vec2 position, Vec2 intention, int speedPerTickMilli, int ticks, float maxLeadCells)
    {
        var toIntention = intention - position;
        float intent = toIntention.Length;
        if (intent <= 0.001f)
        {
            return position;
        }

        float reachable = ticks * speedPerTickMilli / 1000f;
        float lead = MathF.Min(MathF.Min(reachable, intent), maxLeadCells);
        return ClampToPitch(position + (toIntention * (lead / intent)));
    }

    public static Vec2 ClampToArea(Vec2 point, int team) => ClampToArea(point, team, 0f);

    /// <summary>
    /// Acota un punto al área que defiende <paramref name="team"/>, <b>ensanchada</b> en
    /// <paramref name="extraCells"/> casillas (ADR 0141).
    ///
    /// <para>Con <c>extraCells = 0</c> es el clamp de siempre, bit a bit. Con un valor positivo es el
    /// único sitio por el que un portero puede salir del área, y por eso la salida es una <b>excepción
    /// acotada</b> y no un permiso: el portero nunca deja de estar acotado, se le mueve el límite.</para>
    /// </summary>
    public static Vec2 ClampToArea(Vec2 point, int team, float extraCells)
    {
        float minX = team == 0 ? 0f : Pitch.Columns - Pitch.AreaColumns + AreaMargin - extraCells;
        float maxX = team == 0 ? Pitch.AreaColumns - AreaMargin + extraCells : Pitch.Columns;
        float x = Math.Clamp(point.X, minX, maxX);
        float y = Math.Clamp(point.Y, Pitch.AreaTop - extraCells, Pitch.AreaBottom + extraCells);
        return ClampToPitch(new Vec2(x, y));
    }

    /// <summary>Acota un punto al rectángulo del campo.</summary>
    public static Vec2 ClampToPitch(Vec2 point) =>
        new(Math.Clamp(point.X, 0f, Pitch.Columns), Math.Clamp(point.Y, 0f, Pitch.Rows));

    /// <summary>
    /// Columna de la línea defensiva de <paramref name="team"/> (AW-Q, docs/pendientes.md): la de su
    /// defensa de campo más retrasado (más cerca de su propia portería), o la del balón si el balón está
    /// aún más retrasado hacia esa portería, lo que esté más avanzado de los dos. Mismo algoritmo que
    /// AI_GetOffsideLine de gfootball/HELIOS-base (docs/referencia-motores-futbol.md §6.1: "segundo rival
    /// más adelantado, excluido el más adelantado que suele ser el portero, máximo con la columna del
    /// balón"). El portero no entra en la cuenta porque ya lo excluye <c>IsOutfield</c>: en este motor
    /// siempre es el jugador más retrasado de los dos (RF-057b lo mantiene dentro del área), así que no
    /// hace falta distinguirlo aparte como hacen esos motores al recorrer una lista que sí lo incluye.
    /// <para>
    /// No recorta al resultado contra la mitad del campo (a diferencia de gfootball): los dos usos de esta
    /// función ya acotan el resultado por su cuenta (el techo del bloque con un margen pequeño, el recorte
    /// de <c>FindSpace</c> con la pinza de zona existente), así que ese caso límite no hace falta aquí.
    /// </para>
    /// <para>
    /// Caso degenerado sin jugadores de campo sobre el césped (equipo entero expulsado o lesionado): el
    /// avance más retrasado se queda en 0, así que la línea es la del balón, y nunca por detrás de la
    /// propia línea de gol.
    /// </para>
    /// </summary>
    internal static float DefensiveLineColumn(MatchPlayer[] players, Vec2 ballPosition, int team)
    {
        int direction = Pitch.AttackDirection(team);
        float ownGoalColumn = direction > 0 ? 0f : Pitch.Columns;

        // avance(x): cuánto se ha alejado x de la portería PROPIA de team, hacia la portería rival.
        float deepestAdvance = 0f;
        bool any = false;
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p.Team != team || !p.IsOutfield || !p.OnPitch)
            {
                continue;
            }

            float advance = (p.Position.X - ownGoalColumn) * direction;
            if (!any || advance < deepestAdvance)
            {
                deepestAdvance = advance;
                any = true;
            }
        }

        float ballAdvance = (ballPosition.X - ownGoalColumn) * direction;
        float lineAdvance = MathF.Max(deepestAdvance, ballAdvance);
        return ownGoalColumn + (lineAdvance * direction);
    }

    /// <summary>
    /// Línea de fuera de juego que afronta <paramref name="attackingTeam"/> (AW-Q, docs/pendientes.md):
    /// la columna del rival de campo más retrasado en su propio campo, o la del balón si el balón está más
    /// adelantado que él, lo que esté más avanzado <b>en la dirección de ataque de
    /// <paramref name="attackingTeam"/></b>. Es <c>AI_GetOffsideLine</c> de gfootball tal cual
    /// (docs/referencia-motores-futbol.md §6.1, paso 3: <c>offsideLine = max(segundo_más_adelantado,
    /// posición_del_balón)</c>), con los dos términos medidos en el marco del ATACANTE, que es lo que la
    /// distingue de <see cref="DefensiveLineColumn"/>: aquella mide el avance desde la portería del equipo
    /// que defiende, así que su máximo con el balón elige el punto contrario cuando el balón viene por
    /// detrás de la defensa rival —el caso normal de una jugada de ataque— y dejaría al atacante clavado
    /// a la altura del balón. El jugador rival escogido es el mismo en las dos (el más retrasado del
    /// rival); solo cambia el sentido del máximo con el balón.
    /// <para>
    /// El portero rival queda fuera por <c>IsOutfield</c>, que es justo el paso 2 de gfootball (excluir al
    /// más adelantado, que suele ser el portero) sin necesidad de una segunda pasada.
    /// </para>
    /// </summary>
    internal static float OffsideLineColumn(MatchPlayer[] players, Vec2 ballPosition, int attackingTeam)
    {
        int direction = Pitch.AttackDirection(attackingTeam);
        float ownGoalColumn = direction > 0 ? 0f : Pitch.Columns;

        // avance(x): cuánto se ha alejado x de la portería propia de attackingTeam. El balón entra como un
        // candidato más, igual que en gfootball: nunca hay fuera de juego por detrás del balón.
        float lineAdvance = (ballPosition.X - ownGoalColumn) * direction;
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p.Team == attackingTeam || !p.IsOutfield || !p.OnPitch)
            {
                continue;
            }

            float advance = (p.Position.X - ownGoalColumn) * direction;
            if (advance > lineAdvance)
            {
                lineAdvance = advance;
            }
        }

        return ownGoalColumn + (lineAdvance * direction);
    }

    /// <summary>
    /// Recorta <paramref name="rawX"/> contra la línea defensiva <paramref name="line"/> más un margen
    /// (AW-Q): si la columna pedida está MÁS AVANZADA que el techo en la dirección de ataque del equipo,
    /// se recorta; si está por detrás o justo en él, no se toca. El techo solo frena, nunca empuja hacia
    /// adelante. Función pura y separada del bucle de <c>MatchEngine.UpdateBlockShift</c> para poder
    /// probar la aritmética sin montar un partido (mismo criterio que <c>MatchEngine.WithinSaveReach</c>).
    /// </summary>
    internal static float CapToDefensiveLine(float rawX, float line, float marginCells, int direction)
    {
        float capX = line + (marginCells * direction);
        return (rawX - capX) * direction > 0f ? capX : rawX;
    }

    /// <summary>
    /// Convierte una distancia en casillas al entero de centésimas usado en los términos (§3.5).
    /// Floor explícito (revisión independiente, fase 0): el cast directo a int trunca hacia cero, así que
    /// -0.5 casillas se convertía en 0 pero 0.5 se convertía en 50, un salto asimétrico justo alrededor de
    /// cero. La mayoría de llamadas pasan una distancia no negativa (Vec2.Distance), pero
    /// <c>EvaluatePass</c> también la usa sobre un "avance" con signo; con floor, los dos lados de cero se
    /// tratan igual.
    /// </summary>
    public static int Centi(float cells) => (int)MathF.Floor(cells * 100f);

    /// <summary>Acerca value a target en un paso como máximo.</summary>
    private static float MoveToward(float value, float target, float step)
    {
        if (value < target)
        {
            return value + step > target ? target : value + step;
        }

        return value - step < target ? target : value - step;
    }

    /// <summary>
    /// Evalúa una acción. Orden de resolución fijo para las acciones de movimiento (§2.2), documentado
    /// porque de él dependen tanto el resultado como el volcado de utilidad:
    /// <list type="number">
    /// <item>la acción produce un punto objetivo bruto y su término de contexto propio;</item>
    /// <item>el punto se acota al límite duro exterior (y al área, si es portero);</item>
    /// <item>se mide a qué distancia queda el punto <b>ya acotado</b> fuera de la zona blanda;</item>
    /// <item>esa distancia descuenta del contexto, ponderada por la disciplina del jugador;</item>
    /// <item>si la acción exigía salir del límite duro y, acotada, ya no avanza nada, se descarta.</item>
    /// </list>
    /// </summary>
    private static Eval Evaluate(UtilityContext ctx, MatchPlayer p, PlayerAction action)
    {
        var eval = default(Eval);
        eval.Target = p.Position;
        var context = ctx.Weights.Context;
        int direction = Pitch.AttackDirection(p.Team);
        var ball = ctx.Ball;

        switch (action)
        {
            case PlayerAction.ChaseBall:
                EvaluateChaseBall(ctx, p, context, ref eval);
                break;
            case PlayerAction.MarkOpponent:
                EvaluateMark(ctx, p, context, ref eval);
                break;
            case PlayerAction.OfferSupport:
                EvaluateSupport(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.CoverSpace:
                EvaluateCover(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.ShortPass:
                EvaluatePass(ctx, p, context, direction, longPass: false, ref eval);
                break;
            case PlayerAction.LongPass:
                EvaluatePass(ctx, p, context, direction, longPass: true, ref eval);
                break;
            case PlayerAction.ThroughPass:
                EvaluateThroughPass(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.Cross:
                EvaluateCross(ctx, p, context, ref eval);
                break;
            case PlayerAction.Dribble:
                EvaluateDribble(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.Shoot:
                EvaluateShoot(ctx, p, context, ref eval);
                break;
            case PlayerAction.Block:
                EvaluateBlock(ctx, p, context, ref eval);
                break;
            case PlayerAction.Tackle:
                EvaluateTackle(ctx, p, context, ref eval);
                break;
            case PlayerAction.Retreat:
                EvaluateRetreat(ctx, p, context, ref eval);
                break;
            case PlayerAction.FindSpace:
                EvaluateFindSpace(ctx, p, direction, ref eval);
                break;
            case PlayerAction.PressCarrier:
                EvaluatePress(ctx, p, ref eval);
                break;
            case PlayerAction.Shield:
                EvaluateShield(ctx, p, context, ref eval);
                break;
            case PlayerAction.Clear:
                EvaluateClear(ctx, p, context, ref eval);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        // ADR 0142: el cansado deja de presionar. Es la mitad del cansancio que las pendientes de
        // atributo no cubren —perseguir, presionar y pegarse no tienen pendiente— y es justo donde más se
        // nota en un campo de verdad: no es que el cansado persiga peor, es que deja de ir.
        if (!eval.Discarded && IsEffortAction(action) && p.TiredPercent > 0)
        {
            eval.Context -= context.TiredEffortPenalty * p.TiredPercent / 100;
        }

        // ADR 0145: REPRESALIA. Si el objetivo de esta entrada o de esta carga es justo el que le rompió a
        // un compañero delante de mí, vale más. Se aplica aquí, sobre el objetivo YA elegido por cada
        // evaluador, porque el rencor no cambia a quién voy: cambia cuánto me apetece ir.
        if (!eval.Discarded && p.GrudgeTicks > 0 && p.GrudgeTarget is { } grudge)
        {
            bool againstGrudge =
                (action == PlayerAction.Tackle && ReferenceEquals(eval.TackleTarget, grudge))
                || (action == PlayerAction.Block && ReferenceEquals(eval.BlockTarget, grudge));

            if (againstGrudge)
            {
                eval.Context += context.GrudgeBonus;
            }
        }

        if (eval.Discarded || !IsMovementAction(action))
        {
            return eval;
        }

        Vec2 raw = eval.Target;
        Vec2 clamped = ClampToZone(p, raw);
        if (!p.IsOutfield)
        {
            clamped = ClampToArea(clamped, p.Team, ctx.KeeperExitCells[p.Team]);
        }

        float outside = DistanceOutsideZone(p, clamped);
        if (outside > 0f)
        {
            eval.OutsideCentiCells = Centi(outside);
            eval.Context -= OutsidePenalty(ctx, p, eval.OutsideCentiCells);
        }

        bool beyondOuterLimit = p.OuterZone.DistanceOutside(raw, p.EffectiveHome, direction) > 0f;
        if (!eval.IgnoreOuterLimit && beyondOuterLimit
            && Vec2.Distance(clamped, p.Position) < OuterLimitMinAdvance)
        {
            eval.OutsideOuterLimit = true;
        }

        eval.Target = clamped;
        return eval;
    }

    /// <summary>
    /// Penalización de salida de zona (§2.2): <c>outsidePenaltyPerCell × distanciaFuera × disciplina</c>,
    /// con la disciplina leída como porcentaje 0-100 (un enano de 80 paga 0,8 veces la tarifa completa y
    /// un elfo de 35 paga 0,35) y modulada por <c>disciplineWeightPercent</c>. Todo entero (RT-023).
    /// </summary>
    private static int OutsidePenalty(UtilityContext ctx, MatchPlayer p, int outsideCentiCells)
    {
        var zone = ctx.Zone;
        return zone.OutsidePenaltyPerCell * outsideCentiCells / 100
            * p.Discipline / 100
            * zone.DisciplineWeightPercent / 100;
    }

    /// <summary>
    /// Las acciones que se pagan con piernas (ADR 0142). No incluye conducir ni proteger: ésas ya cuestan
    /// energía al ejecutarse y además pierden solas al cansarse, porque su utilidad va por pendiente de
    /// técnica. Aquí están las que, sin esto, un jugador vacío seguiría eligiendo igual que entero.
    /// </summary>
    private static bool IsEffortAction(PlayerAction action) =>
        action is PlayerAction.ChaseBall or PlayerAction.PressCarrier
            or PlayerAction.Tackle or PlayerAction.Block;

    private static bool IsMovementAction(PlayerAction action) =>
        action is PlayerAction.ChaseBall or PlayerAction.MarkOpponent or PlayerAction.OfferSupport
            or PlayerAction.CoverSpace or PlayerAction.Dribble or PlayerAction.Retreat
            or PlayerAction.FindSpace or PlayerAction.PressCarrier
            // Proteger mueve al portador —poco y apartándose del que le aprieta— así que su destino pasa
            // por el mismo recorte de zona que el resto: no es una excepción espacial.
            or PlayerAction.Shield;

    /// <summary>
    /// Cuánto se aparta del rival el que protege el balón, en casillas. No es un número de balance sino la
    /// definición de la acción: proteger es <b>interponer el cuerpo</b>, no huir ni avanzar. Si fuera
    /// mayor sería conducir de espaldas; si fuera cero, el portador sería una estatua y el rival le
    /// rodearía sin esfuerzo.
    /// </summary>
    private const float ShieldStepCells = 0.35f;

    /// <summary>
    /// Proteger el balón (Gameplay AI Foundations Pass, P2). Precondición <b>dura</b>: sin nadie
    /// apretando no hay nada que proteger, y la acción ni se puntúa. Es la misma forma que ya usan el
    /// centro (compañero en la zona de remate) y la entrada (rival al alcance): una precondición que
    /// describe la <i>situación futbolística</i>, no una penalización enorme que la disfrace.
    ///
    /// <para>Lo demás puntúa: cuanto más te aprietan, más vale aguantar; y la fuerza es lo que hace
    /// viable aguantar, igual que la técnica es lo que hace viable regatear.</para>
    /// </summary>
    private static void EvaluateShield(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        if (!ReferenceEquals(ctx.Ball.Owner, p))
        {
            eval.Discarded = true;
            return;
        }

        int pressure = ctx.Pressure[p.Index];
        if (pressure < context.ShieldMinPressure)
        {
            eval.Discarded = true;
            return;
        }

        // El cuerpo se interpone entre el balón y quien aprieta: el portador se aparta del rival más
        // cercano, que es exactamente lo que hace un delantero de espaldas.
        var presser = NearestOpponent(ctx, p);
        Vec2 away = presser is null
            ? new Vec2(0f, 0f)
            : (p.Position - presser.Position).Normalized;
        eval.Target = ClampToPitch(p.Position + (away * ShieldStepCells));

        eval.Context = context.ShieldBase
            + (context.ShieldPressureBonusPerCenti * pressure)
            + Slope(context.ShieldStrengthSlope, p.Strength);
    }

    /// <summary>
    /// Despejar (Gameplay AI Foundations Pass, P4). Simétrica de <see cref="EvaluateShield"/>: la
    /// precondición dura es el <b>peligro</b>, porque un despeje sin peligro no es prudencia, es regalar el
    /// balón. Puntúan el peligro y la presión; la fuerza <b>no</b> entra en la decisión —entra en la
    /// distancia que recorre el balón, que es donde se nota quién despeja—.
    /// </summary>
    private static void EvaluateClear(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        if (!ReferenceEquals(ctx.Ball.Owner, p))
        {
            eval.Discarded = true;
            return;
        }

        int danger = ctx.Danger[p.Team];
        if (danger < context.ClearMinDanger)
        {
            eval.Discarded = true;
            return;
        }

        eval.Context = context.ClearBase
            + (context.ClearDangerBonusPerCenti * danger)
            + (context.ClearPressureBonusPerCenti * ctx.Pressure[p.Index]);
    }

    private static void EvaluateChaseBall(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        var ball = ctx.Ball;
        Vec2 point = ball.InFlight ? ball.FlightTarget : ball.Position;
        bool loose = ball.Owner is null && !ball.InFlight && !ctx.BallDead;

        // ADR 0141: el portero persigue el balón suelto DENTRO de su área... y también fuera de ella
        // cuando este tick se le permite salir. Sin esta segunda mitad, ensanchar el clamp no habría
        // servido de nada: el clamp decide hasta dónde puede llegar, pero esta precondición decide si
        // quiere ir, y con ella cerrada el rasgo «Sale mucho» seguiría sin poder cumplir su nombre —que
        // es exactamente el defecto que esta ADR viene a arreglar—.
        if (!p.IsOutfield && (!loose || !CanKeeperReachLoose(ctx, p, point)))
        {
            eval.Discarded = true;
            return;
        }

        // AW-S (docs/pendientes.md): precondición dura, no penalización. Los tres motores de referencia
        // (docs/referencia-motores-futbol.md §6.3) descalifican a quien no es el perseguidor designado en vez
        // de penalizarlo; con la penalización blanda (chaseBallNotNearestPenalty) el segundo y el tercer
        // defensa más cercanos seguían pudiendo elegir perseguir. La excepción es quien ya tenía su propio
        // motivo documentado para ir a por el balón sin ser el más cercano: el receptor previsto de un pase en
        // vuelo (si no, el balón se quedaba suelto en el 42% de los pases, paquete E).
        //
        // Sin histéresis en esta pasada: los motores de referencia solo relevan al designado si un aspirante
        // es notablemente mejor, para que la designación no oscile tick a tick. Añadir eso exige estado nuevo
        // por equipo (persistencia del "perseguidor designado", cuándo se resetea, orden determinista de
        // actualización) — un cambio de arquitectura mayor que se deja para una vuelta futura si el lote de
        // balance muestra que hace falta. `ctx.NearestToBall[team]` ya se recalcula una vez por tick sin
        // histéresis (en `MatchEngine.UpdateContextCaches`, antes de que nadie decida nada ese tick) y este
        // cambio se apoya en eso tal cual.
        bool isIncomingPassReceiver = ball.InFlight && !ball.IsShot && ReferenceEquals(ball.PassReceiver, p);
        if (!ReferenceEquals(ctx.NearestToBall[p.Team], p) && !isIncomingPassReceiver)
        {
            eval.Discarded = true;
            return;
        }

        eval.Target = point;
        int distance = Centi(Vec2.Distance(p.Position, point));
        int score = -(context.ChaseBallDistancePenaltyPerCell * distance / 100);
        if (loose)
        {
            score += context.ChaseBallLooseBonus;
        }

        if (isIncomingPassReceiver)
        {
            score += context.ChaseBallIncomingPassBonus;
            eval.IgnoreOuterLimit = true;
        }

        eval.Context = score;
    }

    /// <summary>
    /// Marcaje con objetivo estable (§2.3): el rival lo fija <see cref="Marking"/> una vez por posesión.
    /// Si todavía no hay asignación (arranque del partido antes de la primera posesión, o un contexto de
    /// prueba construido a mano) se usa el rival más cercano, que es el comportamiento de la fase 0.
    /// </summary>

    /// <summary>
    /// ¿Está ese balón suelto dentro de lo que el portero puede cubrir <b>este tick</b> (ADR 0141)? Su área
    /// siempre; y, cuando se le permite salir, el área ensanchada por el mismo número que le mueve el
    /// límite de movimiento. Los dos sitios leen la misma cifra a propósito: si la decisión y el clamp
    /// pudieran discrepar, el portero querría ir a sitios a los que no puede llegar.
    /// </summary>
    private static bool CanKeeperReachLoose(UtilityContext ctx, MatchPlayer p, Vec2 point)
    {
        if (Pitch.IsInArea(point, p.Team))
        {
            return true;
        }

        float extra = ctx.KeeperExitCells[p.Team];
        if (extra <= 0f)
        {
            return false;
        }

        return Vec2.Distance(ClampToArea(point, p.Team), point) <= extra;
    }

    private static void EvaluateMark(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        if (!p.IsOutfield)
        {
            eval.Discarded = true;
            return;
        }

        var target = Marking.IsValidTarget(p.MarkTarget, p.Team) ? p.MarkTarget : NearestOpponent(ctx, p);
        if (target is null)
        {
            eval.Discarded = true;
            return;
        }

        eval.Target = target.Position;
        eval.Context = -(context.MarkDistancePenaltyPerCell * Centi(Vec2.Distance(p.Position, target.Position)) / 100);
    }

    private static MatchPlayer? NearestOpponent(UtilityContext ctx, MatchPlayer p)
    {
        MatchPlayer? nearest = null;
        float bestDistance = 0f;
        var players = ctx.Players;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == p.Team || !other.OnPitch || !other.IsOutfield)
            {
                continue;
            }

            float distance = Vec2.Distance(p.Position, other.Position);
            if (nearest is null || distance < bestDistance)
            {
                nearest = other;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// <b>La descarga</b> (ADR 0144): venir <b>corto</b> a dar salida a un compañero al que están
    /// apretando, aunque eso signifique ir hacia atrás.
    ///
    /// <para><b>Por qué cambia entera.</b> Esta acción llevaba muerta desde que la ADR 0022 creó
    /// <see cref="EvaluateFindSpace"/> «para sustituir su punto fijo» y nadie la retiró. Iba a un punto
    /// fijo dos casillas por delante del portador, competía contra dieciséis candidatos evaluados y perdía
    /// siempre: medido con el censo, <b>0,01 elecciones por mil decisiones</b> y un hueco medio de 803
    /// puntos las pocas veces que llegaba a competir. No estaba mal calibrada: no representaba ninguna
    /// situación que FindSpace no cubriera mejor.</para>
    ///
    /// <para>Ahora sí tiene la suya, y es la que le faltaba al motor desde que el portador puede verse
    /// atrapado (ADR 0141): cuando a tu compañero le aprietan, alguien tiene que ofrecerse <b>cerca</b>.
    /// FindSpace busca alejarse de los rivales y avanzar; esto es lo contrario, y por eso no se pisan.</para>
    ///
    /// <para>Precondición dura: <b>sin un compañero apretado no hay descarga que dar</b>. Es la misma forma
    /// que proteger y despejar (ADR 0138) — describir la situación en vez de disfrazarla con una
    /// penalización enorme.</para>
    /// </summary>
    private static void EvaluateSupport(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        if (!p.IsOutfield || ctx.HoldingTeam != p.Team)
        {
            eval.Discarded = true;
            return;
        }

        var carrier = ctx.Carrier[p.Team];
        if (carrier is null || ReferenceEquals(carrier, p))
        {
            eval.Discarded = true;
            return;
        }

        int pressure = ctx.Pressure[carrier.Index];
        if (pressure < context.SupportMinCarrierPressure)
        {
            eval.Discarded = true;
            return;
        }

        // El punto de descarga es cerca del portador y por MI lado: el que se ofrece no cruza el campo, se
        // acerca. Si estamos superpuestos se toma la dirección de ataque, que al menos es una salida.
        var offset = p.Position - carrier.Position;
        var away = offset.Length > 0.01f ? offset.Normalized : new Vec2(direction, 0f);
        var target = ClampToPitch(carrier.Position + (away * OutletCells));
        eval.Target = target;

        int score = context.SupportBase + (context.SupportPressedBonusPerCenti * pressure);

        // Una salida por delante sigue valiendo más que una por detrás, en igualdad de todo lo demás.
        if ((target.X - carrier.Position.X) * direction > 0f)
        {
            score += context.SupportAheadBonus;
        }

        // Y no vale amontonarse: si ya hay alguien ofreciéndose ahí, el segundo no aporta nada.
        score -= context.SupportCrowdedPenalty * TeammatesNear(ctx.Players, p, target, SupportCrowdRadius);

        eval.Context = score;
    }

    /// <summary>
    /// Búsqueda de espacio (ADR 0022, §2.3). Puntúa las ocho direcciones a una y a dos casillas —dieciséis
    /// puntos: el "8 candidatos" de §2.3 son las ocho direcciones— con tres términos enteros: distancia al
    /// rival más cercano (con tope), avance hacia la portería rival y línea de pase abierta con el
    /// poseedor. Los candidatos se acotan a la zona blanda y al campo, así que buscar hueco nunca es la
    /// acción que saca a un jugador de su zona. Empate por índice de candidato ascendente.
    /// </summary>
    private static void EvaluateFindSpace(UtilityContext ctx, MatchPlayer p, int direction, ref Eval eval)
    {
        var context = ctx.Weights.Context;
        var ball = ctx.Ball;
        if (!p.IsOutfield || ctx.HoldingTeam != p.Team || ReferenceEquals(ball.Owner, p))
        {
            eval.Discarded = true;
            return;
        }

        var carrier = ball.Owner is not null && ball.Owner.Team == p.Team ? ball.Owner : null;
        var players = ctx.Players;

        // AW-Q: la línea defensiva rival no depende de la casilla candidata, así que se calcula una vez
        // por evaluación y no una por candidato (RT-051: la evaluación no debe crecer con el tablero).
        float marginedLine = OffsideLineColumn(players, ball.Position, p.Team)
            + (context.FindSpaceLineMarginCells * direction);

        // P3: la oferta de pase al espacio, leída una vez por evaluación y no una por candidato (RT-051).
        var intent = ctx.Intent[p.Team];
        bool offered = intent.OfferedTo(p.Index, ctx.Tick);
        Vec2 intentTarget = offered ? intent.Target : default;
        int intentRadiusCenti = offered ? Centi(context.FindSpaceIntentRadiusCells) : 0;

        bool found = false;
        int bestScore = 0;
        Vec2 bestPoint = p.Position;

        for (int d = 0; d < SpaceDirections.Length; d++)
        {
            for (int s = 0; s < SpaceDistances.Length; s++)
            {
                Vec2 candidate = ClampToPitch(p.Position + (SpaceDirections[d] * SpaceDistances[s]));

                // AW-Q (docs/pendientes.md), recorte posicional: la casilla candidata no puede quedar más
                // allá de la línea defensiva rival más un margen. Sin esto el desmarque premiaba acampar a
                // espaldas de la defensa (findSpaceAdvanceBonusPerCell crece sin techo y allí no hay
                // rivales, así que findSpaceOpponentDistanceBonusPerCell también cobra el máximo). Es el
                // mismo mecanismo que forceNoOffside de gfootball y el recorte de formación de HELIOS-base
                // (docs/referencia-motores-futbol.md §6.1), y no pita nada: solo quita la casilla-objetivo.
                // Se aplica a cualquier jugador de campo, no solo al delantero: en un desmarque cualquiera
                // puede rebasar la línea. El recorte va ANTES de la pinza de zona para que la correa de
                // zona siga aplicando después sobre el resultado ya recortado.
                if ((candidate.X - marginedLine) * direction > 0f)
                {
                    candidate = new Vec2(marginedLine, candidate.Y);
                }

                candidate = p.Zone.Clamp(candidate, p.EffectiveHome, direction);

                int space = Centi(NearestOpponentDistance(players, p.Team, candidate));
                if (space > FindSpaceMaxSpaceCenti)
                {
                    space = FindSpaceMaxSpaceCenti;
                }

                int advance = Centi((candidate.X - p.Position.X) * direction);
                int score = (context.FindSpaceOpponentDistanceBonusPerCell * space / 100)
                    + (context.FindSpaceAdvanceBonusPerCell * advance / 100);

                if (carrier is not null && !SegmentBlocked(players, p.Team, carrier.Position, candidate, context.PassLaneRadiusCells))
                {
                    score += context.FindSpaceOpenLaneBonus;
                }

                // AW-E (docs/pendientes.md, cambio 2 de 2): hasta ahora la única casilla candidata que
                // miraba si ya había compañeros era la de OfferSupport (SupportCrowdedPenalty); FindSpace
                // solo premiaba alejarse del rival, así que dos jugadores podían converger en el mismo
                // hueco sin que nada lo penalizara. Mismo radio que OfferSupport (SupportCrowdRadius),
                // para que "estar apiñado" signifique lo mismo en las dos acciones.
                score -= context.FindSpaceCrowdedPenalty * TeammatesNear(players, p, candidate, SupportCrowdRadius);

                // P3, ARRANQUE COORDINADO: si un compañero está armando ahora mismo un balón al espacio
                // dirigido a mí, las casillas cercanas a ese espacio valen más. El pase en profundidad
                // dejaba de ser un monólogo justo aquí: antes el pasador leía el destino que el receptor
                // ya había elegido por su cuenta, y el receptor no se enteraba de nada.
                //
                // Suma, no manda: un desmarque claramente mejor por espacio o por línea de pase sigue
                // ganando, y eso es deliberado —una intención es una oferta, no una orden—.
                if (intentRadiusCenti > 0)
                {
                    int toIntent = Centi(Vec2.Distance(candidate, intentTarget));
                    if (toIntent < intentRadiusCenti)
                    {
                        score += context.FindSpaceIntentBonus * (intentRadiusCenti - toIntent) / intentRadiusCenti;
                    }
                }

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestPoint = candidate;
                }
            }
        }

        eval.Target = bestPoint;
        eval.Context = bestScore;
    }

    /// <summary>
    /// Presión al poseedor (ADR 0022, §2.3): objetivo el rival que lleva el balón, sea jugador de campo o
    /// el portero en su salida, con un extra si es el portero dentro de su área —el momento en que la
    /// presión gana el balón en zona de gol—.
    /// </summary>
    private static void EvaluatePress(UtilityContext ctx, MatchPlayer p, ref Eval eval)
    {
        var carrier = ctx.Ball.Owner;
        if (!p.IsOutfield || carrier is null || carrier.Team == p.Team)
        {
            eval.Discarded = true;
            return;
        }

        var context = ctx.Weights.Context;
        eval.Target = carrier.Position;
        int distance = Centi(Vec2.Distance(p.Position, carrier.Position));
        int score = context.PressCarrierBonus - (context.PressDistancePenaltyPerCell * distance / 100);
        if (!carrier.IsOutfield && Pitch.IsInArea(carrier.Position, carrier.Team))
        {
            score += context.PressGoalkeeperExitBonus;
        }

        eval.Context = score;
    }

    private static void EvaluateCover(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        var ball = ctx.Ball;
        Vec2 from = ball.Position;
        Vec2 ownGoal = Pitch.GoalCenter(1 - p.Team);
        Vec2 target;

        if (!p.IsOutfield)
        {
            // Portero: punto a 0.7 casillas de la línea de gol sobre la recta portería->balón (§3.5).
            Vec2 toBall = from - ownGoal;
            Vec2 unit = toBall.Normalized;
            target = ownGoal + (unit * 0.7f);
        }
        else
        {
            // El punto por el que la recta balón->portería propia entra en la zona, es decir, el punto
            // más adelantado de esa recta que el jugador puede cubrir sin salirse (§2.2). Sustituye al
            // corte contra el círculo de correa de la fase 0. Si la recta no cruza la zona, se cubre el
            // punto más cercano al balón que la zona permite.
            target = p.Zone.SegmentEntry(from, ownGoal, p.EffectiveHome, direction)
                ?? p.Zone.Clamp(from, p.EffectiveHome, direction);
        }

        eval.Target = target;

        float ballX = from.X;
        float goalX = ownGoal.X;
        float low = MathF.Min(ballX, goalX);
        float high = MathF.Max(ballX, goalX);
        if (p.Position.X >= low && p.Position.X <= high)
        {
            eval.Context = context.CoverBetweenBallAndGoalBonus;
        }
    }

    /// <summary>
    /// Pase, en sus dos bandas de distancia (ADR 0030 §1). Las bandas son <b>disjuntas y exhaustivas</b>:
    /// corto es hasta <c>shortPassMaxCells</c> y largo es de ahí a <c>longPassMaxCells</c>, así que ningún
    /// compañero puntúa en las dos y ninguno se pierde. El portero no tiene tope superior en el pase largo
    /// (el saque de puerta llega a donde llega), pero sí la misma banda inferior: un pase suyo de dos
    /// casillas es un pase corto como el de cualquiera.
    ///
    /// <para>Lo que separa de verdad a las dos acciones es la <b>pendiente por técnica</b>: la del pase
    /// corto es casi plana y la del largo, la más inclinada de la tabla. Un jugador torpe puntúa el pase
    /// largo por debajo de cero y no lo intenta nunca; uno brillante lo pone por encima de conducir. Y el
    /// pase largo exige además <b>línea despejada</b> hasta el receptor: es la contención declarada en la
    /// ADR 0030 contra el partido de balonazos de área a área. Un pase corto se cuela entre cuerpos; uno
    /// de siete casillas, no.</para>
    /// </summary>
    private static void EvaluatePass(
        UtilityContext ctx, MatchPlayer p, AiContext context, int direction, bool longPass, ref Eval eval)
    {
        var players = ctx.Players;
        MatchPlayer? receiver = null;
        int bestRank = 0;
        int bestAdvance = 0;
        int bestDanger = 0;
        int bestPressure = 0;
        int bestHold = 0;

        float minCells = longPass ? context.ShortPassMaxCells : 0f;
        float maxCells = longPass ? context.LongPassMaxCells : context.ShortPassMaxCells;

        // AZ-C (docs/plan-segunda-partida.md): con línea de tiro despejada, un pase hacia atrás ya no es
        // candidato — antes de esto el receptor de mayor rank podía quedar detrás del portador aunque
        // tirar a puerta estuviera libre, así que el equipo se pasaba el balón en vez de rematar.
        // "Solo ante el portero": línea limpia a portería Y ningún rival de campo por delante (AZ-C).
        // Sin la segunda mitad la precondición cubría todo el alcance de tiro y convertía demasiados
        // pases en tiros: passChainAvgLength caía de 2,31 a 1,93 (banda 2-4).
        bool clearShot = HasClearShot(players, p, context, ctx.ShootBlockRadiusCells)
            && OpponentsAheadCount(players, p, direction) == 0;

        for (int i = 0; i < players.Length; i++)
        {
            var mate = players[i];
            if (mate.Team != p.Team || ReferenceEquals(mate, p) || !mate.OnPitch)
            {
                continue;
            }

            float distance = Vec2.Distance(p.Position, mate.Position);
            if (distance <= minCells || (p.IsOutfield && distance > maxCells))
            {
                continue;
            }

            // ADR 0141: EL RECEPTOR PRESIONADO YA NO SE DESCARTA. Antes bastaba con tener un rival dentro
            // del radio para dejar de ser candidato, así que el pase era binario —libre o inexistente— y
            // los atributos del que recibe no entraban en la decisión por ningún sitio. Ahora la presión
            // sobre él RESTA al compararlo con los demás, igual que ya hacía el pasillo tapado desde el
            // paso 3 de la ADR 0091. El descarte duro se queda sólo donde describe una situación
            // imposible, no una mala.
            int matePressure = ctx.Pressure[mate.Index];

            if (longPass && SegmentBlocked(players, p.Team, p.Position, mate.Position, context.PassLaneRadiusCells))
            {
                continue;
            }

            if (clearShot && (mate.Position.X - p.Position.X) * direction < 0f)
            {
                continue;
            }

            int advance = Centi((mate.Position.X - p.Position.X) * direction);
            int rank = p.IsOutfield ? advance - (Centi(distance) * 20 / 100) : advance;

            // AZ-B paso 3: el pasillo PUNTÚA, nunca descarta. Un pasillo tapado del todo vale como
            // retroceder passBlockedLaneRankPenalty centésimas de casilla al comparar receptores.
            int danger = LaneDanger(players, p.Team, p.Position, mate.Position, context.PassLaneRadiusCells);
            rank -= context.PassBlockedLaneRankPenalty * danger / 100;
            rank -= context.PassReceiverPressureRankPenalty * matePressure / 100;
            if (receiver is null || rank > bestRank)
            {
                receiver = mate;
                bestRank = rank;
                bestAdvance = advance;
                bestDanger = danger;
                bestPressure = matePressure;
                bestHold = HoldUnderPressure(mate);
            }
        }

        int score;
        if (receiver is null)
        {
            score = -context.PassNoReceiverPenalty;
        }
        else
        {
            // AZ-B paso 3: un pasillo tapado casi anula el bono del receptor abierto (200 sobre 220) pero
            // nunca empuja la acción al acantilado de passNoReceiverPenalty.
            score = context.PassOpenReceiverBonus - (context.PassBlockedLanePenalty * bestDanger / 100);

            // AW-D (docs/pendientes.md, cambio 1 de 2): el bonus de arriba se cobraba entero con
            // cualquier receptor legal, sin mirar si quedaba delante o detrás del pasador. La primera
            // versión penalizaba TODO pase hacia atrás y descompensó la circulación normal (lote de
            // balance: possessionChanges, passChainAvgLength, shotsPerMatch y tacklesPerMatch se salieron
            // de rango) porque la mayoría de los pases atrás son circulación sana, no el caso que describe
            // la anotación. Acotado: solo paga quien además tiene el mismo carril libre que
            // EvaluateDribble consultaría para regatear — ahí sí había alternativa real y prefirió el
            // pase atrás sin necesidad. Un pase atrás por estar acorralado no paga nada.
            if (bestAdvance < 0 && OpponentsAheadCount(players, p, direction) == 0)
            {
                score += bestAdvance * context.PassBackwardPenaltyPerCell / 100;
            }

            // ADR 0141: meterle el balón a alguien presionado cuesta, y cuesta MENOS si ese alguien
            // aguanta. Con el valor publicado, un receptor totalmente presionado y del montón anula el
            // bono de «receptor abierto»: el pase sigue siendo posible —a veces es el único— pero deja de
            // ser el pase cómodo que era antes por no mirar a quién se la das.
            int holdRelief = Slope(context.PassReceiverHoldSlope, bestHold);
            int pressureCost = context.PassReceiverPressurePenalty - holdRelief;
            if (pressureCost > 0)
            {
                score -= pressureCost * bestPressure / 100;
            }
        }

        score += Slope(longPass ? context.LongPassTechniqueSlope : context.ShortPassTechniqueSlope, p.Technique);

        // AZ-C: el portero rival no cuenta como presión — no se lanza a presionar como un jugador de
        // campo, y contarlo daba PassUnderPressureBonus solo por tener al portero cerca del área.
        if (HasOpponentWithin(players, p, PitchConstants.PressureRadius, excludeGoalkeeper: true))
        {
            score += context.PassUnderPressureBonus;
        }

        eval.Receiver = receiver;
        eval.Context = score;
    }


    /// <summary>
    /// Lo que un jugador aporta a <b>aguantar un balón que le llega con un rival encima</b> (ADR 0141):
    /// la media de su técnica y su fuerza.
    ///
    /// <para>Las dos, y no una: recibir presionado es controlarla <i>y</i> que no te la quiten. Un técnico
    /// frágil y un armario torpe resuelven la misma jugada por caminos distintos, y el motor no tiene
    /// motivo para preferir a ninguno de los dos.</para>
    /// </summary>
    private static int HoldUnderPressure(MatchPlayer player) => (player.Technique + player.Strength) / 2;

    /// <summary>
    /// Término de utilidad que aporta un atributo a una acción (ADR 0030 §1): <c>pendiente × (atributo −
    /// 50)</c>, con signo. El pivote es el 50 de un jugador medio, así que la pendiente no infla la
    /// puntuación global: reparte. Todo entero (RT-023).
    /// </summary>
    private static int Slope(int slope, int attribute) => slope * (attribute - AttributePivot);

    /// <summary>
    /// True si algún rival vive a menos de <paramref name="radius"/> de <paramref name="target"/>.
    /// <paramref name="excludeGoalkeeper"/> (AZ-C) saca al portero rival de la cuenta para los sitios donde
    /// no debe contar como presión: por defecto entra, igual que antes.
    /// </summary>
    private static bool HasOpponentWithin(
        MatchPlayer[] players, MatchPlayer target, float radius, bool excludeGoalkeeper = false)
    {
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == target.Team || !other.OnPitch || (excludeGoalkeeper && !other.IsOutfield))
            {
                continue;
            }

            if (Vec2.Distance(other.Position, target.Position) < radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Línea de tiro despejada del portador (AZ-C, docs/plan-segunda-partida.md): dentro de alcance de tiro
    /// (mismo cálculo que <see cref="EvaluateShoot"/>) y sin ningún rival de campo a menos de
    /// <paramref name="blockRadiusCells"/> del segmento portador→portería — el mismo radio con el que
    /// <c>MatchEngine.TryBlockShot</c> bloquea el tiro en vuelo. El portero rival se excluye a propósito:
    /// tiene su propio mecanismo de parada (<c>TryGoalkeeperReach</c>) y no cuenta como bloqueo de línea.
    /// </summary>
    private static bool HasClearShot(MatchPlayer[] players, MatchPlayer p, AiContext context, float blockRadiusCells)
    {
        if (!p.IsOutfield)
        {
            return false;
        }

        Vec2 goal = Pitch.GoalCenter(p.Team);
        float rangeCells = context.ShootBaseRangeCells + p.ShootRangeBonusCells;
        if (Vec2.Distance(p.Position, goal) > rangeCells)
        {
            return false;
        }

        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == p.Team || !other.OnPitch || !other.IsOutfield)
            {
                continue;
            }

            if (DistanceToSegment(p.Position, goal, other.Position) < blockRadiusCells)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Distancia al rival más cercano a un punto; el ancho del campo si no queda ninguno.</summary>
    private static float NearestOpponentDistance(MatchPlayer[] players, int team, Vec2 point)
    {
        float best = Pitch.Columns;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == team || !other.OnPitch)
            {
                continue;
            }

            float distance = Vec2.Distance(other.Position, point);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Compañeros de <paramref name="p"/> (sin contar a <paramref name="p"/>) a menos de
    /// <paramref name="radius"/> casillas de <paramref name="point"/>. AW-E (docs/pendientes.md, cambio
    /// 2 de 2): mismo cálculo que <see cref="EvaluateSupport"/> hace en línea para su propio punto de
    /// apoyo, ahora también reutilizado por <see cref="EvaluateFindSpace"/> sobre cada casilla candidata.
    /// </summary>
    private static int TeammatesNear(MatchPlayer[] players, MatchPlayer p, Vec2 point, float radius)
    {
        int count = 0;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team != p.Team || ReferenceEquals(other, p) || !other.OnPitch)
            {
                continue;
            }

            if (Vec2.Distance(other.Position, point) < radius)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>True si algún rival está a menos de <paramref name="laneRadiusCells"/> del segmento from-&gt;to.</summary>
    private static bool SegmentBlocked(MatchPlayer[] players, int team, Vec2 from, Vec2 to, float laneRadiusCells)
    {
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == team || !other.OnPitch)
            {
                continue;
            }

            if (DistanceToSegment(from, to, other.Position) < laneRadiusCells)
            {
                return true;
            }
        }

        return false;
    }

    private static void EvaluateDribble(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        eval.Target = ClampToPitch(new Vec2(p.Position.X + direction, MoveToward(p.Position.Y, PitchConstants.CenterRow, 1f)));

        int ahead = OpponentsAheadCount(ctx.Players, p, direction);

        eval.Context = (ahead == 0
                ? context.DribbleOpenSpaceBonus
                : -(context.DribbleOpponentAheadPenalty * ahead))
            + Slope(context.DribbleTechniqueSlope, p.Technique)
            + Slope(context.DribbleSpeedSlope, p.Speed);
    }

    /// <summary>
    /// Rivales de campo dentro de <see cref="DribbleAheadRadius"/> por delante de <paramref name="p"/>
    /// (mismo criterio que <see cref="EvaluateDribble"/> usa para decidir si hay hueco para regatear). AW-D
    /// (docs/pendientes.md, cambio 1 de 2, acotado): también lo consulta <see cref="EvaluatePass"/>, para
    /// no penalizar un pase hacia atrás cuando el pasador no tiene ninguna alternativa de regate real —
    /// un pase de circulación normal, no el caso que la anotación describe. AZ-C: el portero rival no
    /// cuenta como rival "por delante" que cierre el regate o el pase; no sale de su portería a taparlo.
    /// </summary>
    private static int OpponentsAheadCount(MatchPlayer[] players, MatchPlayer p, int direction)
    {
        int ahead = 0;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == p.Team || !other.OnPitch || !other.IsOutfield)
            {
                continue;
            }

            if ((other.Position.X - p.Position.X) * direction > 0f
                && Vec2.Distance(other.Position, p.Position) < DribbleAheadRadius)
            {
                ahead++;
            }
        }

        return ahead;
    }

    /// <summary>Ticks que tarda alguien en recorrer <paramref name="distanceCells"/> a <paramref name="speedPerTickMilli"/> (entero, redondeo hacia arriba).</summary>
    internal static int TicksToReach(float distanceCells, int speedPerTickMilli)
    {
        int distanceMilli = (int)MathF.Ceiling(distanceCells * 1000f);
        int speed = speedPerTickMilli > 0 ? speedPerTickMilli : 1;
        return (distanceMilli + speed - 1) / speed;
    }

    /// <summary>Ticks de vuelo del balón para <paramref name="distance"/> casillas a <paramref name="speedMilli"/> (el mismo cálculo que usa el motor).</summary>
    internal static int FlightTicks(float distance, int speedMilli)
    {
        int distanceMilli = (int)(distance * 1000f);
        int speed = speedMilli > 0 ? speedMilli : 1;
        int ticks = (distanceMilli + speed - 1) / speed;
        return ticks < 1 ? 1 : ticks;
    }

    /// <summary>
    /// Legalidad del pase en profundidad (AZ-B paso 5): una carrera en ticks, adimensional. El receptor
    /// puede llegar hasta <paramref name="lateTicks"/> después que el balón (que se para donde cae) y tiene
    /// que ganarle al defensa más cercano por <paramref name="marginTicks"/>. Compara tiempos, no radios:
    /// es lo que le faltaba al pasillo por tiempo descartado en plan-intercepcion-disparo.md.
    /// </summary>
    internal static bool ThroughPassIsLegal(int ticksBall, int ticksReceiver, int ticksDefender, int lateTicks, int marginTicks) =>
        ticksReceiver <= ticksBall + lateTicks && ticksReceiver + marginTicks <= ticksDefender;

    /// <summary>
    /// Pase en profundidad (AZ-B paso 5, ADR 0091): a una casilla vacía <c>throughPassMinCells..MaxCells</c>
    /// por delante de un compañero que <b>va hacia delante</b>, recortada por la línea defensiva rival como en
    /// FindSpace salvo en la zona libre (<c>throughPassFreeZoneCells</c> de la portería rival: el pase a la
    /// espalda de la defensa es lo que la acción es), y legal solo si el receptor llega antes que cualquier
    /// rival (carrera en ticks). El pasillo puntúa igual que en el paso 3. Sin candidato legal la acción se
    /// descarta: es una acción extra, no sustituye al pase.
    /// </summary>
    private static void EvaluateThroughPass(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        var players = ctx.Players;
        if (!p.IsOutfield)
        {
            eval.Discarded = true;
            return;
        }

        float marginedLine = OffsideLineColumn(players, ctx.Ball.Position, p.Team)
            + (context.FindSpaceLineMarginCells * direction);
        float rivalGoalColumn = direction > 0 ? Pitch.Columns : 0f;
        MatchPlayer? runner = null;
        Vec2 bestCell = p.Position;
        int bestScore = 0;

        for (int i = 0; i < players.Length; i++)
        {
            var mate = players[i];
            if (mate.Team != p.Team || ReferenceEquals(mate, p) || !mate.OnPitch || !mate.IsOutfield)
            {
                continue;
            }

            var run = mate.TargetPoint - mate.Position;
            float runLength = run.Length;
            if (runLength <= 0.001f || run.X * direction <= 0f)
            {
                continue;
            }

            var unit = run * (1f / runLength);
            for (int cells = context.ThroughPassMinCells; cells <= context.ThroughPassMaxCells; cells++)
            {
                Vec2 cell = ClampToPitch(mate.Position + (unit * cells));
                bool inFreeZone = MathF.Abs(rivalGoalColumn - cell.X) <= context.ThroughPassFreeZoneCells;
                if (!inFreeZone && (cell.X - marginedLine) * direction > 0f)
                {
                    cell = new Vec2(marginedLine, cell.Y);
                }

                if ((cell.X - p.Position.X) * direction <= 0f)
                {
                    continue;
                }

                int ticksBall = FlightTicks(Vec2.Distance(p.Position, cell), ctx.PassSpeedCellsPerTickMilli);
                int ticksReceiver = TicksToReach(Vec2.Distance(mate.Position, cell), mate.SpeedPerTickMilli);
                int ticksDefender = int.MaxValue;
                for (int j = 0; j < players.Length; j++)
                {
                    var rival = players[j];
                    if (rival.Team == p.Team || !rival.OnPitch)
                    {
                        continue;
                    }

                    int t = TicksToReach(Vec2.Distance(rival.Position, cell), rival.SpeedPerTickMilli);
                    if (t < ticksDefender)
                    {
                        ticksDefender = t;
                    }
                }

                if (!ThroughPassIsLegal(ticksBall, ticksReceiver, ticksDefender, context.ThroughPassLateTicks, context.ThroughPassMarginTicks))
                {
                    continue;
                }

                int advance = Centi((cell.X - p.Position.X) * direction);
                int score = context.ThroughPassBase
                    + (context.FindSpaceAdvanceBonusPerCell * advance / 100)
                    - (context.PassBlockedLanePenalty * LaneDanger(players, p.Team, p.Position, cell, context.PassLaneRadiusCells) / 100);
                if (runner is null || score > bestScore || (score == bestScore && mate.Id < runner.Id))
                {
                    runner = mate;
                    bestCell = cell;
                    bestScore = score;
                }
            }
        }

        if (runner is null)
        {
            eval.Discarded = true;
            return;
        }

        eval.Receiver = runner;
        eval.Target = bestCell;
        eval.Context = bestScore + Slope(context.ThroughPassTechniqueSlope, p.Technique);
    }

    /// <summary>
    /// <b>Apertura</b> de un punto a la portería que ataca <paramref name="goal"/>, en centésimas (0..100):
    /// el coseno del ángulo entre la línea a portería y la perpendicular a la línea de gol, que es
    /// <c>|dx| / distancia</c>. Vale <b>100 de frente</b> a la portería y <b>0 desde la propia línea de
    /// fondo</b>, que es exactamente «sin ángulo».
    ///
    /// <para>Es la misma magnitud con la que se midió BA-E (<c>docs/ba-e-goles-sin-angulo.md</c> §1), y se
    /// usa aquí a propósito: la cifra que decide si el centro funcionó es la misma que documentó el
    /// problema. Entero, como todo lo que entra en la utilidad (RT-023).</para>
    /// </summary>
    internal static int ApertureCenti(Vec2 point, Vec2 goal)
    {
        float distance = Vec2.Distance(point, goal);
        if (distance <= 0.001f)
        {
            // Sobre la línea de gol misma: de frente por convenio, y no hay división por cero.
            return 100;
        }

        int aperture = Centi(MathF.Abs(goal.X - point.X) / distance);
        return aperture < 0 ? 0 : (aperture > 100 ? 100 : aperture);
    }

    /// <summary>
    /// Centrar (ADR 0136): un pase <b>alto</b> a un compañero de la zona de remate que tiene <b>mejor
    /// apertura a portería</b> que el propio pasador, y que al llegar el balón lo remata sin controlarlo
    /// (<c>MatchEngine.LaunchVolley</c>).
    ///
    /// <para><b>Dos precondiciones duras y ninguna más.</b> El compañero tiene que estar al alcance del
    /// centro (<c>crossMaxCells</c>) y dentro de la zona de remate
    /// (<c>crossTargetGoalDistanceCells</c> de la portería rival), y su apertura tiene que ser
    /// <b>estrictamente mejor</b> que la del pasador: un centro a alguien peor colocado no es un centro,
    /// es un mal pase. Sin candidato la acción se descarta, igual que hace
    /// <see cref="EvaluateThroughPass"/> sin corredor — no paga una penalización como el pase normal,
    /// porque un centro sin rematador no es una jugada peor: no es una jugada.</para>
    ///
    /// <para><b>Lo que hace la acción local</b>, que es lo que la ADR 0111 exigía después de rechazar la
    /// corrección global de <c>FindSpace</c>, es el término de apertura ganada: paga por centésima de
    /// mejora, así que un centro desde el cordel a alguien de frente cobra casi todo y un centro entre dos
    /// posiciones parecidas no cobra nada. La acción sólo existe donde el problema existe.</para>
    ///
    /// <para><b>Por qué el pasillo pesa menos aquí.</b> <c>crossBlockedLanePenalty</c> es menor que el del
    /// pase raso a propósito: el balón pasa por <b>encima</b> del pasillo a mitad de vuelo, y quien decide
    /// de verdad es la física (<c>MatchEngine.TryIntercept</c> mide en esfera). El término sigue existiendo
    /// porque en los extremos del vuelo el balón va bajo y un rival pegado al centrador sí lo corta.</para>
    /// </summary>
    private static void EvaluateCross(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        if (!p.IsOutfield)
        {
            eval.Discarded = true;
            return;
        }

        var players = ctx.Players;
        Vec2 goal = Pitch.GoalCenter(p.Team);
        int ownAperture = ApertureCenti(p.Position, goal);

        MatchPlayer? receiver = null;
        int bestRank = 0;
        int bestGain = 0;
        int bestDanger = 0;
        int bestMarked = 0;

        for (int i = 0; i < players.Length; i++)
        {
            var mate = players[i];
            if (mate.Team != p.Team || ReferenceEquals(mate, p) || !mate.OnPitch || !mate.IsOutfield)
            {
                continue;
            }

            // Un centro es un balon LARGO al area: por debajo de CrossMinCells no es un centro, es un
            // pase corto con comba, y permitirlo haria que la accion se colara dentro del area rival.
            float distance = Vec2.Distance(p.Position, mate.Position);
            if (distance < context.CrossMinCells || distance > context.CrossMaxCells)
            {
                continue;
            }

            if (Vec2.Distance(mate.Position, goal) > context.CrossTargetGoalDistanceCells)
            {
                continue;
            }

            int gain = ApertureCenti(mate.Position, goal) - ownAperture;
            if (gain <= 0)
            {
                continue;
            }

            // Que el rematador esté marcado PUNTÚA, no descarta. El primer intento copió del pase raso la
            // precondición «receptor sin rivales cerca» y dejaba el centro en medio centro por partido: es
            // importar la regla del pase al suelo a la acción cuyo propósito es precisamente no jugar por
            // el suelo. Un centro es el balón que pones CUANDO el área está poblada, y el que remata lo
            // hace en disputa —por eso el remate ya cuesta calidad y puntería—. Además es la convención
            // del repositorio desde el paso 3 de la ADR 0091: el pasillo puntúa, nunca descarta.
            int marked = HasOpponentWithin(players, mate, PitchConstants.PressureRadius) ? 1 : 0;

            int danger = LaneDanger(players, p.Team, p.Position, mate.Position, context.PassLaneRadiusCells);
            int rank = (context.CrossApertureGainPerCenti * gain)
                - (context.CrossBlockedLanePenalty * danger / 100)
                - (context.CrossMarkedTargetPenalty * marked);

            // Empate por id de jugador ascendente (RT-041, RT-097).
            if (receiver is null || rank > bestRank || (rank == bestRank && mate.Id < receiver.Id))
            {
                receiver = mate;
                bestRank = rank;
                bestGain = gain;
                bestDanger = danger;
                bestMarked = marked;
            }
        }

        if (receiver is null)
        {
            eval.Discarded = true;
            return;
        }

        eval.Receiver = receiver;
        eval.Context = context.CrossBase
            + (context.CrossApertureGainPerCenti * bestGain)
            - (context.CrossBlockedLanePenalty * bestDanger / 100)
            - (context.CrossMarkedTargetPenalty * bestMarked)
            + Slope(context.CrossTechniqueSlope, p.Technique);
    }

    /// <summary>
    /// Tiro (ADR 0030 §1). El corte binario dentro/fuera de alcance desaparece: nadie tiene prohibido
    /// tirar de lejos, simplemente casi nadie debería querer. Pasado el alcance del jugador, cada casilla
    /// de más resta <c>shootBeyondRangePenaltyPerCell</c>, un múltiplo grande de la penalización normal
    /// por distancia, así que la utilidad cae en rampa en vez de en escalón.
    ///
    /// <para>Quien modula esa rampa es <c>LongShot</c>, y lo hace <b>moviendo dónde empieza</b>: el rasgo
    /// aporta sus casillas de alcance desde <c>data/traits/traits.json</c> (RT-094), así que el tirador
    /// lejano paga la rampa dos casillas más tarde que el resto. No hay ningún <c>if</c> por rasgo aquí.</para>
    /// </summary>
    private static void EvaluateShoot(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        Vec2 goal = Pitch.GoalCenter(p.Team);
        float distance = Vec2.Distance(p.Position, goal);
        int distanceCenti = Centi(distance);
        int rangeCenti = (context.ShootBaseRangeCells + p.ShootRangeBonusCells) * 100;

        int angle = Centi(MathF.Abs(p.Position.Y - PitchConstants.CenterRow));
        int score = context.ShootInRangeBonus
            - (context.ShootDistancePenaltyPerCell * distanceCenti / 100)
            - (context.ShootAnglePenaltyPerRow * angle / 100)
            + Slope(context.ShootTechniqueSlope, p.Technique)
            + Slope(context.ShootStrengthSlope, p.Strength);

        if (distanceCenti > rangeCenti)
        {
            score -= context.ShootBeyondRangePenaltyPerCell * (distanceCenti - rangeCenti) / 100;
        }

        // AZ-B paso 4: la decisión de tirar sabe si hay un cuerpo en la línea (la física ya lo bloqueaba
        // desde el paso 2). Es un término, no un veto: un delantero con buena técnica puede seguir tirando.
        score -= context.ShootBlockedLanePenalty * LaneDanger(ctx.Players, p.Team, p.Position, Pitch.GoalCenter(p.Team), context.PassLaneRadiusCells, ignoreGoalkeeper: true) / 100;

        eval.Context = score;
    }

    /// <summary>
    /// Entrada (RF-054). Dos objetivos legales, en este orden de preferencia:
    ///
    /// <list type="number">
    /// <item><b>El poseedor rival</b>, si está dentro de <c>tackleDistanceMaxCells</c>. Vale
    /// <c>tackleBallCarrierBonus</c> y es siempre la opción mejor puntuada de las dos.</item>
    /// <item><b>El marcado</b> (ADR 0105), si no hay poseedor rival al alcance. Vale el
    /// <c>tackleMarkTargetBonus</c> <b>de su puesto</b> (ADR 0125 D2), un ajuste con signo y siempre
    /// estrictamente menor que el anterior: quitar el balón tiene que seguir siendo mejor que pegarle a
    /// quien no lo lleva.</item>
    /// </list>
    ///
    /// <para>La entrada sin balón lleva tres condiciones, y las tres son de diseño, no de implementación:
    /// la decide el <b>ajuste por puesto</b> del dato (<see cref="AiWeights.OffBallTackleAdjust"/>), que
    /// en 0 significa que ese puesto no entra nunca —desde la ADR 0125 D2/D3 pueden los tres roles de
    /// campo, con el defensa por delante, y ya no hay guarda por rol en el código—; el objetivo es el
    /// <b>marcado asignado</b> por
    /// <see cref="Marking"/> y nadie más, que es lo que hace el suceso predecible antes del partido y
    /// cierra AY-A; y el marcado tiene que estar <b>en la jugada activa</b> de RF-057
    /// (<see cref="IsInActivePlay"/>), el mismo criterio que ya acota el bloqueo sin balón: sin él un
    /// defensa podría ir a partir a su par al otro lado del campo, que es contacto fuera de la jugada y
    /// el requisito no lo permite.</para>
    /// </summary>
    private static void EvaluateTackle(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        // ADR 0129: cada entrada comprueba SU propio enfriamiento. Haber pegado a quien no llevaba el
        // balón no puede impedir disputarlo.
        var carrier = ctx.Ball.Owner;
        if (carrier is not null && carrier.Team != p.Team
            && Vec2.Distance(p.Position, carrier.Position) <= context.TackleDistanceMaxCells)
        {
            // Con un poseedor rival al alcance la entrada es SIEMPRE a él (ADR 0105): si está en
            // enfriamiento, no se sustituye por pegarle a otro.
            if (p.TackleCooldown > 0)
            {
                eval.Discarded = true;
                return;
            }

            eval.TackleTarget = carrier;
            eval.Target = carrier.Position;
            eval.Context = context.TackleBallCarrierBonus;
            return;
        }

        if (p.OffBallTackleCooldown > 0)
        {
            eval.Discarded = true;
            return;
        }

        // ADR 0105: no hay poseedor rival al alcance. Queda el objetivo sin balón, y C7 (docs/analisis/
        // perks-catalogo-unificado.md §3.2) deja que un perk sesgue CUÁL: el marcado sigue siendo el
        // criterio por defecto, pero "Olfato de sangre" y "Rabia" lo sustituyen cuando aplican. No cambia
        // la prioridad del poseedor rival de arriba (ADR 0105 sigue mandando: quitar el balón es siempre
        // mejor que pegarle a quien no lo lleva), solo QUIÉN es el objetivo sin balón.
        // ADR 0125 D2/D3: quién DECIDE entrar sin balón lo dice el dato, no una guarda por rol. El ajuste
        // por puesto en 0 es la forma explícita de decir "este puesto no entra nunca" — y hay que
        // comprobarlo aquí, porque un 0 no desactiva nada por sí solo: medido, con el término plano en 0
        // seguían ocurriendo 0,98 entradas sin balón por partido. El portero queda fuera por IsOutfield:
        // no tiene marca asignada.
        // El "nunca" alcanza a esta decisión, no al motor entero: MatchEngine.RepeatTackle ejecuta el
        // efecto extraAction sin pasar por aquí y sin mirar el mapa (agujero conocido, sin evidencia de
        // activación en /data; docs/pendientes/BE-A.md).
        int offBallAdjust = ctx.Weights.OffBallTackleAdjust(p.Role);
        if (p.IsOutfield && offBallAdjust != 0)
        {
            // El objetivo sesgado ya viene comprobado del todo (alcance, jugada activa y
            // CanReceiveOffBallTackle, que a diferencia de CanBeBlocked SÍ admite un rival derribado): no
            // se vuelve a pasar por CanBeBlocked, que lo rechazaría precisamente por estar en el suelo.
            if (BiasedOffBallTackleTarget(ctx, context, p) is { } biased)
            {
                eval.TackleTarget = biased;
                eval.TackleOffBall = true;
                eval.Target = biased.Position;
                eval.Context = offBallAdjust;
                return;
            }

            var mark = p.MarkTarget;
            if (Marking.IsValidTarget(mark, p.Team)
                && CanBeBlocked(mark!)
                && Vec2.Distance(p.Position, mark!.Position) <= context.TackleDistanceMaxCells
                && IsInActivePlay(ctx, context, mark.Position))
            {
                eval.TackleTarget = mark;
                eval.TackleOffBall = true;
                eval.Target = mark.Position;
                eval.Context = offBallAdjust;
                return;
            }
        }

        eval.Context = -context.TackleOutOfReachPenalty;
    }

    /// <summary>
    /// C7 (docs/analisis/perks-catalogo-unificado.md §3.2): el criterio de a quién entrar sin balón,
    /// cuando un perk lo sesga. Dos variantes cerradas, comprobadas en este orden: el rival que le hizo la
    /// última falta (<see cref="MatchPlayer.TackleNemesis"/>, "Rabia") y, si no aplica, el rival ya
    /// derribado (<see cref="MatchPlayer.PreferKnockedDownTackleTarget"/>, "Olfato de sangre"). Sin
    /// ninguna de las dos marcadas -el 99% del catálogo- devuelve null y el criterio sigue siendo el fijo
    /// de siempre (el marcado). Usa <see cref="CanReceiveOffBallTackle"/> y no
    /// <see cref="CanBeBlocked"/>: un rival ya derribado SÍ puede recibir una entrada -es justo el que
    /// "olfato de sangre" busca-, mientras que <c>CanBeBlocked</c> lo excluye a propósito para la carga
    /// sin balón (no tiene sentido cargar contra alguien que ya está en el suelo).
    /// </summary>
    private static MatchPlayer? BiasedOffBallTackleTarget(UtilityContext ctx, AiContext context, MatchPlayer p)
    {
        if (p.TackleNemesis is { } nemesis
            && CanReceiveOffBallTackle(nemesis)
            && nemesis.Team != p.Team
            && Vec2.Distance(p.Position, nemesis.Position) <= context.TackleDistanceMaxCells
            && IsInActivePlay(ctx, context, nemesis.Position))
        {
            return nemesis;
        }

        if (p.PreferKnockedDownTackleTarget)
        {
            var players = ctx.Players;
            for (int i = 0; i < players.Length; i++)
            {
                var candidate = players[i];
                if (candidate.Team != p.Team
                    && candidate.State == PlayerState.KnockedDown
                    && CanReceiveOffBallTackle(candidate)
                    && Vec2.Distance(p.Position, candidate.Position) <= context.TackleDistanceMaxCells
                    && IsInActivePlay(ctx, context, candidate.Position))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Un rival al que tiene sentido entrar SIN balón (C7): en el campo, no expulsado y no celebrando. A
    /// diferencia de <see cref="CanBeBlocked"/> (la carga de <c>EvaluateBlock</c>) SÍ admite
    /// <see cref="PlayerState.KnockedDown"/>: "Olfato de sangre" busca exactamente al que ya está en el
    /// suelo.
    /// </summary>
    private static bool CanReceiveOffBallTackle(MatchPlayer player) =>
        player.OnPitch && player.State is not (PlayerState.SentOff or PlayerState.Celebrating);

    /// <summary>
    /// Bloqueo sin balón (ADR 0030 §2): cargar contra un rival que <b>no</b> lleva el balón para quitarlo
    /// de en medio. Con los cuerpos de la ADR 0020, derribarlo abre hueco de verdad.
    ///
    /// <para>El límite que impone RF-057 —"solo hay contacto entre jugadores que disputan el balón o que
    /// se encuentran en la trayectoria de la jugada activa"— se concreta en
    /// <see cref="IsInActivePlay"/>: no se puede ir a partir a un rival al otro lado del campo. Sobre eso
    /// van dos condiciones más: el rival tiene que estar al alcance de la carga
    /// (<c>blockReachMaxCells</c>) y tiene que estar en pie, porque derribar al que ya está en el suelo
    /// no abre ningún hueco. Si no queda ningún objetivo legal, la acción se <b>descarta</b> en vez de
    /// puntuar en negativo: en el volcado de utilidad (RT-098) "no había a quién" y "no valía la pena"
    /// son dos respuestas distintas y conviene poder distinguirlas.</para>
    /// </summary>
    private static void EvaluateBlock(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        if (!p.IsOutfield || p.BlockCooldown > 0)
        {
            eval.Discarded = true;
            return;
        }

        var players = ctx.Players;
        var carrier = ctx.Ball.Owner;
        MatchPlayer? target = null;
        float bestDistance = 0f;

        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == p.Team || ReferenceEquals(other, carrier) || !CanBeBlocked(other))
            {
                continue;
            }

            float distance = Vec2.Distance(p.Position, other.Position);
            if (distance > context.BlockReachMaxCells || !IsInActivePlay(ctx, context, other.Position))
            {
                continue;
            }

            if (target is null || distance < bestDistance)
            {
                target = other;
                bestDistance = distance;
            }
        }

        if (target is null)
        {
            eval.Discarded = true;
            return;
        }

        eval.BlockTarget = target;
        eval.Target = target.Position;

        int score = context.BlockTargetBonus
            - (context.BlockDistancePenaltyPerCell * Centi(bestDistance) / 100);
        if (p.HasTrait(Trait.Aggressive))
        {
            score += context.BlockAggressiveBonus;
        }

        if (p.Definition.HasTag(BruteTag))
        {
            score += context.BlockBruteTagBonus;
        }

        eval.Context = score;
    }

    /// <summary>Un rival al que tiene sentido cargar: en el campo y en pie.</summary>
    private static bool CanBeBlocked(MatchPlayer player) =>
        player.OnPitch
        && player.State is not (PlayerState.KnockedDown or PlayerState.Injured
            or PlayerState.SentOff or PlayerState.Celebrating);

    /// <summary>
    /// Criterio operativo de <b>jugada activa</b> de RF-057, matizado por la ADR 0030 §2. Un punto está en
    /// la jugada si cumple una de estas dos, que es la lectura literal del requisito:
    /// <list type="number">
    /// <item>está a menos de <c>blockActiveRadiusCells</c> del balón —"disputa el balón"—;</item>
    /// <item>o está a menos de <c>blockCorridorHalfWidthCells</c> del <b>segmento</b> que une el balón con
    /// la portería que ataca el equipo que lo tiene —"está en la trayectoria de la jugada"—.</item>
    /// </list>
    /// Con el balón suelto o en vuelo sin dueño no hay equipo atacante ni, por tanto, corredor: queda solo
    /// el radio, que es la lectura conservadora. El corredor es un segmento y no una recta infinita a
    /// propósito: por detrás del balón no hay jugada que proteger.
    /// </summary>
    private static bool IsInActivePlay(UtilityContext ctx, AiContext context, Vec2 point)
    {
        // ADR 0132: con el balón MUERTO no hay jugada activa, así que no hay contacto legítimo con nadie
        // (RF-057: solo entre quienes disputan el balón o están en la trayectoria de la jugada). Sin esto,
        // durante la cuenta atrás de una reanudación el balón sigue en su sitio y la geometría de abajo
        // declaraba "en jugada" a todo el que estuviera cerca: el que iba a sacar recibía entradas y
        // cargas mientras esperaba, y el empuje acumulado lo desplazaba del punto de saque.
        // Acota las DOS acciones que usan este criterio —la entrada sin balón (ADR 0105) y el bloqueo
        // sin balón (ADR 0030 §2)—, que es donde estaba la causa común.
        if (ctx.BallDead)
        {
            return false;
        }

        Vec2 ball = ctx.Ball.Position;
        if (Vec2.Distance(ball, point) <= context.BlockActiveRadiusCells)
        {
            return true;
        }

        int attacking = ctx.HoldingTeam;
        if (attacking < 0)
        {
            return false;
        }

        return DistanceToSegment(ball, Pitch.GoalCenter(attacking), point) <= context.BlockCorridorHalfWidthCells;
    }

    /// <summary>Distancia de un punto al segmento from-&gt;to (0 si el segmento es un punto).</summary>
    private static float DistanceToSegment(Vec2 from, Vec2 to, Vec2 point)
    {
        Vec2 segment = to - from;
        float lengthSquared = (segment.X * segment.X) + (segment.Y * segment.Y);
        Vec2 offset = point - from;
        float t = lengthSquared <= 0f
            ? 0f
            : Math.Clamp(((offset.X * segment.X) + (offset.Y * segment.Y)) / lengthSquared, 0f, 1f);
        return Vec2.Distance(point, from + (segment * t));
    }

    /// <summary>
    /// Replegar (§2.2): además del bono por distancia a la casilla-hogar, gana peso cuanto más fuera de
    /// su zona está el jugador <b>ahora mismo</b>. Es la otra mitad de la correa blanda: salir cuesta, y
    /// volver paga. La distancia se mide sobre la posición actual, no sobre el objetivo, porque el
    /// objetivo de replegar es siempre la casilla-hogar y por definición está dentro de la zona.
    /// </summary>
    private static void EvaluateRetreat(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        eval.Target = p.EffectiveHome;
        float distance = Vec2.Distance(p.Position, p.EffectiveHome);
        eval.Context = context.RetreatDistanceBonusPerCell * Centi(distance) / 100;
        if (distance < 0.5f)
        {
            eval.Context -= context.RetreatAtHomePenalty;
        }

        float outsideNow = DistanceOutsideZone(p, p.Position);
        if (outsideNow > 0f)
        {
            eval.Context += ctx.Zone.RetreatBonusOutsidePerCell * Centi(outsideNow) / 100;
        }
    }
}
