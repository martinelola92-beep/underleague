using Underleague.Sim.Events;
using Underleague.Sim.Model;

namespace Underleague.Sim.Run.View;

/// <summary>De quién es la acción que narra una línea del log.</summary>
public enum MatchSide
{
    /// <summary>Ni de un equipo ni de otro: saque inicial, final, turba, árbitro.</summary>
    Neutral,

    /// <summary>Del equipo del jugador.</summary>
    Own,

    /// <summary>Del rival.</summary>
    Rival,
}

/// <summary>
/// Una línea del log de eventos del partido (RF-121). Es <b>dato estructurado, no texto</b>, igual que
/// <see cref="LineupWarning"/>: la frase visible la compone la interfaz con sus claves de localización
/// (RT-073), porque <c>MatchReport.Log</c> es el volcado interno del motor —en inglés y con el detalle
/// crudo— y sirve para depurar, no para leerlo en pantalla.
/// </summary>
/// <param name="Tick">Tick lógico del evento (RT-020).</param>
/// <param name="Minute">
/// Minuto del partido, 0..90 en el tiempo reglamentario y por encima en la prórroga de turba. Se deriva
/// de los ticks reglamentarios del catálogo: el jugador lee minutos, no ticks
/// (<c>docs/estilo-descripciones.md</c>).
/// </param>
/// <param name="Detail">Detalle del evento sin el sufijo de anulación; clave de la sección <c>details</c> de <c>data/l10n</c>.</param>
/// <param name="Cancelled">True si un perk anuló el evento (§7 de fase 1): pasó y no contó.</param>
/// <param name="ActorName">Nombre de quien actúa; el del equipo si el evento no tiene actor.</param>
/// <param name="OtherName">Nombre del objetivo o del rival implicado; vacío si no lo hay.</param>
/// <param name="GoalsFor">Marcador del equipo del jugador <b>después</b> de esta línea.</param>
/// <param name="GoalsAgainst">Marcador del rival después de esta línea.</param>
/// <param name="Highlight">
/// Lo que el jugador no puede perderse: goles, tarjetas, lesiones, muertes, turba y final. Es lo que la
/// pantalla saca del scroll y pone en la columna de sucesos clave.
/// </param>
public sealed record MatchLogLine(
    int Tick,
    int Minute,
    EventType Type,
    string Detail,
    bool Cancelled,
    MatchSide Side,
    string ActorName,
    string OtherName,
    int GoalsFor,
    int GoalsAgainst,
    bool Highlight);

/// <summary>
/// Convierte la secuencia de eventos de un partido en el log de RF-121. Puro: no simula nada, solo
/// filtra, ordena y le pone nombre a los identificadores de jugador.
/// </summary>
public static class MatchLogView
{
    /// <summary>Sufijo con el que el motor marca un evento anulado por un perk (§7 de fase 1).</summary>
    private const string CancelledSuffix = ":cancelled";

    /// <summary>Minutos de un partido reglamentario: la unidad en la que el jugador lee el tiempo.</summary>
    private const int RegulationMinutes = 90;

    /// <summary>
    /// Log del partido. Se descartan los eventos que no narran nada por sí solos —el inicio y el final de
    /// jugada, y los "intento de" que siempre van seguidos de su resultado— porque duplicarían cada
    /// acción y el log dejaría de poder leerse.
    /// </summary>
    /// <param name="playback">Partido reproducido (<see cref="MatchPlaybacks.Of"/>).</param>
    /// <param name="regulationTicks">Ticks del tiempo reglamentario (<c>catalog.Tuning.RegulationTicks</c>).</param>
    public static IReadOnlyList<MatchLogLine> Build(MatchPlayback playback, int regulationTicks)
    {
        ArgumentNullException.ThrowIfNull(playback);
        return Build(playback.Setup, playback.Result.Events, playback.PlayerTeam, regulationTicks);
    }

    /// <summary>Log del partido a partir de sus piezas sueltas.</summary>
    public static IReadOnlyList<MatchLogLine> Build(
        MatchSetup setup, IReadOnlyList<MatchEvent> events, int playerTeam, int regulationTicks)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(events);

        var names = Names(setup);
        var lines = new List<MatchLogLine>(events.Count);
        int goalsFor = 0;
        int goalsAgainst = 0;

        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (!Narrates(matchEvent.Type))
            {
                continue;
            }

            bool cancelled = matchEvent.Detail.EndsWith(CancelledSuffix, StringComparison.Ordinal);
            string detail = cancelled
                ? matchEvent.Detail[..^CancelledSuffix.Length]
                : matchEvent.Detail;

            var side = matchEvent.Team < 0
                ? MatchSide.Neutral
                : matchEvent.Team == playerTeam ? MatchSide.Own : MatchSide.Rival;

            if (matchEvent.Type == EventType.Goal && !cancelled)
            {
                if (side == MatchSide.Own)
                {
                    goalsFor++;
                }
                else if (side == MatchSide.Rival)
                {
                    goalsAgainst++;
                }
            }

            string actor = names.GetValueOrDefault(matchEvent.Actor)
                ?? (matchEvent.Team >= 0 ? TeamName(setup, matchEvent.Team) : string.Empty);
            string other = names.GetValueOrDefault(matchEvent.Target)
                ?? names.GetValueOrDefault(matchEvent.Opponent)
                ?? string.Empty;

            lines.Add(new MatchLogLine(
                matchEvent.Tick,
                Minute(matchEvent.Tick, regulationTicks),
                matchEvent.Type,
                detail,
                cancelled,
                side,
                actor,
                other,
                goalsFor,
                goalsAgainst,
                IsHighlight(matchEvent.Type)));
        }

        return lines;
    }

    /// <summary>Las líneas que la pantalla saca del scroll: goles, tarjetas, lesiones, muertes y cortes del partido.</summary>
    public static IReadOnlyList<MatchLogLine> Highlights(IReadOnlyList<MatchLogLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var result = new List<MatchLogLine>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Highlight)
            {
                result.Add(lines[i]);
            }
        }

        return result;
    }

    /// <summary>Minuto de un partido de 90 equivalente al tick indicado; la prórroga de turba pasa de 90.</summary>
    public static int Minute(int tick, int regulationTicks) =>
        regulationTicks <= 0 ? 0 : tick * RegulationMinutes / regulationTicks;

    /// <summary>
    /// Un evento narra por sí solo. Fuera quedan el inicio y el final de jugada (contabilidad de la
    /// máquina de estados) y los "intento de", que el motor emite siempre junto a su resultado.
    /// </summary>
    private static bool Narrates(EventType type) => type switch
    {
        EventType.PlayStart => false,
        EventType.PlayEnd => false,
        EventType.PassAttempted => false,
        EventType.DribbleAttempted => false,
        _ => true,
    };

    private static bool IsHighlight(EventType type) => type switch
    {
        EventType.Goal => true,
        EventType.Card => true,
        EventType.Injury => true,
        EventType.Death => true,
        EventType.MobStart => true,
        EventType.RefereeLeaves => true,
        EventType.MatchEnd => true,
        EventType.ConsumableUsed => true,
        _ => false,
    };

    private static string TeamName(MatchSetup setup, int team) =>
        team == 0 ? setup.Home.Name : setup.Away.Name;

    private static Dictionary<int, string> Names(MatchSetup setup)
    {
        var names = new Dictionary<int, string>();
        Add(names, setup.Home);
        Add(names, setup.Away);
        return names;

        static void Add(Dictionary<int, string> into, TeamSetup team)
        {
            for (int i = 0; i < team.Players.Count; i++)
            {
                into[team.Players[i].Id] = team.Players[i].Name;
            }
        }
    }
}
