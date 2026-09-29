using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Medical;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// ADR 0164, RF-095c (enmendada): a la tercera prótesis el jugador GANA <c>Automaton</c> y CONSERVA su
/// etiqueta de especie. Antes la perdía y <c>Simulator.ValidatePerks</c> lanzaba en el partido siguiente si un
/// perk exigía esa etiqueta (<c>gentle_giant</c>, <c>iron_gate</c>, <c>deathless_march</c>).
/// </summary>
public sealed class AutomatonTests
{
    private static readonly string[] ThreeProstheses = { "iron_arm", "peg_leg", "glass_eye" };

    /// <summary>Un jugador de la plantilla de cada posición que pueda llevar prótesis, llevado a autómata.</summary>
    private static PlayerDefinition Automaton(PlayerDefinition source, Race race)
    {
        var catalog = SystemsTestSupport.Systems.Prostheses;
        var species = SystemsTestSupport.Catalog.Race(race).SpeciesTag;
        var tags = new List<string>(source.Tags.Where(t => !PerkLoaderSpecies(t))) { species };
        var run = RunPlayer.From(source with { Race = race, SpeciesTag = species, Tags = tags });
        foreach (string id in ThreeProstheses)
        {
            run = MedicalSystem.Install(run, catalog.Find(id)!);
        }

        Assert.Contains(MedicalSystem.AutomatonTag, run.Tags);
        Assert.Equal(species, run.SpeciesTag);
        Assert.Contains(species, run.Tags);
        return run.ToDefinition(SystemsTestSupport.Catalog, applyMinorInjuryPenalty: false);
    }

    private static bool PerkLoaderSpecies(string tag) => Underleague.Sim.Perks.PerkLoader.IsSpeciesTag(tag);

    private static MatchSetup WithPlayer(PlayerDefinition replacement, ulong seed)
    {
        var setup = TestMatches.Reference(SystemsTestSupport.Catalog, seed);
        var players = setup.Home.Players.ToList();
        int at = players.FindIndex(p => p.Position == replacement.Position && setup.Home.Lineup.Slots.Any(s => s.PlayerId == p.Id));
        Assert.True(at >= 0, $"no hay titular {replacement.Position}");
        var starter = players[at];
        players[at] = replacement with { Id = starter.Id, Name = starter.Name };
        return setup with { Home = setup.Home with { Players = players } };
    }

    [Fact]
    public void AnAutomatonWithEveryRacialPerkPlaysAFullMatch()
    {
        var catalog = SystemsTestSupport.Catalog;
        var setup0 = TestMatches.Reference(catalog, 1);
        var perks = catalog.Perks.All
            .Where(p => p.Race is not null || p.TagsRequired.Any(Underleague.Sim.Perks.PerkLoader.IsSpeciesTag))
            .ToList();
        Assert.NotEmpty(perks);
        Assert.Contains(perks, p => p.Id is "gentle_giant" or "iron_gate" or "deathless_march");
        foreach (var perk in perks)
        {
            var race = perk.Race ?? Enum.Parse<Race>(perk.TagsRequired.First(Underleague.Sim.Perks.PerkLoader.IsSpeciesTag));
            var position = perk.PositionOnly ?? Position.Midfielder;
            var template = setup0.Home.Players.First(p => p.Position == position && setup0.Home.Lineup.Slots.Any(s => s.PlayerId == p.Id));
            var player = Automaton(template with { Rarity = Rarity.Legendary, Perks = new[] { perk.Id } }, race);
            var setup = WithPlayer(player, 3);
            var result = Simulator.Run(setup, 3, catalog, SimConfig.Default);
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void AnAutomatonKeepsItsRestrictedItemAndPlaysAFullMatch()
    {
        var catalog = SystemsTestSupport.Catalog;
        var items = SystemsTestSupport.Systems.Items;
        var restricted = items.All.Where(i => i.RequiredTag.Length > 0).ToList();
        Assert.NotEmpty(restricted);
        var setup0 = TestMatches.Reference(catalog, 1);
        foreach (var item in restricted)
        {
            var race = Enum.Parse<Race>(item.RequiredTag);
            var template = setup0.Home.Players.First(p => p.Position == Position.Midfielder && setup0.Home.Lineup.Slots.Any(s => s.PlayerId == p.Id));
            var player = Automaton(template, race) with { Item = RunEquipment.ToMatchItem(item) };
            Assert.True(player.Item!.AppliesTo(player), $"el objeto {item.Id} deja de aplicarse al autómata");
            Assert.NotNull(Simulator.Run(WithPlayer(player, 5), 5, catalog, SimConfig.Default));
        }
    }
}
