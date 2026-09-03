using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Random;

public class RngStreamsTests
{
    [Fact]
    public void DifferentKinds_ProduceDifferentSequences()
    {
        var match = RngStreams.Match(123, 0);
        var map = RngStreams.Map(123, 0);
        var rewards = RngStreams.Rewards(123, 0);
        var generation = RngStreams.Generation(123, 0);

        var values = new[] { match.Next(), map.Next(), rewards.Next(), generation.Next() };
        Assert.Equal(4, values.Distinct().Count());
    }

    [Fact]
    public void DifferentIndices_ProduceDifferentSequences()
    {
        var a = RngStreams.Match(123, 0);
        var b = RngStreams.Match(123, 1);
        Assert.NotEqual(a.Next(), b.Next());
    }

    [Fact]
    public void DifferentRunSeeds_ProduceDifferentSequences()
    {
        var a = RngStreams.Match(1, 0);
        var b = RngStreams.Match(2, 0);
        Assert.NotEqual(a.Next(), b.Next());
    }

    [Fact]
    public void SameInput_ProducesSameSequence()
    {
        var a = RngStreams.Match(999, 5);
        var b = RngStreams.Match(999, 5);
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(a.Next(), b.Next());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AllStreamKinds_AreDeterministic(int kind)
    {
        Pcg32 First(ulong seed, int index) => kind switch
        {
            0 => RngStreams.Match(seed, index),
            1 => RngStreams.Map(seed, index),
            2 => RngStreams.Rewards(seed, index),
            3 => RngStreams.Generation(seed, index),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        var a = First(42, 3);
        var b = First(42, 3);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(a.Next(), b.Next());
        }
    }
}
