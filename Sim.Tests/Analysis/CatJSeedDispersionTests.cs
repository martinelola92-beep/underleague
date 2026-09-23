using Underleague.Sim.Analysis;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// CAT-J: ¿las tres puertas rojas de <see cref="BuildGateTests"/> lo están por balance o por ruido de una
/// sola semilla? La puerta mide con <c>Seed = 1</c> y 480 partidos por celda, y su propia documentación
/// dice que el error típico de una tasa de victoria ahí es de <b>2,3 puntos</b>. Con eso:
///
/// <list type="bullet">
/// <item><c>badBuildsLoseToNone_elf_brawler</c> = 48,12 contra un tope de 45,00 → se pasa por 3,12, que
/// son <b>1,36 errores típicos</b>.</item>
/// <item><c>buildsWinDifferently_passChain</c> = 1,10 contra un mínimo de 1,11 → se queda corto por
/// <b>0,01</b>.</item>
/// </list>
///
/// <para>Es el mismo perfil que tenía la puerta de equipamiento antes de la ADR 0118. Este test NO es una
/// puerta: mide la dispersión entre semillas para decidir con datos, y se borra o se convierte en otra
/// cosa cuando CAT-J se cierre.</para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class CatJSeedDispersionTests
{
    private static readonly ulong[] SeedBases = { 1, 2, 3, 4, 5, 6, 7, 8 };

    private readonly ITestOutputHelper _output;
    public CatJSeedDispersionTests(ITestOutputHelper output) => _output = output;

    [Fact(Skip = "Diagnóstico bajo demanda: ocho pasadas completas de la puerta, ~1 m 50 s. Quita el Skip para remedir CAT-J.")]
    public void HowMuchDoTheRedGateMetricsMoveBetweenSeeds()
    {
        string[] watched =
        {
            "buildsWinDifferently_injuries",
            "badBuildsLoseToNone_elf_brawler",
            "badBuildsLoseToNone_elf_out_of_zone",
            "buildsWinDifferently_passChain",
            "badBuildsLoseToNone_orc_misplaced",
            "coherentBuildsBeatNone_orc_violence",
        };

        var byMetric = watched.ToDictionary(m => m, _ => new List<double>(), StringComparer.Ordinal);
        var ranges = new Dictionary<string, (double? Min, double? Max)>(StringComparer.Ordinal);

        foreach (ulong seed in SeedBases)
        {
            var metrics = BuildGateTests.MetricsWithSeed(seed);
            foreach (string name in watched)
            {
                var metric = metrics.FirstOrDefault(m => m.Name == name);
                if (metric is null)
                {
                    continue;
                }

                byMetric[name].Add(metric.Value);
                ranges[name] = (metric.RangeMin, metric.RangeMax);
            }
        }

        foreach (string name in watched)
        {
            var values = byMetric[name];
            if (values.Count == 0)
            {
                _output.WriteLine($"{name}: NO EXISTE con ese nombre");
                continue;
            }

            double mean = values.Average();
            double sd = values.Count > 1
                ? Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1))
                : 0.0;
            var (min, max) = ranges[name];
            int outside = values.Count(v => (min is { } lo && v < lo) || (max is { } hi && v > hi));

            _output.WriteLine("");
            _output.WriteLine($"=== {name} ===");
            _output.WriteLine($"  rango exigido: [{min?.ToString("F2") ?? "-"} , {max?.ToString("F2") ?? "-"}]");
            _output.WriteLine($"  valores: {string.Join(" · ", values.Select(v => v.ToString("F2")))}");
            _output.WriteLine($"  media {mean:F2} · sd {sd:F2} · error típico de la media {sd / Math.Sqrt(values.Count):F2}");
            _output.WriteLine($"  FUERA DE RANGO en {outside} de {values.Count} semillas");
        }

        Assert.NotEmpty(byMetric["badBuildsLoseToNone_elf_brawler"]);
    }
}
