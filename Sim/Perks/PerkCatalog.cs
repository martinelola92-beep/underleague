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
        : this(perks, BuildArcs.None)
    {
    }

    /// <summary>El catálogo con sus arcos de build (ADR 0051): las líneas y la curva de profundidad.</summary>
    public PerkCatalog(IEnumerable<PerkDefinition> perks, BuildArcs arcs)
    {
        ArgumentNullException.ThrowIfNull(perks);
        Arcs = arcs ?? throw new ArgumentNullException(nameof(arcs));
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

    /// <summary>Líneas y curva de profundidad del catálogo (ADR 0051).</summary>
    public BuildArcs Arcs { get; }

    /// <summary>
    /// Perks maestros del catálogo (ADR 0051), por id ascendente. La ADR los acota al 5-10% del catálogo:
    /// esa proporción se vigila con <see cref="MasterSharePercent"/>.
    /// </summary>
    public IReadOnlyList<PerkDefinition> Masters
    {
        get
        {
            var masters = new List<PerkDefinition>();
            for (int i = 0; i < _perks.Length; i++)
            {
                if (_perks[i].IsMaster)
                {
                    masters.Add(_perks[i]);
                }
            }

            return masters;
        }
    }

    /// <summary>Porcentaje del catálogo que son maestros (ADR 0051: entre el 5% y el 10%).</summary>
    public int MasterSharePercent => _perks.Length == 0 ? 0 : Masters.Count * 100 / _perks.Length;

    /// <summary>Miembros de una línea, por id ascendente; vacío si la línea no existe (ADR 0051).</summary>
    public IReadOnlyList<PerkDefinition> MembersOf(string family)
    {
        var members = new List<PerkDefinition>();
        if (string.IsNullOrEmpty(family))
        {
            return members;
        }

        for (int i = 0; i < _perks.Length; i++)
        {
            if (string.Equals(_perks[i].Family, family, StringComparison.Ordinal))
            {
                members.Add(_perks[i]);
            }
        }

        return members;
    }

    /// <summary>Número de perks del catálogo.</summary>
    public int Count => _perks.Length;

    /// <summary>Busca un perk por id; null si no existe.</summary>
    public PerkDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Número de perks de cada eje de activación (docs/perks-ejes.md), indexado por
    /// <see cref="PerkAxis"/>. La distribución objetivo del catálogo se vigila con esto: un catálogo con
    /// la proporción 60/30/10 de RF-069 correcta puede seguir siendo aburrido si todos los perks se
    /// activan por lo mismo.
    /// </summary>
    public int[] CountByAxis()
    {
        var counts = new int[Enum.GetValues<PerkAxis>().Length];
        for (int i = 0; i < _perks.Length; i++)
        {
            counts[(int)_perks[i].Axis]++;
        }

        return counts;
    }

    /// <summary>
    /// Perks que pueden aparecer en el pool de una run de esa raza (ADR 0023): los universales más los
    /// exclusivos de esa raza. Orden de id ordinal ascendente, como <see cref="All"/>.
    /// </summary>
    public IReadOnlyList<PerkDefinition> AvailableTo(Model.Race race)
    {
        var available = new List<PerkDefinition>(_perks.Length);
        for (int i = 0; i < _perks.Length; i++)
        {
            if (_perks[i].Race is null || _perks[i].Race == race)
            {
                available.Add(_perks[i]);
            }
        }

        return available;
    }

    /// <summary>Busca un perk por id; lanza si no existe.</summary>
    public PerkDefinition Get(string id) =>
        Find(id) ?? throw new InvalidOperationException($"perk no encontrado en el catálogo: {id}");
}
