using Underleague.Sim.Data;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Random;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>Un rival que acaba de convertirse en némesis en este partido (ADR 0165).</summary>
public sealed record NemesisMade(
    int NemesisId, string TitleId, string Name, string ClanId, int VictimPlayerId, string VictimName, int Act);

/// <summary>Una venganza cobrada en este partido (ADR 0165): <paramref name="Slain"/> si el némesis murió.</summary>
public sealed record NemesisRevenge(
    int NemesisId, string TitleId, string NemesisName, string VictimName, int AvengerPlayerId, string AvengerName, bool Slain);

/// <summary>Resultado de aplicar un partido a la memoria de rivales.</summary>
public sealed record NemesisOutcome(
    RunState State,
    IReadOnlyList<NemesisMade> Made,
    IReadOnlyList<NemesisRevenge> Revenges,
    int Capped)
{
    /// <summary>Sin nada que anotar.</summary>
    public static NemesisOutcome Unchanged(RunState state) =>
        new(state, Array.Empty<NemesisMade>(), Array.Empty<NemesisRevenge>(), 0);
}

/// <summary>
/// La regla de némesis (ADR 0165, RF-015 enmendada), pura y determinista: sin E/S, sin reloj, con flujos de
/// RNG derivados de la semilla de la run y nunca del de partido (RT-022).
///
/// <list type="bullet">
/// <item><b>Némesis</b>: un rival de clan que MATA a un jugador propio gana título y un nivel; con el tope de
/// vivos, el asesino no se convierte y se anota.</item>
/// <item><b>Venganza</b>: un jugador propio que lesiona o mata a un némesis suma una venganza a su carrera y
/// la run cobra su oro; un némesis muerto deja de serlo. Una venganza por némesis y partido.</item>
/// <item><b>Muertos</b>: un rival de clan que muere no vuelve; su puesto lo cubre un fichaje.</item>
/// <item><b>Traspaso</b>: al entrar en el acto siguiente, cada némesis vivo pasa a otro clan.</item>
/// </list>
/// </summary>
public static class NemesisSystem
{
    /// <summary>Sufijo con el que el motor marca un evento anulado por un perk.</summary>
    private const string CancelledSuffix = ":cancelled";

    /// <summary>
    /// Flujos propios de la run (RT-022), sobre <see cref="OfferStream"/>: el título de un némesis usa el
    /// «nodo» del partido y <see cref="TitleStreamBase"/> + su id; el traspaso usa un «nodo» ficticio por
    /// acto, <see cref="TransferNodeBase"/> + acto (por encima de cualquier nodo real del mapa), y el id del
    /// némesis como sorteo. Ninguno comparte desplazamiento con los de BetSystem (8000), MedicalSystem (9000+)
    /// ni los de recompensas (7000-7100+).
    /// </summary>
    private const int TitleStreamBase = 9500;

    private const int TransferNodeBase = 9500;

    /// <summary>Puestos titulares de un rival (0..6): el resto es banquillo.</summary>
    private const int StarterSlots = 7;

    /// <summary>
    /// Aplica los eventos de un partido de catálogo a la memoria de rivales. <paramref name="after"/> es el estado
    /// con el partido ya aplicado (plantilla incluida); la memoria de partida es la de <paramref name="before"/>,
    /// que es la que armó al rival. <paramref name="processedEvents"/> son los eventos que cuentan (los mismos que
    /// el bucle de bajas: hasta el que acaba la run, si lo hubo).
    /// </summary>
    public static NemesisOutcome Resolve(
        RunState before,
        RunState after,
        MapNode node,
        IReadOnlyList<MatchEvent> events,
        int processedEvents,
        Catalog catalog,
        NemesisCatalog nemesis)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(nemesis);

        if (!nemesis.IsActive || node.OpponentId.Length == 0 || !NodeKinds.IsCatalogRivalMatch(node.Kind))
        {
            return NemesisOutcome.Unchanged(after);
        }

        var team = nemesis.Rivals.Find(node.OpponentId);
        if (team is null)
        {
            return NemesisOutcome.Unchanged(after);
        }

        var memory = before.RivalMemory;
        var occupants = RivalRoster.Resolve(team, memory, before.Seed, catalog);
        var state = after;
        var made = new List<NemesisMade>();
        var revenges = new List<NemesisRevenge>();
        var revenged = new HashSet<int>();
        var fallenOwn = new HashSet<int>();
        var fallenRival = new HashSet<int>();
        int capped = 0;

        for (int i = 0; i < processedEvents && i < events.Count; i++)
        {
            var e = events[i];
            if (e.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            if (e.Type is not (EventType.Injury or EventType.Death))
            {
                continue;
            }

            bool victimIsOwn = state.FindPlayer(e.Actor) is not null;
            int rivalSlot = RivalSlot(victimIsOwn ? e.Opponent : e.Actor, team);
            bool causerIsOwn = e.Opponent >= 0 && state.FindPlayer(e.Opponent) is not null;

            if (victimIsOwn)
            {
                // Sólo cuenta la primera muerte de cada jugador propio y sólo si la causa un rival de clan.
                if (e.Type != EventType.Death || !fallenOwn.Add(e.Actor) || rivalSlot < 0 || causerIsOwn)
                {
                    continue;
                }

                var victim = state.FindPlayer(e.Actor)!;
                (memory, capped) = RegisterKill(memory, team, occupants, rivalSlot, victim, node, before.Seed, nemesis, made, capped);
                continue;
            }

            if (rivalSlot < 0)
            {
                continue;
            }

            // A partir de aquí la baja es de un rival de clan (Team 1), con o sin autor propio.
            if (e.Type == EventType.Death && !fallenRival.Add(e.Actor))
            {
                continue;
            }

            if (e.Type == EventType.Injury && fallenRival.Contains(e.Actor))
            {
                continue;
            }

            var current = memory.ActiveAt(team.ClanId, rivalSlot);
            if (current is not null && causerIsOwn && revenged.Add(current.Id))
            {
                var avenger = state.FindPlayer(e.Opponent)!;
                bool slain = e.Type == EventType.Death;
                state = state.WithPlayer(avenger with { Career = avenger.Career with { Revenges = avenger.Career.Revenges + 1 } });
                state = state.WithCounter(RunState.RevengesCounter, state.Counter(RunState.RevengesCounter) + 1);
                revenges.Add(new NemesisRevenge(current.Id, current.TitleId, current.Name, current.VictimName, avenger.Id, avenger.Name, slain));
            }

            if (e.Type == EventType.Death)
            {
                if (current is not null)
                {
                    memory = memory.WithNemesis(current with { Status = NemesisStatus.Slain });
                    memory = EnsureVacancy(memory, team.ClanId, rivalSlot);
                }
                else
                {
                    memory = VacateOccupant(memory, team.ClanId, rivalSlot);
                }
            }
        }

        if (capped > 0)
        {
            state = state.WithCounter(RunState.NemesisCappedCounter, state.Counter(RunState.NemesisCappedCounter) + capped);
        }

        return new NemesisOutcome(state.WithRivalMemory(memory), made, revenges, capped);
    }

    private static (RivalMemory Memory, int Capped) RegisterKill(
        RivalMemory memory,
        RivalTeam team,
        IReadOnlyList<RivalOccupant> occupants,
        int slot,
        RunPlayer victim,
        MapNode node,
        ulong seed,
        NemesisCatalog nemesis,
        List<NemesisMade> made,
        int capped)
    {
        var existing = memory.ActiveAt(team.ClanId, slot);
        if (existing is not null)
        {
            // Ya es némesis: la cuenta sube, el título y la primera víctima se quedan.
            return (memory.WithNemesis(existing with { Kills = existing.Kills + 1 }), capped);
        }

        if (memory.Alive.Count >= nemesis.MaxAlive)
        {
            return (memory, capped + 1);
        }

        int id = 1;
        for (int i = 0; i < memory.Nemeses.Count; i++)
        {
            id = Math.Max(id, memory.Nemeses[i].Id + 1);
        }

        var title = PickTitle(memory, nemesis, seed, node.Id, id);
        var created = new RivalNemesis(
            id,
            title.Id,
            occupants[slot].Name,
            team.Players[slot].Position,
            team.ClanId,
            slot,
            team.ClanId,
            slot,
            victim.Name,
            victim.Id,
            node.Act,
            Kills: 1,
            NemesisStatus.Active);
        made.Add(new NemesisMade(id, title.Id, created.Name, team.ClanId, victim.Id, victim.Name, node.Act));
        return (memory.WithNemesis(created), capped);
    }

    /// <summary>
    /// Título de un némesis: sorteado con el flujo propio de la run, entre los que aún no lleva ningún némesis
    /// (vivo o muerto) y, agotados, entre todos. Determinista: mismo estado, mismo nodo y mismo id, mismo título.
    /// </summary>
    private static NemesisTitle PickTitle(RivalMemory memory, NemesisCatalog nemesis, ulong seed, int nodeId, int nemesisId)
    {
        var free = new List<NemesisTitle>();
        for (int i = 0; i < nemesis.Titles.Count; i++)
        {
            bool used = false;
            for (int n = 0; n < memory.Nemeses.Count; n++)
            {
                if (string.Equals(memory.Nemeses[n].TitleId, nemesis.Titles[i].Id, StringComparison.Ordinal))
                {
                    used = true;
                    break;
                }
            }

            if (!used)
            {
                free.Add(nemesis.Titles[i]);
            }
        }

        var pool = free.Count > 0 ? free : nemesis.Titles;
        var rng = OfferStream.For(seed, nodeId, TitleStreamBase + nemesisId);
        return rng.Pick(pool);
    }

    /// <summary>
    /// El jugador rival del puesto sale de su clan (murió): si lo cubría el jugador de datos la vacante nace con
    /// el primer fichaje (generación 0); si lo cubría un fichaje, el siguiente.
    /// </summary>
    private static RivalMemory VacateOccupant(RivalMemory memory, string clanId, int slot)
    {
        var vacancy = memory.VacancyAt(clanId, slot);
        return memory.WithVacancy(new RivalVacancy(clanId, slot, vacancy is null ? 0 : vacancy.Generation + 1));
    }

    /// <summary>
    /// Un némesis deja el puesto (muere o se marcha): si ese puesto ya era una vacante, el siguiente fichaje ya
    /// estaba fijado; si era el suyo de nacimiento, nace la vacante con el primer fichaje.
    /// </summary>
    private static RivalMemory EnsureVacancy(RivalMemory memory, string clanId, int slot) =>
        memory.VacancyAt(clanId, slot) is null ? memory.WithVacancy(new RivalVacancy(clanId, slot, 0)) : memory;

    private static int RivalSlot(int playerId, RivalTeam team)
    {
        int slot = playerId - RivalTeamBuilder.OpponentFirstPlayerId;
        return slot >= 0 && slot < team.Players.Count ? slot : -1;
    }

    // ------------------------------------------------------------------ traspaso entre actos

    /// <summary>
    /// Al entrar en el acto <paramref name="act"/>, cada némesis vivo (por id) pasa de forma determinista a
    /// otro clan («se ha dado a conocer», ADR 0165): el destino sale de un flujo propio de la run y el puesto
    /// que ocupa es el del titular de menos nivel de su mismo puesto que no sea otro némesis (a igual nivel, el de
    /// menor suma de atributos y luego el de índice mayor); sustituye a quien lo ocupara. Su puesto de origen
    /// lo cubre un fichaje. Sin otro clan con hueco, el némesis se queda donde está.
    /// </summary>
    public static RunState TransferOnActEntry(RunState state, NemesisCatalog nemesis, int act)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(nemesis);
        if (!nemesis.IsActive || state.RivalMemory.Alive.Count == 0)
        {
            return state;
        }

        var memory = state.RivalMemory;
        var alive = memory.Alive;
        for (int a = 0; a < alive.Count; a++)
        {
            var current = memory.Find(alive[a].Id)!;
            var clans = new List<string>();
            var allClans = nemesis.Rivals.ClanIds;
            for (int c = 0; c < allClans.Count; c++)
            {
                if (!string.Equals(allClans[c], current.ClanId, StringComparison.Ordinal)
                    && nemesis.Rivals.OfClan(allClans[c], act) is not null)
                {
                    clans.Add(allClans[c]);
                }
            }

            var rng = OfferStream.For(state.Seed, TransferNodeBase + act, current.Id);
            rng.Shuffle(clans);
            for (int c = 0; c < clans.Count; c++)
            {
                var target = nemesis.Rivals.OfClan(clans[c], act)!;
                int slot = ReplacementSlot(memory, target, current.Position);
                if (slot < 0)
                {
                    continue;
                }

                // El puesto de origen lo cubre un fichaje; el de destino deja de tener a quien lo ocupaba.
                memory = EnsureVacancy(memory, current.ClanId, current.Slot);
                memory = VacateOccupant(memory, target.ClanId, slot);
                memory = memory.WithNemesis(current with { ClanId = target.ClanId, Slot = slot });
                break;
            }
        }

        return state.WithRivalMemory(memory);
    }

    /// <summary>
    /// Puesto titular (0..6) del clan destino que ocupa un némesis de ese puesto: el de menos nivel que no sea
    /// ya de otro némesis; a igual nivel, menos atributos y luego índice mayor. -1 si no hay ninguno.
    /// </summary>
    private static int ReplacementSlot(RivalMemory memory, RivalTeam target, Position position)
    {
        int best = -1;
        for (int slot = 0; slot < StarterSlots && slot < target.Players.Count; slot++)
        {
            var player = target.Players[slot];
            if (player.Position != position || memory.ActiveAt(target.ClanId, slot) is not null)
            {
                continue;
            }

            if (best < 0 || IsWeaker(player, slot, target.Players[best], best))
            {
                best = slot;
            }
        }

        return best;
    }

    private static bool IsWeaker(RivalPlayer candidate, int slot, RivalPlayer best, int bestSlot)
    {
        if (candidate.Level != best.Level)
        {
            return candidate.Level < best.Level;
        }

        int a = AttributeSum(candidate);
        int b = AttributeSum(best);
        return a != b ? a < b : slot > bestSlot;
    }

    private static int AttributeSum(RivalPlayer player) =>
        player.Attributes.Strength + player.Attributes.Speed + player.Attributes.Technique
        + player.Attributes.Stamina + player.Attributes.Leash;
}
