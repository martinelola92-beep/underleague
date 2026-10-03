using System.Diagnostics;
using System.Globalization;
using Underleague.Sim.Analysis;
using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Run;
using Underleague.Sim.Run.Bosses;
using Underleague.Sim.Run.Systems;
using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Balance;

/// <summary>
/// Frecuencia de una apuesta en un grupo de partidos: <see cref="Matches"/> partidos de liga, élite o jefe
/// jugados, <see cref="Hits"/> en los que la condición se cumple, y el error típico de la frecuencia
/// tratando cada run como un conglomerado (los partidos de una misma run no son independientes: mismo
/// club, misma plantilla que se desgasta).
/// </summary>
public sealed record BetCensusCell(string BetId, string Group, string Key, long Matches, long Hits, double StdErr)
{
    /// <summary>Frecuencia en [0, 1]; 0 si no hay muestra.</summary>
    public double Frequency => Matches == 0 ? 0 : (double)Hits / Matches;
}

/// <summary>Resultado del modo <c>--bet-census</c>.</summary>
public sealed record BetCensusResult(
    int Runs,
    long Matches,
    IReadOnlyList<BetCensusCell> Cells,
    IReadOnlyList<long> MatchesByDifficulty,
    TimeSpan Elapsed);

/// <summary>
/// Modo <c>--bet-census N</c> de <c>/Balance</c> (ADR 0157, paso 2): juega N runs completas con la política
/// contextual de <see cref="RunPolicy"/> y, en CADA partido de liga, élite o jefe que se juega, evalúa las
/// once condiciones de apuesta (todas, no solo la que se habría ofrecido) con
/// <see cref="BetConditions.Evaluate"/>. De la frecuencia de cada una por distintivo de dificultad (RF-012)
/// salen las cuotas de <c>data/bets/bets.json</c>: <c>cuota = 85 / p</c>.
///
/// <para><b>Aviso al recensar.</b> Si se recensa y se vuelcan las frecuencias en <c>bets.json</c>, la celda
/// <c>comeback</c> d1 recupera el valor de este censo (política <c>Never</c>, 6,25 % y cobro 1.360 %) y pierde la
/// recalibración del 2 oct 2026 (7,76 % y 1.095 %, medida con <c>Blind</c>; ADR 0157, enmienda de cierre,
/// decisión 5), y lo mismo ocurre con las celdas recalibradas con <c>Blind</c> el 3 oct 2026 (<c>into_the_mob</c> d2/d3,
/// <c>eye_for_eye</c> d3, <c>hunt_the_star</c> d1, <c>blood_before_goals</c> d1/d2, <c>thrashing</c> d1/d2; ADR 0157,
/// enmienda del 3 oct; <c>BetSystemTests.RecalibratedCellsAnnounceTheMeasuredFrequencyAndReturnInLineWithTheRest</c>).
/// Hay que decidir a conciencia cuál de las dos procedencias se queda; las celdas
/// <c>withdrawnDifficulties</c> tampoco las toca el censo. <c>BetSystemTests.ComebackOnDifficultyOneReturnsInLineWithTheRest</c>
/// falla si se pisa sin querer.</para>
///
/// <para><b>Qué mide y qué no.</b> Mide la frecuencia con la que un partido REAL de la política automática
/// cumple cada condición, sin apostar: es la frecuencia de fondo. No mide lo que hace subir esa frecuencia
/// quien prepara el partido para ganar la apuesta (alineación, orden, consumible: ADR 0157 punto 5), que es
/// exactamente el margen de decisión del jugador. Cuenta también los partidos que terminan la run
/// (<see cref="IRunSystems.OnMatchPlayed"/>): dejarlos fuera sesgaría a la baja todas las condiciones de
/// derrota.</para>
///
/// <para><b>Determinismo y paralelismo.</b> La semilla de la run <c>i</c> es <c>seed * 100.000 + i</c>
/// (función pura del índice; <c>seed + i</c> como en <c>--full-runs</c> haría que <c>--seed 1</c> y
/// <c>--seed 7</c> compartieran casi todas sus runs). Las runs se juegan con <c>Parallel.For</c> sobre un
/// array por índice, cada hilo con SU catálogo (<see cref="BalanceCatalogs"/>), y la reducción se hace
/// después en orden de índice: la salida es idéntica a la secuencial.</para>
/// </summary>
public static class BetCensusRunner
{
    /// <summary>Distintivos de dificultad (RF-012): 1..5.</summary>
    public const int Difficulties = 5;

    /// <summary>Semilla de la run <paramref name="index"/> del lote <paramref name="seed"/>.</summary>
    public static ulong RunSeed(ulong seed, int index) => (seed * 100_000UL) + (ulong)index;

    /// <summary>Contadores de UNA run: partidos y aciertos por (apuesta, dificultad) y por (apuesta, tipo de nodo).</summary>
    private sealed class RunTally
    {
        public RunTally(int bets)
        {
            MatchesByDifficulty = new long[Difficulties];
            HitsByDifficulty = new long[bets, Difficulties];
            MatchesByKind = new long[NodeKinds3];
            HitsByKind = new long[bets, NodeKinds3];
        }

        public long[] MatchesByDifficulty { get; }

        public long[,] HitsByDifficulty { get; }

        public long[] MatchesByKind { get; }

        public long[,] HitsByKind { get; }
    }

    /// <summary>Tipos de nodo de partido: liga, élite, jefe.</summary>
    private const int NodeKinds3 = 3;

    private static readonly string[] NodeKindNames = { "league", "elite", "boss" };

    private static int KindIndex(NodeKind kind) => kind switch
    {
        NodeKind.LeagueMatch => 0,
        NodeKind.EliteMatch => 1,
        _ => 2,
    };

    /// <param name="catalogPerThread">
    /// Catálogo del hilo que ejecuta la run. Por defecto <see cref="BalanceCatalogs"/> (que <c>Program</c>
    /// inicializa); los tests, que no pueden inicializarlo, pasan el suyo para no compartir un catálogo entre
    /// hilos (CLAUDE.md: <c>/Sim</c> no es reentrante).
    /// </param>
    public static BetCensusResult Run(
        Catalog catalog,
        IReadOnlyDictionary<string, string> dataFiles,
        ulong seed,
        int runs,
        Func<Catalog>? catalogPerThread = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dataFiles);
        ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);

        var standard = StandardRunSystems.FromJson(dataFiles);
        var bosses = BossCatalog.FromJson(dataFiles);
        var races = FullRunRunner.LaunchRaces(catalog);
        var bets = standard.Bets.All;
        var options = RunPolicyOptions.For(PurchaseDoctrine.Contextual);

        var stopwatch = Stopwatch.StartNew();
        var tallies = new RunTally[runs];
        Parallel.For(0, runs, i =>
        {
            var tally = new RunTally(bets.Count);
            var threadCatalog = catalogPerThread?.Invoke() ?? BalanceCatalogs.Current(catalog);
            var setup = FullRunRunner.SetupFor(races[i % races.Count], standard, dataFiles);
            RunPolicy.Play(
                setup,
                RunSeed(seed, i),
                threadCatalog,
                standard,
                bosses,
                options,
                (stateBefore, node, matchSetup, result, summary) => Observe(tally, bets, stateBefore, node, matchSetup, result));
            tallies[i] = tally;
        });

        // Reducción en orden de índice, después del Parallel.For.
        var cells = new List<BetCensusCell>();
        var matchesByDifficulty = new List<long>();
        long total = 0;
        for (int d = 0; d < Difficulties; d++)
        {
            long m = 0;
            for (int i = 0; i < runs; i++)
            {
                m += tallies[i].MatchesByDifficulty[d];
            }

            matchesByDifficulty.Add(m);
            total += m;
        }

        for (int b = 0; b < bets.Count; b++)
        {
            for (int d = 0; d < Difficulties; d++)
            {
                int diff = d;
                int bet = b;
                cells.Add(Cell(bets[b].Id, "difficulty", (d + 1).ToString(CultureInfo.InvariantCulture), tallies,
                    t => t.MatchesByDifficulty[diff], t => t.HitsByDifficulty[bet, diff]));
            }

            int allBet = b;
            cells.Add(Cell(bets[b].Id, "difficulty", "all", tallies,
                t => t.MatchesByDifficulty.Sum(), t => Enumerable.Range(0, Difficulties).Sum(d => t.HitsByDifficulty[allBet, d])));
            for (int k = 0; k < NodeKinds3; k++)
            {
                int kind = k;
                int bet = b;
                cells.Add(Cell(bets[b].Id, "nodekind", NodeKindNames[k], tallies,
                    t => t.MatchesByKind[kind], t => t.HitsByKind[bet, kind]));
            }
        }

        stopwatch.Stop();
        return new BetCensusResult(runs, total, cells, matchesByDifficulty, stopwatch.Elapsed);
    }

    private static void Observe(
        RunTally tally,
        IReadOnlyList<BetDefinition> bets,
        RunState stateBefore,
        MapNode node,
        MatchSetup setup,
        MatchResult result)
    {
        int diff = Math.Clamp(node.Difficulty, 1, Difficulties) - 1;
        int kind = KindIndex(node.Kind);
        tally.MatchesByDifficulty[diff]++;
        tally.MatchesByKind[kind]++;

        int target = BetSystem.TargetFor(setup.Away)?.Id ?? -1;
        var context = new BetContext(
            setup,
            result,
            id => stateBefore.FindPlayer(id) is { IsYouth: true },
            target);
        for (int b = 0; b < bets.Count; b++)
        {
            if (BetConditions.Evaluate(bets[b].Kind, context))
            {
                tally.HitsByDifficulty[b, diff]++;
                tally.HitsByKind[b, kind]++;
            }
        }
    }

    /// <summary>
    /// Celda con el error típico por conglomerados: con m_i partidos y h_i aciertos en la run i,
    /// var(p) = n/(n-1) · Σ (h_i - p·m_i)² / (Σ m_i)². Un partido no es una muestra independiente de otro de
    /// su misma run, y la fórmula binomial subestimaría el error.
    /// </summary>
    private static BetCensusCell Cell(
        string betId,
        string group,
        string key,
        RunTally[] tallies,
        Func<RunTally, long> matchesOf,
        Func<RunTally, long> hitsOf)
    {
        long matches = 0;
        long hits = 0;
        for (int i = 0; i < tallies.Length; i++)
        {
            matches += matchesOf(tallies[i]);
            hits += hitsOf(tallies[i]);
        }

        double stdErr = 0;
        if (matches > 0 && tallies.Length > 1)
        {
            double p = (double)hits / matches;
            double sum = 0;
            int clusters = 0;
            for (int i = 0; i < tallies.Length; i++)
            {
                long m = matchesOf(tallies[i]);
                if (m == 0)
                {
                    continue;
                }

                clusters++;
                double residual = hitsOf(tallies[i]) - (p * m);
                sum += residual * residual;
            }

            if (clusters > 1)
            {
                stdErr = Math.Sqrt(clusters / (double)(clusters - 1) * sum) / matches;
            }
        }

        return new BetCensusCell(betId, group, key, matches, hits, stdErr);
    }
}
