namespace Underleague.Sim.Model;

/// <summary>
/// Casilla-hogar de un jugador en la alineación. Coordenadas relativas al equipo: Column 0..7 desde
/// la propia portería, Row 0..4. El motor refleja las columnas para el equipo 1.
/// </summary>
public sealed record LineupSlot(int PlayerId, Cell HomeCell);

/// <summary>Alineación: qué jugadores titulares ocupan qué casilla-hogar.</summary>
public sealed record Lineup(IReadOnlyList<LineupSlot> Slots)
{
    private static readonly Cell[] GoalkeeperCells = { new(0, 2) };
    private static readonly Cell[] DefenderCells = { new(2, 1), new(2, 3) };
    private static readonly Cell[] MidfielderCells = { new(4, 0), new(4, 2), new(4, 4) };
    private static readonly Cell[] ForwardCells = { new(6, 2) };

    /// <summary>
    /// Alineación por defecto: GK (0,2); DEF (2,1),(2,3); MID (4,0),(4,2),(4,4); FWD (6,2).
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
public sealed record TeamSetup(string Id, string Name, Race Race, IReadOnlyList<PlayerDefinition> Players, Lineup Lineup);
