using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Random;
using Underleague.Sim.Run.Bosses;

namespace Underleague.Sim.Analysis;

/// <summary>
/// Una celda de la curva de puertas de la ADR 0033: un nivel de calidad de build contra un jefe.
/// </summary>
/// <param name="BossId">Jefe (<c>data/bosses/</c>).</param>
/// <param name="Act">Acto del jefe, 1..3.</param>
/// <param name="Level">Nivel de build: <c>incoherent</c>, <c>correct</c>, <c>good</c> o <c>excellent</c>.</param>
/// <param name="BuildId">Build concreta que produjo la celda (una por raza).</param>
/// <param name="Matches">Partidos jugados.</param>
/// <param name="Wins">Partidos <b>superados</b>: ganados y sin que se cumpliera la condición de derrota propia del jefe.</param>
/// <param name="PitchWins">Partidos ganados en el campo, antes de aplicar la condición propia del jefe.</param>
public readonly record struct BossGateCell(
    string BossId,
    int Act,
    string Level,
    string BuildId,
    int Matches,
    int Wins,
    int PitchWins)
{
    /// <summary>Tasa de superación de la puerta, en porcentaje.</summary>
    public double WinRate => Matches > 0 ? 100.0 * Wins / Matches : 0.0;

    /// <summary>Tasa de victoria en el campo, sin la condición de derrota propia del jefe.</summary>
    public double PitchWinRate => Matches > 0 ? 100.0 * PitchWins / Matches : 0.0;
}

/// <summary>
/// <b>La</b> métrica de la fase 2 (ADR 0033): cada nivel de calidad de build contra cada jefe, contra la
/// tabla de exigencia que la ADR fija y que cada jefe lleva escrita en su propio dato
/// (<c>gate.targets</c>). Mide con <b>partidos directos build-contra-jefe</b>, no con runs completas: la
/// pregunta es si la construcción pasa el examen, y para eso no hace falta la economía.
///
/// <para>Pura y sin E/S, como <see cref="MatchMetrics"/> y <see cref="BuildMetrics"/>:
/// <see cref="PlayCell"/> recibe una fábrica de plantillas (quien sepa leer
/// <c>data/balance/builds/</c> se la pasa) y <see cref="Compute"/> recibe las celdas ya jugadas. La
/// comparten la puerta de <c>Sim.Tests</c> y el modo <c>--boss-gate</c> de <c>/Balance</c>.</para>
///
/// <para><b>Metodología</b> (la del paquete I, que es la única que da una medida estable): cada celda se
/// promedia sobre varias plantillas generadas, y cada plantilla juega el mismo número de partidos de
/// local y de visitante y con los ids de jugador bajos y altos alternados, porque los desempates del
/// motor van por id ascendente y valen 2-3 puntos.</para>
/// </summary>
public static class BossGateMetrics
{
    /// <summary>Prefijo de la métrica agregada por (jefe, nivel de build): es la que compara con la tabla de la ADR 0033.</summary>
    public const string GatePrefix = "bossGate_";

    /// <summary>Prefijo de la métrica informativa por (build, jefe): la misma celda desglosada por raza.</summary>
    public const string BuildPrefix = "bossGateBuild_";

    /// <summary>Prefijo de la métrica informativa de cuánto pesa la condición de derrota propia del jefe.</summary>
    public const string DefeatConditionPrefix = "bossGateDefeatCondition_";

    /// <summary>Niveles de calidad de build, del peor al mejor (ADR 0033).</summary>
    public static IReadOnlyList<string> Levels { get; } = new[] { "incoherent", "correct", "good", "excellent" };

    /// <summary>
    /// Desplazamiento del índice de generación de la plantilla del jefe respecto del de la build, para
    /// que jefe y jugador no salgan nunca del mismo dado (son equipos distintos, no comparables).
    /// </summary>
    public const int BossRosterOffset = 700;

    /// <summary>
    /// Separación entre los bloques de plantillas de jefe de dos celdas que no comparten dado. Importa
    /// para la precisión: con <b>las mismas</b> plantillas de jefe en todas las celdas, la varianza de
    /// esas plantillas no se promedia al agregar por (jefe, nivel) y la celda se mueve 10 puntos al
    /// cambiar de semilla. Los cuatro niveles de una misma raza sí comparten las plantillas de jefe (así
    /// la escalera se compara contra el mismo rival); razas distintas usan bloques distintos.
    /// </summary>
    public const int BossRosterBlock = 64;

    /// <summary>Primer id de jugador del equipo que lleva los ids bajos.</summary>
    public const int PrimaryIdBase = 1;

    /// <summary>Primer id de jugador del equipo que lleva los ids altos.</summary>
    public const int SecondaryIdBase = 100001;

    private static readonly RefereeSetup Referee = new("Referee", RefereeTrait.Neutral, 0);

    /// <summary>
    /// Juega una celda completa: <paramref name="rosters"/> plantillas ×
    /// <paramref name="matchesPerRoster"/> partidos de la build contra el jefe, con los modificadores de
    /// regla aplicados al equipo del jugador y la condición de derrota propia del jefe evaluada sobre el
    /// resultado.
    /// </summary>
    /// <param name="subject">
    /// Fábrica de la plantilla del jugador: (índice de plantilla, primer id de jugador) -&gt; equipo. Debe
    /// ser determinista y no depender de nada más.
    /// </param>
    /// <param name="matchIndexOffset">Índice global del primer partido, para que dos celdas no compartan semilla de partido.</param>
    /// <param name="bossRosterBlock">Bloque de plantillas de jefe (ver <see cref="BossRosterBlock"/>): mismo bloque = mismos rivales.</param>
    public static BossGateCell PlayCell(
        Catalog catalog,
        BossDefinition boss,
        string level,
        string buildId,
        Func<int, int, TeamSetup> subject,
        ulong seed,
        int rosters,
        int matchesPerRoster,
        int matchIndexOffset,
        int bossRosterBlock = 0)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentOutOfRangeException.ThrowIfLessThan(rosters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(matchesPerRoster, 1);

        var config = new SimConfig(CollectLog: false);
        int matches = 0, wins = 0, pitchWins = 0, matchIndex = matchIndexOffset;

        // Generar un equipo cuesta más que simular un partido, y cada plantilla se usa en varios
        // partidos (local/visitante x reparto de ids): se generan una vez por (plantilla, reparto de ids).
        var subjectTeams = new TeamSetup?[rosters, 2];
        var bossTeams = new TeamSetup?[rosters, 2];

        for (int roster = 0; roster < rosters; roster++)
        {
            for (int k = 0; k < matchesPerRoster; k++)
            {
                bool subjectAway = (k % 2) == 1;
                bool subjectHasHighIds = ((k / 2) % 2) == 1;
                int idSlot = subjectHasHighIds ? 1 : 0;
                int subjectIdBase = subjectHasHighIds ? SecondaryIdBase : PrimaryIdBase;
                int bossIdBase = subjectHasHighIds ? PrimaryIdBase : SecondaryIdBase;

                var subjectTeam = subjectTeams[roster, idSlot] ??= subject(roster, subjectIdBase);
                var bossTeam = bossTeams[roster, idSlot];
                if (bossTeam is null)
                {
                    var bossRng = RngStreams.Generation(seed, BossRosterOffset + (bossRosterBlock * BossRosterBlock) + roster);
                    bossTeam = boss.Template.ToTeamSetup(ref bossRng, catalog, boss.Id, bossIdBase);
                    bossTeams[roster, idSlot] = bossTeam;
                }

                int subjectSide = subjectAway ? 1 : 0;
                var setup = subjectAway
                    ? new MatchSetup(bossTeam, subjectTeam, Referee)
                    : new MatchSetup(subjectTeam, bossTeam, Referee);
                setup = BossRules.Apply(setup, subjectSide, boss.Modifiers, catalog);

                var report = Simulator.Run(setup, RngStreams.MatchSeed(seed, matchIndex++), catalog, config).Report;

                matches++;
                if (report.Winner == subjectSide)
                {
                    pitchWins++;
                }

                if (BossRules.Passed(boss, report, subjectSide))
                {
                    wins++;
                }
            }
        }

        return new BossGateCell(boss.Id, boss.Act, level, buildId, matches, wins, pitchWins);
    }

    /// <summary>
    /// Contrasta las celdas con la tabla de la ADR 0033. Devuelve, por cada (jefe, nivel de build), la
    /// tasa <b>agregada sobre todas las razas</b> con su banda —la tabla de la ADR no distingue raza: la
    /// exigencia es de la puerta, no del club— y, como <c>INFO</c>, el desglose por build y el peso de la
    /// condición de derrota propia del jefe.
    /// </summary>
    public static List<MetricResult> Compute(IReadOnlyList<BossGateCell> cells, IReadOnlyList<BossDefinition> bosses)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(bosses);

        var rows = new List<MetricResult>();
        var byBoss = new Dictionary<string, BossDefinition>(StringComparer.Ordinal);
        foreach (var boss in bosses)
        {
            byBoss[boss.Id] = boss;
        }

        var pooled = new SortedDictionary<(int Act, string BossId, int LevelIndex), (int Matches, int Wins, int PitchWins)>();
        foreach (var cell in cells)
        {
            int levelIndex = IndexOfLevel(cell.Level);
            var key = (cell.Act, cell.BossId, levelIndex);
            pooled.TryGetValue(key, out var acc);
            pooled[key] = (acc.Matches + cell.Matches, acc.Wins + cell.Wins, acc.PitchWins + cell.PitchWins);
        }

        foreach (var (key, acc) in pooled)
        {
            var boss = byBoss.GetValueOrDefault(key.BossId);
            string level = Levels[key.LevelIndex];
            double rate = acc.Matches > 0 ? 100.0 * acc.Wins / acc.Matches : 0.0;
            var target = boss?.TargetFor(level);
            rows.Add(new MetricResult(
                $"{GatePrefix}{key.BossId}_{level}", rate, target?.MinPercent, target?.MaxPercent,
                target is null ? "INFO" : Status(rate, target.MinPercent, target.MaxPercent)));

            if (boss?.DefeatCondition is not null && acc.Matches > 0)
            {
                rows.Add(new MetricResult(
                    $"{DefeatConditionPrefix}{key.BossId}_{level}",
                    100.0 * (acc.PitchWins - acc.Wins) / acc.Matches, null, null, "INFO"));
            }
        }

        foreach (var cell in cells.OrderBy(c => c.BossId, StringComparer.Ordinal).ThenBy(c => c.BuildId, StringComparer.Ordinal))
        {
            rows.Add(new MetricResult($"{BuildPrefix}{cell.BuildId}_{cell.BossId}", cell.WinRate, null, null, "INFO"));
        }

        return rows;
    }

    /// <summary>Índice del nivel de build en <see cref="Levels"/>; lanza si el nivel no existe.</summary>
    public static int IndexOfLevel(string level)
    {
        for (int i = 0; i < Levels.Count; i++)
        {
            if (string.Equals(Levels[i], level, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new ArgumentException($"nivel de calidad de build desconocido: '{level}'", nameof(level));
    }

    private static string Status(double value, double? min, double? max) =>
        (min is null || value >= min.Value) && (max is null || value <= max.Value) ? "IN" : "OUT";
}
