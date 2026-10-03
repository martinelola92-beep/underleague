using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// ADR 0188 (BV-D): lesionar en un contacto limpio cuesta más; la falta —señalada o no— y el efecto <c>injure</c>
/// de un perk (<c>MatchEngine.ProvokeInjury</c>, que tira siempre como falta) lesionan exactamente lo mismo que
/// antes. Se hace moviendo base de <c>onTackleBase</c> a <c>onFoulBase</c> sin cambiar su suma, así que la cuota
/// de una falta es la de antes bit a bit y la de un contacto limpio baja lo que se movió.
/// </summary>
public sealed class InjuryChanceTests
{
    // Las bases de antes de la ADR 0188 (tuning.injury desde la ADR 0050 P2), el control de estos tests.
    // Con el motor VIGENTE (sostenida 50 y las reglas de BV-B encendidas), las huellas de RT-024 de MobNarrowingTests
    // (60 partidos de referencia) NO cambian con la ADR 0188: en esos 60 partidos la cuota limpia está entre 48 y 116 y
    // ningún dado de un contacto limpio cae en la franja de 50 puntos que se movió (21 lesiones con las dos bases;
    // esperable ≈ 2 casos, P(0) ≈ 0,1; comprobado, Regla J). Con el motor de ANTES de la ADR 0184 (sostenida 0, BV-B
    // apagada) las trayectorias son otras y sí cruzan la franja: por eso MobNarrowingTests.Before lleva estas bases.
    // Ninguna huella es un control discriminante de esta ADR; el control es éste, par a par sobre la cuota.
    internal const int OnTackleBaseBeforeAdr0188 = 140;
    internal const int OnFoulBaseBeforeAdr0188 = 60;

    private static readonly Catalog Current = TestData.LoadCatalog();

    internal static Catalog WithOldInjuryBases(Catalog catalog) => catalog with
    {
        Tuning = catalog.Tuning with
        {
            Injury = catalog.Tuning.Injury with
            {
                OnTackleBase = OnTackleBaseBeforeAdr0188,
                OnFoulBase = OnFoulBaseBeforeAdr0188,
            },
        },
    };

    [Fact]
    public void TheFoulKeepsItsOldBaseAndOnlyTheCleanContactPaysTheMove()
    {
        var injury = Current.Tuning.Injury;
        Assert.Equal(OnTackleBaseBeforeAdr0188 + OnFoulBaseBeforeAdr0188, injury.OnTackleBase + injury.OnFoulBase);
        Assert.True(injury.OnTackleBase < OnTackleBaseBeforeAdr0188, "la ADR 0188 baja la base del contacto limpio");
        Assert.True(injury.OnTackleBase >= 0);
    }

    /// <summary>
    /// Valor conocido con control, par a par sobre los doce jugadores de campo del partido de referencia: la cuota de
    /// una falta es la misma con las bases de antes y con las de ahora; la de un contacto limpio baja exactamente lo
    /// que se movió a la falta (salvo donde el suelo de 0 ya la cortaba, p. ej. contra un <c>Resilient</c>).
    /// </summary>
    [Fact]
    public void AFoulInjuresAsBeforeAndACleanContactLessByTheMovedBase()
    {
        var old = WithOldInjuryBases(Current);
        var now = new MatchEngine(TestMatches.Reference(Current, 5), 5, Current, SimConfig.Default);
        var before = new MatchEngine(TestMatches.Reference(old, 5), 5, old, SimConfig.Default);
        int moved = OnTackleBaseBeforeAdr0188 - Current.Tuning.Injury.OnTackleBase;

        int pairs = 0;
        int exact = 0;
        foreach (var (tackler, victim) in Pairs(now))
        {
            pairs++;
            Assert.Equal(before.InjuryChanceForTest(tackler, victim, isFoul: true), now.InjuryChanceForTest(tackler, victim, isFoul: true));

            int cleanBefore = before.InjuryChanceForTest(tackler, victim, isFoul: false);
            int cleanNow = now.InjuryChanceForTest(tackler, victim, isFoul: false);
            Assert.True(cleanNow <= cleanBefore);
            Assert.True(cleanNow <= now.InjuryChanceForTest(tackler, victim, isFoul: true));
            if (cleanNow > 0 && cleanBefore < 5000)
            {
                Assert.Equal(moved, cleanBefore - cleanNow);
                exact++;
            }
            else
            {
                Assert.Equal(0, cleanNow);
            }
        }

        Assert.Equal(72, pairs);
        Assert.True(exact >= pairs / 2, $"sólo {exact} de {pairs} pares sin recortar: el valor conocido no se está midiendo");
    }

    /// <summary>Control: con las bases de antes, la cuota limpia y la de falta difieren en <c>onFoulBase</c> de antes, como en main.</summary>
    [Fact]
    public void WithTheOldBasesAFoulAddsTheOldFoulBase()
    {
        var old = WithOldInjuryBases(Current);
        var before = new MatchEngine(TestMatches.Reference(old, 5), 5, old, SimConfig.Default);
        int checkedPairs = 0;
        foreach (var (tackler, victim) in Pairs(before))
        {
            int clean = before.InjuryChanceForTest(tackler, victim, isFoul: false);
            int foul = before.InjuryChanceForTest(tackler, victim, isFoul: true);
            if (clean > 0 && foul < 5000)
            {
                Assert.Equal(OnFoulBaseBeforeAdr0188, foul - clean);
                checkedPairs++;
            }
        }

        Assert.True(checkedPairs > 0);
    }

    /// <summary>
    /// ADR 0188, revisión independiente: en el bloqueo, que el árbitro pite o no la falta no cambia que la lesión se tire
    /// como falta (<c>isFoul</c> sale del dado de falta, antes del pitido). Bloqueo siempre falta (<c>block.foulBase</c>
    /// 10.000) con el árbitro neutro pitando el 100 % y el 0 %: las dos tiradas de lesión usan la cuota de falta, y el
    /// control es que de verdad una se pitó y la otra no.
    /// </summary>
    [Theory]
    [InlineData(100, "whistled")]
    [InlineData(0, "unseen")]
    public void ABlockFoulInjuresAsAFoulWhetherOrNotItIsWhistled(int whistlePercent, string expectedFoul)
    {
        var referee = Current.Tuning.Referee;
        var catalog = Current with
        {
            Tuning = Current.Tuning with
            {
                Block = Current.Tuning.Block with { FoulBase = 10000 },
                Referee = referee with
                {
                    Traits = referee.Traits with { Neutral = referee.Traits.Neutral with { WhistlePercent = whistlePercent } },
                },
            },
        };
        var engine = new MatchEngine(TestMatches.Reference(catalog, 5), 5, catalog, SimConfig.Default);
        int blocker = engine.OutfieldIndexForTest(0, 0);
        int target = engine.OutfieldIndexForTest(1, 0);
        for (int i = 0; i < 14; i++)
        {
            if (i != blocker && i != target)
            {
                engine.PlaceForTest(i, new Vec2(engine.PlayerAtForTest(i).Team == 0 ? 0.5f : 15.5f, 0.5f));
            }
        }

        engine.PlaceForTest(blocker, new Vec2(8f, 3.5f));
        engine.PlaceForTest(target, new Vec2(8.4f, 3.5f));
        engine.ParkBallForTest(new Vec2(1f, 1f));
        int foulChance = engine.InjuryChanceForTest(blocker, target, isFoul: true);
        Assert.NotEqual(foulChance, engine.InjuryChanceForTest(blocker, target, isFoul: false));

        engine.ResolveBlockForTest(blocker, target);

        Assert.Contains(engine.EventsForTest, e => e.Type == EventType.Tackle && e.Detail == "blockFoul");
        bool unseen = engine.EventsForTest.Any(e => e.Type == EventType.Foul && e.Detail == "unseen");
        Assert.Equal(expectedFoul == "unseen", unseen);
        Assert.Equal(foulChance, engine.LastInjuryChanceForTest);
    }

    private static IEnumerable<(int Tackler, int Victim)> Pairs(MatchEngine engine)
    {
        for (int a = 0; a < 6; a++)
        {
            for (int b = 0; b < 6; b++)
            {
                yield return (engine.OutfieldIndexForTest(0, a), engine.OutfieldIndexForTest(1, b));
                yield return (engine.OutfieldIndexForTest(1, b), engine.OutfieldIndexForTest(0, a));
            }
        }
    }
}
