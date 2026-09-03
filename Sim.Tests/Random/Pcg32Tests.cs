using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Random;

public class Pcg32Tests
{
    [Fact]
    public void Constructor_And_Next_MatchOfficialReferenceVector()
    {
        // new Pcg32(42, 54): demo oficial de PCG (docs/fase0-diseno.md §2.1).
        var rng = new Pcg32(42, 54);
        uint[] expected =
        {
            0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e,
        };

        foreach (var value in expected)
        {
            Assert.Equal(value, rng.Next());
        }
    }

    [Fact]
    public void Range_StaysWithinBounds()
    {
        var rng = new Pcg32(1, 1);
        for (int i = 0; i < 10_000; i++)
        {
            int value = rng.Range(-5, 5);
            Assert.InRange(value, -5, 4);
        }
    }

    [Fact]
    public void Range_IsUniform_ChiSquareRough()
    {
        var rng = new Pcg32(1, 1);
        const int buckets = 10;
        const int samples = 100_000;
        var counts = new int[buckets];
        for (int i = 0; i < samples; i++)
        {
            counts[rng.Range(0, buckets)]++;
        }

        double expected = samples / (double)buckets;
        double chiSquare = 0;
        for (int i = 0; i < buckets; i++)
        {
            double diff = counts[i] - expected;
            chiSquare += diff * diff / expected;
        }

        // 9 grados de libertad; umbral generoso (valor crítico p~0.001 es ~27.9) para evitar falsos
        // positivos: la semilla es fija, así que el resultado es siempre el mismo.
        Assert.True(chiSquare < 30.0, $"chi-cuadrado demasiado alto: {chiSquare}");
    }

    [Fact]
    public void Shuffle_IsDeterministic_ForSameSeed()
    {
        var itemsA = Enumerable.Range(0, 20).ToList();
        var itemsB = Enumerable.Range(0, 20).ToList();
        var rngA = new Pcg32(7, 11);
        var rngB = new Pcg32(7, 11);

        rngA.Shuffle(itemsA);
        rngB.Shuffle(itemsB);

        Assert.Equal(itemsA, itemsB);
        Assert.NotEqual(Enumerable.Range(0, 20).ToList(), itemsA);
    }

    [Fact]
    public void Pick_ReturnsElementFromList()
    {
        var rng = new Pcg32(3, 3);
        var items = new List<string> { "a", "b", "c" };
        for (int i = 0; i < 100; i++)
        {
            Assert.Contains(rng.Pick(items), items);
        }
    }

    [Fact]
    public void Chance_ZeroIsAlwaysFalse_TenThousandIsAlwaysTrue()
    {
        var rng = new Pcg32(9, 9);
        for (int i = 0; i < 100; i++)
        {
            Assert.False(rng.Chance(0));
            Assert.True(rng.Chance(10000));
        }
    }
}
