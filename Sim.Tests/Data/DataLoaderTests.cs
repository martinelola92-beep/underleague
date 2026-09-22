using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Data;

public class DataLoaderTests
{
    [Fact]
    public void FromJson_LoadsRealData_WithoutErrors()
    {
        var catalog = TestData.LoadCatalog();

        Assert.True(catalog.Races.Count >= 5);
        Assert.Equal(5, catalog.Styles.Count);
        Assert.True(catalog.Traits.Count >= 10);
        Assert.NotNull(catalog.Ai);
        Assert.NotNull(catalog.Tuning);
    }

    [Fact]
    public void FromJson_ExposesRaceFields()
    {
        var catalog = TestData.LoadCatalog();
        var human = catalog.Race(Race.Human);

        Assert.Equal("Human", human.SpeciesTag);
        Assert.True(human.Launch);
        Assert.Equal(1, human.CellsOccupied);
        Assert.True(human.BodyRadius > 0);
        Assert.InRange(human.Discipline, 0, 100);
        Assert.False(string.IsNullOrWhiteSpace(human.Ability));
        Assert.False(string.IsNullOrWhiteSpace(human.Description.Es));
        Assert.False(string.IsNullOrWhiteSpace(human.Description.En));
        Assert.True(human.TraitWeights.Count > 0);
        Assert.True(human.FirstNames.Count > 0);
        Assert.True(human.LastNames.Count > 0);

        // styleTagWeights suma 100 (fase1b-diseno.md §1.1); lo comprueba también DataLoader al cargar.
        Assert.Equal(100, human.StyleTagWeights.Sum(w => w.Weight));
    }

    [Theory]
    [InlineData(Race.Human, StyleTag.Neutral)]
    [InlineData(Race.Orc, StyleTag.Brute)]
    [InlineData(Race.Elf, StyleTag.Fine)]
    [InlineData(Race.Dwarf, StyleTag.Bulwark)]
    [InlineData(Race.Undead, StyleTag.Cold)]
    public void FromJson_EachLaunchRaceHasADominantStyleTagInRange(Race race, StyleTag dominant)
    {
        var catalog = TestData.LoadCatalog();
        var definition = catalog.Race(race);

        var weight = definition.StyleTagWeights.Single(w => w.Style == dominant).Weight;
        Assert.InRange(weight, 60, 85);

        // ADR 0024: al menos una etiqueta opuesta a la identidad de la raza, con peso > 0.
        Assert.True(definition.StyleTagWeights.Count(w => w.Style != dominant && w.Weight > 0) >= 1);
    }

    [Fact]
    public void FromJson_ExposesStyleDefinitions()
    {
        var catalog = TestData.LoadCatalog();
        var brute = catalog.Style(StyleTag.Brute);

        Assert.False(string.IsNullOrWhiteSpace(brute.Name.Es));
        Assert.False(string.IsNullOrWhiteSpace(brute.Description.Es));
        Assert.True(brute.AttributeBias.Strength > 0);

        var neutral = catalog.Style(StyleTag.Neutral);
        Assert.Equal(new Attributes(0, 0, 0, 0, 0), neutral.AttributeBias);
    }

    [Fact]
    public void FromJson_ExposesTraitsWithGoalkeeperFlag()
    {
        var catalog = TestData.LoadCatalog();
        var aggressive = catalog.Trait(Trait.Aggressive);
        var cat = catalog.Trait(Trait.Cat);

        Assert.False(aggressive.GoalkeeperOnly);
        Assert.True(cat.GoalkeeperOnly);
        Assert.Contains(aggressive.ActionMultipliers, m => m.Action == PlayerAction.Tackle && m.MultiplierPercent == 160);
    }

    [Fact]
    public void FromJson_ExposesAiWeights()
    {
        var catalog = TestData.LoadCatalog();

        Assert.Equal(600, catalog.Ai.Base(Position.Goalkeeper, PlayerAction.ShortPass));
        Assert.Equal(100, catalog.Ai.Tactical(TacticalState.InPossession, PlayerAction.ShortPass));
        Assert.Equal(4.0f, catalog.Ai.Shift(TacticalState.InPossession).Shift);
        Assert.Equal(30, catalog.Ai.Shift(TacticalState.InPossession).SpeedTicks);
        Assert.Equal(1.0f, catalog.Ai.Context.TackleDistanceMaxCells);
        Assert.Equal(700, catalog.Ai.Context.ChaseBallIncomingPassBonus);
    }

    [Fact]
    public void FromJson_ExposesGenerationTuning()
    {
        var catalog = TestData.LoadCatalog();
        var generation = catalog.Tuning.Generation;

        Assert.Equal(250, generation.BudgetByRarity.Common);
        Assert.Equal(275, generation.BudgetByRarity.Uncommon);
        Assert.Equal(300, generation.BudgetByRarity.Rare);
        Assert.Equal(8, generation.BudgetPerLevel);
        Assert.Equal(25, generation.AttributeFloor);
        Assert.Equal(92, generation.AttributeCap);
        Assert.Equal(40, generation.RangeByRarity.Common.Min);
        Assert.Equal(70, generation.RangeByRarity.Common.Max);
        Assert.Equal(86, generation.RangeByRarity.Rare.Max);
        Assert.Equal(100, generation.PositionShare.Goalkeeper.Strength + generation.PositionShare.Goalkeeper.Speed
            + generation.PositionShare.Goalkeeper.Technique + generation.PositionShare.Goalkeeper.Stamina + generation.PositionShare.Goalkeeper.Leash);
        Assert.Equal(40, generation.PositionFloors.Defender[AttributeKind.Strength]);
        Assert.False(generation.PositionFloors.Defender.ContainsKey(AttributeKind.Leash));
        Assert.Equal(new[] { 50, 35, 15 }, generation.TraitCountWeights);
        Assert.Equal(5000, generation.GoalkeeperTraitChance);
    }

    [Fact]
    public void FromJson_ExposesBodiesAndActionZoneTuning()
    {
        var catalog = TestData.LoadCatalog();

        Assert.True(catalog.Tuning.Bodies.SeparationEnabled);
        Assert.Equal(60, catalog.Tuning.Bodies.MaxPushPerTickMilli);
        Assert.Equal(250, catalog.Tuning.Bodies.TacklePushMultiplier);

        Assert.Equal(1, catalog.Tuning.ActionZone.Shape.Goalkeeper.Forward);
        Assert.Equal(-1, catalog.Tuning.ActionZone.Shape.Defender.Back);
        Assert.Equal(85, catalog.Tuning.ActionZone.ScaleFromLeashPercent.At1);
        Assert.Equal(115, catalog.Tuning.ActionZone.ScaleFromLeashPercent.At99);
        Assert.Equal(200, catalog.Tuning.ActionZone.OuterLimitMultiplier);
    }

    /// <summary>ADR 0027: común de nivel 8 y legendario de nivel 2 deben quedar a menos de 5 puntos de presupuesto.</summary>
    [Fact]
    public void FromJson_CommonLevel8BudgetApproximatesLegendaryLevel2Budget()
    {
        var catalog = TestData.LoadCatalog();
        var generation = catalog.Tuning.Generation;

        int commonLevel8 = generation.BudgetByRarity.Of(Rarity.Common) + generation.BudgetPerLevel * (8 - 1);
        int legendaryLevel2 = generation.BudgetByRarity.Of(Rarity.Rare) + generation.BudgetPerLevel * (2 - 1);

        Assert.True(Math.Abs(commonLevel8 - legendaryLevel2) < 5, $"{commonLevel8} vs {legendaryLevel2}");
    }

    /// <summary>Constantes que el motor tenía como private const y ahora salen de tuning.json (paquete E).</summary>
    [Fact]
    public void FromJson_ExposesResolutionConstantsThatWereLiterals()
    {
        var catalog = TestData.LoadCatalog();

        Assert.Equal(60, catalog.Tuning.AssistWindowTicks);
        Assert.Equal(6, catalog.Tuning.Dribble.LostKnockdownTicks);
        Assert.Equal(15, catalog.Tuning.Shot.PenaltyQualityBonus);
        Assert.Equal(60, catalog.Tuning.Save.QualityWeight);
        Assert.Equal(300, catalog.Tuning.Tackle.HardTackleYellowBonus);
        Assert.Equal(20, catalog.Tuning.Tackle.HardTackleRedBonus);
        // 60 -> 90 con la ADR 0129: al dejar de compartir contador con la entrada sin balón, las disputas
        // del balón dejaron de estar bloqueadas por los golpes y subieron de 6,92 a 7,86 por partido; este
        // enfriamiento absorbe lo liberado sin tocar el freno de la entrada sin balón.
        Assert.Equal(90, catalog.Tuning.States.TackleCooldownTicks);
    }

    [Fact]
    public void FromJson_UnknownKeyInContext_ThrowsWithFileAndPath()
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = InjectKey(files["ai/weights.json"], "\"context\": {", "\"context\": { \"bogusKey\": 1,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("context", ex.JsonPath);
    }

    /// <summary>
    /// ADR 0125 D2: el mapa por puesto de la entrada sin balón tiene los cuatro puestos <b>obligatorios</b>.
    /// Que falte uno es un error explícito, nunca un 0 implícito — porque 0 significa "este puesto no
    /// entra nunca" y esa decisión tiene que estar escrita, no omitida (RT-032).
    /// </summary>
    [Fact]
    public void FromJson_OffBallTackleAdjustMissingAPosition_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = files["ai/weights.json"].Replace("\"Midfielder\": 0,\n      \"Forward\": 0", "\"Forward\": 0", StringComparison.Ordinal);

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("Midfielder", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Un puesto que no existe no se ignora: el fichero es inválido.</summary>
    [Fact]
    public void FromJson_OffBallTackleAdjustWithAnUnknownPosition_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = files["ai/weights.json"].Replace("\"Forward\": 0", "\"Sweeper\": 0", StringComparison.Ordinal);

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("Sweeper", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El invariante de la ADR 0105 §3, ahora comprobado puesto a puesto: quitar el balón tiene que seguir
    /// puntuando más que pegarle a quien no lo lleva, así que ningún ajuste puede llegar a
    /// <c>tackleBallCarrierBonus</c>.
    /// </summary>
    [Fact]
    public void FromJson_OffBallTackleAdjustReachingTheCarrierBonus_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = files["ai/weights.json"].Replace("\"Defender\": 150", "\"Defender\": 195", StringComparison.Ordinal);

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("tackleBallCarrierBonus", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Una clave <b>numérica</b> no es un puesto: <c>Enum.TryParse</c> la aceptaría ("3" es
    /// <c>Forward</c>), rellenando un puesto en silencio y colándose además por la comprobación de
    /// completitud. Con "7" —fuera del enum— indexaría el array y reventaría con una excepción de runtime
    /// en vez de con un error de dato. Las dos tienen que dar <see cref="DataException"/>.
    /// </summary>
    [Theory]
    [InlineData("3")]
    [InlineData("7")]
    public void FromJson_OffBallTackleAdjustWithANumericPositionKey_Throws(string key)
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = files["ai/weights.json"].Replace("\"Forward\": 0", $"\"{key}\": 0", StringComparison.Ordinal);

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("posición desconocida", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Una clave repetida es JSON válido y gana la última, así que apagaría el único puesto activo sin un
    /// solo error — ni el esquema detecta duplicados. Aquí es un error explícito.
    /// </summary>
    [Fact]
    public void FromJson_OffBallTackleAdjustWithADuplicatePosition_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = files["ai/weights.json"].Replace("\"Defender\": 150", "\"Defender\": 150,\n      \"Defender\": 0", StringComparison.Ordinal);

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("repite el puesto", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>El portero solo admite 0: cualquier otro valor sería un dato inerte que parece hacer algo.</summary>
    [Fact]
    public void FromJson_OffBallTackleAdjustForTheGoalkeeper_MustBeZero()
    {
        var files = TestData.LoadAllFiles();
        files["ai/weights.json"] = files["ai/weights.json"].Replace("\"Goalkeeper\": 0", "\"Goalkeeper\": 150", StringComparison.Ordinal);

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("ai/weights.json", ex.File);
        Assert.Contains("el portero no entra sin balón", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El reparto <b>publicado</b>, fijado aquí para que abrir un puesto sea un acto deliberado y no un
    /// descuido que solo aparezca siete minutos después en las 43 puertas (ADR 0125 D2: la calibración que
    /// abre los tres puestos está medida y pendiente de decisión, `docs/pendientes/BE-A.md`).
    /// </summary>
    [Fact]
    public void FromJson_ThePublishedOffBallTackleMapIsTodaysDistribution()
    {
        var catalog = TestData.LoadCatalog();

        Assert.Equal(150, catalog.Ai.OffBallTackleAdjust(Position.Defender));
        Assert.Equal(0, catalog.Ai.OffBallTackleAdjust(Position.Midfielder));
        Assert.Equal(0, catalog.Ai.OffBallTackleAdjust(Position.Forward));
        Assert.Equal(0, catalog.Ai.OffBallTackleAdjust(Position.Goalkeeper));
    }

    [Fact]
    public void FromJson_UnknownKeyInTuningSection_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["sim/tuning.json"] = InjectKey(files["sim/tuning.json"], "\"movement\": {", "\"movement\": { \"bogusKey\": 1,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("sim/tuning.json", ex.File);
        Assert.Contains("movement", ex.JsonPath);
    }

    [Fact]
    public void FromJson_UnknownKeyAtTuningRoot_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["sim/tuning.json"] = InjectKey(files["sim/tuning.json"], "\"regulationTicks\": 1200,", "\"bogusKey\": 1, \"regulationTicks\": 1200,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("sim/tuning.json", ex.File);
        Assert.Equal("$", ex.JsonPath);
    }

    [Fact]
    public void FromJson_UnknownKeyInTrait_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["traits/traits.json"] = InjectKey(files["traits/traits.json"], "\"Aggressive\": {", "\"Aggressive\": { \"bogusKey\": 1,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("traits/traits.json", ex.File);
        Assert.Contains("Aggressive", ex.JsonPath);
    }

    [Fact]
    public void FromJson_UnknownKeyInRace_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["races/human.json"] = InjectKey(files["races/human.json"], "\"launch\": true,", "\"bogusKey\": 1, \"launch\": true,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("races/human.json", ex.File);
        Assert.Equal("$", ex.JsonPath);
    }

    [Fact]
    public void FromJson_UnknownKeyInStyles_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["tags/styles.json"] = InjectKey(files["tags/styles.json"], "\"Brute\": {", "\"Brute\": { \"bogusKey\": 1,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("tags/styles.json", ex.File);
        Assert.Contains("Brute", ex.JsonPath);
    }

    [Fact]
    public void FromJson_StyleTagWeightsNotSummingTo100_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["races/human.json"] = files["races/human.json"].Replace("\"Neutral\": 70,", "\"Neutral\": 71,");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("races/human.json", ex.File);
        Assert.Contains("styleTagWeights", ex.JsonPath);
    }

    [Fact]
    public void FromJson_PositionShareNotSummingTo100_Throws()
    {
        var files = TestData.LoadAllFiles();
        files["sim/tuning.json"] = files["sim/tuning.json"].Replace(
            "\"Goalkeeper\": { \"strength\": 22, \"speed\": 24, \"technique\": 22, \"stamina\": 22, \"leash\": 10 },",
            "\"Goalkeeper\": { \"strength\": 23, \"speed\": 24, \"technique\": 22, \"stamina\": 22, \"leash\": 10 },");

        var ex = Assert.Throws<DataException>(() => DataLoader.FromJson(files));
        Assert.Equal("sim/tuning.json", ex.File);
        Assert.Contains("positionShare", ex.JsonPath);
    }

    [Fact]
    public void FromJson_IgnoresDocKeys()
    {
        // sim/tuning.json y ai/weights.json ya tienen "_doc"; si no se ignorase, la carga fallaría.
        var catalog = TestData.LoadCatalog();
        Assert.NotNull(catalog);
    }

    private static string InjectKey(string json, string marker, string replacement)
    {
        Assert.Contains(marker, json);
        return json.Replace(marker, replacement);
    }
}
