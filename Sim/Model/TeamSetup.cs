namespace Underleague.Sim.Model;

/// <summary>
/// Casilla-hogar de un jugador en la alineación. Coordenadas relativas al equipo: Column 0..7 desde
/// la propia portería, Row 0..6. El motor refleja las columnas para el equipo 1.
/// </summary>
public sealed record LineupSlot(int PlayerId, Cell HomeCell);

/// <summary>Alineación: qué jugadores titulares ocupan qué casilla-hogar.</summary>
public sealed record Lineup(IReadOnlyList<LineupSlot> Slots)
{
    // Alineación por defecto 2-3-1 (paquete U), en las siete filas sucesoras de la ADR 0103. La de cinco
    // filas —GK (0,2); DEF (2,1),(2,3); MID (3,2),(4,1),(4,3); FWD (6,2)— se diseñó para que se resolvieran
    // ahead, behind, left, right y las dos diagonales (antes de ella, con todo el mundo en columnas pares
    // a dos filas de distancia, ninguna relación se resolvía nunca y los seis perks del eje de colocación
    // aplicaban siempre su elseEffects). Con seis filas (ADR 0103) no había fila central -el centro
    // geométrico caía entre la 2 y la 3- y hubo que repartir el par defensa/mediocentro-banda a las filas
    // 1 y 4 para guardar la simetría; con SIETE vuelve a haber una única fila central (Rows / 2 = 3), así
    // que la formación es la misma de cinco filas trasladada entera un puesto abajo: cada fila +1.
    //
    // GK, el mediocentro y el delantero -los tres sin pareja, siempre en el centro- vuelven a la fila 3
    // (antes 2), que es también la que deriva Sim.Placement.PlacementView.GoalkeeperCell de Pitch.Rows / 2
    // ahora que ese cociente vuelve a caer en la casilla correcta (número impar).
    //
    // Las relaciones se siguen resolviendo exactamente igual que con cinco o con seis filas: Forward y
    // Rightward solo miran DIFERENCIAS de columna y de fila entre casillas-hogar, así que trasladar todas
    // las filas por igual (+1) no cambia ninguna diferencia. Un defensa en (2,2) tiene delante en diagonal
    // al mediocentro de (3,3) (columna +1, fila +1: ahead, diagonalAhead y right a la vez, y el recíproco
    // behind/diagonalBehind/left desde el mediocentro), y los mismos pares dan left/right entre filas
    // contiguas. 'beside' (misma columna, filas contiguas) sigue sin resolverse con esta forma: ningún
    // perk del catálogo la usa.
    private static readonly Cell[] GoalkeeperCells = { new(0, 3) };
    private static readonly Cell[] DefenderCells = { new(2, 2), new(2, 4) };
    private static readonly Cell[] MidfielderCells = { new(3, 3), new(4, 2), new(4, 4) };
    private static readonly Cell[] ForwardCells = { new(6, 3) };

    /// <summary>
    /// Alineación por defecto: GK (0,3); DEF (2,2),(2,4); MID (3,3),(4,2),(4,4); FWD (6,3).
    /// Si faltan jugadores en una posición, se rellenan solo las primeras casillas de esa posición.
    /// Si sobran (más jugadores que casillas definidas para la posición), los excedentes se colocan
    /// en la misma columna con Row = índice % Pitch.Rows (decisión fuera de la especificación: caso
    /// de borde no cubierto por el diseño de fase 0, no ejercitado por TeamGenerator).
    /// </summary>
    public static Lineup Default(IReadOnlyList<PlayerDefinition> starters)
    {
        var slots = new List<LineupSlot>();
        AddSlots(slots, starters, Position.Goalkeeper, GoalkeeperCells);
        AddSlots(slots, starters, Position.Defender, DefenderCells);
        AddSlots(slots, starters, Position.Midfielder, MidfielderCells);
        AddSlots(slots, starters, Position.Forward, ForwardCells);
        return new Lineup(slots);
    }

    private static void AddSlots(List<LineupSlot> slots, IReadOnlyList<PlayerDefinition> starters, Position position, IReadOnlyList<Cell> cells)
    {
        var players = starters.Where(p => p.Position == position).OrderBy(p => p.Id).ToList();
        for (int i = 0; i < players.Count; i++)
        {
            Cell cell = i < cells.Count ? cells[i] : new Cell(cells[^1].Column, i % Pitch.Rows);
            slots.Add(new LineupSlot(players[i].Id, cell));
        }
    }
}

/// <summary>
/// Plantilla y alineación de un equipo. Players incluye titulares y suplentes; Lineup dice quién juega.
/// Validación esperada del motor (paquete B): 5..7 titulares, exactamente 1 portero alineado, casillas
/// en 0..7 x 0..6 sin repetir.
/// </summary>
public sealed record TeamSetup(string Id, string Name, Race Race, IReadOnlyList<PlayerDefinition> Players, Lineup Lineup)
{
    /// <summary>
    /// Consumibles equipados por este equipo para este partido (RF-080..085): hasta 3, con al menos uno
    /// manual y hasta dos condicionales (lo valida <c>RunEngine.Apply(SetConsumables)</c>, RF-080..082).
    /// Se declara como propiedad <c>init</c> y no como parámetro posicional por la misma razón que
    /// <c>PlayerDefinition.Perks</c>: las construcciones existentes siguen valiendo sin tocarlas.
    /// Un equipo rival no lleva ninguno.
    /// </summary>
    public IReadOnlyList<Underleague.Sim.Perks.MatchConsumable> Consumables { get; init; } =
        Array.Empty<Underleague.Sim.Perks.MatchConsumable>();

    /// <summary>
    /// Sustituciones forzadas de este equipo en este partido (ADR 0094, AZ-F), parte del estado inicial
    /// como <see cref="Consumables"/>. Quien entra tiene que estar en <c>Players</c> y no en <c>Lineup</c>.
    /// </summary>
    public IReadOnlyList<Substitution> Substitutions { get; init; } = Array.Empty<Substitution>();

    /// <summary>
    /// Orden táctica con la que este equipo sale al campo (ADR 0140): cuánto riesgo quiere correr. Parte
    /// del estado inicial como la alineación y los consumibles —todas las decisiones del jugador ocurren
    /// entre partidos—, y por eso es <c>init</c> y no algo que se pueda cambiar en vivo.
    ///
    /// <para><b>Es una preferencia, no una instrucción</b>: la urgencia del marcador y del minuto la
    /// desplaza durante el partido sin llegar a sustituirla. Un equipo al que le quedan segundos y va
    /// perdiendo ataca aunque su orden fuera defensiva, y eso es deliberado.</para>
    /// </summary>
    public Underleague.Sim.Engine.Mentality Order { get; init; } = Underleague.Sim.Engine.Mentality.Neutral;

    /// <summary>
    /// Lesionados leves que este equipo ha decidido <b>no</b> retirar (ADR 0134 E), parte del estado inicial
    /// como <see cref="Substitutions"/>. Cada uno tiene que estar en <c>Lineup</c> y haberse lesionado
    /// levemente en su tick; lo demás es <c>ArgumentException</c>.
    ///
    /// <para><b>El rechazo del sustituto no vive aquí</b>, ni en <see cref="Substitutions"/> con un
    /// centinela: «que se quede el hueco» no produce ningún hecho que el motor deba ejecutar, así que se
    /// queda en <c>MatchDecisions</c> y el motor no llega a enterarse. Un centinela habría obligado a cada
    /// consumidor de la lista a acordarse de excluirlo, que es el patrón por el que sigue abierto el
    /// pendiente BE-F.</para>
    /// </summary>
    public IReadOnlyList<PlayOn> PlayOns { get; init; } = Array.Empty<PlayOn>();
}
