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

/// <summary>
/// Recompensa de un tipo de nodo de partido (ADR 0043). Es lo que convierte al jefe en <b>trampolín</b> y
/// no solo en barrera: superar un acto cambia la trayectoria de la run.
/// </summary>
/// <param name="GoldBonusPercent">Recargo sobre el oro del acto ya multiplicado por la dificultad (RF-114g).</param>
/// <param name="Options">Opciones que se ofrecen en cada elección (RF-071: tres).</param>
/// <param name="Picks">Elecciones seguidas que da el nodo; el jefe da dos perks en vez de uno.</param>
/// <param name="RarityFloorPercent">
/// Probabilidad, en tanto por ciento, de que una opción se sortee <b>solo</b> entre las de rareza
/// superior a común: es la "rareza mejorada" que la ADR 0043 le da al nodo de élite.
/// </param>
/// <param name="CommonCeilingPercent">
/// Probabilidad, en tanto por ciento, de que una opción se sortee <b>solo</b> entre las comunes: es la
/// "rareza degradada" con la que la ADR 0052 §2 devuelve la <b>tercera</b> opción al partido de liga sin
/// devolverle la calidad. Se sortea con la misma tirada que <paramref name="RarityFloorPercent"/>, desde
/// el otro extremo, así que los dos no pueden coincidir en la misma opción y su suma no puede pasar de
/// 100.
/// </param>
/// <param name="HealsRoster">Cura la plantilla entera al superarlo (RF-091, RF-092): cierra el ciclo de desgaste del acto.</param>
public sealed record NodeRewardConfig(
    int GoldBonusPercent,
    int Options,
    int Picks,
    int RarityFloorPercent,
    int CommonCeilingPercent,
    bool HealsRoster);

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
    int PriceBandPercent,
    int ConsumablePrice,
    int ItemSellFractionPercent,
    PriceByRarity PlayerSaleBase,
    int PlayerSalePerLevel,
    int PlayerSalePerPerk,
    int PlayerSalePerBond,
    int RecruitQuality,
    int YouthQuality,
    int MercenaryQuality)
{
    /// <summary>
    /// Acota el precio de un artículo a la banda de su rareza (ADR 0044). El precio nace del valor del
    /// artículo (ADR 0038) y se dispersa dentro de su rareza (ADR 0037); las dos cosas juntas producían
    /// rangos de <b>18 a 1</b> dentro de una misma categoría, con media tienda inalcanzable siempre y la
    /// otra media trivial. La banda deja el rango dentro de una rareza en 2:1 y la diferencia grande donde
    /// tiene sentido: entre rarezas y entre categorías. Mínimo 1: nada es gratis por redondeo.
    /// </summary>
    public int ClampToBand(int price, int rarityBasePrice)
    {
        if (PriceBandPercent <= 0 || rarityBasePrice <= 0)
        {
            return price < 1 ? 1 : price;
        }

        int min = rarityBasePrice * (100 - PriceBandPercent) / 100;
        int max = rarityBasePrice * (100 + PriceBandPercent) / 100;
        int clamped = Math.Clamp(price, min < 1 ? 1 : min, max < 1 ? 1 : max);
        return clamped < 1 ? 1 : clamped;
    }
}

/// <summary>
/// Configuración íntegra de la economía de la run (RF-114g..k), cargada de <c>data/economy/economy.json</c>.
/// El paquete X no toca <c>Sim/Data</c> (fuera de sus fronteras): este tipo y su cargador viven aquí,
/// independientes de <see cref="Catalog"/>, y se construyen una vez por partida de pruebas o de
/// <c>/Balance</c>, igual que <c>DataLoader.FromJson</c> construye el catálogo (RT-012: sin E/S, recibe
/// el contenido ya leído).
/// </summary>
public sealed record EconomyConfig(
    IReadOnlyList<int> StartingGoldByDivision,
    int GoldAct1,
    int GoldAct2,
    int GoldAct3,
    IReadOnlyList<int> DifficultyMultiplierPercent,
    NodeRewardConfig LeagueReward,
    NodeRewardConfig EliteReward,
    NodeRewardConfig BossReward,
    int ExcellentMatchBonusGold,
    int ClinicCost,
    IReadOnlyList<int> EnrollmentCosts,
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

    /// <summary>
    /// Oro de partida de la división indicada (RF-128, ADR 0044 §"el oro inicial es la primera palanca de
    /// dificultad por división"). El criterio es que el club empiece con <b>lo justo para un artículo
    /// común</b> en la primera tienda, y que en Mundial esa primera tienda sea solo un escaparate. Es una
    /// palanca de <b>ritmo</b>: 10 de oro sobre los ~100 de una run es el 10% del total y no mueve la
    /// tasa de victoria por sí sola; lo que decide es el tono del arranque.
    /// </summary>
    public int StartingGoldFor(Division division)
    {
        int index = (int)division;
        return index >= 0 && index < StartingGoldByDivision.Count
            ? StartingGoldByDivision[index]
            : StartingGoldByDivision[^1];
    }

    /// <summary>Oro de partida en tercera (RF-128), que es la única división que juega la fase 2.</summary>
    public int StartingGold => StartingGoldFor(Division.Third);

    /// <summary>Oro fijo por victoria de ese acto, antes de multiplicadores (RF-114g).</summary>
    public int GoldForAct(int act) => act switch
    {
        1 => GoldAct1,
        2 => GoldAct2,
        3 => GoldAct3,
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, "el acto debe estar entre 1 y 3"),
    };

    /// <summary>Recompensa del tipo de nodo indicado (ADR 0043). Un nodo que no es de partido no tiene.</summary>
    public NodeRewardConfig RewardFor(NodeKind kind) => kind switch
    {
        NodeKind.EliteMatch => EliteReward,
        NodeKind.Boss => BossReward,
        _ => LeagueReward,
    };

    /// <summary>Multiplicador de dificultad (1..5, RF-012) en tanto por ciento.</summary>
    public int MultiplierForDifficulty(int difficulty)
    {
        int index = Math.Clamp(difficulty, 1, DifficultyMultiplierPercent.Count) - 1;
        return DifficultyMultiplierPercent[index];
    }

    /// <summary>
    /// Coste del siguiente hueco de plantilla (ADR 0046): <b>creciente</b>, uno por entrada de
    /// <c>enrollmentCosts</c>. Devuelve -1 cuando ya no quedan huecos que vender, que es lo que hace del
    /// techo de 12 un techo y no un peaje infinito.
    /// </summary>
    /// <param name="slotsBought">Huecos ya comprados en la run (<c>RunState.EnrollmentSlotsCounter</c>).</param>
    public int EnrollmentCost(int slotsBought) =>
        slotsBought >= 0 && slotsBought < EnrollmentCosts.Count ? EnrollmentCosts[slotsBought] : -1;

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

        var startingGold = new List<int>();
        foreach (var item in root.Prop("startingGoldByDivision").EnumerateArray())
        {
            startingGold.Add(item.AsInt());
        }

        int divisions = Enum.GetValues<Division>().Length;
        if (startingGold.Count != divisions)
        {
            throw new DataException(
                Path,
                "$.startingGoldByDivision",
                $"debe tener exactamente {divisions} valores, uno por división de RF-128 en el orden del enum "
                    + "(tercera, segunda, primera, continental, mundial)");
        }

        for (int i = 1; i < startingGold.Count; i++)
        {
            if (startingGold[i] > startingGold[i - 1])
            {
                throw new DataException(
                    Path,
                    "$.startingGoldByDivision",
                    "el oro de partida NO CRECE con la división (ADR 0044): subir de división es empezar con menos");
            }
        }

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

        var enrollment = new List<int>();
        foreach (var item in root.Prop("enrollmentCosts").EnumerateArray())
        {
            enrollment.Add(item.AsInt());
        }

        if (enrollment.Count != RunRules.MaxEnrollmentSlots)
        {
            throw new DataException(
                Path,
                "$.enrollmentCosts",
                $"debe tener exactamente {RunRules.MaxEnrollmentSlots} valores, uno por hueco de plantilla "
                    + $"entre la base de {RunRules.BaseRosterSize} y el techo de {RunRules.MaxRosterSize} (RF-020, ADR 0046)");
        }

        for (int i = 1; i < enrollment.Count; i++)
        {
            if (enrollment[i] <= enrollment[i - 1])
            {
                throw new DataException(
                    Path,
                    "$.enrollmentCosts",
                    "el coste del hueco de plantilla es CRECIENTE (ADR 0046): cada entrada debe superar a la anterior");
            }
        }

        var market = ReadMarket(root.Prop("market"));

        return new EconomyConfig(
            startingGold,
            root.Int("goldAct1"),
            root.Int("goldAct2"),
            root.Int("goldAct3"),
            difficulty,
            ReadNodeReward(root.Prop("nodeRewards").Prop("league")),
            ReadNodeReward(root.Prop("nodeRewards").Prop("elite")),
            ReadNodeReward(root.Prop("nodeRewards").Prop("boss")),
            root.Int("excellentMatchBonusGold"),
            root.Int("clinicCost"),
            enrollment,
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

    private static NodeRewardConfig ReadNodeReward(Json node) => new(
        node.Int("goldBonusPercent"),
        node.Int("options"),
        node.Int("picks"),
        node.Int("rarityFloorPercent"),
        node.Int("commonCeilingPercent"),
        node.Prop("healsRoster").AsBool());

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
        node.Int("priceBandPercent"),
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
