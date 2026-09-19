using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Perks;

/// <summary>
/// BB-Q Alt 0: <c>extraAction</c> admite <see cref="EventType.Recovery"/> y lo traduce a
/// <c>RepeatTackle</c>. Es la forma de reaccionar al RESULTADO de una entrada sin romper el modelo
/// pre-resolución: <c>Emit(Recovery, "tackle", tackler)</c> (<c>MatchEngine.cs:2223</c>) se publica justo
/// después del derribo de <c>:2221</c>, así que una entrada ganada ES un rival en el suelo.
///
/// <para>No hay <see cref="EventType"/> nuevo, ni primitiva de condición nueva, ni cambio en el
/// comportamiento de TACKLE.</para>
/// </summary>
public sealed class RecoveryExtraActionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
    private const int Matches = 20;

    private readonly ITestOutputHelper _output;
    public RecoveryExtraActionTests(ITestOutputHelper output) => _output = output;

    private static MatchResult Play(int index, string? perkId, out int carrierId, int slot = 1)
    {
        var homeRng = RngStreams.Generation(1, index);
        var awayRng = RngStreams.Generation(1, 10_000 + index);
        var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", Race.Human, 50, 1, 4);
        var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", Race.Human, 50, 100001, 4);

        var players = home.Players.ToList();
        if (perkId is not null)
        {
            players[slot] = players[slot] with { Perks = new[] { perkId } };
        }

        carrierId = players[slot].Id;
        var setup = new MatchSetup(home with { Players = players }, away, Referee);
        return Simulator.Run(setup, RngStreams.MatchSeed(1, index), Catalog, new SimConfig(CollectLog: false));
    }

    private static int Activations(MatchResult result, string perkId, int ownerId) =>
        result.Report.PerksSummary
            .Where(s => s.PerkId == perkId && s.OwnerId == ownerId)
            .Sum(s => s.Activations);

    /// <summary>El cargador admite la combinación, y el efecto llega a ejecutarse: el perk se activa.</summary>
    [Fact]
    public void RecoveryWithDetailTackleCanDriveExtraAction()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "steamroller");
        Assert.Equal(EventType.Recovery, perk.Trigger);
        Assert.Equal("detail() == 'tackle'", perk.Condition);
        Assert.Equal(EffectType.ExtraAction, perk.Effects.Single().Type);

        int total = 0;
        for (int i = 0; i < Matches; i++)
        {
            total += Activations(Play(i, "steamroller", out int carrierId), "steamroller", carrierId);
        }

        _output.WriteLine($"activaciones de steamroller en {Matches} partidos: {total}");
        Assert.True(total > 0, "RECOVERY(detail='tackle') debe poder activar extraAction");
    }

    /// <summary>
    /// Una RECOVERY que no sea de entrada NO lo dispara. Se comprueba contra el flujo real: las
    /// activaciones nunca superan el número de eventos <c>RECOVERY "tackle"</c> cuyo actor es el portador,
    /// aunque en los mismos partidos haya muchas más recuperaciones de otros tipos.
    /// </summary>
    [Fact]
    public void RecoveriesThatAreNotTacklesDoNotFireIt()
    {
        int activations = 0, tackleRecoveries = 0, otherRecoveries = 0;
        for (int i = 0; i < Matches; i++)
        {
            var result = Play(i, "steamroller", out int carrierId);
            activations += Activations(result, "steamroller", carrierId);
            foreach (var e in result.Events.Where(e => e.Type == EventType.Recovery))
            {
                if (e.Actor != carrierId)
                {
                    continue;
                }

                if (e.Detail == "tackle") { tackleRecoveries++; } else { otherRecoveries++; }
            }
        }

        _output.WriteLine($"RECOVERY del portador: 'tackle' {tackleRecoveries} | de otro tipo {otherRecoveries}");
        _output.WriteLine($"activaciones de steamroller: {activations}");

        Assert.True(otherRecoveries > 0, "debe haber recuperaciones de otro tipo, si no el test no discrimina");
        Assert.True(
            activations <= tackleRecoveries,
            $"steamroller se activó {activations} veces con solo {tackleRecoveries} recuperaciones por entrada");
    }

    /// <summary>
    /// TACKLE no se evalúa dos veces. Alt 1 (publicar además el evento TACKLE resuelto) se descartó
    /// precisamente porque habría duplicado la evaluación de los 12 perks con trigger TACKLE y los 7 con
    /// SHOT; este test fija que Alt 0 no la ha introducido por otra vía.
    ///
    /// <para>La comprobación es diferencial: un partido SIN ningún perk produce exactamente los mismos
    /// eventos TACKLE que antes del cambio. Los valores son los medidos sobre el árbol limpio con
    /// <c>git stash</c> (disciplina de la skill <c>balance-measure</c>), semilla a semilla. Si la
    /// evaluación se hubiera duplicado, estas cuentas subirían.</para>
    /// </summary>
    [Theory]
    [InlineData(0, 31)]
    [InlineData(1, 21)]
    [InlineData(2, 26)]
    [InlineData(3, 10)]
    [InlineData(4, 7)]
    public void TackleStreamIsUnchangedForAMatchWithNoPerks(int index, int expectedTackleEvents)
    {
        var result = Play(index, perkId: null, out _);
        int events = result.Events.Count(e => e.Type == EventType.Tackle);
        _output.WriteLine($"semilla {index}: {events} eventos TACKLE (baseline sin el cambio: {expectedTackleEvents})");
        Assert.Equal(expectedTackleEvents, events);
    }

    /// <summary>La recursión sigue acotada por <c>_maxDepth</c> (RT-042): el partido termina y el corte es finito.</summary>
    [Fact]
    public void RecursionIsStillBoundedByMaxDepth()
    {
        for (int i = 0; i < Matches; i++)
        {
            var result = Play(i, "steamroller", out _);
            Assert.True(result.Report.Ticks > 0, "el partido debe terminar");
            Assert.True(result.Report.RecursionCuts >= 0);
            Assert.True(result.Report.Tackles < 10_000, "una cadena sin cortar dispararía las entradas");
        }
    }

    /// <summary>
    /// BC-B: un perk cuyo efecto vuelve a publicar su propio disparador (extraAction) no pasa de su límite.
    /// Con el uso consumido después de aplicar los efectos, la llamada anidada no lo veía y el perk se
    /// encadenaba hasta la profundidad máxima: <c>double_shot</c> daba siempre 5 activaciones con límite 1.
    /// </summary>
    /// <para>La fila de <c>steamroller</c> es solo de guarda: con estas plantillas no llegaba a encadenarse
    /// ni con el fallo. Las otras dos fallan con el código anterior (5 activaciones y 6 acciones en un tick).</para>
    [Theory]
    [InlineData("double_shot", 6, EventType.Shot, true)]
    [InlineData("charge", 1, EventType.Tackle, true)]
    [InlineData("steamroller", 1, EventType.Tackle, false)]
    public void APerkThatRetriggersItselfStillRespectsItsLimit(string perkId, int slot, EventType repeated, bool mustActivate)
    {
        var limit = Catalog.Perks.All.Single(p => p.Id == perkId).Limit;
        Assert.NotNull(limit);
        int active = 0;
        for (int i = 0; i < Matches; i++)
        {
            var result = Play(i, perkId, out int carrierId, slot);
            int n = Activations(result, perkId, carrierId);
            Assert.InRange(n, 0, limit!.Times);
            active += n > 0 ? 1 : 0;

            // Lo observable: la acción repetida del portador ocurre como mucho dos veces en un mismo tick
            // (la suya y UNA extra), no hasta la profundidad de recursión.
            int most = result.Events.Where(e => e.Type == repeated && e.Actor == carrierId)
                .GroupBy(e => e.Tick).Select(g => g.Count()).DefaultIfEmpty(0).Max();
            Assert.InRange(most, 0, 2);
        }

        _output.WriteLine($"{perkId}: se activa en {active} de {Matches} partidos, nunca más de {limit!.Times} vez por partido");
        if (mustActivate)
        {
            Assert.True(active > 0, $"{perkId} no se activa en ningún partido: la prueba no demostraría nada");
        }
    }

    /// <summary>
    /// <c>charge</c> y los tres perks que ya usaban RECOVERY no se mueven. Valores fijados contra el
    /// árbol SIN el cambio de BB-Q (medidos con `git stash`, disciplina de la skill `balance-measure`): mismas
    /// semillas, mismas plantillas. <c>charge</c> se volvió a fijar con BC-B (65 → 13): los 65 se midieron con el fallo
    /// del límite (el perk se encadenaba dentro de su propia activación), imposibles con un límite de 1 por
    /// partido en 20 partidos.
    /// </summary>
    [Theory]
    [InlineData("charge", 1, 13)]
    [InlineData("lane_reader", 1, 19)]
    [InlineData("road_warrior", 1, 0)]
    [InlineData("sweeper_keeper", 0, 23)]
    public void ExistingPerksAreUnchanged(string perkId, int slot, int expected)
    {
        int total = 0;
        for (int i = 0; i < Matches; i++)
        {
            total += Activations(Play(i, perkId, out int carrierId, slot), perkId, carrierId);
        }

        _output.WriteLine($"{perkId}: {total} activaciones en {Matches} partidos (valor fijado: {expected})");
        Assert.Equal(expected, total);
    }
}
