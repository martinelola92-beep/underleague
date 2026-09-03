using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Balance;

/// <summary>Una fila de matches.csv (docs/fase0-diseno.md §4).</summary>
public sealed record MatchRow(
    int Index,
    ulong Seed,
    string HomeId,
    string AwayId,
    int HomeGoals,
    int AwayGoals,
    int Winner,
    int Ticks,
    bool GoldenGoal,
    bool Forfeit,
    int PossessionChanges,
    int PassChains,
    int PassChainTotalLength,
    int Shots,
    int ShotsOnTarget,
    int Tackles,
    int Fouls,
    int Yellow,
    int Red,
    int Injuries,
    int BallThird0,
    int BallThird1,
    int BallThird2,
    int FinalBias);

/// <summary>Acumulador de estadísticas de un jugador generado, a través de todos los partidos que jugó (players.csv).</summary>
public sealed class PlayerAggregate
{
    public required int PlayerId { get; init; }

    public required string TeamId { get; init; }

    public required string Name { get; init; }

    public required string Race { get; init; }

    public required string Position { get; init; }

    public required string Rarity { get; init; }

    public int Matches;
    public int Goals;
    public int Assists;
    public int Shots;
    public int PassesAttempted;
    public int PassesCompleted;
    public int Tackles;
    public int TacklesWon;
    public int Fouls;
    public int Cards;
    public int Injuries;
    public int TicksOnPitch;
}

/// <summary>Resultado completo de un lote de /Balance.</summary>
public sealed record BatchResult(
    IReadOnlyList<MatchRow> Matches,
    IReadOnlyList<PlayerAggregate> Players,
    int TotalRequested,
    int EnginePendingFailures,
    IReadOnlyList<string> FirstMatchLog,
    UtilityDump? FirstMatchUtilityDump,
    TimeSpan Elapsed);

/// <summary>
/// Genera los equipos del conjunto de referencia y ejecuta los partidos del lote (docs/fase0-diseno.md §4).
/// Mientras Simulator.Run lance NotSupportedException("engine pending", paquete B pendiente), cada partido
/// que falle por esa causa se cuenta aparte y no se añade a Matches; Program decide qué hacer con el conteo.
/// </summary>
public static class BatchRunner
{
    public static BatchResult Run(Options options, Catalog catalog, ReferenceConfig reference)
    {
        var teamIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < reference.Teams.Count; i++)
        {
            teamIndex[reference.Teams[i].Id] = i;
        }

        // Cada equipo de reference.teams se genera una sola vez con RngStreams.Generation(seed, índice)
        // y firstPlayerId = 1 + índice*100, para que los ids de jugador no colisionen entre equipos.
        var instances = new TeamSetup[reference.Teams.Count];
        for (int i = 0; i < reference.Teams.Count; i++)
        {
            var team = reference.Teams[i];
            var rng = RngStreams.Generation(options.Seed, i);
            instances[i] = TeamGenerator.Generate(ref rng, catalog, team.Id, team.Race, team.Quality, 1 + (i * 100));
        }

        // Decisión fuera de la especificación: cuando un emparejamiento enfrenta a un equipo consigo mismo,
        // la segunda instancia usa RngStreams.Generation(seed, 1000 + índice) y firstPlayerId derivado del
        // mismo esquema (1 + (1000+índice)*100) para no colisionar con la instancia primaria ni entre sí.
        var twins = new Dictionary<int, TeamSetup>();
        foreach (var pairing in reference.Pairings)
        {
            if (pairing.HomeId != pairing.AwayId)
            {
                continue;
            }

            int idx = teamIndex[pairing.HomeId];
            if (twins.ContainsKey(idx))
            {
                continue;
            }

            var team = reference.Teams[idx];
            var rng = RngStreams.Generation(options.Seed, 1000 + idx);
            twins[idx] = TeamGenerator.Generate(ref rng, catalog, team.Id, team.Race, team.Quality, 1 + ((1000 + idx) * 100));
        }

        var playerLookup = new Dictionary<int, PlayerAggregate>();
        RegisterPlayers(playerLookup, instances);
        RegisterPlayers(playerLookup, twins.Values);

        // Reparto de --runs entre los emparejamientos, en orden, con el resto a los primeros.
        int pairingsCount = reference.Pairings.Count;
        int baseCount = options.Runs / pairingsCount;
        int remainder = options.Runs % pairingsCount;

        // Decisión fuera de la especificación: la referencia del árbitro no está cubierta por §2.2/§4;
        // se usa un árbitro neutro fijo para todos los partidos del lote (sin sesgo inicial).
        var referee = new RefereeSetup("Referee", RefereeTrait.Neutral, 0);

        var matches = new List<MatchRow>(options.Runs);
        string[] firstMatchLog = Array.Empty<string>();
        UtilityDump? firstMatchDump = null;
        int enginePendingFailures = 0;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        int globalIndex = 0;
        for (int p = 0; p < pairingsCount; p++)
        {
            var pairing = reference.Pairings[p];
            int homeIdx = teamIndex[pairing.HomeId];
            int awayIdx = teamIndex[pairing.AwayId];
            TeamSetup homeTeam = instances[homeIdx];
            TeamSetup awayTeam = pairing.HomeId == pairing.AwayId ? twins[awayIdx] : instances[awayIdx];
            var setup = new MatchSetup(homeTeam, awayTeam, referee);

            int matchesForPairing = baseCount + (p < remainder ? 1 : 0);
            for (int k = 0; k < matchesForPairing; k++)
            {
                int i = globalIndex;
                globalIndex++;

                // Semilla del partido i, derivada del flujo de partido (RT-022).
                ulong matchSeed = RngStreams.MatchSeed(options.Seed, i);

                bool isFirst = i == 0;
                var config = new SimConfig(
                    CollectLog: isFirst && options.Log,
                    DumpUtility: isFirst ? options.DumpUtility : null);

                MatchResult result;
                try
                {
                    result = Simulator.Run(setup, matchSeed, catalog, config);
                }
                catch (NotSupportedException)
                {
                    enginePendingFailures++;
                    continue;
                }

                var report = result.Report;
                matches.Add(new MatchRow(
                    Index: i,
                    Seed: (ulong)i,
                    HomeId: pairing.HomeId,
                    AwayId: pairing.AwayId,
                    HomeGoals: report.Goals[0],
                    AwayGoals: report.Goals[1],
                    Winner: report.Winner,
                    Ticks: report.Ticks,
                    GoldenGoal: report.WentToGoldenGoal,
                    Forfeit: report.Forfeit,
                    PossessionChanges: report.PossessionChanges,
                    PassChains: report.PassChains,
                    PassChainTotalLength: report.PassChainTotalLength,
                    Shots: report.Shots[0] + report.Shots[1],
                    ShotsOnTarget: report.ShotsOnTarget[0] + report.ShotsOnTarget[1],
                    Tackles: report.Tackles,
                    Fouls: report.Fouls,
                    Yellow: report.YellowCards,
                    Red: report.RedCards,
                    Injuries: report.Injuries,
                    BallThird0: report.BallTicksByThird[0],
                    BallThird1: report.BallTicksByThird[1],
                    BallThird2: report.BallTicksByThird[2],
                    FinalBias: report.FinalBias));

                AccumulatePlayers(playerLookup, report.Players);

                if (isFirst)
                {
                    firstMatchLog = report.Log.ToArray();
                    firstMatchDump = report.UtilityDump;
                }
            }
        }

        stopwatch.Stop();

        var players = playerLookup.Values
            .OrderBy(p => p.PlayerId)
            .ToList();

        return new BatchResult(matches, players, options.Runs, enginePendingFailures, firstMatchLog, firstMatchDump, stopwatch.Elapsed);
    }

    private static void RegisterPlayers(Dictionary<int, PlayerAggregate> lookup, IEnumerable<TeamSetup> teams)
    {
        foreach (var team in teams)
        {
            foreach (var player in team.Players)
            {
                lookup[player.Id] = new PlayerAggregate
                {
                    PlayerId = player.Id,
                    TeamId = team.Id,
                    Name = player.Name,
                    Race = player.Race.ToString(),
                    Position = player.Position.ToString(),
                    Rarity = player.Rarity.ToString(),
                };
            }
        }
    }

    private static void AccumulatePlayers(Dictionary<int, PlayerAggregate> lookup, IReadOnlyList<PlayerMatchStats> stats)
    {
        foreach (var stat in stats)
        {
            if (!lookup.TryGetValue(stat.PlayerId, out var aggregate))
            {
                // No debería ocurrir: todo PlayerId de Report.Players proviene de un equipo generado
                // arriba. Se ignora en vez de lanzar para no tirar el lote completo por un dato inesperado.
                continue;
            }

            aggregate.Matches++;
            aggregate.Goals += stat.Goals;
            aggregate.Assists += stat.Assists;
            aggregate.Shots += stat.Shots;
            aggregate.PassesAttempted += stat.PassesAttempted;
            aggregate.PassesCompleted += stat.PassesCompleted;
            aggregate.Tackles += stat.Tackles;
            aggregate.TacklesWon += stat.TacklesWon;
            aggregate.Fouls += stat.Fouls;
            aggregate.Cards += stat.Cards;
            aggregate.Injuries += stat.Injured ? 1 : 0;
            aggregate.TicksOnPitch += stat.TicksOnPitch;
        }
    }
}
