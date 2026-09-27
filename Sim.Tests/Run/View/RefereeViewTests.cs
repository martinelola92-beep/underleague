using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>
/// ADR 0158 §6 (lo que necesita /Game): ficha del árbitro de un nodo de partido. Presentación pura, sin
/// decidir nada del partido (RT-014): compone lo que <c>IRunSystems.RefereeFor</c> ya decidió.
/// </summary>
public sealed class RefereeViewTests
{
    private static Catalog Catalog => SystemsTestSupport.Catalog;

    private static (RunState State, MapNode Node) AtAMatchNode(ulong seed) =>
        TestRuns.WalkToMatch(
            RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, SystemsTestSupport.Systems),
            Catalog,
            SystemsTestSupport.Systems);

    [Fact]
    public void ShowsTheNameTraitLineAndCatchphraseOfTheAssignedReferee()
    {
        var (state, node) = AtAMatchNode(11);
        var setup = SystemsTestSupport.Systems.RefereeFor(state, node, Catalog);
        var runReferee = state.Referees.First(r => string.Equals(r.Name, setup.Name, StringComparison.Ordinal));
        var definition = SystemsTestSupport.Systems.Referees.Find(runReferee.DefinitionId);

        var card = RefereeView.For(state, node, SystemsTestSupport.Systems, Catalog, "es");

        Assert.NotNull(definition);
        Assert.Equal(definition!.Name.Es, card.Name);
        Assert.Equal(definition.Trait, card.Trait);
        Assert.Equal(definition.Catchphrase.Es, card.Catchphrase);
        Assert.False(string.IsNullOrEmpty(card.TraitLine));
        Assert.Equal(setup.InitialBias, card.InitialBias);
        Assert.Equal(setup.BlindSide, card.BlindSide);
    }

    [Fact]
    public void SwitchesToEnglish()
    {
        var (state, node) = AtAMatchNode(11);
        var setup = SystemsTestSupport.Systems.RefereeFor(state, node, Catalog);
        var runReferee = state.Referees.First(r => string.Equals(r.Name, setup.Name, StringComparison.Ordinal));
        var definition = SystemsTestSupport.Systems.Referees.Find(runReferee.DefinitionId);

        var card = RefereeView.For(state, node, SystemsTestSupport.Systems, Catalog, "en");

        Assert.NotNull(definition);
        Assert.Equal(definition!.Name.En, card.Name);
        Assert.Equal(definition.Catchphrase.En, card.Catchphrase);
    }

    /// <summary>La ficha enseña la memoria que el árbitro tiene del jugador (ADR 0158 §4).</summary>
    [Fact]
    public void ShowsTheRefereesGrudge()
    {
        var (state, node) = AtAMatchNode(11);
        var setup = SystemsTestSupport.Systems.RefereeFor(state, node, Catalog);
        var withGrudge = state.WithReferees(state.Referees.Select(r =>
            string.Equals(r.Name, setup.Name, StringComparison.Ordinal) ? r with { Grudge = 9 } : r));

        var card = RefereeView.For(withGrudge, node, SystemsTestSupport.Systems, Catalog, "es");

        Assert.Equal(9, card.Grudge);
    }

    /// <summary>Cada rasgo tiene su propia línea, distinta de la de los demás (RT-035: sin texto a mano).</summary>
    [Fact]
    public void EveryTraitHasItsOwnNonEmptyLine()
    {
        var (state, node) = AtAMatchNode(11);
        var lines = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trait in Enum.GetValues<RefereeTrait>())
        {
            var withTrait = state.WithReferees(state.Referees.Select(r => r with { Trait = trait }));
            var card = RefereeView.For(withTrait, node, SystemsTestSupport.Systems, Catalog, "es");
            Assert.False(string.IsNullOrEmpty(card.TraitLine));
            lines.Add(card.TraitLine);
        }

        Assert.Equal(Enum.GetValues<RefereeTrait>().Length, lines.Count);
    }
}
