using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Save;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.View;

namespace Underleague.Sim.Tests.Run.View;

/// <summary>
/// BX-19, ADR 0191, RT-024: <see cref="MatchPlaybacks.PlayAndEnter"/> simula el partido una vez y reutiliza la
/// resolución de la reproducción para entrar en el nodo. Tiene que dar, byte a byte, lo mismo que las dos llamadas de
/// antes por separado (reproducción + <see cref="RunEngine.EnterMatch(RunState, int, Catalog, IRunSystems?, MatchDecisions?)"/>),
/// sobre varias semillas y varias decisiones en vivo, por el camino rápido y por el de respaldo.
/// </summary>
public sealed class PlayAndEnterTests
{
    private static readonly Underleague.Sim.Data.Catalog Catalog = TestData.LoadCatalog();

    private static IEnumerable<(string Name, MatchDecisions Decisions, int Watched)> DecisionSets()
    {
        yield return ("ninguna", MatchDecisions.None, 0);
        yield return ("ofensiva-200", MatchDecisions.None with { OrderChanges = new[] { new OrderChange(200, Mentality.Offensive) } }, 0);
        yield return ("defensiva-600-ofensiva-900", MatchDecisions.None with
        {
            OrderChanges = new[] { new OrderChange(600, Mentality.Defensive), new OrderChange(900, Mentality.Offensive) },
        }, 0);
        yield return ("visto-entero", MatchDecisions.None, 5000);
        yield return ("visto-700-defensiva-701", MatchDecisions.None with { OrderChanges = new[] { new OrderChange(701, Mentality.Defensive) } }, 700);
    }

    private static string Fingerprint(MatchEntry entry) =>
        string.Join(
            "|",
            RunSave.Save(entry.State),
            entry.Outcome.ToString(),
            entry.Summary.Won,
            entry.Summary.GoalsFor,
            entry.Summary.GoalsAgainst,
            entry.Summary.Ticks,
            entry.Summary.WentToGoldenGoal,
            string.Join(",", entry.Summary.PlayedPlayerIds),
            string.Join(",", entry.Summary.BenchedPlayerIds),
            entry.Summary.OwnInjuries,
            entry.Summary.OwnDeaths,
            string.Join(",", entry.Summary.Report.Goals), entry.Summary.Report.Ticks, entry.Summary.Report.PossessionChanges, string.Join(",", entry.Summary.Report.Shots),
            entry.Summary.Bet?.ToString() ?? "-");

    private static string Fingerprint(MatchPlayback playback) =>
        string.Join(
            "|",
            playback.Seed,
            playback.Result.Events.Count,
            string.Join(";", playback.Result.Events.Select(e => $"{e.Tick}:{e.Type}:{e.Team}:{e.Actor}:{e.Opponent}:{e.Detail}")),
            playback.Result.Trace!.FrameCount,
            string.Join(",", playback.Setup.Home.Substitutions.Select(s => $"{s.Tick}:{s.OutPlayerId}:{s.InPlayerId}")),
            string.Join(",", playback.Setup.Away.Substitutions.Select(s => $"{s.Tick}:{s.OutPlayerId}:{s.InPlayerId}")));

    [Fact]
    public void OneSimulationGivesTheSameMatchAndTheSameRunAsTwo()
    {
        var files = TestData.LoadAllFiles();
        int fast = 0;
        int fallback = 0;
        foreach (ulong seed in new ulong[] { 20260905, 20260906, 20260907, 20260908, 1, 2, 3, 42 })
        {
            var systems = new BossRunSystems(BossCatalog.FromJson(files), StandardRunSystems.FromJson(files));
            var setup = StandardRunSystems.FromJson(files).NewRunSetup("orc_ironworks", Race.Orc, files);
            var state = systems.AssignBosses(RunEngine.Start(setup, seed, Catalog, systems));
            var node = RunEngine.AvailableNodes(state).First(n => n.IsMatch);
            foreach (var (name, decisions, watched) in DecisionSets())
            {
                // Antes (RunController.Answer hasta BX-19): reproducción y entrada por separado.
                var playbackBefore = MatchPlaybacks.OfResolvingBlockedPoints(
                    state, node.Id, Catalog, systems, trace: true, decisions, watched, out var resolvedBefore);
                var entryBefore = RunEngine.EnterMatch(state, node.Id, Catalog, systems, resolvedBefore);

                var (playback, entry) = MatchPlaybacks.PlayAndEnter(state, node.Id, Catalog, systems, decisions, watched, out var resolved);

                Assert.Equal(Fingerprint(playbackBefore), Fingerprint(playback));
                Assert.Equal(resolvedBefore.Substitutions, resolved.Substitutions);
                Assert.Equal(Fingerprint(entryBefore), Fingerprint(entry));

                if (SubstitutionPoints.Pending(playback.Setup, playback.Result, playback.PlayerTeam, Catalog, resolved.Declines) is null)
                {
                    fast++;
                }
                else
                {
                    fallback++;
                }
            }
        }

        // Regla J: los dos caminos se han ejercitado de verdad, si no la igualdad no prueba nada del que falta.
        Assert.True(fast > 0, $"ningún caso por el camino rápido ({fallback} por el de respaldo)");
        Assert.True(fallback > 0, $"ningún caso por el camino de respaldo ({fast} por el rápido)");
    }
}
