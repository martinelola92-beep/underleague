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
    public static int GoldForWin(RunState state, MapNode node, RunMatchSummary summary, EconomyConfig economy)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(economy);

        int gold = economy.GoldForAct(node.Act) * economy.MultiplierForDifficulty(node.Difficulty) / 100;

        if (node.Kind == NodeKind.EliteMatch)
        {
            gold += gold * economy.EliteBonusPercent / 100;
        }
        else if (node.Kind == NodeKind.Boss)
        {
            gold += gold * economy.BossBonusPercent / 100;
        }

        var objective = ExcellentMatchObjectives.For(state.Seed, node);
        if (ExcellentMatchObjectives.Satisfied(objective, state, summary))
        {
            gold += economy.ExcellentMatchBonusGold;
        }

        return gold;
    }
}
