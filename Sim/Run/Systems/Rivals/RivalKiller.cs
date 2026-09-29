using Underleague.Sim.Data;

namespace Underleague.Sim.Run.Systems.Rivals;

/// <summary>
/// BS-A (ADR 0165): quién ocupaba el puesto del rival que mató a un jugador propio, en el momento del partido. Los
/// créditos de rival identifican al matador por equipo de acto y puesto, y ese puesto puede cambiar de ocupante (un
/// fichaje tras una muerte, un némesis traspasado); sin esto la esquela nombraba al jugador de datos.
///
/// <para>Se guarda como un entero en <see cref="RunState.DeathKillerPrefix"/> + id del muerto (contabilidad de clave
/// libre: no sube el guardado). Código: 0 = el jugador de datos (o no consta); 1..<see cref="NemesisBase"/>−1 = el
/// fichaje de generación <c>código − 1</c>; <see cref="NemesisBase"/> + id = ese némesis. El valor se guarda, así que
/// <b>no se renumera</b>.</para>
/// </summary>
public static class RivalKiller
{
    public const int NemesisBase = 1_000_000;

    /// <summary>Código del ocupante de ese puesto con la memoria de antes del partido.</summary>
    public static int Encode(RivalTeam team, RivalMemory memory, int slot)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(memory);
        if (memory.ActiveAt(team.ClanId, slot) is { } nemesis)
        {
            return NemesisBase + nemesis.Id;
        }

        return memory.VacancyAt(team.ClanId, slot) is { } vacancy ? vacancy.Generation + 1 : 0;
    }

    /// <summary>Nombre del matador a partir de su código; el jugador de datos si no consta.</summary>
    public static string Name(int code, RivalTeam team, int slot, RivalMemory memory, ulong seed, Catalog catalog)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(memory);
        if (code >= NemesisBase && memory.Find(code - NemesisBase) is { } nemesis)
        {
            return nemesis.Name;
        }

        if (code > 0 && code < NemesisBase)
        {
            return RivalRoster.SigningName(team, new RivalVacancy(team.ClanId, slot, code - 1), seed, catalog);
        }

        return team.Players[slot].Name;
    }
}
