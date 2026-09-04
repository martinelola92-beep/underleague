using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>
/// Generación procedural de un jugador a partir de raza, posición, rareza y nivel (fase1b-diseno.md §1.3,
/// ADR 0024, ADR 0025, ADR 0027). Orden: posición (decidida por el llamador según la composición del
/// club) -&gt; etiqueta de estilo (sorteo por <c>race.StyleTagWeights</c>) -&gt; rareza (decidida por el
/// llamador) -&gt; atributos (modelo de presupuesto). Solo el sorteo de estilo y el de atributos ocurren
/// aquí; posición y rareza llegan ya resueltas porque dependen de la composición del club, no del
/// individuo.
/// </summary>
public static class PlayerGenerator
{
    private static readonly AttributeKind[] AttributeOrder =
    {
        AttributeKind.Strength, AttributeKind.Speed, AttributeKind.Technique, AttributeKind.Stamina, AttributeKind.Leash,
    };

    /// <summary>
    /// Genera un jugador de nivel <paramref name="level"/> (1..8, RF-023/Progression.MaxLevel) para la
    /// rareza y posición dadas. Tags = [SpeciesTag, StyleTag, Position, ...Traits] (ADR 0024).
    /// </summary>
    public static PlayerDefinition Generate(ref Pcg32 rng, Catalog catalog, RaceDefinition race, Position position, Rarity rarity, int level, int id, string name)
    {
        var styleTag = PickStyleTag(ref rng, race.StyleTagWeights);
        var style = catalog.Style(styleTag);

        var attributes = GenerateAttributes(ref rng, catalog.Tuning.Generation, race, style, position, rarity, level);
        var traits = PickTraits(ref rng, catalog, race, position);

        var tags = new List<string> { race.SpeciesTag, styleTag.ToString(), position.ToString() };
        foreach (var trait in traits)
        {
            tags.Add(trait.ToString());
        }

        return new PlayerDefinition(id, name, race.Id, position, rarity, level, attributes, traits, tags, PhysicalState.Healthy)
        {
            SpeciesTag = race.SpeciesTag,
            StyleTag = styleTag,
        };
    }

    private static StyleTag PickStyleTag(ref Pcg32 rng, IReadOnlyList<(StyleTag Style, int Weight)> weights)
    {
        int total = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            total += weights[i].Weight;
        }

        int r = rng.Range(0, total);
        int cumulative = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i].Weight;
            if (r < cumulative)
            {
                return weights[i].Style;
            }
        }

        return weights[^1].Style;
    }

    /// <summary>
    /// Modelo de presupuesto (fase1b-diseno.md §1.3, ADR 0025, ADR 0027).
    ///
    /// 1. <c>budget = budgetByRarity[rarity] + budgetPerLevel * (level - 1)</c>.
    /// 2. Reparto inicial por <c>positionShare</c> (porcentajes 0..100 que suman 100) con el método del
    ///    resto mayor (largest remainder): se asigna primero <c>budget * share / 100</c> (división
    ///    entera) a cada atributo y el resto sin repartir se entrega, de uno en uno, a los atributos con
    ///    mayor resto (empate por el orden fijo Strength, Speed, Technique, Stamina, Leash), de modo que
    ///    la suma del reparto inicial es exactamente <c>budget</c>.
    /// 3. A cada atributo se le suma <c>race.AttributeBias</c> + <c>style.AttributeBias</c> + una
    ///    desviación individual entera en [-dev, dev] (<c>race.IndividualDeviation</c>), un dado por
    ///    atributo en el orden fijo de arriba.
    /// 4. Se calculan suelo y techo por atributo: <c>floor = max(attributeFloor, rangeByRarity[rarity].min,
    ///    positionFloors[position][attr] si existe)</c>, <c>cap = min(attributeCap, rangeByRarity[rarity].max)</c>.
    ///    Cada atributo se acota a [floor, cap].
    /// 5. Acotar mueve la suma lejos de <c>budget</c> (los sesgos empujan algunos atributos contra su
    ///    tope). Se renormaliza con un reparto iterativo de 1 en 1: mientras la suma sea menor que
    ///    <c>budget</c>, se suma 1 a cada atributo que aún tenga hueco bajo su techo, en el orden fijo,
    ///    una pasada tras otra; mientras sea mayor, se resta 1 a cada atributo que aún tenga hueco sobre
    ///    su suelo, igual. Si una pasada completa no mueve nada (todos los atributos están en su tope o
    ///    su suelo), el presupuesto no es alcanzable dentro de los baremos configurados y se para: con los
    ///    rangos de tuning.json (holgura de decenas de puntos por atributo frente a presupuestos de un par
    ///    de cientos) esto no ocurre, pero es la salvaguarda de terminación del bucle. El resultado nunca
    ///    viola floor/cap por construcción, y la suma final iguala al presupuesto salvo en ese caso límite
    ///    (documentado también en fase1b-diseno.md §1.3).
    /// </summary>
    private static Attributes GenerateAttributes(ref Pcg32 rng, GenerationTuning tuning, RaceDefinition race, StyleDefinition style, Position position, Rarity rarity, int level)
    {
        int budget = tuning.BudgetByRarity.Of(rarity) + tuning.BudgetPerLevel * (level - 1);
        var share = PositionShareOf(tuning.PositionShare, position);
        var range = RangeOf(tuning.RangeByRarity, rarity);
        var positionFloor = tuning.PositionFloors.Of(position);

        var values = new int[AttributeOrder.Length];
        var remainders = new int[AttributeOrder.Length];
        int allocated = 0;
        for (int i = 0; i < AttributeOrder.Length; i++)
        {
            int shareValue = share.Get(AttributeOrder[i]);
            int quotient = budget * shareValue / 100;
            values[i] = quotient;
            remainders[i] = budget * shareValue - quotient * 100;
            allocated += quotient;
        }

        int leftover = budget - allocated;
        while (leftover > 0)
        {
            int bestIndex = -1;
            int bestRemainder = -1;
            for (int i = 0; i < AttributeOrder.Length; i++)
            {
                if (remainders[i] > bestRemainder)
                {
                    bestRemainder = remainders[i];
                    bestIndex = i;
                }
            }

            values[bestIndex]++;
            remainders[bestIndex] = -1; // ya recibió su punto extra, no vuelve a competir
            leftover--;
        }

        var floors = new int[AttributeOrder.Length];
        var caps = new int[AttributeOrder.Length];
        for (int i = 0; i < AttributeOrder.Length; i++)
        {
            var attribute = AttributeOrder[i];
            int extraFloor = positionFloor.TryGetValue(attribute, out int f) ? f : tuning.AttributeFloor;
            floors[i] = Math.Max(tuning.AttributeFloor, Math.Max(range.Min, extraFloor));
            caps[i] = Math.Min(tuning.AttributeCap, range.Max);
            if (floors[i] > caps[i])
            {
                floors[i] = caps[i];
            }

            int dev = race.IndividualDeviation;
            int bias = race.AttributeBias.Get(attribute) + style.AttributeBias.Get(attribute) + rng.Range(-dev, dev + 1);
            values[i] = Math.Clamp(values[i] + bias, floors[i], caps[i]);
        }

        RenormalizeToBudget(values, floors, caps, budget);

        return Attributes.Clamp(new Attributes(
            values[0], values[1], values[2], values[3], values[4]));
    }

    /// <summary>Reparte 1 en 1 la diferencia entre la suma actual y el presupuesto, respetando floor/cap (ver comentario de <see cref="GenerateAttributes"/>).</summary>
    private static void RenormalizeToBudget(int[] values, int[] floors, int[] caps, int budget)
    {
        int sum = 0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }

        while (sum < budget)
        {
            bool moved = false;
            for (int i = 0; i < values.Length && sum < budget; i++)
            {
                if (values[i] < caps[i])
                {
                    values[i]++;
                    sum++;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }

        while (sum > budget)
        {
            bool moved = false;
            for (int i = 0; i < values.Length && sum > budget; i++)
            {
                if (values[i] > floors[i])
                {
                    values[i]--;
                    sum--;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }
    }

    private static AttributeShare PositionShareOf(PositionShareTable table, Position position) => position switch
    {
        Position.Goalkeeper => table.Goalkeeper,
        Position.Defender => table.Defender,
        Position.Midfielder => table.Midfielder,
        Position.Forward => table.Forward,
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    private static AttributeRange RangeOf(RarityRangeTable table, Rarity rarity) => rarity switch
    {
        Rarity.Common => table.Common,
        Rarity.Rare => table.Rare,
        Rarity.Legendary => table.Legendary,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };

    private static List<Trait> PickTraits(ref Pcg32 rng, Catalog catalog, RaceDefinition race, Position position)
    {
        var countWeights = catalog.Tuning.Generation.TraitCountWeights;
        int count = PickWeightedIndex(ref rng, countWeights) + 1;

        var pool = new List<(Trait Trait, int Weight)>(race.TraitWeights);
        var chosen = new List<Trait>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int totalWeight = 0;
            for (int j = 0; j < pool.Count; j++)
            {
                totalWeight += pool[j].Weight;
            }

            int r = rng.Range(0, totalWeight);
            int cumulative = 0;
            int selectedIndex = pool.Count - 1;
            for (int j = 0; j < pool.Count; j++)
            {
                cumulative += pool[j].Weight;
                if (r < cumulative)
                {
                    selectedIndex = j;
                    break;
                }
            }

            chosen.Add(pool[selectedIndex].Trait);
            pool.RemoveAt(selectedIndex);
        }

        if (position == Position.Goalkeeper && rng.Chance(catalog.Tuning.Generation.GoalkeeperTraitChance))
        {
            var goalkeeperTraits = catalog.Traits.Where(t => t.GoalkeeperOnly).OrderBy(t => t.Id).Select(t => t.Id).ToList();
            if (goalkeeperTraits.Count > 0)
            {
                chosen.Add(rng.Pick(goalkeeperTraits));
            }
        }

        return chosen;
    }

    private static int PickWeightedIndex(ref Pcg32 rng, IReadOnlyList<int> weights)
    {
        int total = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            total += weights[i];
        }

        int r = rng.Range(0, total);
        int cumulative = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (r < cumulative)
            {
                return i;
            }
        }

        return weights.Count - 1;
    }
}
