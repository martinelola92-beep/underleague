using System.Text.Json;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Engine;

/// <summary>
/// Puerta de salida de la fase 0 (RT-081, docs/fase0-diseno.md §6): 1.000 partidos del conjunto de
/// referencia con la misma generación de equipos y las mismas semillas que Balance/BatchRunner, y todas
/// las métricas de RT-056 dentro de rango. Se marca con <c>Category=Gate</c> para poder excluirla del
/// bucle de desarrollo (<c>dotnet test --filter Category!=Gate</c>); en CI corre entera.
///
/// El cálculo de métricas es el de <see cref="MatchMetrics"/>, el mismo que usa /Balance: la puerta y el
/// lote no pueden divergir.
/// </summary>
[Trait("Category", "Gate")]
public sealed class StatisticalTests
{
    /// <summary>Partidos del lote de la puerta (RT-081).</summary>
    private const int Runs = 1000;

    /// <summary>Semilla base del lote; la misma que la medición de referencia de docs/balance.md.</summary>
    private const ulong Seed = 1;

    /// <summary>Separación entre los ids de jugador de dos equipos generados (Balance/BatchRunner).</summary>
    private const int PlayerIdStride = 100;

    /// <summary>Desplazamiento del índice de generación de la segunda instancia de un equipo contra sí mismo.</summary>
    private const int TwinIndexOffset = 1000;

    private static readonly Lazy<IReadOnlyList<MetricResult>> Results = new(Compute);

    [Fact]
    public void PossessionChangesAreInRange() => AssertInRange(MatchMetrics.PossessionChanges);

    [Fact]
    public void PassChainAvgLengthIsInRange() => AssertInRange(MatchMetrics.PassChainAvgLength);

    [Fact]
    public void ShotsPerMatchAreInRange() => AssertInRange(MatchMetrics.ShotsPerMatch);

    [Fact]
    public void ScorelineShareIsInRange() => AssertInRange(MatchMetrics.ScorelineShare);

    [Fact]
    public void BallThirdMaxShareIsInRange() => AssertInRange(MatchMetrics.BallThirdMaxShare);

    [Fact]
    public void TacklesPerMatchAreInRange() => AssertInRange(MatchMetrics.TacklesPerMatch);

    [Fact]
    public void InjuriesPerMatchAreInRange() => AssertInRange(MatchMetrics.InjuriesPerMatch);

    /// <summary>Fase 0: el equipo con 20 puntos más de calidad gana entre el 65% y el 80% (docs/balance.md).</summary>
    [Fact]
    public void BetterTeamWinRateIsInRange()
    {
        var gated = Results.Value
            .Where(r => r.Name.StartsWith(MatchMetrics.BetterTeamWinRatePrefix, StringComparison.Ordinal)
                && r.Status != "INFO")
            .ToList();

        Assert.NotEmpty(gated);
        foreach (var row in gated)
        {
            Assert.True(
                row.Value is >= 65 and <= 80,
                $"{row.Name} = {row.Value:F2}, fuera de 65..80 en {Runs} partidos con semilla {Seed}");
        }
    }

    /// <summary>Ninguna métrica obligatoria puede quedar OUT: es el criterio de salida completo.</summary>
    [Fact]
    public void NoMandatoryMetricIsOutOfRange()
    {
        var offenders = Results.Value.Where(r => r.Status == "OUT").ToList();
        Assert.True(
            offenders.Count == 0,
            "métricas fuera de rango: " + string.Join(", ", offenders.Select(r => $"{r.Name}={r.Value:F2}")));
    }

    private static void AssertInRange(string metric)
    {
        var row = Results.Value.SingleOrDefault(r => r.Name == metric);
        Assert.NotNull(row);
        Assert.True(
            row!.Status == "IN",
            $"{row.Name} = {row.Value:F2}, fuera de {row.RangeMin?.ToString("F2") ?? "-"}..{row.RangeMax?.ToString("F2") ?? "-"}");
    }

    private static IReadOnlyList<MetricResult> Compute()
    {
        var catalog = TestData.LoadCatalog();
        var reference = ReferenceSet.Load(TestData.DataDirectory);

        // Generación de equipos idéntica a Balance/BatchRunner: un equipo por entrada de reference.json
        // con RngStreams.Generation(seed, índice) e ids a partir de 1 + índice*100; una segunda instancia
        // con índice 1000+i para los emparejamientos de un equipo contra sí mismo.
        var instances = new TeamSetup[reference.Teams.Count];
        for (int i = 0; i < reference.Teams.Count; i++)
        {
            var team = reference.Teams[i];
            var rng = RngStreams.Generation(Seed, i);
            instances[i] = TeamGenerator.Generate(ref rng, catalog, team.Id, team.Race, team.Quality, 1 + (i * PlayerIdStride), team.Level, team.Rarity);
        }

        var twins = new Dictionary<int, TeamSetup>();
        foreach (var pairing in reference.Pairings)
        {
            if (pairing.HomeId != pairing.AwayId)
            {
                continue;
            }

            int index = reference.IndexOf(pairing.HomeId);
            if (twins.ContainsKey(index))
            {
                continue;
            }

            var team = reference.Teams[index];
            var rng = RngStreams.Generation(Seed, TwinIndexOffset + index);
            twins[index] = TeamGenerator.Generate(
                ref rng, catalog, team.Id, team.Race, team.Quality, 1 + ((TwinIndexOffset + index) * PlayerIdStride), team.Level, team.Rarity);
        }

        var referee = new RefereeSetup("Referee", RefereeTrait.Neutral, 0);
        var config = new SimConfig(CollectLog: false);

        int pairingCount = reference.Pairings.Count;
        int baseCount = Runs / pairingCount;
        int remainder = Runs % pairingCount;

        var summaries = new List<MatchSummary>(Runs);
        int globalIndex = 0;
        for (int p = 0; p < pairingCount; p++)
        {
            var pairing = reference.Pairings[p];
            int homeIndex = reference.IndexOf(pairing.HomeId);
            int awayIndex = reference.IndexOf(pairing.AwayId);
            var home = instances[homeIndex];
            var away = pairing.HomeId == pairing.AwayId ? twins[awayIndex] : instances[awayIndex];
            var setup = new MatchSetup(home, away, referee);

            int matchesForPairing = baseCount + (p < remainder ? 1 : 0);
            for (int k = 0; k < matchesForPairing; k++)
            {
                ulong matchSeed = RngStreams.MatchSeed(Seed, globalIndex);
                globalIndex++;
                var result = Simulator.Run(setup, matchSeed, catalog, config);
                summaries.Add(MatchSummary.FromReport(result.Report, pairing.HomeId, pairing.AwayId));
            }
        }

        Assert.Equal(Runs, summaries.Count);
        return MatchMetrics.Compute(summaries, reference.Pairings
            .Select(p => new MetricPairing(p.HomeId, p.AwayId, reference.QualityOf(p.HomeId), reference.QualityOf(p.AwayId)))
            .ToList());
    }

    /// <summary>Lectura mínima de data/balance/reference.json; /Balance tiene la suya, aquí no se comparte proyecto.</summary>
    private sealed record ReferenceSet(
        IReadOnlyList<(string Id, Race Race, int Quality, int Level, Rarity? Rarity)> Teams,
        IReadOnlyList<(string HomeId, string AwayId)> Pairings)
    {
        public static ReferenceSet Load(string dataDirectory)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDirectory, "balance", "reference.json")));
            var root = document.RootElement;

            var teams = new List<(string, Race, int, int, Rarity?)>();
            foreach (var team in root.GetProperty("teams").EnumerateArray())
            {
                teams.Add((
                    team.GetProperty("id").GetString()!,
                    Enum.Parse<Race>(team.GetProperty("race").GetString()!),
                    team.GetProperty("quality").GetInt32(),
                    team.TryGetProperty("level", out var level) ? level.GetInt32() : 1,
                    team.TryGetProperty("rarity", out var rarity) ? Enum.Parse<Rarity>(rarity.GetString()!, ignoreCase: true) : null));
            }

            var pairings = new List<(string, string)>();
            foreach (var pairing in root.GetProperty("pairings").EnumerateArray())
            {
                pairings.Add((pairing[0].GetString()!, pairing[1].GetString()!));
            }

            return new ReferenceSet(teams, pairings);
        }

        public int IndexOf(string teamId)
        {
            for (int i = 0; i < Teams.Count; i++)
            {
                if (Teams[i].Id == teamId)
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"equipo no encontrado en reference.json: {teamId}");
        }

        public int QualityOf(string teamId) => Teams[IndexOf(teamId)].Quality;
    }
}
