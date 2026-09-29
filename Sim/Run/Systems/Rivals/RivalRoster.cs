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
    /// Desplazamiento de la semilla de los nombres de fichaje (flujo propio de la run, RT-022): 950 M queda
    /// muy por encima de <c>nodo * 10000 + reroll</c> de cualquier flujo de recompensas.
    /// </summary>
    private const int SigningStreamBase = 950_000_000;

    /// <summary>Intentos de sortear un nombre que no repita a nadie del equipo antes de aceptar el último.</summary>
    private const int SigningNameAttempts = 32;

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
    /// Nombre del fichaje de una vacante: sale de las listas de la raza con un flujo de la semilla de la run
    /// para (clan, puesto, generación), sin repetir el nombre de nadie de los datos del equipo.
    /// </summary>
    public static string SigningName(RivalTeam team, RivalVacancy vacancy, ulong seed, Catalog catalog)
    {
        var generator = new NameGenerator(catalog.Race(team.Race));
        var rng = RngStreams.Rewards(seed, checked(
            SigningStreamBase + (ClanKey(vacancy.ClanId) * 1000) + (vacancy.Slot * 100) + vacancy.Generation));
        string name = string.Empty;
        for (int attempt = 0; attempt < SigningNameAttempts; attempt++)
        {
            name = generator.Next(ref rng).Es;
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

    /// <summary>Clave numérica estable (0..9999) de un id de clan: FNV-1a sobre sus caracteres, no <c>GetHashCode</c> (RT-021).</summary>
    private static int ClanKey(string clanId)
    {
        uint hash = 2166136261;
        for (int i = 0; i < clanId.Length; i++)
        {
            hash = (hash ^ clanId[i]) * 16777619;
        }

        return (int)(hash % 9_000);
    }
}
