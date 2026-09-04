namespace Underleague.Sim.Model;

/// <summary>Raza de un jugador (RF-004). Prototipo (D-5): Human, Orc, Elf.</summary>
public enum Race
{
    Human,
    Orc,
    Elf,
    Dwarf,
    Undead,
    DarkElf,
    Demon,
    Vampire,
    Lizard,
}

/// <summary>Posición en el campo.</summary>
public enum Position
{
    Goalkeeper,
    Defender,
    Midfielder,
    Forward,
}

/// <summary>
/// Rareza de un jugador, perk, objeto o consumible (RF-005, RF-023, ADR 0039).
///
/// <para><b>Tres rarezas generables</b> —<see cref="Common"/>, <see cref="Uncommon"/> y
/// <see cref="Rare"/>, con 2, 3 y 4 slots de perk (<c>Progression.PerkSlots</c>)— y una cuarta,
/// <see cref="Legendary"/>, que <b>la generación nunca produce</b>: los legendarios son personajes
/// únicos escritos a mano que se desbloquean ganando divisiones (ADR 0039, fase 4). Ni el generador de
/// equipos, ni el mercado, ni las recompensas, ni el pool de objetos la sortean jamás; existe en el
/// enum para que el escalón esté reservado y para que precios, presupuestos y slots ya tengan su
/// entrada cuando la fase 4 escriba los personajes.</para>
/// </summary>
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Legendary,
}

/// <summary>
/// Etiqueta de estilo individual (ADR 0024): una por jugador, sorteada con la distribución de la raza
/// (<c>data/races/*.json</c>, <c>styleTagWeights</c>) e independiente de la etiqueta de especie fija.
/// Consultada por las sinergias de los perks universales; su sesgo de atributos vive en
/// <c>data/tags/styles.json</c> (Sim.Data.StyleDefinition).
/// </summary>
public enum StyleTag
{
    Brute,
    Fine,
    Bulwark,
    Cold,
    Neutral,
}

/// <summary>Estado físico de un jugador entre partidos (RF-093).</summary>
public enum PhysicalState
{
    Healthy,
    MinorInjury,
    SevereInjury,
    Dead,
}

/// <summary>Rasgo de jugador (incluye los de portero: Cat, Wall, Rusher).</summary>
public enum Trait
{
    Aggressive,
    Fast,
    Scorer,
    LongShot,
    Cerebral,
    Dirty,
    Resilient,
    Coward,
    Leader,
    Lazy,
    Cat,
    Wall,
    Rusher,
}

/// <summary>Rasgo del árbitro de un partido.</summary>
public enum RefereeTrait
{
    Neutral,
    Strict,
    Lenient,
    Homer,
    OneEyed,
    Cowardly,
    Corrupt,
    Incorruptible,
}

/// <summary>Tercio del campo relativo a un equipo.</summary>
public enum Zone
{
    Own,
    Middle,
    Opposing,
}
