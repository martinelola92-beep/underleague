using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Fase B (§26): las tres mediciones descriptivas congeladas en
/// <c>docs/analisis/fase-b-preguntas-congeladas.md</c> (commit e582ab0), ejecutadas DESPUÉS de congelar.
/// No modifica umbrales, circuito, <c>PopulationFitness</c>, la predicción de la Fase A, <c>/data</c> ni
/// ningún perk. Puramente descriptiva.
/// </summary>
public sealed class FaseBMeasurementTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;
    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    private readonly ITestOutputHelper _output;
    public FaseBMeasurementTests(ITestOutputHelper output) => _output = output;

    /// <summary>Familias tal como las define el fichero congelado: PLAY_START va aparte de MATCH_START.</summary>
    private static string Family(EventType trigger) => trigger switch
    {
        EventType.MatchStart or EventType.MatchEnd => "una-vez-por-partido",
        EventType.PlayStart => "por-jugada",
        _ => "de-suceso",
    };

    // ------------------------------------------------------------------------------------------------
    // Q1 — censo estático de familias sobre los 94.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void Q1_FamilyCensusOverTheWholeCatalog()
    {
        var byFamily = Catalog.Perks.All
            .GroupBy(p => Family(p.Trigger))
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"total perks: {Catalog.Perks.All.Count}");
        foreach (var group in byFamily)
        {
            _output.WriteLine($"{group.Key}: {group.Count()} ({100.0 * group.Count() / Catalog.Perks.All.Count:F1}%)");
        }

        _output.WriteLine("");
        _output.WriteLine("desglose por disparador concreto:");
        foreach (var group in Catalog.Perks.All.GroupBy(p => p.Trigger).OrderByDescending(g => g.Count()))
        {
            _output.WriteLine($"  {group.Key,-18} {group.Count(),3}  [{Family(group.Key)}]");
        }

        // Comparación con la clasificación que YA existe en el protocolo, que agrupa las dos primeras.
        int alwaysOnceEnProtocolo = Catalog.Perks.All.Count(p => PerkAudit.ClassifyTriggerFrequency(p.Trigger) == TriggerFrequencyCategory.AlwaysOnce);
        int unaVezPorPartido = Catalog.Perks.All.Count(p => Family(p.Trigger) == "una-vez-por-partido");
        _output.WriteLine("");
        _output.WriteLine($"PerkAudit.ClassifyTriggerFrequency=AlwaysOnce agrupa {alwaysOnceEnProtocolo}; de esos, {unaVezPorPartido} son de verdad una-vez-por-partido.");
    }

    // ------------------------------------------------------------------------------------------------
    // Q2 — delta dentro del submuestreo cualificado, Human (casi nadie cualifica) vs Dwarf (~80%).
    // Q3 — el mismo caso sobre Human con 20 frente a 120 plantillas.
    // ------------------------------------------------------------------------------------------------

    private sealed record Qualified(
        int TotalMatches, int QualifiedMatches, double ExposurePercent,
        double ArmedMean, double ControlMean, double Delta, double StandardError, bool PowerSufficient);

    /// <summary>
    /// Mide el delta de la métrica primaria SOLO en los partidos cualificados (≥1 activación), contra sus
    /// partidos de control emparejados — mismas plantillas, mismas semillas, nunca contra otros.
    /// </summary>
    private static Qualified MeasureQualifiedSubsample(PerkDefinition perk, string metric, Race race, int rosters)
    {
        var config = new SimConfig(CollectLog: false, Trace: false);
        var armedQualified = new List<double>();
        var controlPaired = new List<double>();
        int total = 0, qualified = 0;

        for (int roster = 0; roster < rosters; roster++)
        {
            var homeRng = RngStreams.Generation(1, roster);
            var awayRng = RngStreams.Generation(1, 10_000 + roster);
            var home = TeamGenerator.Generate(ref homeRng, Catalog, "home", race, Quality, 1, Level);
            var away = TeamGenerator.Generate(ref awayRng, Catalog, "away", race, Quality, 100001, Level);

            int slot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
            if (slot < 0)
            {
                continue;
            }

            var armedPlayers = home.Players.ToList();
            armedPlayers[slot] = armedPlayers[slot] with { Perks = new[] { perk.Id } };
            var armedHome = home with { Players = armedPlayers };
            int carrierId = armedPlayers[slot].Id;

            for (int direction = 0; direction < 2; direction++)
            {
                ulong seed = RngStreams.MatchSeed(1, (roster * 2) + direction);
                bool away0 = direction == 1;

                var armedSetup = away0 ? new MatchSetup(away, armedHome, Referee) : new MatchSetup(armedHome, away, Referee);
                var armed = Simulator.Run(armedSetup, seed, Catalog, config);
                total++;

                long activations = armed.Report.PerksSummary
                    .Where(s => string.Equals(s.PerkId, perk.Id, StringComparison.Ordinal) && s.OwnerId == carrierId)
                    .Sum(s => s.Activations);
                if (activations == 0)
                {
                    continue;
                }

                var controlSetup = away0 ? new MatchSetup(away, home, Referee) : new MatchSetup(home, away, Referee);
                var control = Simulator.Run(controlSetup, seed, Catalog, config);

                var armedSummary = MatchSummary.FromReport(armed.Report, "home", "away");
                var controlSummary = MatchSummary.FromReport(control.Report, "home", "away");
                if (PrimaryMetricPerMatch.Value(metric, armedSummary) is not { } a
                    || PrimaryMetricPerMatch.Value(metric, controlSummary) is not { } c)
                {
                    continue;
                }

                qualified++;
                armedQualified.Add(a);
                controlPaired.Add(c);
            }
        }

        if (armedQualified.Count < 2)
        {
            return new Qualified(total, qualified, total == 0 ? 0 : 100.0 * qualified / total, 0, 0, 0, double.NaN, false);
        }

        double armedMean = armedQualified.Average();
        double controlMean = controlPaired.Average();
        double delta = armedMean - controlMean;
        double varArmed = BalancePowerCheck.SampleVariance(armedQualified);
        double varControl = BalancePowerCheck.SampleVariance(controlPaired);
        double se = BalancePowerCheck.StandardError(varArmed, armedQualified.Count, varControl, controlPaired.Count);
        bool power = BalancePowerCheck.HasSufficientPower(delta, varArmed, armedQualified.Count, varControl, controlPaired.Count);

        return new Qualified(total, qualified, 100.0 * qualified / total, armedMean, controlMean, delta, se, power);
    }

    [Fact]
    public void Q2_and_Q3_QualifiedSubsampleForBulwarkStance()
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == "bulwark_stance");
        string metric = PerkBalanceClassifier.Classify(perk).PrimaryMetric;
        _output.WriteLine($"bulwark_stance | métrica primaria: {metric} | condición: {perk.Condition}");
        _output.WriteLine("");
        _output.WriteLine("caso | partidos | cualificados | exposición | media armada | media control | delta | error estándar | potencia");

        foreach (var (label, race, rosters) in new[]
        {
            ("Q3: Human, 20 plantillas", Race.Human, 20),
            ("Q3: Human, 120 plantillas", Race.Human, 120),
            ("Q2: Dwarf, 120 plantillas", Race.Dwarf, 120),
        })
        {
            var q = MeasureQualifiedSubsample(perk, metric, race, rosters);
            _output.WriteLine(
                $"{label} | {q.TotalMatches} | {q.QualifiedMatches} | {q.ExposurePercent:F1}% | " +
                $"{q.ArmedMean:F3} | {q.ControlMean:F3} | {q.Delta:+0.000;-0.000;0.000} | {q.StandardError:F3} | {q.PowerSufficient}");
        }
    }
}
