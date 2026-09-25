using System.Globalization;
using System.Text;
using Underleague.Sim.Analysis;
using Xunit.Abstractions;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// BI-D / ADR 0137: la conducción con duración dejó <c>coherentBuildsBeatNone_orc_violence</c> por debajo
/// de su mínimo en las cinco dosis medidas, <b>sin explicación</b>, y la hipótesis del derribo del duelo
/// se midió y salió REJECTED. Para explicarlo hace falta ver las <b>catorce celdas</b> con y sin
/// conducción, no solo las que estaban rojas: si bajan todas las coherentes es un efecto de canal, y si
/// baja solo la física es la mecánica de contacto.
///
/// <para>Vuelca las métricas de <see cref="BuildGateTests"/> promediadas sobre las ocho bases de semilla
/// —exactamente lo que promedia la puerta desde la ADR 0131— a un CSV, para poder restar dos árboles con
/// <c>data/sim/tuning.json</c> distintos (la ruta de <c>/data</c> la resuelve <c>TestData</c> subiendo
/// directorios, así que la variante se mide en un <c>git worktree</c> aparte, no editando el árbol vivo
/// mientras algo corre).</para>
///
/// <para>NO es una puerta y no afirma nada por sí mismo: es el instrumento. El destino sale de
/// <c>UNDERLEAGUE_GATE_DUMP</c>, o <c>out/gates/gate-metrics.csv</c> si no está.</para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class DribbleGateContrastTests
{
    private static readonly ulong[] SeedBases = { 1, 2, 3, 4, 5, 6, 7, 8 };

    private readonly ITestOutputHelper _output;
    public DribbleGateContrastTests(ITestOutputHelper output) => _output = output;

    [Fact(Skip = "Diagnóstico bajo demanda (BI-D): ocho pasadas completas de la puerta, ~2 m 30 s. Quita el Skip para volcar el CSV de las catorce celdas con su valor por semilla.")]
    public void DumpEveryGateMetricAveragedOverTheEightSeeds()
    {
        var sums = new Dictionary<string, (double Sum, int Count, double? Min, double? Max)>(StringComparer.Ordinal);
        var perSeed = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (ulong seed in SeedBases)
        {
            foreach (var metric in BuildGateTests.MetricsWithSeed(seed))
            {
                if (!sums.TryGetValue(metric.Name, out var acc))
                {
                    order.Add(metric.Name);
                    perSeed[metric.Name] = new List<double>();
                    acc = (0.0, 0, metric.RangeMin, metric.RangeMax);
                }

                sums[metric.Name] = (acc.Sum + metric.Value, acc.Count + 1, acc.Min, acc.Max);
                perSeed[metric.Name].Add(metric.Value);
            }
        }

        // La media es lo que juzga la puerta; los valores por semilla son lo que permite decir si una
        // diferencia entre dos árboles es señal o muestreo, que es la lección de la ADR 0118 y de CAT-J.
        var csv = new StringBuilder("metric,value,min,max," + string.Join(',', SeedBases.Select(s => "s" + s)) + "\n");
        foreach (string name in order)
        {
            var (sum, count, min, max) = sums[name];
            double mean = count > 0 ? sum / count : 0.0;
            csv.Append(name).Append(',')
               .Append(mean.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
               .Append(min?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
               .Append(max?.ToString("F4", CultureInfo.InvariantCulture) ?? string.Empty);
            foreach (double v in perSeed[name])
            {
                csv.Append(',').Append(v.ToString("F4", CultureInfo.InvariantCulture));
            }

            csv.Append('\n');
        }

        string path = Environment.GetEnvironmentVariable("UNDERLEAGUE_GATE_DUMP")
            ?? Path.Combine(TestData.DataDirectory, "..", "out", "gates", "gate-metrics.csv");
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, csv.ToString());
        _output.WriteLine($"{order.Count} métricas escritas en {full}");
    }
}
