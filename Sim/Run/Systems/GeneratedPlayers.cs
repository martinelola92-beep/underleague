using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems;

/// <summary>
/// Flujo de RNG del surtido de un nodo (mercado o recompensa), derivado de <c>RngStreams.Rewards</c>
/// (W-12, <c>fase2-diseno.md</c> §13): mismo nodo y mismo número de rerolls producen siempre el mismo
/// surtido, sin necesidad de serializarlo. El mercado nunca se renueva (RF-114), así que siempre deriva
/// con <c>rerollCount = 0</c>; las recompensas usan <c>state.NodeRerolls</c> (RF-071b).
/// </summary>
public static class OfferStream
{
    public static Pcg32 For(ulong seed, int nodeId, int rerollCount) =>
        RngStreams.Rewards(seed, checked((nodeId * 10_000) + rerollCount));
}

/// <summary>Reparto por rareza para sortear la rareza de un jugador generado en el mercado o en una recompensa.</summary>
public sealed record RarityWeights(int Common, int Rare, int Legendary)
{
    public Rarity Pick(ref Pcg32 rng)
    {
        int total = Common + Rare + Legendary;
        int roll = rng.Range(0, total);
        if (roll < Common)
        {
            return Rarity.Common;
        }

        return roll < Common + Rare ? Rarity.Rare : Rarity.Legendary;
    }
}

/// <summary>
/// Genera jugadores para el mercado y las recompensas: fichajes, canteranos y mercenarios. Id -1 a
/// propósito (<c>RunState.WithNewPlayer</c> le asigna <c>NextPlayerId</c> al comprarlo/elegirlo; hasta
/// entonces el jugador generado no forma parte de la plantilla).
/// </summary>
public static class GeneratedPlayers
{
    private static readonly Position[] AllPositions =
    {
        Position.Goalkeeper, Position.Defender, Position.Midfielder, Position.Forward,
    };

    private static readonly RarityWeights RecruitWeights = new(60, 32, 8);
    private static readonly RarityWeights MercenaryWeights = new(25, 55, 20);
    private static readonly RarityWeights RewardWeights = new(35, 50, 15);

    /// <summary>
    /// Fichaje de pago (RF-114): raza del club, posición y rareza sorteadas, y el <b>nivel del acto</b>
    /// (<c>economy.recruitLevelByAct</c>). Que un fichaje de pago entre en el nivel 1 en el acto 3 lo
    /// convierte en oro tirado: la plantilla va por el 6 o el 7 y ningún criterio razonable lo alinea,
    /// así que el mercado deja de ser un sumidero justo cuando más oro hay (medido en el paquete Z).
    /// </summary>
    public static RunPlayer Recruit(ref Pcg32 rng, Catalog catalog, Race race, int quality, int level = 1)
    {
        var rarity = RecruitWeights.Pick(ref rng);
        return Generate(ref rng, catalog, race, rarity, quality, level, youth: false, mercenary: false, wage: 0);
    }

    /// <summary>Canterano gratuito (RF-114b/c): común, de la raza del club, atributos bajos, +33% de experiencia.</summary>
    public static RunPlayer Youth(ref Pcg32 rng, Catalog catalog, Race race, int quality) =>
        Generate(ref rng, catalog, race, Rarity.Common, quality, level: 1, youth: true, mercenary: false, wage: 0);

    /// <summary>
    /// Mercenario (RF-110..113): raza distinta a la del club (RF-004c), estadísticas por encima de la
    /// media de su rareza (calidad más alta), salario por partido, y cuenta como <c>Stranger</c> para las
    /// sinergias de cohesión (RF-111).
    /// </summary>
    public static RunPlayer Mercenary(ref Pcg32 rng, Catalog catalog, Race foreignRace, int quality, int wage, int level = 1)
    {
        var rarity = MercenaryWeights.Pick(ref rng);
        var player = Generate(ref rng, catalog, foreignRace, rarity, quality, level, youth: false, mercenary: true, wage: wage);
        var tags = new List<string>(player.Tags) { "Stranger" };
        return player with { Tags = tags };
    }

    /// <summary>Jugador de recompensa (RF-071): raza del club, rareza sesgada al alza.</summary>
    public static RunPlayer Reward(ref Pcg32 rng, Catalog catalog, Race race, int quality, int level = 1)
    {
        var rarity = RewardWeights.Pick(ref rng);
        return Generate(ref rng, catalog, race, rarity, quality, level, youth: false, mercenary: false, wage: 0);
    }

    private static RunPlayer Generate(
        ref Pcg32 rng,
        Catalog catalog,
        Race race,
        Rarity rarity,
        int quality,
        int level,
        bool youth,
        bool mercenary,
        int wage)
    {
        var raceDefinition = catalog.Race(race);
        var nameGenerator = new NameGenerator(raceDefinition);
        string name = nameGenerator.Next(ref rng);
        var position = rng.Pick(AllPositions);
        var definition = PlayerGenerator.Generate(ref rng, catalog, raceDefinition, position, rarity, level, id: -1, name, quality);

        // La experiencia tiene que corresponder al nivel con el que entra: si no, el primer partido lo
        // recalcularía desde cero y el jugador se quedaría clavado en su nivel durante media run
        // (Progression.LevelUp nunca baja, pero tampoco sube hasta cruzar el umbral).
        var table = catalog.Progression.ExperiencePerLevel;
        int experience = level >= 1 && level <= table.Count ? table[level - 1] : 0;
        return RunPlayer.From(definition) with
        {
            Experience = experience,
            IsYouth = youth,
            IsMercenary = mercenary,
            Wage = wage,
        };
    }
}
