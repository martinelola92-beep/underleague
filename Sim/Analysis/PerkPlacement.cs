using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run;

namespace Underleague.Sim.Analysis;

/// <summary>
/// <b>Lo que un jugador razonable lee en la descripción de un perk antes de dárselo a alguien</b>: si la
/// condición habla de <i>dónde empieza</i>, <i>con quién está</i> o <i>qué es</i> el portador, se puede
/// saber antes del partido si se va a cumplir. La política automática de <see cref="RunPolicy"/> lo usa
/// para no comprar un perk que en ese portador solo va a aplicar su castigo (<c>elseEffects</c>), que es
/// exactamente cómo está construido el escalón "incoherente" de la ADR 0033.
///
/// <para><b>Es una lectura, no una evaluación.</b> El motor compila las condiciones y las evalúa con el
/// contexto del evento (<c>ConditionCompiler</c>), y ese contexto no existe fuera del partido. Aquí se
/// reconocen únicamente los cinco predicados sobre <c>owner</c> que dependen solo de la plantilla y de
/// la colocación —<c>hasTag</c>, <c>startsIn</c>, <c>startsOn</c>, <c>linked</c> y
/// <c>teammatesWithTag</c>— combinados con <c>||</c>. Cualquier otra cosa (todo lo que mira al
/// <c>actor</c>, al marcador o al reloj) se considera <b>no juzgable</b> y el perk se da por válido: la
/// regla solo puede rechazar cuando está segura, nunca cuando duda.</para>
///
/// <para>Cuando la pantalla de plantilla quiera dar el mismo aviso al jugador (RF-012d), lo correcto
/// será un evaluador estático sobre el AST en <c>Sim/Perks</c>; esto es lo que la medida necesita hoy y
/// vive en <c>Sim/Analysis</c> por eso.</para>
/// </summary>
public static class PerkPlacement
{
    /// <summary>
    /// True si el perk <b>encaja</b> en ese portador con la alineación dada: su condición se cumple, o
    /// no es juzgable fuera del partido, o el perk no castiga cuando falla (sin <c>elseEffects</c>, un
    /// perk que no se activa no resta, solo no suma).
    /// </summary>
    public static bool Fits(PerkDefinition perk, int carrierId, Lineup lineup, RunState state)
    {
        ArgumentNullException.ThrowIfNull(perk);
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(state);

        string source = perk.Condition;
        if (source.Length == 0)
        {
            return true;
        }

        var carrier = state.FindPlayer(carrierId);
        if (carrier is null)
        {
            return false;
        }

        bool anyJudged = false;
        foreach (var term in source.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var verdict = Judge(term, carrier, lineup, state);
            if (verdict is null)
            {
                // Un término no juzgable hace no juzgable a toda la disyunción: podría ser el que se
                // cumple.
                return true;
            }

            anyJudged = true;
            if (verdict.Value)
            {
                return true;
            }
        }

        return !anyJudged;
    }

    /// <summary>True, false o null (no juzgable fuera del partido) para un término de la condición.</summary>
    private static bool? Judge(string term, RunPlayer carrier, Lineup lineup, RunState state)
    {
        if (Argument(term, "hasTag(owner,") is { } tag)
        {
            return carrier.Tags.Contains(tag) || string.Equals(carrier.StyleTag.ToString(), tag, StringComparison.Ordinal);
        }

        if (Argument(term, "startsIn(owner,") is { } zone)
        {
            var home = HomeOf(lineup, carrier.Id);
            return home is { } cell && LinkGeometry.ZoneOfHome(cell, 0).ToString() == zone;
        }

        if (Argument(term, "startsOn(owner,") is { } flank)
        {
            var home = HomeOf(lineup, carrier.Id);
            return home is { } cell && LinkGeometry.FlankOfHome(cell, 0).ToString() == flank;
        }

        if (Argument(term, "linked(owner,") is { } relationName)
        {
            if (!Enum.TryParse<LinkRelation>(relationName, ignoreCase: true, out var relation))
            {
                return null;
            }

            var home = HomeOf(lineup, carrier.Id);
            if (home is not { } cell)
            {
                return false;
            }

            for (int i = 0; i < lineup.Slots.Count; i++)
            {
                if (lineup.Slots[i].PlayerId != carrier.Id
                    && LinkGeometry.Matches(cell, lineup.Slots[i].HomeCell, 0, relation))
                {
                    return true;
                }
            }

            return false;
        }

        if (Argument(term, "teammatesWithTag(owner,") is { } teamTag)
        {
            int threshold = TrailingThreshold(term);
            if (threshold < 0)
            {
                return null;
            }

            int count = 0;
            for (int i = 0; i < lineup.Slots.Count; i++)
            {
                if (lineup.Slots[i].PlayerId == carrier.Id)
                {
                    continue;
                }

                var mate = state.FindPlayer(lineup.Slots[i].PlayerId);
                if (mate is not null
                    && (mate.Tags.Contains(teamTag) || string.Equals(mate.StyleTag.ToString(), teamTag, StringComparison.Ordinal)))
                {
                    count++;
                }
            }

            return count > threshold;
        }

        return null;
    }

    /// <summary>Primer argumento entrecomillado de <paramref name="call"/> dentro de <paramref name="term"/>, o null.</summary>
    private static string? Argument(string term, string call)
    {
        int start = term.IndexOf(call, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        int open = term.IndexOf('\'', start + call.Length);
        if (open < 0)
        {
            return null;
        }

        int close = term.IndexOf('\'', open + 1);
        return close < 0 ? null : term[(open + 1)..close];
    }

    /// <summary>Umbral de un término <c>... &gt; N</c>; -1 si no lo tiene con esa forma exacta.</summary>
    private static int TrailingThreshold(string term)
    {
        int at = term.LastIndexOf('>');
        return at >= 0 && int.TryParse(term[(at + 1)..].Trim(), out int value) ? value : -1;
    }

    private static Cell? HomeOf(Lineup lineup, int playerId)
    {
        for (int i = 0; i < lineup.Slots.Count; i++)
        {
            if (lineup.Slots[i].PlayerId == playerId)
            {
                return lineup.Slots[i].HomeCell;
            }
        }

        return null;
    }
}
