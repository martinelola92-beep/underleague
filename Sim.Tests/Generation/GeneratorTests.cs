using Underleague.Sim.Data;
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
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Midfielder, Rarity.Common, 4, i, "Test Player");
            Assert.InRange(player.Attributes.Strength, 1, 99);
            Assert.InRange(player.Attributes.Speed, 1, 99);
            Assert.InRange(player.Attributes.Technique, 1, 99);
            Assert.InRange(player.Attributes.Stamina, 1, 99);
            Assert.InRange(player.Attributes.Leash, 1, 99);
        }
    }

    [Fact]
    public void Generate_ExtremeLevel_StillRespectsFloorAndCap()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Orc);
        var rngLow = RngStreams.Generation(2, 0);
        var rngHigh = RngStreams.Generation(2, 1);

        for (int i = 0; i < 50; i++)
        {
            // Nivel 1 (mínimo real) y un nivel muy por encima del máximo de juego (RF-023): la robustez
            // del acotado a floor/cap no debe depender de que el llamador respete 1..8.
            var lowPlayer = PlayerGenerator.Generate(ref rngLow, catalog, race, Position.Defender, Rarity.Common, 1, i, "Low");
            var highPlayer = PlayerGenerator.Generate(ref rngHigh, catalog, race, Position.Defender, Rarity.Rare, 50, i, "High");
            AssertWithinFloorAndCap(catalog, race, Position.Defender, Rarity.Common, lowPlayer.Attributes);
            AssertWithinFloorAndCap(catalog, race, Position.Defender, Rarity.Rare, highPlayer.Attributes);
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
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Forward, Rarity.Common, 3, i, "Test Player");
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
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Goalkeeper, Rarity.Common, 3, i, "Test GK");
            Assert.InRange(player.Traits.Count, 1, 4);
            Assert.Equal(player.Traits.Count, player.Traits.Distinct().Count());
        }
    }

    [Fact]
    public void Generate_TagsIncludeSpeciesStylePositionAndTraits()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Human);
        var rng = RngStreams.Generation(5, 0);

        var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Forward, Rarity.Common, 3, 1, "Test Player");
        Assert.Contains(race.SpeciesTag, player.Tags);
        Assert.Contains(player.StyleTag.ToString(), player.Tags);
        Assert.Contains(Position.Forward.ToString(), player.Tags);
        foreach (var trait in player.Traits)
        {
            Assert.Contains(trait.ToString(), player.Tags);
        }

        Assert.Equal(race.SpeciesTag, player.SpeciesTag);
        Assert.Equal("Human", player.SpeciesTag);
    }

    [Fact]
    public void Generate_IsDeterministic_ForSameSeed()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Orc);
        var rngA = RngStreams.Generation(11, 0);
        var rngB = RngStreams.Generation(11, 0);

        var playerA = PlayerGenerator.Generate(ref rngA, catalog, race, Position.Midfielder, Rarity.Uncommon, 5, 1, "Same");
        var playerB = PlayerGenerator.Generate(ref rngB, catalog, race, Position.Midfielder, Rarity.Uncommon, 5, 1, "Same");

        Assert.Equal(playerA.Attributes, playerB.Attributes);
        Assert.Equal(playerA.StyleTag, playerB.StyleTag);
        Assert.Equal(playerA.Traits, playerB.Traits);
    }

    [Theory]
    [InlineData(Rarity.Common, 1)]
    [InlineData(Rarity.Common, 8)]
    [InlineData(Rarity.Uncommon, 1)]
    [InlineData(Rarity.Rare, 2)]
    [InlineData(Rarity.Rare, 8)]
    public void Generate_AttributeSumMatchesBudgetWithinTwoPoints(Rarity rarity, int level)
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Human);
        var generation = catalog.Tuning.Generation;
        int budget = generation.BudgetByRarity.Of(rarity) + generation.BudgetPerLevel * (level - 1);
        var rng = RngStreams.Generation(21, (int)rarity * 100 + level);

        foreach (var position in new[] { Position.Goalkeeper, Position.Defender, Position.Midfielder, Position.Forward })
        {
            for (int i = 0; i < 30; i++)
            {
                var player = PlayerGenerator.Generate(ref rng, catalog, race, position, rarity, level, i, "Budget");
                var a = player.Attributes;
                int sum = a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;
                Assert.True(Math.Abs(sum - budget) <= 2, $"{position} {rarity} L{level}: sum={sum} budget={budget}");
            }
        }
    }

    [Fact]
    public void Generate_RangeAndPositionFloorsAreRespected()
    {
        var catalog = TestData.LoadCatalog();
        var generation = catalog.Tuning.Generation;
        var rng = RngStreams.Generation(22, 0);

        foreach (var race in catalog.Races)
        {
            foreach (var position in new[] { Position.Goalkeeper, Position.Defender, Position.Midfielder, Position.Forward })
            {
                foreach (var rarity in new[] { Rarity.Common, Rarity.Uncommon, Rarity.Rare })
                {
                    for (int i = 0; i < 20; i++)
                    {
                        var player = PlayerGenerator.Generate(ref rng, catalog, race, position, rarity, 1, i, "Range");
                        AssertWithinFloorAndCap(catalog, race, position, rarity, player.Attributes);
                    }
                }
            }
        }
    }

    [Fact]
    public void Generate_StyleTagDistribution_ApproximatesRaceWeights_WithLargeSample()
    {
        var catalog = TestData.LoadCatalog();
        var race = catalog.Race(Race.Orc); // Brute 75, Fine 10, Bulwark 8, Cold 4, Neutral 3
        var rng = RngStreams.Generation(31, 0);

        const int sampleSize = 20000;
        var counts = new Dictionary<StyleTag, int>();
        foreach (var (style, _) in race.StyleTagWeights)
        {
            counts[style] = 0;
        }

        for (int i = 0; i < sampleSize; i++)
        {
            var player = PlayerGenerator.Generate(ref rng, catalog, race, Position.Midfielder, Rarity.Common, 1, i, "Sample");
            counts[player.StyleTag]++;
        }

        foreach (var (style, weight) in race.StyleTagWeights)
        {
            double expected = sampleSize * weight / 100.0;
            // Margen generoso (semilla fija, RT-056): a 20000 muestras la desviación típica de la etiqueta
            // dominante (p=0.75) es de unos 65 jugadores; se tolera un 4% absoluto (800) para no depender
            // de la implementación exacta del sorteo, solo de que siga la distribución declarada.
            double tolerance = sampleSize * 0.04;
            Assert.True(Math.Abs(counts[style] - expected) <= tolerance,
                $"{style}: esperado {expected}, obtenido {counts[style]}");
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

        Assert.Equal(1, team.Players.Count(p => p.Rarity == Rarity.Uncommon));
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
        Assert.Equal(teamA.Players.Select(p => p.StyleTag), teamB.Players.Select(p => p.StyleTag));
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

    private static void AssertWithinFloorAndCap(Catalog catalog, RaceDefinition race, Position position, Rarity rarity, Attributes attributes)
    {
        var generation = catalog.Tuning.Generation;
        var range = rarity switch
        {
            Rarity.Common => generation.RangeByRarity.Common,
            Rarity.Uncommon => generation.RangeByRarity.Uncommon,
            Rarity.Rare => generation.RangeByRarity.Rare,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
        };
        var positionFloors = generation.PositionFloors.Of(position);

        foreach (var kind in new[] { AttributeKind.Strength, AttributeKind.Speed, AttributeKind.Technique, AttributeKind.Stamina, AttributeKind.Leash })
        {
            int floor = Math.Max(generation.AttributeFloor, Math.Max(range.Min, positionFloors.TryGetValue(kind, out int f) ? f : generation.AttributeFloor));
            int cap = Math.Min(generation.AttributeCap, range.Max);
            int value = attributes.Get(kind);
            Assert.True(value >= floor && value <= cap, $"{race.Id} {position} {rarity} {kind}={value} fuera de [{floor},{cap}]");
        }
    }

    /// <summary>
    /// Paquete U: el dial de calidad desplaza los atributos punto por punto. Un equipo de calidad 60
    /// tiene veinte puntos más por atributo que uno de calidad 40, que es literalmente lo que dice medir
    /// <c>betterTeamWinRate_60_vs_40</c> (docs/balance.md). Hasta el paquete Q la calidad se traducía a
    /// <c>nivel = quality/10</c> y la diferencia real era de 3,2 puntos por atributo.
    /// </summary>
    [Fact]
    public void TeamGenerator_QualityShiftsEveryAttributePointByPoint()
    {
        var catalog = TestData.LoadCatalog();

        static double MeanAttribute(Catalog catalog, int quality)
        {
            double total = 0;
            int players = 0;
            for (int i = 0; i < 40; i++)
            {
                var rng = RngStreams.Generation(11, i);
                var team = TeamGenerator.Generate(ref rng, catalog, "t", Race.Human, quality, 1);
                foreach (var player in team.Players)
                {
                    var a = player.Attributes;
                    total += a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;
                    players++;
                }
            }

            return total / (players * 5);
        }

        double low = MeanAttribute(catalog, 40);
        double high = MeanAttribute(catalog, 60);

        Assert.InRange(high - low, 19.0, 21.0);
    }

    /// <summary>Calidad y nivel son diales independientes: el nivel suma budgetPerLevel por nivel, no calidad.</summary>
    [Fact]
    public void TeamGenerator_LevelIsIndependentOfQuality()
    {
        var catalog = TestData.LoadCatalog();

        var rngLow = RngStreams.Generation(12, 0);
        var levelOne = TeamGenerator.Generate(ref rngLow, catalog, "t", Race.Human, 50, 1, level: 1);
        var rngHigh = RngStreams.Generation(12, 0);
        var levelEight = TeamGenerator.Generate(ref rngHigh, catalog, "t", Race.Human, 50, 1, level: 8);

        static int Sum(Model.PlayerDefinition p) =>
            p.Attributes.Strength + p.Attributes.Speed + p.Attributes.Technique + p.Attributes.Stamina + p.Attributes.Leash;

        // budgetPerLevel = 8 por nivel: siete niveles de diferencia son 56 puntos de presupuesto.
        Assert.Equal(56, Sum(levelEight.Players[0]) - Sum(levelOne.Players[0]));
        Assert.All(levelEight.Players, p => Assert.Equal(8, p.Level));
    }

    /// <summary>La rareza uniforme sustituye al sorteo de RF-005: los diez jugadores comparten rareza.</summary>
    [Fact]
    public void TeamGenerator_UniformRarityAppliesToEveryPlayer()
    {
        var catalog = TestData.LoadCatalog();
        var rng = RngStreams.Generation(13, 0);

        var team = TeamGenerator.Generate(ref rng, catalog, "t", Race.Human, 50, 1, level: 2, uniformRarity: Rarity.Rare);

        Assert.All(team.Players, p => Assert.Equal(Rarity.Rare, p.Rarity));
    }
}
