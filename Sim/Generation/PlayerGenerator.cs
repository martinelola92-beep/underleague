using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Generation;

/// <summary>Generación procedural de un jugador a partir de raza, posición, calidad y semilla.</summary>
public static class PlayerGenerator
{
    /// <summary>
    /// Cada atributo = clamp(quality + raceBias + positionBias + Range(-dev, dev+1), 1, 99).
    /// Rasgos: n = pick ponderado de tuning.generation.traitCountWeights (1..3), elegidos por peso de
    /// race.TraitWeights sin repetición. Portero: además, con probabilidad
    /// tuning.generation.goalkeeperTraitChance, un rasgo de portero (Cat/Wall/Rusher, uniforme).
    /// Decisión fuera de la especificación: Level se fija a 1 (Generate no recibe nivel).
    /// </summary>
    public static PlayerDefinition Generate(ref Pcg32 rng, Catalog catalog, RaceDefinition race, Position position, Rarity rarity, int quality, int id, string name)
    {
        var positionBias = PositionBiasOf(catalog.Tuning.Generation.PositionBias, position);
        int dev = race.IndividualDeviation;

        var attributes = new Attributes(
            Math.Clamp(quality + race.AttributeBias.Strength + positionBias.Strength + rng.Range(-dev, dev + 1), 1, 99),
            Math.Clamp(quality + race.AttributeBias.Speed + positionBias.Speed + rng.Range(-dev, dev + 1), 1, 99),
            Math.Clamp(quality + race.AttributeBias.Technique + positionBias.Technique + rng.Range(-dev, dev + 1), 1, 99),
            Math.Clamp(quality + race.AttributeBias.Stamina + positionBias.Stamina + rng.Range(-dev, dev + 1), 1, 99),
            Math.Clamp(quality + race.AttributeBias.Leash + positionBias.Leash + rng.Range(-dev, dev + 1), 1, 99));

        var traits = PickTraits(ref rng, catalog, race, position);

        var tags = new List<string> { race.Tag, position.ToString() };
        foreach (var trait in traits)
        {
            tags.Add(trait.ToString());
        }

        return new PlayerDefinition(id, name, race.Id, position, rarity, Level: 1, attributes, traits, tags, PhysicalState.Healthy);
    }

    private static Attributes PositionBiasOf(PositionBiasTable table, Position position) => position switch
    {
        Position.Goalkeeper => table.Goalkeeper,
        Position.Defender => table.Defender,
        Position.Midfielder => table.Midfielder,
        Position.Forward => table.Forward,
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
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
