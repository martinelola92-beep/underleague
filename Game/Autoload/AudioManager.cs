using System;
using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Autoload;

/// <summary>
/// Reproductor de efectos de sonido por <b>pools de carpeta</b>: el código de juego pide un pool
/// —<c>AudioManager.PlayRandomSfx("combat/heavy_hit")</c>— y el gestor elige una variante. Quién llama
/// <b>no sabe qué ficheros existen</b>, así que añadir <c>heavy_hit_06.wav</c> mañana no toca ni una línea
/// de gameplay ni del <c>PresentationDirector</c>.
///
/// <para><b>Tres reglas, y las tres son de sensación, no de arquitectura:</b></para>
/// <list type="number">
/// <item><b>Bolsa barajada</b>, no azar puro: las N variantes entran en una bolsa, se barajan, suenan
/// todas una vez y entonces se rebaraja. Evita el <c>heavy_hit_02 · heavy_hit_02 · heavy_hit_02</c> que
/// delata el truco. Al rebarajar, si la primera de la bolsa nueva es la última que sonó, se cambia por la
/// segunda: el corte entre bolsas tampoco repite.</item>
/// <item><b>Variación por reproducción</b>: un pequeño desvío de tono y volumen en cada golpe
/// (<see cref="PitchJitter"/>, <see cref="VolumeJitterDb"/>). Con cinco ficheros suenan muchos más.</item>
/// <item><b>Pool de reproductores</b>: N <see cref="AudioStreamPlayer"/> creados una vez y reutilizados
/// (el patrón de la skill <c>audio-system</c>), en vez de instanciar un nodo por golpe.</item>
/// </list>
///
/// <para><b>Frontera</b> (RT-011/RT-014): esto es presentación pura. Consume lo que el director decide y
/// no devuelve nada a la simulación. Su aleatoriedad es <b>suya</b> y no sale de ningún flujo de
/// <c>RngStreams</c>: que dos reproducciones del mismo partido suenen distinto es correcto —el resultado
/// no cambia— y el determinismo de RT-021 no la alcanza.</para>
///
/// <para><b>Tolerante a que no haya sonidos.</b> Mientras no existan los ficheros, cada llamada es un
/// no-op silencioso que se anota <b>una sola vez</b> por pool: el juego no revienta ni llena la consola
/// mientras el set de audio está en camino.</para>
/// </summary>
public sealed partial class AudioManager : Node
{
    /// <summary>Raíz de los efectos. Cada carpeta hoja es un pool y su ruta relativa es su nombre.</summary>
    public const string SfxRoot = "res://audio/sfx";

    /// <summary>Bus de efectos. Debe existir en el layout de buses; si no, Godot los manda al Master.</summary>
    private const string SfxBus = "SFX";

    /// <summary>Reproductores del pool. Ocho bastan para el pico de un partido (golpe + grito + grada).</summary>
    private const int PlayerCount = 8;

    /// <summary>Desvío de tono por reproducción, arriba y abajo (0,04 = ±4 %).</summary>
    private const float PitchJitter = 0.04f;

    /// <summary>Desvío de volumen por reproducción, arriba y abajo, en decibelios.</summary>
    private const float VolumeJitterDb = 1.5f;

    /// <summary>Extensiones que Godot importa como <see cref="AudioStream"/> y que este gestor reconoce.</summary>
    private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3" };

    private readonly Dictionary<string, SoundPool> _pools = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);
    private readonly List<AudioStreamPlayer> _players = new(PlayerCount);

    /// <summary>
    /// Aleatoriedad <b>de presentación</b>: elige variante, tono y volumen. Sembrada con el reloj a
    /// propósito —dos reproducciones del mismo partido suenan distinto y eso es lo que se quiere—, y
    /// aislada de cualquier flujo de la simulación (RT-021 no la alcanza: no decide nada del partido).
    /// </summary>
    private readonly Random _rng = new();

    /// <summary>La instancia del autoload, para que el resto del juego no tenga que buscarla en el árbol.</summary>
    public static AudioManager? Instance { get; private set; }

    /// <summary>Pools descubiertos al arrancar, ordenados. Solo para diagnóstico y para el volcado de arranque.</summary>
    public IReadOnlyCollection<string> DiscoveredPools => _pools.Keys;

    public override void _Ready()
    {
        Instance = this;

        for (int i = 0; i < PlayerCount; i++)
        {
            var player = new AudioStreamPlayer { Bus = SfxBus, Name = $"SfxPlayer{i}" };
            AddChild(player);
            _players.Add(player);
        }

        DiscoverPools(SfxRoot, string.Empty);

        GD.Print(_pools.Count == 0
            ? $"[audio] sin pools en {SfxRoot}: los efectos quedan en silencio hasta que haya ficheros"
            : $"[audio] {_pools.Count} pools cargados desde {SfxRoot}");
    }

    /// <summary>
    /// Reproduce una variante del pool indicado (<c>"combat/heavy_hit"</c>). Si el pool no existe, no
    /// suena nada y se anota una vez: quien llama no tiene que comprobar si el sonido está puesto todavía.
    /// </summary>
    public void PlayRandomSfx(string pool)
    {
        if (string.IsNullOrEmpty(pool))
        {
            return;
        }

        if (!_pools.TryGetValue(pool, out var sounds))
        {
            if (_warned.Add(pool))
            {
                GD.Print($"[audio] pool '{pool}' sin sonidos todavía: la llamada se ignora");
            }

            return;
        }

        var player = FreePlayer();
        if (player is null)
        {
            // Los ocho están ocupados: se descarta este golpe en vez de cortar otro a medias. Con el pico
            // real de un partido no debería pasar; si pasa, se sube PlayerCount, no se roba un reproductor.
            return;
        }

        player.Stream = sounds.Next(_rng);
        player.PitchScale = 1f + (((float)_rng.NextDouble() * 2f) - 1f) * PitchJitter;
        player.VolumeDb = (((float)_rng.NextDouble() * 2f) - 1f) * VolumeJitterDb;
        player.Play();
    }

    /// <summary>Volumen de un bus desde un deslizador 0..1. Silencia de verdad en 0 (evita <c>-inf</c> dB).</summary>
    public static void SetBusVolumeLinear(string busName, float linear)
    {
        int index = AudioServer.GetBusIndex(busName);
        if (index < 0)
        {
            return;
        }

        bool mute = linear <= 0.001f;
        AudioServer.SetBusMute(index, mute);
        if (!mute)
        {
            AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(linear));
        }
    }

    private AudioStreamPlayer? FreePlayer()
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (!_players[i].Playing)
            {
                return _players[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Recorre el árbol de <see cref="SfxRoot"/> y registra como pool cada carpeta que contenga audio. El
    /// nombre del pool es su ruta relativa con barras (<c>combat/heavy_hit</c>), así que la estructura de
    /// carpetas <b>es</b> la API: no hay ninguna lista de sonidos que mantener en el código.
    /// </summary>
    private void DiscoverPools(string path, string poolName)
    {
        using var dir = DirAccess.Open(path);
        if (dir is null)
        {
            return;
        }

        var streams = new List<AudioStream>();
        foreach (string file in dir.GetFiles())
        {
            // Godot sirve los recursos importados: en una build exportada el fichero fuente aparece como
            // "algo.wav.import" o directamente como "algo.wav", según el modo. Se normaliza a la ruta del
            // recurso y se deja que ResourceLoader diga si existe.
            string name = file.EndsWith(".import", StringComparison.Ordinal)
                ? file[..^".import".Length]
                : file;

            if (!IsAudio(name))
            {
                continue;
            }

            string resourcePath = $"{path}/{name}";
            if (ResourceLoader.Load(resourcePath) is AudioStream stream)
            {
                streams.Add(stream);
            }
        }

        if (streams.Count > 0 && poolName.Length > 0)
        {
            // Orden estable por nombre: la bolsa baraja, pero el punto de partida no depende del orden en
            // que el sistema de ficheros devuelva las entradas.
            streams.Sort(static (a, b) => string.CompareOrdinal(a.ResourcePath, b.ResourcePath));
            _pools[poolName] = new SoundPool(streams);
        }

        foreach (string sub in dir.GetDirectories())
        {
            DiscoverPools($"{path}/{sub}", poolName.Length == 0 ? sub : $"{poolName}/{sub}");
        }
    }

    private static bool IsAudio(string file)
    {
        foreach (string extension in AudioExtensions)
        {
            if (file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Las variantes de un pool con su <b>bolsa barajada</b>: se reparten todas antes de repetir ninguna, y
    /// al rebarajar no se permite que la primera sea la última que sonó.
    /// </summary>
    private sealed class SoundPool
    {
        private readonly List<AudioStream> _all;
        private readonly List<AudioStream> _bag;
        private AudioStream? _last;

        public SoundPool(List<AudioStream> all)
        {
            _all = all;
            _bag = new List<AudioStream>(all.Count);
        }

        public AudioStream Next(Random rng)
        {
            if (_all.Count == 1)
            {
                return _all[0];
            }

            if (_bag.Count == 0)
            {
                Refill(rng);
            }

            var chosen = _bag[^1];
            _bag.RemoveAt(_bag.Count - 1);
            _last = chosen;
            return chosen;
        }

        private void Refill(Random rng)
        {
            _bag.AddRange(_all);
            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }

            // La bolsa se consume por el final, así que el próximo en sonar es _bag[^1]. Si es el mismo que
            // acaba de sonar, se intercambia con el anterior: dos bolsas seguidas no pueden empezar y
            // terminar con la misma variante.
            if (_bag.Count > 1 && ReferenceEquals(_bag[^1], _last))
            {
                (_bag[^1], _bag[^2]) = (_bag[^2], _bag[^1]);
            }
        }
    }
}
