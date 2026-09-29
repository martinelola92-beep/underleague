using Underleague.Sim.Data;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>
/// El retículo de nombres completos de una raza: todos los «Nombre Apellido» que sus dos listas permiten, numerados
/// de 0 a <see cref="Count"/> - 1 (BA-G, ADR 0169). <see cref="NameGenerator"/> sortea de él sin memoria, así que un
/// mismo nombre puede salir dos veces en una run; esto es lo que permite <b>volver a sortear con memoria</b> sin
/// tocar el flujo de RNG que ya se había gastado, y numerar nombres de forma reproducible por <b>billete</b>.
///
/// <para>Un billete es un entero que la semilla convierte, <b>una a una</b>, en un nombre del retículo: la
/// correspondencia billete → nombre es una biyección afín <c>(a·t + b) mod N</c> con <c>a</c> coprimo de <c>N</c>,
/// así que dos billetes distintos menores que <c>N</c> dan <b>siempre</b> dos nombres distintos, sin estado y sin
/// mirar a nadie. Es lo que hace que los fichajes de un clan rival (ADR 0165), que se derivan sin guardarse, no
/// puedan repetirse entre sí.</para>
///
/// <para>Puro y determinista (RT-021): el español (<see cref="LocalizedName.Es"/>) es el nombre de estado, como en el
/// resto de <c>/Sim</c>; el índice de nombre de pila y el de apellido señalan al mismo jugador en los dos idiomas
/// (RT-073).</para>
/// </summary>
public sealed class NameLattice
{
    private readonly RaceDefinition _race;

    public NameLattice(RaceDefinition race)
    {
        ArgumentNullException.ThrowIfNull(race);
        _race = race;
    }

    /// <summary>Nombres completos distintos que admite la raza: nombres de pila por apellidos.</summary>
    public int Count => _race.FirstNames.Count * _race.LastNames.Count;

    /// <summary>El nombre completo (es) en esa posición del retículo, con <c>0 &lt;= index &lt; <see cref="Count"/></c>.</summary>
    public string NameAt(int index)
    {
        int firstCount = _race.FirstNames.Count;
        if (index < 0 || index >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"el retículo de {_race.Id} tiene {Count} nombres");
        }

        return $"{_race.FirstNames.Es[index % firstCount]} {_race.LastNames.Es[index / firstCount]}";
    }

    /// <summary>
    /// El nombre del billete <paramref name="ticket"/> para esa semilla. Billetes distintos y menores que
    /// <see cref="Count"/> dan nombres distintos; a partir de <see cref="Count"/> se da la vuelta.
    /// </summary>
    public string NameOfTicket(ulong runSeed, int ticket)
    {
        int count = Count;
        if (count == 0)
        {
            throw new InvalidOperationException($"la raza {_race.Id} no tiene nombres");
        }

        var (a, b) = Affine(runSeed);
        long index = ((a * (long)Math.Max(ticket, 0)) + b) % count;
        return NameAt((int)index);
    }

    /// <summary>
    /// Los nombres de los billetes <c>0..<paramref name="tickets"/> - 1</c>: el conjunto que se reserva para quien
    /// los reparte por billete. Conjunto de pertenencia; no se recorre para decidir nada (RT-041).
    /// </summary>
    public HashSet<string> NamesOfTickets(ulong runSeed, int tickets)
    {
        var (a, b) = Affine(runSeed);
        int count = Count;
        var names = new HashSet<string>(Math.Min(tickets, count), StringComparer.Ordinal);
        for (int t = 0; t < tickets && t < count; t++)
        {
            names.Add(NameAt((int)(((a * t) + b) % count)));
        }

        return names;
    }

    /// <summary>
    /// Parámetros de la biyección afín de esta raza y semilla: un flujo propio (<see cref="RngStreams.Names"/>) por
    /// raza, así que no gasta dados de nadie. <c>a</c> se elige entre los coprimos de <see cref="Count"/> con
    /// sorteos sucesivos; <c>a = 1</c> siempre lo es, así que termina.
    /// </summary>
    private (long A, long B) Affine(ulong runSeed)
    {
        int count = Count;
        var rng = RngStreams.Names(runSeed, TicketStreamBase + (int)_race.Id);
        long a = 1;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            long candidate = 1 + rng.Range(0, Math.Max(count - 1, 1));
            if (Gcd(candidate, count) == 1)
            {
                a = candidate;
                break;
            }
        }

        long b = rng.Range(0, count);
        return (a, b);
    }

    /// <summary>Desplazamiento del flujo de billetes por raza, muy por encima de los índices de los nombres sorteados.</summary>
    public const int TicketStreamBase = 900_000_000;

    private static long Gcd(long x, long y)
    {
        while (y != 0)
        {
            (x, y) = (y, x % y);
        }

        return x;
    }
}
