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
public sealed class ConsumableLoopTests
{
    private static RunPlayResult Play(ulong seed, PurchaseDoctrine doctrine = PurchaseDoctrine.Contextual)
    {
        var catalog = TestData.LoadCatalog();
        var files = TestData.LoadAllFiles();
        var standard = StandardRunSystems.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var setup = standard.NewRunSetup("cat_b_club", Race.Orc, files) with { GeneratedQuality = 50 };
        return RunPolicy.Play(setup, seed, catalog, standard, bosses, RunPolicyOptions.For(doctrine));
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
            bought += Play(seed).ConsumablesBought;
        }

        Assert.True(bought > 0, "en doce runs no se ha comprado un solo consumible: el mercado no los ofrece o la política no los mira");
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
            activations += Play(seed).ConsumablesUsed;
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
