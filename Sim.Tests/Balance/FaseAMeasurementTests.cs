using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Balance;

/// <summary>
/// Fase A de §22.8: MEDICIÓN contra la predicción congelada en
/// <c>docs/analisis/fase-a-prediccion-congelada.md</c> (commit e152253). El analizador NO se toca durante
/// ni después de esta medición: si aparecen discrepancias, son el resultado, no un motivo para reajustar.
///
/// <para><b>Sobre el circuito de lote</b>: aquí se llama a <c>ScreeningRunner.RunPerk</c> perk a perk, NO a
/// <c>RunBatch</c>. No se está saltando el circuito del 20% — el lote de §20.5 sigue detenido en 5/24 y
/// ningún perk cambia de estado de balance por lo que salga aquí. Lo que se mide es la capacidad
/// predictiva del analizador, no el balance de ningún perk.</para>
/// </summary>
public sealed class FaseAMeasurementTests
{
    private static readonly Catalog Catalog = TestData.LoadCatalog();
    private const int Rosters = 20;
    private const ulong Seed = 1;

    private readonly ITestOutputHelper _output;
    public FaseAMeasurementTests(ITestOutputHelper output) => _output = output;

    private static IReadOnlyList<string> HoldOutReadyPerkIds() => PerkAudit
        .AuditCatalog(Catalog.Perks.All, Catalog)
        .Where(e => e.FinalReadiness == AuditReadiness.ReadyForScreening)
        .Select(e => e.PerkId)
        .Where(id => id is not ("back_to_back" or "bulwark_stance" or "shadow_marker" or "cannon")) // conjunto de hipótesis
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList();

    // ------------------------------------------------------------------------------------------------
    // Prueba 1 — ESPECIFICIDAD DE RAZA. La predicción congelada dice: de los 20 ReadyForScreening del
    // hold-out, la raza no debería importar en NINGUNO; y en todo el hold-out solo debería importar en
    // pack_mentality. Se mide sobre TODAS las razas del catálogo y se mira la dispersión.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void RaceSpecificity_HoldOutReadyPerksPlusTheSinglePredictedPositive()
    {
        var ids = HoldOutReadyPerkIds().Concat(new[] { "pack_mentality" }).ToList();
        _output.WriteLine($"razas del catálogo, en orden de carga: {string.Join(", ", Catalog.Races.Select(r => r.Id))}");
        _output.WriteLine("perk | pred_raza_importa | raza_afin_predicha | exposicion por raza (etiquetada) | min | max | dispersion | raza_de_max | veredicto");

        int agree = 0, total = 0;
        foreach (var id in ids)
        {
            var perk = Catalog.Perks.All.Single(p => p.Id == id);
            var fitness = PopulationFitness.Analyze(perk, Catalog, perk.Race ?? Race.Human, null);
            bool predictedRaceMatters = fitness.PopulationCheck == PopulationVerdict.WrongPopulation;

            var byRace = new List<(Race Race, double Exposure, int Matches)>();
            foreach (var race in Catalog.Races.Select(r => r.Id))
            {
                // Un perk con raza propia (iron_gate) solo puede medirse en la suya: no es una discrepancia.
                if (perk.Race is { } fixedRace && fixedRace != race)
                {
                    continue;
                }

                var probe = RaceExposureProbe.Measure(Catalog, perk, race, Rosters, Seed);
                byRace.Add((race, probe.ExposurePercent, probe.Matches));
            }

            var measured = byRace.Where(b => b.Matches > 0).ToList();
            double min = measured.Count == 0 ? 0 : measured.Min(b => b.Exposure);
            double max = measured.Count == 0 ? 0 : measured.Max(b => b.Exposure);
            double spread = max - min;

            // Criterio fijado ANTES de mirar: "la raza importa" = la dispersión entre razas supera 25
            // puntos de exposición (la mitad del suelo del 50%). No es un umbral del protocolo, es solo
            // cómo se lee esta comparación.
            bool observedRaceMatters = spread > 25.0;
            bool matches = observedRaceMatters == predictedRaceMatters;
            if (matches)
            {
                agree++;
            }

            total++;

            string detail = string.Join(" ", byRace.Select(b => b.Matches == 0 ? $"{b.Race}=n/a" : $"{b.Race}={b.Exposure:F0}"));
            var raceOfMax = measured.Count == 0 ? "-" : measured.OrderByDescending(b => b.Exposure).First().Race.ToString();
            _output.WriteLine($"{id} | {(predictedRaceMatters ? "SI" : "NO")} | {fitness.AffineRace?.ToString() ?? "-"} | {detail} | {min:F0} | {max:F0} | {spread:F0} | {raceOfMax} | {(matches ? "COINCIDE" : "DISCREPA")}");
        }

        _output.WriteLine("");
        _output.WriteLine($"coincidencias predicción/observación: {agree}/{total}");
    }

    // ------------------------------------------------------------------------------------------------
    // Prueba 2 — EJE DE POSICIÓN. double_shot es el caso explícito del hold-out (predicho WrongPosition,
    // acción Shoot, medido sobre Defensa). Los "Adequate" sirven de control negativo.
    // ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("double_shot", Position.Forward)]      // predicho WrongPosition -> debería mejorar en Delantero
    [InlineData("own_third_anchor", Position.Defender)] // predicho Adequate (eje de EFECTO) -> ya está en su rol
    [InlineData("last_ditch", Position.Defender)]       // predicho Adequate -> control negativo
    // `steamroller` estaba aquí con Position.Defender, predicho cuando su disparador era TACKLE.
    // Al cerrar BB-Q pasó a RECOVERY, y `PopulationFitness.ClassifyTrigger` —congelado en e152253, y que
    // solo mapea sitios de emisión verificados— ya no infiere rol para él. Se retira el caso en vez de
    // tocar cualquiera de los dos lados: ni el analizador (congelado) ni la predicción (congelada en
    // docs/analisis/fase-a-prediccion-congelada.md). La predicción no se ha falsado; el perk sobre el que
    // se hizo dejó de existir con esa forma. Ver docs/pendientes/BB-Q.md.
    public void PositionAxis_MeasureOnEveryRoleAndCompareAgainstThePrediction(string perkId, Position predictedRole)
    {
        var perk = Catalog.Perks.All.Single(p => p.Id == perkId);
        var race = perk.Race ?? Race.Human;
        var fitness = PopulationFitness.Analyze(perk, Catalog, race, null);

        _output.WriteLine($"{perkId}: predicción rol requerido={fitness.RequiredRole}, vía disparador={fitness.RequiredAction?.ToString() ?? "-"}, vía efecto={fitness.EffectAction?.ToString() ?? "-"}");
        Assert.Equal(predictedRole, fitness.RequiredRole);

        foreach (var role in Enum.GetValues<Position>())
        {
            var probe = RaceExposureProbe.Measure(Catalog, perk, race, Rosters, Seed, forcedRole: role);
            string marker = role == predictedRole ? "  <- rol predicho" : "";
            _output.WriteLine($"  {role,-11}: exposición {probe.ExposurePercent,5:F1}% ({probe.MatchesWithActivation}/{probe.Matches} partidos, {probe.Activations} activaciones){marker}");
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Prueba 3 — todas las columnas que pide la Fase A, por perk, con el screening real.
    // ------------------------------------------------------------------------------------------------

    [Fact]
    public void FullScreeningColumnsForEveryHoldOutReadyPerk()
    {
        _output.WriteLine("perk | pred_poblacion | pred_pos_disparador | pred_pos_efecto | rol_carrier | exposicion | activaciones/partidos | delta | estado_screening | prediccion_si_baja");

        foreach (var id in HoldOutReadyPerkIds())
        {
            var perk = Catalog.Perks.All.Single(p => p.Id == id);
            var race = perk.Race ?? Race.Human;
            var probe = RaceExposureProbe.Measure(Catalog, perk, race, Rosters, Seed);
            var fitness = PopulationFitness.Analyze(perk, Catalog, race, probe.CarrierRole);
            var screening = ScreeningRunner.RunPerk(Catalog, perk, Seed);

            string delta = screening.PrimaryDelta is { } d ? $"{d:F4}" : "-";
            string exposure = screening.ExposureFraction is { } e ? $"{e:P1}" : "-";

            _output.WriteLine(string.Join(" | ", new[]
            {
                id,
                fitness.PopulationCheck.ToString(),
                fitness.RequiredAction?.ToString() ?? "-",
                fitness.EffectAction?.ToString() ?? "-",
                probe.CarrierRole?.ToString() ?? "-",
                exposure,
                $"{probe.Activations}/{probe.Matches}",
                delta,
                screening.DisplayState,
                fitness.Diagnose().ToString(),
            }));
        }
    }
}
