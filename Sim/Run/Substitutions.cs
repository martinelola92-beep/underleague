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
            if (LeftPitchTick(result.Report, e.Actor) != e.Tick || !WasOnPitch(side, e.Actor) || HasSubstitution(side, e.Actor))
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
    /// en tantas vueltas como suplentes hay. Las sustituciones que ya trae <paramref name="setup"/> se tratan
    /// como respuestas del jugador a su punto de decisión y se aplican al llegar a él, sin política (BC-E).
    /// Devuelve el estado inicial final (con las sustituciones) y su
    /// resultado, que es el que se aplica a la run y el que se reproduce (RT-024).
    /// </summary>
    public static (MatchSetup Setup, MatchResult Result) ResolveAutomatically(
        MatchSetup setup, ulong seed, Catalog catalog, SimConfig config, Func<int, bool>? usesPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(config);
        usesPolicy ??= static _ => true;

        // BC-E: las sustituciones que ya trae el estado inicial (las que eligió el jugador en /Game) son
        // RESPUESTAS a un punto de decisión, no hechos fijos desde el tick 0. Se retiran del estado inicial y
        // se aplican cuando la resolución, en orden cronológico, llega a ese punto. Con ellas fijas desde el
        // principio, una sustitución del rival anterior a T —que se resuelve después— hacía divergir el
        // partido antes de T, el que sale ya no se lesionaba en T y el motor rechazaba la sustitución (9,4 %
        // de los puntos del jugador). Sin sustituciones de entrada (todo /Balance) nada cambia.
        var answers = Answers(setup);
        var used = new bool[answers.Count];
        if (answers.Count > 0)
        {
            setup = setup with
            {
                Home = setup.Home with { Substitutions = Array.Empty<Substitution>() },
                Away = setup.Away with { Substitutions = Array.Empty<Substitution>() },
            };
        }

        var result = Simulator.Run(setup, seed, catalog, config);
        for (int round = 0; round < 32; round++)
        {
            // Siempre el punto MÁS TEMPRANO de los dos equipos: una sustitución en T solo cambia el partido
            // después de T, así que las anteriores siguen siendo válidas y las posteriores (calculadas sobre
            // un futuro que ya no existe) se descartan y se vuelven a resolver en la vuelta siguiente.
            SubstitutionPoint? point = null;
            int pointAnswer = -1;
            for (int team = 0; team < 2; team++)
            {
                var candidate = Pending(setup, result, team);
                if (candidate is null)
                {
                    continue;
                }

                int answer = AnswerFor(answers, used, candidate);
                if (answer < 0 && !usesPolicy(team))
                {
                    continue;
                }

                if (point is null || candidate.Tick < point.Tick)
                {
                    point = candidate;
                    pointAnswer = answer;
                }
            }

            if (point is null)
            {
                // Una respuesta que el partido no llegó a pedir no se descarta en silencio: el llamador pidió
                // una sustitución que no corresponde a ningún punto de decisión, y eso es un error explícito
                // (ADR 0094: «lo demás es ArgumentException»; RT-032).
                for (int i = 0; i < answers.Count; i++)
                {
                    if (!used[i])
                    {
                        var (team, orphan) = answers[i];
                        throw new ArgumentException(
                            $"la sustitución del equipo {team} en el tick {orphan.Tick} (sale {orphan.OutPlayerId}, entra {orphan.InPlayerId}) no corresponde a ningún punto de decisión del partido");
                    }
                }

                return (setup, result);
            }

            var side = point.Team == 0 ? setup.Home : setup.Away;
            var outPlayer = FindPlayer(side, point.OutPlayerId);
            int chosenId;
            if (pointAnswer >= 0)
            {
                used[pointAnswer] = true;
                chosenId = answers[pointAnswer].Substitution.InPlayerId;
            }
            else
            {
                chosenId = SubstitutionPolicy.Default(point, outPlayer).Id;
            }
            setup = setup with
            {
                Home = WithSubstitution(setup.Home, point.Team == 0 ? new Substitution(point.Tick, point.OutPlayerId, chosenId) : null, point.Tick),
                Away = WithSubstitution(setup.Away, point.Team == 1 ? new Substitution(point.Tick, point.OutPlayerId, chosenId) : null, point.Tick),
            };
            result = Simulator.Run(setup, seed, catalog, config);
        }

        throw new InvalidOperationException("la resolución automática de sustituciones no converge (ADR 0094)");
    }

    /// <summary>Las sustituciones que trae el estado inicial, con su equipo, en el orden de la plantilla.</summary>
    private static List<(int Team, Substitution Substitution)> Answers(MatchSetup setup)
    {
        var answers = new List<(int Team, Substitution Substitution)>();
        for (int i = 0; i < setup.Home.Substitutions.Count; i++)
        {
            answers.Add((0, setup.Home.Substitutions[i]));
        }

        for (int i = 0; i < setup.Away.Substitutions.Count; i++)
        {
            answers.Add((1, setup.Away.Substitutions[i]));
        }

        return answers;
    }

    /// <summary>
    /// La respuesta a <paramref name="point"/>: mismo equipo, mismo tick, mismo jugador que sale y un
    /// candidato que siga siendo legal. Si el partido ya no llega a ese punto, o el elegido dejó de ser
    /// candidato, no hay respuesta: el punto queda para la política o, si el equipo no la usa, pendiente, y la
    /// respuesta sin usar hace fallar la resolución al terminar. Devuelve su índice, o -1.
    /// </summary>
    private static int AnswerFor(List<(int Team, Substitution Substitution)> answers, bool[] used, SubstitutionPoint point)
    {
        for (int i = 0; i < answers.Count; i++)
        {
            var (team, answer) = answers[i];
            if (used[i] || team != point.Team || answer.Tick != point.Tick || answer.OutPlayerId != point.OutPlayerId)
            {
                continue;
            }

            for (int j = 0; j < point.Candidates.Count; j++)
            {
                if (point.Candidates[j].Id == answer.InPlayerId)
                {
                    return i;
                }
            }
        }

        return -1;
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

    /// <summary>
    /// Si ese jugador murió de verdad en ese tick. Una muerte <b>anulada</b> por un perk ("Prohibido
    /// morir") queda en el registro con el detalle sufijado <c>:cancelled</c> y no cuenta: el jugador
    /// sigue vivo, y si además deja el campo es por la lesión, que es lo que hay que decirle a quien
    /// abre la ventana de sustitución.
    /// </summary>
    private static bool DiedAt(IReadOnlyList<MatchEvent> events, int playerId, int tick)
    {
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Type == EventType.Death
                && e.Actor == playerId
                && e.Tick == tick
                && !e.Detail.EndsWith(":cancelled", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Si ese jugador estaba en el campo: en el once inicial **o** habiendo entrado por una sustitución
    /// anterior de este mismo partido.
    ///
    /// <para>BA-B: antes solo miraba <c>Lineup.Slots</c>, que es el once de salida. Un suplente que ya había
    /// entrado no figura ahí, así que **al lesionarse él no se abría ninguna ventana de sustitución** y el
    /// partido se quedaba esperando una decisión que el jugador no podía tomar — con la única salida de
    /// cerrar el juego, y con guardado ironman (RT-061) eso es perder la run. Se veía sobre todo en un jefe,
    /// que es donde hay más bajas por partido.</para>
    /// </summary>
    private static bool WasOnPitch(TeamSetup side, int playerId)
    {
        if (IsLinedUp(side, playerId))
        {
            return true;
        }

        var substitutions = side.Substitutions;
        for (int i = 0; i < substitutions.Count; i++)
        {
            if (substitutions[i].InPlayerId == playerId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Si ese jugador salió en el once inicial. Lo usa <c>Candidates</c> para no proponer a un titular.</summary>
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
