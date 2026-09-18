using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// La lectura de plantilla que necesita el Mercado (BB-J, RF-012d): un perk a la venta todavía no tiene
/// portador ni colocación, así que <see cref="LineupPerkPreviewer"/> no puede decir nada. Lo que sí se
/// puede decir es cuántos jugadores de la plantilla llevan la etiqueta que el perk cuenta.
/// </summary>
public sealed class PerkSquadRequirementsTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static List<PlayerDefinition> Squad(params string[] tagsPerPlayer)
    {
        var attributes = new Attributes(50, 50, 50, 50, 50);
        var players = new List<PlayerDefinition>();
        for (int i = 0; i < tagsPerPlayer.Length; i++)
        {
            players.Add(new PlayerDefinition(
                i + 1, "p" + (i + 1), Race.Human, Position.Midfielder, Rarity.Common, 1,
                attributes, Array.Empty<Trait>(), new List<string> { tagsPerPlayer[i] }, PhysicalState.Healthy));
        }

        return players;
    }

    private static PerkDefinition Perk(string id) => Catalog.Perks.All.Single(p => p.Id == id);

    /// <summary>El caso del encargo: "tienes 1 de 2 Finos en la plantilla".</summary>
    [Fact]
    public void CountsTheTagAcrossTheWholeSquad()
    {
        var requirement = PerkSquadRequirements.For(Perk("first_touch_school"), Squad("Fine", "Neutral", "Neutral")).Single();

        Assert.Equal("teammatesWithTag", requirement.Function);
        Assert.Equal("Fine", requirement.Tag);
        Assert.Equal(1, requirement.Current);
        Assert.Equal(2, requirement.Required);
        Assert.False(requirement.Met);
    }

    /// <summary>`pack_mentality`, el perk de BB-J: con enanos (Bulwark) no hay Brutos que contar.</summary>
    [Fact]
    public void PackMentalityOnADwarfSquadShowsZeroOfThree()
    {
        var requirement = PerkSquadRequirements.For(
            Perk("pack_mentality"), Squad("Bulwark", "Bulwark", "Bulwark", "Bulwark")).Single();

        Assert.Equal("Brute", requirement.Tag);
        Assert.Equal(0, requirement.Current);
        Assert.Equal(3, requirement.Required);
        Assert.False(requirement.Met);
    }

    [Fact]
    public void PackMentalityOnAnOrcSquadIsAlreadyMet()
    {
        var requirement = PerkSquadRequirements.For(
            Perk("pack_mentality"), Squad("Brute", "Brute", "Brute", "Neutral")).Single();

        Assert.Equal(3, requirement.Current);
        Assert.True(requirement.Met);
    }

    /// <summary>Un `hasTag` suelto: hace falta alguien que la lleve, y se dice cuántos hay.</summary>
    [Fact]
    public void HasTagReportsHowManyCouldCarryIt()
    {
        var requirement = PerkSquadRequirements.For(Perk("bulwark_stance"), Squad("Bulwark", "Neutral", "Bulwark")).Single();

        Assert.Equal("hasTag", requirement.Function);
        Assert.Equal("Bulwark", requirement.Tag);
        Assert.Equal(2, requirement.Current);
        Assert.Equal(1, requirement.Required);
        Assert.True(requirement.Met);
    }

    /// <summary>
    /// Lo que NO cuenta: un perk sin condición, y uno cuya condición depende del partido o de la
    /// colocación. No se inventa un conteo donde no lo hay.
    /// </summary>
    [Theory]
    [InlineData("charge")]            // sin condición
    [InlineData("back_to_back")]      // nearAlly: depende de la jugada
    [InlineData("last_ditch")]        // zone(actor): depende de la jugada
    [InlineData("captains_voice")]    // startsIn: depende de la colocación
    public void NothingCountableMeansNoRequirement(string perkId)
    {
        Assert.Empty(PerkSquadRequirements.For(Perk(perkId), Squad("Fine", "Brute", "Bulwark")));
    }

    /// <summary>Todos los perks del catálogo se leen sin excepción, y los que cuentan salen con etiqueta real.</summary>
    [Fact]
    public void EveryPerkInTheCatalogCanBeRead()
    {
        var squad = Squad("Fine", "Brute", "Bulwark", "Cold", "Neutral");
        int withRequirements = 0;
        foreach (var perk in Catalog.Perks.All)
        {
            foreach (var requirement in PerkSquadRequirements.For(perk, squad))
            {
                withRequirements++;
                Assert.False(string.IsNullOrEmpty(requirement.Tag));
                Assert.True(requirement.Required >= 1);
                Assert.True(requirement.Current >= 0);
            }
        }

        Assert.True(withRequirements > 0, "el catálogo tiene perks de conteo: si sale 0, algo dejó de leerse");
    }
}
