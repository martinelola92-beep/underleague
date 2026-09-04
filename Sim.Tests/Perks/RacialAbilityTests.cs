using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using ProgressionRules = Underleague.Sim.Progression.Progression;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// Habilidades raciales (RF-031b, ADR 0026): un perk de <c>data/perks/</c> con su campo <c>race</c>
/// puesto, asignado automáticamente a toda la plantilla de esa raza y **sin ocupar slot** de perk.
/// </summary>
public sealed class RacialAbilityTests
{
    private const string Speed1 =
        """[{ "type": "modifyAttribute", "target": "owner", "attribute": "speed", "value": 1, "duration": "match" }]""";

    private static readonly Catalog Catalog = TestPerks.CatalogWith(
        ("filler", TestPerks.Json("filler", "MATCH_START", Speed1)),
        ("orc_only", TestPerks.Json("orc_only", "MATCH_START", Speed1, race: "Orc", rarity: "rare")));

    [Fact]
    public void EveryPlayerOfTheRaceCarriesItAndItTakesNoSlot()
    {
        var engine = Engine(Race.Human);
        var subscriptions = engine.Effects!.Subscriptions;

        // Siete titulares por equipo, catorce en total, todos humanos.
        Assert.Equal(14, subscriptions.Count(s => s.Perk.Id == "quick_learner"));

        // Y ninguno lo lleva en su lista de perks, que es la que cuenta contra Progression.PerkSlots.
        for (int id = 0; id < 7; id++)
        {
            var player = engine.PlayerById(id)!;
            Assert.DoesNotContain("quick_learner", player.Definition.Perks);
            Assert.True(player.Definition.Perks.Count <= ProgressionRules.PerkSlots(player.Definition.Rarity));
        }
    }

    /// <summary>Orcos: Sangre caliente abre el canal de duración del derribo que el motor suma al rival.</summary>
    [Fact]
    public void HotBloodedLengthensTheKnockdownTheOwnerCauses()
    {
        var engine = Engine(Race.Orc);
        var orc = engine.PlayerById(1)!;

        Assert.Equal(0, engine.Effects!.Modifiers.KnockdownTicks(orc));
        engine.Effects.Publish(MatchStart(engine));
        Assert.Equal(5, engine.Effects.Modifiers.KnockdownTicks(orc));
    }

    /// <summary>Elfos: Toque abre los dos canales de evasión, en puntos base sobre 10.000.</summary>
    [Fact]
    public void ElfTouchOpensBothEvasionChannels()
    {
        var engine = Engine(Race.Elf);
        var elf = engine.PlayerById(1)!;

        engine.Effects!.Publish(MatchStart(engine));
        Assert.Equal(1000, engine.Effects.Modifiers.Probability(elf, ProbabilityKind.TackleEvasion));
        Assert.Equal(1000, engine.Effects.Modifiers.Probability(elf, ProbabilityKind.InterceptEvasion));
    }

    /// <summary>
    /// Enanos: Raíces. El motor siembra <c>Immovable</c> desde <c>race.ability</c> (ADR 0020), y el efecto
    /// <c>immunity</c> del perk expresa lo mismo como dato, para que cualquier objeto o consumible futuro
    /// pueda concederlo sin un caso especial en el simulador.
    /// </summary>
    [Fact]
    public void RootsMakesTheDwarfImmovable()
    {
        var engine = Engine(Race.Dwarf);
        var dwarf = engine.PlayerById(1)!;

        Assert.True(dwarf.Immovable);
        engine.Effects!.Publish(MatchStart(engine));
        Assert.True(engine.Effects.Modifiers.HasImmunity(dwarf, ImmunityKind.Push));
        Assert.True(dwarf.Immovable);
    }

    [Fact]
    public void OtherRacesAreNotImmovable() => Assert.False(Engine(Race.Human).PlayerById(1)!.Immovable);

    /// <summary>No-muertos: las dos inmunidades son de fuera del partido, y se consultan en la progresión.</summary>
    [Fact]
    public void NumbGrantsTheOutOfMatchImmunities()
    {
        var undead = Player(Race.Undead);
        Assert.True(ProgressionRules.HasImmunity(undead, Catalog, ImmunityKind.Mourning));
        Assert.True(ProgressionRules.HasImmunity(undead, Catalog, ImmunityKind.MinorInjuryPenalty));

        var human = Player(Race.Human);
        Assert.False(ProgressionRules.HasImmunity(human, Catalog, ImmunityKind.Mourning));
        Assert.False(ProgressionRules.HasImmunity(human, Catalog, ImmunityKind.MinorInjuryPenalty));
    }

    /// <summary>Humanos: Adaptables actúa fuera del partido, sobre el reparto de experiencia (RF-025).</summary>
    [Fact]
    public void QuickLearnerRaisesTheExperienceOfHumansOnly()
    {
        var human = Player(Race.Human);
        var orc = Player(Race.Orc);

        Assert.Equal(125, ProgressionRules.ExperiencePercent(human, Catalog));
        Assert.Equal(100, ProgressionRules.ExperiencePercent(orc, Catalog));

        var awards = ProgressionRules.AwardExperience(
            new[] { human }, Array.Empty<PlayerDefinition>(), Catalog, Catalog.Progression, matchExperienceOverride: 100);
        Assert.Equal(125, Assert.Single(awards).Experience);

        var orcAwards = ProgressionRules.AwardExperience(
            new[] { orc }, Array.Empty<PlayerDefinition>(), Catalog, Catalog.Progression, matchExperienceOverride: 100);
        Assert.Equal(100, Assert.Single(orcAwards).Experience);
    }

    /// <summary>
    /// ADR 0023 §4: un perk exclusivo de raza exige la etiqueta de especie para surtir efecto, así que
    /// dárselo a un mercenario de otra raza (RF-110/111) no hace nada. No es un error de carga: el perk
    /// existe y es legal, simplemente no se activa.
    /// </summary>
    [Fact]
    public void ARaceExclusivePerkDoesNothingOnAnotherSpecies()
    {
        var withOrc = Engine(Race.Orc, "orc_only");
        Assert.Contains(withOrc.Effects!.Subscriptions, s => s.Perk.Id == "orc_only");

        var withHuman = Engine(Race.Human, "orc_only");
        Assert.DoesNotContain(withHuman.Effects!.Subscriptions, s => s.Perk.Id == "orc_only");
    }

    private static MatchEngine Engine(Race race, string perkId = "filler")
    {
        var setup = new MatchSetup(
            Team(race, "home", 0, perkId),
            Team(race, "away", 100, perkId),
            new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));

        return TestPerks.Engine(Catalog, setup);
    }

    private static TeamSetup Team(Race race, string id, int firstId, string perkId)
    {
        var rng = RngStreams.Generation(3, firstId);
        var team = TeamGenerator.Generate(ref rng, Catalog, id, race, 50, firstId);

        // Un perk cualquiera en un titular: sin ningún perk asignado el motor no construye el motor de
        // efectos (§3, coste cero), y con él no habría nada que observar.
        return team with
        {
            Players = team.Players
                .Select(p => p.Id == firstId + 1 ? p with { Perks = new[] { perkId } } : p)
                .ToList(),
        };
    }

    private static PlayerDefinition Player(Race race)
    {
        var rng = RngStreams.Generation(3, 0);
        return TeamGenerator.Generate(ref rng, Catalog, "t", race, 50, 0).Players[1];
    }

    private static MatchEvent MatchStart(MatchEngine engine) => new(
        EventType.MatchStart, engine.Tick, -1, -1, -1, -1,
        new Cell(0, 0), Zone.Middle, MatchPhase.Kickoff, 0, 0, "kickoff");
}
