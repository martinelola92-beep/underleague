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
public sealed class BuildGateTests
{
    /// <summary>Plantillas distintas sobre las que se promedia cada celda.</summary>
    private const int Rosters = 40;

    /// <summary>Partidos por plantilla (múltiplo de 4: local/visitante × reparto de ids).</summary>
    private const int MatchesPerRoster = 12;

    /// <summary>Semilla base del lote de la puerta.</summary>
    private const ulong Seed = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids bajos.</summary>
    private const int PrimaryIdBase = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids altos.</summary>
    private const int SecondaryIdBase = 100001;

    /// <summary>Build "gana por contacto" de <c>buildsWinDifferently</c> (§8).</summary>
    private const string PhysicalBuild = "orc_violence";

    /// <summary>Build "gana por técnica" de <c>buildsWinDifferently</c> (§8).</summary>
    private const string TechnicalBuild = "elf_tiki_taka";

    private static readonly Lazy<IReadOnlyList<MetricResult>> Results = new(Compute);

    /// <summary>§8: cada build coherente gana ≥ 58% a la referencia sin perks de su raza.</summary>
    [Fact]
    public void CoherentBuildsBeatTheirBaseline() =>
        AssertAllIn(BuildMetrics.CoherentBuildsBeatNonePrefix);

    /// <summary>§8: cada build mal construida a propósito gana ≤ 45% a su referencia.</summary>
    [Fact]
    public void BadBuildsLoseToTheirBaseline() =>
        AssertAllIn(BuildMetrics.BadBuildsLoseToNonePrefix);

    /// <summary>§8: la build sin criterio se queda entre el 40% y el 60%.</summary>
    [Fact]
    public void RandomBuildStaysNearItsBaseline() =>
        AssertAllIn(BuildMetrics.RandomBuildNearNonePrefix);

    /// <summary>§8: la build de contacto lesiona mucho más y la técnica encadena muchos más pases.</summary>
    [Fact]
    public void BuildsWinDifferently()
    {
        AssertIn(BuildMetrics.BuildsWinDifferentlyInjuries);
        AssertIn(BuildMetrics.BuildsWinDifferentlyPassChain);
    }

    /// <summary>§8/RF-070: ningún perk asignado se queda por debajo del 1% de partidos con activación.</summary>
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

    private static IReadOnlyList<MetricResult> Compute()
    {
        var catalog = TestData.LoadCatalog();
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var groups = BuildGroupsFile.Load(TestData.DataDirectory);

        var subjects = groups.Coherent.Concat(groups.Bad).Concat(groups.Random)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var baselines = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in subjects)
        {
            baselines[id] = groups.BaselineByRace[builds[id].Race.ToString()];
        }

        var cells = new List<BuildCellResult>();
        var activations = new List<PerkActivationResult>();
        int matchIndex = 0;

        foreach (var id in subjects)
        {
            var (subject, baseline, perks) = RunCell(catalog, builds, id, baselines[id], ref matchIndex);
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
    private static (BuildCellResult Subject, BuildCellResult Baseline, List<PerkActivationResult> Perks) RunCell(
        Catalog catalog,
        IReadOnlyDictionary<string, BuildFile> builds,
        string buildId,
        string baselineId,
        ref int matchIndex)
    {
        var build = builds[buildId];
        var baseline = builds[baselineId];
        var config = new SimConfig(CollectLog: false);

        int matches = 0, subjectWins = 0;
        int subjectGoals = 0, baselineGoals = 0;
        int subjectInjured = 0, baselineInjured = 0;
        int subjectTackles = 0, baselineTackles = 0;
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

                var subjectTeam = build.ToTeamSetup(catalog, Seed, roster, subjectIdBase);
                var baselineTeam = baseline.ToTeamSetup(catalog, Seed, roster, baselineIdBase);

                var setup = subjectAway
                    ? new MatchSetup(baselineTeam, subjectTeam, Referee)
                    : new MatchSetup(subjectTeam, baselineTeam, Referee);

                var result = Simulator.Run(setup, RngStreams.MatchSeed(Seed, matchIndex++), catalog, config);
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
                        if (stat.Injured)
                        {
                            subjectInjured++;
                        }
                    }
                    else
                    {
                        baselineTackles += stat.Tackles;
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
            subjectInjured, baselineInjured, subjectTackles, subjectChains, subjectChainLength, subjectActivations);

        var baselineCell = new BuildCellResult(
            baselineId, buildId, matches, matches - subjectWins, baselineGoals, subjectGoals,
            baselineInjured, subjectInjured, baselineTackles, baselineChains, baselineChainLength, 0);

        var perkRows = assignedPerks
            .Select(p => new PerkActivationResult(p, buildId, matches, perkMatches[p]))
            .ToList();

        return (subjectCell, baselineCell, perkRows);
    }

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);
}
