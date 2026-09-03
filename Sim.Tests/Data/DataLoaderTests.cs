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

        Assert.True(catalog.Races.Count >= 3);
        Assert.True(catalog.Traits.Count >= 10);
        Assert.NotNull(catalog.Ai);
        Assert.NotNull(catalog.Tuning);
    }

    [Fact]
    public void FromJson_ExposesRaceFields()
    {
        var catalog = TestData.LoadCatalog();
        var human = catalog.Race(Race.Human);

        Assert.Equal("Neutral", human.Tag);
        Assert.True(human.Launch);
        Assert.Equal(1, human.CellsOccupied);
        Assert.True(human.TraitWeights.Count > 0);
        Assert.True(human.FirstNames.Count > 0);
        Assert.True(human.LastNames.Count > 0);
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

        Assert.Equal(600, catalog.Ai.Base(Position.Goalkeeper, PlayerAction.Pass));
        Assert.Equal(100, catalog.Ai.Tactical(TacticalState.InPossession, PlayerAction.Pass));
        Assert.Equal(4.0f, catalog.Ai.Shift(TacticalState.InPossession).Shift);
        Assert.Equal(30, catalog.Ai.Shift(TacticalState.InPossession).SpeedTicks);
        Assert.Equal(1.0f, catalog.Ai.Context.TackleDistanceMaxCells);
        Assert.Equal(700, catalog.Ai.Context.ChaseBallIncomingPassBonus);
    }

    [Fact]
    public void FromJson_ExposesGenerationAndLeashTuning()
    {
        var catalog = TestData.LoadCatalog();

        Assert.Equal(1, catalog.Tuning.Leash.MinCells);
        Assert.Equal(8, catalog.Tuning.Leash.CellsPer99);
        Assert.Equal(50, catalog.Tuning.Generation.LeashBase);
        Assert.Equal(new[] { 50, 35, 15 }, catalog.Tuning.Generation.TraitCountWeights);
        Assert.Equal(5000, catalog.Tuning.Generation.GoalkeeperTraitChance);
        Assert.Equal(-30, catalog.Tuning.Generation.PositionBias.Goalkeeper.Leash);
        Assert.Equal(8, catalog.Tuning.Generation.PositionBias.Midfielder.Leash);
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
        Assert.Equal(1500, catalog.Tuning.Tackle.HardTackleYellowBonus);
        Assert.Equal(200, catalog.Tuning.Tackle.HardTackleRedBonus);
        Assert.Equal(100, catalog.Tuning.States.TackleCooldownTicks);
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
        files["sim/tuning.json"] = InjectKey(files["sim/tuning.json"], "\"ticksPerSecond\": 15,", "\"bogusKey\": 1, \"ticksPerSecond\": 15,");

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
