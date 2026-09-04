using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Tests.Perks;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Tests.Progression;

/// <summary>
/// Progresión entre partidos (RF-023, RF-025, RF-027, RF-070). Todo es puro: mismas entradas, mismas
/// salidas, sin estado ni aleatoriedad.
/// </summary>
public sealed class ProgressionTests
{
    // Catálogo real de /data salvo el catálogo de perks, que está reescribiendo el paquete T al formato
    // de fase1b-diseno.md §1.4 (ver TestPerks.CatalogWith). Las cinco habilidades raciales sí entran,
    // porque son las que la progresión consulta.
    private static readonly Catalog Catalog = TestPerks.CatalogWith();

    private static readonly ProgressionTuning Tuning = Catalog.Progression;

    [Fact]
    public void TuningComesFromData()
    {
        Assert.Equal(100, Tuning.MatchExperience);
        Assert.Equal(45, Tuning.BenchSharePercent);
        Assert.Equal(2, Tuning.AttributesPerLevel);
        Assert.Equal(new[] { 0, 100, 250, 450, 700, 1000, 1400, 1900 }, Tuning.ExperiencePerLevel);
    }

    [Theory]
    [InlineData(Rarity.Common, 2, 0)]
    [InlineData(Rarity.Uncommon, 3, 1)]
    [InlineData(Rarity.Rare, 4, 2)]
    public void RarityDecidesSlotsAndInitialPerks(Rarity rarity, int slots, int initial)
    {
        // RF-023: la rareza es techo de perks, nunca techo de nivel.
        Assert.Equal(slots, ProgressionRules.PerkSlots(rarity));
        Assert.Equal(initial, ProgressionRules.InitialPerks(rarity));
        Assert.Equal(8, ProgressionRules.MaxLevel);
    }

    [Fact]
    public void ExperienceIsFullForPlayersAndPartialForTheBench()
    {
        // RF-025: 100% a los que jugaron, 45% a los suplentes.
        var awards = ProgressionRules.AwardExperience(new[] { 3, 1 }, new[] { 5, 2 }, Tuning);

        Assert.Equal(new[] { 1, 2, 3, 5 }, awards.Select(a => a.PlayerId).ToArray());
        Assert.Equal(100, awards.Single(a => a.PlayerId == 1).Experience);
        Assert.Equal(100, awards.Single(a => a.PlayerId == 3).Experience);
        Assert.Equal(45, awards.Single(a => a.PlayerId == 2).Experience);
        Assert.Equal(45, awards.Single(a => a.PlayerId == 5).Experience);
    }

    [Fact]
    public void APlayerNeverCollectsTwice()
    {
        var awards = ProgressionRules.AwardExperience(new[] { 1 }, new[] { 1 }, Tuning);
        Assert.Equal(100, Assert.Single(awards).Experience);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(449, 3)]
    [InlineData(450, 4)]
    [InlineData(1900, 8)]
    [InlineData(100000, 8)]
    public void LevelComesFromTheCumulativeTable(int experience, int expected) =>
        Assert.Equal(expected, ProgressionRules.LevelFor(experience, Tuning));

    [Fact]
    public void LevellingRaisesEveryAttributeExceptLeash()
    {
        // RF-027: subir de nivel incrementa atributos base y no otorga perks.
        var levelOne = new Attributes(50, 40, 30, 60, 20);
        var levelFour = ProgressionRules.AttributesAtLevel(levelOne, 4, Tuning);

        Assert.Equal(new Attributes(56, 46, 36, 66, 20), levelFour);
        Assert.Equal(levelOne, ProgressionRules.AttributesAtLevel(levelOne, 1, Tuning));
    }

    [Fact]
    public void AttributesAtLevelClampsToTheGameRange()
    {
        var levelOne = new Attributes(98, 99, 30, 60, 20);
        Assert.Equal(new Attributes(99, 99, 44, 74, 20), ProgressionRules.AttributesAtLevel(levelOne, 8, Tuning));
    }

    [Fact]
    public void LevelUpIsIncrementalAndNeverGoesBackwards()
    {
        var player = Player() with { Level = 3, Attributes = new Attributes(50, 50, 50, 50, 20) };

        var raised = ProgressionRules.LevelUp(player, 5, Tuning);
        Assert.Equal(5, raised.Level);
        Assert.Equal(new Attributes(54, 54, 54, 54, 20), raised.Attributes);
        Assert.Empty(raised.Perks);

        Assert.Same(raised, ProgressionRules.LevelUp(raised, 5, Tuning));
        Assert.Same(raised, ProgressionRules.LevelUp(raised, 2, Tuning));
    }

    [Fact]
    public void LevelUpStopsAtTheMaximum()
    {
        var player = Player() with { Level = 7 };
        Assert.Equal(8, ProgressionRules.LevelUp(player, 12, Tuning).Level);
    }

    [Fact]
    public void CounterDeltasAreAddedToThePlayerCounters()
    {
        // RF-070: los contadores de los perks acumulativos viajan al partido siguiente.
        var player = Player().WithCounters(new Dictionary<string, int> { ["matches"] = 3 });
        var deltas = new[]
        {
            new PlayerCounterDelta(player.Id, "matches", 1),
            new PlayerCounterDelta(player.Id, "injuries", 2),
            new PlayerCounterDelta(player.Id + 1, "matches", 99),
        };

        var updated = ProgressionRules.ApplyCounterDeltas(player, deltas);

        Assert.Equal(4, updated.Counters["matches"]);
        Assert.Equal(2, updated.Counters["injuries"]);
        Assert.Equal(new[] { "injuries", "matches" }, updated.Counters.Keys.ToArray());
    }

    [Fact]
    public void ApplyCounterDeltasWithNothingToAddReturnsTheSamePlayer()
    {
        var player = Player();
        Assert.Same(player, ProgressionRules.ApplyCounterDeltas(player, Array.Empty<PlayerCounterDelta>()));
    }

    private static PlayerDefinition Player() => new(
        7, "Test", Race.Human, Position.Midfielder, Rarity.Common, 1,
        new Attributes(50, 50, 50, 50, 20), Array.Empty<Trait>(), new[] { "Neutral", "Midfielder" },
        PhysicalState.Healthy);
}
