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

/// <summary>Rareza de un jugador (RF-005).</summary>
public enum Rarity
{
    Common,
    Rare,
    Legendary,
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
