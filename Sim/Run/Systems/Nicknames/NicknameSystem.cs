using Underleague.Sim.Engine;

namespace Underleague.Sim.Run.Systems.Nicknames;

/// <summary>
/// Derivación del apodo de un jugador de la run (ADR 0163, RF-122). Pura y determinista: sin estado, sin
/// RNG, sin E/S. El apodo <b>no se guarda</b>: sale de <see cref="RunCareer"/>, que solo crece, así que un
/// jugador solo cambia de apodo hacia uno de más prioridad y no hace falta subir el guardado.
/// </summary>
public static class NicknameSystem
{
    /// <summary>Valor del campo de la carrera sobre el que un apodo pone su umbral.</summary>
    public static int Value(RunCareer career, NicknameStat stat)
    {
        ArgumentNullException.ThrowIfNull(career);
        return stat switch
        {
            NicknameStat.Matches => career.Matches,
            NicknameStat.Goals => career.Goals,
            NicknameStat.Assists => career.Assists,
            NicknameStat.Tackles => career.Tackles,
            NicknameStat.TacklesWon => career.TacklesWon,
            NicknameStat.Fouls => career.Fouls,
            NicknameStat.Cards => career.Cards,
            NicknameStat.InjuriesCaused => career.InjuriesCaused,
            NicknameStat.DeathsCaused => career.DeathsCaused,
            NicknameStat.InjuriesSuffered => career.InjuriesSuffered,
            _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "campo de carrera desconocido"),
        };
    }

    /// <summary>
    /// Apodo de una carrera: el de mayor prioridad cuya condición cumple. El catálogo ya viene ordenado
    /// por prioridad descendente y luego id ascendente (RT-097), así que el primero que cumple gana.
    /// Null si ninguno.
    /// </summary>
    public static NicknameDefinition? For(RunCareer career, NicknameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(career);
        ArgumentNullException.ThrowIfNull(catalog);

        var all = catalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (Value(career, all[i].Stat) >= all[i].Threshold)
            {
                return all[i];
            }
        }

        return null;
    }

    /// <summary>Apodo de un jugador de la run según su carrera actual; null si no ha ganado ninguno.</summary>
    public static NicknameDefinition? For(RunPlayer player, NicknameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(player);
        return For(player.Career, catalog);
    }

    /// <summary>
    /// Apodo <b>nuevo</b> ganado al pasar de <paramref name="before"/> a <paramref name="after"/>: el de
    /// después si existe y es distinto del de antes (ascender de «el Carnicero» a «el Matadero» también se
    /// anuncia). Null si no cambió.
    /// </summary>
    public static NicknameDefinition? Earned(RunCareer before, RunCareer after, NicknameCatalog catalog)
    {
        var was = For(before, catalog);
        var now = For(after, catalog);
        return now is not null && !string.Equals(was?.Id, now.Id, StringComparison.Ordinal) ? now : null;
    }

    /// <summary>
    /// Carrera de antes de un partido, deshaciendo <c>RunPlayer.WithCareerFrom</c> (es su inversa exacta:
    /// mismas sumas, mismas condiciones). Existe para que el informe post-partido compare «antes» y
    /// «después» sin que el estado tenga que guardar una copia.
    /// </summary>
    public static RunCareer BeforeMatch(RunCareer after, PlayerMatchStats stats)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(stats);
        return after with
        {
            Matches = after.Matches - (stats.TicksOnPitch > 0 ? 1 : 0),
            Goals = after.Goals - stats.Goals,
            Assists = after.Assists - stats.Assists,
            Tackles = after.Tackles - stats.Tackles - stats.OffBallTackles,
            TacklesWon = after.TacklesWon - stats.TacklesWon,
            Fouls = after.Fouls - stats.Fouls,
            Cards = after.Cards - stats.Cards,
            InjuriesCaused = after.InjuriesCaused - stats.InjuriesCaused,
            DeathsCaused = after.DeathsCaused - stats.DeathsCaused,
            InjuriesSuffered = after.InjuriesSuffered - (stats.Injured ? 1 : 0),
            TicksOnPitch = after.TicksOnPitch - stats.TicksOnPitch,
        };
    }

    /// <summary>Nombre con el apodo entre comillas angulares; el nombre solo si no hay apodo.</summary>
    public static string Display(string name, string nickname) =>
        nickname.Length == 0 ? name : name + " «" + nickname + "»";
}
