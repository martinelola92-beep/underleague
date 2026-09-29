using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Tests.Perks;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0171, la tirada del destino: el motor anuncia con <c>FATE_ROLL</c> la probabilidad real del dado que
/// decide una lesión grave (¿leve o grave?, sólo si hubo lesión) o una muerte, salvadas incluidas, sin cambiar
/// un solo dado (RT-021/RT-024) y sin volverse una pausa en cada partido (RF-012d).
/// </summary>
public sealed class FateRollTests
{
    private const int Seeds = 200;
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    /// <summary>
    /// Desgaste forzado (ADR 0043) para tener sucesos que mirar: en un partido suelto casi no hay lesiones.
    /// El ×6 sobre el partido de referencia produce varias por partido; el acto 3 real es ×4,2.
    /// </summary>
    private static MatchResult RunWorn(ulong seed, int scale = 600) =>
        Simulator.Run(TestMatches.Reference(Catalog, seed), seed, Catalog, SimConfig.Default with { InjuryScalePercent = scale });

    private static IEnumerable<MatchEvent> SameTick(IReadOnlyList<MatchEvent> events, MatchEvent roll) =>
        events.Where(e => e.Tick == roll.Tick && e.Actor == roll.Actor);

    [Fact]
    public void AHitIsAnInjuryOrDeathThatReallyHappenedAndASavedSevereOneIsAMinorOrCancelledInjury()
    {
        int hits = 0;
        int saved = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var events = RunWorn(seed).Events;
            foreach (var roll in events.Where(e => e.Type == EventType.FateRoll))
            {
                var same = SameTick(events, roll).ToList();
                if (roll.Detail.EndsWith(":hit", StringComparison.Ordinal))
                {
                    hits++;

                    // Un golpe real: el INJURY grave o el DEATH del mismo jugador y tick, NO anulado.
                    Assert.Contains(same, e =>
                        (e.Type == EventType.Death || (e.Type == EventType.Injury && e.Detail == "severe"))
                        && !e.Detail.EndsWith(":cancelled", StringComparison.Ordinal));
                }
                else if (roll.Detail.StartsWith("severe", StringComparison.Ordinal))
                {
                    saved++;

                    // Salvarse de la grave es salir con una lesión leve o con la lesión anulada.
                    Assert.Contains(same, e => e.Type == EventType.Injury && e.Detail is "minor" or "severe:cancelled" or "minor:cancelled");
                }
            }
        }

        Assert.True(hits > 0 && saved > 0, $"golpes {hits}, salvadas {saved}: los dos casos tienen que darse");
    }

    [Fact]
    public void TheAnnouncedPercentageIsHonest()
    {
        // La fracción de tiradas de grave anunciadas que caen ≈ la media de los porcentajes anunciados. Es la
        // prueba de que el número que enseña la pantalla es el del dado que se tiró (revisión independiente: la
        // primera versión anunciaba el 20 % cuando al jugador le caía el 37 %).
        int total = 0;
        int hits = 0;
        long announced = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            foreach (var roll in RunWorn(seed).Events.Where(e => e.Type == EventType.FateRoll && e.Detail.StartsWith("severe", StringComparison.Ordinal)))
            {
                var parts = roll.Detail.Split(':');
                Assert.Equal(3, parts.Length);
                total++;
                announced += int.Parse(parts[1]);
                if (parts[2] == "hit")
                {
                    hits++;
                }
            }
        }

        Assert.True(total >= 200, $"sólo {total} tiradas de grave anunciadas");
        double meanAnnounced = announced / (double)total / 10000d;
        double observed = hits / (double)total;
        Assert.InRange(observed, meanAnnounced - 0.07, meanAnnounced + 0.07);
    }

    [Fact]
    public void AnnouncedRollsAreWellFormedAndAboveTheThreshold()
    {
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            foreach (var roll in RunWorn(seed).Events.Where(e => e.Type == EventType.FateRoll))
            {
                var parts = roll.Detail.Split(':');
                Assert.Equal(3, parts.Length);
                Assert.True(int.Parse(parts[1]) >= (parts[0] == "death"
                    ? MatchEngine.FateRollMinDeathBasisPoints
                    : MatchEngine.FateRollMinSevereBasisPoints));
                Assert.Contains(parts[0], new[] { "severe", "death" });
                Assert.Contains(parts[2], new[] { "hit", "saved" });
            }
        }
    }

    [Fact]
    public void OwnFateMomentsAreRarePerMatchInALateAct()
    {
        // Acto 3 (×4,2), sólo los jugadores propios (los que la retransmisión presenta): tope ~1 por partido
        // (encargo, ADR 0171). La medida que manda es `fateMoments` en runs.csv (0,67 ± 0,03 medido).
        int total = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { InjuryScalePercent = 420 });
            total += Underleague.Sim.Analysis.FateMomentCounter.Count(setup, result);
        }

        Assert.True(total > 0, "el instrumento no ve ninguna tirada: la prueba estaría vacía");
        Assert.True(total <= Seeds * 1.0, $"{total} tiradas propias en {Seeds} partidos de acto 3: hay que subir el umbral");
    }

    [Fact]
    public void ALethalRollIsAnnouncedOnceAndItsOutcomeIsDecidedAfterTheCancellation()
    {
        const string NoDeath = """[{ "type": "cancelEvent" }]""";
        var catalog = TestPerks.CatalogWith(
            ("no_death", TestPerks.Json("no_death", "DEATH", NoDeath, scope: "actor", rarity: "legendary", kind: "ruleBreaker")));
        var setup = TestPerks.Match(catalog, 1, (1, new[] { "no_death" }));
        var engine = TestPerks.Engine(catalog, setup);
        var protectedPlayer = engine.PlayerById(1)!;
        var exposed = engine.PlayerById(2)!;
        var killer = engine.PlayerById(101)!;

        // El protegido: la muerte se anula, así que la tirada se anuncia como SALVADA.
        engine.Kill(protectedPlayer, "perk:test_lethal", killer);
        engine.AnnounceLethalRoll(protectedPlayer, killer, 2500);
        // El expuesto: la muerte ocurre, así que se anuncia como GOLPE; y una segunda tirada no se repite.
        engine.Kill(exposed, "perk:test_lethal", killer);
        engine.AnnounceLethalRoll(exposed, killer, 2500);
        engine.AnnounceLethalRoll(exposed, killer, 2500);
        // Por debajo del umbral, nada.
        engine.AnnounceLethalRoll(engine.PlayerById(3)!, killer, MatchEngine.FateRollMinDeathBasisPoints - 1);

        var rolls = engine.EventsForTest.Where(e => e.Type == EventType.FateRoll).ToList();
        Assert.Equal(2, rolls.Count);
        Assert.Equal((1, "death:2500:saved"), (rolls[0].Actor, rolls[0].Detail));
        Assert.Equal((2, "death:2500:hit"), (rolls[1].Actor, rolls[1].Detail));
    }

    [Fact]
    public void AnInjuryCancelledByAPerkIsAlwaysASavedRoll()
    {
        const string NoInjury = """[{ "type": "cancelEvent" }]""";
        var catalog = TestPerks.CatalogWith(
            ("iron_gate_test", TestPerks.Json("iron_gate_test", "INJURY", NoInjury, scope: "team", rarity: "legendary", kind: "ruleBreaker")));

        int saved = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            // El perk protege a todo el equipo local (scope team): ninguna lesión suya llega a ocurrir.
            var setup = TestPerks.Match(catalog, seed, (1, new[] { "iron_gate_test" }));
            var result = Simulator.Run(setup, seed, catalog, new SimConfig(CollectLog: false, InjuryScalePercent: 600));
            var homeIds = setup.Home.Players.Select(p => p.Id).ToHashSet();
            foreach (var roll in result.Events.Where(e => e.Type == EventType.FateRoll && homeIds.Contains(e.Actor)))
            {
                Assert.EndsWith(":saved", roll.Detail);
                saved++;
            }
        }

        Assert.True(saved > 0, "ninguna tirada anulada llegó a anunciarse: la prueba estaría vacía");
    }

    [Fact]
    public void TheCounterSeesOnlyOwnRollsAndSomeExist()
    {
        // Regla J: el instrumento se contrasta contra un caso de respuesta sabida y no nula. En partidos con
        // desgaste hay tiradas de los dos equipos; el contador debe dar exactamente las del local, que son
        // algunas pero no todas.
        int counted = 0;
        int expectedHome = 0;
        int all = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var setup = TestMatches.Reference(Catalog, seed);
            var result = Simulator.Run(setup, seed, Catalog, SimConfig.Default with { InjuryScalePercent = 600 });
            var homeIds = setup.Home.Players.Select(p => p.Id).ToHashSet();
            counted += Underleague.Sim.Analysis.FateMomentCounter.Count(setup, result);
            expectedHome += result.Events.Count(e => e.Type == EventType.FateRoll && homeIds.Contains(e.Actor));
            all += result.Events.Count(e => e.Type == EventType.FateRoll);
        }

        Assert.True(counted > 0);
        Assert.Equal(expectedHome, counted);
        Assert.True(counted < all, "el contador también está contando las tiradas del rival");
    }
}
