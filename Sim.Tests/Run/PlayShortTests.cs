using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// BC-H, RF-002d (<c>docs/pendientes/BC-H.md</c>, ADR 0134 «lo que no hace»): <b>jugar con menos de siete por decisión
/// propia antes del partido</b>. Hasta aquí, mientras hubiera banquillo, el once se completaba solo y nadie podía decir
/// «esta casilla la dejo vacía» para no exponer a otro cuerpo. La decisión viaja en <c>SetLineup(PlayShort)</c> y vive en
/// un contador de la run (W-11) que <c>MatchResolution</c> borra al terminar el partido, como la marca de RF-093.
/// </summary>
public sealed class PlayShortTests
{
    private static Underleague.Sim.Data.Catalog Catalog => SystemsTestSupport.Catalog;

    /// <summary>
    /// Una plantilla de nueve con un titular de baja (lesión grave, fuera de la alineación guardada, como lo deja un
    /// partido): ocho disponibles, seis guardados, y el banquillo tiene con qué tapar el hueco.
    /// </summary>
    private static (RunState State, RunPlayer Absent) WithOneStarterOut(ulong seed = 20260929UL)
    {
        var state = RunEngine.Start(SystemsTestSupport.Setup(), seed, Catalog, SystemsTestSupport.Systems);
        var absent = state.Roster.First(p => p.Position == Position.Midfielder && state.Lineup.Slots.Any(s => s.PlayerId == p.Id));
        var remaining = state.Lineup.Slots.Where(s => s.PlayerId != absent.Id).ToList();
        var next = state
            .WithPlayer(absent with { PhysicalState = PhysicalState.SevereInjury })
            .WithLineup(new Lineup(remaining));
        return (next, absent);
    }

    [Fact]
    public void WithoutTheDecisionTheBenchStillCoversTheHoleAndTheWarningSaysSo()
    {
        var (state, _) = WithOneStarterOut();

        var effective = RunLineup.Effective(state);
        var warnings = RunEngine.LineupWarnings(state);

        Assert.Equal(RunRules.MaxStarters, effective.Lineup.Slots.Count);
        Assert.Single(effective.FilledIds);
        Assert.Contains(warnings, w => w.Kind == LineupWarningKind.FilledFromBench && w.PlayerId == effective.FilledIds[0]);
        Assert.DoesNotContain(warnings, w => w.Kind is LineupWarningKind.ShortByChoice or LineupWarningKind.Shorthanded);
    }

    [Fact]
    public void ThePlayerCanDecideToPlayWithWhoHePutAndNobodyIsFilledIn()
    {
        var (state, _) = WithOneStarterOut();
        int saved = state.Lineup.Slots.Count;

        var decided = RunEngine.Apply(state, new SetLineup(state.Lineup, PlayShort: true), Catalog, SystemsTestSupport.Systems);

        var effective = RunLineup.Effective(decided);
        Assert.True(RunLineup.PlaysShort(decided));
        Assert.Equal(saved, effective.Lineup.Slots.Count);
        Assert.Empty(effective.FilledIds);

        // Y es lo que se juega, no sólo lo que se enseña: el mismo cálculo que construye el partido.
        var match = RunLineup.Build(decided, Catalog);
        Assert.Equal(saved, match.Starters.Count);
        Assert.Equal(effective.Lineup.Slots.Select(s => s.PlayerId), match.Lineup.Slots.Select(s => s.PlayerId));
        Assert.Equal(state.AvailablePlayerCount - saved, match.Bench.Count);
    }

    /// <summary>
    /// RF-002d: «la interfaz lo advierte de forma explícita antes de confirmar». Con banquillo disponible, la
    /// advertencia es la de la decisión tomada, no la de falta de gente: <c>Shorthanded</c> queda para la inferioridad
    /// que ni el banquillo entero arregla.
    /// </summary>
    [Fact]
    public void ChoosingToPlayShortIsWarnedAsAChoiceNotAsALackOfPlayers()
    {
        var (state, _) = WithOneStarterOut();
        var decided = RunEngine.Apply(state, new SetLineup(state.Lineup, PlayShort: true), Catalog, SystemsTestSupport.Systems);

        var warnings = RunEngine.LineupWarnings(decided);

        Assert.Single(warnings, w => w.Kind == LineupWarningKind.ShortByChoice);
        Assert.DoesNotContain(warnings, w => w.Kind is LineupWarningKind.Shorthanded or LineupWarningKind.FilledFromBench);
    }

    [Fact]
    public void WhenThereIsNobodyToFillInTheShortageIsReal()
    {
        var (state, _) = WithOneStarterOut();

        // Tres más de baja —un titular y los dos suplentes—: quedan cinco disponibles y cinco en la alineación, así
        // que ni el banquillo entero llega a siete aunque el jugador lo quisiera. Decidir no cambia lo que es.
        var slots = state.Lineup.Slots.ToList();
        var starter = state.Roster.First(p => p.IsAvailable && slots.Any(s => s.PlayerId == p.Id) && p.Position == Position.Defender);
        var benched = state.Roster.Where(p => p.IsAvailable && slots.All(s => s.PlayerId != p.Id)).ToList();
        Assert.Equal(2, benched.Count);
        var stripped = state
            .WithPlayer(starter with { PhysicalState = PhysicalState.SevereInjury })
            .WithPlayer(benched[0] with { PhysicalState = PhysicalState.SevereInjury })
            .WithPlayer(benched[1] with { PhysicalState = PhysicalState.SevereInjury })
            .WithLineup(new Lineup(slots.Where(s => s.PlayerId != starter.Id).ToList()));
        var decided = RunEngine.Apply(stripped, new SetLineup(stripped.Lineup, PlayShort: true), Catalog, SystemsTestSupport.Systems);

        var warnings = RunEngine.LineupWarnings(decided);

        Assert.Equal(RunRules.MinimumAvailablePlayers, decided.AvailablePlayerCount);
        Assert.Single(warnings, w => w.Kind == LineupWarningKind.Shorthanded);
        Assert.DoesNotContain(warnings, w => w.Kind == LineupWarningKind.ShortByChoice);
    }

    /// <summary>
    /// La decisión no puede dejar a la run sin equipo: si después de decidir alguien de los que puso deja de poder
    /// jugar, el once se completa hasta el mínimo con el que el simulador admite un equipo, y no más.
    /// </summary>
    [Fact]
    public void ADecisionThatFallsBelowTheMinimumIsCompletedOnlyUpToTheMinimum()
    {
        var (state, _) = WithOneStarterOut();
        var five = state.Lineup.Slots.Take(RunRules.MinimumAvailablePlayers).ToList();
        var decided = RunEngine.Apply(state, new SetLineup(new Lineup(five), PlayShort: true), Catalog, SystemsTestSupport.Systems);
        var lost = decided.GetPlayer(five[1].PlayerId);
        var stranded = decided.WithPlayer(lost with { PhysicalState = PhysicalState.SevereInjury });

        var effective = RunLineup.Effective(stranded);

        Assert.Equal(RunRules.MinimumAvailablePlayers, effective.Lineup.Slots.Count);
        Assert.Single(effective.FilledIds);
    }

    /// <summary>
    /// La decisión es de este partido, no de la run: al terminar un partido se borra, y el siguiente vuelve al
    /// comportamiento de siempre. Es la misma regla que RF-093 vía 1 —«el riesgo se asume partido a partido»—.
    /// </summary>
    [Fact]
    public void TheDecisionDoesNotSurviveTheMatch()
    {
        var (state, _) = WithOneStarterOut();
        var (atMatch, node) = TestRuns.WalkToMatch(state, Catalog, SystemsTestSupport.Systems);
        var decided = RunEngine.Apply(atMatch, new SetLineup(atMatch.Lineup, PlayShort: true), Catalog, SystemsTestSupport.Systems);

        var played = RunEngine.BuildMatch(decided, node.Id, Catalog, SystemsTestSupport.Systems);
        Assert.Equal(decided.Lineup.Slots.Count, played.Lineup.Starters.Count);

        var after = RunEngine.Enter(decided, node.Id, Catalog, SystemsTestSupport.Systems);

        Assert.False(RunLineup.PlaysShort(after));
        Assert.Equal(0, after.Counter(RunLineup.ShortCounter));
    }

    /// <summary>
    /// Pedirlo otra vez sin la decisión la quita, y un <c>SetLineup</c> corriente no la deja escrita: un estado que
    /// nunca la usó es el mismo de antes, campo a campo. Es lo que mantiene idéntica la política de <c>/Balance</c>, que
    /// nunca la pide.
    /// </summary>
    [Fact]
    public void ASetLineupWithoutTheDecisionNeverWritesTheCounterAndClearsIt()
    {
        var (state, _) = WithOneStarterOut();

        var plain = RunEngine.Apply(state, new SetLineup(state.Lineup), Catalog, SystemsTestSupport.Systems);
        Assert.False(plain.Counters.ContainsKey(RunLineup.ShortCounter));

        var decided = RunEngine.Apply(plain, new SetLineup(plain.Lineup, PlayShort: true), Catalog, SystemsTestSupport.Systems);
        Assert.True(RunLineup.PlaysShort(decided));

        var undone = RunEngine.Apply(decided, new SetLineup(decided.Lineup), Catalog, SystemsTestSupport.Systems);
        Assert.False(RunLineup.PlaysShort(undone));
        Assert.Single(RunLineup.Effective(undone).FilledIds);
    }
}
