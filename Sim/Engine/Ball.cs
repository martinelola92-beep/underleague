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

    /// <summary>
    /// Altura del balón sobre el césped, en casillas (ADR 0135). Cero es el suelo, que es donde el balón
    /// ha estado siempre hasta esta ADR.
    ///
    /// <para>La altura es del BALÓN, no del espacio: el campo sigue siendo 2D y los jugadores corren por
    /// el suelo. Se descartó migrar <see cref="Vec2"/> a tres dimensiones porque habría tocado las
    /// posiciones de los catorce jugadores, las zonas, la separación de cuerpos, el marcaje y la traza —y
    /// el 99 % de eso no necesita altura para nada—. El patrón del repositorio es añadir un escalar al
    /// actor que lo necesita, no una dimensión al mundo.</para>
    ///
    /// <para><c>float</c> con el mismo estatus de determinismo que la X y la Y (RT-023, posiciones); las
    /// magnitudes que la gobiernan —gravedad, impulso— son enteras en milésimas en <c>tuning</c>, como
    /// <c>shotSpeedCellsPerTickMilli</c> ya lo es.</para>
    /// </summary>
    public float Z { get; set; }

    /// <summary>Velocidad vertical, en casillas por tick. Positiva hacia arriba (ADR 0135).</summary>
    public float VelocityZ { get; set; }

    /// <summary>
    /// Altura a la que llega el vuelo actual, en casillas (ADR 0135 paso 2). Cero para un pase, que sigue
    /// siendo raso; para un tiro es el punto de la portería al que se apuntó, o por dónde se fue si iba
    /// fuera.
    /// </summary>
    public float FlightTargetZ { get; set; }

    /// <summary>
    /// Comba del vuelo: cuánto se eleva el balón a mitad de camino <b>por encima</b> de la recta que une
    /// origen y destino (ADR 0135 paso 2). Es lo que distingue un disparo de un rayo láser, y lo que el
    /// paso 3 usará para saber si a un defensa el balón le pasa por encima.
    /// </summary>
    public float FlightArc { get; set; }

    /// <summary>Poseedor actual; null si el balón está suelto o en vuelo.</summary>
    public MatchPlayer? Owner { get; set; }

    /// <summary>True mientras el balón viaja (pase o tiro).</summary>
    public bool InFlight { get; set; }

    /// <summary>True si el vuelo actual es un tiro; false si es un pase.</summary>
    public bool IsShot { get; set; }

    /// <summary>Pase en profundidad en vuelo (AZ-B paso 5): a una casilla, lo gana quien llega antes.</summary>
    public bool IsThroughPass { get; set; }

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

    /// <summary>
    /// Si el portero ya disputó este tiro (AW-A, paso 1): un solo duelo de parada por disparo, aunque el
    /// balón siga volando después de perderlo. Es el equivalente para el tiro de
    /// <see cref="InterceptAttempted"/> en el pase, y se limpia al lanzar cada tiro.
    /// </summary>
    public bool SaveAttempted { get; set; }

    /// <summary>Equipo del último jugador que tocó el balón; decide saques y recuperaciones.</summary>
    public int LastTouchTeam { get; set; } = -1;

    /// <summary>Último jugador que tocó el balón.</summary>
    public MatchPlayer? LastTouchPlayer { get; set; }

    /// <summary>Un hueco por jugador: si ya intentó interceptar el pase en vuelo actual (§3.7).</summary>
    public bool[] InterceptAttempted { get; set; } = Array.Empty<bool>();

    /// <summary>
    /// Un hueco por jugador: si ya intentó bloquear el tiro en vuelo actual (AW-A, paso 3). Es el
    /// equivalente para el disparo de <see cref="InterceptAttempted"/>, y va aparte a propósito: durante
    /// un tiro <c>TryIntercept</c> no corre (se salta con <c>!IsShot</c>), así que
    /// <see cref="InterceptAttempted"/> conserva el estado del último pase y no se limpia por su cuenta.
    /// Se limpia al lanzar cada tiro.
    /// </summary>
    public bool[] BlockAttempted { get; set; } = Array.Empty<bool>();

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
        Z = 0f;
        VelocityZ = 0f;
        FlightTargetZ = 0f;
        FlightArc = 0f;
    }

    /// <summary>
    /// Deja el balón suelto con velocidad en el plano <b>y en vertical</b> (ADR 0135): es lo que usa un
    /// rechace, que sale del punto de contacto hacia algún sitio y por el aire.
    /// </summary>
    public void SetLoose(Vec2 velocity, float velocityZ)
    {
        SetLoose(velocity);
        VelocityZ = velocityZ;
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
        Z = 0f;
        VelocityZ = 0f;
        FlightTargetZ = 0f;
        FlightArc = 0f;
    }
}
