using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Analysis.Detectors;

/// <summary>
/// Vista plana de una traza (RT-098) para la batería de detectores de síntomas: arrays por fotograma y por
/// jugador, sin <see cref="MatchTrace"/> de por medio. Existe por una razón de la Regla J: el constructor de
/// <see cref="MatchTrace"/> es <c>internal</c> y pide 27 arrays, así que no se puede construir un caso
/// <b>positivo con respuesta conocida</b> con él. Aquí sí: <see cref="Synthetic"/> da una traza vacía y
/// quieta que cada prueba deforma a mano, y <see cref="From"/> copia la de un partido de verdad.
/// Solo lectura sobre el motor: no cambia nada de <c>/Sim</c>.
/// </summary>
internal sealed class DetectorTrace
{
    public int Frames { get; }
    public int Players { get; }

    public int[] Tick { get; }
    public MatchPhase[] Phase { get; }
    public RestartKind[] Restart { get; }
    public int[] Taker { get; }
    public float[] BallX { get; }
    public float[] BallY { get; }
    public int[] Owner { get; }
    public bool[] Flight { get; }

    /// <summary>Por fotograma y jugador: índice <c>frame * Players + player</c>.</summary>
    public float[] X { get; }
    public float[] Y { get; }
    public PlayerState[] State { get; }
    public bool[] OnPitch { get; }

    /// <summary>La acción elegida, o -1 si todavía no ha decidido.</summary>
    public int[] Action { get; }

    public int[] Team { get; }
    public Position[] Role { get; }
    public int[] Id { get; }

    public IReadOnlyList<MatchEvent> Events { get; }

    public DetectorTrace(int frames, int[] team, Position[] role, int[] id, IReadOnlyList<MatchEvent> events)
    {
        Frames = frames;
        Players = team.Length;
        Team = team;
        Role = role;
        Id = id;
        Events = events;
        Tick = new int[frames];
        Phase = new MatchPhase[frames];
        Restart = new RestartKind[frames];
        Taker = new int[frames];
        BallX = new float[frames];
        BallY = new float[frames];
        Owner = new int[frames];
        Flight = new bool[frames];
        int slots = frames * Players;
        X = new float[slots];
        Y = new float[slots];
        State = new PlayerState[slots];
        OnPitch = new bool[slots];
        Action = new int[slots];
    }

    public int Slot(int frame, int player) => (frame * Players) + player;

    public Vec2 Pos(int frame, int player) => new(X[Slot(frame, player)], Y[Slot(frame, player)]);

    public Vec2 Ball(int frame) => new(BallX[frame], BallY[frame]);

    public bool On(int frame, int player) => OnPitch[Slot(frame, player)];

    public int IndexOfId(int id)
    {
        for (int i = 0; i < Id.Length; i++)
        {
            if (Id[i] == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Fotograma del tick (la traza tiene uno por tick).</summary>
    public int FrameOfTick(int tick)
    {
        if (Frames == 0)
        {
            return 0;
        }

        int index = tick - Tick[0];
        return index < 0 ? 0 : (index >= Frames ? Frames - 1 : index);
    }

    /// <summary>Copia la traza de un partido jugado con <c>SimConfig.Trace = true</c>.</summary>
    public static DetectorTrace From(MatchResult result)
    {
        var trace = result.Trace ?? throw new ArgumentException("el partido no trae traza");
        int n = trace.Players.Count;
        var team = new int[n];
        var role = new Position[n];
        var id = new int[n];
        for (int i = 0; i < n; i++)
        {
            team[i] = trace.Players[i].Team;
            role[i] = trace.Players[i].Role;
            id[i] = trace.Players[i].Id;
        }

        var t = new DetectorTrace(trace.FrameCount, team, role, id, result.Events);
        for (int f = 0; f < trace.FrameCount; f++)
        {
            t.Tick[f] = trace.TickAt(f);
            t.Phase[f] = trace.PhaseAt(f);
            t.Restart[f] = trace.RestartAt(f);
            t.Taker[f] = trace.RestartTakerAt(f);
            var ball = trace.BallAt(f);
            t.BallX[f] = ball.X;
            t.BallY[f] = ball.Y;
            t.Owner[f] = trace.BallOwnerAt(f);
            t.Flight[f] = trace.BallInFlightAt(f);
            for (int p = 0; p < n; p++)
            {
                int s = t.Slot(f, p);
                var pos = trace.PositionAt(f, p);
                t.X[s] = pos.X;
                t.Y[s] = pos.Y;
                t.State[s] = trace.StateAt(f, p);
                t.OnPitch[s] = trace.OnPitchAt(f, p);
                t.Action[s] = trace.ActionAt(f, p) is { } a ? (int)a : -1;
            }
        }

        return t;
    }

    /// <summary>
    /// Traza sintética y quieta: 7 + 7 jugadores (índice 0 y 7 porteros, 1-2 defensas, 3-4 centrocampistas,
    /// 5-6 delanteros), todos en el campo y parados en su casilla, balón libre y quieto en el centro, juego
    /// abierto, sin eventos. Cada prueba la deforma a mano para construir un caso con respuesta conocida.
    /// </summary>
    public static DetectorTrace Synthetic(int frames, IReadOnlyList<MatchEvent>? events = null)
    {
        var team = new int[14];
        var role = new Position[14];
        var id = new int[14];
        Position[] roles = [Position.Goalkeeper, Position.Defender, Position.Defender, Position.Midfielder,
            Position.Midfielder, Position.Forward, Position.Forward];
        for (int i = 0; i < 14; i++)
        {
            team[i] = i / 7;
            role[i] = roles[i % 7];
            id[i] = (team[i] * 100) + (i % 7);
        }

        var t = new DetectorTrace(frames, team, role, id, events ?? Array.Empty<MatchEvent>());
        for (int f = 0; f < frames; f++)
        {
            t.Tick[f] = f;
            t.Phase[f] = MatchPhase.OpenPlay;
            t.Taker[f] = -1;
            t.BallX[f] = 8f;
            t.BallY[f] = 3.5f;
            t.Owner[f] = -1;
            for (int p = 0; p < 14; p++)
            {
                int s = t.Slot(f, p);
                int k = p % 7;
                // Cada uno en una casilla propia, lejos de los demás (nadie a menos de 1 casilla de nadie).
                float baseX = k == 0 ? 0.5f : 2f + (k * 1.9f);
                t.X[s] = team[p] == 0 ? baseX : Pitch.Columns - baseX;
                t.Y[s] = 0.5f + (k * 0.9f);
                t.OnPitch[s] = true;
                t.State[s] = PlayerState.Positioning;
                t.Action[s] = -1;
            }
        }

        return t;
    }

    public void SetPos(int frame, int player, float x, float y)
    {
        int s = Slot(frame, player);
        X[s] = x;
        Y[s] = y;
    }

    /// <summary>Un evento mínimo para los casos sintéticos.</summary>
    public static MatchEvent Event(EventType type, int tick, int team = 0, int actor = 0, int opponent = 0, string detail = "")
        => new(type, tick, team, actor, 0, opponent, new Cell(0, 0), Zone.Middle, MatchPhase.OpenPlay, 0, 0, detail);
}
