using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Xunit;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// La escala multiplicativa de la ADR 0050 P1. Lo que se afirma aquí es lo que la ADR promete y lo que
/// justifica retirar la tabla por canal de la ADR 0035: que un perk vale <b>lo mismo</b> en cualquier
/// canal, cerca de 0 y cerca de 1, y que la operación es reversible.
/// </summary>
public sealed class ProbabilityScaleTests
{
    /// <summary>Los catorce valores legales y ninguno más; el negativo es el inverso del positivo.</summary>
    [Theory]
    [InlineData(15, 11500)]
    [InlineData(30, 13000)]
    [InlineData(50, 15000)]
    [InlineData(100, 20000)]
    [InlineData(200, 30000)]
    [InlineData(300, 40000)]
    [InlineData(500, 60000)]
    [InlineData(-15, 8696)]
    [InlineData(-30, 7692)]
    [InlineData(-50, 6667)]
    [InlineData(-100, 5000)]
    [InlineData(-200, 3333)]
    [InlineData(-300, 2500)]
    [InlineData(-500, 1667)]
    public void EachLegalPercentBecomesItsMultiplier(int percent, int expected)
    {
        Assert.True(ProbabilityScale.IsLegal(percent));
        Assert.Equal(expected, ProbabilityScale.ToMultiplier(percent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(150)]
    [InlineData(400)]
    [InlineData(-25)]
    public void AnythingElseIsIllegal(int percent) => Assert.False(ProbabilityScale.IsLegal(percent));

    /// <summary>
    /// El negativo deshace al positivo: componer <c>k</c> con <c>1/k</c> devuelve el neutro, y por tanto
    /// un perk y su contrario se cancelan en vez de dejar residuo (que es lo que pasaba al restar puntos
    /// contra un suelo o un techo).
    /// </summary>
    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(500)]
    public void APositiveAndItsNegativeCancel(int percent)
    {
        int combined = ProbabilityScale.Combine(
            ProbabilityScale.ToMultiplier(percent), ProbabilityScale.ToMultiplier(-percent));

        // Uno o dos puntos de 10.000 de deriva por el redondeo entero del inverso; nada observable.
        Assert.InRange(combined, ProbabilityScale.Neutral - 2, ProbabilityScale.Neutral + 2);
    }

    /// <summary>
    /// <b>La</b> propiedad de la ADR 0050 P1: el mismo <c>k</c> multiplica la cuota por el mismo factor
    /// en un canal de base diminuta y en uno de base alta. Con la fórmula aditiva, un <c>+5</c> triplicaba
    /// la intercepción (base 2,5%) y movía el pase (base 77%) un 6,5%.
    /// </summary>
    [Theory]
    [InlineData(250)]    // intercept
    [InlineData(3740)]   // tackle
    [InlineData(5100)]   // save
    [InlineData(7700)]   // pass
    [InlineData(9500)]
    public void TheOddsRatioIsTheSameInEveryChannel(int probability)
    {
        int multiplier = ProbabilityScale.ToMultiplier(50);
        int after = ProbabilityScale.Apply(probability, multiplier);

        long oddsBefore = 10000L * probability / (ProbabilityScale.Full - probability);
        long oddsAfter = 10000L * after / (ProbabilityScale.Full - after);

        // 15.000 milésimas = ×1,5, con margen para el redondeo entero de la probabilidad resultante.
        Assert.InRange(10000L * oddsAfter / oddsBefore, 14900, 15100);
    }

    /// <summary>El 0 y el 100% son puntos fijos: lo imposible sigue siéndolo y lo seguro también.</summary>
    [Fact]
    public void ImpossibleAndCertainDoNotMove()
    {
        int doubled = ProbabilityScale.ToMultiplier(100);
        Assert.Equal(0, ProbabilityScale.Apply(0, doubled));
        Assert.Equal(10000, ProbabilityScale.Apply(10000, doubled));
        Assert.Equal(-500, ProbabilityScale.Apply(-500, doubled));
    }

    /// <summary>Sin multiplicador no se toca nada, ni siquiera un punto de redondeo.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(7700)]
    [InlineData(9999)]
    public void TheNeutralMultiplierIsTheIdentity(int probability)
    {
        Assert.Equal(probability, ProbabilityScale.Apply(probability, ProbabilityScale.Neutral));
        Assert.Equal(probability, ProbabilityScale.ApplyAveraged(probability, ProbabilityScale.Neutral));
    }

    /// <summary>
    /// En un canal de tirada promediada (ADR 0050 P2) lo que se multiplica es la cuota del suceso que
    /// <b>ocurre</b>, no la del parámetro. Sin esto, un ×2 sobre el parámetro de la entrada valdría ×3,6
    /// sobre la entrada de verdad y el defecto que la P1 quita volvería por la puerta de atrás.
    /// </summary>
    [Theory]
    [InlineData(3740)]   // tackle
    [InlineData(6260)]   // dribble
    [InlineData(5100)]   // save
    public void OnAnAveragedChannelTheRatioIsOnTheRealisedProbability(int raw)
    {
        int multiplier = ProbabilityScale.ToMultiplier(100);
        int after = ProbabilityScale.ApplyAveraged(raw, multiplier);

        int before = ProbabilityScale.Triangular(raw);
        int realised = ProbabilityScale.Triangular(after);
        long oddsBefore = 10000L * before / (ProbabilityScale.Full - before);
        long oddsAfter = 10000L * realised / (ProbabilityScale.Full - realised);

        Assert.InRange(10000L * oddsAfter / oddsBefore, 19700, 20300);
    }

    /// <summary>La acumulada triangular y su inversa son inversas de verdad, con redondeo entero.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(3740)]
    [InlineData(5000)]
    [InlineData(6260)]
    [InlineData(9800)]
    public void TheTriangularRoundTripIsStable(int probability)
    {
        int back = ProbabilityScale.InverseTriangular(ProbabilityScale.Triangular(probability));
        Assert.InRange(back, probability - 1, probability + 1);
    }

    /// <summary>
    /// Un contador acumula <b>copias</b> del perk, no un multiplicador mayor: cinco veces ×1,5 son ×7,59,
    /// que es lo que deja al eje de acumulación (RF-070) crecer más allá del ×2 de la escala.
    /// </summary>
    [Fact]
    public void CounterStacksCompound()
    {
        int oneAndAHalf = ProbabilityScale.ToMultiplier(50);
        Assert.Equal(ProbabilityScale.Neutral, ProbabilityScale.Power(oneAndAHalf, 0));
        Assert.Equal(oneAndAHalf, ProbabilityScale.Power(oneAndAHalf, 1));
        Assert.InRange(ProbabilityScale.Power(oneAndAHalf, 5), 75800, 76000);
    }

    /// <summary>
    /// La descripción habla de <b>cuota</b> (ADR 0058): un ×1,3 se lee "multiplica por 1,3" y su inverso
    /// "divide por 1,3". La misma cifra con dos verbos, y las dos son exactas, que es justo lo que la
    /// convención de proporción de probabilidad no conseguía (en <c>pass</c>, base 77%, "un 30% más" era
    /// en realidad un 5,6%).
    /// </summary>
    [Theory]
    [InlineData(15, "1,15", "1.15")]
    [InlineData(30, "1,3", "1.3")]
    [InlineData(50, "1,5", "1.5")]
    [InlineData(100, "2", "2")]
    [InlineData(200, "3", "3")]
    [InlineData(300, "4", "4")]
    [InlineData(500, "6", "6")]
    public void TheDescribedFactorIsTheSameUpAndDown(int percent, string spanish, string english)
    {
        int up = ProbabilityScale.ToMultiplier(percent);
        int down = ProbabilityScale.ToMultiplier(-percent);

        Assert.Equal(spanish, ProbabilityScale.FactorText(up, ','));
        Assert.Equal(spanish, ProbabilityScale.FactorText(down, ','));
        Assert.Equal(english, ProbabilityScale.FactorText(up, '.'));
        Assert.Equal(percent, ProbabilityScale.Magnitude(up));
        Assert.Equal(percent, ProbabilityScale.Magnitude(down));
        Assert.True(ProbabilityScale.IsIncrease(up));
        Assert.False(ProbabilityScale.IsIncrease(down));
    }

    /// <summary>
    /// El techo por rareza de la ADR 0058: cada rareza añade un escalón, y en un efecto con contador el
    /// techo baja uno más porque el multiplicador se aplica hasta n veces.
    /// </summary>
    [Theory]
    [InlineData(Rarity.Common, 100, 50)]
    [InlineData(Rarity.Uncommon, 200, 100)]
    [InlineData(Rarity.Rare, 300, 200)]
    [InlineData(Rarity.Legendary, 500, 300)]
    public void EachRarityBuysOneMoreRung(Rarity rarity, int ceiling, int counterCeiling)
    {
        Assert.Equal(ceiling, ProbabilityScale.CeilingFor(rarity));
        Assert.Equal(counterCeiling, ProbabilityScale.CounterCeilingFor(rarity));
        Assert.True(ProbabilityScale.IsLegalUpTo(ceiling, ceiling));
        Assert.True(ProbabilityScale.IsLegalUpTo(-ceiling, ceiling));
    }

    /// <summary>Un común no puede llegar a lo que llega un legendario, y eso es lo que compra la rareza.</summary>
    [Fact]
    public void TheCeilingIsStrictlyBelowTheNextRarity()
    {
        Assert.False(ProbabilityScale.IsLegalUpTo(200, ProbabilityScale.CeilingFor(Rarity.Common)));
        Assert.False(ProbabilityScale.IsLegalUpTo(-300, ProbabilityScale.CeilingFor(Rarity.Uncommon)));
        Assert.False(ProbabilityScale.IsLegalUpTo(500, ProbabilityScale.CeilingFor(Rarity.Rare)));
        Assert.True(ProbabilityScale.IsLegalUpTo(500, ProbabilityScale.CeilingFor(Rarity.Legendary)));
    }
}
