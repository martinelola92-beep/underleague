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
    /// Calidad de referencia del dial de <c>/Balance</c>: un jugador de calidad 50 es exactamente el
    /// jugador que describe el modelo de presupuesto de <c>tuning.generation</c> sin desplazar (ADR 0025).
    /// Es la misma definición de "jugador medio" que usan las fórmulas de <c>tuning.json</c>, que restan
    /// 50 en todas partes, así que vive en código y no en datos: no es un valor de balance.
    /// </summary>
    public const int QualityPivot = 50;

    /// <summary>Número de atributos que reparten el presupuesto (fuerza, velocidad, técnica, resistencia, correa).</summary>
    public const int AttributeCount = 5;

    /// <summary>
    /// Genera un jugador de nivel <paramref name="level"/> (1..8, RF-023/Progression.MaxLevel) para la
    /// rareza y posición dadas. Tags = [SpeciesTag, StyleTag, Position, ...Traits] (ADR 0024).
    /// <paramref name="quality"/> es el dial de fuerza de <c>/Balance</c> (RT-052): desplaza el
    /// presupuesto y la banda de atributos punto por punto respecto de <see cref="QualityPivot"/>.
    /// </summary>
    public static PlayerDefinition Generate(ref Pcg32 rng, Catalog catalog, RaceDefinition race, Position position, Rarity rarity, int level, int id, string name, int quality = QualityPivot, StyleTag? forcedStyle = null)
    {
        // El dado de estilo se tira SIEMPRE, aunque el llamador imponga la etiqueta: el flujo de RNG no
        // puede depender de si hay imposición o no (RT-021). forcedStyle es un instrumento de /Balance
        // (builds que necesitan una etiqueta concreta para probar un perk que la exige), no una mecánica.
        var styleTag = PickStyleTag(ref rng, race.StyleTagWeights);
        if (forcedStyle is { } imposed)
        {
            styleTag = imposed;
        }

        var style = catalog.Style(styleTag);

        var attributes = GenerateAttributes(ref rng, catalog.Tuning.Generation, race, style, position, rarity, level, quality);
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
    /// 0. <b>Dial de calidad</b> (paquete U). <c>quality</c> es el dial de fuerza de <c>/Balance</c>
    ///    (equipos de referencia, builds, campañas y tests estadísticos) y significa lo que siempre dijo
    ///    <c>data/balance/reference.json</c>: la media objetivo de atributos del equipo. Se aplica como un
    ///    desplazamiento <c>q = quality - <see cref="QualityPivot"/></c> que mueve a la vez el presupuesto
    ///    (<c>+ q * <see cref="AttributeCount"/></c>) y la banda de suelo y techo (<c>+ q</c> en los dos),
    ///    de modo que un equipo de calidad 60 es un equipo de calidad 40 con veinte puntos más en cada
    ///    atributo, que es literalmente lo que mide <c>betterTeamWinRate</c> (docs/balance.md, fase 0 §4).
    ///    Hasta el paquete Q el dial se traducía a <c>nivel = quality/10</c>, y entonces 60 contra 40 eran
    ///    dos niveles —16 puntos de presupuesto sobre ~290— y la métrica no medía lo que decía medir.
    ///    Nivel y calidad son ahora diales independientes: el nivel es progresión dentro de la run
    ///    (8 puntos de presupuesto por nivel), la calidad es de qué liga sale el equipo.
    /// 1. <c>budget = budgetByRarity[rarity] + budgetPerLevel * (level - 1) + q * AttributeCount</c>.
    /// 2. Reparto inicial por <c>positionShare</c> (porcentajes 0..100 que suman 100) con el método del
    ///    resto mayor (largest remainder): se asigna primero <c>budget * share / 100</c> (división
    ///    entera) a cada atributo y el resto sin repartir se entrega, de uno en uno, a los atributos con
    ///    mayor resto (empate por el orden fijo Strength, Speed, Technique, Stamina, Leash), de modo que
    ///    la suma del reparto inicial es exactamente <c>budget</c>.
    /// 3. A cada atributo se le suma <c>race.AttributeBias</c> + <c>style.AttributeBias</c> + una
    ///    desviación individual entera en [-dev, dev] (<c>race.IndividualDeviation</c>), un dado por
    ///    atributo en el orden fijo de arriba.
    /// 4. Se calculan suelo y techo por atributo: <c>floor = max(attributeFloor, rangeByRarity[rarity].min + q,
    ///    positionFloors[position][attr] + q si existe)</c>, <c>cap = min(attributeCap, rangeByRarity[rarity].max + q)</c>.
    ///    Cada atributo se acota a [floor, cap]. <c>attributeFloor</c> y <c>attributeCap</c> son cotas
    ///    absolutas de cordura y no se desplazan con la calidad.
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
    private static Attributes GenerateAttributes(ref Pcg32 rng, GenerationTuning tuning, RaceDefinition race, StyleDefinition style, Position position, Rarity rarity, int level, int quality)
    {
        int qualityShift = quality - QualityPivot;
        int budget = tuning.BudgetByRarity.Of(rarity) + tuning.BudgetPerLevel * (level - 1) + (qualityShift * AttributeCount);
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
            int extraFloor = positionFloor.TryGetValue(attribute, out int f) ? f + qualityShift : tuning.AttributeFloor;
            floors[i] = Math.Max(tuning.AttributeFloor, Math.Max(range.Min + qualityShift, extraFloor));
            caps[i] = Math.Min(tuning.AttributeCap, range.Max + qualityShift);
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
