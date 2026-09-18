using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// §35 (Parte B): las ÚNICAS mediciones nuevas que el informe de decisión del catálogo necesita — las que
/// distinguen una causa de otra. Todo lo demás sale de §33/§34, ya medido.
///
/// <para>La pregunta que estas sondas contestan es siempre la misma: <b>¿la exposición baja (o el efecto
/// nulo) es del perk, o de la población sobre la que lo estamos midiendo?</b> Si al cambiar la raza o el
/// puesto del portador la exposición sube mucho, el perk no es raro: el banco de pruebas es el
/// equivocado. Si no sube en ninguna población, la rareza es del perk.</para>
///
/// <para>No cambia umbrales, ni el circuito, ni <c>/data</c>, ni ninguna métrica del cribado.</para>
/// </summary>
public sealed class CatalogDecisionProbeTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Rosters = 20;
    private const ulong Seed = 1;

    private static readonly Race[] Races = { Race.Human, Race.Orc, Race.Elf, Race.Dwarf, Race.Undead };
    private static readonly Position[] Roles = { Position.Goalkeeper, Position.Defender, Position.Midfielder, Position.Forward };

    private readonly ITestOutputHelper _output;
    public CatalogDecisionProbeTests(ITestOutputHelper output) => _output = output;

    /// <summary>Puesto real al que cae cada uno de los 24 — dato de ficha, sin simular nada.</summary>
    [Fact]
    public void WhichCarrierDoesEachOfTheTwentyFourActuallyLandOn()
    {
        foreach (var perk in ReadyForScreening())
        {
            var rng = RngStreams.Generation(Seed, 0);
            var home = TeamGenerator.Generate(ref rng, Catalog, "home", perk.Race ?? Race.Human, 50, 1, 4);
            int slot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
            string carrier = slot < 0 ? "NINGUNO" : $"slot {slot} = {home.Players[slot].Position}";
            _output.WriteLine(
                $"{perk.Id,-20} | {carrier,-24} | trigger={perk.Trigger,-10} | positionOnly={perk.PositionOnly?.ToString() ?? "-",-12} | " +
                $"race={perk.Race?.ToString() ?? "-",-6} | tags=[{string.Join(",", perk.TagsRequired)}] | cond=\"{perk.Condition}\"");
        }
    }

    /// <summary>
    /// Para cada perk que no llegó al 50 %: ¿existe alguna población (raza × puesto) donde sí se exponga?
    /// Discrimina "perk raro" de "banco de pruebas equivocado".
    /// </summary>
    [Fact]
    public void DoesAnyPopulationExposeThePerksThatFellShort()
    {
        // shadow_marker estaba en este lote de baja exposición y se borró del catálogo (revisor, 18 sep
        // 2026); se retira de la lista sin sustituto — la pregunta que responde este test es sobre ESTE
        // perk concreto, no sobre "un perk con nearAlly('Brute',2)" en abstracto.
        string[] lowExposure =
        {
            "steamroller", "double_shot", "iron_price", "bulwark_stance", "back_to_back",
            "grudge", "second_wound", "iron_gate", "game_management",
        };

        foreach (string id in lowExposure)
        {
            var perk = Catalog.Perks.All.Single(p => p.Id == id);
            double baseline = RaceExposureProbe.Measure(Catalog, perk, perk.Race ?? Race.Human, Rosters, Seed).ExposurePercent;

            double best = baseline;
            string bestWhere = $"{perk.Race ?? Race.Human}/por defecto";
            foreach (var race in Races)
            {
                foreach (var role in Roles)
                {
                    if (perk.PositionOnly is { } only && only != role)
                    {
                        continue;
                    }

                    RaceExposureProbe.Probe probe;
                    try
                    {
                        probe = RaceExposureProbe.Measure(Catalog, perk, race, Rosters, Seed, forcedRole: role);
                    }
                    catch (ArgumentException)
                    {
                        // El perk exige una etiqueta que esa raza no puede llevar (p. ej. iron_gate/Dwarf):
                        // población no válida, no un resultado. Se salta sin contarla.
                        continue;
                    }

                    if (probe.Matches > 0 && probe.ExposurePercent > best)
                    {
                        best = probe.ExposurePercent;
                        bestWhere = $"{race}/{role}";
                    }
                }
            }

            _output.WriteLine(
                $"{id,-18} por defecto {baseline,6:F1}%  ->  mejor población {best,6:F1}% en {bestWhere,-22} " +
                $"| factor {(baseline <= 0.01 ? "inf" : $"{best / baseline:F1}x")}");
        }
    }

    /// <summary>
    /// `cannon` y `double_shot`: ¿el mecanismo es malo, o el portador no puede manifestarlo? Los dos
    /// dependen de que el portador DISPARE, y los dos caen en un Defensa por no declarar `positionOnly`.
    /// Se compara el mismo perk sobre Defensa y sobre Delantero, con el mismo arnés emparejado.
    /// </summary>
    [Theory]
    [InlineData("cannon", MatchMetrics.ShotsPerMatch)]
    [InlineData("double_shot", MatchMetrics.ShotsPerMatch)]
    public void IsItThePerkOrTheCarrier(string perkId, string metric)
    {
        foreach (var role in new[] { Position.Defender, Position.Midfielder, Position.Forward })
        {
            var run = PairedBalanceHarness.Run(Catalog, perkId, role, Rosters, Seed);
            if (run.ArmedMatches.Count == 0)
            {
                _output.WriteLine($"{perkId,-12} {role,-11} | sin portador");
                continue;
            }

            double armed = MatchMetrics.Compute(run.ArmedMatches, Array.Empty<MetricPairing>()).Single(m => m.Name == metric).Value;
            double control = MatchMetrics.Compute(run.ControlMatches, Array.Empty<MetricPairing>()).Single(m => m.Name == metric).Value;
            double exposure = run.ArmedActivationsPerMatch.Count == 0
                ? 0.0
                : 100.0 * run.ArmedActivationsPerMatch.Count(a => a > 0) / run.ArmedActivationsPerMatch.Count;

            _output.WriteLine(
                $"{perkId,-12} {role,-11} | exposición {exposure,6:F1}% | {metric} armado {armed,7:F3} control {control,7:F3} " +
                $"delta {armed - control,+7:F3} | {run.ArmedMatches.Count} partidos/brazo");
        }
    }

    private static IReadOnlyList<PerkDefinition> ReadyForScreening()
    {
        var ids = PerkAudit.AuditCatalog(Catalog.Perks.All, Catalog)
            .Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening)
            .Select(e => e.PerkId)
            .ToHashSet(StringComparer.Ordinal);
        return Catalog.Perks.All.Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }
}
