using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run;

/// <summary>
/// Punto de decisión de sustitución (ADR 0094): en el tick <paramref name="Tick"/> el jugador
/// <paramref name="OutPlayerId"/> del equipo <paramref name="Team"/> dejó el campo (<paramref name="Detail"/>
/// <c>injury</c> o <c>death</c>) y hay <paramref name="Candidates"/> en el banquillo que podrían entrar.
/// </summary>
public sealed record SubstitutionPoint(int Team, int Tick, int OutPlayerId, string Detail, IReadOnlyList<PlayerDefinition> Candidates);

/// <summary>
/// Lo que comparten <c>/Game</c> (abrir la ventana) y <see cref="RunEngine"/> (resolverla sola): encontrar el
/// primer punto de decisión sin sustitución en un partido ya jugado, y volver a jugarlo con la decisión
/// añadida hasta que no quede ninguno. Es el mecanismo de RF-082 aplicado a la sustitución.
/// </summary>
public static class SubstitutionPoints
{
    /// <summary>
    /// Primer punto de decisión pendiente del equipo, o <c>null</c>. Un jugador cuenta como salido cuando
    /// hay un evento <c>INJURY</c> o <c>DEATH</c> suyo y el informe lo confirma (lesionado o muerto); los
    /// candidatos son los de la plantilla no alineados, sanos o con lesión leve, y no usados ya.
    /// </summary>
    public static SubstitutionPoint? Pending(MatchSetup setup, MatchResult result, int team)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(result);
        var side = team == 0 ? setup.Home : setup.Away;
        var events = result.Events;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Team != team || (e.Type != EventType.Injury && e.Type != EventType.Death))
            {
                continue;
            }

            // Un INJURY cancelado por un perk queda en el registro sin que nadie salga: lo que cuenta es el
            // tick en el que el informe dice que el jugador dejó el campo.
            if (LeftPitchTick(result.Report, e.Actor) != e.Tick || !IsLinedUp(side, e.Actor) || HasSubstitution(side, e.Actor))
            {
                continue;
            }

            var candidates = Candidates(side);
            if (candidates.Count == 0)
            {
                return null;
            }

            return new SubstitutionPoint(team, e.Tick, e.Actor, DiedAt(events, e.Actor, e.Tick) ? "death" : "injury", candidates);
        }

        return null;
    }

    /// <summary>
    /// Juega el partido y, mientras un equipo al que <paramref name="usesPolicy"/> dé el visto bueno tenga un
    /// punto de decisión pendiente, añade la sustitución de la política por defecto y vuelve a jugar. Converge
    /// en tantas vueltas como suplentes hay. Devuelve el estado inicial final (con las sustituciones) y su
    /// resultado, que es el que se aplica a la run y el que se reproduce (RT-024).
    /// </summary>
    public static (MatchSetup Setup, MatchResult Result) ResolveAutomatically(
        MatchSetup setup, ulong seed, Catalog catalog, SimConfig config, Func<int, bool>? usesPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(config);
        usesPolicy ??= static _ => true;
        var result = Simulator.Run(setup, seed, catalog, config);
        for (int round = 0; round < 32; round++)
        {
            // Siempre el punto MÁS TEMPRANO de los dos equipos: una sustitución en T solo cambia el partido
            // después de T, así que las anteriores siguen siendo válidas y las posteriores (calculadas sobre
            // un futuro que ya no existe) se descartan y se vuelven a resolver en la vuelta siguiente.
            SubstitutionPoint? point = null;
            for (int team = 0; team < 2; team++)
            {
                if (!usesPolicy(team))
                {
                    continue;
                }

                var candidate = Pending(setup, result, team);
                if (candidate is not null && (point is null || candidate.Tick < point.Tick))
                {
                    point = candidate;
                }
            }

            if (point is null)
            {
                return (setup, result);
            }

            var side = point.Team == 0 ? setup.Home : setup.Away;
            var outPlayer = FindPlayer(side, point.OutPlayerId);
            var chosen = SubstitutionPolicy.Default(point, outPlayer);
            setup = setup with
            {
                Home = WithSubstitution(setup.Home, point.Team == 0 ? new Substitution(point.Tick, point.OutPlayerId, chosen.Id) : null, point.Tick),
                Away = WithSubstitution(setup.Away, point.Team == 1 ? new Substitution(point.Tick, point.OutPlayerId, chosen.Id) : null, point.Tick),
            };
            result = Simulator.Run(setup, seed, catalog, config);
        }

        throw new InvalidOperationException("la resolución automática de sustituciones no converge (ADR 0094)");
    }

    /// <summary>Las sustituciones del equipo hasta <paramref name="tick"/> incluido, más <paramref name="added"/> si la hay.</summary>
    private static TeamSetup WithSubstitution(TeamSetup side, Substitution? added, int tick)
    {
        var kept = new List<Substitution>(side.Substitutions.Count + 1);
        for (int i = 0; i < side.Substitutions.Count; i++)
        {
            if (side.Substitutions[i].Tick <= tick)
            {
                kept.Add(side.Substitutions[i]);
            }
        }

        if (added is not null)
        {
            kept.Add(added);
        }

        return kept.Count == side.Substitutions.Count && added is null ? side : side with { Substitutions = kept };
    }

    private static int LeftPitchTick(MatchReport report, int playerId)
    {
        var players = report.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].PlayerId == playerId)
            {
                return players[i].LeftPitchTick;
            }
        }

        return -1;
    }

    private static bool DiedAt(IReadOnlyList<MatchEvent> events, int playerId, int tick)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type == EventType.Death && events[i].Actor == playerId && events[i].Tick == tick)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLinedUp(TeamSetup side, int playerId)
    {
        var slots = side.Lineup.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].PlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSubstitution(TeamSetup side, int outPlayerId)
    {
        var substitutions = side.Substitutions;
        for (int i = 0; i < substitutions.Count; i++)
        {
            if (substitutions[i].OutPlayerId == outPlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<PlayerDefinition> Candidates(TeamSetup side)
    {
        var candidates = new List<PlayerDefinition>();
        var players = side.Players;
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (IsLinedUp(side, player.Id) || player.PhysicalState is PhysicalState.SevereInjury or PhysicalState.Dead)
            {
                continue;
            }

            bool used = false;
            for (int j = 0; j < side.Substitutions.Count; j++)
            {
                used |= side.Substitutions[j].InPlayerId == player.Id;
            }

            if (!used)
            {
                candidates.Add(player);
            }
        }

        candidates.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return candidates;
    }

    private static PlayerDefinition FindPlayer(TeamSetup side, int playerId)
    {
        for (int i = 0; i < side.Players.Count; i++)
        {
            if (side.Players[i].Id == playerId)
            {
                return side.Players[i];
            }
        }

        throw new ArgumentException($"el jugador {playerId} no está en la plantilla de '{side.Id}'");
    }
}

/// <summary>Política por defecto de sustitución (ADR 0094): sano antes que tocado, misma posición que el que sale, y después el de mayor calidad. Empates por id ascendente.</summary>
public static class SubstitutionPolicy
{
    public static PlayerDefinition Default(SubstitutionPoint point, PlayerDefinition outPlayer)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(outPlayer);
        // Orden: sano antes que tocado (un lesionado leve que salta al campo multiplica su tirada letal, ADR
        // 0048: la política no manda a nadie a morir sin que lo pida la interfaz), misma posición, calidad, id.
        PlayerDefinition? best = null;
        int bestRank = int.MinValue;
        for (int i = 0; i < point.Candidates.Count; i++)
        {
            var candidate = point.Candidates[i];
            int rank = (candidate.PhysicalState == PhysicalState.Healthy ? 1_000_000 : 0)
                + (candidate.Position == outPlayer.Position ? 100_000 : 0)
                + Quality(candidate);
            if (best is null || rank > bestRank || (rank == bestRank && candidate.Id < best.Id))
            {
                best = candidate;
                bestRank = rank;
            }
        }

        return best ?? throw new ArgumentException("el punto de decisión no tiene candidatos", nameof(point));
    }

    private static int Quality(PlayerDefinition player)
    {
        var a = player.Attributes;
        return a.Strength + a.Speed + a.Technique + a.Stamina + a.Leash;
    }
}
