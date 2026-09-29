using Underleague.Sim.Analysis;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// ADR 0168 (Regla J): el instrumento de la sangre contrastado con casos de respuesta sabida antes de creerse la
/// métrica.
/// </summary>
public sealed class BloodCasualtyCounterTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    private static MatchEvent Event(EventType type, int actor, string detail) =>
        new(type, 10, 0, actor, -1, -1, new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, detail);

    private static int Count(params MatchEvent[] events)
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var result = new MatchResult(events, new MatchReportBuilder().Build(), Array.Empty<PlayerCounterDelta>());
        return BloodCasualtyCounter.Count(setup, result);
    }

    [Fact]
    public void ItCountsDistinctOwnPlayersWithASevereInjuryOrADeathAndNothingElse()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        int own1 = setup.Home.Players[1].Id;
        int own2 = setup.Home.Players[2].Id;
        int rival = setup.Away.Players[1].Id;

        Assert.Equal(0, Count());
        Assert.Equal(0, Count(Event(EventType.Injury, own1, "minor")));
        Assert.Equal(1, Count(Event(EventType.Injury, own1, "severe")));
        Assert.Equal(1, Count(Event(EventType.Injury, own1, "severe"), Event(EventType.Death, own1, "severeInjury")));
        Assert.Equal(2, Count(Event(EventType.Injury, own1, "severe"), Event(EventType.Death, own2, "perk:x")));
        Assert.Equal(0, Count(Event(EventType.Death, own1, "perk:x:cancelled"), Event(EventType.Injury, own2, "severe:cancelled")));
        Assert.Equal(0, Count(Event(EventType.Injury, rival, "severe"), Event(EventType.Death, rival, "perk:x")));
    }
}
