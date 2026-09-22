using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Tests.Run.Systems;

namespace Underleague.Sim.Tests.Run;

/// <summary>
/// Paso 3b de <c>MatchResolution.Apply</c> (RF-122, ADR 0124, enmienda tras la revisión independiente):
/// el único camino de producción que escribe <see cref="RunPlayer.Career"/>, y el que no tenía ni un test
/// -"un <c>with</c> mal puesto pasaba los 1.073", corrección 1 de la enmienda-. Cada test de aquí demuestra
/// una propiedad concreta que la enmienda exige, no solo que un entero suba.
/// </summary>
public sealed class MatchResolutionCareerTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static RunState BaseState(ulong seed = 20260922UL) =>
        RunEngine.Start(TestRuns.Setup(), seed, Catalog);

    private static MapNode Node(int id = 101) =>
        new(id, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

    private static MatchLineup LineupFor(IReadOnlyList<RunPlayer> starters, IReadOnlyList<RunPlayer> bench)
    {
        var startDefs = starters.Select(p => p.ToDefinition(Catalog)).ToList();
        var benchDefs = bench.Select(p => p.ToDefinition(Catalog)).ToList();
        return new MatchLineup(startDefs, benchDefs, Lineup.Default(startDefs), EmergencyGoalkeeperId: -1);
    }

    private static PlayerMatchStats Stats(
        int playerId,
        int team,
        int goals = 0,
        int assists = 0,
        int tackles = 0,
        int tacklesWon = 0,
        int fouls = 0,
        int cards = 0,
        bool injured = false,
        int ticksOnPitch = 900,
        int injuriesCaused = 0,
        int deathsCaused = 0) =>
        new(playerId, team, goals, assists, 0, 0, 0, tackles, tacklesWon, fouls, cards, injured,
            ticksOnPitch, injuriesCaused, deathsCaused);

    private static MatchResult ResultWith(
        IReadOnlyList<PlayerMatchStats> stats, IReadOnlyList<MatchEvent>? events = null, int ticks = 900)
    {
        var builder = new MatchReportBuilder();
        builder.Winner = 0;
        builder.Ticks = ticks;
        builder.Players.AddRange(stats);
        var report = builder.Build();
        return new MatchResult(events ?? Array.Empty<MatchEvent>(), report, Array.Empty<PlayerCounterDelta>());
    }

    /// <summary>
    /// Camino de producción completo, sin nada hecho a mano: <c>RunEngine.EnterMatch</c> juega un partido
    /// real (con <c>Simulator.Run</c> de por medio) y todo titular que llega a pisar el campo tiene que
    /// salir con <c>Career.Matches == 1</c> y ticks jugados &gt; 0.
    /// </summary>
    [Fact]
    public void ARealMatchResolvedByMatchResolutionAccumulatesCareerForWhoeverPlayed()
    {
        var state = BaseState();
        var (walked, node) = TestRuns.WalkToMatch(state, Catalog, SystemsTestSupport.Systems);

        var entry = RunEngine.EnterMatch(walked, node.Id, Catalog, SystemsTestSupport.Systems);

        Assert.NotEmpty(entry.Summary.PlayedPlayerIds);
        foreach (int playedId in entry.Summary.PlayedPlayerIds)
        {
            var player = entry.State.GetPlayer(playedId);
            Assert.Equal(1, player.Career.Matches);
            Assert.True(player.Career.TicksOnPitch > 0);
        }
    }

    /// <summary>Un segundo partido SUMA a la carrera existente, nunca la sustituye.</summary>
    [Fact]
    public void ASecondMatchAddsToCareerInsteadOfReplacingIt()
    {
        var state = BaseState();
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter }, Array.Empty<RunPlayer>());

        var first = MatchResolution.Apply(
            state, Node(101), lineup, ResultWith(new[] { Stats(starter.Id, 0, goals: 2, ticksOnPitch: 900) }), Catalog);
        var afterFirst = first.State.GetPlayer(starter.Id);
        Assert.Equal(1, afterFirst.Career.Matches);
        Assert.Equal(2, afterFirst.Career.Goals);
        Assert.Equal(900, afterFirst.Career.TicksOnPitch);

        var second = MatchResolution.Apply(
            first.State, Node(102), lineup, ResultWith(new[] { Stats(starter.Id, 0, goals: 3, ticksOnPitch: 700) }), Catalog);
        var afterSecond = second.State.GetPlayer(starter.Id);

        Assert.Equal(2, afterSecond.Career.Matches);
        Assert.Equal(5, afterSecond.Career.Goals);
        Assert.Equal(1600, afterSecond.Career.TicksOnPitch);
    }

    /// <summary>
    /// El paso 3 (progresión: experiencia y nivel) corre justo antes del 3b (carrera) sobre el MISMO
    /// objeto <c>RunPlayer</c>: un <c>with</c> del paso 3b que no arrastrara el resto de campos se
    /// llevaría por delante la experiencia recién ganada, y viceversa.
    /// </summary>
    [Fact]
    public void CareerSurvivesApplyProgressionWhichRunsRightBeforeInTheSameApplyCall()
    {
        var state = BaseState();
        var starter = state.Roster[0];
        var before = starter;
        var lineup = LineupFor(new[] { starter }, Array.Empty<RunPlayer>());

        var applied = MatchResolution.Apply(
            state, Node(), lineup, ResultWith(new[] { Stats(starter.Id, 0, goals: 1, ticksOnPitch: 900) }), Catalog);
        var after = applied.State.GetPlayer(starter.Id);

        // El paso 3 (RF-025) sí corrió: jugar el partido da experiencia.
        Assert.True(after.Experience > before.Experience, "ApplyProgression no sumó experiencia: no se puede demostrar que el paso 3b sobrevive a él");

        // Y el paso 3b, justo después, no ha sido pisado por ese cambio ni lo ha pisado él.
        Assert.Equal(1, after.Career.Matches);
        Assert.Equal(1, after.Career.Goals);
    }

    /// <summary>
    /// Un suplente que no pisó el campo (RF-122, regla R1 de la enmienda: "un partido cuenta solo si se
    /// pisó el campo") no suma a <c>Career.Matches</c>, pero el resto de su historial -de partidos
    /// anteriores- llega intacto al otro lado.
    /// </summary>
    [Fact]
    public void ABenchedSubstituteWhoNeverEnteredKeepsTheRestOfTheirCareerButNotMatches()
    {
        var state = BaseState();
        var starter = state.Roster[0];
        var priorCareer = new RunCareer(
            Matches: 4, Goals: 1, Assists: 0, Tackles: 2, TacklesWon: 1, Fouls: 0, Cards: 0,
            InjuriesCaused: 0, DeathsCaused: 0, InjuriesSuffered: 1, TicksOnPitch: 3600);
        state = state.WithPlayer(state.Roster[1] with { Career = priorCareer });
        var sub = state.GetPlayer(state.Roster[1].Id);

        var lineup = LineupFor(new[] { starter }, new[] { sub });
        var stats = new[]
        {
            Stats(starter.Id, 0, ticksOnPitch: 900),
            Stats(sub.Id, 0, ticksOnPitch: 0), // en el banquillo: nunca entra (MatchEngine.ToStats de todos modos lo lista).
        };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats), Catalog);
        var after = applied.State.GetPlayer(sub.Id);

        Assert.Equal(priorCareer, after.Career);
    }

    /// <summary>
    /// Las estadísticas de un rival (Team 1) nunca se acreditan a la plantilla propia, ni siquiera con una
    /// colisión de id deliberada: si el filtro "Team == 0" del paso 3b se perdiera, los 99 goles y los
    /// 99999 ticks del "rival" se colarían en la carrera del titular propio con el mismo id.
    /// </summary>
    [Fact]
    public void RivalPlayerStatsNeverLeakIntoTheOwnRosterEvenOnAnIdCollision()
    {
        var state = BaseState();
        var starter = state.Roster[0];
        var lineup = LineupFor(new[] { starter }, Array.Empty<RunPlayer>());

        var stats = new[]
        {
            Stats(starter.Id, 0, goals: 1, ticksOnPitch: 900),
            Stats(starter.Id, 1, goals: 99, ticksOnPitch: 99999, injuriesCaused: 50),
        };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats), Catalog);
        var after = applied.State.GetPlayer(starter.Id);

        Assert.Equal(1, after.Career.Matches);
        Assert.Equal(1, after.Career.Goals);
        Assert.Equal(900, after.Career.TicksOnPitch);
        Assert.Equal(0, after.Career.InjuriesCaused);
    }

    /// <summary>Un jugador que muere en el partido conserva la carrera que acaba de hacer en él.</summary>
    [Fact]
    public void ADeadPlayerKeepsTheCareerFromTheMatchThatKilledThem()
    {
        var state = BaseState();
        var victim = state.Roster[0];
        var lineup = LineupFor(new[] { victim }, Array.Empty<RunPlayer>());
        var stats = new[] { Stats(victim.Id, 0, tackles: 3, ticksOnPitch: 400) };
        var events = new[]
        {
            new MatchEvent(
                EventType.Death, 300, 0, victim.Id, -1, -1,
                new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, string.Empty),
        };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events, ticks: 400), Catalog);
        var after = applied.State.GetPlayer(victim.Id);

        Assert.Equal(PhysicalState.Dead, after.PhysicalState);
        Assert.Equal(1, after.Career.Matches);
        Assert.Equal(3, after.Career.Tackles);
    }

    /// <summary>
    /// El matador de la enmienda R3: <c>MatchResolution</c> lee el <c>Opponent</c> del evento DEATH (que
    /// ahora rellena <c>MatchEngine.Kill</c>) y lo deja en <see cref="PlayerDeathDetail.KillerPlayerId"/>.
    /// Sin matador el valor es -1 (probado en el test anterior, donde Opponent también es -1).
    /// </summary>
    [Fact]
    public void DeathDetailRecordsTheKillerFromTheEventOpponent()
    {
        var state = BaseState();
        var victim = state.Roster[0];
        const int KillerId = 999321; // rival: no hace falta que exista en la plantilla propia.
        var lineup = LineupFor(new[] { victim }, Array.Empty<RunPlayer>());
        var stats = new[] { Stats(victim.Id, 0, ticksOnPitch: 400) };
        var events = new[]
        {
            new MatchEvent(
                EventType.Death, 300, 0, victim.Id, -1, KillerId,
                new Cell(0, 0), Zone.Own, MatchPhase.OpenPlay, 0, 0, string.Empty),
        };

        var applied = MatchResolution.Apply(state, Node(), lineup, ResultWith(stats, events, ticks: 400), Catalog);

        var detail = Assert.Single(applied.Summary.DeathDetails);
        Assert.Equal(victim.Id, detail.PlayerId);
        Assert.Equal(KillerId, detail.KillerPlayerId);
    }
}
