using Underleague.Sim.Run.Systems.Bets;

namespace Underleague.Sim.Analysis;

/// <summary>Una celda del censo de aciertos: una condición de apuesta en una dificultad.</summary>
/// <param name="Taken">Apuestas tomadas y resueltas.</param>
/// <param name="Met">De ellas, las que se cumplieron.</param>
/// <param name="NetGold">Oro neto (cobrado menos apostado).</param>
/// <param name="AnnouncedBasisPoints">Frecuencia de <c>data/bets/bets.json</c> (la <c>p</c> de la cuota y de las palabras de la UI), en centésimas de punto.</param>
public sealed record BetHitCell(BetKind Kind, string BetId, int Difficulty, long Taken, long Met, long NetGold, int AnnouncedBasisPoints)
{
    /// <summary>Porcentaje medido de cumplimiento (0 si no se tomó ninguna).</summary>
    public double MeasuredPercent => Taken == 0 ? 0 : 100.0 * Met / Taken;

    /// <summary>Porcentaje anunciado.</summary>
    public double AnnouncedPercent => AnnouncedBasisPoints / 100.0;

    /// <summary>Error típico binomial del porcentaje medido, en puntos (0 si no hay datos).</summary>
    public double StdErrPercent
    {
        get
        {
            if (Taken == 0)
            {
                return 0;
            }

            double p = (double)Met / Taken;
            return 100.0 * Math.Sqrt(p * (1 - p) / Taken);
        }
    }
}

/// <summary>
/// ADR 0157 (enmienda del 2 oct 2026): de las apuestas que una política tomó, cuántas se cumplieron por condición y
/// dificultad, para contrastarlo con la frecuencia que el juego anuncia (RF-012d). Suma <see cref="RunPlayResult.BetCells"/>
/// en orden de run, sin RNG.
/// </summary>
public static class BetHitCensus
{
    /// <summary>Una celda por (condición, dificultad), en orden de condición y dificultad ascendentes.</summary>
    public static IReadOnlyList<BetHitCell> Compute(IReadOnlyList<RunPlayResult> runs, BetCatalog bets)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(bets);

        var taken = new long[RunPolicy.BetCellCount];
        var met = new long[RunPolicy.BetCellCount];
        var net = new long[RunPolicy.BetCellCount];
        for (int r = 0; r < runs.Count; r++)
        {
            if (runs[r].BetCells is not { } cells)
            {
                continue;
            }

            for (int c = 0; c < RunPolicy.BetCellCount; c++)
            {
                taken[c] += cells[(c * 3)];
                met[c] += cells[(c * 3) + 1];
                net[c] += cells[(c * 3) + 2];
            }
        }

        var result = new List<BetHitCell>();
        foreach (BetKind kind in Enum.GetValues<BetKind>().OrderBy(k => (int)k))
        {
            var definition = bets.Find(kind);
            for (int d = 1; d <= 5; d++)
            {
                int c = ((int)kind * 5) + (d - 1);
                result.Add(new BetHitCell(
                    kind,
                    definition?.Id ?? kind.ToString(),
                    d,
                    taken[c],
                    met[c],
                    net[c],
                    definition?.FrequencyBasisPointsFor(d) ?? 0));
            }
        }

        return result;
    }
}
