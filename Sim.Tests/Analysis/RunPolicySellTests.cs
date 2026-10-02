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

    /// <summary>
    /// BT-A: el caso que lanzaba —<c>Play</c> entero con <c>SystemsTestSupport.Setup()</c>, las razas de lanzamiento por 20
    /// semillas (con tres razas, 9 de 60 runs lanzaban «todavía no ha jugado un partido con el club»)— completa todas las runs.
    /// </summary>
    [Fact]
    public void AWholePolicyRunNeverTriesToSellASigningWhoHasNotPlayed()
    {
        var bosses = Underleague.Sim.Run.Bosses.BossCatalog.FromJson(TestData.LoadAllFiles());
        var races = SystemsTestSupport.Catalog.Races.Where(r => r.Launch).Select(r => r.Id).ToList();
        var failures = new List<string>();
        foreach (var race in races)
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                try
                {
                    RunPolicy.Play(SystemsTestSupport.Setup(race), seed, SystemsTestSupport.Catalog, SystemsTestSupport.Systems, bosses);
                }
                catch (ArgumentException e) when (e.Message.Contains("no se puede vender", StringComparison.Ordinal))
                {
                    failures.Add($"{race}/{seed}");
                }
            }
        }

        Assert.True(failures.Count == 0, "la política intentó vender a un fichaje sin partidos en: " + string.Join(", ", failures));
    }
}
