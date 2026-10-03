using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-U (ADR 0182): «Inamovible», el tercer perk común de Bulwark. Al portador de estilo Bulwark al que le
/// entran le sube la resistencia a la entrada en esa jugada (RF-069c); a cualquier otro estilo ni se le puede asignar.
/// </summary>
public sealed class ImmovablePerkTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void ABulwarkHolderResistsTheTackleAndOnlyOnce()
    {
        var engine = Engine(StyleTag.Bulwark);
        var holder = engine.PlayerById(1)!;

        engine.Effects!.Publish(TackleOn(engine, holder.Id));
        int once = engine.Effects.Modifiers.Probability(holder, ProbabilityKind.TackleEvasion);
        Assert.True(once > ProbabilityScale.Neutral, "tiene que ayudar a evitar la entrada, no empeorarla");

        // limit per play, una vez: una segunda entrada de la misma jugada no compone el modificador (Regla I).
        engine.Effects.Publish(TackleOn(engine, holder.Id));
        Assert.Equal(once, engine.Effects.Modifiers.Probability(holder, ProbabilityKind.TackleEvasion));
    }

    [Fact]
    public void OnlyABulwarkPlayerCanCarryIt()
    {
        // tagsRequired: no sale como carta muerta a quien no es Bulwark (el motor ni deja asignarlo).
        var perk = Catalog.Perks.Get("immovable");
        Assert.Contains("Bulwark", perk.TagsRequired);
        Assert.Equal("wall", perk.Family);
        Assert.Throws<ArgumentException>(() => Simulator.Run(Setup(StyleTag.Brute), 1UL, Catalog, new SimConfig(CollectLog: false)));
    }

    private static MatchSetup Setup(StyleTag style) => new(
        Team("home", 0, style),
        Team("away", 100, StyleTag.Neutral),
        new RefereeSetup("Neutral", RefereeTrait.Neutral, 0));

    private static MatchEngine Engine(StyleTag style) => TestPerks.Engine(Catalog, Setup(style));

    private static TeamSetup Team(string id, int firstId, StyleTag style)
    {
        var rng = RngStreams.Generation(3, firstId);
        var team = TeamGenerator.Generate(ref rng, Catalog, id, Race.Dwarf, 50, firstId);
        return team with
        {
            Players = team.Players
                .Select(p => p.Id == firstId + 1
                    ? p with
                    {
                        Perks = new[] { "immovable" },
                        StyleTag = style,
                        Tags = p.Tags.Where(t => !Enum.TryParse<StyleTag>(t, out _)).Append(style.ToString()).ToList(),
                    }
                    : p)
                .ToList(),
        };
    }

    private static MatchEvent TackleOn(MatchEngine engine, int victimId) => new(
        EventType.Tackle, engine.Tick, 1, 101, -1, victimId,
        new Cell(0, 0), Zone.Middle, MatchPhase.OpenPlay, 0, 0, "attempted");
}
