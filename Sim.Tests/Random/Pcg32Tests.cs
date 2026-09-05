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

    [Fact]
    public void ChanceAveraged_ZeroIsAlwaysFalse_TenThousandIsAlwaysTrue()
    {
        var rng = new Pcg32(19, 19);
        for (int i = 0; i < 100; i++)
        {
            Assert.False(rng.ChanceAveraged(0));
            Assert.True(rng.ChanceAveraged(10000));
        }
    }

    /// <summary>
    /// ADR 0050 P2: el promedio de dos tiradas <b>conserva la media</b> de la tirada y baja su desviación
    /// típica alrededor de un 30% (2.887 → 2.041 sobre 10.000, un 29,3% exacto). Es la propiedad de la que
    /// cuelga todo lo demás, y por eso se mide aquí y no solo en <c>/Balance</c>.
    /// </summary>
    [Fact]
    public void AveragedRoll_KeepsTheMean_AndCutsTheDeviationByAboutThirtyPercent()
    {
        const int Samples = 200_000;
        var uniform = new Pcg32(101, 202);
        var averaged = new Pcg32(101, 202);

        double sumU = 0, sumU2 = 0, sumA = 0, sumA2 = 0;
        for (int i = 0; i < Samples; i++)
        {
            double u = uniform.Range(0, 10000);
            sumU += u;
            sumU2 += u * u;

            double a = (averaged.Range(0, 10000) + averaged.Range(0, 10000)) / 2;
            sumA += a;
            sumA2 += a * a;
        }

        double meanU = sumU / Samples;
        double meanA = sumA / Samples;
        double sdU = Math.Sqrt((sumU2 / Samples) - (meanU * meanU));
        double sdA = Math.Sqrt((sumA2 / Samples) - (meanA * meanA));

        Assert.True(Math.Abs(meanU - meanA) < 40, $"la media se mueve: {meanU:F1} contra {meanA:F1}");
        double reduction = 1.0 - (sdA / sdU);
        Assert.True(reduction > 0.27 && reduction < 0.32, $"la desviación baja un {reduction:P1}, no un ~30%");
    }

    /// <summary>
    /// La probabilidad efectiva del promedio de dos es la <b>acumulada triangular</b>: 2p² por debajo del
    /// centro y 1−2(1−p)² por encima. Es lo que hace que la pendiente en el punto de trabajo sea 1,5 veces
    /// la de antes —más peso de la habilidad— y lo que obliga a reexpresar las bases de tiro, parada,
    /// entrada y regate en la escala nueva para no mover el punto de trabajo (docs/fase2-diseno.md §22).
    /// </summary>
    [Theory]
    [InlineData(2000)]
    [InlineData(3740)]
    [InlineData(5000)]
    [InlineData(6260)]
    [InlineData(8000)]
    public void ChanceAveraged_FollowsTheTriangularDistribution(int probability)
    {
        const int Samples = 200_000;
        var rng = new Pcg32(303, 404);
        int hits = 0;
        for (int i = 0; i < Samples; i++)
        {
            if (rng.ChanceAveraged(probability))
            {
                hits++;
            }
        }

        double p = probability / 10000.0;
        double expected = p <= 0.5 ? 2 * p * p : 1 - (2 * (1 - p) * (1 - p));
        double measured = (double)hits / Samples;

        Assert.True(
            Math.Abs(measured - expected) < 0.006,
            $"con base {probability} la triangular predice {expected:P2} y se mide {measured:P2}");
    }
}
