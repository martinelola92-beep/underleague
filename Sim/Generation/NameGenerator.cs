using Underleague.Sim.Data;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>
/// Genera nombres "Nombre Apellido" a partir de las listas de una raza. No garantiza unicidad por sí
/// mismo; el llamador (TeamGenerator) reintenta hasta no repetir dentro de un equipo.
///
/// <para>El idioma es de presentación, nunca de estado (RT-073): se sortean un <b>índice</b> de nombre
/// de pila y un <b>índice</b> de apellido, no una cadena, así que el idioma activo no cambia cuánto avanza
/// el RNG. <see cref="Next"/> devuelve las dos variantes ya montadas (<see cref="LocalizedName.Es"/> y
/// <see cref="LocalizedName.En"/>); quien la consuma elige cuál mostrar sin volver a tirar el dado.</para>
/// </summary>
public sealed class NameGenerator
{
    private readonly RaceDefinition _race;

    public NameGenerator(RaceDefinition race)
    {
        _race = race;
    }

    /// <summary>Siguiente nombre aleatorio "Nombre Apellido" de la raza, en los dos idiomas.</summary>
    public LocalizedName Next(ref Pcg32 rng)
    {
        int firstIndex = rng.Range(0, _race.FirstNames.Count);
        int lastIndex = rng.Range(0, _race.LastNames.Count);
        string es = $"{_race.FirstNames.Es[firstIndex]} {_race.LastNames.Es[lastIndex]}";
        string en = $"{_race.FirstNames.En[firstIndex]} {_race.LastNames.En[lastIndex]}";
        return new LocalizedName(es, en);
    }
}
