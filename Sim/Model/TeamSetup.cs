namespace Underleague.Sim.Model;

/// <summary>
/// Casilla-hogar de un jugador en la alineación. Coordenadas relativas al equipo: Column 0..7 desde
/// la propia portería, Row 0..5. El motor refleja las columnas para el equipo 1.
/// </summary>
public sealed record LineupSlot(int PlayerId, Cell HomeCell);

/// <summary>Alineación: qué jugadores titulares ocupan qué casilla-hogar.</summary>
public sealed record Lineup(IReadOnlyList<LineupSlot> Slots)
{
    // Alineación por defecto 2-3-1 (paquete U, adaptada a las seis filas de la ADR 0103). La de cinco
    // filas —GK (0,2); DEF (2,1),(2,3); MID (3,2),(4,1),(4,3); FWD (6,2)— se diseñó para que se
    // resolvieran ahead, behind, left, right y las dos diagonales (antes de ella, con todo el mundo en
    // columnas pares a dos filas de distancia, ninguna relación se resolvía nunca y los seis perks del eje
    // de colocación aplicaban siempre su elseEffects).
    //
    // Con Rows=6 el centro geométrico cae entre las filas 2 y 3 (CenterRow = 3,0), así que ya no hay una
    // única fila central alrededor de la que colocar la línea defensiva y el centrocampo abierto de forma
    // simétrica: se reparten a fila 1 y fila 4, equidistantes del centro (1,5 casillas cada una), en vez de
    // 1 y 3 como con cinco filas (que sólo eran simétricas respecto de la fila 2, no del centro real).
    // GK, el mediocentro y el delantero —los tres sin pareja con la que guardar simetría— se quedan en la
    // fila 2: es la misma fila que ocupaban con cinco filas (cambia lo mínimo posible fuera de lo que exige
    // la simetría del par defensivo/mediocentros) y, sobre todo, es la fila que ya usa como casilla fija
    // del portero Sim.Placement.PlacementView.GoalkeeperCell (RF-041, pantalla de Equipo): esa casilla no
    // se deriva de Pitch.Rows y hay que declararla igual que esta, así que fijar aquí la 3 en vez de la 2
    // habría exigido cambiarla también. Dentro de las dos filas centrales da igual elegir la 2 o la 3
    // (ninguna es "más centro" que la otra); el portero queda descentrado media casilla mire donde mire,
    // tal y como acepta la ADR 0103: es cosmético, el motor lo corrige en el primer tick porque el portero
    // se mueve en continuo.
    //
    // Con estas filas (1, 2 y 4) las relaciones se siguen resolviendo igual que con cinco filas: un
    // defensa en (2,1) tiene delante en diagonal al mediocentro de (3,2) (columna +1, fila +1: ahead,
    // diagonalAhead y right a la vez, y el recíproco behind/diagonalBehind/left desde el mediocentro), y
    // los mismos pares dan left/right entre filas contiguas. 'beside' (misma columna, filas contiguas)
    // sigue sin resolverse con esta forma: ningún perk del catálogo la usa.
    private static readonly Cell[] GoalkeeperCells = { new(0, 2) };
    private static readonly Cell[] DefenderCells = { new(2, 1), new(2, 4) };
    private static readonly Cell[] MidfielderCells = { new(3, 2), new(4, 1), new(4, 4) };
    private static readonly Cell[] ForwardCells = { new(6, 2) };

    /// <summary>
    /// Alineación por defecto: GK (0,2); DEF (2,1),(2,4); MID (3,2),(4,1),(4,4); FWD (6,2).
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
/// en 0..7 x 0..5 sin repetir.
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
}
