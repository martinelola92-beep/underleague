namespace Underleague.Sim.Analysis;

/// <summary>
/// Densidad de build alcanzable en un punto de la run (ADR 0040): cuántos perks caben en el once,
/// cuántos titulares llevan objeto y hasta dónde han podido crecer los contadores de acumulación.
///
/// <para><b>Por qué existe.</b> La tabla de exigencia de la ADR 0033 no cambia; lo que cambia es
/// <b>con qué material se mide</b>. Las builds de <c>data/balance/builds/</c> tienen los catorce slots
/// llenos porque representan una plantilla terminada, y medir con ellas contra el jefe del acto 1 es
/// comparar al jugador del acto 1 con una plantilla que solo existe en el acto 3. Cada celda de la tabla
/// se instancia con la densidad de su acto, y esas doce variantes por raza se <b>derivan de la completa
/// quitando piezas</b>, nunca se escriben a mano: si se escribieran, el escalón de calidad dejaría de
/// ser el mismo entre actos y la comparación no mediría lo que dice medir.</para>
///
/// <para><b>Qué pieza se quita.</b> Los perks se recortan <b>en rondas por titular</b> —un perk de cada
/// slot, luego otro, luego otro—, que es como los reparte una run: una recompensa por victoria,
/// entregada al titular con menos perks. Lo que <b>no</b> se hace es recortar por el orden del fichero,
/// que agrupa por titular y dejaría al portero y a los dos centrales terminados y al delantero vacío.
/// Dentro de cada titular se conserva <b>el último</b> perk de la lista: los cuatro escalones escriben
/// primero el perk de base y después el que define ese escalón, así que quitar por delante deja la build
/// de cinco perks <b>bien elegidos</b> que la ADR 0040 describe, y no cinco perks que los cuatro
/// escalones comparten y que borrarían la escalera. Los objetos se quedan en los slots de índice más
/// bajo, que es el orden en el que la política automática equipa (por id ascendente). Los contadores se
/// acotan a lo que cabe en los partidos jugados hasta esa puerta.</para>
/// </summary>
/// <param name="Perks">Perks totales en el once.</param>
/// <param name="Items">Titulares con objeto (RF-076: uno como mucho por jugador).</param>
/// <param name="CounterCap">Tope de cada contador de acumulación (RF-070).</param>
public sealed record BuildDensity(int Perks, int Items, int CounterCap)
{
    /// <summary>La build completa, sin recortar: lo que el fichero declara.</summary>
    public static BuildDensity Full { get; } = new(int.MaxValue, int.MaxValue, int.MaxValue);

    /// <summary>True si no recorta nada.</summary>
    public bool IsFull => Perks == int.MaxValue && Items == int.MaxValue && CounterCap == int.MaxValue;

    /// <summary>
    /// Recorta las asignaciones de perk a <see cref="Perks"/> repartiendo por rondas entre los slots que
    /// aparecen, en orden de slot ascendente y conservando dentro de cada slot el orden del fichero.
    /// </summary>
    public IReadOnlyList<T> TrimPerks<T>(IReadOnlyList<T> assignments, Func<T, int> slotOf)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(slotOf);
        if (Perks >= assignments.Count)
        {
            return assignments;
        }

        var bySlot = new SortedDictionary<int, List<T>>();
        foreach (var assignment in assignments)
        {
            int slot = slotOf(assignment);
            if (!bySlot.TryGetValue(slot, out var list))
            {
                list = new List<T>();
                bySlot[slot] = list;
            }

            list.Add(assignment);
        }

        var kept = new List<T>(Perks);
        int deepest = 0;
        foreach (var list in bySlot.Values)
        {
            deepest = Math.Max(deepest, list.Count);
        }

        for (int round = 0; round < deepest && kept.Count < Perks; round++)
        {
            foreach (var list in bySlot.Values)
            {
                if (kept.Count >= Perks)
                {
                    break;
                }

                if (round < list.Count)
                {
                    kept.Add(list[list.Count - 1 - round]);
                }
            }
        }

        return kept;
    }

    /// <summary>Recorta el equipamiento a <see cref="Items"/> titulares, quedándose con los slots más bajos.</summary>
    public IReadOnlyDictionary<int, string> TrimItems(IReadOnlyDictionary<int, string>? items)
    {
        if (items is null || items.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        if (Items >= items.Count)
        {
            return items;
        }

        var kept = new SortedDictionary<int, string>();
        foreach (var (slot, id) in items.OrderBy(e => e.Key))
        {
            if (kept.Count >= Items)
            {
                break;
            }

            kept[slot] = id;
        }

        return kept;
    }

    /// <summary>Acota cada contador de acumulación a <see cref="CounterCap"/>; quita los que quedan a cero.</summary>
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>> CapCounters(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>>? counters)
    {
        var result = new SortedDictionary<int, IReadOnlyDictionary<string, int>>();
        if (counters is null || counters.Count == 0 || CounterCap <= 0)
        {
            return result;
        }

        foreach (var (slot, values) in counters.OrderBy(e => e.Key))
        {
            var capped = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var (name, value) in values.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                int amount = Math.Min(value, CounterCap);
                if (amount > 0)
                {
                    capped[name] = amount;
                }
            }

            if (capped.Count > 0)
            {
                result[slot] = capped;
            }
        }

        return result;
    }
}
