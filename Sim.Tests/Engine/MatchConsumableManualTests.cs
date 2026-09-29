using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// BA-H, RF-082: el consumible manual se activa en el tick que el jugador pulsó, guardado como parte del
/// estado inicial (<c>MatchConsumable.ManualTick</c>, <c>docs/arquitectura.md</c>). Mismo contrato de
/// determinismo que la orden táctica (ADR 0154, ver <see cref="MatchOrderTests"/>): re-simular con la
/// activación dentro no cambia nada anterior al tick en el que se pulsó.
/// </summary>
public sealed class MatchConsumableManualTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>Vendaje de campaña equipado en el slot manual, sin pulsar (ManualTick por defecto, -1: nunca se dispara).</summary>
    private static MatchConsumable FieldBandage(int manualTick) => new(
        "field_bandage",
        Rarity.Common,
        new[]
        {
            new EffectDefinition(
                EffectType.ModifyProbability,
                Probability: ProbabilityKind.Injury,
                Value: ProbabilityScale.ToMultiplier(-100),
                Duration: EffectDuration.Match),
        },
        ConsumableTrigger.Manual)
    { ManualTick = manualTick };

    /// <summary>
    /// Activar el consumible manual en el tick T no altera nada anterior a T: es lo que permite a la
    /// pantalla reanudar el partido desde donde el jugador pulsó el botón, como con la orden táctica y las
    /// sustituciones (ADR 0094, ADR 0154).
    /// </summary>
    [Fact]
    public void ActivatingTheManualConsumableAtTickTLeavesEverythingBeforeTUntouched()
    {
        const int T = 600;
        int diverged = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var untouched = Simulator.Run(
                setup with { Home = setup.Home with { Consumables = new[] { FieldBandage(-1) } } },
                seed, Catalog, SimConfig.Default);
            var activated = Simulator.Run(
                setup with { Home = setup.Home with { Consumables = new[] { FieldBandage(T) } } },
                seed, Catalog, SimConfig.Default);

            var before = untouched.Events.Where(e => e.Tick < T).Select(Describe).ToList();
            var beforeActivated = activated.Events.Where(e => e.Tick < T).Select(Describe).ToList();
            Assert.Equal(before, beforeActivated);

            // El propio CONSUMABLE_USED tiene que aparecer exactamente en T, y nunca antes de pulsarlo.
            var used = activated.Events.Where(e => e.Type == EventType.ConsumableUsed).ToList();
            Assert.Single(used);
            Assert.Equal(T, used[0].Tick);
            Assert.DoesNotContain(untouched.Events, e => e.Type == EventType.ConsumableUsed);

            if (!untouched.Events.Select(Describe).SequenceEqual(activated.Events.Select(Describe)))
            {
                diverged++;
            }
        }

        // Y el efecto llega al campo: con la protección contra lesión activa desde el minuto ~40, algún
        // partido de los 20 tiene que cambiar (menos lesiones propias cambia entradas, sustituciones...).
        Assert.True(diverged >= 1, "ningún partido cambió tras activar el consumible manual");
    }

    private static string Describe(MatchEvent e) => $"{e.Tick}:{e.Type}:{e.Detail}:{e.Actor}";
}
