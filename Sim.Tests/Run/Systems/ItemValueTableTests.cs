using System.Text.Json.Nodes;
using Underleague.Sim.Data;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Tests.Run.Systems;

/// <summary>
/// AT-A (paso 1): la tabla de <b>valor medido de cada objeto</b>, en la misma unidad que la de perks.
/// Cubre la carga de <c>data/economy/item-values.json</c>, <see cref="ItemValueTable.ValueOf"/> sobre un
/// objeto real y sobre uno inexistente, el caso "instantánea sin fichero" y el rechazo del cargador ante
/// datos inconsistentes (RT-032). Aritmética entera en todo (RT-023): las comparaciones son igualdades
/// exactas.
/// </summary>
public sealed class ItemValueTableTests
{
    private static ItemValueTable RealTable => SystemsTestSupport.Systems.Economy.ItemValues;

    private static ItemCatalog RealItems => SystemsTestSupport.Systems.Items;

    [Fact]
    public void RealTableMeasuresEveryItemOfTheCatalogAndNothingElse()
    {
        // Todo objeto del catálogo es medible con el espejo: los restringidos exigen la etiqueta de
        // especie de su raza y la plantilla de prueba se genera de esa raza, así que los siete titulares
        // pueden llevarlo. Una entrada de más o de menos significa que la tabla y /data/items se han
        // separado: hay que remedirla con /Balance --item-values.
        Assert.Equal(RealItems.All.Count, RealTable.Count);

        foreach (var item in RealItems.All)
        {
            Assert.NotNull(RealTable.ValueOf(item.Id));
        }
    }

    [Fact]
    public void ValueOfAnUnknownItemIsNull()
    {
        Assert.Null(RealTable.ValueOf("no_such_item"));
        Assert.Null(RealTable.ValueOf(string.Empty));
    }

    [Fact]
    public void RealTableDeclaresItsMeasurementNoise()
    {
        // La desviación por fila es lo que impedirá que un umbral en el cero exacto sea un umbral, igual
        // que en la tabla de perks (ADR 0072). Tiene que estar declarada y ser menor que la dispersión
        // observada: si no, la tabla sería sólo ruido y no ordenaría nada.
        Assert.True(RealTable.RowDeviation > 0);
        Assert.True(RealTable.ObservedDeviation > RealTable.RowDeviation);
    }

    [Fact]
    public void ASnapshotWithoutTheFileHasNoMeasuredValues()
    {
        var table = ItemValueTable.FromJson(new Dictionary<string, string>());

        Assert.Same(ItemValueTable.Empty, table);
        Assert.Equal(0, table.Count);
        Assert.Equal(0, table.RowDeviation);
        Assert.Equal(0, table.MeanValue);
        Assert.Equal(0, table.ObservedDeviation);
        Assert.Null(table.ValueOf("worn_boots"));
    }

    [Fact]
    public void MeanAndDeviationAreIntegerArithmeticOverTheDeclaredValues()
    {
        // values = {10, 50, -20}: suma 40, media entera 13; cuadrados 9 + 1369 + 1089 = 2467, /3 = 822,
        // raíz 28,67 -> 28. Sin coma flotante en el resultado (RT-023).
        var table = ItemValueTable.FromJson(MinimalFiles());

        Assert.Equal(3, table.Count);
        Assert.Equal(13, table.MeanValue);
        Assert.Equal(28, table.ObservedDeviation);
        Assert.Equal(7, table.RowDeviation);
        Assert.Equal(10, table.ValueOf("item_a"));
        Assert.Equal(-20, table.ValueOf("item_c"));
    }

    [Fact]
    public void LoaderRejectsATablePresentButEmpty()
    {
        // Un fichero con values vacío es una regeneración a medias, no "no hay tabla": el caso de no
        // tener tabla es no tener fichero (RT-032, error explícito y nunca silencioso).
        const string json = """
            {
              "rowDeviation": 7,
              "values": {}
            }
            """;

        var ex = Assert.Throws<DataException>(() => ItemValueTable.FromJson(FilesFor(json)));
        Assert.Equal("$.values", ex.JsonPath);
    }

    [Fact]
    public void LoaderRejectsATableWithoutRowDeviation()
    {
        const string json = """
            {
              "values": { "item_a": 10 }
            }
            """;

        var ex = Assert.Throws<DataException>(() => ItemValueTable.FromJson(FilesFor(json)));
        Assert.Contains("rowDeviation", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoaderRejectsANegativeRowDeviation()
    {
        const string json = """
            {
              "rowDeviation": -1,
              "values": { "item_a": 10 }
            }
            """;

        var ex = Assert.Throws<DataException>(() => ItemValueTable.FromJson(FilesFor(json)));
        Assert.Equal("$.rowDeviation", ex.JsonPath);
    }

    [Fact]
    public void LoaderRejectsInvalidJson()
    {
        var ex = Assert.Throws<DataException>(() => ItemValueTable.FromJson(FilesFor("{ no es json")));
        Assert.Equal(ItemValueTable.Path, ex.File);
    }

    [Fact]
    public void RealFileParsesToTheSameTableAsTheLoadedSnapshot()
    {
        // La tabla que llega por EconomyConfig y la que sale de leer el fichero a pelo son la misma: el
        // campo nuevo de EconomyConfig está efectivamente cableado al fichero.
        string path = System.IO.Path.Combine(TestData.DataDirectory, "economy", "item-values.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var direct = ItemValueTable.FromJson(FilesFor(root.ToJsonString()));

        Assert.Equal(RealTable.Count, direct.Count);
        Assert.Equal(RealTable.RowDeviation, direct.RowDeviation);
        Assert.Equal(RealTable.MeanValue, direct.MeanValue);
        Assert.Equal(RealTable.ObservedDeviation, direct.ObservedDeviation);
    }

    private static Dictionary<string, string> FilesFor(string json) =>
        new() { [ItemValueTable.Path] = json };

    private static Dictionary<string, string> MinimalFiles()
    {
        const string json = """
            {
              "rowDeviation": 7,
              "values": {
                "item_a": 10,
                "item_b": 50,
                "item_c": -20
              }
            }
            """;

        return FilesFor(json);
    }
}
