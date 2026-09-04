using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Sim.Run.Systems.Items;

/// <summary>
/// Puente entre lo que la run guarda (ids: <c>RunPlayer.Item</c>, <c>RunState.Consumables</c>) y lo que
/// el partido consume (<see cref="MatchItem"/> y <see cref="MatchConsumable"/>, ya resueltos). Es lo que
/// cierra el agujero que dejaron los paquetes W y X: "el estado los guarda pero <c>MatchSetup</c> no los
/// recibe".
///
/// <para><b>De dónde salen los catálogos.</b> De la <b>instantánea de <c>/data</c> de la propia run</b>
/// (<c>RunState.DataSnapshot</c>, RT-061b), no de <c>/data</c> ni de <c>Catalog</c>. Tres razones: (1)
/// <c>/Sim</c> no hace E/S (RT-012), así que alguien tiene que traer el texto ya leído y la run ya lo
/// trae; (2) cargar una run vieja debe usar los objetos con los que se empezó, no los de hoy; (3)
/// <c>Catalog</c> no gana ningún campo, que es lo que el paquete X decidió (X-1) para no chocar con los
/// agentes que trabajan en <c>Sim/Data</c>. <c>RunState.WithDataSnapshot</c> construye esto una sola vez
/// por run y lo lleva consigo.</para>
///
/// <para>Un estado <b>sin</b> instantánea (modo de depuración, RT-062, W-17) juega sin equipamiento:
/// <see cref="None"/>. Es explícito y no silencioso: <see cref="MatchItemOf"/> devuelve null porque no
/// hay catálogo, mientras que con catálogo un id desconocido <b>lanza</b>.</para>
/// </summary>
public sealed class RunEquipment
{
    private readonly ItemCatalog? _items;
    private readonly ConsumableCatalog? _consumables;

    private RunEquipment(ItemCatalog? items, ConsumableCatalog? consumables)
    {
        _items = items;
        _consumables = consumables;
    }

    /// <summary>Sin catálogos: una run sin instantánea de <c>/data</c> juega sin objetos ni consumibles.</summary>
    public static RunEquipment None { get; } = new(null, null);

    /// <summary>True si hay catálogos cargados.</summary>
    public bool IsEmpty => _items is null && _consumables is null;

    /// <summary>Catálogo de objetos de la instantánea, o null si la run no la trae.</summary>
    public ItemCatalog? Items => _items;

    /// <summary>Catálogo de consumibles de la instantánea, o null si la run no la trae.</summary>
    public ConsumableCatalog? Consumables => _consumables;

    /// <summary>
    /// Construye los catálogos de una instantánea de <c>/data</c> (el mismo diccionario ruta -&gt;
    /// contenido que consume <c>DataLoader.FromJson</c>). Tolera que no haya ninguna de las dos carpetas
    /// —una instantánea parcial es lo normal en los tests— pero no tolera un fichero inválido: eso sigue
    /// siendo un <c>DataException</c> con fichero y ruta (RT-032).
    /// </summary>
    public static RunEquipment FromSnapshot(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var items = Has(files, "items/") ? ItemLoader.FromJson(files) : null;
        var consumables = Has(files, "consumables/") ? ConsumableLoader.FromJson(files) : null;
        return items is null && consumables is null ? None : new RunEquipment(items, consumables);
    }

    /// <summary>
    /// Objeto equipado de un jugador, listo para el partido (RF-075..078); null si no lleva ninguno o si
    /// la run no trae catálogo. Un id que no está en el catálogo es un error explícito, nunca un objeto
    /// que calla y no hace nada.
    /// </summary>
    public MatchItem? MatchItemOf(string? itemId)
    {
        if (itemId is null || _items is null)
        {
            return null;
        }

        var item = _items.Find(itemId)
            ?? throw new InvalidOperationException(
                $"el jugador lleva el objeto '{itemId}', que no está en la instantánea de data/items/ de esta run");

        return ToMatchItem(item);
    }

    /// <summary>Convierte un objeto del catálogo en su forma de partido (RF-077, arquetipos incluidos).</summary>
    public static MatchItem ToMatchItem(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new MatchItem(item.Id, item.Rarity, item.Effects)
        {
            // El maldito lleva su contrapartida puesta siempre; el frágil y el restringido no tienen
            // ninguna (lo valida ItemLoader), así que esta lista está vacía en todos los demás.
            DrawbackEffects = item.DrawbackEffects,
            RequiredTag = item.RequiredTag,
        };
    }

    /// <summary>
    /// Consumibles equipados listos para el partido (RF-080..085). El modo manda sobre el disparador: un
    /// slot manual (RF-082) se resuelve con la activación que traiga
    /// <paramref name="manualActivations"/> —el tick en el que el jugador lo pulsó— y nunca solo; un slot
    /// condicional (RF-081) se resuelve con su disparador (RF-083).
    ///
    /// <para>Se conserva el orden en el que están equipados, que es el orden en el que el motor los
    /// resuelve si dos se disparan en el mismo tick.</para>
    /// </summary>
    public IReadOnlyList<MatchConsumable> ForMatch(
        IReadOnlyList<EquippedConsumable> equipped,
        IReadOnlyList<ManualActivation>? manualActivations = null)
    {
        ArgumentNullException.ThrowIfNull(equipped);
        if (equipped.Count == 0 || _consumables is null)
        {
            return Array.Empty<MatchConsumable>();
        }

        var result = new List<MatchConsumable>(equipped.Count);
        for (int i = 0; i < equipped.Count; i++)
        {
            var slot = equipped[i];
            var definition = _consumables.Find(slot.Id)
                ?? throw new InvalidOperationException(
                    $"el consumible equipado '{slot.Id}' no está en la instantánea de data/consumables/ de esta run");

            var (trigger, threshold) = slot.Mode == ConsumableMode.Manual
                ? (ConsumableTrigger.Manual, 0)
                : ConsumableTriggers.Parse(slot.Trigger);

            result.Add(new MatchConsumable(definition.Id, definition.Rarity, definition.Effects, trigger)
            {
                Threshold = threshold,
                ManualTick = trigger == ConsumableTrigger.Manual ? TickOf(manualActivations, slot.Id) : -1,
            });
        }

        return result;
    }

    private static int TickOf(IReadOnlyList<ManualActivation>? activations, string id)
    {
        if (activations is null)
        {
            return -1;
        }

        for (int i = 0; i < activations.Count; i++)
        {
            if (string.Equals(activations[i].ConsumableId, id, StringComparison.Ordinal))
            {
                return activations[i].Tick;
            }
        }

        return -1;
    }

    private static bool Has(IReadOnlyDictionary<string, string> files, string prefix)
    {
        foreach (string path in files.Keys)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
