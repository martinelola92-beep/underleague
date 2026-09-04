namespace Underleague.Sim.Model;

/// <summary>
/// Casilla-hogar de un jugador en la alineación. Coordenadas relativas al equipo: Column 0..7 desde
/// la propia portería, Row 0..4. El motor refleja las columnas para el equipo 1.
/// </summary>
public sealed record LineupSlot(int PlayerId, Cell HomeCell);

/// <summary>Alineación: qué jugadores titulares ocupan qué casilla-hogar.</summary>
public sealed record Lineup(IReadOnlyList<LineupSlot> Slots)
{
    // Alineación por defecto 2-3-1 (paquete U). La anterior —GK (0,2); DEF (2,1),(2,3); MID (4,0),(4,2),
    // (4,4); FWD (6,2)— dejaba a todo el mundo en columnas pares y a los compañeros de línea a dos filas
    // de distancia, así que NINGUNA relación direccional de la ADR 0021 se resolvía nunca: los seis perks
    // del eje de colocación aplicaban siempre su elseEffects y eran maluses puros. Con este 2-3-1 se
    // resuelven ahead, behind, left, right y las dos diagonales, y los tres tercios de inicio de
    // startsIn() quedan ocupados (portero y defensas en el propio, centro del campo en el medio, delantero
    // en el atacante). 'beside' (misma columna, filas contiguas) sigue sin resolverse con esta forma:
    // ningún perk del catálogo la usa.
    private static readonly Cell[] GoalkeeperCells = { new(0, 2) };
    private static readonly Cell[] DefenderCells = { new(2, 1), new(2, 3) };
    private static readonly Cell[] MidfielderCells = { new(3, 2), new(4, 1), new(4, 3) };
    private static readonly Cell[] ForwardCells = { new(6, 2) };

    /// <summary>
    /// Alineación por defecto: GK (0,2); DEF (2,1),(2,3); MID (3,2),(4,1),(4,3); FWD (6,2).
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
/// en 0..7 x 0..4 sin repetir.
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
}
