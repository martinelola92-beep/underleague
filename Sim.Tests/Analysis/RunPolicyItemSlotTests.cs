using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// El <b>listón del objeto de mercado</b> (AT-A, paso 2): comprar un objeto no cuesta sólo oro, cuesta
/// además el slot irreversible del titular que lo lleva, y hasta este paquete el mercado no preguntaba
/// nunca si el objeto lo valía —miraba rareza, precio y encaje de atributos, y nada más—.
///
/// <para><b>Por qué se prueba de punta a punta y no la función sola.</b> El gate vive dentro de
/// <c>NextMarketAction</c>, que es privado, y lo que hay que afirmar no es que una comparación devuelva
/// <c>false</c> sino que <b>no se compra el objeto</b> cuando devuelve <c>false</c> — con presupuesto de
/// sobra y encaje bueno, que es justo el caso en el que el mercado antes compraba—. Se mide con
/// <see cref="RunPlayResult.ItemsBought"/>, que cuenta <b>sólo</b> las compras de objeto en el mercado:
/// los objetos de recompensa no pasan por aquí y este paquete no los toca.</para>
///
/// <para>El listón se fija constante con <see cref="RunPolicyOptions.MinItemValueMarket"/>, igual que la
/// ADR 0072/0073 hizo con los perks: así la afirmación es del gate y no del coste de oportunidad del
/// slot, que cambia de nodo a nodo.</para>
/// </summary>
public sealed class RunPolicyItemSlotTests
{
    /// <summary>Runs por configuración. Bastan pocas: lo que se afirma es cualitativo (cero o no cero).</summary>
    private const int Runs = 12;

    private const ulong Seed = 1;

    private const string ClubId = "item_slot_club";

    /// <summary>Calidad de la plantilla inicial, la misma que usan las demás puertas de run.</summary>
    private const int StartingQuality = 50;

    /// <summary>Listón que ninguna tabla de objetos alcanza: con él el gate cierra siempre.</summary>
    private const int AboveEveryTable = 1_000_000;

    /// <summary>Listón que toda tabla supera: con él el gate abre siempre, que es el mercado de antes de AT-A.</summary>
    private const int BelowEveryTable = -1_000_000;

    private static readonly Lazy<World> Data = new(Load);

    /// <summary>Los mismos sistemas pero <b>sin</b> tabla de valor de objetos, construidos una sola vez.</summary>
    private static readonly Lazy<StandardRunSystems> Unmeasured = new(WithoutItemValues);

    /// <summary>
    /// Memoria de lotes ya jugados. Los tests comparten configuraciones —el gate abierto sale en tres— y
    /// una run completa cuesta lo que cuesta: sin esto la clase juega el mismo lote tres veces.
    /// </summary>
    private static readonly Dictionary<(RunPolicyOptions Options, bool Measured), int> Memo = new();

    private sealed record World(
        Catalog Catalog,
        IReadOnlyDictionary<string, string> Files,
        StandardRunSystems Standard,
        BossCatalog Bosses,
        IReadOnlyList<Race> Races);

    /// <summary>
    /// Las dos doctrinas puras no miran valor —una compra lo más barato, la otra lo más raro— y son
    /// precisamente lo que las hace comparables con la contextual. El gate no puede tocarlas: con un
    /// listón inalcanzable compran exactamente los mismos objetos que sin listón.
    /// </summary>
    [Theory]
    [InlineData(PurchaseDoctrine.Spender)]
    [InlineData(PurchaseDoctrine.Saver)]
    public void TheGateNeverBlocksADoctrineThatDoesNotLookAtValue(PurchaseDoctrine doctrine)
    {
        int free = ItemsBought(RunPolicyOptions.For(doctrine));
        int barred = ItemsBought(RunPolicyOptions.For(doctrine) with { MinItemValueMarket = AboveEveryTable });

        Assert.True(free > 0, $"{doctrine} no compró ningún objeto en el mercado: la muestra no prueba nada");
        Assert.Equal(free, barred);
    }

    /// <summary>
    /// Una instantánea de <c>/data</c> sin <c>economy/item-values.json</c> tiene que comportarse como
    /// antes de que los objetos estuvieran medidos, no como si todos valieran cero: sin tabla no hay
    /// gate, exactamente igual que un perk sin tabla no lo tendría.
    /// </summary>
    [Fact]
    public void WithoutAMeasuredTableThereIsNoGate()
    {
        var unmeasured = Unmeasured.Value;
        var contextual = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        int free = ItemsBought(contextual with { MinItemValueMarket = BelowEveryTable }, unmeasured);
        int barred = ItemsBought(contextual with { MinItemValueMarket = AboveEveryTable }, unmeasured);

        Assert.True(free > 0, "sin tabla y sin listón no se compró ningún objeto: la muestra no prueba nada");
        Assert.Equal(free, barred);
    }

    /// <summary>
    /// Con la tabla medida y un listón por encima de su nivel, la contextual <b>no compra ni un objeto
    /// en el mercado</b>: ni el que mejor encaja, ni el que le sobra oro para pagar. Es la afirmación
    /// central del paquete — hasta AT-A no había forma de que el mercado dijera que no a un objeto.
    /// </summary>
    [Fact]
    public void ABarAboveTheLevelOfTheTableStopsEveryMarketItem()
    {
        var contextual = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        int free = ItemsBought(contextual with { MinItemValueMarket = BelowEveryTable });
        int barred = ItemsBought(contextual with { MinItemValueMarket = AboveEveryTable });

        Assert.True(free > 0, "con el gate abierto no se compró ningún objeto: la muestra no prueba nada");
        Assert.Equal(0, barred);
    }

    /// <summary>
    /// El corte está donde dice la comparación y no un punto más allá: al <b>nivel exacto</b> de la tabla
    /// (<c>MeanValue</c>) el gate deja pasar —es un <c>&gt;=</c>—, y una milésima por encima cierra. Que
    /// el listón que abre produzca el mismo mercado que no tener listón es lo que dice que el gate abierto
    /// es de verdad el comportamiento de antes de este paquete.
    /// </summary>
    [Fact]
    public void TheCutIsAtTheLevelOfTheTableAndNotOneThousandthAbove()
    {
        int level = Data.Value.Standard.Economy.ItemValues.MeanValue;
        var contextual = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        int free = ItemsBought(contextual with { MinItemValueMarket = BelowEveryTable });
        int atLevel = ItemsBought(contextual with { MinItemValueMarket = level });
        int justAbove = ItemsBought(contextual with { MinItemValueMarket = level + 1 });

        Assert.True(free > 0, "con el gate abierto no se compró ningún objeto: la muestra no prueba nada");
        Assert.Equal(free, atLevel);
        Assert.Equal(0, justAbove);
    }

    /// <summary>
    /// Con el listón que la política usa de verdad —el <b>coste de oportunidad del slot</b> de la ADR
    /// 0072, no una constante— el mercado de objetos sigue existiendo pero <b>muerde</b>: la contextual
    /// compra bastantes menos objetos que con el gate abierto, y no cero. Los dos extremos son fallos
    /// distintos y silenciosos: en cero el paquete habría prohibido comprar objetos en vez de exigirles
    /// que valgan el slot; igual que abierto, el gate no estaría haciendo nada.
    /// </summary>
    [Fact]
    public void TheSlotBarThinsTheItemMarketWithoutClosingIt()
    {
        var contextual = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        int free = ItemsBought(contextual with { MinItemValueMarket = BelowEveryTable });
        int gated = ItemsBought(contextual);

        Assert.True(gated > 0, "con el listón del slot no se compró ni un objeto: el gate cerró el canal entero");
        Assert.True(gated < free, $"el listón del slot no descartó ningún objeto ({gated} de {free}): el gate no hace nada");
    }

    /// <summary>
    /// La tabla que <c>/data</c> trae hoy tiene <b>nivel positivo</b> (+37 milésimas, error típico 4), que
    /// es lo único que AT-A dejó bien determinado. Si dejara de serlo, el gate por nivel agregado se
    /// volvería una prohibición general de comprar objetos y habría que remedir antes de tocar nada.
    /// </summary>
    [Fact]
    public void TheMeasuredCatalogueOfItemsIsWorthSomething()
    {
        var table = Data.Value.Standard.Economy.ItemValues;
        Assert.True(table.Count > 0, "data/economy/item-values.json no está en la instantánea");
        Assert.True(table.MeanValue > 0, $"el nivel medido del catálogo de objetos es {table.MeanValue}");
    }

    /// <summary>Runs con la política dada; devuelve los objetos comprados <b>en el mercado</b> en total.</summary>
    private static int ItemsBought(RunPolicyOptions options, StandardRunSystems? systems = null)
    {
        var world = Data.Value;
        var run = systems ?? world.Standard;
        var key = (options, ReferenceEquals(run, world.Standard));
        lock (Memo)
        {
            if (Memo.TryGetValue(key, out int cached))
            {
                return cached;
            }
        }

        int bought = 0;
        for (int i = 0; i < Runs; i++)
        {
            var setup = run.NewRunSetup(ClubId, world.Races[i % world.Races.Count], world.Files)
                with { GeneratedQuality = StartingQuality };
            bought += RunPolicy.Play(setup, Seed + (ulong)i, world.Catalog, run, world.Bosses, options).ItemsBought;
        }

        lock (Memo)
        {
            Memo[key] = bought;
        }

        return bought;
    }

    /// <summary>Los mismos sistemas sin tabla de valor de objetos: la instantánea de antes de AT-A.</summary>
    private static StandardRunSystems WithoutItemValues()
    {
        var standard = Data.Value.Standard;
        return new StandardRunSystems(
            standard.Economy with { ItemValues = ItemValueTable.Empty },
            standard.Items,
            standard.Consumables,
            standard.Rivals,
            standard.Map,
            standard.Clubs);
    }

    private static World Load()
    {
        var files = TestData.LoadAllFiles();
        var catalog = DataLoader.FromJson(files);
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var races = catalog.Races.Where(r => r.Launch).Select(r => r.Id).OrderBy(r => r).ToList();
        return new World(catalog, files, standard, bosses, races);
    }
}
