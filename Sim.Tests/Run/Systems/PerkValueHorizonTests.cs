using System.Text.Json.Nodes;
using Underleague.Sim.Data;
using Underleague.Sim.Run.Systems;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// AV-B: la tabla de valor gana una <b>curva por horizonte</b> (los partidos que le quedan al perk por
/// jugar) además del valor único a la campaña de referencia. Cubre <see cref="PerkValueTable.ValueAt"/>,
/// <see cref="PerkValueTable.ValueAtQuantile(long, long, int)"/>, los medios (<c>MeanValueAt</c>,
/// <c>ObservedDeviationAt</c>, <c>RowDeviationAt</c>), el recorte por los extremos del rango medido y el
/// rechazo del cargador ante una tabla inconsistente (RT-032). Aritmética entera en todo (RT-023): las
/// comparaciones son igualdades exactas, no rangos.
/// </summary>
public sealed class PerkValueHorizonTests
{
    private static PerkValueTable RealTable => SystemsTestSupport.Systems.Economy.PerkValues;

    // Perk con curva (accumulatesAcrossMatches, ADR 0070): deathless_march.
    // valuesByHorizon.deathless_march = [0, 23, 111, 126, 150, 161, 173, 178, 183, 193, 198, 205, 200, 199, 208, 207]
    // values.deathless_march = 178 (columna 8, el horizonte de referencia).
    private const string CurvedPerkId = "deathless_march";

    // Perk sin curva: vale lo mismo a cualquier horizonte.
    private const string FlatPerkId = "sweeper_keeper";

    [Fact]
    public void RealTableHasSixteenHorizonsWithReferenceEight()
    {
        Assert.Equal(16, RealTable.Horizons);
        Assert.Equal(8, RealTable.ReferenceHorizon);
    }

    [Fact]
    public void ValueAtMatchesReferenceValueAtTheReferenceHorizon()
    {
        int? reference = RealTable.ValueOf(CurvedPerkId);
        Assert.Equal(178, reference);
        Assert.Equal(reference, RealTable.ValueAt(CurvedPerkId, RealTable.ReferenceHorizon));
    }

    [Fact]
    public void ValueAtClampsBelowTheFirstMeasuredHorizon()
    {
        // Horizonte 1 es el primer partido de la campaña: -78, y es negativo porque un acumulador que
        // todavía no ha acumulado nada no da nada y sí ocupa un slot. Por debajo de 1 no hay dato, así que
        // se recorta al primero en vez de extrapolar (RT-023: sin aritmética que invente una columna).
        Assert.Equal(0, RealTable.ValueAt(CurvedPerkId, 1));
        Assert.Equal(0, RealTable.ValueAt(CurvedPerkId, 0));
        Assert.Equal(0, RealTable.ValueAt(CurvedPerkId, -5));
    }

    [Fact]
    public void ValueAtClampsAboveTheLastMeasuredHorizon()
    {
        // Horizonte 16 es el último medido: 355. Por encima, la curva es plana porque los contadores ya
        // han tocado su maxValue y no hay más partidos que arrastrarlos.
        Assert.Equal(207, RealTable.ValueAt(CurvedPerkId, 16));
        Assert.Equal(207, RealTable.ValueAt(CurvedPerkId, 99));
    }

    [Fact]
    public void ValueAtIsFlatForAPerkWithoutACurve()
    {
        int? flatValue = RealTable.ValueOf(FlatPerkId);
        Assert.Equal(7, flatValue);

        foreach (int horizon in new[] { 1, 4, 8, 16, 99 })
        {
            Assert.Equal(flatValue, RealTable.ValueAt(FlatPerkId, horizon));
        }
    }

    [Fact]
    public void ValueAtIsZeroForAnUnmeasuredPerk()
    {
        Assert.Equal(0, RealTable.ValueAt("this_perk_does_not_exist", 5));
    }

    [Fact]
    public void QuantileAtTheReferenceHorizonMatchesTheHorizonlessQuantile()
    {
        // A horizonte de referencia la distribución valorada por horizonte y la de siempre son la misma
        // columna (ADR 0072 + AV-B): ValueAt(id, referenceHorizon) == ValueOf(id) para todo perk.
        foreach (var (numerator, denominator) in new (long, long)[] { (1, 4), (1, 2), (3, 4), (9, 10) })
        {
            Assert.Equal(
                RealTable.ValueAtQuantile(numerator, denominator),
                RealTable.ValueAtQuantile(numerator, denominator, RealTable.ReferenceHorizon));
        }
    }

    [Fact]
    public void HighQuantileGrowsFromTheFirstHorizonToTheLast()
    {
        // A horizonte 1 los acumuladores todavía no han jugado casi nada: la cola alta de la oferta vale
        // menos que a horizonte 16, donde ya han tocado techo.
        int early = RealTable.ValueAtQuantile(9, 10, 1);
        int late = RealTable.ValueAtQuantile(9, 10, 16);
        Assert.True(early <= late, $"se esperaba que el cuantil alto no bajara de horizonte 1 ({early}) a 16 ({late})");
    }

    [Fact]
    public void QuantileEdgesReturnTheBoundsOfTheOfferDistribution()
    {
        // Extremos de la distribución de oferta sin horizonte (ADR 0072): mob_instigator (-7) es el
        // mínimo medido y deathless_march (178) el máximo.
        Assert.Equal(-7, RealTable.ValueAtQuantile(0, 10));
        Assert.Equal(-7, RealTable.ValueAtQuantile(-5, 10));
        Assert.Equal(178, RealTable.ValueAtQuantile(10, 10));
        Assert.Equal(178, RealTable.ValueAtQuantile(15, 10));
        Assert.Equal(0, RealTable.ValueAtQuantile(5, 0));
        Assert.Equal(0, RealTable.ValueAtQuantile(5, -3));
    }

    [Fact]
    public void QuantileEdgesReturnTheBoundsOfTheOfferDistributionByHorizon()
    {
        // Los mismos bordes, con horizonte explícito: a horizonte de referencia coinciden con los de
        // arriba porque es la misma columna.
        Assert.Equal(-7, RealTable.ValueAtQuantile(0, 10, 8));
        Assert.Equal(-7, RealTable.ValueAtQuantile(-5, 10, 8));
        Assert.Equal(178, RealTable.ValueAtQuantile(10, 10, 8));
        Assert.Equal(178, RealTable.ValueAtQuantile(15, 10, 8));
        Assert.Equal(0, RealTable.ValueAtQuantile(5, 0, 8));
        Assert.Equal(0, RealTable.ValueAtQuantile(5, -3, 8));
    }

    [Fact]
    public void UniformTableHasNoHorizonsAndFallsBackEverywhere()
    {
        var uniform = PerkValueTable.Uniform;

        Assert.Equal(0, uniform.Horizons);
        Assert.Equal(uniform.ValueAtQuantile(1, 4), uniform.ValueAtQuantile(1, 4, 5));
        Assert.Equal(uniform.MeanValue, uniform.MeanValueAt(5));
        Assert.Equal(uniform.ObservedDeviation, uniform.ObservedDeviationAt(5));
        Assert.Equal(uniform.RowDeviation, uniform.RowDeviationAt(5));
    }

    [Fact]
    public void TableWithoutACurveFallsBackToTheHorizonlessValuesEverywhere()
    {
        // Tabla mínima con "values" pero sin "valuesByHorizon": el formato de antes de AV-B, que tiene
        // que seguir cargando y comportándose exactamente igual que antes de este cambio.
        var table = PerkValueTable.FromJson(MinimalFiles());

        Assert.Equal(0, table.Horizons);
        Assert.Equal(10, table.ValueAt("perk_a", 3));

        foreach (int horizon in new[] { 1, 5, 99 })
        {
            Assert.Equal(table.ValueAtQuantile(1, 3), table.ValueAtQuantile(1, 3, horizon));
            Assert.Equal(table.MeanValue, table.MeanValueAt(horizon));
            Assert.Equal(table.ObservedDeviation, table.ObservedDeviationAt(horizon));
            Assert.Equal(table.RowDeviation, table.RowDeviationAt(horizon));
        }

        Assert.Equal(17, table.RowDeviation);
    }

    [Fact]
    public void LoaderRejectsACurveShorterThanRowDeviationByHorizon()
    {
        var root = ParseRealTable();
        var curve = root["valuesByHorizon"]![CurvedPerkId]!.AsArray();
        curve.RemoveAt(curve.Count - 1);

        var ex = Assert.Throws<DataException>(() => PerkValueTable.FromJson(FilesFor(root)));
        Assert.Contains(CurvedPerkId, ex.JsonPath, StringComparison.Ordinal);
    }

    [Fact]
    public void LoaderRejectsAReferenceHorizonColumnThatDisagreesWithValues()
    {
        var root = ParseRealTable();
        // Índice 7 (0-based) es el horizonte 8, el de referencia; values.deathless_march vale 178.
        root["valuesByHorizon"]![CurvedPerkId]![7] = 300;

        var ex = Assert.Throws<DataException>(() => PerkValueTable.FromJson(FilesFor(root)));
        Assert.Contains(CurvedPerkId, ex.JsonPath, StringComparison.Ordinal);
    }

    private static JsonObject ParseRealTable()
    {
        string path = System.IO.Path.Combine(TestData.DataDirectory, "economy", "perk-values.json");
        string content = File.ReadAllText(path);
        return JsonNode.Parse(content)!.AsObject();
    }

    private static Dictionary<string, string> FilesFor(JsonObject root) =>
        new() { [PerkValueTable.Path] = root.ToJsonString() };

    private static Dictionary<string, string> MinimalFiles()
    {
        const string json = """
            {
              "baseWeight": 100,
              "referenceValue": 500,
              "valueShift": 500,
              "valueFloor": 150,
              "minWeight": 25,
              "maxWeight": 250,
              "rowDeviation": 17,
              "values": {
                "perk_a": 10,
                "perk_b": 50,
                "perk_c": -20
              }
            }
            """;

        return new Dictionary<string, string> { [PerkValueTable.Path] = json };
    }
}
