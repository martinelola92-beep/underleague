using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>Estado de un némesis (ADR 0165): vivo, o muerto a manos del jugador (deja de serlo).</summary>
public enum NemesisStatus
{
    Active,
    Slain,
}

/// <summary>
/// Un puesto de un clan cuyo jugador de datos ya no juega en él (ADR 0165, «Los rivales tienen memoria»):
/// murió, o se lo llevó otro clan como némesis, o lo desplazó un némesis. Lo ocupa un <b>fichaje</b> con
/// las mismas cifras y un nombre generado por raza. <see cref="Generation"/> cuenta cuántos fichajes ha
/// habido ya en el puesto (0 = el primero): si el fichaje también muere, el siguiente tiene otro nombre.
/// </summary>
public sealed record RivalVacancy(string ClanId, int Slot, int Generation);

/// <summary>
/// Un jugador rival que ha matado a uno de los tuyos (ADR 0165). Vive en <see cref="RunState.RivalMemory"/>
/// desde que se convierte en némesis y se conserva al morir (<see cref="NemesisStatus.Slain"/>): la Gaceta y
/// la venganza lo leen. Identidad = (<see cref="HomeClanId"/>, <see cref="HomeSlot"/>) al nacer; juega hoy
/// en (<see cref="ClanId"/>, <see cref="Slot"/>), que cambia con cada traspaso entre actos.
/// </summary>
/// <param name="Id">Número de orden (1, 2, ...) en la run; nunca se reutiliza.</param>
/// <param name="TitleId">Título de <c>data/nemesis/titles.json</c>.</param>
/// <param name="Name">Nombre del jugador rival (es, como el resto de nombres de jugador dentro de <c>/Sim</c>).</param>
/// <param name="Position">Su puesto; el traspaso sólo lo coloca en un puesto igual.</param>
/// <param name="HomeClanId">Clan en el que nació.</param>
/// <param name="HomeSlot">Puesto (índice 0..9 de <c>data/rivals</c>) en el que jugaba al matar.</param>
/// <param name="ClanId">Clan en el que juega ahora.</param>
/// <param name="Slot">Puesto en el que juega ahora.</param>
/// <param name="VictimName">Nombre de la primera víctima propia, para «mató a X en el acto N».</param>
/// <param name="VictimPlayerId">Id de esa víctima en la plantilla.</param>
/// <param name="Act">Acto en el que la mató.</param>
/// <param name="Kills">Muertes propias que suma en la run (empieza en 1).</param>
/// <param name="Status">Vivo o muerto.</param>
public sealed record RivalNemesis(
    int Id,
    string TitleId,
    string Name,
    Position Position,
    string HomeClanId,
    int HomeSlot,
    string ClanId,
    int Slot,
    string VictimName,
    int VictimPlayerId,
    int Act,
    int Kills,
    NemesisStatus Status)
{
    /// <summary>True mientras siga vivo.</summary>
    public bool IsActive => Status == NemesisStatus.Active;
}

/// <summary>
/// La memoria de los clanes rivales durante una run (ADR 0165, RF-015 enmendada), estado guardado desde
/// la versión 8 del esquema: qué puestos ya no los ocupa su jugador de datos y qué jugadores rivales han
/// pasado a ser némesis. Las lesiones de los rivales <b>no</b> se arrastran (curan entre partidos).
/// Inmutable y ordenada (RT-041): vacantes por clan ordinal y puesto, némesis por id.
/// </summary>
public sealed record RivalMemory
{
    /// <summary>Sin memoria: todos los clanes tal y como los escribe <c>/data</c>.</summary>
    public static RivalMemory Empty { get; } = new();

    /// <summary>Puestos cuyo jugador de datos ya no juega, ordenados por clan ordinal y puesto.</summary>
    public IReadOnlyList<RivalVacancy> Vacancies { get; init; } = Array.Empty<RivalVacancy>();

    /// <summary>Todos los némesis de la run (vivos y muertos), ordenados por id.</summary>
    public IReadOnlyList<RivalNemesis> Nemeses { get; init; } = Array.Empty<RivalNemesis>();

    /// <summary>Némesis vivos, por id.</summary>
    public IReadOnlyList<RivalNemesis> Alive
    {
        get
        {
            var alive = new List<RivalNemesis>();
            for (int i = 0; i < Nemeses.Count; i++)
            {
                if (Nemeses[i].IsActive)
                {
                    alive.Add(Nemeses[i]);
                }
            }

            return alive;
        }
    }

    /// <summary>La vacante de ese puesto, o null si lo ocupa su jugador de datos.</summary>
    public RivalVacancy? VacancyAt(string clanId, int slot)
    {
        for (int i = 0; i < Vacancies.Count; i++)
        {
            if (Vacancies[i].Slot == slot && string.Equals(Vacancies[i].ClanId, clanId, StringComparison.Ordinal))
            {
                return Vacancies[i];
            }
        }

        return null;
    }

    /// <summary>El némesis vivo que juega hoy en ese puesto, o null.</summary>
    public RivalNemesis? ActiveAt(string clanId, int slot)
    {
        for (int i = 0; i < Nemeses.Count; i++)
        {
            var n = Nemeses[i];
            if (n.IsActive && n.Slot == slot && string.Equals(n.ClanId, clanId, StringComparison.Ordinal))
            {
                return n;
            }
        }

        return null;
    }

    /// <summary>El némesis con ese id, o null.</summary>
    public RivalNemesis? Find(int id)
    {
        for (int i = 0; i < Nemeses.Count; i++)
        {
            if (Nemeses[i].Id == id)
            {
                return Nemeses[i];
            }
        }

        return null;
    }

    /// <summary>Némesis vivos que juegan hoy en ese clan, por id.</summary>
    public IReadOnlyList<RivalNemesis> ActiveIn(string clanId)
    {
        var list = new List<RivalNemesis>();
        for (int i = 0; i < Nemeses.Count; i++)
        {
            if (Nemeses[i].IsActive && string.Equals(Nemeses[i].ClanId, clanId, StringComparison.Ordinal))
            {
                list.Add(Nemeses[i]);
            }
        }

        return list;
    }

    /// <summary>Copia con esa vacante fijada (sustituye la del mismo puesto), reordenada.</summary>
    public RivalMemory WithVacancy(RivalVacancy vacancy)
    {
        ArgumentNullException.ThrowIfNull(vacancy);
        var list = new List<RivalVacancy>(Vacancies.Count + 1);
        for (int i = 0; i < Vacancies.Count; i++)
        {
            if (!(Vacancies[i].Slot == vacancy.Slot && string.Equals(Vacancies[i].ClanId, vacancy.ClanId, StringComparison.Ordinal)))
            {
                list.Add(Vacancies[i]);
            }
        }

        list.Add(vacancy);
        list.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.ClanId, b.ClanId);
            return c != 0 ? c : a.Slot.CompareTo(b.Slot);
        });
        return this with { Vacancies = list };
    }

    /// <summary>Copia con ese némesis añadido o sustituido (por id), reordenada por id.</summary>
    public RivalMemory WithNemesis(RivalNemesis nemesis)
    {
        ArgumentNullException.ThrowIfNull(nemesis);
        var list = new List<RivalNemesis>(Nemeses.Count + 1);
        for (int i = 0; i < Nemeses.Count; i++)
        {
            if (Nemeses[i].Id != nemesis.Id)
            {
                list.Add(Nemeses[i]);
            }
        }

        list.Add(nemesis);
        list.Sort((a, b) => a.Id.CompareTo(b.Id));
        return this with { Nemeses = list };
    }
}
