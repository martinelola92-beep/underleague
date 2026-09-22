using System.Globalization;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>Dirección y hecho de un knaveo (BE-B, ADR 0124): ver el comentario de <see cref="RunState.RivalCreditPrefix"/>.</summary>
public enum RivalCreditKind
{
    CausedInjury,
    CausedDeath,
    SufferedInjury,
    SufferedDeath,
}

/// <summary>
/// Un hecho acreditado entre un jugador <b>propio</b> y un jugador <b>rival</b> concreto (BE-B, ADR
/// 0124): cuántas veces, en lo que va de run, <see cref="OwnPlayerId"/> ha causado o sufrido esa lesión o
/// muerte a manos del rival en el índice <see cref="RivalIndex"/> del clan <see cref="RivalId"/> (índice
/// dentro de <c>data/rivals/&lt;RivalId&gt;.json</c>, ver <c>RivalTeamBuilder.OpponentFirstPlayerId</c>).
/// </summary>
public sealed record RivalCredit(string RivalId, int RivalIndex, int OwnPlayerId, RivalCreditKind Kind, int Count);

/// <summary>
/// Lectura pura sobre <see cref="RunState.Counters"/> del par knaveador-víctima que
/// <see cref="RunState.RivalCreditPrefix"/> documenta (BE-B, enmienda de la ADR 0124). Sin estado, sin
/// RNG, sin E/S -mismo patrón que <see cref="RivalHistory"/>, pero para el par de jugadores en vez del
/// enfrentamiento contra el clan entero.
/// <para><b>Sin cronología</b>: el contador es una cuenta acumulada (RT-023), no una bitácora de sucesos
/// con orden temporal, así que esta clase no puede decir "lo último que pasó" -solo "cuánto ha pasado".
/// <see cref="MostNotable"/> elige de forma determinista (muerte antes que lesión; a igualdad de
/// severidad, más repeticiones antes que menos; los empates residuales los resuelve el orden ya
/// determinista de <see cref="Against"/>, que recorre <see cref="RunState.Counters"/> -un
/// <c>SortedDictionary</c> ordinal, RT-041-), no por fecha.</para>
/// </summary>
public static class RivalCredits
{
    /// <summary>Todos los hechos acreditados contra ese rival, en el orden determinista de sus claves.</summary>
    public static IReadOnlyList<RivalCredit> Against(RunState state, string rivalId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrEmpty(rivalId);

        var credits = new List<RivalCredit>();
        string prefix = RunState.RivalCreditPrefix + rivalId + ":";
        foreach (var (key, count) in state.Counters)
        {
            if (count <= 0 || !key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParse(key, prefix.Length, out int rivalIndex, out int ownPlayerId, out RivalCreditKind kind))
            {
                continue;
            }

            credits.Add(new RivalCredit(rivalId, rivalIndex, ownPlayerId, kind, count));
        }

        return credits;
    }

    /// <summary>
    /// El hecho más destacado contra ese rival, o null si no hay ninguno todavía. "Destacado" es
    /// determinista, <b>no</b> cronológico (ver el comentario de la clase).
    /// </summary>
    public static RivalCredit? MostNotable(RunState state, string rivalId)
    {
        var all = Against(state, rivalId);
        RivalCredit? best = null;
        int bestRank = -1;
        for (int i = 0; i < all.Count; i++)
        {
            int rank = Rank(all[i]);
            if (rank > bestRank)
            {
                bestRank = rank;
                best = all[i];
            }
        }

        return best;
    }

    private static int Rank(RivalCredit credit)
    {
        bool isDeath = credit.Kind is RivalCreditKind.CausedDeath or RivalCreditKind.SufferedDeath;
        return ((isDeath ? 1 : 0) * 1_000_000) + credit.Count;
    }

    private static bool TryParse(string key, int start, out int rivalIndex, out int ownPlayerId, out RivalCreditKind kind)
    {
        rivalIndex = 0;
        ownPlayerId = 0;
        kind = default;

        string remainder = key[start..];
        int firstColon = remainder.IndexOf(':');
        if (firstColon < 0)
        {
            return false;
        }

        int secondColon = remainder.IndexOf(':', firstColon + 1);
        if (secondColon < 0)
        {
            return false;
        }

        if (!int.TryParse(remainder[..firstColon], NumberStyles.Integer, CultureInfo.InvariantCulture, out rivalIndex))
        {
            return false;
        }

        if (!int.TryParse(remainder[(firstColon + 1)..secondColon], NumberStyles.Integer, CultureInfo.InvariantCulture, out ownPlayerId))
        {
            return false;
        }

        string suffix = remainder[(secondColon + 1)..];
        switch (suffix)
        {
            case "causedInjury":
                kind = RivalCreditKind.CausedInjury;
                return true;
            case "causedDeath":
                kind = RivalCreditKind.CausedDeath;
                return true;
            case "sufferedInjury":
                kind = RivalCreditKind.SufferedInjury;
                return true;
            case "sufferedDeath":
                kind = RivalCreditKind.SufferedDeath;
                return true;
            default:
                return false;
        }
    }
}
