using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>
/// Quién juega de verdad en un puesto de un rival (ADR 0165): el jugador de datos, un <b>fichaje</b> que
/// cubre a un muerto o a uno que se ha ido, o un <b>némesis</b> (con el nombre y el nivel de némesis).
/// </summary>
/// <param name="Slot">Índice 0..9 del puesto dentro de <c>data/rivals/&lt;id&gt;.json</c>.</param>
/// <param name="Name">Nombre con el que juega.</param>
/// <param name="Nemesis">El némesis vivo que lo ocupa, o null.</param>
/// <param name="IsSigning">True si es un fichaje que cubre una vacante.</param>
public sealed record RivalOccupant(int Slot, string Name, RivalNemesis? Nemesis, bool IsSigning);

/// <summary>
/// Resuelve quién ocupa cada puesto de un rival dada la <see cref="RivalMemory"/> de la run (ADR 0165).
/// Función pura: sin RNG compartido —los nombres de los fichajes salen de un flujo derivado de la semilla de
/// la run y de (clan, puesto, generación), así que el mismo fichaje se llama igual en cada partido— y sin E/S.
/// </summary>
public static class RivalRoster
{
    /// <summary>
    /// Generaciones de fichaje por puesto que caben en la reserva de nombres del clan. Un puesto que se vacía más
    /// veces que esto sigue teniendo nombre propio, pero ya no fuera del alcance del club (BA-G, ADR 0169).
    /// </summary>
    public const int SigningReservedGenerations = 4;

    /// <summary>Puestos de un clan (data/rivals: diez jugadores).</summary>
    private const int Slots = 10;

    /// <summary>
    /// Billetes reservados a los fichajes de un clan: <c>puestos x generaciones</c> (BA-G, ADR 0169). El club no puede
    /// llevar ninguno de los nombres de esos billetes (<see cref="Underleague.Sim.Run.Systems.RunNames"/>), así que un
    /// fichaje rival nunca repite a un jugador del club aunque se derive sin mirar la plantilla.
    /// </summary>
    public const int SigningPoolTickets = Slots * SigningReservedGenerations;

    /// <summary>
    /// Separación entre el billete de un fichaje y el siguiente intento si el nombre coincide con un jugador de
    /// datos del clan. Mayor que cualquier billete de un puesto hasta la generación 9 (<c>generación x 10 + puesto</c>
    /// &lt; 100), así que hasta ahí los intentos de dos fichajes distintos nunca se pisan; más allá, un puesto vaciado diez
    /// veces en una run, deja de estar garantizado. Con los datos de hoy la generación máxima medida en 120 runs es 1.
    /// </summary>
    private const int ProbeStep = 100;

    /// <summary>Intentos de dar con un nombre que no repita a nadie de los datos del equipo antes de aceptar el último.</summary>
    private const int SigningNameAttempts = 3;

    /// <summary>Quién juega en cada uno de los diez puestos de este rival, por índice de puesto.</summary>
    public static IReadOnlyList<RivalOccupant> Resolve(RivalTeam team, RivalMemory memory, ulong seed, Catalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(memory);

        var occupants = new List<RivalOccupant>(team.Players.Count);
        for (int slot = 0; slot < team.Players.Count; slot++)
        {
            var nemesis = memory.ActiveAt(team.ClanId, slot);
            if (nemesis is not null)
            {
                occupants.Add(new RivalOccupant(slot, nemesis.Name, nemesis, IsSigning: false));
                continue;
            }

            var vacancy = memory.VacancyAt(team.ClanId, slot);
            if (vacancy is null)
            {
                occupants.Add(new RivalOccupant(slot, team.Players[slot].Name, null, IsSigning: false));
                continue;
            }

            if (catalog is null)
            {
                throw new ArgumentNullException(nameof(catalog), "un fichaje necesita las listas de nombres de su raza");
            }

            occupants.Add(new RivalOccupant(slot, SigningName(team, vacancy, seed, catalog), null, IsSigning: true));
        }

        return occupants;
    }

    /// <summary>
    /// Nombre del fichaje de una vacante: el del <b>billete</b> <c>generación x 10 + puesto</c> del retículo de la raza
    /// (<see cref="NameLattice.NameOfTicket"/>), sin repetir el nombre de nadie de los datos del equipo. Billetes
    /// distintos dan nombres distintos, así que dos fichajes del mismo clan nunca se llaman igual sin necesidad de
    /// mirar a los demás: se sigue derivando de (semilla, raza, puesto, generación) y nada más (BA-G, ADR 0169). La clave
    /// es la <b>raza</b> y no el clan porque hoy hay un solo clan por raza (los tres rivales de una raza son el mismo
    /// clan, ADR 0165); si algún día hubiera dos de la misma raza, compartirían billetes y sus fichajes se llamarían igual
    /// de un clan a otro —no dentro de uno—.
    /// </summary>
    public static string SigningName(RivalTeam team, RivalVacancy vacancy, ulong seed, Catalog catalog)
    {
        var lattice = new NameLattice(catalog.Race(team.Race));
        int ticket = (vacancy.Generation * Slots) + vacancy.Slot;
        string name = string.Empty;
        for (int attempt = 0; attempt < SigningNameAttempts; attempt++)
        {
            name = lattice.NameOfTicket(seed, ticket + (attempt * ProbeStep));
            if (!TeamHasName(team, name))
            {
                break;
            }
        }

        return name;
    }

    private static bool TeamHasName(RivalTeam team, string name)
    {
        for (int i = 0; i < team.Players.Count; i++)
        {
            if (string.Equals(team.Players[i].Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
