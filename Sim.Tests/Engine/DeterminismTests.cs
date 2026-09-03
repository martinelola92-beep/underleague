using System.Text;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Determinismo del motor (RT-020..RT-024, RT-082). Misma semilla y mismo binario, misma secuencia de
/// eventos elemento a elemento; los flujos de RNG de recompensas no tocan el partido (RT-022); y se
/// escribe la huella FNV-1a de 100 semillas para que la CI compare Windows contra Linux (RT-024).
/// </summary>
public sealed class DeterminismTests
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static readonly Catalog Catalog = TestData.LoadCatalog();

    [Fact]
    public void SameSeedSameEvents()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var first = Simulator.Run(setup, seed, Catalog, SimConfig.Default);
            var second = Simulator.Run(setup, seed, Catalog, SimConfig.Default);

            Assert.Equal(first.Events.Count, second.Events.Count);
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.Equal(first.Events[i], second.Events[i]);
            }

            Assert.Equal(first.Report.Goals[0], second.Report.Goals[0]);
            Assert.Equal(first.Report.Goals[1], second.Report.Goals[1]);
            Assert.Equal(first.Report.Ticks, second.Report.Ticks);
            Assert.Equal(first.Report.Winner, second.Report.Winner);
        }
    }

    [Fact]
    public void SameSeedSameEventsWithFreshSetupInstances()
    {
        // El setup se regenera (objetos distintos, mismos datos): el resultado no puede depender de
        // identidades de objeto ni de orden de asignación de memoria.
        const ulong Seed = 7;
        var first = Simulator.Run(TestMatches.Reference(Catalog, Seed), Seed, Catalog, SimConfig.Default);
        var second = Simulator.Run(TestMatches.Reference(Catalog, Seed), Seed, TestData.LoadCatalog(), SimConfig.Default);

        Assert.Equal(first.Events.Count, second.Events.Count);
        for (int i = 0; i < first.Events.Count; i++)
        {
            Assert.Equal(first.Events[i], second.Events[i]);
        }
    }

    [Fact]
    public void IndependentStreams()
    {
        // RT-022: la semilla del partido sale del flujo Match(runSeed, node). Consumir el flujo de
        // recompensas (o cambiar la semilla de recompensas) no altera un solo evento del partido.
        const ulong RunSeed = 987654321UL;
        const int Node = 3;

        var baseline = RunNode(RunSeed, Node, rewardsDraws: 0, rewardsSeed: RunSeed);
        var withRewardsConsumed = RunNode(RunSeed, Node, rewardsDraws: 500, rewardsSeed: RunSeed);
        var withOtherRewardsSeed = RunNode(RunSeed, Node, rewardsDraws: 500, rewardsSeed: RunSeed + 1);

        Assert.Equal(baseline.Count, withRewardsConsumed.Count);
        Assert.Equal(baseline.Count, withOtherRewardsSeed.Count);
        for (int i = 0; i < baseline.Count; i++)
        {
            Assert.Equal(baseline[i], withRewardsConsumed[i]);
            Assert.Equal(baseline[i], withOtherRewardsSeed[i]);
        }

        // Y el flujo del partido de otro nodo sí produce otro partido.
        var otherNode = RunNode(RunSeed, Node + 1, rewardsDraws: 0, rewardsSeed: RunSeed);
        Assert.NotEqual(Fingerprint(baseline), Fingerprint(otherNode));
    }

    [Fact]
    public void CrossPlatformFingerprint()
    {
        ulong hash = FnvOffsetBasis;
        int events = 0;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            events += result.Events.Count;
            for (int i = 0; i < result.Events.Count; i++)
            {
                hash = Hash(hash, Canonical(result.Events[i]));
            }
        }

        string path = Path.Combine(AppContext.BaseDirectory, "fingerprint.txt");
        File.WriteAllText(path, $"{hash:X16}\n", new UTF8Encoding(false));

        Assert.True(File.Exists(path));
        Assert.True(events > 0, "las 100 semillas de referencia deben producir eventos");
    }

    private static List<MatchEvent> RunNode(ulong runSeed, int node, int rewardsDraws, ulong rewardsSeed)
    {
        var rewards = RngStreams.Rewards(rewardsSeed, node);
        for (int i = 0; i < rewardsDraws; i++)
        {
            _ = rewards.Next();
        }

        var match = RngStreams.Match(runSeed, node);
        ulong matchSeed = ((ulong)match.Next() << 32) | match.Next();
        var setup = TestMatches.Reference(Catalog, runSeed + (ulong)node);
        return new List<MatchEvent>(Simulator.Run(setup, matchSeed, Catalog, new SimConfig(CollectLog: false)).Events);
    }

    private static ulong Fingerprint(List<MatchEvent> events)
    {
        ulong hash = FnvOffsetBasis;
        for (int i = 0; i < events.Count; i++)
        {
            hash = Hash(hash, Canonical(events[i]));
        }

        return hash;
    }

    /// <summary>Representación canónica y estable de un evento, base de la huella.</summary>
    private static string Canonical(MatchEvent e) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{(int)e.Type}|{e.Tick}|{e.Team}|{e.Actor}|{e.Target}|{e.Opponent}|{e.Cell.Column}|{e.Cell.Row}|{(int)e.Zone}|{(int)e.Phase}|{e.Bias}|{e.DistanceToGoal}|{e.Detail}");

    /// <summary>FNV-1a de 64 bits sobre los bytes UTF-8 del texto (RT-024).</summary>
    private static ulong Hash(ulong hash, string text)
    {
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        hash ^= (byte)'\n';
        hash *= FnvPrime;
        return hash;
    }
}
