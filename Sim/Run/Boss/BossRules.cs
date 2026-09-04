using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;

namespace Underleague.Sim.Run.Bosses;

/// <summary>
/// Aplicación de los modificadores de regla de un jefe (RF-001b, RF-001c) y de su condición de derrota
/// propia (RF-001c, D-9). Pura y determinista: transforma el <see cref="MatchSetup"/> antes de simular
/// y recorre siempre a los jugadores por id ascendente (RT-041), de modo que el mismo jefe contra la
/// misma alineación produce siempre el mismo partido.
///
/// <para><b>Por qué se aplica sobre el <see cref="MatchSetup"/> y no dentro del motor</b>: el motor no
/// puede saber que existe un jefe (RT-011, <c>/Sim/Engine</c> solo conoce equipos y árbitro). Un
/// modificador es, por construcción, una transformación de la entrada del partido: quita perks, mueve
/// casillas-hogar. Eso lo hace además <b>anticipable</b> (RF-012d): el informe de ojeo puede construir
/// el mismo <see cref="MatchSetup"/> transformado y enseñar exactamente con qué once se va a jugar.</para>
/// </summary>
public static class BossRules
{
    /// <summary>
    /// Devuelve el <see cref="MatchSetup"/> con los modificadores aplicados al equipo del jugador
    /// (<paramref name="playerTeamIndex"/> 0 = local, 1 = visitante). Los modificadores se aplican en el
    /// orden en que los declara el jefe.
    /// </summary>
    public static MatchSetup Apply(
        MatchSetup setup, int playerTeamIndex, IReadOnlyList<BossModifier> modifiers, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(modifiers);
        ArgumentNullException.ThrowIfNull(catalog);
        if (playerTeamIndex is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(playerTeamIndex), playerTeamIndex, "el equipo del jugador es 0 o 1");
        }

        if (modifiers.Count == 0)
        {
            return setup;
        }

        var team = playerTeamIndex == 0 ? setup.Home : setup.Away;
        for (int i = 0; i < modifiers.Count; i++)
        {
            team = Apply(team, modifiers[i], catalog);
        }

        return playerTeamIndex == 0 ? setup with { Home = team } : setup with { Away = team };
    }

    /// <summary>Aplica un solo modificador a un equipo.</summary>
    public static TeamSetup Apply(TeamSetup team, BossModifier modifier, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(catalog);

        return modifier.Kind switch
        {
            BossModifierKind.SingleCopy => SingleCopy(team),
            BossModifierKind.MarkStar => MarkStar(team),
            BossModifierKind.BanChannel => BanChannel(team, modifier.Probability, catalog),
            BossModifierKind.PushBack => PushBack(team, modifier.Column),
            _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier.Kind, "modificador de jefe desconocido"),
        };
    }

    /// <summary>
    /// True si el jugador ha superado la puerta: ganó el partido <b>y</b> no se cumplió la condición de
    /// derrota propia del jefe (RF-001c). <paramref name="playerTeamIndex"/> es 0 o 1.
    /// </summary>
    public static bool Passed(BossDefinition boss, MatchReport report, int playerTeamIndex)
    {
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(report);
        return report.Winner == playerTeamIndex && !DefeatConditionMet(boss.DefeatCondition, report);
    }

    /// <summary>
    /// True si se cumplió la condición de derrota propia del jefe. <see cref="BossDefeatConditionKind.DrawIsDefeat"/>
    /// se lee sobre <c>WentToGoldenGoal</c>: el motor no deja empates (RF-055b lo resuelve a gol de oro),
    /// así que "llegar empatado al final del tiempo reglamentario" es exactamente haber entrado en la
    /// prórroga de la turba.
    /// </summary>
    public static bool DefeatConditionMet(BossDefeatCondition? condition, MatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return condition?.Kind switch
        {
            BossDefeatConditionKind.DrawIsDefeat => report.WentToGoldenGoal,
            _ => false,
        };
    }

    // ------------------------------------------------------------------ modificadores

    /// <summary>Un perk repetido en el once solo cuenta en su portador de id más bajo.</summary>
    private static TeamSetup SingleCopy(TeamSetup team)
    {
        var starters = StarterIds(team);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var players = new List<PlayerDefinition>(team.Players.Count);
        foreach (var player in team.Players.OrderBy(p => p.Id))
        {
            if (!starters.Contains(player.Id) || player.Perks.Count == 0)
            {
                players.Add(player);
                continue;
            }

            var kept = new List<string>(player.Perks.Count);
            foreach (var perkId in player.Perks)
            {
                if (seen.Add(perkId))
                {
                    kept.Add(perkId);
                }
            }

            players.Add(kept.Count == player.Perks.Count ? player : player with { Perks = kept });
        }

        return team with { Players = SortById(players) };
    }

    /// <summary>
    /// El titular <b>de campo</b> con más perks juega marcado y pierde todos los suyos. El portero queda
    /// fuera a propósito: no se marca al hombre a un portero, y con él dentro el desempate por suma de
    /// atributos lo elegía casi siempre (medido: el modificador pasaba a ser "el jefe te anula el
    /// portero", que vale 30 puntos de tasa de victoria y no mide nada de la construcción).
    /// </summary>
    private static TeamSetup MarkStar(TeamSetup team)
    {
        var starters = StarterIds(team);
        PlayerDefinition? star = null;
        foreach (var player in team.Players.OrderBy(p => p.Id))
        {
            if (!starters.Contains(player.Id) || player.Perks.Count == 0 || player.Position == Position.Goalkeeper)
            {
                continue;
            }

            if (star is null || Ranks(player, star))
            {
                star = player;
            }
        }

        if (star is null)
        {
            return team;
        }

        var players = new List<PlayerDefinition>(team.Players.Count);
        foreach (var player in team.Players)
        {
            players.Add(player.Id == star.Id ? player with { Perks = Array.Empty<string>() } : player);
        }

        return team with { Players = SortById(players) };
    }

    /// <summary>Desempate del marcaje: más perks, luego mayor suma de atributos, luego menor id.</summary>
    private static bool Ranks(PlayerDefinition candidate, PlayerDefinition current)
    {
        if (candidate.Perks.Count != current.Perks.Count)
        {
            return candidate.Perks.Count > current.Perks.Count;
        }

        int candidateTotal = Total(candidate.Attributes);
        int currentTotal = Total(current.Attributes);
        return candidateTotal != currentTotal ? candidateTotal > currentTotal : candidate.Id < current.Id;
    }

    private static int Total(Attributes a) => a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;

    /// <summary>Los perks que tocan ese canal de probabilidad no se aplican: se retiran del once.</summary>
    private static TeamSetup BanChannel(TeamSetup team, ProbabilityKind probability, Catalog catalog)
    {
        var starters = StarterIds(team);
        var players = new List<PlayerDefinition>(team.Players.Count);
        foreach (var player in team.Players)
        {
            if (!starters.Contains(player.Id) || player.Perks.Count == 0)
            {
                players.Add(player);
                continue;
            }

            var kept = new List<string>(player.Perks.Count);
            foreach (var perkId in player.Perks)
            {
                var perk = catalog.Perks.Find(perkId);
                if (perk is null || !TouchesChannel(perk, probability))
                {
                    kept.Add(perkId);
                }
            }

            players.Add(kept.Count == player.Perks.Count ? player : player with { Perks = kept });
        }

        return team with { Players = SortById(players) };
    }

    /// <summary>
    /// True si el perk <b>sube</b> ese canal, es decir si alguno de sus <c>effects</c> (la rama que se
    /// aplica cuando la condición se cumple) lo modifica. Los <c>elseEffects</c> no cuentan a propósito:
    /// el modificador apaga los perks que compran el canal, no los que castigan por no cumplirse.
    /// </summary>
    public static bool TouchesChannel(PerkDefinition perk, ProbabilityKind probability)
    {
        ArgumentNullException.ThrowIfNull(perk);
        return Touches(perk.Effects, probability);
    }

    private static bool Touches(IReadOnlyList<EffectDefinition> effects, ProbabilityKind probability)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].Type == EffectType.ModifyProbability && effects[i].Probability == probability)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ningún titular empieza por delante de <paramref name="maxColumn"/>. El que esté más adelantado
    /// retrocede a la casilla libre más avanzada: misma fila si se puede y, si no, la fila más cercana
    /// (desempate hacia la fila baja), columna a columna hacia la portería propia. Recorrido por id
    /// ascendente, así que dos alineaciones iguales dan siempre el mismo resultado.
    /// </summary>
    private static TeamSetup PushBack(TeamSetup team, int maxColumn)
    {
        var occupied = new HashSet<Cell>();
        foreach (var slot in team.Lineup.Slots)
        {
            occupied.Add(slot.HomeCell);
        }

        var moved = new Dictionary<int, Cell>();
        foreach (var slot in team.Lineup.Slots.OrderBy(s => s.PlayerId))
        {
            if (slot.HomeCell.Column <= maxColumn)
            {
                continue;
            }

            occupied.Remove(slot.HomeCell);
            var target = FreeCell(occupied, slot.HomeCell, maxColumn);
            occupied.Add(target);
            moved[slot.PlayerId] = target;
        }

        if (moved.Count == 0)
        {
            return team;
        }

        var slots = new List<LineupSlot>(team.Lineup.Slots.Count);
        foreach (var slot in team.Lineup.Slots)
        {
            slots.Add(moved.TryGetValue(slot.PlayerId, out var cell) ? slot with { HomeCell = cell } : slot);
        }

        return team with { Lineup = new Lineup(slots) };
    }

    private static Cell FreeCell(HashSet<Cell> occupied, Cell from, int maxColumn)
    {
        for (int column = maxColumn; column >= 0; column--)
        {
            for (int distance = 0; distance < Pitch.Rows; distance++)
            {
                for (int sign = distance == 0 ? 1 : -1; sign <= 1; sign += 2)
                {
                    int row = from.Row + (sign * distance);
                    if (row < 0 || row >= Pitch.Rows)
                    {
                        continue;
                    }

                    var candidate = new Cell(column, row);
                    if (!occupied.Contains(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            $"no queda ninguna casilla libre por detrás de la columna {maxColumn} para retrasar al titular de {from}");
    }

    private static HashSet<int> StarterIds(TeamSetup team)
    {
        var ids = new HashSet<int>();
        foreach (var slot in team.Lineup.Slots)
        {
            ids.Add(slot.PlayerId);
        }

        return ids;
    }

    private static List<PlayerDefinition> SortById(List<PlayerDefinition> players)
    {
        players.Sort((a, b) => a.Id.CompareTo(b.Id));
        return players;
    }
}
