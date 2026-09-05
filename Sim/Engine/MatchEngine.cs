using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Sim.Engine;

/// <summary>
/// Motor de partido (docs/fase0-diseno.md §3). Bucle de ticks determinista: un único Pcg32 para todo
/// el partido, recorrido de jugadores por id (ascendente en los ticks pares, descendente en los impares,
/// ver <see cref="PlayerInTurnOrder"/>) y ninguna colección sin orden (RT-020..RT-024).
/// Se construye una vez por partido y se ejecuta con <see cref="Run"/>.
/// </summary>
internal sealed class MatchEngine : IPerkWorld
{
    /// <summary>Distancia del punto de penalti a la línea de gol, en casillas (§3.8).</summary>
    private const float PenaltySpotCells = 2f;

    /// <summary>Radio de recogida de un balón suelto (§3.7).</summary>
    private const float PickupRadius = 0.5f;

    /// <summary>Radio en el que el receptor recoge un pase que llega (§3.7).</summary>
    private const float PassArrivalRadius = 1.0f;

    /// <summary>Radio en el que se dispara un duelo de regate (§3.7).</summary>
    private const float DribbleDuelRadius = 0.8f;

    /// <summary>Margen extra de alcance al resolver una entrada (§3.7).</summary>
    private const float TackleReachMargin = 0.3f;

    /// <summary>Velocidad con la que queda un balón suelto tras un pase fallido (§3.7).</summary>
    private const float LooseBallSpeed = 0.1f;

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
    private readonly EffectEngine? _effects;

    /// <summary>Separación blanda entre cuerpos del tick (ADR 0020); guarda su propio buffer de empuje.</summary>
    private readonly BodySeparation _bodies;

    /// <summary>Buffer de trabajo del marcaje estable; se reutiliza para no asignar por tick (RT-051).</summary>
    private readonly bool[] _markScratch;

    private Pcg32 _rng;
    private int _bias;
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

    /// <summary>True cuando hay que rehacer el emparejamiento de marcaje (cambio de posesión, §2.3).</summary>
    private bool _markingDirty = true;

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
            if (!_players[i].IsOutfield)
            {
                _goalkeepers[_players[i].Team] = _players[i];
            }
        }

        // Bono de Leader (§3.5): suma de los bonos de los compañeros con casilla-hogar contigua. Las
        // casillas-hogar son fijas durante el partido, así que se resuelve una sola vez aquí.
        for (int i = 0; i < _players.Length; i++)
        {
            int bonus = 0;
            for (int j = 0; j < _players.Length; j++)
            {
                if (i == j || _players[j].Team != _players[i].Team || _players[j].AdjacentTeammateBonusPercent == 0)
                {
                    continue;
                }

                if (Pitch.AreAdjacent(_players[i].HomeCell, _players[j].HomeCell))
                {
                    bonus += _players[j].AdjacentTeammateBonusPercent;
                }
            }

            _players[i].LeaderBonusPercent = bonus;
        }

        for (int i = 0; i < _players.Length; i++)
        {
            _players[i].Index = i;
        }

        // El motor de efectos solo existe si hay algo que ejecutar: con 0 perks y 0 habilidades no hay
        // suscripciones, no hay publicaciones y el coste añadido sobre la fase 0 es exactamente cero (§3).
        // La habilidad racial cuenta (ADR 0026, §5.10): no está en Definition.Perks porque no ocupa slot,
        // así que mirando solo esa lista un equipo sin ningún perk asignado se quedaba sin su habilidad
        // de partido. Es el hueco que el paquete S dejó anotado para este.
        // El equipamiento entra por la misma puerta (RF-075..085): un objeto equipado o un consumible
        // también necesitan el motor de efectos, porque sus efectos son los mismos EffectDefinition y los
        // aplican los mismos Modifiers. Con 0 perks, 0 objetos y 0 consumibles no hay motor y no cambia
        // ni un evento respecto a la fase 1 (huella de DeterminismTests).
        bool anyEffects = setup.Home.Consumables.Count > 0 || setup.Away.Consumables.Count > 0;
        for (int i = 0; i < _players.Length && !anyEffects; i++)
        {
            if (_players[i].Definition.Perks.Count > 0
                || _players[i].Definition.Item is not null
                || _catalog.Race(_players[i].Definition.Race).Ability.Length > 0)
            {
                anyEffects = true;
            }
        }

        _effects = anyEffects ? new EffectEngine(this, _players, _report, config.MaxDepth) : null;

        _ball.InterceptAttempted = new bool[_players.Length];
        _bodies = new BodySeparation(_tuning.Bodies, _players.Length);
        _markScratch = new bool[_players.Length];
        _context = new UtilityContext(_players, _ball, catalog.Ai, _tuning.ActionZone);
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

    /// <summary>Catálogo del partido; el motor de efectos lo necesita para resolver los ids de perk.</summary>
    public Catalog Catalog => _catalog;

    /// <summary>Entrada del partido; el motor de efectos lee de ahí los consumibles equipados (RF-080).</summary>
    public MatchSetup Setup => _setup;

    /// <summary>Goles de un equipo ahora mismo; lo consultan los disparadores de consumible (RF-083).</summary>
    public int GoalsOf(int team) => _report.Goals[team];

    /// <summary>Ticks que quedan de tiempo reglamentario; 0 en la prórroga de turba (RF-083, "últimos 20 segundos").</summary>
    public int TicksLeftInRegulation => Math.Max(0, _regulationTicks - _tick);

    /// <summary>Motor de efectos del partido, o null si ningún jugador en campo lleva perks.</summary>
    public EffectEngine? Effects => _effects;

    /// <summary>Informe en construcción; el motor de efectos registra ahí sus activaciones (RT-043).</summary>
    public MatchReportBuilder Report => _report;

    /// <inheritdoc/>
    public int Tick => _tick;

    /// <inheritdoc/>
    public bool IsMob => _goldenGoal;

    private int RegulationTicks => _regulationTicks;

    /// <summary>Ejecuta el partido completo y devuelve eventos e informe (§3.2).</summary>
    public MatchResult Run()
    {
        ResetPositions();
        Marking.Assign(_players, _markScratch, force: true);
        _ball.Park(new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow));
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
        if (_effects is not null)
        {
            _report.PerksSummary.AddRange(_effects.Summary());
        }

        var counterDeltas = _effects is not null
            ? _effects.CounterDeltas()
            : (IReadOnlyList<PlayerCounterDelta>)Array.Empty<PlayerCounterDelta>();

        return new MatchResult(_events, _report.Build(), counterDeltas);
    }

    // ---------------------------------------------------------------- IPerkWorld (§2, funciones NCalc)

    /// <inheritdoc/>
    public int BiasFor(int team) => team == 0 ? _bias : -_bias;

    /// <inheritdoc/>
    public int ScoreDiff(int team) => _report.Goals[team] - _report.Goals[1 - team];

    /// <inheritdoc/>
    public Zone ZoneOf(MatchPlayer player) => Pitch.ZoneOf(player.Position, player.Team);

    /// <inheritdoc/>
    public int DistanceToGoalCells(MatchPlayer player) =>
        (int)MathF.Floor(Vec2.Distance(player.Position, Pitch.GoalCenter(player.Team)));

    /// <inheritdoc/>
    public int AdjacentCount(MatchPlayer player, string tag)
    {
        int count = 0;
        for (int i = 0; i < _players.Length; i++)
        {
            var other = _players[i];
            if (other.Team != player.Team || other.Id == player.Id || !other.OnPitch)
            {
                continue;
            }

            if (Pitch.AreAdjacent(player.HomeCell, other.HomeCell) && other.Definition.HasTag(tag))
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc/>
    public int TeammatesWithTag(MatchPlayer player, string tag)
    {
        int count = 0;
        for (int i = 0; i < _players.Length; i++)
        {
            var other = _players[i];
            if (other.Team == player.Team && other.Id != player.Id && other.OnPitch && other.Definition.HasTag(tag))
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc/>
    public int Counter(MatchPlayer player, string name) => _effects?.Counter(player, name) ?? 0;

    /// <summary>Jugador con ese id, o null (incluido el -1 de "no aplica"). Lo usa el motor de efectos.</summary>
    public MatchPlayer? PlayerById(int id)
    {
        if (id < 0)
        {
            return null;
        }

        for (int i = 0; i < _players.Length; i++)
        {
            if (_players[i].Id == id)
            {
                return _players[i];
            }
        }

        return null;
    }

    /// <summary>Desplaza el criterio del árbitro (efecto modifyBias, §2); positivo favorece al local.</summary>
    public void ApplyBiasDelta(int delta) => _bias = Math.Clamp(_bias + delta, -100, 100);

    /// <summary>Derriba a un jugador (efecto setState, §2). Si llevaba el balón, queda suelto.</summary>
    public void KnockDown(MatchPlayer player, int ticks)
    {
        if (!player.OnPitch || ticks <= 0)
        {
            return;
        }

        if (ReferenceEquals(_ball.Owner, player))
        {
            ParkBall(player.Position);
        }

        player.EnterState(PlayerState.KnockedDown, ticks);
    }

    /// <summary>Modificador de una probabilidad para el jugador; 0 si no hay perks (§2).</summary>
    private int Probability(MatchPlayer player, ProbabilityKind kind) =>
        _effects is null ? 0 : _effects.Modifiers.Probability(player, kind);

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
            players.Add(new MatchPlayer(definition, teamIndex, new Cell(column, slot.HomeCell.Row), _catalog));
        }
    }

    private void Step()
    {
        _tick++;

        // Consumibles condicionales (RF-081..083): se comprueban antes que nada, así que el disparador ve
        // el estado consolidado del tick anterior y lo que conceden vale ya para este tick. Sin
        // consumibles equipados el bucle recorre un array vacío y no toca el orden de nada.
        _effects?.ResolveConsumables();

        UpdateTacticalState();
        UpdateBlockShift();
        UpdateContextCaches();

        // El emparejamiento de marcaje se rehace solo al cambiar la posesión (§2.3); el resto de ticks
        // esta llamada únicamente rellena los huecos que deje un jugador que ha salido del campo.
        Marking.Assign(_players, _markScratch, _markingDirty);
        _markingDirty = false;

        _bodies.BeginTick();

        if (_restartTicksLeft > 0)
        {
            // Con el balón muerto, el enfriamiento de entrada y de duelo de regate siguen bajando tick a
            // tick igual que fuera de una reanudación (revisión independiente, fase 0): antes se congelaban
            // durante Restart/Kickoff/Penalty porque solo se llamaba a TickStateTimer, y un jugador podía
            // salir de la reanudación con un enfriamiento más largo del que tuning.json pedía.
            for (int i = 0; i < _players.Length; i++)
            {
                var player = PlayerInTurnOrder(i);
                TickStateTimer(player);
                if (player.DribbleDuelCooldown > 0)
                {
                    player.DribbleDuelCooldown--;
                }

                if (player.TackleCooldown > 0)
                {
                    player.TackleCooldown--;
                }
            }

            _bodies.Resolve(_players);
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
                UpdatePlayer(PlayerInTurnOrder(i));
            }

            // Separación de cuerpos al final del movimiento y antes de tocar el balón (§2.1): así el
            // balón, que sigue al poseedor, ve ya las posiciones definitivas del tick.
            _bodies.Resolve(_players);

            // Una falta resuelta dentro de este bucle puede haber pedido un penalti (SchedulePenalty ->
            // BeginRestart), que aparca el balón en el punto de penalti y deja _restartTicksLeft > 0 a
            // mitad de este mismo Step. Si se llamara igualmente a UpdateBall/CheckOutOfBounds, el balón
            // aparcado se trataría como suelto y el jugador más cercano lo recogería ese mismo tick, antes
            // de que la reanudación llegue a resolverse (revisión independiente, fase 0).
            if (_restartTicksLeft == 0)
            {
                UpdateBall();
                CheckOutOfBounds();
            }
        }

        CheckForfeit();
        if (_phase == MatchPhase.Finished)
        {
            return;
        }

        AccumulateMetrics();
        CheckEndConditions();
    }

    /// <summary>
    /// Jugador que ocupa la posición i del recorrido de este tick (§3.2). El array está ordenado por id
    /// ascendente; el recorrido lo hace en ese orden en los ticks pares y en orden inverso en los impares.
    /// Con el orden fijo, el equipo cuyos jugadores tienen los ids más bajos resuelve antes las entradas,
    /// los pases y los tiros del mismo tick y ganaba el 53,6% de los partidos espejo (paquete E): la
    /// ventaja no era del local sino del que iba primero en el bucle. Alternar por paridad de tick es
    /// igual de determinista y reproducible, y reparte esa ventaja entre los dos equipos.
    /// </summary>
    private MatchPlayer PlayerInTurnOrder(int i) =>
        (_tick & 1) == 0 ? _players[i] : _players[_players.Length - 1 - i];

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
            _markingDirty = true;
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

        if (player.TackleCooldown > 0)
        {
            player.TackleCooldown--;
        }

        if (player.BlockCooldown > 0)
        {
            player.BlockCooldown--;
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
            case PlayerState.Blocking:
                ResolveBlock(player);
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

        // El jugador solo decide cada tuning.decisionIntervalTicks ticks, desplazado por su propio id
        // ((tick + Id) % decisionIntervalTicks == 0): el tick exacto pedido en --dump-utility casi nunca
        // coincide con uno de sus ticks de decisión. Se captura la PRIMERA decisión de ese jugador en un
        // tick >= el pedido (revisión independiente, fase 0); "_report.UtilityDump is null" sigue
        // garantizando que solo se captura una vez por partido.
        if (_config.DumpUtility is { } dump && dump.PlayerId == player.Id && _tick >= dump.Tick && _report.UtilityDump is null)
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
            case PlayerAction.ShortPass:
            case PlayerAction.LongPass:
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
                // Un jugador no se tira dos veces seguidas: el enfriamiento (§3.5) evita las 75 entradas
                // por partido que salían cuando la utilidad elegía Tackle en cada decisión con el rival
                // cerca, y deja el número de entradas gobernado por un valor de datos (paquete E).
                player.EnterState(PlayerState.Tackling, _tuning.States.TacklingTicks);
                player.TackleCooldown = _tuning.States.TackleCooldownTicks + _tuning.States.TacklingTicks;
                break;
            case PlayerAction.Block:
                // El bloqueo lleva su PROPIO enfriamiento (paquete U). El paquete V lo compartía con el
                // de la entrada para que un jugador no alternara las dos y repartiera golpes cada dos
                // ticks (fase1b-diseno.md §6.8); el precio medido era la mitad de las entradas por
                // partido (4,37 -> 2,31), que es la métrica de RT-056 que estaba más lejos de su rango.
                // Con contadores separados el tope por acción sigue existiendo —cargar dos veces
                // seguidas sigue costando block.cooldownTicks— y disputar el balón deja de pagar por
                // haber cargado.
                player.EnterState(PlayerState.Blocking, _tuning.Block.BlockingTicks);
                player.BlockCooldown = _tuning.Block.CooldownTicks + _tuning.Block.BlockingTicks;
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
        Vec2 target = Utility.ClampToZone(player, player.TargetPoint);
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
            if (!_rng.Chance(InterceptChance(player, passer)))
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

    /// <summary>
    /// Probabilidad de que <paramref name="player"/> intercepte el pase de <paramref name="passer"/>
    /// (§3.7). El término de evasión es la mitad del canal Toque de los elfos (ADR 0026): la resistencia
    /// del <b>pasador</b> a que le lean el pase, que resta a quien intenta interceptarlo.
    /// </summary>
    internal int InterceptChance(MatchPlayer player, MatchPlayer passer)
    {
        // ADR 0041: la técnica del que intercepta se mide contra la del que pasa, no contra el 50 del
        // nivel 1. Lo que decide una intercepción es quién lee mejor a quién, no cuánto han subido los dos.
        var pass = _tuning.Pass;
        return pass.InterceptBaseChance
            + (pass.InterceptTechniqueFactor * (player.Technique - passer.Technique))
            + Probability(player, ProbabilityKind.Intercept)
            - Probability(passer, ProbabilityKind.InterceptEvasion);
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
    /// Si hay un pase en vuelo (no un tiro) pendiente de llegar, lo cierra con <c>PassFailed</c>
    /// Detail "cancelled" antes de que otra cosa se lleve el balón por delante (§3.7): sin este cierre,
    /// un pase que <see cref="ParkBall"/> o <see cref="EndMatch"/> interrumpían a mitad de vuelo se
    /// quedaba sin resolver y el flujo de eventos dejaba de cuadrar
    /// (PASS_ATTEMPTED != PASS_COMPLETED + PASS_FAILED), revisión independiente de fase 0.
    /// </summary>
    private void CancelInFlightPass()
    {
        if (_ball.InFlight && !_ball.IsShot && _ball.Passer is not null)
        {
            Emit(EventType.PassFailed, "cancelled", _ball.Passer);
        }
    }

    /// <summary>
    /// Deja el balón parado en un punto sin dueño. Si había poseedor, sale de Dribbling/Passing/Shooting
    /// para que no siga conduciendo un balón que ya no tiene (§3.6). Si había un pase en vuelo, se cierra
    /// primero con PassFailed "cancelled" (§3.7).
    /// </summary>
    private void ParkBall(Vec2 position)
    {
        var previous = _ball.Owner;
        if (previous is not null && previous.State is PlayerState.Dribbling or PlayerState.Passing or PlayerState.Shooting)
        {
            previous.EnterState(PlayerState.Positioning, 0);
        }

        CancelInFlightPass();
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

            // La asistencia (§3.8) exige que el equipo que remata haya conservado el balón desde el
            // último pase completado: si el rival lo tocó entre medias (robo, intercepción, duelo de
            // regate perdido) y el mismo equipo lo recupera después, el pasador anterior ya no cuenta
            // como asistente aunque el gol llegue dentro de assistWindowTicks (revisión independiente,
            // fase 0). Se limpia en cualquier cambio de equipo poseedor, no solo cuando el rival marca.
            _lastCompletedPasser = null;
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

        // Los modificadores de duración "play" caducan DESPUÉS de publicar PLAY_END (§2): un perk
        // disparado por el fin de jugada aplica sobre la siguiente, no sobre la que acaba de cerrarse.
        _effects?.EndPlay();

        if (_playPasses >= 1)
        {
            _report.PassChains++;
            _report.PassChainTotalLength += _playPasses;

            // Reparto por equipo (paquete I): la métrica buildsWinDifferently de §8 compara la cadena de
            // pases de una build contra la de otra, y una estadística de todo el partido no puede
            // distinguirlas — con ella la cadena de un lote de orcos salía mayor que la de un lote de
            // elfos porque la mide el partido entero, no el equipo. La jugada pertenece al equipo que
            // tenía la posesión (_playTeam), que es exactamente quien encadenó los pases.
            if (_playTeam >= 0)
            {
                _report.PassChainsByTeam[_playTeam]++;
                _report.PassChainTotalLengthByTeam[_playTeam] += _playPasses;
            }
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

        // PASS_ATTEMPTED se emite y publica ANTES del roll de éxito (§3): así un perk de pase (+pass, o
        // +técnica con duración "play") entra en esta misma resolución. Sin perks, el evento sigue
        // ocupando exactamente el mismo lugar en la secuencia y el flujo de _rng no cambia.
        Emit(EventType.PassAttempted, "attempted", passer, receiver);
        passer.PassesAttempted++;

        int pressure = HasOpponentWithin(passer, PitchConstants.PressureRadius) ? 1 : 0;
        int probability = pass.BaseSuccess
            + (pass.TechniqueFactor * (passer.Technique - 50))
            + (passer.PassQualityBonus * 100)
            - (pass.DistancePenaltyPerCell * Utility.Centi(distance) / 100)
            - (pass.PressurePenalty * pressure)
            + Probability(passer, ProbabilityKind.Pass);

        // El roll se consume siempre, haya o no receptor (revisión independiente, fase 0): con
        // "receiver is not null && _rng.Chance(...)" el cortocircuito del && saltaba el Chance() entero
        // cuando no había receptor, así que el número de números que _rng.Next() consume en un pase
        // dependía de si había o no compañero visible. Con esto, el flujo de RNG solo depende de la
        // secuencia de decisiones tomadas, nunca de sus resultados intermedios.
        bool chanceRoll = _rng.Chance(Math.Clamp(probability, 500, 9800));
        bool succeeds = receiver is not null && chanceRoll;
        int ticks = FlightTicks(distance, _tuning.Ball.PassSpeedCellsPerTickMilli);
        Vec2 target = receiver is not null
            ? Utility.ClampToPitch(receiver.Position + (receiver.Velocity * ticks))
            : receiverPoint;

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

        // SHOT se publica antes de calcular calidad, desvío y parada (§3): el evento definitivo, con su
        // Detail real ("onTarget"/"offTarget"), se emite más abajo con publish: false.
        PublishBeforeResolving(EventType.Shot, "attempted", shooter);

        var shot = _tuning.Shot;
        Vec2 goal = Pitch.GoalCenter(shooter.Team);
        float distance = Vec2.Distance(shooter.Position, goal);
        int pressure = isPenalty ? 0 : CountOpponentsWithin(shooter, PitchConstants.PressureRadius);

        int raw = shot.BaseQuality
            + (shot.TechniqueFactor * shooter.Technique)
            + (shot.StrengthFactor * shooter.Strength)
            + (shooter.ShotQualityBonus * 100)
            - (shot.DistancePenaltyPerCell * Utility.Centi(distance) / 100)
            - (shot.PressurePenalty * pressure);
        int quality = Math.Clamp(raw / 100, 5, 95);
        if (isPenalty)
        {
            quality = Math.Clamp(quality + shot.PenaltyQualityBonus, 5, 95);
        }

        bool offTarget = _rng.Chance(shot.OffTargetBase
            + (shot.OffTargetDistanceFactor * Utility.Centi(distance) / 100)
            - (quality * 20)
            - Probability(shooter, ProbabilityKind.ShotOnTarget));

        _report.Shots[shooter.Team]++;
        shooter.Shots++;
        if (!offTarget)
        {
            _report.ShotsOnTarget[shooter.Team]++;
        }

        Emit(EventType.Shot, offTarget ? "offTarget" : "onTarget", shooter, publish: false);
        EndPlay("shot");

        Vec2 target = offTarget
            ? new Vec2(goal.X, shooter.Position.Y < PitchConstants.CenterRow ? 0f : Pitch.Rows)
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
            // ADR 0041: el portero se mide contra el rematador, no contra el 50. El primer término es el
            // duelo (su atributo relevante frente a la técnica del que remata) y el segundo lo que este
            // remate concreto tuvo de bueno o de malo respecto del disparo medio (qualityPivot):
            // distancia, presión y penalti. Queda un resto de deriva por nivel —la calidad del disparo
            // sigue subiendo con los atributos del rematador—, pero es de un punto y pico a nivel 8
            // frente a los cuatro que tenía la fórmula absoluta, y en el sentido contrario.
            int savePercent = Math.Clamp(
                save.BasePercent
                + ((relevant - shooter.Technique) * save.AttributeWeightPercent / 50)
                - ((_ball.ShotQuality - save.QualityPivot) * save.QualityWeight / 100)
                - decay,
                5,
                95);

            if (_rng.Chance(Math.Clamp(
                (savePercent * 100) + Probability(goalkeeper, ProbabilityKind.Save), 0, 10000)))
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
            && _tick - _lastCompletedPassTick < _tuning.AssistWindowTicks)
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

        // El enfriamiento se aplica a los dos duelistas, no solo al conductor (§3.7). Con el enfriamiento
        // solo en el conductor, el defensor que ganaba el balón lo perdía al tick siguiente contra el
        // mismo rival, que seguía a menos de 0,8 casillas: el balón rebotaba entre los dos equipos y
        // producía decenas de cambios de posesión por partido (paquete E).
        carrier.DribbleDuelCooldown = _tuning.States.DribbleDuelCooldownTicks;
        defender.DribbleDuelCooldown = _tuning.States.DribbleDuelCooldownTicks;

        // ADR 0041: la técnica del conductor contra la cobertura del defensor —velocidad y fuerza, con el
        // reparto de defenderSpeedSharePercent—, no las tres contra el 50.
        int guard = ((dribble.DefenderSpeedSharePercent * defender.Speed)
            + ((100 - dribble.DefenderSpeedSharePercent) * defender.Strength)) / 100;
        int win = dribble.BaseWin
            + (dribble.AttackerTechniqueFactor * (carrier.Technique - guard))
            + Probability(carrier, ProbabilityKind.Dribble);

        if (_rng.Chance(win))
        {
            Emit(EventType.DribbleWon, "won", carrier, opponent: defender);
            defender.EnterState(PlayerState.KnockedDown, _tuning.Dribble.LostKnockdownTicks);
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
        if (carrier is null || !carrier.OnPitch || Vec2.Distance(tackler.Position, carrier.Position) > reach)
        {
            // El rival se fue de su alcance antes de que la entrada llegara: no hay contacto ni evento.
            tackler.EnterState(PlayerState.Positioning, 0);
            return;
        }

        // Entrada a destiempo: el rival soltó el balón mientras duraba Tackling. Antes el motor volvía a
        // Positioning en silencio y no pasaba nada; ahora la entrada llega igual y se tira la falta (y la
        // lesión si la hay), pero no cuenta como TACKLE ni puede robar un balón que ya no está: el evento
        // TACKLE sigue siendo una disputa del balón (§3.7, RT-056) y las faltas tardías dejan de ser
        // gratis. Sin esto, un defensor podía tirarse una y otra vez sin coste (paquete E).
        bool carrierHasBall = ReferenceEquals(_ball.Owner, carrier);

        // TACKLE se publica antes de los rolls de falta, victoria y lesión (§3). El evento definitivo,
        // con su Detail real, se emite más abajo (y solo si hubo disputa del balón) con publish: false.
        PublishBeforeResolving(EventType.Tackle, "attempted", tackler, opponent: carrier);

        var tackle = _tuning.Tackle;
        int win = TackleWinChance(tackler, carrier);

        // ADR 0041: la falta también es relativa. Una entrada es sucia cuando el que entra es mucho más
        // fuerte que el que la recibe, no cuando es fuerte en abstracto: contra el 50 fijo, un equipo de
        // nivel 8 cometía faltas todo el rato aunque su rival fuese igual de grande.
        int foulChance = tackle.FoulBase
            + (tackle.FoulStrengthFactor * (tackler.Strength - carrier.Strength))
            + (tackler.FoulChanceBonus * 100)
            + (tackler.HardTackleBonus * 100)
            + BiasRollShift(_tuning.Referee.BiasFoulShiftPer10, tackler.Team)
            + Probability(tackler, ProbabilityKind.Foul);

        bool isFoul = _rng.Chance(foulChance);
        bool isWin = _rng.Chance(win) && carrierHasBall;

        if (carrierHasBall)
        {
            _report.Tackles++;
            tackler.Tackles++;
            Emit(EventType.Tackle, isFoul ? "foul" : (isWin ? "won" : "missed"), tackler, opponent: carrier, publish: false);
        }

        if (isFoul)
        {
            ResolveFoul(tackler, carrier);
        }
        else if (isWin)
        {
            tackler.TacklesWon++;
            carrier.EnterState(PlayerState.KnockedDown, KnockdownTicksCausedBy(tackler, _tuning.States.KnockedDownTicks));
            SetOwner(tackler);
            Emit(EventType.Recovery, "tackle", tackler);
        }
        else
        {
            tackler.EnterState(PlayerState.KnockedDown, _tuning.States.KnockedDownTicks / 2);
        }

        // Sin balón y sin falta el que entra se retiró a tiempo: no hay contacto y no se tira lesión.
        if (carrierHasBall || isFoul)
        {
            _bodies.AddTacklePush(tackler, carrier);
            ResolveInjury(tackler, carrier, isFoul);
        }
    }

    /// <summary>
    /// Probabilidad de que la entrada de <paramref name="tackler"/> le quite el balón a
    /// <paramref name="carrier"/> (§3.7). El término de evasión es la otra mitad del canal Toque de los
    /// elfos (ADR 0026): la resistencia del <b>conductor</b> a que le roben, que resta a quien entra.
    /// </summary>
    internal int TackleWinChance(MatchPlayer tackler, MatchPlayer carrier)
    {
        // ADR 0041: la presión del que entra —fuerza y velocidad, con el reparto que fija
        // strengthSharePercent— se mide contra la técnica del conductor, no los tres contra el 50.
        // Subir de nivel a los dos no cambia nada; lo que cambia el duelo es la diferencia.
        var tackle = _tuning.Tackle;
        int pressure = ((tackle.StrengthSharePercent * tackler.Strength)
            + ((100 - tackle.StrengthSharePercent) * tackler.Speed)) / 100;
        return tackle.BaseWin
            + (tackle.PressureFactor * (pressure - carrier.Technique))
            + Probability(tackler, ProbabilityKind.Tackle)
            - Probability(carrier, ProbabilityKind.TackleEvasion);
    }

    /// <summary>
    /// Ticks que dura el derribo que <paramref name="causer"/> le provoca a un <b>rival</b>: la duración
    /// base de la acción más el canal <c>modifyKnockdownTicks</c> (Sangre caliente de los orcos, ADR 0026).
    /// Se aplica solo a los derribos que el jugador causa a otro —entrada ganada y bloqueo—, nunca a las
    /// veces que se cae él mismo al fallar o al cometer falta: la habilidad se describe como "sus entradas
    /// dejan al rival derribado más tiempo" (§5.14) y un jugador no puede llevarse un castigo que su
    /// descripción no anuncia (RF-012d).
    /// </summary>
    internal int KnockdownTicksCausedBy(MatchPlayer causer, int baseTicks) =>
        baseTicks + (_effects is null ? 0 : _effects.Modifiers.KnockdownTicks(causer));

    /// <summary>
    /// Efecto del criterio del árbitro sobre una tirada que juzga a <paramref name="team"/> (RF-064):
    /// <paramref name="perTen"/> puntos base 10.000 por cada 10 puntos de criterio, a favor del equipo al
    /// que el criterio favorece (positivo = local, RF-062).
    ///
    /// <para>División simétrica explícita (revisión independiente, fase 0): <c>Math.DivRem</c> trunca
    /// hacia cero, igual que el operador "/" desnudo, así que el resultado no cambia. Se deja explícito
    /// porque es la propiedad que hace correcto invertir el signo para el equipo 1: con truncamiento hacia
    /// cero, <c>-(a/b) == (-a)/b</c> siempre, así que la magnitud es idéntica para criterio positivo y
    /// negativo y para los dos equipos. Con un floor "hacia -infinito" esa igualdad se rompe y el sesgo
    /// dejaría de ser simétrico.</para>
    /// </summary>
    private int BiasRollShift(int perTen, int team)
    {
        int shift = -Math.DivRem(perTen * _bias, 10, out _);
        return team == 1 ? -shift : shift;
    }

    /// <summary>
    /// Desplaza el criterio <b>en contra</b> del equipo que cometió la acción sucia (RF-063). Se llama
    /// tanto si el árbitro señala como si no: "el árbitro toma nota aunque no pite". La magnitud la fija
    /// la gravedad, sumando los <c>biasShift...</c> de <c>tuning.referee</c>.
    /// </summary>
    private void ShiftBiasAgainst(int team, int points)
    {
        if (points <= 0 || team < 0)
        {
            return;
        }

        _bias = Math.Clamp(_bias + (team == 0 ? -points : points), -100, 100);
    }

    /// <summary>
    /// Falta señalada por el árbitro. <paramref name="offBall"/> distingue el bloqueo sin balón
    /// (ADR 0030 §2), que es más grave a ojos del árbitro que una entrada al que lleva el balón.
    ///
    /// <para>Orden dentro de la falta: primero se mueve el criterio (RF-063) y después se tiran las
    /// tarjetas (RF-064), no al revés. Es la cadena causal legible —el árbitro se enfada y por eso saca la
    /// tarjeta— y hace que la falta que colma el vaso sea la que se castiga, no la siguiente.</para>
    /// </summary>
    private void ResolveFoul(MatchPlayer tackler, MatchPlayer carrier, bool offBall = false)
    {
        var tackle = _tuning.Tackle;
        var referee = _tuning.Referee;
        _report.Fouls++;
        tackler.Fouls++;

        int offBallExtra = offBall ? referee.BiasShiftBlockExtra : 0;

        // La falta se publica antes de su consecuencia (§3): un perk puede anularla (cancelEvent). La
        // falta sigue registrada en el informe y en la secuencia de eventos -ocurrió-, pero el árbitro no
        // la castiga: ni derribo, ni tarjeta, ni penalti. El criterio sí se mueve, con la magnitud de una
        // acción sucia no señalada: el árbitro la vio ocurrir aunque no la castigue (RF-063).
        if (EmitCancellable(EventType.Foul, "foul", tackler, opponent: carrier))
        {
            ShiftBiasAgainst(tackler.Team, referee.BiasShiftFoulUnseen + offBallExtra);
            return;
        }

        bool inOwnArea = Pitch.IsInArea(tackler.Position, tackler.Team);
        tackler.EnterState(PlayerState.KnockedDown, _tuning.States.KnockedDownTicks);

        bool hard = tackler.HasTrait(Trait.Aggressive)
            || tackler.HasTrait(Trait.Dirty)
            || (tackler.Strength - carrier.Strength) * 100 >= tackle.HardTackleThreshold;

        ShiftBiasAgainst(
            tackler.Team,
            referee.BiasShiftFoulSeen + offBallExtra + (hard ? referee.BiasShiftHardExtra : 0));

        int cardShift = Probability(tackler, ProbabilityKind.Card)
            + BiasRollShift(referee.BiasCardShiftPer10, tackler.Team);
        if (_rng.Chance(tackle.RedCardBase + (hard ? tackle.HardTackleRedBonus : 0) + cardShift))
        {
            SendOff(tackler);
            ShiftBiasAgainst(tackler.Team, referee.BiasShiftRedExtra);
        }
        else if (_rng.Chance(tackle.YellowCardBase + (hard ? tackle.HardTackleYellowBonus : 0) + cardShift))
        {
            if (!EmitCancellable(EventType.Card, "yellow", tackler))
            {
                tackler.YellowCards++;
                tackler.Cards++;
                _report.YellowCards++;
                ShiftBiasAgainst(tackler.Team, referee.BiasShiftYellowExtra);
                if (tackler.YellowCards >= 2 && tackle.SecondYellowIsRed)
                {
                    SendOff(tackler);
                    ShiftBiasAgainst(tackler.Team, referee.BiasShiftRedExtra);
                }
            }
        }

        if (inOwnArea && _rng.Chance(
            referee.PenaltyOnFoulInArea + BiasRollShift(referee.BiasPenaltyShiftPer10, tackler.Team)))
        {
            SchedulePenalty(carrier.Team);
        }
    }

    /// <summary>
    /// Bloqueo sin balón (ADR 0030 §2): la carga contra un rival que no lleva el balón, decidida por
    /// <see cref="Utility"/> con el criterio de jugada activa de RF-057 ya aplicado. Es el gemelo sin
    /// balón de <see cref="ResolveTackle"/> y sigue su mismo orden de resolución: comprobación de alcance,
    /// publicación previa del evento, tirada de falta, tirada de derribo, empuje y lesión.
    ///
    /// <para>Reutiliza el tipo de evento <c>TACKLE</c> con Detail propio (<c>block</c>, <c>blockFoul</c>,
    /// <c>blockMissed</c>) y los canales de probabilidad de la entrada: un bloqueo <b>es</b> una entrada
    /// sin balón, y separar el tipo de evento habría dejado a todos los perks de contacto ciegos ante la
    /// mitad de los golpes del partido. Lo que <b>no</b> hace es contar en <c>report.Tackles</c>: esa
    /// métrica de RT-056 mide disputas del balón y un bloqueo no lo es. Los bloqueos se cuentan aparte, en
    /// <c>report.Blocks</c>.</para>
    /// </summary>
    private void ResolveBlock(MatchPlayer blocker)
    {
        var target = blocker.BlockTarget;
        blocker.BlockTarget = null;
        var block = _tuning.Block;

        if (target is null || !target.OnPitch || ReferenceEquals(_ball.Owner, target)
            || Vec2.Distance(blocker.Position, target.Position) > _catalog.Ai.Context.BlockReachMaxCells + TackleReachMargin)
        {
            // El rival se fue, salió del campo o pasó a llevar el balón (y entonces esto sería una entrada,
            // no un bloqueo): no hay contacto ni evento.
            blocker.EnterState(PlayerState.Positioning, 0);
            return;
        }

        PublishBeforeResolving(EventType.Tackle, "block", blocker, opponent: target);

        int foulChance = block.FoulBase
            + (blocker.FoulChanceBonus * 100)
            + BiasRollShift(_tuning.Referee.BiasFoulShiftPer10, blocker.Team)
            + Probability(blocker, ProbabilityKind.Foul);

        int win = block.BaseWin
            + (block.StrengthFactor * (blocker.Strength - target.Strength))
            + (block.SpeedFactor * (blocker.Speed - target.Speed))
            + Probability(blocker, ProbabilityKind.Tackle);

        bool isFoul = _rng.Chance(foulChance);
        bool isWin = _rng.Chance(win);

        _report.Blocks++;
        Emit(
            EventType.Tackle,
            isFoul ? "blockFoul" : (isWin ? "block" : "blockMissed"),
            blocker,
            opponent: target,
            publish: false);

        if (isWin)
        {
            target.EnterState(PlayerState.KnockedDown, KnockdownTicksCausedBy(blocker, block.KnockdownTicks));
        }

        if (isFoul)
        {
            ResolveFoul(blocker, target, offBall: true);
        }
        else
        {
            // Falta que el árbitro no ve, que es la razón de ser de RF-063: el criterio se mueve igual.
            ShiftBiasAgainst(
                blocker.Team,
                _tuning.Referee.BiasShiftFoulUnseen + _tuning.Referee.BiasShiftBlockExtra);
            blocker.EnterState(PlayerState.Positioning, 0);
        }

        // Mismo criterio que la entrada: sin derribo y sin falta el que carga se frenó a tiempo, no hubo
        // contacto y no se tira lesión. Un bloqueo que no tumba a nadie y que el árbitro no ve es una
        // carga que se quedó corta, no un golpe gratis.
        if (isWin || isFoul)
        {
            _bodies.AddTacklePush(blocker, target);
            ResolveInjury(blocker, target, isFoul);
        }
    }

    private void SendOff(MatchPlayer player)
    {
        // La roja se publica antes de expulsar (§3): un perk rompe-reglas puede anularla y el jugador se
        // queda en el campo, con el evento CARD registrado igual y Detail "red:cancelled".
        if (EmitCancellable(EventType.Card, "red", player))
        {
            return;
        }

        player.Cards++;
        _report.RedCards++;
        if (ReferenceEquals(_ball.Owner, player))
        {
            ParkBall(player.Position);
        }

        player.LeavePitch(PlayerState.SentOff);
    }

    private void ResolveInjury(MatchPlayer tackler, MatchPlayer victim, bool isFoul)
    {
        // ADR 0041: la fuerza del que entra contra la resistencia del que la recibe, sin ninguna
        // constante de por medio. Un nivel 8 que entra a otro nivel 8 lesiona aproximadamente lo mismo
        // que un nivel 1 contra otro nivel 1; lo que mueve el riesgo es la diferencia entre los dos.
        var injury = _tuning.Injury;
        int chance = injury.OnTackleBase
            + (isFoul ? injury.OnFoulBase : 0)
            + (injury.RelativeFactor * (tackler.Strength - victim.Stamina))
            + (tackler.InjuryChanceBonus * 100)
            - (victim.InjuryResistanceBonus * 100)
            + Probability(tackler, ProbabilityKind.Injure)
            + Probability(victim, ProbabilityKind.Injury);

        // ADR 0043: el desgaste crece por acto y en el nodo de élite. Es un multiplicador sobre la
        // probabilidad ya calculada, no un término de la fórmula: en un partido suelto vale 100 y no
        // cambia nada (SimConfig.InjuryScalePercent).
        chance = Math.Clamp(chance, 0, 5000) * _config.InjuryScalePercent / 100;

        if (!_rng.Chance(Math.Clamp(chance, 0, 5000)))
        {
            return;
        }

        bool severe = _rng.Chance(injury.SevereShare + Probability(victim, ProbabilityKind.SevereInjury));

        // La lesión se publica antes de aplicarla (§3): un perk puede anularla y el jugador sigue en el
        // campo, con el evento INJURY registrado y Detail sufijado ":cancelled".
        if (EmitCancellable(EventType.Injury, severe ? "severe" : "minor", victim, opponent: tackler))
        {
            return;
        }

        _report.Injuries++;
        victim.Injured = true;

        // Lesionar a un rival mueve el criterio en contra aunque no haya habido falta (RF-063): es la
        // acción sucia más visible que existe y el árbitro toma nota igual.
        ShiftBiasAgainst(tackler.Team, _tuning.Referee.BiasShiftInjuryExtra);

        if (ReferenceEquals(_ball.Owner, victim))
        {
            ParkBall(victim.Position);
        }

        victim.LeavePitch(PlayerState.Injured);

        // RF-093 vía 1: quien salió al campo arrastrando una lesión grave sin tratar y vuelve a
        // lesionarse, muere. Es la única consecuencia de alinearlo, y estaba anunciada antes de
        // confirmar la alineación (RunEngine.LineupWarnings, RF-012d).
        if (victim.Definition.PhysicalState == PhysicalState.SevereInjury)
        {
            Kill(victim, "severeInjury");
        }
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
            float cornerY = position.Y < PitchConstants.CenterRow ? 0f : Pitch.Rows;
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
        BeginRestart(RestartKind.Kickoff, team, new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow), _tuning.Restart.KickoffTicks, MatchPhase.Kickoff);
    }

    private void SchedulePenalty(int team)
    {
        int direction = Pitch.AttackDirection(team);
        Vec2 goal = Pitch.GoalCenter(team);
        var point = new Vec2(goal.X - (PenaltySpotCells * direction), PitchConstants.CenterRow);

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
        CancelPendingTackles();
        EndPlay("lost");
    }

    /// <summary>
    /// Cancela cualquier entrada o bloqueo en curso (Tackling/Blocking) al empezar una reanudación
    /// (§3.6, §3.8, ADR 0030 §2), igual que
    /// <see cref="ParkBall"/> ya sacaba al dueño de Dribbling/Passing/Shooting: con el balón muerto durante
    /// Restart/Kickoff/Penalty no debe resolverse una entrada que estaba a mitad de TacklingTicks
    /// (revisión independiente, fase 0). El que entraba vuelve a Positioning sin evento, igual que cuando
    /// el objetivo se sale de alcance por su cuenta (ResolveTackle).
    /// </summary>
    private void CancelPendingTackles()
    {
        for (int i = 0; i < _players.Length; i++)
        {
            var player = _players[i];
            if (player.State is PlayerState.Tackling or PlayerState.Blocking)
            {
                player.EnterState(PlayerState.Positioning, 0);
            }
        }
    }

    private void ResolveRestart()
    {
        var kind = _pendingRestart;
        _pendingRestart = RestartKind.None;

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

        // La fase vuelve a OpenPlay/MobGoldenGoal DESPUÉS del switch (revisión independiente, fase 0):
        // TakeRestart/TakeGoalKick/TakeKickoff emiten su Recovery mientras el saque se resuelve, y ese
        // evento debe llevar Phase = Restart/Kickoff/Penalty, no la fase de juego abierto en la que el
        // motor entra justo después. Si TakePenalty no encuentra tirador, llama a ScheduleGoalKick, que
        // abre una reanudación nueva (BeginRestart dentro del propio switch) y dejará _restartTicksLeft
        // > 0: en ese caso no se toca _phase aquí, porque BeginRestart ya la puso en Restart.
        if (_restartTicksLeft == 0)
        {
            _phase = _goldenGoal ? MatchPhase.MobGoldenGoal : MatchPhase.OpenPlay;
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

        var center = new Vec2(Pitch.Columns / 2f, PitchConstants.CenterRow);
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
        if (_phase == MatchPhase.Finished)
        {
            // El partido ya terminó en este mismo Step (por ejemplo, EndConditions o una incomparecencia
            // resuelta antes en la cadena); comprobar de nuevo emitiría un segundo MATCH_END (revisión
            // independiente, fase 0).
            return;
        }

        int home = CountOnPitch(0);
        int away = CountOnPitch(1);
        if (home >= 5 && away >= 5)
        {
            return;
        }

        _report.Forfeit = true;

        // Incomparecencia simultánea de los dos equipos en el mismo tick (§3.8, §3.9): gana el que tenga
        // más jugadores en campo; si empatan (incluido 0 a 0, ambos equipos vaciados el mismo tick), se
        // aplica la misma cadena de desempate que el gol de oro agotado (más tiros a puerta, más ticks de
        // posesión, visitante). El Detail sigue siendo "forfeit": para el informe es una incomparecencia,
        // no un desempate de partido completo.
        int winner;
        if (home < 5 && away < 5)
        {
            winner = home != away ? (home > away ? 0 : 1) : TiebreakWinner();
        }
        else
        {
            winner = home < 5 ? 1 : 0;
        }

        EndMatch(winner, "forfeit");
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
            _effects?.StartMob();
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

        EndMatch(TiebreakWinner(), "tiebreak");
    }

    /// <summary>
    /// Cadena de desempate de §3.9: más tiros a puerta, si no más ticks de posesión, si no el visitante
    /// (equipo 1). Se usa al agotar el gol de oro y, desde la revisión independiente de fase 0, también
    /// en la incomparecencia simultánea de los dos equipos (§3.8), que necesita el mismo criterio.
    /// </summary>
    private int TiebreakWinner()
    {
        if (_report.ShotsOnTarget[0] != _report.ShotsOnTarget[1])
        {
            return _report.ShotsOnTarget[0] > _report.ShotsOnTarget[1] ? 0 : 1;
        }

        if (_report.PossessionTicks[0] != _report.PossessionTicks[1])
        {
            return _report.PossessionTicks[0] > _report.PossessionTicks[1] ? 0 : 1;
        }

        return 1;
    }

    private void EndMatch(int winner, string detail)
    {
        if (_phase == MatchPhase.Finished)
        {
            // Ya se cerró el partido en este mismo Step (por ejemplo, CheckEndConditions seguido de
            // CheckForfeit sobre el mismo tick): un segundo MATCH_END sería un evento fantasma
            // (revisión independiente, fase 0).
            return;
        }

        CancelInFlightPass();
        EndPlay("lost");
        _phase = MatchPhase.Finished;
        _report.Winner = winner;
        _report.Ticks = _tick;
        Emit(EventType.MatchEnd, detail, team: winner);
    }

    // ---------------------------------------------------------------- eventos y log

    private MatchEvent BuildEvent(
        EventType type,
        string detail,
        MatchPlayer? actor,
        MatchPlayer? target,
        MatchPlayer? opponent,
        int team)
    {
        int eventTeam = actor is not null ? actor.Team : team;
        Vec2 position = actor is not null && actor.OnPitch ? actor.Position : _ball.Position;
        int reference = eventTeam >= 0 ? eventTeam : 0;

        return new MatchEvent(
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
    }

    /// <summary>
    /// Muerte de un jugador (RF-093). <b>Solo</b> se llama desde las dos vías del requisito: un titular
    /// que se alineó con lesión grave sin tratar y vuelve a lesionarse (<see cref="ResolveInjury"/>), y
    /// un perk rival marcado como letal sobre alguien que ya no estaba sano (<c>EffectEngine</c>). No hay
    /// una tercera, y por eso "un jugador sano nunca muere" es una propiedad del sistema.
    /// <para>El jugador sale del campo con <c>PlayerState.Injured</c>: el motor no necesita un estado
    /// propio para el muerto —lo que importa es que ya no juega— y el evento DEATH es lo que
    /// <c>Sim.Run.MatchResolution</c> lee para pasarlo a <c>PhysicalState.Dead</c> en la plantilla.</para>
    /// </summary>
    internal void Kill(MatchPlayer victim, string detail)
    {
        if (victim.Dead)
        {
            return;
        }

        victim.Dead = true;
        victim.Injured = true;
        _report.Deaths++;

        if (ReferenceEquals(_ball.Owner, victim))
        {
            ParkBall(victim.Position);
        }

        if (victim.OnPitch)
        {
            victim.LeavePitch(PlayerState.Injured);
        }

        Emit(EventType.Death, detail, victim);
    }

    /// <summary>Registra el uso de un consumible en la secuencia de eventos (RF-080..085, RF-066).</summary>
    internal void EmitConsumableUsed(MatchConsumable consumable, int team) =>
        Emit(EventType.ConsumableUsed, consumable.Id, actor: null, team: team);

    /// <summary>
    /// Registra un evento en la secuencia y, salvo que se indique lo contrario, lo publica al motor de
    /// efectos (RT-040). <paramref name="publish"/> es false en los eventos cuya publicación ya se hizo
    /// antes de resolverlos (SHOT, TACKLE) o que se publican con la variante cancelable.
    /// </summary>
    private void Emit(
        EventType type,
        string detail,
        MatchPlayer? actor = null,
        MatchPlayer? target = null,
        MatchPlayer? opponent = null,
        int team = -1,
        bool publish = true)
    {
        var matchEvent = BuildEvent(type, detail, actor, target, opponent, team);
        _events.Add(matchEvent);

        if (_config.CollectLog)
        {
            _report.Log.Add(FormatLog(matchEvent, actor, target, opponent));
        }

        if (publish)
        {
            _effects?.Publish(matchEvent);
        }
    }

    /// <summary>
    /// Publica un evento **antes** de resolverlo, sin registrarlo todavía (§3, semántica pre-resolución):
    /// así un perk disparado por SHOT o TACKLE puede modificar atributos y probabilidades de la propia
    /// resolución que ese evento gobierna. El evento definitivo se emite después con su Detail real y con
    /// <c>publish: false</c>, para que la secuencia de eventos siga teniendo exactamente una entrada.
    /// </summary>
    private void PublishBeforeResolving(
        EventType type, string detail, MatchPlayer? actor, MatchPlayer? target = null, MatchPlayer? opponent = null)
    {
        if (_effects is null)
        {
            return;
        }

        _effects.Publish(BuildEvent(type, detail, actor, target, opponent, -1));
    }

    /// <summary>
    /// Emite un evento cuya consecuencia un perk puede anular (§2, cancelEvent; solo FOUL, CARD e
    /// INJURY). Publica primero, registra después: el evento queda siempre en la secuencia, con el Detail
    /// sufijado ":cancelled" si algún perk lo anuló. Devuelve true si se anuló.
    /// </summary>
    private bool EmitCancellable(
        EventType type, string detail, MatchPlayer? actor = null, MatchPlayer? target = null, MatchPlayer? opponent = null)
    {
        if (_effects is null)
        {
            Emit(type, detail, actor, target, opponent, publish: false);
            return false;
        }

        bool cancelled = !_effects.Publish(BuildEvent(type, detail, actor, target, opponent, -1));
        Emit(type, cancelled ? detail + ":cancelled" : detail, actor, target, opponent, publish: false);
        return cancelled;
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
