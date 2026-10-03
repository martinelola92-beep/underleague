using Underleague.Sim.Analysis;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Items;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// CAT-B: el consumible se compra, se equipa y llega al partido. Antes de este paquete la cadena estaba
/// entera en <c>/Sim</c> menos el llamador —<c>SetConsumables</c> no lo emitía <b>nadie</b>, ni la
/// política automática ni <c>/Game</c>—, así que los cuatro consumibles de RF-084 se compraban y no se
/// jugaban jamás, y cualquier cambio en <c>data/consumables/</c> era invisible para las 43 puertas (así
/// se coló CAT-A, el vendaje que protegía al rival).
/// </summary>
public sealed class ConsumableLoopTests : IClassFixture<ConsumableLoopTests.ContextualRuns>
{
    private readonly ContextualRuns _runs;

    public ConsumableLoopTests(ContextualRuns runs) => _runs = runs;

    /// <summary>
    /// Las runs contextuales sin observador de las semillas 1..12, ya jugadas (técnica «compartir lo que se repite»):
    /// <c>ThePolicyBuysConsumables</c> y <c>TheConsumablesBoughtActuallyReachAMatch</c> juegan las mismas doce y leen
    /// contadores distintos del resultado. La run es función pura de la semilla; el fixture se suelta al acabar.
    /// </summary>
    public sealed class ContextualRuns
    {
        private readonly Dictionary<ulong, RunPlayResult> _bySeed = new();

        public RunPlayResult Get(ulong seed)
        {
            lock (_bySeed)
            {
                if (!_bySeed.TryGetValue(seed, out var result))
                {
                    result = Play(seed);
                    _bySeed[seed] = result;
                }

                return result;
            }
        }
    }

    private static RunPlayResult Play(ulong seed, PurchaseDoctrine doctrine = PurchaseDoctrine.Contextual, MatchObserver? observer = null)
    {
        var catalog = TestData.LoadCatalog();
        var files = TestData.LoadAllFiles();
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var setup = standard.NewRunSetup("cat_b_club", Race.Orc, files) with { GeneratedQuality = 50 };
        return RunPolicy.Play(setup, seed, catalog, standard, bosses, RunPolicyOptions.For(doctrine), observer);
    }

    /// <summary>
    /// La política compra consumibles. Es lo que hace medible a la familia: sin compras, el sumidero de
    /// la cuarta categoría del mercado (RF-114) no se ejercita.
    /// </summary>
    [Fact]
    public void ThePolicyBuysConsumables()
    {
        int bought = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            bought += _runs.Get(seed).ConsumablesBought;
        }

        Assert.True(bought > 0, "en doce runs no se ha comprado un solo consumible: el mercado no los ofrece o la política no los mira");
    }

    /// <summary>
    /// ADR 0172: la política nunca lleva más consumibles que huecos hay, y en la medición todos van
    /// condicionales (un manual no lo pulsa nadie en <c>/Balance</c>): así el efecto que se mide es el de
    /// los dos huecos enteros y no el de uno perdido.
    /// </summary>
    [Fact]
    public void ThePolicyCarriesAtMostTheSlotsAndAllOfThemFireByThemselves()
    {
        int matches = 0;
        int carried = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            Play(seed, observer: (_, _, setup, _, _) =>
            {
                matches++;
                carried += setup.Home.Consumables.Count;
                Assert.True(setup.Home.Consumables.Count <= RunRules.ConsumableSlots);
                Assert.DoesNotContain(setup.Home.Consumables, c => c.Trigger == Underleague.Sim.Perks.ConsumableTrigger.Manual);
            });
        }

        Assert.True(matches > 0);
        Assert.True(carried > 0, "en seis runs la política no ha llevado un solo consumible a un partido");
    }

    /// <summary>
    /// Y los <b>gasta</b>: que se compren no basta, porque comprar sin equipar es justo el defecto que
    /// CAT-B describe. El contador de inventario baja cuando uno se activa
    /// (<c>MatchResolution.ConsumeConsumables</c>), así que activaciones &gt; 0 es la prueba de que el
    /// consumible llegó al campo.
    /// </summary>
    [Fact]
    public void TheConsumablesBoughtActuallyReachAMatch()
    {
        int activations = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            activations += _runs.Get(seed).ConsumablesUsed;
        }

        Assert.True(
            activations > 0,
            "en doce runs no se ha activado un solo consumible: se compran y no llegan al partido (CAT-B)");
    }

    /// <summary>
    /// El manual no se activa solo, y tiene que seguir sin hacerlo: en <c>/Balance</c> no hay quien lo
    /// pulse (<c>ManualTick</c> −1), así que el efecto medible sale entero de los condicionales. Si esto
    /// dejara de ser cierto, el manual se estaría disparando sin que nadie lo pulse y la medición estaría
    /// contando un efecto que en el juego exige una decisión (RF-082).
    /// </summary>
    [Fact]
    public void TheManualSlotNeverFiresOnItsOwn()
    {
        var equipment = RunEquipment.FromSnapshot(TestData.LoadAllFiles());

        var equipped = new[] { new EquippedConsumable("field_bandage", ConsumableMode.Manual, string.Empty) };
        var forMatch = equipment.ForMatch(equipped);

        var single = Assert.Single(forMatch);
        Assert.Equal(Underleague.Sim.Perks.ConsumableTrigger.Manual, single.Trigger);
        Assert.Equal(-1, single.ManualTick);
    }

    /// <summary>
    /// RT-013: equipar es una decisión del estado, así que la misma semilla sigue produciendo la misma
    /// run. Es lo que impide que este cambio se lea en el dado en vez de en la métrica.
    /// </summary>
    [Fact]
    public void EquippingKeepsTheRunDeterministic()
    {
        var first = Play(7);
        var second = Play(7);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Matches, second.Matches);
        Assert.Equal(first.ConsumablesBought, second.ConsumablesBought);
        Assert.Equal(first.ConsumablesUsed, second.ConsumablesUsed);
        Assert.Equal(first.GoldSpentMarket, second.GoldSpentMarket);
    }
}
