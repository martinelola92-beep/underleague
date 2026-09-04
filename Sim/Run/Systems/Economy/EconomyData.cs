using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Bandas de precio por rareza. Índice = <see cref="Rarity"/> (Common, Rare, Legendary), como en
/// <c>tuning.generation.budgetByRarity</c>.
/// </summary>
public sealed record PriceByRarity(int Common, int Rare, int Legendary)
{
    public int Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => Common,
        Rarity.Rare => Rare,
        Rarity.Legendary => Legendary,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };
}

/// <summary>Configuración del surtido del mercado (RF-114..114f).</summary>
public sealed record MarketConfig(
    int PlayerOffers,
    int PerkOffers,
    int ItemOffers,
    int ConsumableOffers,
    int MercenaryOffers,
    int YouthMin,
    int YouthMax,
    PriceByRarity PlayerPrice,
    PriceByRarity PerkPrice,
    PriceByRarity ItemPrice,
    int ConsumablePrice,
    int ItemSellFractionPercent,
    PriceByRarity PlayerSaleBase,
    int PlayerSalePerLevel,
    int PlayerSalePerPerk,
    int PlayerSalePerBond,
    int RecruitQuality,
    int YouthQuality,
    int MercenaryQuality);

/// <summary>
/// Configuración íntegra de la economía de la run (RF-114g..k), cargada de <c>data/economy/economy.json</c>.
/// El paquete X no toca <c>Sim/Data</c> (fuera de sus fronteras): este tipo y su cargador viven aquí,
/// independientes de <see cref="Catalog"/>, y se construyen una vez por partida de pruebas o de
/// <c>/Balance</c>, igual que <c>DataLoader.FromJson</c> construye el catálogo (RT-012: sin E/S, recibe
/// el contenido ya leído).
/// </summary>
public sealed record EconomyConfig(
    int GoldAct1,
    int GoldAct2,
    int GoldAct3,
    IReadOnlyList<int> DifficultyMultiplierPercent,
    int EliteBonusPercent,
    int BossBonusPercent,
    int ExcellentMatchBonusGold,
    int ClinicCost,
    int RerollBaseCost,
    int RerollStepCost,
    int TrainingExperience,
    int EventGoldMin,
    int EventGoldMax,
    int MercenaryBaseWage,
    int MercenaryWagePerRarityStep,
    int MercenaryBenchAbandonMatches,
    int MercenaryLossStreakAbandon,
    int RewardPlayerQuality,
    int RewardPerkWeight,
    int RewardPlayerWeight,
    int RewardItemWeight,
    MarketConfig Market)
{
    /// <summary>Oro fijo por victoria de ese acto, antes de multiplicadores (RF-114g).</summary>
    public int GoldForAct(int act) => act switch
    {
        1 => GoldAct1,
        2 => GoldAct2,
        3 => GoldAct3,
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, "el acto debe estar entre 1 y 3"),
    };

    /// <summary>Multiplicador de dificultad (1..5, RF-012) en tanto por ciento.</summary>
    public int MultiplierForDifficulty(int difficulty)
    {
        int index = Math.Clamp(difficulty, 1, DifficultyMultiplierPercent.Count) - 1;
        return DifficultyMultiplierPercent[index];
    }

    /// <summary>Coste de un reroll de recompensa, creciente con el número de rerolls ya usados en la run (RF-071b).</summary>
    public int RerollCost(int rerollsUsedInRun) => RerollBaseCost + (RerollStepCost * rerollsUsedInRun);

    /// <summary>Salario por partido de un mercenario de esa rareza (RF-111): base + escalón por rareza.</summary>
    public int MercenaryWage(Rarity rarity) => MercenaryBaseWage + (MercenaryWagePerRarityStep * (int)rarity);
}

/// <summary>Carga <c>data/economy/economy.json</c> (RT-012: sin E/S, recibe el contenido ya leído).</summary>
public static class EconomyLoader
{
    private const string Path = "economy/economy.json";

    /// <summary>Carga la configuración de economía de la instantánea de ficheros indicada.</summary>
    public static EconomyConfig FromJson(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!files.TryGetValue(Path, out var content))
        {
            throw new DataException(Path, "$", "fichero requerido ausente");
        }

        using var document = Parse(content);
        var root = Json.Root(Path, document);

        var difficulty = new List<int>();
        foreach (var item in root.Prop("difficultyMultiplierPercent").EnumerateArray())
        {
            difficulty.Add(item.AsInt());
        }

        if (difficulty.Count != 5)
        {
            throw new DataException(Path, "$.difficultyMultiplierPercent", "debe tener exactamente 5 valores (RF-012: 5 niveles de dificultad)");
        }

        var market = ReadMarket(root.Prop("market"));

        return new EconomyConfig(
            root.Int("goldAct1"),
            root.Int("goldAct2"),
            root.Int("goldAct3"),
            difficulty,
            root.Int("eliteBonusPercent"),
            root.Int("bossBonusPercent"),
            root.Int("excellentMatchBonusGold"),
            root.Int("clinicCost"),
            root.Int("rerollBaseCost"),
            root.Int("rerollStepCost"),
            root.Int("trainingExperience"),
            root.Int("eventGoldMin"),
            root.Int("eventGoldMax"),
            root.Int("mercenaryBaseWage"),
            root.Int("mercenaryWagePerRarityStep"),
            root.Int("mercenaryBenchAbandonMatches"),
            root.Int("mercenaryLossStreakAbandon"),
            root.Int("rewardPlayerQuality"),
            root.Int("rewardPerkWeight"),
            root.Int("rewardPlayerWeight"),
            root.Int("rewardItemWeight"),
            market);
    }

    private static MarketConfig ReadMarket(Json node) => new(
        node.Int("playerOffers"),
        node.Int("perkOffers"),
        node.Int("itemOffers"),
        node.Int("consumableOffers"),
        node.Int("mercenaryOffers"),
        node.Int("youthMin"),
        node.Int("youthMax"),
        ReadPriceByRarity(node.Prop("playerPriceByRarity")),
        ReadPriceByRarity(node.Prop("perkPriceByRarity")),
        ReadPriceByRarity(node.Prop("itemPriceByRarity")),
        node.Int("consumablePrice"),
        node.Int("itemSellFractionPercent"),
        ReadPriceByRarity(node.Prop("playerSaleBaseByRarity")),
        node.Int("playerSalePerLevel"),
        node.Int("playerSalePerPerk"),
        node.Int("playerSalePerBond"),
        node.Int("recruitQuality"),
        node.Int("youthQuality"),
        node.Int("mercenaryQuality"));

    private static PriceByRarity ReadPriceByRarity(Json node)
    {
        var values = new List<int>(3);
        foreach (var item in node.EnumerateArray())
        {
            values.Add(item.AsInt());
        }

        if (values.Count != 3)
        {
            throw new DataException(node.File, node.Path, "debe tener exactamente 3 valores: [common, rare, legendary]");
        }

        return new PriceByRarity(values[0], values[1], values[2]);
    }

    private static JsonDocument Parse(string content)
    {
        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DataException(Path, "$", $"JSON inválido: {ex.Message}");
        }
    }
}
