using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Placement;

/// <summary>
/// Zona de acción de un jugador ya colocado, resuelta en <b>casillas absolutas</b> del campo para que la
/// interfaz solo tenga que pintarlas (RF-045, ADR 0028, ADR 0029). Dos capas separadas porque son dos
/// promesas distintas: <see cref="Zone"/> es "aquí estará" y <see cref="Margin"/> es "aquí puede llegar".
/// <see cref="Margin"/> excluye a <see cref="Zone"/>: son conjuntos disjuntos, listos para dibujar.
/// </summary>
public sealed record PlacementZone(Cell Home, IReadOnlyList<Cell> Zone, IReadOnlyList<Cell> Margin);

/// <summary>
/// Vínculo direccional entre dos casillas-hogar de la misma alineación (RF-044, RF-106, ADR 0021).
/// <paramref name="Relation"/> se lee "<paramref name="ToPlayerId"/> es el compañero
/// <c>relation</c> de <paramref name="FromPlayerId"/>".
/// </summary>
public sealed record PlacementLink(int FromPlayerId, int ToPlayerId, LinkRelation Relation);

/// <summary>
/// Mapa de cobertura del equipo (ADR 0029 §4): cuántos jugadores de la alineación tienen cada casilla
/// dentro de su zona de acción. Responde a la pregunta que de verdad importa —¿qué parte del campo no
/// cubre nadie?— y hace visible el coste de apiñar el equipo.
/// </summary>
public sealed class CoverageMap
{
    private readonly int[] _counts;

    internal CoverageMap(int[] counts)
    {
        _counts = counts;
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > Max)
            {
                Max = counts[i];
            }

            if (counts[i] == 0)
            {
                Holes++;
            }
        }
    }

    /// <summary>Casillas del campo que no cubre ningún jugador.</summary>
    public int Holes { get; }

    /// <summary>Mayor número de jugadores que cubren una misma casilla.</summary>
    public int Max { get; }

    /// <summary>Jugadores cuya zona de acción contiene la casilla; 0 fuera del campo.</summary>
    public int Count(Cell cell) =>
        cell.Column < 0 || cell.Column >= Pitch.Columns || cell.Row < 0 || cell.Row >= Pitch.Rows
            ? 0
            : _counts[(cell.Row * Pitch.Columns) + cell.Column];
}

/// <summary>
/// Lectura <b>pura</b> de la colocación para la interfaz (RT-011, RT-014): la pantalla de Equipo no
/// calcula nada del juego, pregunta aquí. Toda la geometría reutiliza la del motor
/// (<c>Sim.Engine.ActionZone</c>, <c>Sim.Perks.LinkGeometry</c>), así que lo que se pinta es exactamente
/// lo que el simulador va a aplicar.
/// <para>
/// Sin E/S, sin reloj y sin aleatoriedad (RT-012, RT-021): recibe datos y devuelve datos. Las listas
/// salen siempre en orden determinista (casillas por fila y luego columna ascendente; vínculos por id de
/// jugador y luego por relación).
/// </para>
/// </summary>
public static class PlacementView
{
    /// <summary>
    /// Casilla fija del portero (RF-041): centro de la línea de gol propia. Las columnas de una
    /// alineación son 0..<see cref="Pitch.PlacementColumns"/>-1 desde la portería propia.
    /// </summary>
    public static Cell GoalkeeperCell => new(0, Pitch.Rows / 2);

    /// <summary>
    /// True si <paramref name="position"/> puede colocarse en <paramref name="cell"/> (RF-041): la
    /// colocación es libre dentro de la mitad propia salvo el portero, que ocupa una casilla fija y no
    /// la comparte con nadie.
    /// <para>
    /// Lectura conservadora de RF-022b ("la posición restringe las filas y columnas donde puede
    /// colocarse"): la única restricción que el diseño concreta hoy es la del portero, así que es la
    /// única que se aplica. Cualquier restricción adicional es una regla de juego nueva y se decide en
    /// <c>requisitos.md</c>, no aquí.
    /// </para>
    /// </summary>
    public static bool CanPlace(Position position, Cell cell)
    {
        if (cell.Column < 0 || cell.Column >= Pitch.PlacementColumns || cell.Row < 0 || cell.Row >= Pitch.Rows)
        {
            return false;
        }

        return position == Position.Goalkeeper ? cell == GoalkeeperCell : cell != GoalkeeperCell;
    }

    /// <summary>
    /// Las dos capas de la zona de acción del jugador si su casilla-hogar fuese <paramref name="home"/>
    /// (RF-045). La forma la da la posición y el tamaño lo escala el atributo de correa más el bono de
    /// los rasgos, igual que en el partido; el margen exterior es esa misma zona multiplicada por
    /// <c>tuning.actionZone.outerLimitMultiplier</c>, el límite duro de la ADR 0028.
    /// <para>
    /// Una casilla pertenece a una capa si su <b>centro</b> cae dentro: es el mismo punto con el que el
    /// motor decide si un jugador está fuera de su zona, así que el dibujo no promete nada que la
    /// simulación no vaya a cumplir.
    /// </para>
    /// </summary>
    public static PlacementZone ZoneOf(PlayerDefinition player, Cell home, Catalog catalog, int team = 0)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(catalog);

        var tuning = catalog.Tuning.ActionZone;
        var zone = BuildZone(player, catalog, tuning);
        var outer = zone.Scaled(tuning.OuterLimitMultiplier);

        int direction = Pitch.AttackDirection(team);
        var homeCenter = Pitch.CellCenter(home);
        var inZone = new List<Cell>();
        var inMargin = new List<Cell>();

        for (int row = 0; row < Pitch.Rows; row++)
        {
            for (int column = 0; column < Pitch.Columns; column++)
            {
                var cell = new Cell(column, row);
                var center = Pitch.CellCenter(cell);
                if (zone.DistanceOutside(center, homeCenter, direction) <= 0f)
                {
                    inZone.Add(cell);
                }
                else if (outer.DistanceOutside(center, homeCenter, direction) <= 0f)
                {
                    inMargin.Add(cell);
                }
            }
        }

        return new PlacementZone(home, inZone, inMargin);
    }

    /// <summary>
    /// Vínculos direccionales que produce una alineación (RF-044, RF-106). Misma regla que la tabla del
    /// motor (<c>Sim.Perks.LinkTable</c>): un candidato por relación, el más cercano por distancia entre
    /// casillas-hogar y, a igual distancia, el de id ascendente; si no hay candidato no hay vínculo.
    /// Resultado ordenado por id de origen y luego por relación.
    /// </summary>
    public static IReadOnlyList<PlacementLink> Links(Lineup lineup, int team = 0)
    {
        ArgumentNullException.ThrowIfNull(lineup);

        var slots = new List<LineupSlot>(lineup.Slots);
        slots.Sort(static (a, b) => a.PlayerId.CompareTo(b.PlayerId));

        var links = new List<PlacementLink>();
        for (int i = 0; i < slots.Count; i++)
        {
            foreach (var relation in Enum.GetValues<LinkRelation>())
            {
                int best = -1;
                int bestDistance = int.MaxValue;
                for (int j = 0; j < slots.Count; j++)
                {
                    if (j == i || !LinkGeometry.Matches(slots[i].HomeCell, slots[j].HomeCell, team, relation))
                    {
                        continue;
                    }

                    int distance = LinkGeometry.SquaredDistance(slots[i].HomeCell, slots[j].HomeCell);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = j;
                    }
                }

                if (best >= 0)
                {
                    links.Add(new PlacementLink(slots[i].PlayerId, slots[best].PlayerId, relation));
                }
            }
        }

        return links;
    }

    /// <summary>
    /// Mapa de cobertura de la alineación (ADR 0029 §4): por cada casilla del campo, cuántos titulares la
    /// tienen dentro de su <b>zona</b> (el margen exterior no cuenta: es adonde el jugador puede llegar,
    /// no donde va a estar).
    /// </summary>
    public static CoverageMap Coverage(IReadOnlyList<PlayerDefinition> players, Lineup lineup, Catalog catalog, int team = 0)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(catalog);

        var counts = new int[Pitch.Columns * Pitch.Rows];
        var slots = new List<LineupSlot>(lineup.Slots);
        slots.Sort(static (a, b) => a.PlayerId.CompareTo(b.PlayerId));

        for (int i = 0; i < slots.Count; i++)
        {
            var player = Find(players, slots[i].PlayerId);
            if (player is null)
            {
                continue;
            }

            var zone = ZoneOf(player, slots[i].HomeCell, catalog, team);
            for (int c = 0; c < zone.Zone.Count; c++)
            {
                counts[(zone.Zone[c].Row * Pitch.Columns) + zone.Zone[c].Column]++;
            }
        }

        return new CoverageMap(counts);
    }

    /// <summary>
    /// Alineación resultante de dejar a <paramref name="playerId"/> en <paramref name="target"/>
    /// (RF-041). Es la única regla de colocación y vive aquí, no en la interfaz:
    /// <list type="bullet">
    /// <item>si el jugador ya estaba alineado y la casilla está ocupada, los dos <b>intercambian</b> casilla;</item>
    /// <item>si estaba alineado y la casilla está libre, se <b>mueve</b>;</item>
    /// <item>si venía del banquillo, <b>sustituye</b> al ocupante (que pasa al banquillo) o entra en la casilla libre.</item>
    /// </list>
    /// Devuelve la alineación sin tocar si el movimiento no es válido. Las casillas salen ordenadas por
    /// id de jugador ascendente, que es el orden que espera el motor (RT-041).
    /// </summary>
    public static Lineup WithPlayerAt(Lineup lineup, IReadOnlyList<PlayerDefinition> players, int playerId, Cell target)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(players);

        var moved = Find(players, playerId);
        if (moved is null || !CanPlace(moved.Position, target))
        {
            return lineup;
        }

        var slots = new List<LineupSlot>(lineup.Slots);
        int from = IndexOfPlayer(slots, playerId);
        int to = IndexOfCell(slots, target);

        if (from >= 0 && to >= 0)
        {
            var occupant = Find(players, slots[to].PlayerId);
            if (occupant is null || !CanPlace(occupant.Position, slots[from].HomeCell))
            {
                return lineup;
            }

            var origin = slots[from].HomeCell;
            slots[from] = slots[from] with { HomeCell = target };
            slots[to] = slots[to] with { HomeCell = origin };
        }
        else if (from >= 0)
        {
            slots[from] = slots[from] with { HomeCell = target };
        }
        else if (to >= 0)
        {
            slots[to] = new LineupSlot(playerId, target);
        }
        else
        {
            slots.Add(new LineupSlot(playerId, target));
        }

        slots.Sort(static (a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return new Lineup(slots);
    }

    /// <summary>Construye la zona del jugador con la misma fórmula que <c>Sim.Engine.MatchPlayer</c> (ADR 0028).</summary>
    private static ActionZone BuildZone(PlayerDefinition player, Catalog catalog, ActionZoneTuning tuning)
    {
        var shape = player.Position switch
        {
            Position.Goalkeeper => tuning.Shape.Goalkeeper,
            Position.Defender => tuning.Shape.Defender,
            Position.Midfielder => tuning.Shape.Midfielder,
            _ => tuning.Shape.Forward,
        };

        int leashBonus = 0;
        for (int i = 0; i < player.Traits.Count; i++)
        {
            leashBonus += catalog.Trait(player.Traits[i]).LeashBonus;
        }

        int leash = Math.Clamp(player.Attributes.Leash, 1, 99);
        int percent = tuning.ScaleFromLeashPercent.At1
            + ((tuning.ScaleFromLeashPercent.At99 - tuning.ScaleFromLeashPercent.At1) * (leash - 1) / 98);
        int extraMilli = leashBonus * 1000;

        return new ActionZone(
            ExtentMilli(shape.Forward, percent, extraMilli),
            ExtentMilli(shape.Back, percent, extraMilli),
            ExtentMilli(shape.Sides, percent, extraMilli));
    }

    /// <summary>Extensión en milicasillas de una dirección de la forma; -1 (sin límite) se propaga.</summary>
    private static int ExtentMilli(int shapeCells, int percent, int extraMilli)
    {
        if (shapeCells == ActionZone.Unlimited)
        {
            return ActionZone.Unlimited;
        }

        int milli = (shapeCells * 1000 * percent / 100) + extraMilli;
        return milli < 0 ? 0 : milli;
    }

    private static PlayerDefinition? Find(IReadOnlyList<PlayerDefinition> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return players[i];
            }
        }

        return null;
    }

    private static int IndexOfPlayer(IReadOnlyList<LineupSlot> slots, int id)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].PlayerId == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfCell(IReadOnlyList<LineupSlot> slots, Cell cell)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].HomeCell == cell)
            {
                return i;
            }
        }

        return -1;
    }
}
