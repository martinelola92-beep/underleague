using Underleague.Sim.Model;

namespace Underleague.Sim.Engine;

/// <summary>
/// Datos <b>fijos</b> de una de las 20 fichas de la traza: lo que no cambia en todo el partido. Se
/// guarda una sola vez, fuera de los arrays por tick.
/// </summary>
/// <param name="Id">Identificador global del jugador (el mismo de <c>MatchEvent.Actor</c>).</param>
/// <param name="Team">0 local, 1 visitante.</param>
/// <param name="Role">Posición nominal; la espacial es la de cada tick.</param>
/// <param name="Name">Nombre visible, para la ficha seleccionada.</param>
/// <param name="Initials">Hasta dos iniciales del nombre, para cuando el dorsal no basta.</param>
/// <param name="Number">
/// Dorsal 1..N dentro de su equipo. <b>No existe en el modelo</b> (un jugador de la run no tiene número):
/// lo asigna la traza ordenando el equipo por posición y después por id, así que el portero siempre es el
/// 1 y el mismo jugador lleva el mismo dorsal en cada reproducción del mismo partido (RT-021).
/// </param>
public sealed record TracePlayer(int Id, int Team, Position Role, string Name, string Initials, int Number);

/// <summary>
/// Zona de acción de un jugador en un tick (ADR 0028, ADR 0029), en casillas y ya en coordenadas
/// absolutas de campo: la casilla-hogar <b>efectiva</b> —que el bloque táctico mueve cada tick— y las
/// tres extensiones de la región blanda. <see cref="Unlimited"/> en una extensión significa que esa
/// dirección no tiene tope y el borde del campo es el único límite.
/// <para>
/// <see cref="Direction"/> es el sentido de ataque del equipo (+1 o -1): las extensiones están en el
/// marco local del jugador, así que quien pinte el rectángulo tiene que multiplicar el eje X por él.
/// </para>
/// </summary>
public readonly record struct TraceZone(Vec2 Home, int Direction, float Forward, float Back, float Sides)
{
    /// <summary>Extensión sin tope (el -1 de <c>tuning.actionZone.shape</c>).</summary>
    public const float Unlimited = -1f;
}

/// <summary>
/// Traza completa de un partido: por cada tick, la posición y el estado de los 20 jugadores, la del
/// balón, quién lo lleva y qué eventos se dispararon (RT-098 hace lo mismo con la tabla de utilidad de
/// <b>un</b> jugador en <b>un</b> tick; esto es el equivalente espacial, del partido entero).
///
/// <para><b>Es opcional y cuesta cero cuando está apagada</b>: solo existe si
/// <c>SimConfig.Trace</c> es true, que es como lo pide la pantalla de Partido y como <b>no</b> lo piden
/// ni <c>/Balance</c> ni <c>RunEngine</c>. Con la traza apagada el motor no asigna un solo byte por este
/// fichero.</para>
///
/// <para><b>Datos puros</b> (RT-011, RT-012): sin Godot, sin ficheros y sin reloj. Nada de lo que hay
/// aquí se le devuelve al motor, así que la traza no puede cambiar el resultado del partido (RT-024).</para>
///
/// <para><b>Sin submuestrear</b>: hay un fotograma por tick lógico, 1.200 de reglamento y hasta 2.100 con
/// muerte súbita. Un partido completo ocupa del orden de dos megas, que es lo que cuesta poder ver el
/// comportamiento en vez de un resumen.</para>
///
/// <para>El almacenamiento son arrays planos indexados por <c>fotograma * jugadores + jugador</c>, no un
/// objeto por tick: 2.100 objetos con tres arrays cada uno serían 6.000 asignaciones para dibujar
/// círculos.</para>
/// </summary>
public sealed class MatchTrace
{
    private readonly int[] _tick;
    private readonly byte[] _phase;
    private readonly float[] _ballX;
    private readonly float[] _ballY;
    private readonly int[] _ballOwner;
    private readonly bool[] _ballInFlight;

    /// <summary>Número de eventos emitidos <b>hasta el final</b> de cada fotograma (acumulado).</summary>
    private readonly int[] _eventsUpTo;

    private readonly float[] _x;
    private readonly float[] _y;
    private readonly byte[] _state;
    private readonly bool[] _onPitch;
    private readonly float[] _homeX;
    private readonly float[] _homeY;
    private readonly float[] _zoneForward;
    private readonly float[] _zoneBack;
    private readonly float[] _zoneSides;
    private readonly int[] _markTarget;
    private readonly byte[] _action;
    private readonly float[] _targetX;
    private readonly float[] _targetY;

    internal MatchTrace(
        TracePlayer[] players,
        int regulationTicks,
        int[] tick,
        byte[] phase,
        float[] ballX,
        float[] ballY,
        int[] ballOwner,
        bool[] ballInFlight,
        int[] eventsUpTo,
        float[] x,
        float[] y,
        byte[] state,
        bool[] onPitch,
        float[] homeX,
        float[] homeY,
        float[] zoneForward,
        float[] zoneBack,
        float[] zoneSides,
        int[] markTarget,
        byte[] action,
        float[] targetX,
        float[] targetY)
    {
        Players = players;
        RegulationTicks = regulationTicks;
        FrameCount = tick.Length;
        _tick = tick;
        _phase = phase;
        _ballX = ballX;
        _ballY = ballY;
        _ballOwner = ballOwner;
        _ballInFlight = ballInFlight;
        _eventsUpTo = eventsUpTo;
        _x = x;
        _y = y;
        _state = state;
        _onPitch = onPitch;
        _homeX = homeX;
        _homeY = homeY;
        _zoneForward = zoneForward;
        _zoneBack = zoneBack;
        _zoneSides = zoneSides;
        _markTarget = markTarget;
        _action = action;
        _targetX = targetX;
        _targetY = targetY;
    }

    /// <summary>Las 20 fichas, ordenadas por id ascendente igual que el motor (RT-041).</summary>
    public IReadOnlyList<TracePlayer> Players { get; }

    /// <summary>Fotogramas de la traza: uno por tick jugado, sin huecos ni submuestreo.</summary>
    public int FrameCount { get; }

    /// <summary>Ticks del tiempo reglamentario, para pasar de tick a minuto (<see cref="MinuteAt"/>).</summary>
    public int RegulationTicks { get; }

    /// <summary>Tick lógico del fotograma (RT-020). Es 1 en el primero: el tick 0 es el saque.</summary>
    public int TickAt(int frame) => _tick[frame];

    /// <summary>Minuto de 0..90 (y más en la prórroga de turba) del fotograma; el jugador lee minutos.</summary>
    public int MinuteAt(int frame) => RegulationTicks <= 0 ? 0 : _tick[frame] * 90 / RegulationTicks;

    /// <summary>Fase del partido en el fotograma (saque, juego, reanudación, penalti, turba).</summary>
    public MatchPhase PhaseAt(int frame) => (MatchPhase)_phase[frame];

    /// <summary>Posición continua del balón, en casillas.</summary>
    public Vec2 BallAt(int frame) => new(_ballX[frame], _ballY[frame]);

    /// <summary>Índice en <see cref="Players"/> de quien lleva el balón; -1 si está suelto o en vuelo.</summary>
    public int BallOwnerAt(int frame) => _ballOwner[frame];

    /// <summary>True si el balón viaja (pase o tiro): no lo lleva nadie pero tampoco está parado.</summary>
    public bool BallInFlightAt(int frame) => _ballInFlight[frame];

    /// <summary>
    /// Primer evento de <c>MatchResult.Events</c> disparado en este fotograma. Junto a
    /// <see cref="EventCountAt"/> delimita el tramo <c>[desde, desde+cuántos)</c> de la secuencia: la
    /// traza no copia los eventos, los referencia.
    /// </summary>
    public int EventFromAt(int frame) => frame == 0 ? 0 : _eventsUpTo[frame - 1];

    /// <summary>Cuántos eventos se dispararon en este fotograma; 0 en la inmensa mayoría de ticks.</summary>
    public int EventCountAt(int frame) => _eventsUpTo[frame] - EventFromAt(frame);

    /// <summary>Posición continua del jugador, en casillas.</summary>
    public Vec2 PositionAt(int frame, int player) => new(_x[Slot(frame, player)], _y[Slot(frame, player)]);

    /// <summary>Estado de la máquina de estados del jugador en ese tick (RT-089c).</summary>
    public PlayerState StateAt(int frame, int player) => (PlayerState)_state[Slot(frame, player)];

    /// <summary>False si el jugador ya no está en el campo (expulsado, lesionado o muerto).</summary>
    public bool OnPitchAt(int frame, int player) => _onPitch[Slot(frame, player)];

    /// <summary>Zona de acción del jugador en ese tick (ADR 0028): la correa que se puede pintar encima.</summary>
    public TraceZone ZoneAt(int frame, int player)
    {
        int slot = Slot(frame, player);
        return new TraceZone(
            new Vec2(_homeX[slot], _homeY[slot]),
            Players[player].Team == 0 ? 1 : -1,
            _zoneForward[slot],
            _zoneBack[slot],
            _zoneSides[slot]);
    }

    /// <summary>
    /// Índice en <see cref="Players"/> del rival al que este jugador tiene <b>asignado</b> marcar en ese
    /// tick, o -1 si no tiene asignación (portero, jugador fuera del campo, o antes de la primera
    /// posesión). Es la asignación estable de <c>Marking</c> (ADR 0022), que dura toda la posesión: el
    /// jugador solo se mueve hacia ella cuando además su acción es <see cref="PlayerAction.MarkOpponent"/>
    /// (<see cref="ActionAt"/>), y por eso las dos cosas se graban por separado.
    /// </summary>
    public int MarkTargetAt(int frame, int player) => _markTarget[Slot(frame, player)];

    /// <summary>
    /// Acción que la tabla de utilidad eligió para este jugador en su última decisión (RT-098), o null si
    /// todavía no ha decidido nada. <b>No cambia cada tick</b>: se decide una vez cada
    /// <c>tuning.decisionIntervalTicks</c> y se mantiene, así que esto es "lo que está haciendo", no "lo
    /// que ha elegido en este tick". Es lo que distingue las cinco maneras distintas de estar
    /// <see cref="PlayerState.Positioning"/>.
    /// </summary>
    public PlayerAction? ActionAt(int frame, int player)
    {
        byte value = _action[Slot(frame, player)];
        return value == NoAction ? null : (PlayerAction)value;
    }

    /// <summary>
    /// Punto al que el jugador intenta ir en ese tick: el destino que la acción elegida le puso
    /// (<c>MatchPlayer.TargetPoint</c>), ya recortado a la zona por el motor solo en el momento de mover.
    /// Con la acción al lado explica la mitad de lo que se ve: dónde está y hacia dónde tira.
    /// </summary>
    public Vec2 TargetAt(int frame, int player) => new(_targetX[Slot(frame, player)], _targetY[Slot(frame, player)]);

    /// <summary>Fotograma cuyo tick es el indicado, acotado al rango; los ticks van de 1 en 1 y sin huecos.</summary>
    public int FrameOfTick(int tick)
    {
        if (FrameCount == 0)
        {
            return 0;
        }

        int index = tick - _tick[0];
        return index < 0 ? 0 : (index >= FrameCount ? FrameCount - 1 : index);
    }

    private int Slot(int frame, int player) => (frame * Players.Count) + player;

    /// <summary>Valor de <see cref="_action"/> para "todavía no ha decidido"; el enum no llega a 255.</summary>
    internal const byte NoAction = 255;
}

/// <summary>
/// Grabador de la traza: lo único que <see cref="MatchEngine"/> conoce de todo esto. Vive en su propio
/// fichero a propósito, para que el motor solo tenga que declararlo, construirlo si la configuración lo
/// pide y llamar a <see cref="Capture"/> una vez por tick.
/// </summary>
internal sealed class MatchTraceRecorder
{
    /// <summary>Ticks reglamentarios de un partido: el tamaño con el que se reservan las listas.</summary>
    private const int ExpectedFrames = 1300;

    private readonly MatchPlayer[] _source;
    private readonly TracePlayer[] _players;
    private readonly int _regulationTicks;

    private readonly List<int> _tick = new(ExpectedFrames);
    private readonly List<byte> _phase = new(ExpectedFrames);
    private readonly List<float> _ballX = new(ExpectedFrames);
    private readonly List<float> _ballY = new(ExpectedFrames);
    private readonly List<int> _ballOwner = new(ExpectedFrames);
    private readonly List<bool> _ballInFlight = new(ExpectedFrames);
    private readonly List<int> _eventsUpTo = new(ExpectedFrames);

    private readonly List<float> _x;
    private readonly List<float> _y;
    private readonly List<byte> _state;
    private readonly List<bool> _onPitch;
    private readonly List<float> _homeX;
    private readonly List<float> _homeY;
    private readonly List<float> _zoneForward;
    private readonly List<float> _zoneBack;
    private readonly List<float> _zoneSides;
    private readonly List<int> _markTarget;
    private readonly List<byte> _action;
    private readonly List<float> _targetX;
    private readonly List<float> _targetY;

    /// <summary>
    /// Última acción elegida por cada jugador, indexada por su posición en el array del motor. La memoria
    /// vive aquí y no en <c>MatchPlayer</c> a propósito: el motor decide cada
    /// <c>tuning.decisionIntervalTicks</c> ticks y no guarda lo que eligió, y esto es información de
    /// <b>observación</b>, no de juego. Con la traza apagada este array no existe.
    /// </summary>
    private readonly byte[] _lastAction;

    public MatchTraceRecorder(MatchPlayer[] players, int regulationTicks)
    {
        _source = players;
        _regulationTicks = regulationTicks;
        _players = Describe(players);

        int cells = ExpectedFrames * players.Length;
        _x = new List<float>(cells);
        _y = new List<float>(cells);
        _state = new List<byte>(cells);
        _onPitch = new List<bool>(cells);
        _homeX = new List<float>(cells);
        _homeY = new List<float>(cells);
        _zoneForward = new List<float>(cells);
        _zoneBack = new List<float>(cells);
        _zoneSides = new List<float>(cells);
        _markTarget = new List<int>(cells);
        _action = new List<byte>(cells);
        _targetX = new List<float>(cells);
        _targetY = new List<float>(cells);

        _lastAction = new byte[players.Length];
        for (int i = 0; i < _lastAction.Length; i++)
        {
            _lastAction[i] = MatchTrace.NoAction;
        }
    }

    /// <summary>
    /// La acción que la tabla de utilidad acaba de elegir para un jugador. Es lo único que el motor le
    /// cuenta al grabador además del tick: la acción no se queda en <c>MatchPlayer</c>, así que si no se
    /// anota en el momento de decidirla se pierde. <b>Solo escribe en el grabador</b>: nada de esto vuelve
    /// al motor (RT-024).
    /// </summary>
    public void Decided(int player, PlayerAction action) => _lastAction[player] = (byte)action;

    /// <summary>
    /// Un fotograma con el estado consolidado del tick. Solo <b>lee</b>: ni toca el motor ni consume
    /// aleatoriedad, así que un partido con traza y el mismo partido sin ella son el mismo partido
    /// (RT-024).
    /// </summary>
    public void Capture(int tick, MatchPhase phase, Ball ball, int eventCount)
    {
        _tick.Add(tick);
        _phase.Add((byte)phase);
        _ballX.Add(ball.Position.X);
        _ballY.Add(ball.Position.Y);
        _ballOwner.Add(ball.Owner is null ? -1 : ball.Owner.Index);
        _ballInFlight.Add(ball.InFlight);
        _eventsUpTo.Add(eventCount);

        for (int i = 0; i < _source.Length; i++)
        {
            var player = _source[i];
            _x.Add(player.Position.X);
            _y.Add(player.Position.Y);
            _state.Add((byte)player.State);
            _onPitch.Add(player.OnPitch);
            _homeX.Add(player.EffectiveHome.X);
            _homeY.Add(player.EffectiveHome.Y);

            var zone = player.Zone;
            _zoneForward.Add(Cells(zone.ForwardMilli));
            _zoneBack.Add(Cells(zone.BackMilli));
            _zoneSides.Add(Cells(zone.SidesMilli));

            // El objetivo de marcaje se graba por índice, no por id: la traza indexa por índice y así la
            // pantalla no tiene que buscar a nadie para pintar la línea entre marcador y marcado.
            var mark = player.MarkTarget;
            _markTarget.Add(mark is null || !Marking.IsValidTarget(mark, player.Team) ? -1 : mark.Index);
            _action.Add(_lastAction[i]);
            _targetX.Add(player.TargetPoint.X);
            _targetY.Add(player.TargetPoint.Y);
        }
    }

    /// <summary>Traza inmutable con lo grabado; se llama una vez, al terminar el partido.</summary>
    public MatchTrace Build() => new(
        _players,
        _regulationTicks,
        _tick.ToArray(),
        _phase.ToArray(),
        _ballX.ToArray(),
        _ballY.ToArray(),
        _ballOwner.ToArray(),
        _ballInFlight.ToArray(),
        _eventsUpTo.ToArray(),
        _x.ToArray(),
        _y.ToArray(),
        _state.ToArray(),
        _onPitch.ToArray(),
        _homeX.ToArray(),
        _homeY.ToArray(),
        _zoneForward.ToArray(),
        _zoneBack.ToArray(),
        _zoneSides.ToArray(),
        _markTarget.ToArray(),
        _action.ToArray(),
        _targetX.ToArray(),
        _targetY.ToArray());

    private static float Cells(int milli) => milli == ActionZone.Unlimited ? TraceZone.Unlimited : milli / 1000f;

    /// <summary>
    /// Los datos fijos de cada ficha, con el dorsal repartido por equipo. El orden del reparto es
    /// (posición, id) ascendente —el mismo criterio determinista de RT-041— así que el portero es el 1 y
    /// el resto numera de atrás hacia delante.
    /// </summary>
    private static TracePlayer[] Describe(MatchPlayer[] players)
    {
        var numbers = new int[players.Length];
        for (int team = 0; team < 2; team++)
        {
            int next = 1;
            for (int role = 0; role <= (int)Position.Forward; role++)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].Team == team && (int)players[i].Role == role)
                    {
                        numbers[i] = next++;
                    }
                }
            }
        }

        var described = new TracePlayer[players.Length];
        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            described[i] = new TracePlayer(
                player.Id,
                player.Team,
                player.Role,
                player.Name,
                Initials(player.Name),
                numbers[i]);
        }

        return described;
    }

    /// <summary>Iniciales del nombre, hasta dos letras y en mayúsculas; vacío si el nombre lo está.</summary>
    private static string Initials(string name)
    {
        Span<char> letters = stackalloc char[2];
        int count = 0;
        bool atWordStart = true;
        for (int i = 0; i < name.Length && count < 2; i++)
        {
            char c = name[i];
            if (c == ' ' || c == '\'' || c == '-')
            {
                atWordStart = true;
                continue;
            }

            if (atWordStart)
            {
                letters[count++] = char.ToUpperInvariant(c);
                atWordStart = false;
            }
        }

        return new string(letters[..count]);
    }
}
