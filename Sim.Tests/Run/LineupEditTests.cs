using Underleague.Sim.Model;
using Underleague.Sim.Placement;
using Underleague.Sim.Run;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BX-1 (<c>docs/pendientes/BX-playtest-3oct.md</c>): «Equipo no dejaba alinear y luego el partido salía con 7». La
/// causa era un desacuerdo de reglas entre la pantalla y <c>/Sim</c>: tras las bajas, <c>PruneLineup</c> deja la
/// alineación guardada con menos de 5, <c>SetLineup</c> exigía 5 como mínimo y lanzaba al mover cualquier ficha, y a
/// la vez <c>RunLineup.Build</c> completaba hasta 7 al jugar. Dos reglas para el mismo hecho.
/// </summary>
public sealed class LineupEditTests
{
    private static Underleague.Sim.Data.Catalog Catalog => SystemsTestSupport.Catalog;

    private static RunState Pruned(int keep)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 20260905UL, Catalog, SystemsTestSupport.Systems);
        return state.WithLineup(new Lineup(state.Lineup.Slots.Take(keep).ToList()));
    }

    [Fact]
    public void ALineupPrunedBelowFiveCanBeEditedAndThePlayedElevenIsStillSeven()
    {
        var state = Pruned(4);
        Assert.Equal(4, state.Lineup.Slots.Count);

        // El jugador mueve una ficha de las que quedan: antes lanzaba (4 < 5).
        var players = state.Roster.Select(p => p.ToDefinition(Catalog)).ToList();
        var first = state.Lineup.Slots[1];
        var target = new Cell(5, 5);
        var moved = PlacementView.WithPlayerAt(state.Lineup, players, first.PlayerId, target);
        Assert.NotSame(state.Lineup, moved);

        var next = RunEngine.Apply(state, new SetLineup(moved), Catalog, SystemsTestSupport.Systems);
        Assert.Equal(4, next.Lineup.Slots.Count);
        Assert.Equal(RunRules.MaxStarters, RunLineup.Build(next, Catalog).Starters.Count);
    }

    [Fact]
    public void AnEighthStarterIsRefusedByThePlacementRuleNotByAThrow()
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), 20260905UL, Catalog, SystemsTestSupport.Systems);
        Assert.Equal(RunRules.MaxStarters, state.Lineup.Slots.Count);
        var players = state.Roster.Select(p => p.ToDefinition(Catalog)).ToList();
        var bench = players.First(p => state.Lineup.Slots.All(s => s.PlayerId != p.Id) && p.Position != Position.Goalkeeper);
        var empty = Enumerable.Range(0, Pitch.Rows)
            .Select(r => new Cell(5, r))
            .First(c => state.Lineup.Slots.All(s => s.HomeCell != c) && PlacementView.CanPlace(bench.Position, c));

        Assert.Same(state.Lineup, PlacementView.WithPlayerAt(state.Lineup, players, bench.Id, empty));

        // Sustituir a un titular sí es posible: el once sigue en siete.
        var occupied = state.Lineup.Slots.First(s => s.PlayerId != state.Lineup.Slots[0].PlayerId
            && PlacementView.CanPlace(bench.Position, s.HomeCell));
        var swapped = PlacementView.WithPlayerAt(state.Lineup, players, bench.Id, occupied.HomeCell);
        Assert.Equal(RunRules.MaxStarters, swapped.Slots.Count);
        Assert.Contains(swapped.Slots, s => s.PlayerId == bench.Id);
        Assert.DoesNotContain(swapped.Slots, s => s.PlayerId == occupied.PlayerId);
    }

    /// <summary>
    /// BX-1, RF-012d: lo que Equipo enseña es lo que juega. La pantalla pinta <c>RunLineup.Effective</c> (la misma llamada que
    /// <c>Build</c>), no la guardada: con la guardada podada, el once mostrado ya trae el relleno colocado y marcado, y
    /// confirmarlo no cambia a nadie.
    /// </summary>
    [Fact]
    public void TheElevenEquipoShowsIsTheElevenThatPlays()
    {
        var state = Pruned(4);
        var shown = RunLineup.Effective(state);
        var played = RunLineup.Build(state, Catalog).Lineup;

        Assert.Equal(played.Slots, shown.Lineup.Slots);
        Assert.Equal(RunRules.MaxStarters, shown.Lineup.Slots.Count);
        Assert.Equal(3, shown.FilledIds.Count);

        // El jugador mueve una ficha del once mostrado: se guarda ese once entero y ya nadie entra de oficio.
        var moved = RunEngine.Apply(state, new SetLineup(shown.Lineup), Catalog, SystemsTestSupport.Systems);
        Assert.Equal(played.Slots, RunLineup.Build(moved, Catalog).Lineup.Slots);
        Assert.Empty(RunLineup.Effective(moved).FilledIds);
    }
}