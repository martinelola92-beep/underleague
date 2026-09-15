using Underleague.Sim.Data;
using Underleague.Sim.Engine;
using Underleague.Sim.Events;

namespace Underleague.Sim.Run.View;

/// <summary>
/// Un aviso que la pantalla de partido pinta sobre la cabeza de un jugador cuando un perk suyo se activa
/// (C9). Es <b>dato estructurado, no texto pintado</b>, igual que <see cref="MatchLogLine"/>: aquí se
/// resuelve el fotograma y el nombre del perk, y la interfaz decide cómo se dibuja (RT-014: el render
/// consume eventos, nunca calcula ni decide nada del partido).
/// </summary>
/// <param name="Frame">Fotograma de la traza en el que se disparó; la traza tiene uno por tick (RT-020).</param>
/// <param name="Player">Índice del portador en <c>MatchTrace.Players</c>, que es con lo que se dibuja.</param>
/// <param name="PlayerId">Id del portador, para cruzarlo con el informe o con la plantilla.</param>
/// <param name="Team">Equipo del portador, para que la interfaz pueda colorearlo.</param>
/// <param name="PerkId">Id del perk, por si la interfaz quiere un icono o un efecto propio.</param>
/// <param name="Name">Nombre visible del perk.</param>
public sealed record MatchFlash(int Frame, int Player, int PlayerId, int Team, string PerkId, string Name);

/// <summary>
/// Compone los avisos de activación de perk de un partido ya jugado.
///
/// <para><b>Por qué existe.</b> Hasta C9 la activación de un perk solo llegaba al informe de <b>después</b>
/// del partido, así que durante el partido ningún perk se veía: la auditoría de diseño midió 41 de 61
/// perks como imperceptibles o casi. El aviso no arregla por sí solo un perk que no cambia lo que alguien
/// intenta hacer —eso es diseño—, pero es condición necesaria para poder <b>atribuir</b> el que sí lo
/// cambia.</para>
/// </summary>
public static class MatchFlashView
{
    /// <summary>Cuántos fotogramas dura el aviso: 1 s a 15 ticks/s (RT-020).</summary>
    public const int DurationFrames = 15;

    /// <summary>
    /// Los avisos del partido, por fotograma ascendente y, dentro de un fotograma, por índice de jugador
    /// ascendente (RT-041: el orden nunca depende de un diccionario).
    /// </summary>
    /// <param name="events">Eventos del partido (<c>MatchResult.Events</c>).</param>
    /// <param name="trace">Traza del partido: pone el tick en su fotograma y el id en su ficha.</param>
    /// <param name="catalog">Catálogo, para el nombre del perk; un id desconocido se pinta tal cual.</param>
    public static IReadOnlyList<MatchFlash> Build(
        IReadOnlyList<MatchEvent> events, MatchTrace trace, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(catalog);

        var rows = new List<MatchFlash>();
        for (int i = 0; i < events.Count; i++)
        {
            var matchEvent = events[i];
            if (matchEvent.Type != EventType.PerkTriggered)
            {
                continue;
            }

            int player = IndexOf(trace, matchEvent.Actor);
            if (player < 0)
            {
                continue;
            }

            var perk = catalog.Perks.Find(matchEvent.Detail);
            rows.Add(new MatchFlash(
                trace.FrameOfTick(matchEvent.Tick),
                player,
                matchEvent.Actor,
                matchEvent.Team,
                matchEvent.Detail,
                perk?.Name.Es ?? matchEvent.Detail));
        }

        rows.Sort(static (a, b) => a.Frame != b.Frame ? a.Frame.CompareTo(b.Frame) : a.Player.CompareTo(b.Player));
        return rows;
    }

    private static int IndexOf(MatchTrace trace, int playerId)
    {
        var players = trace.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id == playerId)
            {
                return i;
            }
        }

        return -1;
    }
}
