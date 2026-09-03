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
    /// <summary>True si tag está en Tags (raza, posición y rasgos, como strings; RF-022d).</summary>
    public bool HasTag(string tag) => Tags.Contains(tag);
}
