using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Engine;

/// <summary>
/// Motor de partido (docs/fase0-diseno.md §3). Bucle de ticks determinista: un único Pcg32 para todo
/// el partido, iteración de jugadores por id ascendente y ninguna colección sin orden (RT-020..RT-024).
/// Se construye una vez por partido y se ejecuta con <see cref="Run"/>.
/// </summary>
internal sealed class MatchEngine
{
    /// <summary>Ventana en ticks para atribuir asistencia al último pase completado (§3.8).</summary>
    private const int AssistWindowTicks = 60;

    /// <summary>Ticks que queda derribado el defensor que pierde un regate (§3.7).</summary>
    private const int DribbleLostKnockdownTicks = 6;

    /// <summary>Sumando a la probabilidad de roja cuando la entrada es dura (§3.7).</summary>
    private const int HardTackleRedBonus = 200;

    /// <summary>Sumando a la probabilidad de amarilla cuando la entrada es dura (§3.7).</summary>
    private const int HardTackleYellowBonus = 1500;

    /// <summary>Peso de la calidad del tiro en el porcentaje de parada (§3.7).</summary>
    private const int SaveQualityWeight = 60;

    /// <summary>Bono de calidad de un penalti (§3.8).</summary>
    private const int PenaltyQualityBonus = 15;

    /// <summary>Distancia del punto de penalti a la línea de gol, en casillas (§3.8).</summary>
    private const float PenaltySpotCells = 2f;

    /// <summary>Radio de recogida de un balón suelto (§3.7).</summary>
    private const float PickupRadius = 0.5f;

    /// <summary>Radio en el que el receptor recoge un pase que llega (§3.7).</summary>
    private const float PassArrivalRadius = 1.0f;

    /// <summary>Radio de presión de un rival sobre el poseedor (§3.7).</summary>
    private const float PressureRadius = 1.0f;

    /// <summary>Radio en el que se dispara un duelo de regate (§3.7).</summary>
    private const float DribbleDuelRadius = 0.8f;

    /// <summary>Margen extra de alcance al resolver una entrada (§3.7).</summary>
    private const float TackleReachMargin = 0.3f;

    /// <summary>Velocidad con la que queda un balón suelto tras un pase fallido (§3.7).</summary>
    private const float LooseBallSpeed = 0.1f;

    /// <summary>Fila central del campo.</summary>
    private const float CenterRow = Pitch.Rows / 2f;

    private readonly MatchSetup _setup;
    private readonly Catalog _catalog;
    private readonly SimConfig _config;
    private readonly Tuning _tuning;
    private readonly MatchPlayer[] _players;
    private readonly MatchPlayer?[] _goalkeepers = new MatchPlayer?[2];
    private readonly Ball _ball = new();
    private readonly UtilityContext _context;
    private readonly List<MatchEvent> _events = new();
    private readonly MatchReportBuilder _report = new();
    private readonly float[] _shift = new float[2];
    private readonly int _regulationTicks;
    private readonly int _bias;

    private Pcg32 _rng;
    private MatchPhase _phase = MatchPhase.Kickoff;
    private int _tick;
    private bool _goldenGoal;
    private int _possessingTeam = -1;
    private int _lastOwningTeam = -1;
    private int _transitionTicksLeft;
    private int _playTeam = -1;
    private bool _playOpen;
    private int _playPasses;
    private int _lastCompletedPassTick = -1;
    private MatchPlayer? _lastCompletedPasser;

    private RestartKind _pendingRestart = RestartKind.None;
    private int _restartTeam = -1;
    private int _restartTicksLeft;
    private Vec2 _restartPoint;
    private MatchPlayer? _penaltyTaker;

    public MatchEngine(MatchSetup setup, ulong seed, Catalog catalog, SimConfig config)
    {
        _setup = setup;
        _catalog = catalog;
        _config = config;
        _tuning = catalog.Tuning;
        _rng = new Pcg32(seed, seed ^ 0x5DEECE66DUL);
        _bias = setup.Referee.InitialBias;
        _regulationTicks = config.RegulationTicksOverride ?? _tuning.RegulationTicks;

        var players = new List<MatchPlayer>();
        AddTeam(players, setup.Home, 0);
        AddTeam(players, setup.Away, 1);
        players.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        _players = players.ToArray();
        for (int i = 0; i < _players.Length; i++)
        {
            _players[i].Index = i;
            if (!_players[i].IsOutfield)
            {
                _goalkeepers[_players[i].Team] = _players[i];
            }
        }

        _ball.InterceptAttempted = new bool[_players.Length];
        _context = new UtilityContext(_players, _ball, catalog.Ai);
        _context.TacticalStates[0] = TacticalState.OutOfPossession;
        _context.TacticalStates[1] = TacticalState.OutOfPossession;
    }

    /// <summary>Tipo de reanudación pendiente durante una fase Restart/Kickoff/Penalty (§3.8).</summary>
    private enum RestartKind
    {
        None,
        ThrowIn,
        GoalKick,
        Corner,
        Kickoff,
        Penalty,
    }

    private int RegulationTicks => _regulationTicks;

    /// <summary>Ejecuta el partido completo y devuelve eventos e informe (§3.2).</summary>
    public MatchResult Run()
    {
        ResetPositions();
        _ball.Park(new Vec2(Pitch.Columns / 2f, CenterRow));
        Emit(EventType.MatchStart, "kickoff");
        ScheduleKickoff(0);

        while (_phase != MatchPhase.Finished)
        {
            Step();
        }

        for (int i = 0; i < _players.Length; i++)
        {
            _report.Players.Add(_players[i].ToStats());
        }

        _report.FinalBias = _bias;
        return new MatchResult(_events, _report.Build());
    }

    private void AddTeam(List<MatchPlayer> players, TeamSetup team, int teamIndex)
    {
        var slots = team.Lineup.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            PlayerDefinition? definition = null;
            for (int j = 0; j < team.Players.Count; j++)
            {
                if (team.Players[j].Id == slot.PlayerId)
                {
                    definition = team.Players[j];
                    break;
                }
            }

            if (definition is null)
            {
                throw new ArgumentException($"la alineación de '{team.Id}' referencia al jugador {slot.PlayerId}, que no está en su plantilla", nameof(team));
            }

            int column = teamIndex == 0 ? slot.HomeCell.Column : Pitch.Columns - 1 - slot.HomeCell.Column;
            players.Add(new MatchPlayer(definition, teamIndex, players.Count, new Cell(column, slot.HomeCell.Row), _catalog));
        }
    }

    private void Step()
    {
        _tick++;
        UpdateTacticalState();
        UpdateBlockShift();
        UpdateContextCaches();

        if (_restartTicksLeft > 0)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                TickStateTimer(_players[i]);
            }

            _restartTicksLeft--;
            if (_restartTicksLeft == 0)
            {
                ResolveRestart();
            }
        }
        else
        {
            for (int i = 0; i < _players.Length; i++)
            {
                UpdatePlayer(_players[i]);
            }

            UpdateBall();
            CheckOutOfBounds();
        }

        CheckForfeit();
        if (_phase == MatchPhase.Finished)
        {
            return;
        }

        AccumulateMetrics();
        CheckEndConditions();
    }

    // ---------------------------------------------------------------- 3.4 estado táctico y bloque

    private void UpdateTacticalState()
    {
        int holding = -1;
        if (_ball.Owner is not null)
        {
            holding = _ball.Owner.Team;
        }
        else if (_ball.InFlight)
        {
            holding = _ball.LastTouchTeam;
        }

        if (holding >= 0 && holding != _possessingTeam)
        {
            if (_possessingTeam >= 0)
            {
                _transitionTicksLeft = _tuning.TransitionTicks;
                _context.TacticalStates[holding] = TacticalState.OffensiveTransition;
                _context.TacticalStates[1 - holding] = TacticalState.DefensiveTransition;
            }

            _possessingTeam = holding;
        }

        if (_transitionTicksLeft > 0)
        {
            _transitionTicksLeft--;
        }

        if (_transitionTicksLeft == 0 && _possessingTeam >= 0)
        {
            _context.TacticalStates[_possessingTeam] = TacticalState.InPossession;
            _context.TacticalStates[1 - _possessingTeam] = TacticalState.OutOfPossession;
        }
    }

    private void UpdateBlockShift()
    {
        for (int team = 0; team < 2; team++)
        {
            var shift = _catalog.Ai.Shift(_context.TacticalStates[team]);
            float target = shift.Shift;
            int speedTicks = shift.SpeedTicks > 0 ? shift.SpeedTicks : 1;
            float step = MathF.Abs(target) / speedTicks;
            float current = _shift[team];
            if (current < target)
            {
                current = current + step > target ? target : current + step;
            }
            else if (current > target)
            {
                current = current - step < target ? target : current - step;
            }

            _shift[team] = current;
        }

        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            float offset = _shift[player.Team] * Pitch.AttackDirection(player.Team);
            player.EffectiveHome = new Vec2(
                Math.Clamp(player.HomeCenter.X + offset, 0f, Pitch.Columns),
                player.HomeCenter.Y);
        }
    }

    private void UpdateContextCaches()
    {
        _context.NearestToBall[0] = null;
        _context.NearestToBall[1] = null;
        float[] best = { 0f, 0f };

        Vec2 point = _ball.InFlight ? _ball.FlightTarget : _ball.Position;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (!player.OnPitch)
            {
                continue;
            }

            float distance = Vec2.Distance(player.Position, point);
            if (_context.NearestToBall[player.Team] is null || distance < best[player.Team])
            {
                _context.NearestToBall[player.Team] = player;
                best[player.Team] = distance;
            }
        }

        if (_ball.Owner is not null)
        {
            _context.HoldingTeam = _ball.Owner.Team;
        }
        else if (_ball.InFlight)
        {
            _context.HoldingTeam = _ball.LastTouchTeam;
        }
        else
        {
            _context.HoldingTeam = -1;
        }
    }

    // ---------------------------------------------------------------- 3.2/3.3/3.6 jugadores

    private void UpdatePlayer(MatchPlayer player)
    {
        if (!player.OnPitch)
        {
            return;
        }

        TickStateTimer(player);
        if (!player.OnPitch)
        {
            return;
        }

        if (player.DribbleDuelCooldown > 0)
        {
            player.DribbleDuelCooldown--;
        }

        if (StateMachine.IsDecisionState(player.State)
            && (_tick + player.Id) % _tuning.DecisionIntervalTicks == 0)
        {
            Decide(player);
        }

        ExecuteAction(player);
    }

    private void TickStateTimer(MatchPlayer player)
    {
        if (player.StateTicksLeft <= 0)
        {
            return;
        }

        player.StateTicksLeft--;
        if (player.StateTicksLeft > 0)
        {
            return;
        }

        switch (player.State)
        {
            case PlayerState.Passing:
                LaunchPass(player);
                break;
            case PlayerState.Shooting:
                LaunchShot(player, isPenalty: false);
                break;
            case PlayerState.Tackling:
                ResolveTackle(player);
                break;
            case PlayerState.KnockedDown:
            case PlayerState.Celebrating:
                player.EnterState(PlayerState.Positioning, 0);
                break;
            default:
                break;
        }
    }

    private void Decide(MatchPlayer player)
    {
        List<UtilityRow>? rows = null;
        if (_config.DumpUtility is { } dump && dump.PlayerId == player.Id && dump.Tick == _tick && _report.UtilityDump is null)
        {
            rows = new List<UtilityRow>();
        }

        var state = player.State;
        var action = Utility.Choose(_context, player, rows);
        if (rows is not null)
        {
            _report.UtilityDump = new UtilityDump(player.Id, _tick, state, rows, action);
        }

        switch (action)
        {
            case PlayerAction.Pass:
                if (ReferenceEquals(_ball.Owner, player))
                {
                    player.EnterState(PlayerState.Passing, _tuning.States.PassingTicks);
                }

                break;
            case PlayerAction.Shoot:
                if (ReferenceEquals(_ball.Owner, player))
                {
                    player.EnterState(PlayerState.Shooting, _tuning.States.ShootingTicks);
                }

                break;
            case PlayerAction.Tackle:
                player.EnterState(PlayerState.Tackling, _tuning.States.TacklingTicks);
                break;
            case PlayerAction.Dribble:
                if (ReferenceEquals(_ball.Owner, player))
                {
                    player.EnterState(PlayerState.Dribbling, 0);
                }

                break;
            case PlayerAction.ChaseBall:
                player.EnterState(PlayerState.Chasing, 0);
                break;
            default:
                player.EnterState(PlayerState.Positioning, 0);
                break;
        }
    }

    private void ExecuteAction(MatchPlayer player)
    {
        switch (player.State)
        {
            case PlayerState.Positioning:
            case PlayerState.Chasing:
                Move(player, dribbling: false);
                break;
            case PlayerState.Dribbling:
                TryDribbleDuel(player);
                if (player.State == PlayerState.Dribbling)
                {
                    Move(player, dribbling: true);
                }

                break;
            default:
                player.Velocity = new Vec2(0f, 0f);
                break;
        }
    }

    private void Move(MatchPlayer player, bool dribbling)
    {
        Vec2 target = Utility.ClampToLeash(player, player.TargetPoint);
        if (!player.IsOutfield)
        {
            target = Utility.ClampToArea(target, player.Team);
        }

        Vec2 delta = target - player.Position;
        float distance = delta.Length;
        float step = SpeedPerTick(player, dribbling);

        Vec2 next = distance <= step || distance <= 0f
            ? target
            : player.Position + (delta * (step / distance));

        next = Utility.ClampToPitch(next);
        if (!player.IsOutfield)
        {
            next = Utility.ClampToArea(next, player.Team);
        }

        player.Velocity = next - player.Position;
        player.Position = next;
    }

    private float SpeedPerTick(MatchPlayer player, bool dribbling)
    {
        var movement = _tuning.Movement;
        int milli = movement.BaseCellsPerTickMilli + (movement.SpeedCellsPerTickMilliPer99 * player.Speed / 99);

        int percent = 100;
        if (dribbling)
        {
            percent = percent * movement.DribbleSpeedPercent / 100;
        }

        if (player.SpeedBonusPercent != 0)
        {
            percent = percent * (100 + player.SpeedBonusPercent) / 100;
        }

        milli = milli * percent / 100;

        if (_tick > movement.FatigueStartTick)
        {
            int span = RegulationTicks - movement.FatigueStartTick;
            if (span > 0)
            {
                int progress = Math.Clamp((_tick - movement.FatigueStartTick) * 1000 / span, 0, 1000);
                int slow = movement.FatigueMaxSlowPercent * (100 - player.Stamina) / 100 * progress / 100;
                if (player.FatigueResistancePercent != 0)
                {
                    slow = slow * (100 - player.FatigueResistancePercent) / 100;
                }

                milli = milli * (1000 - Math.Clamp(slow, 0, 990)) / 1000;
            }
        }

        return milli / 1000f;
    }

    // ---------------------------------------------------------------- 3.7 balón

    private void UpdateBall()
    {
        if (_ball.Owner is not null)
        {
            _ball.Position = _ball.Owner.Position;
            return;
        }

        if (_ball.InFlight)
        {
            UpdateFlight();
            return;
        }

        UpdateLooseBall();
    }

    private void UpdateFlight()
    {
        _ball.FlightTicksLeft--;
        int elapsed = _ball.FlightTicksTotal - _ball.FlightTicksLeft;
        float t = _ball.FlightTicksTotal <= 0 ? 1f : elapsed / (float)_ball.FlightTicksTotal;
        _ball.Position = Vec2.Lerp(_ball.FlightOrigin, _ball.FlightTarget, t);

        if (!_ball.IsShot && TryIntercept())
        {
            return;
        }

        if (_ball.FlightTicksLeft <= 0)
        {
            if (_ball.IsShot)
            {
                ResolveShotArrival();
            }
            else
            {
                ResolvePassArrival();
            }
        }
    }

    private bool TryIntercept()
    {
        var passer = _ball.Passer;
        if (passer is null)
        {
            return false;
        }

        var pass = _tuning.Pass;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player.Team == passer.Team || !CanTouchBall(player) || _ball.InterceptAttempted[i])
            {
                continue;
            }

            if (Vec2.Distance(player.Position, _ball.Position) >= pass.InterceptRadiusCells)
            {
                continue;
            }

            _ball.InterceptAttempted[i] = true;
            if (!_rng.Chance(pass.InterceptBaseChance + (pass.InterceptTechniqueFactor * (player.Technique - 50))))
            {
                continue;
            }

            Emit(EventType.PassFailed, "intercepted", passer, opponent: player);
            SetOwner(player);
            Emit(EventType.Recovery, "intercepted", player);
            return true;
        }

        return false;
    }

    private void ResolvePassArrival()
    {
        var passer = _ball.Passer;
        var receiver = _ball.PassReceiver;

        if (_ball.PassSucceeds && receiver is not null && CanTouchBall(receiver)
            && Vec2.Distance(receiver.Position, _ball.Position) < PassArrivalRadius)
        {
            SetOwner(receiver);
            Emit(EventType.PassCompleted, "completed", passer, receiver);
            if (passer is not null)
            {
                passer.PassesCompleted++;
                _lastCompletedPassTick = _tick;
                _lastCompletedPasser = passer;
            }

            _playPasses++;
            return;
        }

        Vec2 direction = (_ball.FlightTarget - _ball.FlightOrigin).Normalized;
        _ball.SetLoose(direction * LooseBallSpeed);
        Emit(EventType.PassFailed, "loose", passer);
    }

    private void UpdateLooseBall()
    {
        _ball.Position += _ball.Velocity;
        _ball.Velocity *= _tuning.Ball.LooseBallFrictionPercent / 100f;

        MatchPlayer? nearest = null;
        float bestDistance = 0f;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (!CanTouchBall(player))
            {
                continue;
            }

            float distance = Vec2.Distance(player.Position, _ball.Position);
            if (distance >= PickupRadius)
            {
                continue;
            }

            if (nearest is null || distance < bestDistance)
            {
                nearest = player;
                bestDistance = distance;
            }
        }

        if (nearest is null)
        {
            return;
        }

        int previousTouchTeam = _ball.LastTouchTeam;
        SetOwner(nearest);
        if (previousTouchTeam >= 0 && previousTouchTeam != nearest.Team)
        {
            Emit(EventType.Recovery, "loose", nearest);
        }
    }

    private bool CanTouchBall(MatchPlayer player) =>
        player.OnPitch
        && player.State is not (PlayerState.KnockedDown or PlayerState.Injured
            or PlayerState.SentOff or PlayerState.Celebrating);

    /// <summary>
    /// Deja el balón parado en un punto sin dueño. Si había poseedor, sale de Dribbling/Passing/Shooting
    /// para que no siga conduciendo un balón que ya no tiene (§3.6).
    /// </summary>
    private void ParkBall(Vec2 position)
    {
        var previous = _ball.Owner;
        if (previous is not null && previous.State is PlayerState.Dribbling or PlayerState.Passing or PlayerState.Shooting)
        {
            previous.EnterState(PlayerState.Positioning, 0);
        }

        _ball.Park(position);
    }

    private void SetOwner(MatchPlayer player)
    {
        var previous = _ball.Owner;
        if (previous is not null && !ReferenceEquals(previous, player)
            && previous.State is PlayerState.Dribbling or PlayerState.Passing or PlayerState.Shooting)
        {
            previous.EnterState(PlayerState.Positioning, 0);
        }

        _ball.Owner = player;
        _ball.InFlight = false;
        _ball.IsShot = false;
        _ball.PassReceiver = null;
        _ball.Passer = null;
        _ball.Shooter = null;
        _ball.Velocity = new Vec2(0f, 0f);
        _ball.Position = player.Position;
        _ball.LastTouchPlayer = player;
        _ball.LastTouchTeam = player.Team;

        if (player.State is PlayerState.Positioning or PlayerState.Chasing or PlayerState.Tackling)
        {
            player.EnterState(PlayerState.Dribbling, 0);
        }

        if (_lastOwningTeam >= 0 && _lastOwningTeam != player.Team)
        {
            _report.PossessionChanges++;
        }

        _lastOwningTeam = player.Team;

        if (_playTeam != player.Team)
        {
            EndPlay("lost");
            StartPlay(player);
        }
    }

    private void StartPlay(MatchPlayer player)
    {
        _playOpen = true;
        _playTeam = player.Team;
        _playPasses = 0;
        Emit(EventType.PlayStart, "start", player);
    }

    private void EndPlay(string detail)
    {
        if (!_playOpen)
        {
            return;
        }

        Emit(EventType.PlayEnd, detail, team: _playTeam);
        if (_playPasses >= 1)
        {
            _report.PassChains++;
            _report.PassChainTotalLength += _playPasses;
        }

        _playOpen = false;
        _playTeam = -1;
        _playPasses = 0;
    }

    private void LaunchPass(MatchPlayer passer)
    {
        if (!ReferenceEquals(_ball.Owner, passer))
        {
            passer.EnterState(PlayerState.Positioning, 0);
            return;
        }

        var receiver = passer.PassReceiver;
        if (receiver is null || !receiver.OnPitch)
        {
            receiver = MostAdvancedTeammate(passer);
        }

        var pass = _tuning.Pass;
        int direction = Pitch.AttackDirection(passer.Team);
        Vec2 receiverPoint = receiver is not null
            ? receiver.Position
            : Utility.ClampToPitch(new Vec2(passer.Position.X + (3f * direction), passer.Position.Y));

        float distance = Vec2.Distance(passer.Position, receiverPoint);
        int pressure = HasOpponentWithin(passer, PressureRadius) ? 1 : 0;
        int probability = pass.BaseSuccess
            + (pass.TechniqueFactor * (passer.Technique - 50))
            + (passer.PassQualityBonus * 100)
            - (pass.DistancePenaltyPerCell * Utility.Centi(distance) / 100)
            - (pass.PressurePenalty * pressure);

        bool succeeds = receiver is not null && _rng.Chance(Math.Clamp(probability, 500, 9800));
        int ticks = FlightTicks(distance, _tuning.Ball.PassSpeedCellsPerTickMilli);
        Vec2 target = receiver is not null
            ? Utility.ClampToPitch(receiver.Position + (receiver.Velocity * ticks))
            : receiverPoint;

        Emit(EventType.PassAttempted, "attempted", passer, receiver);
        passer.PassesAttempted++;

        _ball.Owner = null;
        _ball.InFlight = true;
        _ball.IsShot = false;
        _ball.Passer = passer;
        _ball.PassReceiver = receiver;
        _ball.PassSucceeds = succeeds;
        _ball.FlightOrigin = passer.Position;
        _ball.FlightTarget = target;
        _ball.FlightTicksTotal = ticks;
        _ball.FlightTicksLeft = ticks;
        _ball.LastTouchPlayer = passer;
        _ball.LastTouchTeam = passer.Team;
        Array.Clear(_ball.InterceptAttempted);

        passer.EnterState(PlayerState.Positioning, 0);
    }

    private MatchPlayer? MostAdvancedTeammate(MatchPlayer passer)
    {
        int direction = Pitch.AttackDirection(passer.Team);
        MatchPlayer? best = null;
        float bestAdvance = 0f;
        for (int i = 0; i < _players.Length; i++)
        {
            var mate = _players[i];
            if (mate.Team != passer.Team || ReferenceEquals(mate, passer) || !CanTouchBall(mate))
            {
                continue;
            }

            float advance = (mate.Position.X - passer.Position.X) * direction;
            if (best is null || advance > bestAdvance)
            {
                best = mate;
                bestAdvance = advance;
            }
        }

        return best;
    }

    private int FlightTicks(float distance, int speedMilli)
    {
        int distanceMilli = (int)(distance * 1000f);
        int speed = speedMilli > 0 ? speedMilli : 1;
        int ticks = (distanceMilli + speed - 1) / speed;
        return ticks < 1 ? 1 : ticks;
    }

    private bool HasOpponentWithin(MatchPlayer player, float radius)
    {
        for (int i = 0; i < _players.Length; i++)
        {
            var other = _players[i];
            if (other.Team == player.Team || !other.OnPitch)
            {
                continue;
            }

            if (Vec2.Distance(other.Position, player.Position) < radius)
            {
                return true;
            }
        }

        return false;
    }

    private int CountOpponentsWithin(MatchPlayer player, float radius)
    {
        int count = 0;
        for (int i = 0; i < _players.Length; i++)
        {
            var other = _players[i];
            if (other.Team == player.Team || !other.OnPitch)
            {
                continue;
            }

            if (Vec2.Distance(other.Position, player.Position) < radius)
            {
                count++;
            }
        }

        return count;
    }

    private void LaunchShot(MatchPlayer shooter, bool isPenalty)
    {
        if (!ReferenceEquals(_ball.Owner, shooter))
        {
            shooter.EnterState(PlayerState.Positioning, 0);
            return;
        }

        var shot = _tuning.Shot;
        Vec2 goal = Pitch.GoalCenter(shooter.Team);
        float distance = Vec2.Distance(shooter.Position, goal);
        int pressure = isPenalty ? 0 : CountOpponentsWithin(shooter, PressureRadius);

        int raw = shot.BaseQuality
            + (shot.TechniqueFactor * shooter.Technique)
            + (shot.StrengthFactor * shooter.Strength)
            + (shooter.ShotQualityBonus * 100)
            - (shot.DistancePenaltyPerCell * Utility.Centi(distance) / 100)
            - (shot.PressurePenalty * pressure);
        int quality = Math.Clamp(raw / 100, 5, 95);
        if (isPenalty)
        {
            quality = Math.Clamp(quality + PenaltyQualityBonus, 5, 95);
        }

        bool offTarget = _rng.Chance(shot.OffTargetBase
            + (shot.OffTargetDistanceFactor * Utility.Centi(distance) / 100)
            - (quality * 20));

        _report.Shots[shooter.Team]++;
        shooter.Shots++;
        if (!offTarget)
        {
            _report.ShotsOnTarget[shooter.Team]++;
        }

        Emit(EventType.Shot, offTarget ? "offTarget" : "onTarget", shooter);
        EndPlay("shot");

        Vec2 target = offTarget
            ? new Vec2(goal.X, shooter.Position.Y < CenterRow ? 0f : Pitch.Rows)
            : goal;

        _ball.Owner = null;
        _ball.InFlight = true;
        _ball.IsShot = true;
        _ball.Shooter = shooter;
        _ball.ShotOnTarget = !offTarget;
        _ball.ShotQuality = quality;
        _ball.ShotDistance = distance;
        _ball.ShotIsPenalty = isPenalty;
        _ball.FlightOrigin = shooter.Position;
        _ball.FlightTarget = target;
        int ticks = FlightTicks(Vec2.Distance(shooter.Position, target), _tuning.Ball.ShotSpeedCellsPerTickMilli);
        _ball.FlightTicksTotal = ticks;
        _ball.FlightTicksLeft = ticks;
        _ball.LastTouchPlayer = shooter;
        _ball.LastTouchTeam = shooter.Team;

        shooter.EnterState(PlayerState.Positioning, 0);
    }

    private void ResolveShotArrival()
    {
        var shooter = _ball.Shooter;
        if (shooter is null)
        {
            _ball.SetLoose(new Vec2(0f, 0f));
            return;
        }

        int attackingTeam = shooter.Team;
        int defendingTeam = 1 - attackingTeam;

        if (!_ball.ShotOnTarget)
        {
            ScheduleGoalKick(defendingTeam);
            return;
        }

        var goalkeeper = _goalkeepers[defendingTeam];
        if (goalkeeper is not null && goalkeeper.OnPitch)
        {
            var save = _tuning.Save;
            int relevant = _ball.ShotDistance <= save.CloseRangeCells
                ? goalkeeper.Speed + goalkeeper.SaveBonusClose
                : goalkeeper.Strength + goalkeeper.SaveBonusFar;

            int decayFactor = Math.Clamp((100 - goalkeeper.Stamina) * 100 / 50, 20, 200);
            int decay = save.ConsecutiveShotDecayPercent * goalkeeper.ConsecutiveSaves * decayFactor / 100;
            int savePercent = Math.Clamp(
                save.BasePercent
                + ((relevant - 50) * save.AttributeWeightPercent / 50)
                - ((_ball.ShotQuality - 50) * SaveQualityWeight / 100)
                - decay,
                5,
                95);

            if (_rng.Chance(savePercent * 100))
            {
                goalkeeper.ConsecutiveSaves++;
                SetOwner(goalkeeper);
                Emit(EventType.Save, _ball.ShotIsPenalty ? "penalty" : "save", goalkeeper, opponent: shooter);
                return;
            }
        }

        ScoreGoal(shooter);
    }

    private void ScoreGoal(MatchPlayer shooter)
    {
        int team = shooter.Team;
        _report.Goals[team]++;
        shooter.Goals++;

        var goalkeeper = _goalkeepers[1 - team];
        if (goalkeeper is not null)
        {
            goalkeeper.ConsecutiveSaves = 0;
        }

        MatchPlayer? assistant = null;
        if (_lastCompletedPasser is not null
            && _lastCompletedPasser.Team == team
            && !ReferenceEquals(_lastCompletedPasser, shooter)
            && _tick - _lastCompletedPassTick < AssistWindowTicks)
        {
            assistant = _lastCompletedPasser;
            assistant.Assists++;
        }

        Emit(EventType.Goal, _goldenGoal ? "goldenGoal" : "goal", shooter, assistant);
        shooter.EnterState(PlayerState.Celebrating, _tuning.States.CelebratingTicks);
        ParkBall(Pitch.GoalCenter(team));
        _lastCompletedPasser = null;

        if (_goldenGoal)
        {
            EndMatch(team, "goldenGoal");
            return;
        }

        ScheduleKickoff(1 - team);
    }

    private void TryDribbleDuel(MatchPlayer carrier)
    {
        if (carrier.DribbleDuelCooldown > 0)
        {
            return;
        }

        MatchPlayer? defender = null;
        float bestDistance = 0f;
        for (int i = 0; i < _players.Length; i++)
        {
            var other = _players[i];
            if (other.Team == carrier.Team || !other.IsOutfield || !CanTouchBall(other))
            {
                continue;
            }

            float distance = Vec2.Distance(other.Position, carrier.Position);
            if (distance >= DribbleDuelRadius)
            {
                continue;
            }

            if (defender is null || distance < bestDistance)
            {
                defender = other;
                bestDistance = distance;
            }
        }

        if (defender is null)
        {
            return;
        }

        var dribble = _tuning.Dribble;
        Emit(EventType.DribbleAttempted, "attempted", carrier, opponent: defender);
        carrier.DribbleDuelCooldown = _tuning.States.DribbleDuelCooldownTicks;

        int win = dribble.BaseWin
            + (dribble.AttackerTechniqueFactor * (carrier.Technique - 50))
            - (dribble.DefenderSpeedFactor * (defender.Speed - 50))
            - (dribble.DefenderStrengthFactor * (defender.Strength - 50));

        if (_rng.Chance(win))
        {
            Emit(EventType.DribbleWon, "won", carrier, opponent: defender);
            defender.EnterState(PlayerState.KnockedDown, DribbleLostKnockdownTicks);
            return;
        }

        Emit(EventType.DribbleLost, "lost", carrier, opponent: defender);
        SetOwner(defender);
        Emit(EventType.Recovery, "dribble", defender);
    }

    private void ResolveTackle(MatchPlayer tackler)
    {
        var carrier = tackler.TackleTarget;
        float reach = _catalog.Ai.Context.TackleDistanceMaxCells + TackleReachMargin;
        if (carrier is null || !ReferenceEquals(_ball.Owner, carrier)
            || Vec2.Distance(tackler.Position, carrier.Position) > reach)
        {
            tackler.EnterState(PlayerState.Positioning, 0);
            return;
        }

        var tackle = _tuning.Tackle;
        int win = tackle.BaseWin
            + (tackle.StrengthFactor * (tackler.Strength - 50))
            + (tackle.SpeedFactor * (tackler.Speed - 50))
            - (tackle.CarrierTechniqueFactor * (carrier.Technique - 50));

        int biasShift = -(_tuning.Referee.BiasFoulShiftPer10 * _bias / 10);
        if (tackler.Team == 1)
        {
            biasShift = -biasShift;
        }

        int foulChance = tackle.FoulBase
            + (tackle.FoulStrengthFactor * (tackler.Strength - 50))
            + (tackler.FoulChanceBonus * 100)
            + (tackler.HardTackleBonus * 100)
            + biasShift;

        bool isFoul = _rng.Chance(foulChance);
        bool isWin = _rng.Chance(win);

        _report.Tackles++;
        tackler.Tackles++;
        Emit(EventType.Tackle, isFoul ? "foul" : (isWin ? "won" : "missed"), tackler, opponent: carrier);

        if (isFoul)
        {
            ResolveFoul(tackler, carrier);
        }
        else if (isWin)
        {
            tackler.TacklesWon++;
            carrier.EnterState(PlayerState.KnockedDown, _tuning.States.KnockedDownTicks);
            SetOwner(tackler);
            Emit(EventType.Recovery, "tackle", tackler);
        }
        else
        {
            tackler.EnterState(PlayerState.KnockedDown, _tuning.States.KnockedDownTicks / 2);
        }

        ResolveInjury(tackler, carrier, isFoul);
    }

    private void ResolveFoul(MatchPlayer tackler, MatchPlayer carrier)
    {
        var tackle = _tuning.Tackle;
        _report.Fouls++;
        tackler.Fouls++;
        Emit(EventType.Foul, "foul", tackler, opponent: carrier);
        bool inOwnArea = Pitch.IsInArea(tackler.Position, tackler.Team);
        tackler.EnterState(PlayerState.KnockedDown, _tuning.States.KnockedDownTicks);

        bool hard = tackler.HasTrait(Trait.Aggressive)
            || tackler.HasTrait(Trait.Dirty)
            || tackler.Strength * 100 >= tackle.HardTackleThreshold;

        if (_rng.Chance(tackle.RedCardBase + (hard ? HardTackleRedBonus : 0)))
        {
            SendOff(tackler);
        }
        else if (_rng.Chance(tackle.YellowCardBase + (hard ? HardTackleYellowBonus : 0)))
        {
            tackler.YellowCards++;
            tackler.Cards++;
            _report.YellowCards++;
            Emit(EventType.Card, "yellow", tackler);
            if (tackler.YellowCards >= 2 && tackle.SecondYellowIsRed)
            {
                SendOff(tackler);
            }
        }

        if (inOwnArea && _rng.Chance(_tuning.Referee.PenaltyOnFoulInArea))
        {
            SchedulePenalty(carrier.Team);
        }
    }

    private void SendOff(MatchPlayer player)
    {
        player.Cards++;
        _report.RedCards++;
        Emit(EventType.Card, "red", player);
        if (ReferenceEquals(_ball.Owner, player))
        {
            ParkBall(player.Position);
        }

        player.LeavePitch(PlayerState.SentOff);
    }

    private void ResolveInjury(MatchPlayer tackler, MatchPlayer victim, bool isFoul)
    {
        var injury = _tuning.Injury;
        int chance = injury.OnTackleBase
            + (isFoul ? injury.OnFoulBase : 0)
            + (injury.AttackerStrengthFactor * (tackler.Strength - 50))
            - (injury.VictimStaminaResistFactor * (victim.Stamina - 50))
            + (tackler.InjuryChanceBonus * 100)
            - (victim.InjuryResistanceBonus * 100);

        if (!_rng.Chance(Math.Clamp(chance, 0, 5000)))
        {
            return;
        }

        bool severe = _rng.Chance(injury.SevereShare);
        _report.Injuries++;
        victim.Injured = true;
        Emit(EventType.Injury, severe ? "severe" : "minor", victim, opponent: tackler);

        if (ReferenceEquals(_ball.Owner, victim))
        {
            ParkBall(victim.Position);
        }

        victim.LeavePitch(PlayerState.Injured);
    }

    // ---------------------------------------------------------------- 3.8 fuera, reanudaciones

    private void CheckOutOfBounds()
    {
        if (_ball.Owner is not null || (_ball.InFlight && _ball.IsShot))
        {
            return;
        }

        Vec2 position = _ball.Position;
        if (position.Y < 0f || position.Y > Pitch.Rows)
        {
            int team = _ball.LastTouchTeam >= 0 ? 1 - _ball.LastTouchTeam : 0;
            ScheduleThrowIn(team, Utility.ClampToPitch(position));
            return;
        }

        if (position.X >= 0f && position.X <= Pitch.Columns)
        {
            return;
        }

        int defendingTeam = position.X < 0f ? 0 : 1;
        int attackingTeam = 1 - defendingTeam;
        if (_ball.LastTouchTeam == attackingTeam)
        {
            ScheduleGoalKick(defendingTeam);
        }
        else
        {
            float cornerX = defendingTeam == 0 ? 0f : Pitch.Columns;
            float cornerY = position.Y < CenterRow ? 0f : Pitch.Rows;
            ScheduleCorner(attackingTeam, new Vec2(cornerX, cornerY));
        }
    }

    private void ScheduleThrowIn(int team, Vec2 point)
    {
        BeginRestart(RestartKind.ThrowIn, team, point, _tuning.Restart.ThrowInTicks, MatchPhase.Restart);
    }

    private void ScheduleGoalKick(int team)
    {
        var goalkeeper = _goalkeepers[team];
        Vec2 point = goalkeeper is not null && goalkeeper.OnPitch
            ? goalkeeper.HomeCenter
            : Pitch.GoalCenter(1 - team);
        BeginRestart(RestartKind.GoalKick, team, point, _tuning.Restart.GoalKickTicks, MatchPhase.Restart);
    }

    private void ScheduleCorner(int team, Vec2 point)
    {
        BeginRestart(RestartKind.Corner, team, point, _tuning.Restart.CornerTicks, MatchPhase.Restart);
    }

    private void ScheduleKickoff(int team)
    {
        BeginRestart(RestartKind.Kickoff, team, new Vec2(Pitch.Columns / 2f, CenterRow), _tuning.Restart.KickoffTicks, MatchPhase.Kickoff);
    }

    private void SchedulePenalty(int team)
    {
        int direction = Pitch.AttackDirection(team);
        Vec2 goal = Pitch.GoalCenter(team);
        var point = new Vec2(goal.X - (PenaltySpotCells * direction), CenterRow);

        _penaltyTaker = BestPenaltyTaker(team);
        BeginRestart(RestartKind.Penalty, team, point, _tuning.Restart.PenaltyTicks, MatchPhase.Penalty);
    }

    private MatchPlayer? BestPenaltyTaker(int team)
    {
        MatchPlayer? best = null;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player.Team != team || !player.IsOutfield || !player.OnPitch)
            {
                continue;
            }

            if (best is null || player.Technique > best.Technique)
            {
                best = player;
            }
        }

        return best;
    }

    private void BeginRestart(RestartKind kind, int team, Vec2 point, int ticks, MatchPhase phase)
    {
        _pendingRestart = kind;
        _restartTeam = team;
        _restartPoint = point;
        _restartTicksLeft = ticks > 0 ? ticks : 1;
        _phase = phase;
        ParkBall(point);
        EndPlay("lost");
    }

    private void ResolveRestart()
    {
        var kind = _pendingRestart;
        _pendingRestart = RestartKind.None;
        _phase = _goldenGoal ? MatchPhase.MobGoldenGoal : MatchPhase.OpenPlay;

        switch (kind)
        {
            case RestartKind.ThrowIn:
                TakeRestart(_restartTeam, _restartPoint, "throwIn", outfieldOnly: true);
                break;
            case RestartKind.Corner:
                TakeRestart(_restartTeam, _restartPoint, "corner", outfieldOnly: true);
                break;
            case RestartKind.GoalKick:
                TakeGoalKick(_restartTeam);
                break;
            case RestartKind.Kickoff:
                TakeKickoff(_restartTeam);
                break;
            case RestartKind.Penalty:
                TakePenalty();
                break;
            default:
                break;
        }
    }

    private void TakeRestart(int team, Vec2 point, string detail, bool outfieldOnly)
    {
        MatchPlayer? taker = null;
        float bestDistance = 0f;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player.Team != team || !CanTouchBall(player) || (outfieldOnly && !player.IsOutfield))
            {
                continue;
            }

            float distance = Vec2.Distance(player.Position, point);
            if (taker is null || distance < bestDistance)
            {
                taker = player;
                bestDistance = distance;
            }
        }

        if (taker is null)
        {
            return;
        }

        taker.Position = point;
        taker.Velocity = new Vec2(0f, 0f);
        taker.EnterState(PlayerState.Positioning, 0);
        SetOwner(taker);
        Emit(EventType.Recovery, detail, taker);
    }

    private void TakeGoalKick(int team)
    {
        var goalkeeper = _goalkeepers[team];
        if (goalkeeper is null || !goalkeeper.OnPitch)
        {
            TakeRestart(team, _restartPoint, "goalKick", outfieldOnly: false);
            return;
        }

        goalkeeper.Position = goalkeeper.HomeCenter;
        goalkeeper.Velocity = new Vec2(0f, 0f);
        goalkeeper.EnterState(PlayerState.Positioning, 0);
        SetOwner(goalkeeper);
        Emit(EventType.Recovery, "goalKick", goalkeeper);
    }

    private void TakeKickoff(int team)
    {
        _shift[0] = 0f;
        _shift[1] = 0f;
        ResetPositions();

        var center = new Vec2(Pitch.Columns / 2f, CenterRow);
        MatchPlayer? taker = null;
        float bestDistance = 0f;
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player.Team != team || !CanTouchBall(player) || !player.IsOutfield)
            {
                continue;
            }

            float distance = Vec2.Distance(player.HomeCenter, center);
            if (taker is null || distance < bestDistance)
            {
                taker = player;
                bestDistance = distance;
            }
        }

        if (taker is null)
        {
            return;
        }

        taker.Position = center;
        taker.Velocity = new Vec2(0f, 0f);
        taker.EnterState(PlayerState.Positioning, 0);
        SetOwner(taker);
        Emit(EventType.Recovery, "kickoff", taker);
    }

    private void TakePenalty()
    {
        var taker = _penaltyTaker;
        _penaltyTaker = null;
        if (taker is null || !CanTouchBall(taker))
        {
            ScheduleGoalKick(1 - _restartTeam);
            return;
        }

        taker.Position = _restartPoint;
        taker.Velocity = new Vec2(0f, 0f);
        taker.EnterState(PlayerState.Positioning, 0);
        SetOwner(taker);
        LaunchShot(taker, isPenalty: true);
    }

    private void ResetPositions()
    {
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (!player.OnPitch)
            {
                continue;
            }

            player.Position = player.HomeCenter;
            player.Velocity = new Vec2(0f, 0f);
            player.EffectiveHome = player.HomeCenter;
            if (player.State is not (PlayerState.Celebrating or PlayerState.KnockedDown))
            {
                player.EnterState(PlayerState.Positioning, 0);
            }

            player.TargetPoint = player.HomeCenter;
        }
    }

    // ---------------------------------------------------------------- 3.9/3.10 fin y métricas

    private void CheckForfeit()
    {
        int home = CountOnPitch(0);
        int away = CountOnPitch(1);
        if (home >= 5 && away >= 5)
        {
            return;
        }

        _report.Forfeit = true;
        EndMatch(home < 5 ? 1 : 0, "forfeit");
    }

    private int CountOnPitch(int team)
    {
        int count = 0;
        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i].Team == team && _players[i].OnPitch)
            {
                count++;
            }
        }

        return count;
    }

    private void AccumulateMetrics()
    {
        float third = Pitch.Columns / 3f;
        float x = _ball.Position.X;
        int index = x < third ? 0 : (x < 2f * third ? 1 : 2);
        _report.BallTicksByThird[index]++;

        if (_ball.Owner is not null)
        {
            _report.PossessionTicks[_ball.Owner.Team]++;
        }

        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player.OnPitch)
            {
                player.TicksOnPitch++;
            }
        }

        for (int team = 0; team < 2; team++)
        {
            var goalkeeper = _goalkeepers[team];
            if (goalkeeper is not null && goalkeeper.OnPitch && !Pitch.IsInArea(goalkeeper.Position, team))
            {
                _report.GoalkeeperLeftArea = true;
            }
        }
    }

    private void CheckEndConditions()
    {
        if (!_goldenGoal)
        {
            if (_tick < RegulationTicks)
            {
                return;
            }

            if (_report.Goals[0] != _report.Goals[1])
            {
                EndMatch(_report.Goals[0] > _report.Goals[1] ? 0 : 1, "regulation");
                return;
            }

            _phase = MatchPhase.RegulationEnd;
            Emit(EventType.MobStart, "mob");
            Emit(EventType.RefereeLeaves, "refereeLeaves");
            _goldenGoal = true;
            _report.WentToGoldenGoal = true;
            ScheduleKickoff(1);
            return;
        }

        if (_tick < RegulationTicks + _tuning.GoldenGoalMaxTicks)
        {
            return;
        }

        int winner;
        if (_report.ShotsOnTarget[0] != _report.ShotsOnTarget[1])
        {
            winner = _report.ShotsOnTarget[0] > _report.ShotsOnTarget[1] ? 0 : 1;
        }
        else if (_report.PossessionTicks[0] != _report.PossessionTicks[1])
        {
            winner = _report.PossessionTicks[0] > _report.PossessionTicks[1] ? 0 : 1;
        }
        else
        {
            winner = 1;
        }

        EndMatch(winner, "tiebreak");
    }

    private void EndMatch(int winner, string detail)
    {
        EndPlay("lost");
        _phase = MatchPhase.Finished;
        _report.Winner = winner;
        _report.Ticks = _tick;
        Emit(EventType.MatchEnd, detail, team: winner);
    }

    // ---------------------------------------------------------------- eventos y log

    private void Emit(
        EventType type,
        string detail,
        MatchPlayer? actor = null,
        MatchPlayer? target = null,
        MatchPlayer? opponent = null,
        int team = -1)
    {
        int eventTeam = actor is not null ? actor.Team : team;
        Vec2 position = actor is not null && actor.OnPitch ? actor.Position : _ball.Position;
        int reference = eventTeam >= 0 ? eventTeam : 0;

        var matchEvent = new MatchEvent(
            type,
            _tick,
            eventTeam,
            actor is not null ? actor.Id : -1,
            target is not null ? target.Id : -1,
            opponent is not null ? opponent.Id : -1,
            Pitch.CellOf(position),
            Pitch.ZoneOf(position, reference),
            _phase,
            _bias,
            Utility.Centi(Vec2.Distance(position, Pitch.GoalCenter(reference))),
            detail);

        _events.Add(matchEvent);

        if (_config.CollectLog)
        {
            _report.Log.Add(FormatLog(matchEvent, actor, target, opponent));
        }
    }

    private string FormatLog(MatchEvent matchEvent, MatchPlayer? actor, MatchPlayer? target, MatchPlayer? opponent)
    {
        string actorName = actor is not null ? actor.Name : (matchEvent.Team >= 0 ? TeamName(matchEvent.Team) : "match");
        var other = target ?? opponent;
        string otherName = other is not null ? other.Name : string.Empty;
        string verb = Verb(matchEvent.Type);
        string tick = _tick.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        return otherName.Length == 0
            ? $"[t={tick}] {actorName} {verb}: {matchEvent.Detail}"
            : $"[t={tick}] {actorName} {verb} {otherName}: {matchEvent.Detail}";
    }

    private string TeamName(int team) => team == 0 ? _setup.Home.Name : _setup.Away.Name;

    private static string Verb(EventType type) => type switch
    {
        EventType.MatchStart => "starts",
        EventType.MatchEnd => "ends",
        EventType.MobStart => "storms the pitch",
        EventType.RefereeLeaves => "leaves",
        EventType.PlayStart => "opens a play",
        EventType.PlayEnd => "closes a play",
        EventType.PassAttempted => "passes to",
        EventType.PassCompleted => "finds",
        EventType.PassFailed => "loses the pass",
        EventType.DribbleAttempted => "takes on",
        EventType.DribbleWon => "beats",
        EventType.DribbleLost => "is dispossessed by",
        EventType.AerialDuel => "jumps against",
        EventType.Tackle => "tackles",
        EventType.Recovery => "recovers",
        EventType.Shot => "shoots",
        EventType.Goal => "scores",
        EventType.Save => "saves from",
        EventType.Foul => "fouls",
        EventType.Card => "is booked",
        EventType.Injury => "is hurt by",
        EventType.Death => "dies",
        EventType.Substitution => "comes on for",
        EventType.ConsumableUsed => "uses",
        _ => "acts",
    };
}
