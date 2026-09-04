using Underleague.Sim.Perks;

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
    /// Etiqueta de especie (ADR 0024): fija, la misma para todo jugador de <see cref="Race"/>
    /// (<c>RaceDefinition.SpeciesTag</c>). Se declara como propiedad <c>init</c>, no posicional, por la
    /// misma razón que <see cref="Perks"/>: las construcciones existentes (tests, equipos a mano) siguen
    /// valiendo sin tocarlas. Vacía si el llamador no la ha rellenado.
    /// </summary>
    public string SpeciesTag { get; init; } = string.Empty;

    /// <summary>
    /// Etiqueta de estilo (ADR 0024): una por jugador, sorteada al generarlo (Sim.Generation.PlayerGenerator)
    /// con la distribución de <c>race.StyleTagWeights</c>. Desplaza los atributos del individuo hacia su
    /// estilo (su <c>attributeBias</c> ya está incorporado en <see cref="Attributes"/> al generarse).
    /// Por defecto <see cref="StyleTag.Neutral"/> para las construcciones que no la especifican.
    /// </summary>
    public StyleTag StyleTag { get; init; } = StyleTag.Neutral;

    /// <summary>
    /// Perks asignados al jugador (RF-071), por id de <c>data/perks/</c>. Se declara como propiedad
    /// <c>init</c> y no como parámetro posicional para que las construcciones existentes sigan valiendo
    /// y para poder escribir <c>definition with { Perks = ["bloodlust"] }</c>.
    /// El número máximo depende de la rareza (RF-023, Progression.PerkSlots); lo valida Simulator.Run.
    /// </summary>
    public IReadOnlyList<string> Perks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Objeto equipado, o null (RF-076: uno como máximo). <b>No ocupa slot de perk</b>: no está en
    /// <see cref="Perks"/> y no cuenta para <c>Progression.PerkSlots</c>, exactamente igual que la
    /// habilidad racial (ADR 0026). Llega ya resuelto a <see cref="MatchItem"/> —no como id— porque
    /// <c>Simulator.Run</c> no lee <c>/data</c> (RT-012) y el catálogo de objetos no vive en
    /// <c>Catalog</c>; quien lo resuelve es <c>Sim.Run.Systems.Items.RunEquipment</c> desde la
    /// instantánea de la run (RT-061b).
    /// </summary>
    public MatchItem? Item { get; init; }

    /// <summary>
    /// Contadores acumulados entre partidos de la misma run (RF-070, §6). Inmutable y ordenado por clave
    /// ordinal: úsese <see cref="WithCounters"/> para construirlo, nunca un Dictionary sin orden.
    /// </summary>
    public IReadOnlyDictionary<string, int> Counters { get; init; } = NoCounters;

    /// <summary>
    /// True si tag está en Tags (especie, estilo, posición y rasgos, como strings; RF-022d, ADR 0024).
    /// Sim.Generation.PlayerGenerator compone Tags con [SpeciesTag, StyleTag, Position, ...Traits].
    /// </summary>
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
