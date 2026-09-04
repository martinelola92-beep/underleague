using System.Text.Json;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Tests.Run;

namespace Underleague.Sim.Tests.Analysis;

/// <summary>
/// Puerta de la curva de jefes (ADR 0033), que es <b>la</b> métrica de la fase 2: cada nivel de calidad
/// de build (incoherente, correcta, buena, muy buena) contra cada jefe de <c>data/bosses/</c>, contra la
/// banda de tasa de victoria que la ADR fija y que cada jefe lleva escrita en su propio dato
/// (<c>gate.targets</c>).
///
/// <para><b>Se mide con partidos directos build-contra-jefe</b>, no con runs completas: la pregunta de la
/// ADR es si la construcción pasa el examen, y eso no depende de la economía. La plantilla del jugador
/// llega a cada puerta con el nivel que le da la progresión (<c>gate.playerLevel</c>: 5 en el acto 1, 6 en
/// el 2, 7 en el 3, de 100 de experiencia por partido jugado y ~6 partidos por acto) y con los
/// modificadores de regla del jefe ya aplicados a su once.</para>
///
/// <para><b>Muestra</b>: semilla <see cref="Seed"/>, <see cref="Rosters"/> plantillas ×
/// <see cref="MatchesPerRoster"/> partidos por celda (local/visitante × reparto de ids alternados) para
/// las cinco razas de lanzamiento = <b>640 partidos por celda de la tabla</b> y 7.680 en total, unos 36 s.
/// Los cuatro niveles de una raza comparten plantilla generada y rivales, así que la <i>escalera</i> se
/// mide sin ruido de generación; el nivel absoluto de una celda sí lo tiene, y es grande: la varianza
/// entre plantillas domina y una celda se mueve <b>3-4 puntos</b> al cambiar de semilla. De ahí
/// <see cref="TolerancePercent"/>: la banda de la ADR se ensancha ese margen de medida, y los valores
/// medidos exactos están en docs/fase2-diseno.md.</para>
/// </summary>
[Trait("Category", "Gate")]
public sealed class BossGateTests
{
    /// <summary>Plantillas distintas por celda.</summary>
    private const int Rosters = 32;

    /// <summary>Partidos por plantilla. Múltiplo de 4: local/visitante × reparto de ids.</summary>
    private const int MatchesPerRoster = 4;

    /// <summary>Semilla del lote.</summary>
    private const ulong Seed = 1;

    /// <summary>
    /// Margen de medida con el que se ensancha la banda de la ADR 0033. No es una rebaja del criterio:
    /// es el error de una celda con esta muestra, dominado por la varianza de generación de plantillas
    /// (medido: la misma configuración se mueve 3-4 puntos entre semillas).
    /// </summary>
    private const double TolerancePercent = 2.5;

    /// <summary>Tasa mínima con la que un equipo sin legendarios tiene que poder ganarle al jefe final (ADR 0027).</summary>
    private const double MinNoLegendaryWinRate = 25.0;

    private static readonly Lazy<Measured> Result = new(Run);

    private sealed record Measured(IReadOnlyList<BossGateCell> Cells, IReadOnlyList<MetricResult> Metrics, BossCatalog Bosses);

    /// <summary>
    /// La curva de la ADR 0033: las doce celdas (cuatro niveles de build × tres jefes) dentro de su banda.
    /// Es el criterio de salida de la fase 2.
    /// </summary>
    [Fact]
    public void TheGateCurveMatchesTheAdr0033Table()
    {
        var rows = Result.Value.Metrics
            .Where(m => m.Name.StartsWith(BossGateMetrics.GatePrefix, StringComparison.Ordinal))
            .ToList();
        Assert.Equal(12, rows.Count);

        var offenders = rows
            .Where(r => !Within(r.Value, r.RangeMin, r.RangeMax))
            .Select(r => $"{r.Name}={r.Value:F2} (banda {Band(r)})")
            .ToList();

        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }

    /// <summary>
    /// La escalera es monótona en todos los jefes: incoherente &lt; correcta &lt; buena &lt; muy buena. Es
    /// lo que la ADR 0033 pide de verdad —que construir mejor se note— y no depende del ancho de las
    /// bandas, así que se comprueba aparte.
    /// </summary>
    [Fact]
    public void EveryBossRewardsABetterBuild()
    {
        foreach (var boss in Result.Value.Bosses.All)
        {
            double previous = -1;
            foreach (var level in BossGateMetrics.Levels)
            {
                double rate = Rate(boss.Id, level);
                Assert.True(
                    rate > previous,
                    $"{boss.Id}: el nivel '{level}' gana el {rate:F2}%, que no mejora al anterior ({previous:F2}%)");
                previous = rate;
            }
        }
    }

    /// <summary>
    /// Salvaguarda de la ADR 0027, que la ADR 0033 obliga a mantener viva: un equipo <b>sin ningún
    /// legendario</b> pero con build muy buena tiene que poder ganarle al jefe final. Las builds
    /// <c>*_excellent</c> son comunes con dos o tres raros y ni un legendario, así que la celda
    /// (jefe final, muy buena) <b>es</b> la salvaguarda.
    /// </summary>
    [Fact]
    public void ATeamWithoutLegendariesAndAVeryGoodBuildBeatsTheFinalBoss()
    {
        var finalBoss = Result.Value.Bosses.ForAct(RunRules.Acts);
        double rate = Rate(finalBoss.Id, "excellent");
        Assert.True(
            rate >= MinNoLegendaryWinRate,
            $"un equipo sin legendarios con build muy buena gana al jefe final el {rate:F2}%, por debajo del "
                + $"{MinNoLegendaryWinRate}% que la ADR 0027 fija como condición para no revisarla");

        foreach (var cell in Result.Value.Cells.Where(c => c.BossId == finalBoss.Id && c.Level == "excellent"))
        {
            var build = BuildFile.LoadAll(TestData.DataDirectory)[cell.BuildId];
            Assert.NotEqual(Rarity.Legendary, build.UniformRarity);
            Assert.DoesNotContain(build.Rarities, r => r.Value == Rarity.Legendary);
        }
    }

    /// <summary>
    /// La condición de derrota propia del jefe final (RF-001c, D-9) se aplica de verdad: anula victorias
    /// que en el campo lo eran. Si no anulara ninguna, la condición sería letra muerta.
    /// </summary>
    [Fact]
    public void TheFinalBossOwnDefeatConditionAnnulsWins()
    {
        var finalBoss = Result.Value.Bosses.ForAct(RunRules.Acts);
        Assert.NotNull(finalBoss.DefeatCondition);

        var cells = Result.Value.Cells.Where(c => c.BossId == finalBoss.Id).ToList();
        int pitchWins = cells.Sum(c => c.PitchWins);
        int wins = cells.Sum(c => c.Wins);
        Assert.True(pitchWins > wins, "la condición de derrota propia del jefe final no anuló ninguna victoria");

        // Y no afecta a los demás jefes, que no tienen condición propia.
        foreach (var cell in Result.Value.Cells.Where(c => c.BossId != finalBoss.Id))
        {
            Assert.Equal(cell.PitchWins, cell.Wins);
        }
    }

    /// <summary>
    /// RF-012b/RF-012d: el informe de ojeo del jefe permite anticipar la derrota. El partido que
    /// <see cref="BossRunSystems.BuildBossMatch"/> devuelve <b>antes</b> de jugar es exactamente el que se
    /// juega, con los modificadores ya aplicados al once del jugador: quien abra el informe ve el equipo
    /// real, no el de antes de la regla. Y RF-014: el modificador está oculto hasta llegar al nodo.
    /// </summary>
    [Fact]
    public void TheBossScoutingReportShowsTheMatchThatWillBePlayed()
    {
        var catalog = TestData.LoadCatalog();
        var bosses = BossCatalog.FromJson(TestData.LoadAllFiles());
        var systems = new BossRunSystems(bosses);

        var state = systems.AssignBosses(
            RunStateBuilder.From(TestRuns.Setup(), 4242, catalog, systems).BeforeBoss().Build());

        var map = state.MapOf(state.Act);
        var bossNode = map.Get(map.BossNodeId);
        var boss = bosses.ForAct(state.Act);

        // RF-014: el nodo de jefe es visible desde el principio del acto, pero su modificador no.
        Assert.Equal(boss.Id, map.BossModifierId);
        Assert.False(map.BossModifierRevealed);

        // Los datos con los que decidir están disponibles ANTES de entrar (RF-012b): plantilla completa
        // del jefe, sus perks, y el once propio tal y como quedará con la regla aplicada.
        var (scouted, seed, _) = systems.BuildBossMatch(state, bossNode.Id, catalog);
        Assert.Equal(boss.Id, scouted.Away.Id);
        Assert.All(scouted.Away.Players, p => Assert.Equal(boss.Template.Level, p.Level));
        Assert.Contains(scouted.Away.Players, p => p.Perks.Count > 0);

        // El modificador se nota en el informe: el once del jugador que se va a alinear no es el mismo
        // que sin la regla. Es lo que hace la derrota anticipable (RF-012d).
        var raw = RunEngine.BuildMatch(state, bossNode.Id, catalog, systems);
        Assert.Equal(raw.Seed, seed);
        var modifiers = bosses.Modifiers(systems.BossRuleModifiers(state, bossNode, catalog));
        Assert.NotEmpty(modifiers);
        var expected = BossRules.Apply(raw.Setup, 0, modifiers, catalog);
        Assert.Equal(
            expected.Home.Players.Select(p => string.Join('+', p.Perks)),
            scouted.Home.Players.Select(p => string.Join('+', p.Perks)));
        Assert.Equal(
            expected.Home.Lineup.Slots.Select(s => (s.PlayerId, s.HomeCell)),
            scouted.Home.Lineup.Slots.Select(s => (s.PlayerId, s.HomeCell)));

        // RF-014b: una vez jugado el nodo, el modificador queda descubierto y el compendio puede
        // registrarlo por su id. Ojo: RunEngine solo marca el descubrimiento cuando el partido NO termina
        // la run, así que perder contra el jefe deja el modificador sin registrar — justo el caso en el
        // que el jugador ha pagado la sorpresa. Es un hueco del paquete W, anotado como Y-8 en
        // docs/fase2-diseno.md; aquí se comprueba lo que hoy está garantizado.
        var after = RunEngine.Enter(state, bossNode.Id, catalog, systems);
        var outcome = RunEngine.Outcome(after);
        Assert.True(
            after.MapOf(bossNode.Act).BossModifierRevealed || outcome.Cause == DefeatCause.BossMatchLost,
            "el modificador del jefe no quedó descubierto y el partido tampoco terminó la run (RF-014b)");
        Assert.All(boss.ModifierIds, id => Assert.NotNull(bosses.FindModifier(id)));
    }

    /// <summary>RF-001b/RF-001c: un modificador en los actos 1 y 2, dos y condición propia en el jefe final.</summary>
    [Fact]
    public void EveryActHasItsBossWithTheModifiersItsRequirementDemands()
    {
        var bosses = BossCatalog.FromJson(TestData.LoadAllFiles());
        for (int act = 1; act <= RunRules.Acts; act++)
        {
            var boss = bosses.ForAct(act);
            Assert.Equal(act == RunRules.Acts ? 2 : 1, boss.Modifiers.Count);
            if (act == RunRules.Acts)
            {
                Assert.NotNull(boss.DefeatCondition);
                Assert.Equal(Rarity.Legendary, boss.Template.UniformRarity);   // RF-001c
            }
            else
            {
                Assert.Null(boss.DefeatCondition);
            }
        }
    }

    // ------------------------------------------------------------------ medición

    private static double Rate(string bossId, string level)
    {
        var row = Result.Value.Metrics.FirstOrDefault(
            m => m.Name == $"{BossGateMetrics.GatePrefix}{bossId}_{level}");
        Assert.NotNull(row);
        return row.Value;
    }

    private static bool Within(double value, double? min, double? max) =>
        (min is null || value >= min.Value - TolerancePercent)
        && (max is null || value <= max.Value + TolerancePercent);

    private static string Band(MetricResult row) =>
        $"{row.RangeMin?.ToString("F0") ?? "-"}..{row.RangeMax?.ToString("F0") ?? "-"} ±{TolerancePercent}";

    private static Measured Run()
    {
        var files = TestData.LoadAllFiles();
        var catalog = DataLoader.FromJson(files);
        var bosses = BossCatalog.FromJson(files);
        var builds = BuildFile.LoadAll(TestData.DataDirectory);
        var counters = LoadCounters(TestData.DataDirectory);
        var levels = LoadQualityLevels(TestData.DataDirectory);

        var cells = new List<BossGateCell>();
        int matchIndex = 0;

        foreach (var boss in bosses.All)
        {
            foreach (var level in BossGateMetrics.Levels)
            {
                foreach (var buildId in levels[level].OrderBy(id => id, StringComparer.Ordinal))
                {
                    var build = builds[buildId];
                    var slotCounters = counters.GetValueOrDefault(buildId);
                    var cell = BossGateMetrics.PlayCell(
                        catalog, boss, level, buildId,
                        (roster, idBase) => WithCounters(
                            build.ToTeamSetup(catalog, Seed, roster, idBase, boss.GatePlayerLevel), slotCounters),
                        Seed, Rosters, MatchesPerRoster, matchIndex, (int)build.Race);

                    matchIndex += cell.Matches;
                    cells.Add(cell);
                }
            }
        }

        return new Measured(cells, BossGateMetrics.Compute(cells, bosses.All), bosses);
    }

    /// <summary>
    /// Contadores de carrera con los que entra cada titular (<c>counters</c> de la build). BuildFile no
    /// los lee: son de la escala de la ADR 0033, no de la puerta de fase 1. Es lo que separa "buena" de
    /// "muy buena": un perk de acumulación con el contador a cero no vale nada.
    /// </summary>
    private static TeamSetup WithCounters(
        TeamSetup team, IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>>? bySlot)
    {
        if (bySlot is null || bySlot.Count == 0)
        {
            return team;
        }

        var players = team.Players.ToList();
        foreach (var (slot, values) in bySlot.OrderBy(e => e.Key))
        {
            players[slot] = players[slot].WithCounters(values);
        }

        return team with { Players = players };
    }

    private static Dictionary<string, IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>>> LoadCounters(string dataDirectory)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyDictionary<string, int>>>(StringComparer.Ordinal);
        string dir = Path.Combine(dataDirectory, "balance", "builds");
        foreach (var path in Directory.EnumerateFiles(dir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("counters", out var element) || element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var bySlot = new SortedDictionary<int, IReadOnlyDictionary<string, int>>();
            foreach (var slot in element.EnumerateObject())
            {
                var values = new SortedDictionary<string, int>(StringComparer.Ordinal);
                foreach (var counter in slot.Value.EnumerateObject())
                {
                    values[counter.Name] = counter.Value.GetInt32();
                }

                bySlot[int.Parse(slot.Name)] = values;
            }

            result[root.GetProperty("id").GetString()!] = bySlot;
        }

        return result;
    }

    private static Dictionary<string, IReadOnlyList<string>> LoadQualityLevels(string dataDirectory)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(dataDirectory, "balance", "groups.json")));
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.GetProperty("qualityLevels").EnumerateObject())
        {
            result[property.Name] = property.Value.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        return result;
    }
}
