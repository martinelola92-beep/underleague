using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Un némesis listo para enseñarse (ADR 0165): quién es, con su título y su clan actual, y a quién mató y en
/// qué acto. Sin referencias internas: todo son nombres.
/// </summary>
public sealed record NemesisLine(
    int NemesisId,
    string Name,
    string Title,
    string Clan,
    string ClanId,
    int Slot,
    string VictimName,
    int Act,
    int Kills,
    bool Active);

/// <summary>Traduce la <see cref="RivalMemory"/> a lo que el ojeo, el mapa y la Gaceta enseñan (ADR 0165). Puro, sin E/S.</summary>
public static class NemesisView
{
    /// <summary>Los némesis vivos que juegan en el clan de ese nodo de partido de catálogo; vacío en jefe, en nodos sin rival o sin némesis.</summary>
    public static IReadOnlyList<NemesisLine> ForNode(RunState state, NemesisCatalog nemesis, MapNode node, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nemesis);
        ArgumentNullException.ThrowIfNull(node);
        if (node.OpponentId.Length == 0 || !NodeKinds.IsCatalogRivalMatch(node.Kind))
        {
            return Array.Empty<NemesisLine>();
        }

        var team = nemesis.Rivals.Find(node.OpponentId);
        if (team is null)
        {
            return Array.Empty<NemesisLine>();
        }

        var lines = new List<NemesisLine>();
        var active = state.RivalMemory.ActiveIn(team.ClanId);
        for (int i = 0; i < active.Count; i++)
        {
            lines.Add(Describe(active[i], nemesis, language));
        }

        return lines;
    }

    /// <summary>
    /// El primer némesis (por id) que <b>ya lo era</b> al empezar un partido y estaba en el campo rival: su nombre
    /// está entre <paramref name="rivalPlayerNames"/> y no figura en <paramref name="madeThisMatch"/> (los que
    /// nacieron en ese mismo partido no eran némesis al pitar el inicio). Vivo o muerto después: el pregón del
    /// saque anuncia lo que había antes del pitido. Null si ninguno.
    /// </summary>
    public static NemesisLine? OnPitch(
        RunState state,
        NemesisCatalog nemesis,
        IEnumerable<string> rivalPlayerNames,
        IReadOnlyList<NemesisMade> madeThisMatch,
        string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nemesis);
        ArgumentNullException.ThrowIfNull(rivalPlayerNames);
        ArgumentNullException.ThrowIfNull(madeThisMatch);
        var names = new HashSet<string>(rivalPlayerNames, StringComparer.Ordinal);
        for (int i = 0; i < state.RivalMemory.Nemeses.Count; i++)
        {
            var n = state.RivalMemory.Nemeses[i];
            bool fresh = false;
            for (int m = 0; m < madeThisMatch.Count; m++)
            {
                fresh |= madeThisMatch[m].NemesisId == n.Id;
            }

            if (!fresh && names.Contains(n.Name))
            {
                return Describe(n, nemesis, language);
            }
        }

        return null;
    }

    /// <summary>
    /// El némesis que la Gaceta prefiere como villano (ADR 0165 punto 4): entre los vivos y los muertos a manos
    /// del jugador (vengados), el que más muertes propias suma; a igual cuenta, el de id menor. Null si no hubo ninguno.
    /// </summary>
    public static NemesisLine? Villain(RunState state, NemesisCatalog nemesis, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nemesis);
        RivalNemesis? best = null;
        var all = state.RivalMemory.Nemeses;
        for (int i = 0; i < all.Count; i++)
        {
            if (best is null || all[i].Kills > best.Kills)
            {
                best = all[i];
            }
        }

        return best is null ? null : Describe(best, nemesis, language);
    }

    /// <summary>Todos los némesis de la run, por id, vivos y muertos.</summary>
    public static IReadOnlyList<NemesisLine> All(RunState state, NemesisCatalog nemesis, string language = "es")
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nemesis);
        var lines = new List<NemesisLine>(state.RivalMemory.Nemeses.Count);
        for (int i = 0; i < state.RivalMemory.Nemeses.Count; i++)
        {
            lines.Add(Describe(state.RivalMemory.Nemeses[i], nemesis, language));
        }

        return lines;
    }

    private static NemesisLine Describe(RivalNemesis n, NemesisCatalog nemesis, string language)
    {
        var title = nemesis.Find(n.TitleId);
        var clan = nemesis.Rivals.ClanName(n.ClanId);
        bool english = string.Equals(language, "en", StringComparison.Ordinal);
        return new NemesisLine(
            n.Id,
            n.Name,
            title is null ? string.Empty : title.NameIn(language),
            clan is null ? string.Empty : english ? clan.En : clan.Es,
            n.ClanId,
            n.Slot,
            n.VictimName,
            n.Act,
            n.Kills,
            n.IsActive);
    }
}
