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
/// <param name="Total">Oro cobrado, idéntico al de <see cref="GoldCalculator.GoldForWin"/>.</param>
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
