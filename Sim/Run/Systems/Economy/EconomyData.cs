using System.Text.Json;
using Underleague.Sim.Data;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>
/// Bandas de precio por rareza. Índice = <see cref="Rarity"/> (Common, Uncommon, Rare, Legendary), como
/// en <c>tuning.generation.budgetByRarity</c>. La entrada de legendario existe por completitud del enum
/// (ADR 0039): nada lo genera ni lo pone a la venta.
/// </summary>
public sealed record PriceByRarity(int Common, int Uncommon, int Rare, int Legendary)
{
    public int Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => Common,
        Rarity.Uncommon => Uncommon,
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
    int PriceSpreadPercent,
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
    int StartingGold,
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
    IReadOnlyList<int> RecruitLevelByAct,
    int RewardPlayerQuality,
    int RewardPerkWeight,
    int RewardPlayerWeight,
    int RewardItemWeight,
    MarketConfig Market)
{
    /// <summary>
    /// Valor medido de cada perk y peso que ese valor le da en el pool (ADR 0038,
    /// <c>data/economy/perk-values.json</c>). Vive aquí porque es la palanca gemela del precio: donde no
    /// hay precio, la frecuencia. Una instantánea sin el fichero reparte pesos uniformes.
    /// </summary>
    public PerkValueTable PerkValues { get; init; } = PerkValueTable.Uniform;

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

    /// <summary>
    /// Nivel con el que entra un jugador comprado, fichado como mercenario o elegido como recompensa en
    /// el acto indicado (1..3). El canterano no pasa por aquí: entra siempre en el nivel 1, que es lo que
    /// RF-114b/c describe ("malo hoy, potencialmente el mejor del acto 3 si se ficha pronto").
    /// </summary>
    public int RecruitLevel(int act) => act >= 1 && act <= RecruitLevelByAct.Count ? RecruitLevelByAct[act - 1] : 1;

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

        var recruitLevels = new List<int>();
        foreach (var item in root.Prop("recruitLevelByAct").EnumerateArray())
        {
            recruitLevels.Add(item.AsInt());
        }

        if (recruitLevels.Count != 3)
        {
            throw new DataException(Path, "$.recruitLevelByAct", "debe tener exactamente 3 valores, uno por acto (RF-001)");
        }

        var market = ReadMarket(root.Prop("market"));

        return new EconomyConfig(
            root.Int("startingGold"),
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
            recruitLevels,
            root.Int("rewardPlayerQuality"),
            root.Int("rewardPerkWeight"),
            root.Int("rewardPlayerWeight"),
            root.Int("rewardItemWeight"),
            market)
        {
            PerkValues = PerkValueTable.FromJson(files),
        };
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
        node.Int("priceSpreadPercent"),
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
        var values = new List<int>(4);
        foreach (var item in node.EnumerateArray())
        {
            values.Add(item.AsInt());
        }

        if (values.Count != 4)
        {
            throw new DataException(node.File, node.Path, "debe tener exactamente 4 valores: [common, uncommon, rare, legendary]");
        }

        return new PriceByRarity(values[0], values[1], values[2], values[3]);
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
