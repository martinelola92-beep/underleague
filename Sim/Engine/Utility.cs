using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Vista del mundo que necesita la IA de utilidad (§3.5). El motor la rellena una vez por tick y la
/// reutiliza en todas las decisiones de ese tick: no se asigna nada por evaluación (RT-051).
/// </summary>
internal sealed class UtilityContext
{
    public UtilityContext(MatchPlayer[] players, Ball ball, AiWeights weights)
    {
        Players = players;
        Ball = ball;
        Weights = weights;
    }

    /// <summary>Todos los jugadores del partido, ordenados por id ascendente (RT-041, RT-097).</summary>
    public MatchPlayer[] Players { get; }

    /// <summary>Balón del partido.</summary>
    public Ball Ball { get; }

    /// <summary>Pesos de IA cargados de data/ai/weights.json (RT-096).</summary>
    public AiWeights Weights { get; }

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

    /// <summary>Distancia mínima que debe recorrer una acción de movimiento para no ser filtrada (§3.5).</summary>
    private const float LeashFilterMinAdvance = 0.25f;

    /// <summary>Radio en el que un rival presiona a un jugador (§3.5).</summary>
    private const float PressureRadius = 1.0f;

    /// <summary>Distancia máxima de pase para un jugador de campo (§3.5).</summary>
    private const float PassMaxCells = 7.0f;

    /// <summary>Radio de aglomeración alrededor del punto de apoyo (§3.5).</summary>
    private const float SupportCrowdRadius = 1.5f;

    /// <summary>Distancia por delante en la que un rival estorba al regate (§3.5).</summary>
    private const float DribbleAheadRadius = 2.0f;

    /// <summary>Fila central del campo.</summary>
    private const float CenterRow = Pitch.Rows / 2f;

    /// <summary>Resultado de evaluar una acción concreta; struct para no asignar por evaluación.</summary>
    private struct Eval
    {
        public int Context;
        public bool Discarded;
        public bool LeashFiltered;
        public Vec2 Target;
        public MatchPlayer? Receiver;
        public MatchPlayer? TackleTarget;
    }

    /// <summary>
    /// Elige la acción de mayor utilidad para p y deja en el jugador el objetivo de movimiento ya
    /// acotado a la correa (y al área si es portero), el receptor de pase y el objetivo de entrada.
    /// Si rows no es null, añade una fila por acción evaluada (volcado RT-098).
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

        for (int i = 0; i < legal.Count; i++)
        {
            var action = legal[i];
            var eval = Evaluate(ctx, p, action);
            int baseWeight = ctx.Weights.Base(p.Role, action);
            int tactical = ctx.Weights.Tactical(ctx.TacticalStates[p.Team], action);
            int traitMultiplier = p.ActionMultiplier(action);
            int score = (baseWeight * tactical / 100 * traitMultiplier / 100) + eval.Context;

            bool rejected = eval.Discarded || eval.LeashFiltered;
            rows?.Add(new UtilityRow(action, score, baseWeight, tactical, traitMultiplier, eval.Context, rejected));

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
        return best;
    }

    /// <summary>Acota target al radio de correa alrededor de la casilla-hogar efectiva de p (§3.3).</summary>
    public static Vec2 ClampToLeash(MatchPlayer p, Vec2 target)
    {
        Vec2 offset = target - p.EffectiveHome;
        float length = offset.Length;
        if (length <= p.LeashCells || length <= 0f)
        {
            return target;
        }

        return p.EffectiveHome + (offset * (p.LeashCells / length));
    }

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

    /// <summary>Convierte una distancia en casillas al entero de centésimas usado en los términos (§3.5).</summary>
    public static int Centi(float cells) => (int)(cells * 100f);

    /// <summary>Acerca value a target en un paso como máximo.</summary>
    private static float MoveToward(float value, float target, float step)
    {
        if (value < target)
        {
            return value + step > target ? target : value + step;
        }

        return value - step < target ? target : value - step;
    }

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
                EvaluateCover(ctx, p, context, ref eval);
                break;
            case PlayerAction.Pass:
                EvaluatePass(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.Dribble:
                EvaluateDribble(ctx, p, context, direction, ref eval);
                break;
            case PlayerAction.Shoot:
                EvaluateShoot(p, context, ref eval);
                break;
            case PlayerAction.Tackle:
                EvaluateTackle(p, ball, context, ref eval);
                break;
            case PlayerAction.Retreat:
                EvaluateRetreat(p, context, ref eval);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        if (eval.Discarded || !IsMovementAction(action))
        {
            return eval;
        }

        Vec2 raw = eval.Target;
        Vec2 clamped = ClampToLeash(p, raw);
        if (!p.IsOutfield)
        {
            clamped = ClampToArea(clamped, p.Team);
        }

        bool outsideLeash = Vec2.Distance(raw, p.EffectiveHome) > p.LeashCells;
        if (outsideLeash && Vec2.Distance(clamped, p.Position) < LeashFilterMinAdvance)
        {
            eval.LeashFiltered = true;
        }

        eval.Target = clamped;
        return eval;
    }

    private static bool IsMovementAction(PlayerAction action) =>
        action is PlayerAction.ChaseBall or PlayerAction.MarkOpponent or PlayerAction.OfferSupport
            or PlayerAction.CoverSpace or PlayerAction.Dribble or PlayerAction.Retreat;

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

        if (!ReferenceEquals(ctx.NearestToBall[p.Team], p))
        {
            score -= context.ChaseBallNotNearestPenalty;
        }

        eval.Context = score;
    }

    private static void EvaluateMark(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
    {
        if (!p.IsOutfield)
        {
            eval.Discarded = true;
            return;
        }

        MatchPlayer? target = null;
        float bestDistance = 0f;
        var players = ctx.Players;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other.Team == p.Team || !other.OnPitch || !other.IsOutfield)
            {
                continue;
            }

            if (Vec2.Distance(other.Position, p.EffectiveHome) > p.LeashCells)
            {
                continue;
            }

            float distance = Vec2.Distance(p.Position, other.Position);
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

        eval.Target = target.Position;
        eval.Context = -(context.MarkDistancePenaltyPerCell * Centi(bestDistance) / 100);
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
        var target = ClampToPitch(new Vec2(carrierX + (2f * direction), MoveToward(p.EffectiveHome.Y, CenterRow, 1f)));
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

    private static void EvaluateCover(UtilityContext ctx, MatchPlayer p, AiContext context, ref Eval eval)
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
            target = SegmentPointAtLeash(from, ownGoal, p.EffectiveHome, p.LeashCells);
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
    /// Punto del segmento a->b a distancia radius del centro home, el más cercano a a. Si el segmento
    /// nunca alcanza esa distancia, el punto del segmento más cercano a home ("acotado", §3.5).
    /// </summary>
    private static Vec2 SegmentPointAtLeash(Vec2 a, Vec2 b, Vec2 home, float radius)
    {
        Vec2 direction = b - a;
        Vec2 offset = a - home;
        float qa = (direction.X * direction.X) + (direction.Y * direction.Y);
        if (qa <= 0f)
        {
            return a;
        }

        float qb = 2f * ((offset.X * direction.X) + (offset.Y * direction.Y));
        float qc = (offset.X * offset.X) + (offset.Y * offset.Y) - (radius * radius);
        float discriminant = (qb * qb) - (4f * qa * qc);

        float t;
        if (discriminant < 0f)
        {
            t = -((offset.X * direction.X) + (offset.Y * direction.Y)) / qa;
        }
        else
        {
            t = (-qb - MathF.Sqrt(discriminant)) / (2f * qa);
        }

        t = Math.Clamp(t, 0f, 1f);
        return a + (direction * t);
    }

    private static void EvaluatePass(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        var players = ctx.Players;
        MatchPlayer? receiver = null;
        int bestRank = 0;

        for (int i = 0; i < players.Length; i++)
        {
            var mate = players[i];
            if (mate.Team != p.Team || ReferenceEquals(mate, p) || !mate.OnPitch)
            {
                continue;
            }

            float distance = Vec2.Distance(p.Position, mate.Position);
            if (p.IsOutfield && distance > PassMaxCells)
            {
                continue;
            }

            if (HasOpponentWithin(players, mate, PressureRadius))
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

        if (HasOpponentWithin(players, p, PressureRadius))
        {
            score += context.PassUnderPressureBonus;
        }

        eval.Receiver = receiver;
        eval.Context = score;
    }

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

    private static void EvaluateDribble(UtilityContext ctx, MatchPlayer p, AiContext context, int direction, ref Eval eval)
    {
        eval.Target = ClampToPitch(new Vec2(p.Position.X + direction, MoveToward(p.Position.Y, CenterRow, 1f)));

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

        eval.Context = ahead == 0
            ? context.DribbleOpenSpaceBonus
            : -(context.DribbleOpponentAheadPenalty * ahead);
    }

    private static void EvaluateShoot(MatchPlayer p, AiContext context, ref Eval eval)
    {
        Vec2 goal = Pitch.GoalCenter(p.Team);
        float distance = Vec2.Distance(p.Position, goal);
        int range = context.ShootBaseRangeCells + p.ShootRangeBonusCells;

        if (distance > range)
        {
            eval.Context = -context.ShootOutOfRangePenalty;
            return;
        }

        int angle = Centi(MathF.Abs(p.Position.Y - CenterRow));
        eval.Context = context.ShootInRangeBonus
            - (context.ShootDistancePenaltyPerCell * Centi(distance) / 100)
            - (context.ShootAnglePenaltyPerRow * angle / 100);
    }

    private static void EvaluateTackle(MatchPlayer p, Ball ball, AiContext context, ref Eval eval)
    {
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

    private static void EvaluateRetreat(MatchPlayer p, AiContext context, ref Eval eval)
    {
        eval.Target = p.EffectiveHome;
        float distance = Vec2.Distance(p.Position, p.EffectiveHome);
        eval.Context = context.RetreatDistanceBonusPerCell * Centi(distance) / 100;
        if (distance < 0.5f)
        {
            eval.Context -= context.RetreatAtHomePenalty;
        }
    }
}
