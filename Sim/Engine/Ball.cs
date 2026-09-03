namespace Underleague.Sim.Engine;

/// <summary>
/// Estado del balón (§3.7). Con dueño, el balón está en la posición del dueño. En vuelo se interpola
/// linealmente entre el origen y el destino en <see cref="FlightTicksTotal"/> ticks.
/// Las referencias a jugador se guardan como objeto (no como id) para no consultar diccionarios por tick.
/// </summary>
internal sealed class Ball
{
    /// <summary>Posición continua del balón en casillas.</summary>
    public Vec2 Position { get; set; }

    /// <summary>Velocidad del balón suelto, en casillas por tick.</summary>
    public Vec2 Velocity { get; set; }

    /// <summary>Poseedor actual; null si el balón está suelto o en vuelo.</summary>
    public MatchPlayer? Owner { get; set; }

    /// <summary>Id del poseedor, -1 si el balón está suelto (§3.7).</summary>
    public int OwnerId => Owner is null ? -1 : Owner.Id;

    /// <summary>True mientras el balón viaja (pase o tiro).</summary>
    public bool InFlight { get; set; }

    /// <summary>True si el vuelo actual es un tiro; false si es un pase.</summary>
    public bool IsShot { get; set; }

    /// <summary>Punto de partida del vuelo actual.</summary>
    public Vec2 FlightOrigin { get; set; }

    /// <summary>Punto de llegada del vuelo actual.</summary>
    public Vec2 FlightTarget { get; set; }

    /// <summary>Ticks de vuelo que quedan.</summary>
    public int FlightTicksLeft { get; set; }

    /// <summary>Ticks totales del vuelo actual, para interpolar.</summary>
    public int FlightTicksTotal { get; set; }

    /// <summary>Receptor previsto del pase en vuelo.</summary>
    public MatchPlayer? PassReceiver { get; set; }

    /// <summary>Pasador del pase en vuelo (asistencia y estadísticas).</summary>
    public MatchPlayer? Passer { get; set; }

    /// <summary>Resultado del pase decidido al lanzarlo (§3.7): el vuelo solo lo revela al llegar.</summary>
    public bool PassSucceeds { get; set; }

    /// <summary>Tirador del tiro en vuelo.</summary>
    public MatchPlayer? Shooter { get; set; }

    /// <summary>True si el tiro en vuelo va entre los tres palos.</summary>
    public bool ShotOnTarget { get; set; }

    /// <summary>Calidad 0..100 del tiro en vuelo, usada por la parada.</summary>
    public int ShotQuality { get; set; }

    /// <summary>Distancia en casillas desde la que se lanzó el tiro en vuelo (parada de cerca/lejos).</summary>
    public float ShotDistance { get; set; }

    /// <summary>True si el tiro en vuelo es un penalti (§3.8).</summary>
    public bool ShotIsPenalty { get; set; }

    /// <summary>Equipo del último jugador que tocó el balón; decide saques y recuperaciones.</summary>
    public int LastTouchTeam { get; set; } = -1;

    /// <summary>Último jugador que tocó el balón.</summary>
    public MatchPlayer? LastTouchPlayer { get; set; }

    /// <summary>Un hueco por jugador: si ya intentó interceptar el pase en vuelo actual (§3.7).</summary>
    public bool[] InterceptAttempted { get; set; } = Array.Empty<bool>();

    /// <summary>Deja el balón suelto en su posición actual con la velocidad indicada.</summary>
    public void SetLoose(Vec2 velocity)
    {
        Owner = null;
        InFlight = false;
        IsShot = false;
        PassReceiver = null;
        Passer = null;
        Shooter = null;
        Velocity = velocity;
    }

    /// <summary>Detiene el balón en un punto concreto sin dueño (reanudaciones).</summary>
    public void Park(Vec2 position)
    {
        Owner = null;
        InFlight = false;
        IsShot = false;
        PassReceiver = null;
        Passer = null;
        Shooter = null;
        Position = position;
        Velocity = new Vec2(0f, 0f);
    }
}
