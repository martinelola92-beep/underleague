using Underleague.Sim.Analysis;

namespace Underleague.Sim.Tests.Balance;

/// <summary>Round-trip determinista del registro (§10) — sin simular ningún partido.</summary>
public sealed class BalanceRegistryTests
{
    [Fact]
    public void SerializeThenDeserializeRoundTrips()
    {
        var entry = new BalanceRegistryEntry(
            PerkId: "test_fixture",
            ProtocolVersion: "2026-09-18",
            Timestamp: new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero).ToString("o"),
            State: BalanceState.Balanced,
            Category: nameof(PerkBalanceCategory.ProbabilityBonus),
            Readiness: nameof(MetricReadiness.Ready),
            PrimaryMetric: "tacklesPerMatch",
            ValuesTried: new[] { 17.0, 24.0, 48.0 },
            Candidates: new[]
            {
                new BalanceCandidateRecord(
                    24.0,
                    new Dictionary<string, double> { ["tacklesPerMatch"] = 9.5 },
                    new Dictionary<string, double> { ["tacklesPerMatch"] = 8.0 },
                    BalanceState.Balanced),
            },
            ReasonForChange: "candidato central del volcado",
            ReasonForOutcome: "siete condiciones de §6.4 satisfechas");

        string json = BalanceRegistry.Serialize(entry);
        var restored = BalanceRegistry.Deserialize(json);

        Assert.NotNull(restored);
        Assert.Equal(entry.PerkId, restored!.PerkId);
        Assert.Equal(entry.State, restored.State);
        Assert.Equal(entry.ValuesTried, restored.ValuesTried);
        Assert.Single(restored.Candidates);
        Assert.Equal(24.0, restored.Candidates[0].Value);
        Assert.Equal(9.5, restored.Candidates[0].ArmedMetrics["tacklesPerMatch"]);
    }

    [Fact]
    public void FileRoundTripSupportsResumption()
    {
        string path = Path.Combine(Path.GetTempPath(), $"balance-registry-test-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Null(BalanceRegistryFile.Load(path)); // §9.1: sin registro previo, se empieza de cero

            var entry = new BalanceRegistryEntry(
                "test_fixture", "2026-09-18", DateTimeOffset.UtcNow.ToString("o"), BalanceState.Screening,
                nameof(PerkBalanceCategory.ProbabilityBonus), nameof(MetricReadiness.Ready),
                "tacklesPerMatch", Array.Empty<double>(), Array.Empty<BalanceCandidateRecord>());

            BalanceRegistryFile.Save(path, entry);
            var resumed = BalanceRegistryFile.Load(path);

            Assert.NotNull(resumed);
            Assert.Equal(BalanceState.Screening, resumed!.State); // reanuda exactamente donde quedó (§9.1)
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
