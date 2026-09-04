using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Vista del mundo que necesita la IA de utilidad (§3.5). El motor la rellena una vez por tick y la
/// reutiliza en todas las decisiones de ese tick: no se asigna nada por evaluación (RT-051).
/// </summary>
internal sealed class UtilityContext
{
    public UtilityContext(MatchPlayer[] players, Ball ball, AiWeights weights, ActionZoneTuning zone)
    {
        Players = players;
        Ball = ball;
        Weights = weights;
        Zone = zone;
    }

    /// <summary>Todos los jugadores del partido, ordenados por id ascendente (RT-041, RT-097).</summary>
    public MatchPlayer[] Players { get; }

    /// <summary>Balón del partido.</summary>
    public Ball Ball { get; }

    /// <summary>Pesos de IA cargados de data/ai/weights.json (RT-096).</summary>
    public AiWeights Weights { get; }

    /// <summary>Ajustes de la zona de acción, de data/sim/tuning.json (ADR 0028, §2.2).</summary>
    public ActionZoneTuning Zone { get; }

    /// <summary>Estado táctico por equipo (§3.4).</summary>
    public TacticalState[] TacticalStates { get; } = new TacticalState[2];

    /// <summary>Compañero más cercano al balón por equipo (empate por id); término chaseBallNotNearest.</summary>
    public MatchPlayer?[] NearestToBall { get; } = new MatchPlayer?[2];

    /// <summary>Equipo que sostiene el balón ahora mismo (dueño o vuelo); -1 si está suelto.</summary>
    public int HoldingTeam { get; set; } = -1;
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

    /// <summary>Distancia por delante en la que un rival estorba al regate (§3.5).</summary>
    private const float DribbleAheadRadius = 2.0f;

    /// <summary>Radio en el que un rival tapa la línea de pase entre el poseedor y un hueco (§2.3).</summary>
    private const float PassLaneRadius = 0.6f;

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
        int bestScore = 0;
        bool found = false;
        var best = PlayerAction.Retreat;
        Vec2 bestTarget = p.EffectiveHome;
        MatchPlayer? bestReceiver = null;
        MatchPlayer? bestTackleTarget = null;
        MatchPlayer? bestBlockTarget = null;

        for (int i = 0; i < legal.Count; i++)
        {
            var action = legal[i];
            var eval = Evaluate(ctx, p, action);
            int baseWeight = ctx.Weights.Base(p.Role, action);
            int tactical = ctx.Weights.Tactical(ctx.TacticalStates[p.Team], action);
            // El bono de Leader de los compañeros con casilla-hogar contigua entra en el multiplicador de
            // rasgos: la fórmula de §3.5 sigue siendo Base * Tactical / 100 * TraitMult / 100 + Context.
            int traitMultiplier = p.ActionMultiplier(action) * (100 + p.LeaderBonusPercent) / 100;
            int score = (baseWeight * tactical / 100 * traitMultiplier / 100) + eval.Context;

            bool rejected = eval.Discarded || eval.OutsideOuterLimit;
            rows?.Add(new UtilityRow(
                action, score, baseWeight, tactical, traitMultiplier, eval.Context,
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
                bestBlockTarget = eval.BlockTarget;
            }
        }

        if (!found)
        {
            // Todas descartadas (solo posible con datos degenerados): replegar es siempre alcanzable.
            best = PlayerAction.Retreat;
            bestTarget = p.EffectiveHome;
        }

        p.CurrentAction = best;
        p.TargetPoint = bestTarget;
        p.PassReceiver = bestReceiver;
        p.TackleTarget = bestTackleTarget;
        p.BlockTarget = bestBlockTarget;
        return best;
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
    public static Vec2 ClampToArea(Vec2 point, int team)
    {
        float minX = team == 0 ? 0f : Pitch.Columns - Pitch.AreaColumns + AreaMargin;
        float maxX = team == 0 ? Pitch.AreaColumns - AreaMargin : Pitch.Columns;
        float x = Math.Clamp(point.X, minX, maxX);
        float y = Math.Clamp(point.Y, 1f, Pitch.AreaRows + 1f);
        return new Vec2(x, y);
    }

    /// <summary>Acota un punto al rectángulo del campo.</summary>
    public static Vec2 ClampToPitch(Vec2 point) =>
        new(Math.Clamp(point.X, 0f, Pitch.Columns), Math.Clamp(point.Y, 0f, Pitch.Rows));

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
            case PlayerAction.Dribble:
                EvaluateDribble(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.Shoot:
                EvaluateShoot(p, context, ref eval);
                break;
            case PlayerAction.Block:
                EvaluateBlock(ctx, p, context, ref eval);
                break;
            case PlayerAction.Tackle:
                EvaluateTackle(p, ball, context, ref eval);
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
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        if (eval.Discarded || !IsMovementAction(action))
        {
            return eval;
        }

        Vec2 raw = eval.Target;
        Vec2 clamped = ClampToZone(p, raw);
        if (!p.IsOutfield)
        {
            clamped = ClampToArea(clamped, p.Team);
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

    private static bool IsMovementAction(PlayerAction action) =>
        action is PlayerAction.ChaseBall or PlayerAction.MarkOpponent or PlayerAction.OfferSupport
            or PlayerAction.CoverSpace or PlayerAction.Dribble or PlayerAction.Retreat
            or PlayerAction.FindSpace or PlayerAction.PressCarrier;

    private static void EvaluateChaseBall(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        var ball = ctx.Ball;
        Vec2 point = ball.InFlight ? ball.FlightTarget : ball.Position;
        bool loose = ball.Owner is null && !ball.InFlight;

        if (!p.IsOutfield && (!loose || !Pitch.IsInArea(point, p.Team)))
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

        // El receptor previsto de un pase en vuelo va a por el balón (§3.5). Sin este término gana
        // OfferSupport y el receptor se aleja del punto de llegada mientras el pase viaja: el balón
        // caía suelto en el 42% de los pases y la posesión duraba tres segundos (paquete E).
        if (ball.InFlight && !ball.IsShot && ReferenceEquals(ball.PassReceiver, p))
        {
            score += context.ChaseBallIncomingPassBonus;
            eval.IgnoreOuterLimit = true;
        }

        if (!ReferenceEquals(ctx.NearestToBall[p.Team], p))
        {
            score -= context.ChaseBallNotNearestPenalty;
        }

        eval.Context = score;
    }

    /// <summary>
    /// Marcaje con objetivo estable (§2.3): el rival lo fija <see cref="Marking"/> una vez por posesión.
    /// Si todavía no hay asignación (arranque del partido antes de la primera posesión, o un contexto de
    /// prueba construido a mano) se usa el rival más cercano, que es el comportamiento de la fase 0.
    /// </summary>
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

    private static void EvaluateSupport(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        if (!p.IsOutfield || ctx.HoldingTeam != p.Team)
        {
            eval.Discarded = true;
            return;
        }

        var ball = ctx.Ball;
        float carrierX = ball.Owner is not null ? ball.Owner.Position.X : ball.Position.X;

        // "Y propia acercada 1 hacia 2.5" (§3.5) se toma sobre la fila de la casilla-hogar, no sobre la Y
        // instantánea: con la Y instantánea el punto de apoyo se recalcula cada decisión y todo el bloque
        // converge en pocos ticks a la fila 2.5, se solapa y el partido se bloquea (ver informe del paquete B).
        var target = ClampToPitch(new Vec2(carrierX + (2f * direction), MoveToward(p.EffectiveHome.Y, PitchConstants.CenterRow, 1f)));
        eval.Target = target;

        int score = 0;
        if ((p.Position.X - carrierX) * direction > 0f)
        {
            score += context.SupportAheadBonus;
        }

        var players = ctx.Players;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team != p.Team || ReferenceEquals(other, p) || !other.OnPitch)
            {
                continue;
            }

            if (Vec2.Distance(other.Position, target) < SupportCrowdRadius)
            {
                score -= context.SupportCrowdedPenalty;
            }
        }

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
        bool found = false;
        int bestScore = 0;
        Vec2 bestPoint = p.Position;

        for (int d = 0; d < SpaceDirections.Length; d++)
        {
            for (int s = 0; s < SpaceDistances.Length; s++)
            {
                Vec2 candidate = ClampToPitch(p.Position + (SpaceDirections[d] * SpaceDistances[s]));
                candidate = p.Zone.Clamp(candidate, p.EffectiveHome, direction);

                int space = Centi(NearestOpponentDistance(players, p.Team, candidate));
                if (space > FindSpaceMaxSpaceCenti)
                {
                    space = FindSpaceMaxSpaceCenti;
                }

                int advance = Centi((candidate.X - p.Position.X) * direction);
                int score = (context.FindSpaceOpponentDistanceBonusPerCell * space / 100)
                    + (context.FindSpaceAdvanceBonusPerCell * advance / 100);

                if (carrier is not null && !SegmentBlocked(players, p.Team, carrier.Position, candidate))
                {
                    score += context.FindSpaceOpenLaneBonus;
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

        float minCells = longPass ? context.ShortPassMaxCells : 0f;
        float maxCells = longPass ? context.LongPassMaxCells : context.ShortPassMaxCells;

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

            if (HasOpponentWithin(players, mate, PitchConstants.PressureRadius))
            {
                continue;
            }

            if (longPass && SegmentBlocked(players, p.Team, p.Position, mate.Position))
            {
                continue;
            }

            int advance = Centi((mate.Position.X - p.Position.X) * direction);
            int rank = p.IsOutfield ? advance - (Centi(distance) * 20 / 100) : advance;
            if (receiver is null || rank > bestRank)
            {
                receiver = mate;
                bestRank = rank;
            }
        }

        int score;
        if (receiver is null)
        {
            score = -context.PassNoReceiverPenalty;
        }
        else
        {
            score = context.PassOpenReceiverBonus;
        }

        score += Slope(longPass ? context.LongPassTechniqueSlope : context.ShortPassTechniqueSlope, p.Technique);

        if (HasOpponentWithin(players, p, PitchConstants.PressureRadius))
        {
            score += context.PassUnderPressureBonus;
        }

        eval.Receiver = receiver;
        eval.Context = score;
    }

    /// <summary>
    /// Término de utilidad que aporta un atributo a una acción (ADR 0030 §1): <c>pendiente × (atributo −
    /// 50)</c>, con signo. El pivote es el 50 de un jugador medio, así que la pendiente no infla la
    /// puntuación global: reparte. Todo entero (RT-023).
    /// </summary>
    private static int Slope(int slope, int attribute) => slope * (attribute - AttributePivot);

    private static bool HasOpponentWithin(MatchPlayer[] players, MatchPlayer target, float radius)
    {
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == target.Team || !other.OnPitch)
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

    /// <summary>True si algún rival está a menos de <see cref="PassLaneRadius"/> del segmento from-&gt;to.</summary>
    private static bool SegmentBlocked(MatchPlayer[] players, int team, Vec2 from, Vec2 to)
    {
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == team || !other.OnPitch)
            {
                continue;
            }

            if (DistanceToSegment(from, to, other.Position) < PassLaneRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static void EvaluateDribble(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        eval.Target = ClampToPitch(new Vec2(p.Position.X + direction, MoveToward(p.Position.Y, PitchConstants.CenterRow, 1f)));

        int ahead = 0;
        var players = ctx.Players;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == p.Team || !other.OnPitch)
            {
                continue;
            }

            if ((other.Position.X - p.Position.X) * direction > 0f
                && Vec2.Distance(other.Position, p.Position) < DribbleAheadRadius)
            {
                ahead++;
            }
        }

        eval.Context = (ahead == 0
                ? context.DribbleOpenSpaceBonus
                : -(context.DribbleOpponentAheadPenalty * ahead))
            + Slope(context.DribbleTechniqueSlope, p.Technique)
            + Slope(context.DribbleSpeedSlope, p.Speed);
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
    private static void EvaluateShoot(MatchPlayer p, AiContext context, ref Eval eval)
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

        eval.Context = score;
    }

    private static void EvaluateTackle(MatchPlayer p, Ball ball, AiContext context, ref Eval eval)
    {
        if (p.TackleCooldown > 0)
        {
            eval.Discarded = true;
            return;
        }

        var carrier = ball.Owner;
        if (carrier is null || carrier.Team == p.Team)
        {
            eval.Context = -context.TackleOutOfReachPenalty;
            return;
        }

        eval.TackleTarget = carrier;
        eval.Target = carrier.Position;
        float distance = Vec2.Distance(p.Position, carrier.Position);
        eval.Context = distance <= context.TackleDistanceMaxCells
            ? context.TackleBallCarrierBonus
            : -context.TackleOutOfReachPenalty;
    }

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
        if (!p.IsOutfield || p.TackleCooldown > 0)
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
