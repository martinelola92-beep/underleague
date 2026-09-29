using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Consumables;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0161 (revisión independiente), BA-H: un consumible no tiene portador, así que su efecto vale para el
/// equipo propio salvo que declare <c>target: opposingTeam</c>. <c>severeInjury</c> es un canal de quien
/// SUFRE la lesión (<c>Odds(victim, SevereInjury)</c>): <c>the_ambush</c> lo sube al rival, no a los propios.
/// Regla J: el instrumento se valida contra el caso que se conoce, por eso se cuenta a quién le cae la lesión
/// grave y no sólo cuántas hay.
/// </summary>
public sealed class ConsumableTargetTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static readonly ConsumableCatalog Consumables = ConsumableLoader.FromJson(TestData.LoadAllFiles());

    private static MatchConsumable OnlySevereInjuryChannel(string id, EffectTarget? forceTarget = null)
    {
        var definition = Consumables.Find(id) ?? throw new InvalidOperationException(id);
        var severe = definition.Effects.Where(e => e.Probability == ProbabilityKind.SevereInjury)
            .Select(e => forceTarget is { } target ? e with { Target = target } : e).ToList();
        Assert.NotEmpty(severe);
        return new MatchConsumable(definition.Id, definition.Rarity, severe, ConsumableTrigger.Manual) { ManualTick = 1 };
    }

    private static (int Own, int Rival) SevereInjuriesByVictimTeam(MatchConsumable? consumable, int matches)
    {
        int own = 0;
        int rival = 0;
        for (ulong seed = 1; seed <= (ulong)matches; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            if (consumable is not null)
            {
                setup = setup with { Home = setup.Home with { Consumables = new[] { consumable } } };
            }

            foreach (var e in Simulator.Run(setup, seed, Catalog, SimConfig.Default).Events)
            {
                if (e.Type == EventType.Injury && e.Detail.StartsWith("severe", StringComparison.Ordinal))
                {
                    if (e.Team == 0)
                    {
                        own++;
                    }
                    else
                    {
                        rival++;
                    }
                }
            }
        }

        return (own, rival);
    }

    [Fact]
    public void TheAmbushLoadsWithTheSevereInjuryEffectAimedAtTheOpposingTeam()
    {
        var ambush = Consumables.Find("the_ambush")!;
        var severe = Assert.Single(ambush.Effects, e => e.Probability == ProbabilityKind.SevereInjury);
        Assert.Equal(EffectTarget.OpposingTeam, severe.Target);
        Assert.All(
            ambush.Effects.Where(e => e.Probability != ProbabilityKind.SevereInjury),
            e => Assert.Equal(EffectTarget.Team, e.Target));
    }

    [Fact]
    public void TheAmbushRaisesTheRivalsSevereInjuriesAndNotTheOwn()
    {
        const int matches = 600;
        var baseline = SevereInjuriesByVictimTeam(null, matches);
        var wrong = SevereInjuriesByVictimTeam(OnlySevereInjuryChannel("the_ambush", EffectTarget.Team), matches);
        var ambush = SevereInjuriesByVictimTeam(OnlySevereInjuryChannel("the_ambush"), matches);

        // El rival sufre claramente más lesiones graves...
        Assert.True(ambush.Rival > baseline.Rival * 5 / 4, $"rival: base {baseline.Rival}, emboscada {ambush.Rival}, mal apuntada {wrong.Rival}; propias: {baseline.Own}/{ambush.Own}/{wrong.Own}");

        // ...y las propias no suben. Regla J: el instrumento se valida contra el caso conocido —el efecto mal
        // apuntado, que es lo que hacía the_ambush antes del arreglo, sí sube las propias (medido: 43 base,
        // 43 bien apuntada, 67 mal apuntada, en 600 partidos)—.
        Assert.True(wrong.Own > baseline.Own * 5 / 4, $"el instrumento no detecta el caso conocido: base {baseline.Own}, mal apuntada {wrong.Own}");
        Assert.True(ambush.Own <= baseline.Own + 5, $"propias: base {baseline.Own}, emboscada {ambush.Own}");
    }

    [Fact]
    public void AConsumableWithoutTargetStillReachesTheOwnTeam()
    {
        const int matches = 150;
        var baseline = SevereInjuriesByVictimTeam(null, matches);
        var own = SevereInjuriesByVictimTeam(OnlySevereInjuryChannel("phoenix_blood"), matches);

        // phoenix_blood divide la lesión grave: sin target va al equipo propio y las baja.
        Assert.True(own.Own < baseline.Own, $"propias: base {baseline.Own}, phoenix {own.Own}");
    }
}
