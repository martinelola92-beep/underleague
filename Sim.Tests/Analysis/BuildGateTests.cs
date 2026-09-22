using System.Text.Json;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Generation;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Random;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Puerta de salida de la fase 1 (docs/fase1-diseno.md §8): las builds coherentes de
/// <c>data/balance/groups.json</c> ganan a la referencia sin perks de su raza, las malas pierden, la
/// aleatoria queda cerca del 50%, las dos builds de estilo ganan de formas distintas, ningún perk del
/// catálogo está muerto y la distribución RF-069 se respeta. Marcada <c>Category=Gate</c> como la de
/// fase 0.
///
/// <para>
/// <b>Metodología (paquete I).</b> Cada celda enfrenta a la build con su referencia usando la
/// <b>misma plantilla generada</b>: los dos equipos son los mismos jugadores con los mismos atributos y
/// rasgos, y lo único que cambia son los perks, las rarezas y la alineación. Es imprescindible: con
/// plantillas independientes, la tasa de victoria de una misma build contra su referencia iba del 16,5%
/// al 59,5% según el dado del generador (20 plantillas × 200 partidos, desviación típica de 14,9 puntos),
/// de modo que lo que medía la puerta era la generación y no el diseño de la build.
/// </para>
/// <para>
/// Cada partido se juega en las cuatro combinaciones de (local, visitante) × (ids bajos, ids altos). Lo
/// segundo importa porque los desempates del motor van por id ascendente: con el reparto de ids fijo, el
/// equipo de ids bajos gana entre 2 y 3 puntos de más con plantillas idénticas (medido: 53,1% Human,
/// 52,2% Orc, 52,0% Elf; alternando, 50,7% / 50,5% / 49,9%).
/// </para>
/// <para>
/// Tamaño de muestra: <see cref="Rosters"/> × <see cref="MatchesPerRoster"/> = <b>480 partidos por
/// celda</b>, catorce celdas, <b>semilla <see cref="Seed"/></b>; 6.720 partidos en total y unos
/// <b>30 s</b> de ejecución (paquete U; antes eran 80 × 20 = 1.600 por celda y ~85 s, por encima del
/// minuto que se le pide a una puerta). Con 480 partidos y plantillas emparejadas el error típico de una
/// tasa de victoria es de 2,3 puntos, y el margen más ajustado de la medición de cierre es de 6,6 puntos
/// (<c>elf_bulwark</c> 64,6% contra el umbral de 58%), así que la puerta es estable. Los valores medidos
/// están en docs/balance/fase1b-resultados.md.
/// </para>
/// </summary>
[Trait("Category", "Gate")]
[Collection("Gate")]
public sealed class BuildGateTests
{
    /// <summary>Plantillas distintas sobre las que se promedia cada celda.</summary>
    private const int Rosters = 40;

    /// <summary>Partidos por plantilla (múltiplo de 4: local/visitante × reparto de ids).</summary>
    private const int MatchesPerRoster = 12;

    /// <summary>
    /// Las <b>ocho</b> bases de semilla que promedia la puerta (ADR 0131). Era una sola, y con una sola
    /// estas métricas miden <b>una plantilla concreta</b>: CAT-J midió dispersiones de sd 1,6 a 3,2 puntos
    /// en las de build, del orden del margen contra su propio rango. El precedente es la <b>ADR 0118</b>,
    /// que hizo justo esto con la puerta de equipar tras tres falsos positivos seguidos
    /// (<c>docs/pendientes/BB-P.md</c>): <i>subir la muestra en vez de bajar el umbral</i>.
    /// </summary>
    private static readonly ulong[] SeedBases = { 1, 2, 3, 4, 5, 6, 7, 8 };

    /// <summary>Primer id de jugador del equipo que lleva los ids bajos.</summary>
    private const int PrimaryIdBase = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids altos.</summary>
    private const int SecondaryIdBase = 100001;

    /// <summary>Build "gana por contacto" de <c>buildsWinDifferently</c> (§8).</summary>
    private const string PhysicalBuild = "orc_violence";

    /// <summary>Build "gana por técnica" de <c>buildsWinDifferently</c> (§8).</summary>
    private const string TechnicalBuild = "elf_tiki_taka";

    private static readonly Lazy<IReadOnlyList<MetricResult>> Results = new(ComputeAveraged);

    /// <summary>
    /// Las métricas de la puerta promediadas sobre <see cref="SeedBases"/>: cada métrica es la media de su
    /// valor en las ocho plantillas, y el estado IN/OUT se recalcula sobre esa media contra el mismo rango
    /// de siempre. Las filas informativas siguen siendo informativas.
    ///
    /// <para>El paralelismo vive en el arnés (CLAUDE.md): cada pasada ya reparte sus partidos con
    /// <c>Parallel.For</c> por índice y un <c>Catalog</c> por hilo; las ocho pasadas van en serie entre sí
    /// y la reducción es después, en orden.</para>
    /// </summary>
    private static IReadOnlyList<MetricResult> ComputeAveraged()
    {
        var passes = new IReadOnlyList<MetricResult>[SeedBases.Length];
        for (int i = 0; i < SeedBases.Length; i++)
        {
            passes[i] = Compute(SeedBases[i]);
        }

        var averaged = new List<MetricResult>(passes[0].Count);
        foreach (var first in passes[0])
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < passes.Length; i++)
            {
                var row = passes[i].SingleOrDefault(r => string.Equals(r.Name, first.Name, StringComparison.Ordinal));
                if (row is null)
                {
                    continue;
                }

                sum += row.Value;
                count++;
            }

            double mean = count > 0 ? sum / count : first.Value;
            string status = first.Status == "INFO"
                ? "INFO"
                : (first.RangeMin is double min && mean < min) || (first.RangeMax is double max && mean > max) ? "OUT" : "IN";
            averaged.Add(new MetricResult(first.Name, mean, first.RangeMin, first.RangeMax, status));
        }

        return averaged;
    }

    /// <summary>
    /// Las métricas de la puerta con una semilla base cualquiera. Existe para poder MEDIR la dispersión
    /// de estas métricas entre semillas (CAT-J): la puerta usa <see cref="Seed"/> y una sola, igual que
    /// hacía la de equipamiento antes de la ADR 0118, y hay dos métricas cuyo margen contra su rango es
    /// del orden de su propio error de muestreo. No cambia lo que mide la puerta: con
    /// <see cref="Seed"/> devuelve exactamente lo de siempre.
    /// </summary>
    internal static IReadOnlyList<MetricResult> MetricsWithSeed(ulong seed) => Compute(seed);

    /// <summary>§8: cada build coherente gana ≥ 58% a la referencia sin perks de su raza.</summary>
    [Fact]
    public void CoherentBuildsBeatTheirBaseline() =>
        AssertAllIn(BuildMetrics.CoherentBuildsBeatNonePrefix);

    /// <summary>§8 y paquete AY: cada build mal construida a propósito queda en 45-55% contra su referencia: un perk mal puesto no hace nada, ni resta ni suma.</summary>
    [Fact]
    public void BadBuildsLoseToTheirBaseline() =>
        AssertAllIn(BuildMetrics.BadBuildsLoseToNonePrefix);

    /// <summary>§8, ADR 0078 y paquete AY: la build tomada al azar queda en 45-55% contra su referencia, como toda build mal construida.</summary>
    [Fact]
    public void RandomBuildLosesToItsBaseline() =>
        AssertAllIn(BuildMetrics.RandomBuildLosesToNonePrefix);

    /// <summary>§8: la build de contacto lesiona mucho más y la técnica encadena muchos más pases.</summary>
    [Fact]
    public void BuildsWinDifferently()
    {
        AssertIn(BuildMetrics.BuildsWinDifferentlyInjuries);
        AssertIn(BuildMetrics.BuildsWinDifferentlyPassChain);
    }

    /// <summary>§8/RF-070: ningún perk se queda por debajo del 1% de partidos con activación en las builds que lo colocan bien (las coherentes).</summary>
    [Fact]
    public void NoPerkIsDead() => AssertIn(BuildMetrics.NoDeadPerks);

    /// <summary>Todo perk del catálogo tiene que estar asignado en alguna build, o noDeadPerks no lo ve.</summary>
    [Fact]
    public void EveryCatalogPerkIsAssignedInSomeBuild()
    {
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var assigned = builds.Values.SelectMany(b => b.Perks.Select(p => p.Perk)).ToHashSet(StringComparer.Ordinal);

        // Las habilidades raciales (ADR 0026) no se asignan: las concede la raza y no ocupan slot
        // (fase1b-diseno.md §5.10), así que ninguna build puede listarlas y noDeadPerks no las mide.
        var racialAbilities = catalog.Races
            .Select(r => r.Ability)
            .Where(a => !string.IsNullOrEmpty(a))
            .ToHashSet(StringComparer.Ordinal);

        var missing = catalog.Perks.All.Select(p => p.Id)
            .Where(id => !assigned.Contains(id) && !racialAbilities.Contains(id))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "perks del catálogo que ninguna build asigna (noDeadPerks no puede verlos): " + string.Join(", ", missing));
    }

    /// <summary>RF-069: 60/30/10 ± 8 puntos.</summary>
    [Fact]
    public void CatalogDistributionFollowsRf069()
    {
        AssertIn(BuildMetrics.Rf069Filler);
        AssertIn(BuildMetrics.Rf069Conditional);
        AssertIn(BuildMetrics.Rf069RuleBreaker);
    }

    /// <summary>Ninguna métrica de la puerta puede quedar OUT: es el criterio de salida completo.</summary>
    [Fact]
    public void NoGateMetricIsOutOfRange()
    {
        var offenders = Results.Value.Where(r => r.Status == "OUT").ToList();
        Assert.True(
            offenders.Count == 0,
            "métricas de fase 1 fuera de rango: " + string.Join(", ", offenders.Select(r => $"{r.Name}={r.Value:F2}")));
    }

    private static void AssertIn(string metric)
    {
        var row = Results.Value.SingleOrDefault(r => r.Name == metric);
        Assert.NotNull(row);
        Assert.True(
            row!.Status == "IN",
            $"{row.Name} = {row.Value:F2}, fuera de {row.RangeMin?.ToString("F2") ?? "-"}..{row.RangeMax?.ToString("F2") ?? "-"}");
    }

    private static void AssertAllIn(string prefix)
    {
        var rows = Results.Value.Where(r => r.Name.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(rows);

        var offenders = rows.Where(r => r.Status != "IN").ToList();
        Assert.True(
            offenders.Count == 0,
            string.Join(", ", offenders.Select(r => $"{r.Name}={r.Value:F2} (rango {r.RangeMin?.ToString("F2") ?? "-"}..{r.RangeMax?.ToString("F2") ?? "-"})")));
    }

    private static IReadOnlyList<MetricResult> Compute(ulong seed)
    {
        // El catálogo de fuera del bucle solo sirve para la distribución RF-069 del final: los partidos
        // usan el catálogo del hilo (ver ThreadCatalogs), porque las condiciones compiladas no son
        // reentrantes.
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var groups = BuildGroupsFile.Load(TestData.DataDirectory);
        var items = Underleague.Sim.Run.Systems.Items.ItemLoader.FromJson(TestData.LoadAllFiles());

        var subjects = groups.Coherent.Concat(groups.Bad).Concat(groups.Random)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var baselines = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in subjects)
        {
            baselines[id] = groups.BaselineByRace[builds[id].Race.ToString()];
        }

        // El desplazamiento de semilla de partido de cada sujeto se fija ANTES de jugar ninguno. La
        // versión secuencial llevaba un contador que cada celda avanzaba exactamente
        // Rosters × MatchesPerRoster, y los bucles anidados iban (sujeto, plantilla, partido), así que el
        // índice global del partido k de la plantilla r del sujeto s es
        // s × Rosters × MatchesPerRoster + r × MatchesPerRoster + k. Escrita así, la semilla es función
        // pura del índice: las celdas se juegan en paralelo y la secuencia de semillas es la misma bit a
        // bit que en serie (RT-020..024), de modo que la puerta sigue midiendo la misma muestra (RT-057).
        // La reducción se hace después del Parallel.For, recorriendo el array en orden de índice.
        var played = new (BuildCellResult Subject, BuildCellResult Baseline, List<PerkActivationResult> Perks)[subjects.Count];
        Parallel.For(0, subjects.Count, i =>
        {
            string id = subjects[i];
            played[i] = RunCell(ThreadCatalogs.Current, builds, id, baselines[id], i * Rosters * MatchesPerRoster, items, seed);
        });

        var cells = new List<BuildCellResult>();
        var activations = new List<PerkActivationResult>();
        foreach (var (subject, baseline, perks) in played)
        {
            cells.Add(subject);
            cells.Add(baseline);
            activations.AddRange(perks);
        }

        return BuildMetrics.Compute(
            cells, groups.Coherent, groups.Bad, groups.Random, baselines,
            PhysicalBuild, TechnicalBuild, activations,
            catalog.Perks.All.Select(p => p.Kind).ToList());
    }

    /// <summary>
    /// Simula una celda completa build × referencia con plantillas emparejadas y devuelve las dos caras
    /// (la de la build y la de la referencia, que es la que normaliza <c>buildsWinDifferently</c>) más las
    /// activaciones de los perks de la build.
    /// </summary>
    /// <param name="matchIndexOffset">
    /// Índice global del primer partido de la celda, para que dos celdas no compartan semilla de partido.
    /// Es <c>índice del sujeto × Rosters × MatchesPerRoster</c>: la misma secuencia que producía el
    /// contador secuencial.
    /// </param>
    private static (BuildCellResult Subject, BuildCellResult Baseline, List<PerkActivationResult> Perks) RunCell(
        Catalog catalog,
        IReadOnlyDictionary<string, BuildFile> builds,
        string buildId,
        string baselineId,
        int matchIndexOffset,
        Underleague.Sim.Run.Systems.Items.ItemCatalog items,
        ulong seed)
    {
        var build = builds[buildId];
        var baseline = builds[baselineId];
        var config = new SimConfig(CollectLog: false);

        int matches = 0, subjectWins = 0;
        int subjectGoals = 0, baselineGoals = 0;
        int subjectInjured = 0, baselineInjured = 0;
        int subjectTackles = 0, baselineTackles = 0;
        int subjectOffBallTackles = 0, baselineOffBallTackles = 0;
        int subjectChains = 0, subjectChainLength = 0;
        int baselineChains = 0, baselineChainLength = 0;
        int subjectActivations = 0;

        var assignedPerks = build.Perks.Select(p => p.Perk).Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
        var perkMatches = assignedPerks.ToDictionary(p => p, _ => 0, StringComparer.Ordinal);
        var seenThisMatch = new HashSet<string>(StringComparer.Ordinal);

        for (int roster = 0; roster < Rosters; roster++)
        {
            for (int k = 0; k < MatchesPerRoster; k++)
            {
                bool subjectAway = (k % 2) == 1;
                bool subjectHasHighIds = ((k / 2) % 2) == 1;

                int subjectIdBase = subjectHasHighIds ? SecondaryIdBase : PrimaryIdBase;
                int baselineIdBase = subjectHasHighIds ? PrimaryIdBase : SecondaryIdBase;

                // ADR 0106: las builds MALAS equipan objetos malditos en el portador equivocado, que desde
                // la ADR 0088 es la única forma que queda de construir en contra. Sin el catálogo de
                // objetos esta puerta no puede jugarlas — BossGateTests ya lo pasaba y ésta no.
                var subjectTeam = build.ToTeamSetup(catalog, seed, roster, subjectIdBase, itemCatalog: items);
                var baselineTeam = baseline.ToTeamSetup(catalog, seed, roster, baselineIdBase, itemCatalog: items);

                var setup = subjectAway
                    ? new MatchSetup(baselineTeam, subjectTeam, Referee)
                    : new MatchSetup(subjectTeam, baselineTeam, Referee);

                var result = Simulator.Run(
                    setup,
                    RngStreams.MatchSeed(seed, matchIndexOffset + (roster * MatchesPerRoster) + k),
                    catalog,
                    config);
                var report = result.Report;

                int subjectSide = subjectAway ? 1 : 0;
                matches++;
                if (report.Winner == subjectSide)
                {
                    subjectWins++;
                }

                subjectGoals += report.Goals[subjectSide];
                baselineGoals += report.Goals[1 - subjectSide];
                subjectChains += report.PassChainsByTeam[subjectSide];
                subjectChainLength += report.PassChainTotalLengthByTeam[subjectSide];
                baselineChains += report.PassChainsByTeam[1 - subjectSide];
                baselineChainLength += report.PassChainTotalLengthByTeam[1 - subjectSide];

                foreach (var stat in report.Players)
                {
                    bool isSubject = stat.Team == subjectSide;
                    if (isSubject)
                    {
                        subjectTackles += stat.Tackles;
                        subjectOffBallTackles += stat.OffBallTackles;
                        if (stat.Injured)
                        {
                            subjectInjured++;
                        }
                    }
                    else
                    {
                        baselineTackles += stat.Tackles;
                        baselineOffBallTackles += stat.OffBallTackles;
                        if (stat.Injured)
                        {
                            baselineInjured++;
                        }
                    }
                }

                seenThisMatch.Clear();
                var subjectPlayerIds = subjectTeam.Players.Select(p => p.Id).ToHashSet();
                foreach (var activation in report.PerkActivations)
                {
                    if (!subjectPlayerIds.Contains(activation.OwnerId))
                    {
                        continue;
                    }

                    subjectActivations++;
                    seenThisMatch.Add(activation.PerkId);
                }

                foreach (var perkId in seenThisMatch)
                {
                    if (perkMatches.ContainsKey(perkId))
                    {
                        perkMatches[perkId]++;
                    }
                }
            }
        }

        var subjectCell = new BuildCellResult(
            buildId, baselineId, matches, subjectWins, subjectGoals, baselineGoals,
            subjectInjured, baselineInjured, subjectTackles, subjectOffBallTackles, subjectChains, subjectChainLength,
            subjectActivations);

        var baselineCell = new BuildCellResult(
            baselineId, buildId, matches, matches - subjectWins, baselineGoals, subjectGoals,
            baselineInjured, subjectInjured, baselineTackles, baselineOffBallTackles, baselineChains,
            baselineChainLength, 0);

        var perkRows = assignedPerks
            .Select(p => new PerkActivationResult(p, buildId, matches, perkMatches[p]))
            .ToList();

        return (subjectCell, baselineCell, perkRows);
    }

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
}
