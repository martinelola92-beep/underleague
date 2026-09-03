using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Generation;

public class GeneratorTests
{
    [Fact]
    public void Generate_AttributesAreAlwaysInRange()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Human);
        var rng = RngStreams.Generation(1, 0);

        for (int i = 0; i < 200; i++)
        {
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Midfielder, Rarity.Common, 50, i, "Test Player");
            Assert.InRange(player.Attributes.Strength, 1, 99);
            Assert.InRange(player.Attributes.Speed, 1, 99);
            Assert.InRange(player.Attributes.Technique, 1, 99);
            Assert.InRange(player.Attributes.Stamina, 1, 99);
            Assert.InRange(player.Attributes.Leash, 1, 99);
        }
    }

    [Fact]
    public void Generate_ExtremeQuality_StillClampsToRange()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Orc);
        var rngLow = RngStreams.Generation(2, 0);
        var rngHigh = RngStreams.Generation(2, 1);

        for (int i = 0; i < 50; i++)
        {
            var lowPlayer = PlayerGenerator.Generate(ref rngLow, catalog, race, Position.Defender, Rarity.Common, -100, i, "Low");
            var highPlayer = PlayerGenerator.Generate(ref rngHigh, catalog, race, Position.Defender, Rarity.Common, 300, i, "High");
            Assert.InRange(lowPlayer.Attributes.Strength, 1, 99);
            Assert.InRange(highPlayer.Attributes.Strength, 1, 99);
        }
    }

    [Fact]
    public void Generate_TraitsAreOneToThreeAndDistinct_ForFieldPlayers()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Elf);
        var rng = RngStreams.Generation(3, 0);

        for (int i = 0; i < 200; i++)
        {
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Forward, Rarity.Common, 50, i, "Test Player");
            Assert.InRange(player.Traits.Count, 1, 3);
            Assert.Equal(player.Traits.Count, player.Traits.Distinct().Count());
        }
    }

    [Fact]
    public void Generate_GoalkeeperTraits_UpToFourAndDistinct()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Human);
        var rng = RngStreams.Generation(4, 0);

        for (int i = 0; i < 200; i++)
        {
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Goalkeeper, Rarity.Common, 50, i, "Test GK");
            Assert.InRange(player.Traits.Count, 1, 4);
            Assert.Equal(player.Traits.Count, player.Traits.Distinct().Count());
        }
    }

    [Fact]
    public void Generate_TagsIncludeRacePositionAndTraits()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Human);
        var rng = RngStreams.Generation(5, 0);

        var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Forward, Rarity.Common, 50, 1, "Test Player");
        Assert.Contains(race.Tag, player.Tags);
        Assert.Contains(Position.Forward.ToString(), player.Tags);
        foreach (var trait in player.Traits)
        {
            Assert.Contains(trait.ToString(), player.Tags);
        }
    }

    [Fact]
    public void TeamGenerator_ProducesTenPlayersWithoutRepeatedNames()
    {
        var catalog = TestData.LoadCatalog();
        var rng = RngStreams.Generation(6, 0);

        var team = TeamGenerator.Generate(ref rng, catalog, "human_50", Race.Human, 50, 1);

        Assert.Equal(10, team.Players.Count);
        var names = team.Players.Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void TeamGenerator_ProducesExpectedPositionComposition()
    {
        var catalog = TestData.LoadCatalog();
        var rng = RngStreams.Generation(7, 0);

        var team = TeamGenerator.Generate(ref rng, catalog, "orc_50", Race.Orc, 50, 1);

        Assert.Equal(1, team.Players.Count(p => p.Position == Position.Goalkeeper));
        Assert.Equal(3, team.Players.Count(p => p.Position == Position.Defender));
        Assert.Equal(4, team.Players.Count(p => p.Position == Position.Midfielder));
        Assert.Equal(2, team.Players.Count(p => p.Position == Position.Forward));
    }

    [Fact]
    public void TeamGenerator_ProducesExactlyOneRarePlayer()
    {
        var catalog = TestData.LoadCatalog();
        var rng = RngStreams.Generation(8, 0);

        var team = TeamGenerator.Generate(ref rng, catalog, "orc_50", Race.Orc, 50, 1);

        Assert.Equal(1, team.Players.Count(p => p.Rarity == Rarity.Rare));
        Assert.Equal(9, team.Players.Count(p => p.Rarity == Rarity.Common));
    }

    [Fact]
    public void TeamGenerator_DefaultLineupHasExactlyOneGoalkeeperAndNoRepeatedCells()
    {
        var catalog = TestData.LoadCatalog();
        var rng = RngStreams.Generation(9, 0);

        var team = TeamGenerator.Generate(ref rng, catalog, "elf_50", Race.Elf, 50, 1);

        Assert.Equal(7, team.Lineup.Slots.Count);
        var goalkeeperIds = team.Players.Where(p => p.Position == Position.Goalkeeper).Select(p => p.Id).ToHashSet();
        int goalkeepersInLineup = team.Lineup.Slots.Count(s => goalkeeperIds.Contains(s.PlayerId));
        Assert.Equal(1, goalkeepersInLineup);

        var cells = team.Lineup.Slots.Select(s => s.HomeCell).ToList();
        Assert.Equal(cells.Count, cells.Distinct().Count());
    }

    [Fact]
    public void TeamGenerator_IsDeterministic_ForSameSeed()
    {
        var catalog = TestData.LoadCatalog();
        var rngA = RngStreams.Generation(42, 7);
        var rngB = RngStreams.Generation(42, 7);

        var teamA = TeamGenerator.Generate(ref rngA, catalog, "elf_50", Race.Elf, 50, 1);
        var teamB = TeamGenerator.Generate(ref rngB, catalog, "elf_50", Race.Elf, 50, 1);

        Assert.Equal(teamA.Players.Select(p => p.Name), teamB.Players.Select(p => p.Name));
        Assert.Equal(teamA.Players.Select(p => p.Attributes), teamB.Players.Select(p => p.Attributes));
        Assert.Equal(teamA.Players.Select(p => p.Rarity), teamB.Players.Select(p => p.Rarity));
        Assert.Equal(teamA.Players.SelectMany(p => p.Traits), teamB.Players.SelectMany(p => p.Traits));
    }

    [Fact]
    public void TeamGenerator_DifferentSeeds_ProduceDifferentTeams()
    {
        var catalog = TestData.LoadCatalog();
        var rngA = RngStreams.Generation(100, 0);
        var rngB = RngStreams.Generation(200, 0);

        var teamA = TeamGenerator.Generate(ref rngA, catalog, "human_50", Race.Human, 50, 1);
        var teamB = TeamGenerator.Generate(ref rngB, catalog, "human_50", Race.Human, 50, 1);

        Assert.NotEqual(teamA.Players.Select(p => p.Name).ToList(), teamB.Players.Select(p => p.Name).ToList());
    }
}
