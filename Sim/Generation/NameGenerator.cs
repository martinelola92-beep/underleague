using Underleague.Sim.Data;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>
/// Genera nombres "Nombre Apellido" a partir de las listas de una raza. No garantiza unicidad por sí
/// mismo; el llamador (TeamGenerator) reintenta hasta no repetir dentro de un equipo.
/// </summary>
public sealed class NameGenerator
{
    private readonly RaceDefinition _race;

    public NameGenerator(RaceDefinition race)
    {
        _race = race;
    }

    /// <summary>Siguiente nombre aleatorio "Nombre Apellido" de la raza.</summary>
    public string Next(ref Pcg32 rng)
    {
        string first = rng.Pick(_race.FirstNames);
        string last = rng.Pick(_race.LastNames);
        return $"{first} {last}";
    }
}
