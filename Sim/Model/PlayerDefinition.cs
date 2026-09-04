namespace Underleague.Sim.Model;

/// <summary>Definición estática de un jugador (atributos, rasgos, etiquetas) usada como entrada del simulador.</summary>
public sealed record PlayerDefinition(
    int Id,
    string Name,
    Race Race,
    Position Position,
    Rarity Rarity,
    int Level,
    Attributes Attributes,
    IReadOnlyList<Trait> Traits,
    IReadOnlyList<string> Tags,
    PhysicalState PhysicalState)
{
    /// <summary>Contadores vacíos compartidos: el caso normal es que un jugador no tenga ninguno.</summary>
    private static readonly IReadOnlyDictionary<string, int> NoCounters =
        new SortedDictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Perks asignados al jugador (RF-071), por id de <c>data/perks/</c>. Se declara como propiedad
    /// <c>init</c> y no como parámetro posicional para que las construcciones existentes sigan valiendo
    /// y para poder escribir <c>definition with { Perks = ["bloodlust"] }</c>.
    /// El número máximo depende de la rareza (RF-023, Progression.PerkSlots); lo valida Simulator.Run.
    /// </summary>
    public IReadOnlyList<string> Perks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Contadores acumulados entre partidos de la misma run (RF-070, §6). Inmutable y ordenado por clave
    /// ordinal: úsese <see cref="WithCounters"/> para construirlo, nunca un Dictionary sin orden.
    /// </summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = NoCounters;

    /// <summary>True si tag está en Tags (raza, posición y rasgos, como strings; RF-022d).</summary>
    public bool HasTag(string tag) => Tags.Contains(tag);

    /// <summary>Copia del jugador con los contadores indicados, ordenados por clave ordinal.</summary>
    public PlayerDefinition WithCounters(IEnumerable<KeyValuePair<string, int>> counters)
    {
        ArgumentNullException.ThrowIfNull(counters);
        var sorted = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in counters)
        {
            sorted[name] = value;
        }

        return this with { Counters = sorted };
    }
}
