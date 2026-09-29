using Godot;

namespace Underleague.Game.Data;

/// <summary>
/// Ajustes del jugador que sobreviven a la run y al cierre del juego: volumen de música y de efectos y,
/// solo en modo desarrollo, qué pantalla de partido se abre. Viven en <c>user://settings.cfg</c>, aparte
/// del guardado ironman (<c>user://run.json</c>, RT-061): borrar o perder la run no toca los ajustes.
///
/// <para><b>El volumen se aplica sobre la mezcla de partida, no en su lugar.</b> Los buses traen su propio
/// nivel en <c>default_bus_layout.tres</c> (Music a −6 dB, Ambience a −12 dB): esa es la mezcla. El
/// deslizador escala ese nivel —al 100 % suena como siempre, al 50 % seis decibelios por debajo—, así que
/// subirlo al máximo nunca desequilibra la música contra los golpes.</para>
///
/// <para>«Efectos» mueve dos buses, SFX y Ambience: el estadio de fondo es un efecto para el jugador, y un
/// deslizador de efectos que dejara la grada sonando al máximo parecería roto.</para>
/// </summary>
public static class GameSettings
{
    public const string SettingsPath = "user://settings.cfg";

    private const string AudioSection = "audio";
    private const string DevSection = "dev";

    private static readonly string[] MusicBuses = { "Music" };
    private static readonly string[] EffectBuses = { "SFX", "Ambience" };

    private static bool _loaded;
    private static float[]? _baseDb;

    /// <summary>Volumen de la música, de 0 (mudo) a 1 (la mezcla de partida).</summary>
    public static float MusicVolume { get; private set; } = 1f;

    /// <summary>Volumen de efectos y ambiente, de 0 (mudo) a 1 (la mezcla de partida).</summary>
    public static float EffectsVolume { get; private set; } = 1f;

    /// <summary>
    /// Modo desarrollo: cualquier build de depuración, que es lo que exporta hoy
    /// <c>tools/export-windows.sh</c> (<c>--export-debug</c>). Una build de publicación
    /// (<c>--export-release</c>) lo apaga sola, sin tocar código.
    /// </summary>
    public static bool DevMode => OS.IsDebugBuild();

    /// <summary>
    /// El partido se abre en la vista de depuración 2D —tick a tick, con el log de eventos—
    /// (<c>Nav.MatchDebug</c>) en vez de la retransmisión 3D. Solo cuenta en <see cref="DevMode"/>: fuera de
    /// él se ignora aunque esté guardado a true.
    /// </summary>
    public static bool DebugMatchView
    {
        get => DevMode && _debugMatchView;
        private set => _debugMatchView = value;
    }

    private static bool _debugMatchView;

    /// <summary>Lee el fichero (una sola vez) y aplica el volumen. Sin fichero, valores por defecto.</summary>
    public static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var config = new ConfigFile();
        if (config.Load(SettingsPath) == Error.Ok)
        {
            MusicVolume = Mathf.Clamp((float)config.GetValue(AudioSection, "music", 1f), 0f, 1f);
            EffectsVolume = Mathf.Clamp((float)config.GetValue(AudioSection, "effects", 1f), 0f, 1f);
            DebugMatchView = (bool)config.GetValue(DevSection, "debugMatchView", false);
        }

        ApplyAudio();
    }

    public static void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyAudio();
        Save();
    }

    public static void SetEffectsVolume(float value)
    {
        EffectsVolume = Mathf.Clamp(value, 0f, 1f);
        ApplyAudio();
        Save();
    }

    public static void SetDebugMatchView(bool value)
    {
        DebugMatchView = value;
        Save();
    }

    private static void Save()
    {
        var config = new ConfigFile();
        config.SetValue(AudioSection, "music", MusicVolume);
        config.SetValue(AudioSection, "effects", EffectsVolume);
        config.SetValue(DevSection, "debugMatchView", _debugMatchView);
        if (config.Save(SettingsPath) != Error.Ok)
        {
            GD.PushError($"no se pudieron guardar los ajustes en {SettingsPath}");
        }
    }

    private static void ApplyAudio()
    {
        ApplyTo(MusicBuses, MusicVolume);
        ApplyTo(EffectBuses, EffectsVolume);
    }

    private static void ApplyTo(string[] buses, float volume)
    {
        // El nivel de cada bus en el layout se lee la primera vez y se toma como el 100 %: si alguien
        // retoca la mezcla en default_bus_layout.tres, el deslizador la respeta sin cambiar este fichero.
        _baseDb ??= ReadBaseLevels();
        foreach (string bus in buses)
        {
            int index = AudioServer.GetBusIndex(bus);
            if (index < 0)
            {
                continue;
            }

            AudioServer.SetBusMute(index, volume <= 0f);
            if (volume > 0f)
            {
                AudioServer.SetBusVolumeDb(index, _baseDb[index] + Mathf.LinearToDb(volume));
            }
        }
    }

    private static float[] ReadBaseLevels()
    {
        var levels = new float[AudioServer.BusCount];
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = AudioServer.GetBusVolumeDb(i);
        }

        return levels;
    }
}
