using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems.Economy;

/// <summary>Objetivo de "partido excelente" anunciado antes de jugar (RF-114h).</summary>
public enum ExcellentMatchObjective
{
    /// <summary>Ganar por 3 goles de diferencia o más.</summary>
    WinByThreeOrMore,

    /// <summary>Ganar sin encajar ningún gol.</summary>
    CleanSheet,

    /// <summary>Ganar en inferioridad numérica: menos de 7 titulares (RF-002d).</summary>
    WinShortHanded,

    /// <summary>Ganar con un gol de un canterano (RF-114c).</summary>
    WinWithYouthScorer,
}

/// <summary>
/// Objetivo de partido excelente de un nodo de partido (RF-114h). Se deriva de
/// <c>RngStreams.Rewards(seed, node.Id)</c> -no del flujo de partido (RT-022)- así que es el mismo antes
/// de jugar (para anunciarlo, RF-012d) y después (para comprobarlo). Es independiente del surtido de
/// recompensa del mismo nodo, que usa <c>node.Id * 10.000 + rerollCount</c> (<see cref="OfferStream"/>):
/// nunca comparten el mismo índice sintético.
/// </summary>
public static class ExcellentMatchObjectives
{
    private static readonly ExcellentMatchObjective[] All = Enum.GetValues<ExcellentMatchObjective>();

    /// <summary>Objetivo anunciado de ese nodo de partido, determinista por (semilla, nodo).</summary>
    public static ExcellentMatchObjective For(ulong seed, MapNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var rng = RngStreams.Rewards(seed, node.Id);
        return All[rng.Range(0, All.Length)];
    }

    /// <summary>
    /// True si el resumen del partido cumple el objetivo. Solo mira datos conocidos de antemano (goles,
    /// titulares, quién anotó) o el propio desenlace: nunca el rendimiento fino (RF-114i).
    /// </summary>
    public static bool Satisfied(ExcellentMatchObjective objective, RunState stateAfterMatch, RunMatchSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (!summary.Won)
        {
            return false;
        }

        return objective switch
        {
            ExcellentMatchObjective.WinByThreeOrMore => summary.GoalsFor - summary.GoalsAgainst >= 3,
            ExcellentMatchObjective.CleanSheet => summary.GoalsAgainst == 0,
            ExcellentMatchObjective.WinShortHanded => summary.PlayedPlayerIds.Count < RunRules.MaxStarters,
            ExcellentMatchObjective.WinWithYouthScorer => HasYouthScorer(stateAfterMatch, summary),
            _ => false,
        };
    }

    private static bool HasYouthScorer(RunState state, RunMatchSummary summary)
    {
        var players = summary.Report.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var stats = players[i];
            if (stats.Team != 0 || stats.Goals <= 0)
            {
                continue;
            }

            var player = state.FindPlayer(stats.PlayerId);
            if (player is { IsYouth: true })
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Oro por partido ganado (RF-114g..k). Solo depende de datos conocidos antes de jugar (acto, dificultad,
/// tipo de nodo) y de un bonus fijo si se cumplió el objetivo anunciado: nunca del resultado fino dentro
/// del partido (RF-114i).
/// </summary>
public static class GoldCalculator
{
    public static int GoldForWin(RunState state, MapNode node, RunMatchSummary summary, EconomyConfig economy) =>
        Breakdown(state, node, summary, economy).Total;

    /// <summary>
    /// El mismo cálculo, sumando aparte (RF-119). El informe post-partido tiene que poder decir
    /// <b>por qué</b> se ha cobrado esa cantidad, y la única forma de que el desglose no mienta es que sea
    /// el propio cálculo: <see cref="GoldForWin"/> es su total, no una fórmula paralela.
    /// </summary>
    public static GoldForWinBreakdown Breakdown(RunState state, MapNode node, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);

        int actBase = economy.GoldForAct(node.Act);
        int difficultyPercent = economy.MultiplierForDifficulty(node.Difficulty);
        int afterDifficulty = actBase * difficultyPercent / 100;

        // ADR 0043: el escalón por tipo de nodo. La liga paga la base, el élite paga más y el jefe mucho
        // más: sin ese salto, superar un acto no cambia la trayectoria de la run.
        int nodeBonusPercent = economy.RewardFor(node.Kind).GoldBonusPercent;
        int nodeBonus = afterDifficulty * nodeBonusPercent / 100;

        var objective = ExcellentMatchObjectives.For(state.Seed, node);
        bool objectiveMet = ExcellentMatchObjectives.Satisfied(objective, state, summary);
        int objectiveBonus = objectiveMet ? economy.ExcellentMatchBonusGold : 0;

        return new GoldForWinBreakdown(
            node.Act,
            actBase,
            node.Difficulty,
            difficultyPercent,
            afterDifficulty,
            node.Kind,
            nodeBonusPercent,
            nodeBonus,
            objective,
            objectiveMet,
            objectiveBonus,
            afterDifficulty + nodeBonus + objectiveBonus);
    }

    /// <summary>
    /// Oro que pagan los contadores de este partido, <b>se haya ganado o no</b> (ADR 0113). No es premio
    /// de partido y por eso no entra en <see cref="Breakdown"/>: el premio lo cobra quien gana (RF-114g),
    /// mientras que esto es el retorno de una inversión que el jugador ya pagó al gastar un slot.
    /// </summary>
    public static CounterGold CounterGold(RunState state, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);
        var rows = CounterGoldRows(state, summary, economy.CounterGold);
        return new CounterGold(rows, SumGold(rows));
    }

    /// <summary>
    /// Filas de oro por contador (paquete Z, primitiva D): los contadores propios de este partido
    /// (<see cref="RunMatchSummary.CounterDeltas"/>) que tienen tarifa en <paramref name="rates"/>. Solo
    /// cuentan los del propio club -<paramref name="state"/> es la run, y su plantilla es la única que
    /// puede estar en <c>state.Roster</c>-, así que un contador del rival nunca paga a este club aunque
    /// lleve el mismo perk. Ordenadas por id de jugador y luego por nombre de contador (RT-041), heredado
    /// del orden de <see cref="RunMatchSummary.CounterDeltas"/>.
    /// </summary>
    private static IReadOnlyList<CounterGoldRow> CounterGoldRows(
        RunState state, RunMatchSummary summary, CounterGoldTable rates)
    {
        var deltas = summary.CounterDeltas;
        if (deltas.Count == 0 || rates.Count == 0)
        {
            return Array.Empty<CounterGoldRow>();
        }

        var rows = new List<CounterGoldRow>();
        for (int i = 0; i < deltas.Count; i++)
        {
            var delta = deltas[i];
            if (state.FindPlayer(delta.PlayerId) is null)
            {
                continue;
            }

            int rate = rates.RateFor(delta.Counter);
            if (rate == 0)
            {
                continue;
            }

            rows.Add(new CounterGoldRow(delta.PlayerId, delta.Counter, delta.Delta, rate, delta.Delta * rate));
        }

        return rows;
    }

    private static int SumGold(IReadOnlyList<CounterGoldRow> rows)
    {
        int total = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            total += rows[i].Gold;
        }

        return total;
    }
}

/// <summary>
/// Desglose del oro de una victoria (RF-114g..i, RF-119): de dónde sale cada moneda. Todo son datos
/// conocidos antes de jugar salvo si se cumplió el objetivo anunciado, que es lo único que el partido
/// decide (RF-114i: el oro nunca escala con el rendimiento).
/// </summary>
/// <param name="ActBase">Oro fijo por victoria del acto (RF-114g).</param>
/// <param name="Difficulty">Distintivo de dificultad del nodo, 1..5 (RF-012).</param>
/// <param name="DifficultyPercent">Multiplicador de esa dificultad, en tanto por ciento.</param>
/// <param name="AfterDifficulty">Oro tras aplicar la dificultad.</param>
/// <param name="NodeBonusPercent">Escalón por tipo de nodo, en tanto por ciento (ADR 0043).</param>
/// <param name="NodeBonus">Oro que suma ese escalón.</param>
/// <param name="Objective">Objetivo de partido excelente anunciado en el nodo (RF-114h).</param>
/// <param name="ObjectiveMet">True si se cumplió.</param>
/// <param name="ObjectiveBonus">Oro que suma el objetivo cumplido; 0 si no se cumplió.</param>
/// <param name="Total">Oro cobrado, idéntico al de <see cref="GoldCalculator.GoldForWin"/>. <b>No</b>
/// incluye el oro de contador, que es otro canal y se cobra también al perder (ADR 0113).</param>
public sealed record GoldForWinBreakdown(
    int Act,
    int ActBase,
    int Difficulty,
    int DifficultyPercent,
    int AfterDifficulty,
    NodeKind NodeKind,
    int NodeBonusPercent,
    int NodeBonus,
    ExcellentMatchObjective Objective,
    bool ObjectiveMet,
    int ObjectiveBonus,
    int Total);

/// <summary>
/// Una fila del oro que ha pagado un contador de partido (paquete Z, primitiva D). Es la mitad visible
/// del informe (RF-119) de un perk como "cada gol suyo llena la grada": sin esta fila, el oro que
/// paga aparecería mezclado en el total sin que el jugador pueda ver de dónde salió.
/// </summary>
/// <param name="PlayerId">Jugador propio cuyo contador ha generado el oro.</param>
/// <param name="Counter">Nombre del contador, el mismo id que declara el efecto <c>addCounter</c> del perk.</param>
/// <param name="Delta">Unidades que ese contador sumó en este partido.</param>
/// <param name="RatePerUnit">Oro que paga cada unidad, de <c>data/economy/counter-gold.json</c>.</param>
/// <param name="Gold">Oro de esta fila: <c>Delta * RatePerUnit</c> (RT-023, aritmética entera).</param>
public sealed record CounterGoldRow(int PlayerId, string Counter, int Delta, int RatePerUnit, int Gold);

/// <summary>
/// Oro que los contadores de un partido han pagado a la run (ADR 0113), con su desglose. Es un canal
/// <b>aparte</b> del premio de partido: el premio lo cobra quien gana (RF-114g), y esto se cobra siempre,
/// porque el jugador ya lo pagó por adelantado al gastar un slot en el perk. Un perk que solo rindiera en
/// las victorias no sería una inversión, sería una propina.
/// </summary>
/// <param name="Rows">Una fila por contador propio con tarifa; vacía si ninguno la tiene.</param>
/// <param name="Total">Suma de <see cref="CounterGoldRow.Gold"/> de todas las filas.</param>
public sealed record CounterGold(IReadOnlyList<CounterGoldRow> Rows, int Total)
{
    /// <summary>Ningún contador ha pagado nada: lo que devuelve un partido sin perks de negocio.</summary>
    public static CounterGold None { get; } = new(Array.Empty<CounterGoldRow>(), 0);
}
