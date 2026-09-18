using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Fase A de §22.8: emite la PREDICCIÓN del analizador para todo el catálogo, marcando cuáles de los 94
/// perks se usaron para construir la hipótesis (§21) y cuáles no. La salida de este test es lo que se
/// congela en <c>docs/analisis/fase-a-prediccion-congelada.md</c> y se commitea ANTES de medir nada:
/// comparar después contra esa predicción es la única forma de que el resultado no sea circular.
///
/// <para>No simula ningún partido: solo genera plantillas (para saber qué rol elegiría el harness) y
/// consulta el analizador puro.</para>
/// </summary>
public sealed class FaseAPredictionTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Quality = 50;
    private const int Level = 4;

    /// <summary>Los 13 perks que YA se midieron en §21 y de los que salió la hipótesis: no valen como prueba.</summary>
    private static readonly HashSet<string> HypothesisSet = new(StringComparer.Ordinal)
    {
        "back_to_back", "bulwark_stance", "shadow_marker", "safety_net", "cannon",
        "bruised_knuckles", "brute_boots", "cold_focus", "fine_touch", "crowd_control",
        "blood_tithe", "fine_orchestra", "first_touch_school",
    };

    private readonly ITestOutputHelper _output;
    public FaseAPredictionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EmitFrozenPredictionForTheWholeCatalog()
    {
        var entries = PerkAudit.AuditCatalog(Catalog.Perks.All, Catalog);
        var readyIds = entries
            .Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening)
            .Select(e => e.PerkId)
            .ToHashSet(StringComparer.Ordinal);

        _output.WriteLine("perk | en_hipotesis | ready | raza_medida | rol_medido | poblacion | posicion | estilo | raza_afin | accion | rol_requerido | prediccion_si_exposicion_baja | cambiar_raza_deberia_subir_exposicion");

        foreach (var perk in Catalog.Perks.All.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var race = perk.Race ?? Race.Human;
            var role = CarrierRoleTheHarnessWouldUse(perk.Id, race);
            var fitness = PopulationFitness.Analyze(perk, Catalog, race, role);

            // La predicción falsable: ¿cambiar SOLO la raza debería mover la exposición?
            string raceMatters = fitness.PopulationCheck == PopulationVerdict.WrongPopulation ? "SI" : "NO";

            _output.WriteLine(string.Join(" | ", new[]
            {
                perk.Id,
                HypothesisSet.Contains(perk.Id) ? "si" : "no",
                readyIds.Contains(perk.Id) ? "si" : "no",
                race.ToString(),
                role?.ToString() ?? "-",
                fitness.PopulationCheck.ToString(),
                fitness.PositionCheck.ToString(),
                fitness.RequiredStyle?.ToString() ?? "-",
                fitness.AffineRace?.ToString() ?? "-",
                fitness.RequiredAction?.ToString() ?? fitness.EffectAction?.ToString() ?? "-",
                fitness.RequiredRole?.ToString() ?? "-",
                fitness.Diagnose().ToString(),
                raceMatters,
            }));
        }

        _output.WriteLine("");
        _output.WriteLine("=== resumen, solo fuera del conjunto de hipótesis (lo que de verdad prueba algo) ===");
        var holdout = Catalog.Perks.All.Where(p => !HypothesisSet.Contains(p.Id)).ToList();
        foreach (var group in holdout
            .Select(p => PopulationFitness.Analyze(p, Catalog, p.Race ?? Race.Human, CarrierRoleTheHarnessWouldUse(p.Id, p.Race ?? Race.Human)).Diagnose())
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count()))
        {
            _output.WriteLine($"{group.Key}: {group.Count()}/{holdout.Count}");
        }

        // La predicción de Fase A se congeló sobre los 94 perks del 19 sep 2026
        // (docs/analisis/fase-a-prediccion-congelada.md). El catálogo ha crecido desde entonces al cuadrar
        // razas y rasgos, y los perks nuevos quedan FUERA de la predicción: ni se añaden a ella ni se
        // rehace, porque una predicción que se reescribe al ver datos nuevos deja de serlo. Lo que se fija
        // aquí es que los 94 originales siguen todos en el catálogo.
        Assert.True(Catalog.Perks.All.Count >= 94, "el catálogo no puede encoger por debajo de la predicción congelada");
    }

    /// <summary>Rol que el harness elegiría hoy (misma función que usa el screening real), sobre la plantilla 0.</summary>
    private static Position? CarrierRoleTheHarnessWouldUse(string perkId, Race race)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var rng = RngStreams.Generation(1, 0);
        var home = TeamGenerator.Generate(ref rng, Catalog, "home", race, Quality, 1, Level);
        int slot = PairedBalanceHarness.FindEligibleCarrierSlot(home, perk, Catalog);
        return slot < 0 ? null : home.Players[slot].Position;
    }
}
