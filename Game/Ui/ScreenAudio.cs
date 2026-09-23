using Underleague.Game.Autoload;

namespace Underleague.Game.Ui;

/// <summary>
/// Qué se oye <b>de fondo</b> en cada pantalla: la música y el lecho de ambiente. Una sola tabla, por el
/// mismo motivo que <c>MomentSounds</c> es una sola tabla — si cada pantalla eligiera su música en su
/// <c>_Ready</c>, la que se olvidara heredaría la de la anterior y nadie sabría dónde mirar.
///
/// <para><b>Tres fondos y tres estados de la run</b>, que es la distinción que el jugador nota:</para>
/// <list type="bullet">
/// <item><b>Menú</b> (<c>music/menu</c>): antes de empezar y después de morir. La run no está en marcha.</item>
/// <item><b>Gestión</b> (<c>music/map</c>): mapa, ojeo, equipo, mercado, clínica, informe, recompensa. Es
/// una sola música <b>continua</b> —el gestor no la reinicia al cambiar de pantalla— porque para el jugador
/// todo eso es el mismo sitio: el rato entre dos partidos.</item>
/// <item><b>Partido</b>: <b>sin música</b> y con el estadio en bucle (<c>ambience/stadium</c>). Ahí la banda
/// sonora son los eventos —el balón, la entrada, el hueso, la grada— y una melodía encima taparía
/// precisamente lo que hay que oír.</item>
/// </list>
///
/// <para>Se aplica desde <see cref="Nav.Go"/>, que es por donde pasan todos los cambios de pantalla, y por
/// eso el arnés de capturas —que navega con <see cref="Nav.Suppressed"/>— se queda en silencio sin tener
/// que saber nada de audio.</para>
/// </summary>
public static class ScreenAudio
{
    /// <summary>Pone la música y el ambiente que le tocan a esa escena. Idempotente: repetir la llamada con la misma pantalla no reinicia nada.</summary>
    public static void Apply(string scene)
    {
        var audio = AudioManager.Instance;
        if (audio is null)
        {
            return;
        }

        switch (scene)
        {
            case Nav.Match:
            case Nav.MatchDebug:
                audio.PlayMusic(null);
                audio.PlayAmbience("stadium");
                break;

            case Nav.Start:
            case Nav.End:
                audio.PlayMusic("menu");
                audio.PlayAmbience(null);
                break;

            default:
                audio.PlayMusic("map");
                audio.PlayAmbience(null);
                break;
        }
    }
}
