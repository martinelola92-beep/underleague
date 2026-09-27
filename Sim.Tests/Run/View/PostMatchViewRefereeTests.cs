using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>
/// ADR 0158 §5, la mitad de RF-119 que faltaba: el informe cuenta las faltas no señaladas por equipo. El
/// motor ya las emitía y movía el criterio con ellas (RF-063); esto solo comprueba que la capa de vista
/// las lee bien de la secuencia de eventos.
/// </summary>
public sealed class PostMatchViewRefereeTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();

    private static MapNode Node() => new(101, 1, 0, 0, NodeKind.LeagueMatch, Array.Empty<int>(), string.Empty, 3);

    /// <summary>
    /// Busca una semilla del emparejamiento indicado que produzca al menos una falta no señalada del
    /// equipo <paramref name="team"/>. En el emparejamiento brutal el frágil (equipo 0, fuerza 1) nunca
    /// gana el duelo de entrada contra el bruto (medido: 0 faltas propias en 200 semillas), así que "las
    /// dos a la vez" no es un escenario alcanzable con este emparejamiento: se piden por separado, cada
    /// una con el lado agresivo puesto donde hace falta.
    /// </summary>
    private static (MatchResult Result, MatchSetup Setup, ulong Seed) MatchWithUnseenFoulsFrom(MatchSetup setup, int team)
    {
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var result = Simulator.Run(setup, seed, Catalog, new SimConfig(CollectLog: false));
            if (result.Events.Any(e => e.Type == EventType.Foul && e.Detail == "unseen" && e.Team == team))
            {
                return (result, setup, seed);
            }
        }

        throw new InvalidOperationException($"no se encontró una semilla con faltas no señaladas del equipo {team} en 60 intentos");
    }

    private static PostMatchReport Report(MatchResult result, MatchSetup setup, ulong seed)
    {
        var playback = new MatchPlayback(Node(), setup, result, seed);
        var stateAfterMatch = RunEngine.Start(TestRuns.Setup(), 1UL, Catalog);
        var summary = new RunMatchSummary(
            Node().Id, NodeKind.LeagueMatch, result.Report.Winner == 0,
            result.Report.Goals[0], result.Report.Goals[1], result.Report.Ticks, result.Report.WentToGoldenGoal,
            Array.Empty<int>(), Array.Empty<int>(), 0, 0, result.Report);

        return PostMatchView.Build(playback, stateAfterMatch, summary, Catalog);
    }

    /// <summary>El rival (equipo 1, el agresivo del emparejamiento brutal) comete faltas no señaladas: cuentan como UnseenFoulsAgainst.</summary>
    [Fact]
    public void UnseenFoulsByTheRivalCountAsAgainst()
    {
        var (result, setup, seed) = MatchWithUnseenFoulsFrom(TestMatches.Brutal(Catalog), team: 1);
        var report = Report(result, setup, seed);

        int expected = result.Events.Count(e => e.Type == EventType.Foul && e.Detail == "unseen" && e.Team == 1);
        Assert.True(expected > 0);
        Assert.Equal(expected, report.Referee.UnseenFoulsAgainst);
        Assert.Equal(0, report.Referee.UnseenFoulsFor);

        // Invariante estructural: nunca hay más no señaladas que faltas totales de ese lado.
        Assert.True(report.Referee.UnseenFoulsAgainst <= report.Referee.FoulsAgainst);
    }

    /// <summary>
    /// Con los papeles invertidos (el propio equipo, 0, es ahora el agresivo) las mismas faltas no
    /// señaladas cuentan como UnseenFoulsFor: "For" es "cometidas por el jugador", no "a su favor".
    /// </summary>
    [Fact]
    public void UnseenFoulsByThePlayersOwnTeamCountAsFor()
    {
        var brutal = TestMatches.Brutal(Catalog);
        var reversed = new MatchSetup(brutal.Away, brutal.Home, brutal.Referee);
        var (result, setup, seed) = MatchWithUnseenFoulsFrom(reversed, team: 0);
        var report = Report(result, setup, seed);

        int expected = result.Events.Count(e => e.Type == EventType.Foul && e.Detail == "unseen" && e.Team == 0);
        Assert.True(expected > 0);
        Assert.Equal(expected, report.Referee.UnseenFoulsFor);
        Assert.Equal(0, report.Referee.UnseenFoulsAgainst);

        Assert.True(report.Referee.UnseenFoulsFor <= report.Referee.FoulsFor);
    }

    /// <summary>Un partido sin ninguna falta no señalada (por ejemplo cero faltas) informa 0, no null ni excepción.</summary>
    [Fact]
    public void NoUnseenFoulsReportsZero()
    {
        var setup = TestMatches.Reference(Catalog, 1);
        var builder = new MatchReportBuilder { Winner = 0, Ticks = 900 };
        var report0 = builder.Build();
        var result = new MatchResult(Array.Empty<MatchEvent>(), report0, Array.Empty<PlayerCounterDelta>());
        var playback = new MatchPlayback(Node(), setup, result, 1);
        var stateAfterMatch = RunEngine.Start(TestRuns.Setup(), 1UL, Catalog);
        var summary = new RunMatchSummary(
            Node().Id, NodeKind.LeagueMatch, true, 0, 0, 900, false,
            Array.Empty<int>(), Array.Empty<int>(), 0, 0, report0);

        var report = PostMatchView.Build(playback, stateAfterMatch, summary, Catalog);

        Assert.Equal(0, report.Referee.UnseenFoulsFor);
        Assert.Equal(0, report.Referee.UnseenFoulsAgainst);
    }
}
