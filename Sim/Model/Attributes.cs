namespace Underleague.Sim.Model;

/// <summary>Identifica un campo concreto de <see cref="Attributes"/> para acceso genérico.</summary>
public enum AttributeKind
{
    Strength,
    Speed,
    Technique,
    Stamina,
    Leash,
}

/// <summary>Atributos de un jugador. Aritmética entera (RT-023); rango de juego 1..99.</summary>
public readonly record struct Attributes(int Strength, int Speed, int Technique, int Stamina, int Leash)
{
    /// <summary>Lee el atributo indicado por kind.</summary>
    public int Get(AttributeKind kind) => kind switch
    {
        AttributeKind.Strength => Strength,
        AttributeKind.Speed => Speed,
        AttributeKind.Technique => Technique,
        AttributeKind.Stamina => Stamina,
        AttributeKind.Leash => Leash,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Devuelve una copia con el atributo kind sustituido por value.</summary>
    public Attributes With(AttributeKind kind, int value) => kind switch
    {
        AttributeKind.Strength => this with { Strength = value },
        AttributeKind.Speed => this with { Speed = value },
        AttributeKind.Technique => this with { Technique = value },
        AttributeKind.Stamina => this with { Stamina = value },
        AttributeKind.Leash => this with { Leash = value },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Acota todos los atributos al rango de juego 1..99.</summary>
    public static Attributes Clamp(Attributes a) => new(
        Math.Clamp(a.Strength, 1, 99),
        Math.Clamp(a.Speed, 1, 99),
        Math.Clamp(a.Technique, 1, 99),
        Math.Clamp(a.Stamina, 1, 99),
        Math.Clamp(a.Leash, 1, 99));
}
