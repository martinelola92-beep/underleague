using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;
using Underleague.Sim.Model;
using Underleague.Sim.Perks;
using Underleague.Sim.Run.Systems.Economy;
using Underleague.Sim.Run.Systems.Items;

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
public sealed record CasualtyRow(int PlayerId, string PlayerName, Position Position, CasualtyKind Kind, int Minute, string Cause);

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
    int CardsAgainst);

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
    GoldForWinBreakdown? Gold)
{
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
    public static PostMatchReport Build(
        MatchPlayback playback,
        RunState stateAfterMatch,
        RunMatchSummary summary,
        Catalog catalog,
        EconomyConfig? economy = null,
        ItemCatalog? items = null,
        string language = "es")
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
            MatchLogView.Minute(report.Ticks, regulationTicks),
            PerkRows(report, events, catalog, templates, names, own, playback.Setup),
            ItemRows(report, items, templates, names, own),
            Casualties(events, names, playback.Setup, own, regulationTicks, catalog),
            Cards(events, names, own, regulationTicks),
            Referee(playback, report, events, own),
            // El oro solo se cobra si se ganó **y** la run sigue en pie: una baja que baja del mínimo
            // (RF-002b) termina la run antes de que <c>AfterMatch</c> llegue a pagar, y un informe que
            // enseñara ese oro estaría mintiendo sobre un cobro que no ocurrió.
            economy is null || report.Winner != own || stateAfterMatch.Result.IsOver
                ? null
                : GoldCalculator.Breakdown(stateAfterMatch, playback.Node, summary, economy));
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
                perk?.Name.Es ?? entry.PerkId,
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
                item?.Name.Es ?? entry.ItemId,
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
        Catalog catalog)
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

            rows.Add(new CasualtyRow(
                matchEvent.Actor,
                names.GetValueOrDefault(matchEvent.Actor) ?? string.Empty,
                positions.GetValueOrDefault(matchEvent.Actor, Position.Midfielder),
                kind,
                MatchLogView.Minute(matchEvent.Tick, regulationTicks),
                Cause(matchEvent, names, catalog)));
        }

        return rows;
    }

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
                MatchLogView.Minute(matchEvent.Tick, regulationTicks)));
        }

        return rows;
    }

    private static RefereeReport Referee(
        MatchPlayback playback, MatchReport report, IReadOnlyList<MatchEvent> events, int ownTeam)
    {
        int foulsFor = 0;
        int foulsAgainst = 0;
        int cardsFor = 0;
        int cardsAgainst = 0;
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
            cardsAgainst);
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
