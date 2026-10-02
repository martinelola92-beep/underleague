using Underleague.Sim.Analysis;
using Underleague.Sim.Run;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>BT-A, RF-114f, ADR 0108: la política no intenta vender a un fichaje que no ha jugado.</summary>
public sealed class RunPolicySellTests
{
    [Fact]
    public void ThePolicyNeverOffersToSellASigningWhoHasNotPlayed()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 7, SystemsTestSupport.Catalog, SystemsTestSupport.Systems);
        // Un suplente flojo sin partidos (el que la política escogía) y otro algo mejor que sí ha jugado.
        var weak = state.Roster[^1] with { Experience = 0, Attributes = new Underleague.Sim.Model.Attributes(1, 1, 1, 1, 1) };
        var played = state.Roster[^2] with { Experience = 5, Attributes = new Underleague.Sim.Model.Attributes(10, 10, 10, 10, 10) };
        state = state.WithPlayer(weak).WithPlayer(played);
        var starters = new List<RunPlayer>();
        for (int i = 0; i < state.Roster.Count - 2; i++)
        {
            starters.Add(state.Roster[i]);
        }

        var chosen = RunPolicy.WorstSellableForTest(state, starters, RunPolicyOptions.Default);
        Assert.NotNull(chosen);
        Assert.NotEqual(weak.Id, chosen!.Id);
        Assert.True(chosen.Experience > 0);
    }
}
