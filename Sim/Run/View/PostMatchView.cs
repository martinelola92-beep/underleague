using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;
using Underleague.Sim.Run.Systems.Nicknames;
using Underleague.Sim.Run.Systems.Rewards;
using Underleague.Sim.Run.Systems.Rivals;

namespace Underleague.Sim.Run.View;

/// <summary>Gravedad de una baja propia en el informe post-partido.</summary>
public enum CasualtyKind
{
    /// <summary>Lesión leve: penaliza el partido siguiente (RF-091).</summary>
    MinorInjury,

    /// <summary>Lesión grave: si vuelve a lesionarse sin tratar, muere (RF-093 vía 1).</summary>
    SevereInjury,

    /// <summary>Muerte (RF-093, ADR 0048). No vuelve.</summary>
    Death,
}

/// <summary>Una baja propia del partido (RF-119). Ordenadas por tick y, dentro del tick, por id.</summary>
public sealed record CasualtyRow(int PlayerId, string PlayerName, Position Position, CasualtyKind Kind, int Minute, string Cause)
{
    /// <summary>
    /// Reliquia que deja un muerto propio (ADR 0161 §2), en el idioma del informe; vacía si no deja ninguna
    /// (lesión, mercenario, run terminada o catálogo sin reliquias). El informe tiene que decirlo: llega al
    /// cofre sin que nadie la haya visto llegar.
    /// </summary>
    public string RelicName { get; init; } = string.Empty;

    /// <summary>Id de la reliquia de <see cref="RelicName"/>, vacío si no hay.</summary>
    public string RelicId { get; init; } = string.Empty;
}

/// <summary>
/// Estadísticas de un jugador propio en este partido (ADR 0163, RF-119): las mismas cifras de
/// <see cref="Underleague.Sim.Engine.MatchReport.Players"/>, sin recalcular nada. <see cref="Nickname"/> es
/// el apodo que tiene <b>después</b> del partido (vacío si no tiene).
/// </summary>
public sealed record PlayerStatRow(
    int PlayerId,
    string PlayerName,
    string Nickname,
    int Goals,
    int Assists,
    int TacklesWon,
    int Fouls,
    int InjuriesCaused);

/// <summary>
/// Un apodo ganado en este partido (ADR 0163): la carrera de antes no lo cumplía (o cumplía uno de menos
/// prioridad) y la de después sí. <see cref="PreviousNickname"/> vacío si no tenía ninguno.
/// </summary>
public sealed record NicknameGainRow(int PlayerId, string PlayerName, string NicknameId, string Nickname, string PreviousNickname);

/// <summary>
/// Un rival que se ha convertido en némesis en este partido (ADR 0165): quién es, su título, de qué clan y a
/// quién mató. El informe lo cuenta: «X, el Matahermanos, se convierte en tu némesis».
/// </summary>
public sealed record NemesisMadeRow(int NemesisId, string Name, string Title, string Clan, string VictimName);

/// <summary>
/// Una venganza cobrada en este partido (ADR 0165): quién se vengó, de qué némesis y si éste murió.
/// </summary>
public sealed record RevengeRow(int NemesisId, string NemesisName, string Title, string VictimName, string AvengerName, bool Slain);

/// <summary>Una tarjeta mostrada en el partido (RF-062, RF-063), de cualquiera de los dos equipos.</summary>
public sealed record CardRow(int PlayerId, string PlayerName, MatchSide Side, bool Red, int Minute);

/// <summary>
/// Un perk que se activó en el partido, con su número de activaciones y su contribución medible
/// (RF-119, RT-043).
///
/// <para><b>Cómo se mide la contribución.</b> Cada activación queda registrada con su tick (RT-043); la
/// contribución es lo que le pasó al partido <b>en esos mismos ticks</b>: goles del equipo, lesiones
/// causadas al rival, recuperaciones, paradas y eventos que un perk anuló. No es una atribución causal
/// —un tick puede tener dos perks activos— y por eso se enseña como "en sus activaciones", que es
/// exactamente lo que el dato dice. Es la diferencia entre un informe que enseña y uno que inventa.</para>
/// </summary>
public sealed record PerkReportRow(
    string PerkId,
    string PerkName,
    string Description,
    int OwnerId,
    string OwnerName,
    int Activations,
    int Goals,
    int InjuriesCaused,
    int Recoveries,
    int Saves,
    int Cancellations);

/// <summary>Un objeto equipado que entró en el partido (RF-075..078, RT-043).</summary>
/// <param name="Effects">Efectos aplicados de verdad; 0 si el portador no cumple la restricción de raza.</param>
public sealed record ItemReportRow(string ItemId, string ItemName, string Description, int OwnerId, string OwnerName, int Effects, bool Restricted);

/// <summary>Botín de liga (ADR 0161 §1): el objeto común que fue al almacén, además del oro.</summary>
public sealed record LootRow(string ItemId, string ItemName, string Description);

/// <summary>
/// Apartado del árbitro (RF-119, RF-062, RF-063): con qué criterio empezó, con cuál terminó y qué señaló
/// a cada equipo.
/// </summary>
/// <param name="InitialBias">Criterio con el que salió al campo.</param>
/// <param name="FinalBias">Criterio al terminar; se desplaza con cada acción sucia (ADR 0030 §3).</param>
public sealed record RefereeReport(
    string Name,
    RefereeTrait Trait,
    int InitialBias,
    int FinalBias,
    int FoulsFor,
    int FoulsAgainst,
    int CardsFor,
    int CardsAgainst)
{
    /// <summary>
    /// Faltas propias no señaladas (ADR 0158 §5, RF-119, la mitad de RF-119 que faltaba): cometidas por el
    /// jugador y que el árbitro no vio (<c>EventType.Foul</c> con detalle <c>unseen</c>). El motor ya las
    /// emitía y movía el criterio con ellas (RF-063); esto solo las cuenta para el informe.
    /// </summary>
    public int UnseenFoulsFor { get; init; }

    /// <summary>Faltas del rival no señaladas: jugadas sucias en contra que el árbitro tampoco vio.</summary>
    public int UnseenFoulsAgainst { get; init; }
}

/// <summary>
/// Informe post-partido (RF-119): la pantalla obligatoria que explica <b>por qué</b> pasó lo que pasó.
/// Es dato puro; el texto de mobiliario lo pone la interfaz, y las descripciones de perks y objetos ya
/// vienen generadas (RT-035).
/// </summary>
public sealed record PostMatchReport(
    int NodeId,
    NodeKind NodeKind,
    int Act,
    int Difficulty,
    string OwnTeamName,
    string RivalTeamName,
    int GoalsFor,
    int GoalsAgainst,
    bool Won,
    bool WentToGoldenGoal,
    bool Forfeit,
    int Minutes,
    IReadOnlyList<PerkReportRow> Perks,
    IReadOnlyList<ItemReportRow> Items,
    IReadOnlyList<CasualtyRow> Casualties,
    IReadOnlyList<CardRow> Cards,
    RefereeReport Referee,
    GoldForWinBreakdown? Gold,
    CounterGold CounterGold,
    DeathGold DeathGold,
    LootRow? Loot)
{
    /// <summary>
    /// Cómo terminó la apuesta del vestuario que el jugador tomó para este partido (ADR 0157): qué condición,
    /// cuánto apostó, si se cumplió y cuánto cobró. Null si no tomó ninguna. La condición ya viene resuelta
    /// por el motor (RT-014): la interfaz solo la enseña.
    /// </summary>
    public Underleague.Sim.Run.Systems.Bets.BetResult? Bet { get; init; }

    /// <summary>Estadísticas de cada jugador propio que pisó el campo, por id ascendente (ADR 0163).</summary>
    public IReadOnlyList<PlayerStatRow> PlayerStats { get; init; } = Array.Empty<PlayerStatRow>();

    /// <summary>Apodos ganados en este partido, por id de jugador ascendente (ADR 0163).</summary>
    public IReadOnlyList<NicknameGainRow> NicknamesEarned { get; init; } = Array.Empty<NicknameGainRow>();

    /// <summary>Oro que el corredor devolvió al entrar en este partido por una apuesta tomada para otro nodo (ADR 0157); 0 si ninguno.</summary>
    public int BetRefunded { get; init; }

    /// <summary>Rivales que se han convertido en némesis en este partido (ADR 0165), por id de némesis.</summary>
    public IReadOnlyList<NemesisMadeRow> NemesesMade { get; init; } = Array.Empty<NemesisMadeRow>();

    /// <summary>Venganzas de este partido (ADR 0165), por id de némesis.</summary>
    public IReadOnlyList<RevengeRow> Revenges { get; init; } = Array.Empty<RevengeRow>();

    /// <summary>Oro cobrado por las venganzas de este partido (ADR 0165); 0 si ninguna o si la run terminó en él.</summary>
    public int RevengeGold { get; init; }

    /// <summary>Muertes propias (RF-093): lo primero que el informe tiene que decir cuando las hay.</summary>
    public int Deaths
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Casualties.Count; i++)
            {
                if (Casualties[i].Kind == CasualtyKind.Death)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Oro cobrado por el partido; 0 si se perdió (RF-114g: perder no paga).</summary>
    public int GoldEarned => Gold?.Total ?? 0;
}

/// <summary>Compone el informe post-partido de RF-119 desde el partido ya jugado. Puro, sin E/S.</summary>
public static class PostMatchView
{
    private const string CancelledSuffix = ":cancelled";
    private const string SevereDetail = "severe";
    private const string RedDetail = "red";

    /// <summary>
    /// Informe de un partido ya resuelto.
    /// </summary>
    /// <param name="playback">El partido reproducido, con su secuencia de eventos (<see cref="MatchPlaybacks.Of"/>).</param>
    /// <param name="stateAfterMatch">Estado de la run después del partido: de él salen los nombres y el objetivo cumplido.</param>
    /// <param name="summary">Resumen que devuelve <c>RunEngine.EnterMatch</c>.</param>
    /// <param name="catalog">Catálogo de <c>/data</c>: perks, plantillas de descripción y ticks reglamentarios.</param>
    /// <param name="economy">Economía de la run; sin ella el informe no lleva desglose de oro.</param>
    /// <param name="items">Catálogo de equipamiento; sin él el informe no lista objetos.</param>
    /// <param name="language">Idioma de las descripciones generadas (RT-073).</param>
    /// <param name="nicknames">Catálogo de apodos (ADR 0163); sin él el informe no enseña apodos.</param>
    /// <param name="nemesis">Clanes y títulos de némesis (ADR 0165); sin él el informe no cuenta némesis ni venganzas.</param>
    public static PostMatchReport Build(
        MatchPlayback playback,
        RunState stateAfterMatch,
        RunMatchSummary summary,
        Catalog catalog,
        EconomyConfig? economy = null,
        ItemCatalog? items = null,
        string language = "es",
        NicknameCatalog? nicknames = null,
        NemesisCatalog? nemesis = null)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentNullException.ThrowIfNull(stateAfterMatch);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(catalog);

        var templates = catalog.Localization.Get(language);
        var report = playback.Result.Report;
        var events = playback.Result.Events;
        int own = playback.PlayerTeam;
        int regulationTicks = catalog.Tuning.RegulationTicks;
        var names = PlayerNames(playback.Setup);

        return new PostMatchReport(
            playback.Node.Id,
            playback.Node.Kind,
            playback.Node.Act,
            playback.Node.Difficulty,
            playback.OwnName,
            playback.RivalName,
            report.Goals[own],
            report.Goals[1 - own],
            report.Winner == own,
            report.WentToGoldenGoal,
            report.Forfeit,
            MatchLogView.Minute(report.ClockTicks, regulationTicks),
            PerkRows(report, events, catalog, templates, names, own, playback.Setup),
            ItemRows(report, items, templates, names, own),
            Casualties(events, names, playback.Setup, own, regulationTicks, catalog, stateAfterMatch, items, language),
            Cards(events, names, own, regulationTicks),
            Referee(playback, report, events, own),
            // El oro solo se cobra si se ganó **y** la run sigue en pie: una baja que baja del mínimo
            // (RF-002b) termina la run antes de que <c>AfterMatch</c> llegue a pagar, y un informe que
            // enseñara ese oro estaría mintiendo sobre un cobro que no ocurrió.
            economy is null || report.Winner != own || stateAfterMatch.Result.IsOver
                ? null
                : GoldCalculator.Breakdown(stateAfterMatch, playback.Node, summary, economy),
            // El oro de contador se enseña se haya ganado o no (ADR 0113): es el otro canal, y esconderlo
            // en la derrota devolvería a la invisibilidad justo a los perks que se cobran perdiendo.
            // Sigue callándose si la run ha terminado, por el mismo motivo que el premio: no se ingresó.
            economy is null || stateAfterMatch.Result.IsOver
                ? CounterGold.None
                : GoldCalculator.CounterGold(stateAfterMatch, summary, economy),
            // El oro de muerte (paquete BB, Seguro de vida) es el mismo canal aparte que el de contador:
            // se enseña se haya ganado o no, y se calla si la run ha terminado, por el mismo motivo.
            economy is null || stateAfterMatch.Result.IsOver
                ? DeathGold.None
                : GoldCalculator.DeathGold(stateAfterMatch, summary, economy),
            Loot(playback, report, items, templates, stateAfterMatch))
        {
            Bet = summary.Bet,
            BetRefunded = summary.BetRefunded,
            PlayerStats = PlayerStatRows(report, own, stateAfterMatch, nicknames, language),
            NicknamesEarned = NicknameGains(report, own, stateAfterMatch, nicknames, language, summary),
            NemesesMade = MadeRows(summary, nemesis, language),
            Revenges = RevengeRows(summary, nemesis, language),
            RevengeGold = economy is null || stateAfterMatch.Result.IsOver ? 0 : summary.Revenges.Count * economy.RevengeGold,
        };
    }

    /// <summary>
    /// Estadísticas propias del partido (ADR 0163): las de <c>MatchReport.Players</c> del equipo propio, de
    /// los que llegaron a pisar el campo, por id ascendente. Ni una cifra se recalcula.
    /// </summary>
    private static IReadOnlyList<PlayerStatRow> PlayerStatRows(
        MatchReport report, int ownTeam, RunState stateAfterMatch, NicknameCatalog? nicknames, string language)
    {
        var rows = new List<PlayerStatRow>();
        for (int i = 0; i < report.Players.Count; i++)
        {
            var stats = report.Players[i];
            if (stats.Team != ownTeam || stats.TicksOnPitch <= 0)
            {
                continue;
            }

            var player = stateAfterMatch.FindPlayer(stats.PlayerId);
            if (player is null)
            {
                continue;
            }

            var nickname = nicknames is null ? null : NicknameSystem.For(player, nicknames);
            rows.Add(new PlayerStatRow(
                stats.PlayerId,
                player.Name,
                nickname?.NameIn(language) ?? string.Empty,
                stats.Goals,
                stats.Assists,
                stats.TacklesWon,
                stats.Fouls,
                stats.InjuriesCaused));
        }

        rows.Sort(static (a, b) => a.PlayerId.CompareTo(b.PlayerId));
        return rows;
    }

    private static IReadOnlyList<NemesisMadeRow> MadeRows(RunMatchSummary summary, NemesisCatalog? nemesis, string language)
    {
        var rows = new List<NemesisMadeRow>(summary.NemesesMade.Count);
        for (int i = 0; i < summary.NemesesMade.Count; i++)
        {
            var made = summary.NemesesMade[i];
            rows.Add(new NemesisMadeRow(
                made.NemesisId,
                made.Name,
                nemesis?.Find(made.TitleId)?.NameIn(language) ?? string.Empty,
                ClanName(nemesis, made.ClanId, language),
                made.VictimName));
        }

        rows.Sort(static (a, b) => a.NemesisId.CompareTo(b.NemesisId));
        return rows;
    }

    private static string ClanName(NemesisCatalog? nemesis, string clanId, string language)
    {
        var name = nemesis?.Rivals.ClanName(clanId);
        return name is null ? string.Empty : string.Equals(language, "en", StringComparison.Ordinal) ? name.En : name.Es;
    }

    private static IReadOnlyList<RevengeRow> RevengeRows(RunMatchSummary summary, NemesisCatalog? nemesis, string language)
    {
        var rows = new List<RevengeRow>(summary.Revenges.Count);
        for (int i = 0; i < summary.Revenges.Count; i++)
        {
            var revenge = summary.Revenges[i];
            rows.Add(new RevengeRow(
                revenge.NemesisId,
                revenge.NemesisName,
                nemesis?.Find(revenge.TitleId)?.NameIn(language) ?? string.Empty,
                revenge.VictimName,
                revenge.AvengerName,
                revenge.Slain));
        }

        rows.Sort(static (a, b) => a.NemesisId.CompareTo(b.NemesisId));
        return rows;
    }

    /// <summary>
    /// Apodos que el partido ha dado (ADR 0163): compara la carrera de antes, que es la de después menos
    /// las estadísticas del partido (<see cref="NicknameSystem.BeforeMatch"/>), con la de después.
    /// </summary>
    private static IReadOnlyList<NicknameGainRow> NicknameGains(
        MatchReport report, int ownTeam, RunState stateAfterMatch, NicknameCatalog? nicknames, string language, RunMatchSummary summary)
    {
        var rows = new List<NicknameGainRow>();
        if (nicknames is null)
        {
            return rows;
        }

        var priorities = new Dictionary<int, int>();

        for (int i = 0; i < report.Players.Count; i++)
        {
            var stats = report.Players[i];
            var player = stats.Team == ownTeam ? stateAfterMatch.FindPlayer(stats.PlayerId) : null;
            if (player is null)
            {
                continue;
            }

            int revenges = 0;
            for (int r = 0; r < summary.Revenges.Count; r++)
            {
                if (summary.Revenges[r].AvengerPlayerId == player.Id)
                {
                    revenges++;
                }
            }

            var before = NicknameSystem.BeforeMatch(player.Career, stats, revenges);
            var earned = NicknameSystem.Earned(before, player.Career, nicknames);
            if (earned is null)
            {
                continue;
            }

            rows.Add(new NicknameGainRow(
                player.Id,
                player.Name,
                earned.Id,
                earned.NameIn(language),
                NicknameSystem.For(before, nicknames)?.NameIn(language) ?? string.Empty));
            priorities[player.Id] = earned.Priority;
        }

        // Los de mayor prioridad primero (los más raros de ganar): la pantalla sólo enseña los primeros y
        // dice cuántos más hay, así que el orden es lo que decide cuáles se ven (ADR 0163). Luego id (RT-041).
        rows.Sort((a, b) =>
        {
            int byPriority = priorities[b.PlayerId].CompareTo(priorities[a.PlayerId]);
            return byPriority != 0 ? byPriority : a.PlayerId.CompareTo(b.PlayerId);
        });
        return rows;
    }

    /// <summary>
    /// El botín de liga de este partido, si lo hay (ADR 0161 §1). <see cref="LeagueLootSystem.Pick"/> es
    /// pura y determinista: con la misma semilla, nodo, acto y raza siempre elige el mismo objeto que ya
    /// eligió <c>StandardRunSystems.AfterMatch</c> al aplicarlo al almacén, así que este informe puede
    /// recalcularlo sin que nadie se lo pase —RT-014, la pantalla no decide nada, solo enseña lo que ya
    /// pasó—. Null si la run terminó (el botín no se aplicó, igual que el oro de arriba) o si el nodo no es
    /// de liga o no se ganó.
    /// </summary>
    private static LootRow? Loot(
        MatchPlayback playback, MatchReport report, ItemCatalog? items, DescriptionTemplates templates, RunState stateAfterMatch)
    {
        if (items is null || stateAfterMatch.Result.IsOver)
        {
            return null;
        }

        bool won = report.Winner == playback.PlayerTeam;
        if (!LeagueLootSystem.AppliesTo(playback.Node.Kind, won))
        {
            return null;
        }

        var loot = LeagueLootSystem.Pick(stateAfterMatch.Seed, playback.Node.Id, playback.Node.Act, stateAfterMatch.ClubRace, items);
        return new LootRow(loot.Id, NameIn(loot.Name, templates.Language), ItemDescriptions.Describe(loot, templates.Language));
    }

    /// <summary>
    /// Perks propios activados, con su contribución. Ordenados por número de activaciones descendente y,
    /// a igualdad, por id de perk y de jugador ascendente (RT-041): la lista es la misma en dos ejecuciones
    /// del mismo partido.
    /// </summary>
    private static IReadOnlyList<PerkReportRow> PerkRows(
        MatchReport report,
        IReadOnlyList<MatchEvent> events,
        Catalog catalog,
        DescriptionTemplates templates,
        IReadOnlyDictionary<int, string> names,
        int ownTeam,
        MatchSetup setup)
    {
        var ownIds = TeamPlayerIds(setup, ownTeam);
        var rows = new List<PerkReportRow>();

        for (int i = 0; i < report.PerksSummary.Count; i++)
        {
            var entry = report.PerksSummary[i];
            if (!ownIds.Contains(entry.OwnerId) || entry.Activations <= 0)
            {
                continue;
            }

            var ticks = ActivationTicks(report.PerkActivations, entry.PerkId, entry.OwnerId);
            var contribution = Contribution(events, ticks, ownTeam);
            var perk = catalog.Perks.Find(entry.PerkId);

            rows.Add(new PerkReportRow(
                entry.PerkId,
                perk is null ? entry.PerkId : NameIn(perk.Name, templates.Language),
                perk is null ? string.Empty : DescriptionGenerator.Describe(perk, templates, catalog.Perks),
                entry.OwnerId,
                names.GetValueOrDefault(entry.OwnerId) ?? string.Empty,
                entry.Activations,
                contribution.Goals,
                contribution.Injuries,
                contribution.Recoveries,
                contribution.Saves,
                contribution.Cancellations));
        }

        rows.Sort(static (a, b) =>
        {
            int byActivations = b.Activations.CompareTo(a.Activations);
            if (byActivations != 0)
            {
                return byActivations;
            }

            int byPerk = string.CompareOrdinal(a.PerkId, b.PerkId);
            return byPerk != 0 ? byPerk : a.OwnerId.CompareTo(b.OwnerId);
        });

        return rows;
    }

    private static IReadOnlyList<ItemReportRow> ItemRows(
        MatchReport report,
        ItemCatalog? items,
        DescriptionTemplates templates,
        IReadOnlyDictionary<int, string> names,
        int ownTeam)
    {
        var rows = new List<ItemReportRow>();
        if (items is null)
        {
            return rows;
        }

        for (int i = 0; i < report.ItemActivations.Count; i++)
        {
            var entry = report.ItemActivations[i];
            if (entry.Team != ownTeam)
            {
                continue;
            }

            var item = items.Find(entry.ItemId);
            rows.Add(new ItemReportRow(
                entry.ItemId,
                item is null ? entry.ItemId : NameIn(item.Name, templates.Language),
                item is null ? string.Empty : ItemDescriptions.Describe(item, templates.Language),
                entry.OwnerId,
                names.GetValueOrDefault(entry.OwnerId) ?? string.Empty,
                entry.Effects,
                entry.Detail.StartsWith("restricted", StringComparison.Ordinal)));
        }

        return rows;
    }

    /// <summary>
    /// Bajas propias en orden de partido. La causa es el nombre del rival que las provocó cuando el
    /// evento lo lleva: una muerte sin culpable es una muerte que el jugador no puede entender (RF-013).
    /// </summary>
    private static IReadOnlyList<CasualtyRow> Casualties(
        IReadOnlyList<MatchEvent> events,
        IReadOnlyDictionary<int, string> names,
        MatchSetup setup,
        int ownTeam,
        int regulationTicks,
        Catalog catalog,
        RunState stateAfterMatch,
        ItemCatalog? items,
        string language)
    {
        var positions = Positions(setup);
        var rows = new List<CasualtyRow>();
        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Team != ownTeam
                || matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal)
                || (matchEvent.Type != EventType.Injury && matchEvent.Type != EventType.Death))
            {
                continue;
            }

            var kind = matchEvent.Type == EventType.Death
                ? CasualtyKind.Death
                : matchEvent.Detail.StartsWith(SevereDetail, StringComparison.Ordinal)
                    ? CasualtyKind.SevereInjury
                    : CasualtyKind.MinorInjury;

            // ADR 0161 §2: la reliquia depende de la carrera del muerto DESPUÉS de este partido, que es el
            // estado con el que la run la decidió. Igual que el botín, no se enseña si la run terminó.
            var relic = kind == CasualtyKind.Death && items is not null && !stateAfterMatch.Result.IsOver
                ? RelicSystem.RelicFor(stateAfterMatch.GetPlayer(matchEvent.Actor), items)
                : null;

            rows.Add(new CasualtyRow(
                matchEvent.Actor,
                names.GetValueOrDefault(matchEvent.Actor) ?? string.Empty,
                positions.GetValueOrDefault(matchEvent.Actor, Position.Midfielder),
                kind,
                MatchLogView.Minute(matchEvent.ClockTick, regulationTicks),
                Cause(matchEvent, names, catalog))
            {
                RelicId = relic?.Id ?? string.Empty,
                RelicName = relic is null ? string.Empty : NameIn(relic.Name, language),
            });
        }

        return rows;
    }

    private static string NameIn(LocalizedName name, string language) =>
        string.Equals(language, "en", StringComparison.Ordinal) ? name.En : name.Es;

    /// <summary>
    /// Quién causó la baja. En una lesión es el rival que entró; en una muerte, el <b>perk letal</b> que
    /// la provocó, que el motor deja en el detalle del evento como <c>perk:&lt;id&gt;</c>. RF-013 exige
    /// que ninguna muerte quede sin explicar, y el informe es donde se explica.
    /// </summary>
    private static string Cause(MatchEvent matchEvent, IReadOnlyDictionary<int, string> names, Catalog catalog)
    {
        const string PerkPrefix = "perk:";
        if (matchEvent.Detail.StartsWith(PerkPrefix, StringComparison.Ordinal))
        {
            string id = matchEvent.Detail[PerkPrefix.Length..];
            return catalog.Perks.Find(id)?.Name.Es ?? id;
        }

        return names.GetValueOrDefault(matchEvent.Opponent)
            ?? names.GetValueOrDefault(matchEvent.Target)
            ?? string.Empty;
    }

    private static IReadOnlyList<CardRow> Cards(
        IReadOnlyList<MatchEvent> events,
        IReadOnlyDictionary<int, string> names,
        int ownTeam,
        int regulationTicks)
    {
        var rows = new List<CardRow>();
        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Type != EventType.Card || matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(new CardRow(
                matchEvent.Actor,
                names.GetValueOrDefault(matchEvent.Actor) ?? string.Empty,
                matchEvent.Team == ownTeam ? MatchSide.Own : MatchSide.Rival,
                matchEvent.Detail.StartsWith(RedDetail, StringComparison.Ordinal),
                MatchLogView.Minute(matchEvent.ClockTick, regulationTicks)));
        }

        return rows;
    }

    /// <summary>Detalle del evento FOUL para una falta que el árbitro no vio (ADR 0090, ADR 0158 §5).</summary>
    private const string UnseenDetail = "unseen";

    private static RefereeReport Referee(
        MatchPlayback playback, MatchReport report, IReadOnlyList<MatchEvent> events, int ownTeam)
    {
        // ADR 0158 §4, revisión independiente, RF-055d: la turba no tiene árbitro, así que ninguna falta
        // posterior a que se vaya (EventType.RefereeLeaves) es "no señalada por él" -no hay "él" que la
        // señale o no-. Se busca el índice una vez; -1 si el partido no llegó a gol de oro.
        int refereeLeavesIndex = -1;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Type == EventType.RefereeLeaves)
            {
                refereeLeavesIndex = i;
                break;
            }
        }

        int foulsFor = 0;
        int foulsAgainst = 0;
        int cardsFor = 0;
        int cardsAgainst = 0;
        int unseenFoulsFor = 0;
        int unseenFoulsAgainst = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            bool mine = matchEvent.Team == ownTeam;
            if (matchEvent.Type == EventType.Foul)
            {
                if (mine)
                {
                    foulsFor++;
                }
                else
                {
                    foulsAgainst++;
                }

                bool refereeWasPresent = refereeLeavesIndex < 0 || i < refereeLeavesIndex;
                if (refereeWasPresent && string.Equals(matchEvent.Detail, UnseenDetail, StringComparison.Ordinal))
                {
                    if (mine)
                    {
                        unseenFoulsFor++;
                    }
                    else
                    {
                        unseenFoulsAgainst++;
                    }
                }
            }
            else if (matchEvent.Type == EventType.Card)
            {
                if (mine)
                {
                    cardsFor++;
                }
                else
                {
                    cardsAgainst++;
                }
            }
        }

        return new RefereeReport(
            playback.Setup.Referee.Name,
            playback.Setup.Referee.Trait,
            playback.Setup.Referee.InitialBias,
            report.FinalBias,
            foulsFor,
            foulsAgainst,
            cardsFor,
            cardsAgainst)
        {
            UnseenFoulsFor = unseenFoulsFor,
            UnseenFoulsAgainst = unseenFoulsAgainst,
        };
    }

    private static HashSet<int> ActivationTicks(IReadOnlyList<PerkActivation> activations, string perkId, int ownerId)
    {
        var ticks = new HashSet<int>();
        for (int i = 0; i < activations.Count; i++)
        {
            if (activations[i].OwnerId == ownerId && string.Equals(activations[i].PerkId, perkId, StringComparison.Ordinal))
            {
                ticks.Add(activations[i].Tick);
            }
        }

        return ticks;
    }

    private static (int Goals, int Injuries, int Recoveries, int Saves, int Cancellations) Contribution(
        IReadOnlyList<MatchEvent> events, HashSet<int> ticks, int ownTeam)
    {
        int goals = 0;
        int injuries = 0;
        int recoveries = 0;
        int saves = 0;
        int cancellations = 0;

        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (!ticks.Contains(matchEvent.Tick))
            {
                continue;
            }

            if (matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal))
            {
                cancellations++;
                continue;
            }

            bool mine = matchEvent.Team == ownTeam;
            switch (matchEvent.Type)
            {
                case EventType.Goal when mine:
                    goals++;
                    break;
                case EventType.Injury when !mine:
                    injuries++;
                    break;
                case EventType.Recovery when mine:
                    recoveries++;
                    break;
                case EventType.Save when mine:
                    saves++;
                    break;
                default:
                    break;
            }
        }

        return (goals, injuries, recoveries, saves, cancellations);
    }

    private static HashSet<int> TeamPlayerIds(MatchSetup setup, int team)
    {
        var players = team == 0 ? setup.Home.Players : setup.Away.Players;
        var ids = new HashSet<int>();
        for (int i = 0; i < players.Count; i++)
        {
            ids.Add(players[i].Id);
        }

        return ids;
    }

    private static Dictionary<int, string> PlayerNames(MatchSetup setup)
    {
        var names = new Dictionary<int, string>();
        Add(setup.Home);
        Add(setup.Away);
        return names;

        void Add(TeamSetup team)
        {
            for (int i = 0; i < team.Players.Count; i++)
            {
                names[team.Players[i].Id] = team.Players[i].Name;
            }
        }
    }

    private static Dictionary<int, Position> Positions(MatchSetup setup)
    {
        var positions = new Dictionary<int, Position>();
        Add(setup.Home);
        Add(setup.Away);
        return positions;

        void Add(TeamSetup team)
        {
            for (int i = 0; i < team.Players.Count; i++)
            {
                positions[team.Players[i].Id] = team.Players[i].Position;
            }
        }
    }
}
