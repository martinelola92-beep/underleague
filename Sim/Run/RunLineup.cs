using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Equipo del jugador listo para <c>Simulator.Run</c>: titulares, suplentes y colocación.
/// </summary>
/// <param name="Starters">Titulares, ya convertidos a <see cref="PlayerDefinition"/> (con la penalización de lesión leve aplicada).</param>
/// <param name="Bench">Disponibles que no juegan; cobran su parte de experiencia (RF-025).</param>
/// <param name="Lineup">Colocación en la cuadrícula (RF-041).</param>
/// <param name="EmergencyGoalkeeperId">
/// Id del jugador de campo que ha tenido que ponerse de portero, o -1. Ver
/// <see cref="RunLineup"/> para el porqué.
/// </param>
public sealed record MatchLineup(
    IReadOnlyList<PlayerDefinition> Starters,
    IReadOnlyList<PlayerDefinition> Bench,
    Lineup Lineup,
    int EmergencyGoalkeeperId);

/// <summary>
/// Construye la alineación con la que se entra en un nodo de partido. Es responsabilidad del paquete W
/// porque es lo que cierra el contrato con <c>Simulator.Run</c>, que exige entre 5 y 7 titulares y
/// <b>exactamente un portero</b>.
///
/// <para><b>Portero de emergencia.</b> Una plantilla puede quedarse sin portero disponible: el club
/// inicial trae uno solo (RF-005) y una lesión grave lo aparta hasta la clínica (RF-092). Como RF-002b
/// dice que la run solo termina por dos vías, quedarse sin portero <b>no puede</b> terminarla, así que
/// el jugador de campo disponible de menor id se coloca en la portería para ese partido. Es una
/// decisión de implementación del paquete W, no una regla nueva: el cambio vive solo en el
/// <see cref="PlayerDefinition"/> que recibe el simulador y no toca el <see cref="RunPlayer"/> de la
/// plantilla. Lo mismo, al revés, con el segundo portero: si la plantilla tiene dos y los dos son
/// titulares, el sobrante juega de defensa.</para>
///
/// <para><b>Colocación.</b> Casillas fijas por rol, en coordenadas relativas al equipo propio (columna
/// 0..7 desde la portería propia): portero en (0,2), defensas en (2,1) y (2,3), centrocampistas en
/// (3,2), (4,1) y (4,3), delantero en (6,2). Es el 2-3-1 por defecto del paquete U, con el que se midió
/// el balance de la fase 1. Con menos de 7 disponibles (RF-002d, inferioridad) sobran casillas y quedan
/// vacías; nunca se repite una, que es lo que <c>Simulator.Run</c> rechaza.</para>
/// </summary>
public static class RunLineup
{
    /// <summary>Casilla del portero (RF-041: casilla fija dentro del área).</summary>
    public static Cell GoalkeeperCell { get; } = new(0, 2);

    private static readonly Cell[] DefenderCells = { new(2, 1), new(2, 3) };
    private static readonly Cell[] MidfielderCells = { new(3, 2), new(4, 1), new(4, 3) };
    private static readonly Cell[] ForwardCells = { new(6, 2) };
    private static readonly Cell[] OutfieldCells =
    {
        new(2, 1), new(2, 3), new(3, 2), new(4, 1), new(4, 3), new(6, 2),
    };

    /// <summary>
    /// Alineación por defecto de una plantilla: la que se usa al empezar la run y la que repara una
    /// alineación que ha quedado obsoleta por una baja.
    /// </summary>
    public static Lineup Default(RunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Build(state, null).Lineup;
    }

    /// <summary>
    /// Construye el equipo para un partido. Si <paramref name="catalog"/> es null no se convierte a
    /// <see cref="PlayerDefinition"/> (se usa solo para calcular la colocación).
    /// </summary>
    public static MatchLineup Build(RunState state, Catalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(state);

        var available = state.AvailablePlayers;
        if (available.Count < RunRules.MinimumAvailablePlayers)
        {
            throw new InvalidOperationException(
                $"la plantilla tiene {available.Count} jugadores disponibles y el mínimo son "
                    + $"{RunRules.MinimumAvailablePlayers} (RF-002b): la run debería haber terminado ya");
        }

        var starters = SelectStarters(state, available);
        var goalkeeper = PickGoalkeeper(starters);

        var slots = new List<LineupSlot>(starters.Count);
        var definitions = new List<PlayerDefinition>(starters.Count);
        int emergencyGoalkeeperId = -1;

        slots.Add(new LineupSlot(goalkeeper.Id, GoalkeeperCell));
        if (goalkeeper.Position != Position.Goalkeeper)
        {
            emergencyGoalkeeperId = goalkeeper.Id;
        }

        if (catalog is not null)
        {
            definitions.Add(Repositioned(goalkeeper.ToDefinition(catalog), Position.Goalkeeper, goalkeeper.Position));
        }

        var taken = new List<Cell>(OutfieldCells.Length);
        var outfield = new List<RunPlayer>(starters.Count);
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Id != goalkeeper.Id)
            {
                outfield.Add(starters[i]);
            }
        }

        foreach (var player in outfield)
        {
            var cell = CellFor(player.Position, taken);
            taken.Add(cell);
            slots.Add(new LineupSlot(player.Id, cell));
            if (catalog is null)
            {
                continue;
            }

            // El portero sobrante juega de defensa: el simulador solo admite un portero alineado.
            var position = player.Position == Position.Goalkeeper ? Position.Defender : player.Position;
            definitions.Add(Repositioned(player.ToDefinition(catalog), position, player.Position));
        }

        var bench = new List<PlayerDefinition>();
        if (catalog is not null)
        {
            for (int i = 0; i < available.Count; i++)
            {
                if (!Contains(starters, available[i].Id))
                {
                    bench.Add(available[i].ToDefinition(catalog));
                }
            }
        }

        return new MatchLineup(definitions, bench, new Lineup(slots), emergencyGoalkeeperId);
    }

    /// <summary>
    /// Titulares: primero los de la alineación guardada que siguen disponibles, en su orden, y luego se
    /// completa con los disponibles de menor id hasta 7 (o hasta agotarlos, RF-002d).
    /// </summary>
    private static List<RunPlayer> SelectStarters(RunState state, IReadOnlyList<RunPlayer> available)
    {
        var starters = new List<RunPlayer>(RunRules.MaxStarters);
        var slots = state.Lineup.Slots;
        for (int i = 0; i < slots.Count && starters.Count < RunRules.MaxStarters; i++)
        {
            var player = state.FindPlayer(slots[i].PlayerId);
            if (player is not null && player.IsAvailable && !Contains(starters, player.Id))
            {
                starters.Add(player);
            }
        }

        // Relleno por rol, para que una plantilla sin alineación guardada salga con forma de equipo y
        // no con siete centrocampistas: portero, defensas, centrocampistas, delantero, y el resto.
        AddByPosition(starters, available, Position.Goalkeeper, 1);
        AddByPosition(starters, available, Position.Defender, DefenderCells.Length);
        AddByPosition(starters, available, Position.Midfielder, MidfielderCells.Length);
        AddByPosition(starters, available, Position.Forward, ForwardCells.Length);

        for (int i = 0; i < available.Count && starters.Count < RunRules.MaxStarters; i++)
        {
            if (!Contains(starters, available[i].Id))
            {
                starters.Add(available[i]);
            }
        }

        return starters;
    }

    private static void AddByPosition(List<RunPlayer> starters, IReadOnlyList<RunPlayer> available, Position position, int max)
    {
        int already = 0;
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Position == position)
            {
                already++;
            }
        }

        for (int i = 0; i < available.Count && starters.Count < RunRules.MaxStarters && already < max; i++)
        {
            if (available[i].Position == position && !Contains(starters, available[i].Id))
            {
                starters.Add(available[i]);
                already++;
            }
        }
    }

    /// <summary>Portero titular: el de verdad si hay alguno entre los titulares, y si no, el de menor id.</summary>
    private static RunPlayer PickGoalkeeper(List<RunPlayer> starters)
    {
        RunPlayer? goalkeeper = null;
        for (int i = 0; i < starters.Count; i++)
        {
            if (starters[i].Position != Position.Goalkeeper)
            {
                continue;
            }

            if (goalkeeper is null || starters[i].Id < goalkeeper.Id)
            {
                goalkeeper = starters[i];
            }
        }

        if (goalkeeper is not null)
        {
            return goalkeeper;
        }

        var emergency = starters[0];
        for (int i = 1; i < starters.Count; i++)
        {
            if (starters[i].Id < emergency.Id)
            {
                emergency = starters[i];
            }
        }

        return emergency;
    }

    private static Cell CellFor(Position position, List<Cell> taken)
    {
        var preferred = position switch
        {
            Position.Defender => DefenderCells,
            Position.Midfielder => MidfielderCells,
            Position.Forward => ForwardCells,
            _ => DefenderCells,
        };

        for (int i = 0; i < preferred.Length; i++)
        {
            if (!taken.Contains(preferred[i]))
            {
                return preferred[i];
            }
        }

        for (int i = 0; i < OutfieldCells.Length; i++)
        {
            if (!taken.Contains(OutfieldCells[i]))
            {
                return OutfieldCells[i];
            }
        }

        throw new InvalidOperationException("no quedan casillas libres para colocar a un titular");
    }

    /// <summary>
    /// Cambia la posición de una definición solo para este partido, manteniendo <c>Tags</c> coherente
    /// (la etiqueta de posición forma parte de las etiquetas del jugador, ADR 0024).
    /// </summary>
    private static PlayerDefinition Repositioned(PlayerDefinition definition, Position position, Position original)
    {
        if (position == original)
        {
            return definition;
        }

        var tags = new List<string>(definition.Tags.Count);
        for (int i = 0; i < definition.Tags.Count; i++)
        {
            tags.Add(string.Equals(definition.Tags[i], original.ToString(), StringComparison.Ordinal)
                ? position.ToString()
                : definition.Tags[i]);
        }

        return definition with { Position = position, Tags = tags };
    }

    private static bool Contains(IReadOnlyList<RunPlayer> players, int id)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
