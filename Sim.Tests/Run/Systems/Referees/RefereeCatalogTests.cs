using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run.Systems.Referees;

namespace Underleague.Sim.Tests.Run.Systems.Referees;

/// <summary>
/// ADR 0158 §1: carga y validación de <c>data/referees/referees.json</c>. Cierra D-22 junto con
/// <c>Sim.Tests.Engine.RefereeTraitsEngineTests</c>: el motor por fin conoce el rasgo del árbitro.
/// </summary>
public sealed class RefereeCatalogTests
{
    private static RefereeCatalog Catalog => RefereeLoader.FromJson(TestData.LoadAllFiles());

    [Fact]
    public void LoadsBetweenTwelveAndSixteenReferees()
    {
        Assert.InRange(Catalog.All.Count, 12, 16);
    }

    /// <summary>
    /// "Al menos dos de cada rasgo no neutro y dos neutros" (encargo de la ADR 0158): sin variedad real,
    /// el sorteo de 6-8 podría dar siempre el mismo plantel de rasgos.
    /// </summary>
    [Fact]
    public void HasAtLeastTwoRefereesOfEveryTrait()
    {
        var catalog = Catalog;
        var counts = new Dictionary<RefereeTrait, int>();
        foreach (var referee in catalog.All)
        {
            counts[referee.Trait] = counts.GetValueOrDefault(referee.Trait) + 1;
        }

        foreach (var trait in Enum.GetValues<RefereeTrait>())
        {
            Assert.True(
                counts.GetValueOrDefault(trait) >= 2,
                $"el rasgo {trait} tiene {counts.GetValueOrDefault(trait)} árbitros en data/referees/, hacen falta al menos 2");
        }
    }

    [Fact]
    public void OnlyOneEyedRefereesDeclareABlindSide()
    {
        foreach (var referee in Catalog.All)
        {
            if (referee.Trait == RefereeTrait.OneEyed)
            {
                Assert.NotEqual(RefereeSide.None, referee.BlindSide);
            }
            else
            {
                Assert.Equal(RefereeSide.None, referee.BlindSide);
            }
        }
    }

    [Fact]
    public void IdsAreUniqueAndSnakeCase()
    {
        var catalog = Catalog;
        var ids = catalog.All.Select(r => r.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^[a-z][a-z0-9_]*$", id));
    }

    [Fact]
    public void EveryReferee_HasNonEmptyLocalizedNameAndCatchphrase()
    {
        foreach (var referee in Catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(referee.Name.Es));
            Assert.False(string.IsNullOrWhiteSpace(referee.Name.En));
            Assert.False(string.IsNullOrWhiteSpace(referee.Catchphrase.Es));
            Assert.False(string.IsNullOrWhiteSpace(referee.Catchphrase.En));
        }
    }

    [Fact]
    public void FindReturnsTheRightDefinitionOrNull()
    {
        var catalog = Catalog;
        var first = catalog.All[0];
        Assert.Equal(first, catalog.Find(first.Id));
        Assert.Null(catalog.Find("no_such_referee"));
        Assert.Null(catalog.Find(string.Empty));
    }

    [Fact]
    public void MissingFile_ThrowsAnExplicitDataException()
    {
        var ex = Assert.Throws<DataException>(() => RefereeLoader.FromJson(new Dictionary<string, string>()));
        Assert.Equal("referees/referees.json", ex.File);
    }

    [Fact]
    public void ABlindSideOnANonOneEyedRefereeIsRejected()
    {
        string content = "{\"referees\":[{\"id\":\"x\",\"name\":{\"es\":\"X\",\"en\":\"X\"},\"trait\":\"Neutral\",\"blindSide\":\"top\",\"catchphrase\":{\"es\":\"a\",\"en\":\"a\"}}]}";
        var files = new Dictionary<string, string> { ["referees/referees.json"] = content };

        Assert.Throws<DataException>(() => RefereeLoader.FromJson(files));
    }

    [Fact]
    public void AOneEyedRefereeWithoutBlindSideIsRejected()
    {
        string content = "{\"referees\":[{\"id\":\"x\",\"name\":{\"es\":\"X\",\"en\":\"X\"},\"trait\":\"OneEyed\",\"catchphrase\":{\"es\":\"a\",\"en\":\"a\"}}]}";
        var files = new Dictionary<string, string> { ["referees/referees.json"] = content };

        Assert.Throws<DataException>(() => RefereeLoader.FromJson(files));
    }

    [Fact]
    public void ARepeatedIdIsRejected()
    {
        string content = "{\"referees\":["
            + "{\"id\":\"x\",\"name\":{\"es\":\"X\",\"en\":\"X\"},\"trait\":\"Neutral\",\"catchphrase\":{\"es\":\"a\",\"en\":\"a\"}},"
            + "{\"id\":\"x\",\"name\":{\"es\":\"Y\",\"en\":\"Y\"},\"trait\":\"Neutral\",\"catchphrase\":{\"es\":\"b\",\"en\":\"b\"}}]}";
        var files = new Dictionary<string, string> { ["referees/referees.json"] = content };

        Assert.Throws<DataException>(() => RefereeLoader.FromJson(files));
    }
}
