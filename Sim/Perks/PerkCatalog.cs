namespace Underleague.Sim.Perks;

/// <summary>
/// Catálogo de perks cargado de <c>data/perks/*.json</c>. La lista se mantiene ordenada por id ordinal
/// ascendente: cualquier recorrido (informe, <c>--describe</c>, orden de desempate de RT-041) es
/// determinista sin volver a ordenar.
/// </summary>
public sealed class PerkCatalog
{
    private readonly PerkDefinition[] _perks;
    private readonly Dictionary<string, PerkDefinition> _byId;

    /// <summary>Construye el catálogo ordenando por id ordinal; ids repetidos son error del cargador.</summary>
    public PerkCatalog(IEnumerable<PerkDefinition> perks)
    {
        ArgumentNullException.ThrowIfNull(perks);
        _perks = perks.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray();
        _byId = new Dictionary<string, PerkDefinition>(_perks.Length, StringComparer.Ordinal);
        foreach (var perk in _perks)
        {
            _byId.Add(perk.Id, perk);
        }
    }

    /// <summary>Catálogo vacío: el motor con 0 perks no paga ningún coste (§3).</summary>
    public static PerkCatalog Empty { get; } = new(Array.Empty<PerkDefinition>());

    /// <summary>Perks ordenados por id ordinal ascendente.</summary>
    public IReadOnlyList<PerkDefinition> All => _perks;

    /// <summary>Número de perks del catálogo.</summary>
    public int Count => _perks.Length;

    /// <summary>Busca un perk por id; null si no existe.</summary>
    public PerkDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Busca un perk por id; lanza si no existe.</summary>
    public PerkDefinition Get(string id) =>
        Find(id) ?? throw new InvalidOperationException($"perk no encontrado en el catálogo: {id}");
}
