using System;
using System.Collections.Generic;
using Godot;

namespace Underleague.Game.Autoload;

/// <summary>
/// Reproductor de audio por <b>pools de carpeta</b>: el código de juego pide un pool
/// —<c>AudioManager.PlayRandomSfx("combat/heavy_hit")</c>— y el gestor elige una variante. Quién llama
/// <b>no sabe qué ficheros existen</b>, así que añadir <c>heavy_hit_06.wav</c> mañana no toca ni una línea
/// de gameplay ni del <c>PresentationDirector</c>.
///
/// <para><b>Tres capas, tres buses.</b> <c>res://audio</c> tiene tres raíces y cada una sale por su bus,
/// para que un deslizador pueda bajar la música sin tocar los golpes:</para>
/// <list type="bullet">
/// <item><c>sfx/</c> → bus <c>SFX</c>: golpes, balón, grada, árbitro. Cortos, se solapan, van al pool de
/// reproductores (<see cref="PlayRandomSfx"/>).</item>
/// <item><c>music/</c> → bus <c>Music</c>: una pista a la vez, en bucle, con <b>fundido cruzado</b> entre
/// pantallas (<see cref="PlayMusic"/>).</item>
/// <item><c>ambience/</c> → bus <c>Ambience</c>: un lecho continuo, el estadio (<see cref="PlayAmbience"/>).</item>
/// </list>
///
/// <para><b>Tres reglas de los efectos, y las tres son de sensación, no de arquitectura:</b></para>
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
///
/// <para><b>Cómo se demuestra que suena</b> sin un altavoz: arrancar con <c>--audio-trace</c> escribe una
/// línea por reproducción (<c>[audio] play sfx/football/kick</c>). En headless el driver es mudo pero la
/// decisión de sonar se toma igual, así que la traza dice qué se habría oído y con qué frecuencia.</para>
/// </summary>
public sealed partial class AudioManager : Node
{
    /// <summary>Raíz del audio. Cada carpeta hoja es un pool y su ruta relativa desde aquí es su nombre.</summary>
    public const string AudioRoot = "res://audio";

    /// <summary>Prefijos de pool de cada capa: son también el nombre de la carpeta bajo <see cref="AudioRoot"/>.</summary>
    private const string SfxPrefix = "sfx/";
    private const string MusicPrefix = "music/";
    private const string AmbiencePrefix = "ambience/";

    /// <summary>Buses. Deben existir en <c>default_bus_layout.tres</c>; si no, Godot manda todo al Master.</summary>
    private const string SfxBus = "SFX";
    private const string MusicBus = "Music";
    private const string AmbienceBus = "Ambience";

    /// <summary>Reproductores del pool de efectos. Ocho bastan para el pico de un partido: la capa de campo
    /// deja ~1 sonido por segundo a ×1 (medido sobre 5.200 partidos) y ninguno dura tanto.</summary>
    private const int PlayerCount = 8;

    /// <summary>Desvío de tono por reproducción, arriba y abajo (0,04 = ±4 %).</summary>
    private const float PitchJitter = 0.04f;

    /// <summary>Desvío de volumen por reproducción, arriba y abajo, en decibelios.</summary>
    private const float VolumeJitterDb = 1.5f;

    /// <summary>Duración del fundido cruzado entre pistas y del fundido del ambiente.</summary>
    private const float FadeSeconds = 1.2f;

    /// <summary>Volumen de «apagado» de un fundido. -40 dB es inaudible sin ser <c>-inf</c>.</summary>
    private const float SilentDb = -40f;

    /// <summary>Extensiones que Godot importa como <see cref="AudioStream"/> y que este gestor reconoce.</summary>
    private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3" };

    private readonly Dictionary<string, SoundPool> _pools = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AudioStreamPlayer> _players = new(PlayerCount);

    private readonly AudioStreamPlayer[] _music = new AudioStreamPlayer[2];
    private AudioStreamPlayer _ambience = null!;
    private int _activeMusic;
    private string? _musicPool;
    private string? _ambiencePool;
    private Tween? _musicTween;
    private Tween? _ambienceTween;

    /// <summary>
    /// Aleatoriedad <b>de presentación</b>: elige variante, tono y volumen. Sembrada con el reloj a
    /// propósito —dos reproducciones del mismo partido suenan distinto y eso es lo que se quiere—, y
    /// aislada de cualquier flujo de la simulación (RT-021 no la alcanza: no decide nada del partido).
    /// </summary>
    private readonly Random _rng = new();

    /// <summary>La instancia del autoload, para que el resto del juego no tenga que buscarla en el árbol.</summary>
    public static AudioManager? Instance { get; private set; }

    /// <summary>Con <c>--audio-trace</c>, cada reproducción deja una línea en consola. Ver el resumen de la clase.</summary>
    public static bool Trace { get; private set; }

    /// <summary>Pools descubiertos al arrancar, ordenados. Solo para diagnóstico y para el volcado de arranque.</summary>
    public IReadOnlyCollection<string> DiscoveredPools => _pools.Keys;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Las dos listas: Godot separa los argumentos que consume el motor de los que van tras "--", y una
        // misma invocación puede poner el indicador en cualquiera de las dos.
        foreach (string arg in OS.GetCmdlineArgs())
        {
            Trace |= arg == "--audio-trace";
        }

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            Trace |= arg == "--audio-trace";
        }

        for (int i = 0; i < PlayerCount; i++)
        {
            var player = new AudioStreamPlayer { Bus = SfxBus, Name = $"SfxPlayer{i}" };
            AddChild(player);
            _players.Add(player);
        }

        for (int i = 0; i < _music.Length; i++)
        {
            _music[i] = new AudioStreamPlayer { Bus = MusicBus, Name = $"Music{i}", VolumeDb = SilentDb };
            AddChild(_music[i]);
        }

        _ambience = new AudioStreamPlayer { Bus = AmbienceBus, Name = "Ambience", VolumeDb = SilentDb };
        AddChild(_ambience);

        DiscoverPools(AudioRoot, string.Empty);

        if (_pools.Count == 0)
        {
            GD.Print($"[audio] sin pools en {AudioRoot}: el juego queda en silencio hasta que haya ficheros");
            return;
        }

        // El recuento de SONIDOS, no solo el de pools: con solo el de pools, la duplicación que la revisión
        // encontró era invisible desde la consola. Un pool con el doble de variantes de las que hay en la
        // carpeta se ve aquí de un vistazo, y con --audio-trace se ve carpeta a carpeta.
        int total = 0;
        foreach (var pool in _pools)
        {
            total += pool.Value.Count;
        }

        GD.Print($"[audio] {_pools.Count} pools · {total} sonidos cargados desde {AudioRoot}");
        if (Trace)
        {
            var names = new List<string>(_pools.Keys);
            names.Sort(StringComparer.Ordinal);
            foreach (string name in names)
            {
                GD.Print($"[audio]   {name}: {_pools[name].Count}");
            }
        }
    }

    /// <summary>
    /// Reproduce una variante del pool de efectos indicado (<c>"combat/heavy_hit"</c>, relativo a
    /// <c>sfx/</c>). Si el pool no existe, no suena nada y se anota una vez: quien llama no tiene que
    /// comprobar si el sonido está puesto todavía.
    /// </summary>
    public void PlayRandomSfx(string pool)
    {
        if (string.IsNullOrEmpty(pool))
        {
            return;
        }

        if (!TryGetPool(SfxPrefix + pool, out var sounds))
        {
            return;
        }

        var player = FreePlayer();
        if (player is null)
        {
            // Los ocho están ocupados: se descarta este golpe en vez de cortar otro a medias. Con el pico
            // real de un partido no debería pasar; si pasa, se sube PlayerCount, no se roba un reproductor.
            // La traza lo dice en vez de callarlo: un montón de "drop" es la señal de que ocho van cortos.
            if (Trace)
            {
                GD.Print($"[audio] drop {SfxPrefix}{pool}");
            }

            return;
        }

        player.Stream = sounds.Next(_rng);
        player.PitchScale = 1f + (((float)_rng.NextDouble() * 2f) - 1f) * PitchJitter;
        player.VolumeDb = (((float)_rng.NextDouble() * 2f) - 1f) * VolumeJitterDb;
        player.Play();

        if (Trace)
        {
            GD.Print($"[audio] play {SfxPrefix}{pool}");
        }
    }

    /// <summary>
    /// Pone la pista del pool indicado (<c>"menu"</c>, <c>"map"</c>, relativo a <c>music/</c>) con fundido
    /// cruzado sobre la que suene. <b>Si ya está sonando ese pool no hace nada</b>: pasar del mapa al
    /// mercado y al equipo es la misma música, y reiniciarla en cada pantalla la delataría como un bucle.
    /// <paramref name="pool"/> nulo o vacío apaga la música.
    /// </summary>
    public void PlayMusic(string? pool)
    {
        if (string.IsNullOrEmpty(pool))
        {
            StopMusic();
            return;
        }

        if (string.Equals(_musicPool, pool, StringComparison.OrdinalIgnoreCase) && _music[_activeMusic].Playing)
        {
            return;
        }

        if (!TryGetPool(MusicPrefix + pool, out var tracks))
        {
            return;
        }

        var outgoing = _music[_activeMusic];
        _activeMusic = 1 - _activeMusic;
        var incoming = _music[_activeMusic];

        incoming.Stream = tracks.Next(_rng);
        incoming.VolumeDb = SilentDb;
        incoming.Play();
        _musicPool = pool;

        _musicTween?.Kill();
        _musicTween = CreateTween().SetParallel();
        _musicTween.TweenProperty(incoming, "volume_db", 0f, FadeSeconds);
        if (outgoing.Playing)
        {
            _musicTween.TweenProperty(outgoing, "volume_db", SilentDb, FadeSeconds);
            _musicTween.Chain().TweenCallback(Callable.From(outgoing.Stop));
        }

        if (Trace)
        {
            GD.Print($"[audio] music {MusicPrefix}{pool}");
        }
    }

    /// <summary>Apaga la música con fundido. Idempotente.</summary>
    public void StopMusic()
    {
        if (_musicPool is null)
        {
            return;
        }

        var outgoing = _music[_activeMusic];
        _musicPool = null;
        _musicTween?.Kill();
        _musicTween = CreateTween();
        _musicTween.TweenProperty(outgoing, "volume_db", SilentDb, FadeSeconds);
        _musicTween.TweenCallback(Callable.From(outgoing.Stop));

        if (Trace)
        {
            GD.Print("[audio] music off");
        }
    }

    /// <summary>
    /// Pone el lecho de ambiente del pool indicado (<c>"stadium"</c>, relativo a <c>ambience/</c>) en
    /// bucle, con fundido de entrada. Si ya es el que suena, no lo reinicia.
    /// </summary>
    public void PlayAmbience(string? pool)
    {
        if (string.IsNullOrEmpty(pool))
        {
            StopAmbience();
            return;
        }

        if (string.Equals(_ambiencePool, pool, StringComparison.OrdinalIgnoreCase) && _ambience.Playing)
        {
            return;
        }

        if (!TryGetPool(AmbiencePrefix + pool, out var beds))
        {
            return;
        }

        _ambience.Stream = beds.Next(_rng);
        _ambience.VolumeDb = SilentDb;
        _ambience.Play();
        _ambiencePool = pool;

        _ambienceTween?.Kill();
        _ambienceTween = CreateTween();
        _ambienceTween.TweenProperty(_ambience, "volume_db", 0f, FadeSeconds);

        if (Trace)
        {
            GD.Print($"[audio] ambience {AmbiencePrefix}{pool}");
        }
    }

    /// <summary>Apaga el ambiente con fundido. Idempotente.</summary>
    public void StopAmbience()
    {
        if (_ambiencePool is null)
        {
            return;
        }

        _ambiencePool = null;
        _ambienceTween?.Kill();
        _ambienceTween = CreateTween();
        _ambienceTween.TweenProperty(_ambience, "volume_db", SilentDb, FadeSeconds);
        _ambienceTween.TweenCallback(Callable.From(_ambience.Stop));

        if (Trace)
        {
            GD.Print("[audio] ambience off");
        }
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

    /// <summary>El pool, o false con un aviso <b>una sola vez</b> si todavía no tiene ficheros.</summary>
    private bool TryGetPool(string fullName, out SoundPool pool)
    {
        if (_pools.TryGetValue(fullName, out pool!))
        {
            return true;
        }

        if (_warned.Add(fullName))
        {
            GD.Print($"[audio] pool '{fullName}' sin sonidos todavía: la llamada se ignora");
        }

        return false;
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
    /// Recorre el árbol de <see cref="AudioRoot"/> y registra como pool cada carpeta que contenga audio. El
    /// nombre del pool es su ruta relativa con barras (<c>sfx/combat/heavy_hit</c>), así que la estructura
    /// de carpetas <b>es</b> la API: no hay ninguna lista de sonidos que mantener en el código.
    /// </summary>
    private void DiscoverPools(string path, string poolName)
    {
        using var dir = DirAccess.Open(path);
        if (dir is null)
        {
            return;
        }

        // Godot sirve los recursos importados: en el proyecto aparecen el fuente Y su "algo.wav.import",
        // y en una build exportada puede aparecer solo uno de los dos. Se normaliza a la ruta del recurso
        // y **se quitan los repetidos**: sin esa criba, cada variante entraba DOS veces en su bolsa —dos
        // seguidas iguales dejaban de ser imposibles, que es justo lo que la bolsa promete— y los pools de
        // un solo fichero contaban dos. Lo encontró la revisión independiente midiendo los identificadores
        // de instancia, no leyendo el código: el fallo era invisible mientras las carpetas estaban vacías.
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in dir.GetFiles())
        {
            string name = file.EndsWith(".import", StringComparison.Ordinal)
                ? file[..^".import".Length]
                : file;

            if (IsAudio(name))
            {
                names.Add(name);
            }
        }

        // Orden estable por nombre (lo da el SortedSet): la bolsa baraja, pero el punto de partida no
        // depende del orden en que el sistema de ficheros devuelva las entradas.
        var streams = new List<AudioStream>();
        foreach (string name in names)
        {
            if (ResourceLoader.Load($"{path}/{name}") is AudioStream stream)
            {
                streams.Add(stream);
            }
        }

        if (streams.Count > 0 && poolName.Length > 0)
        {
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

        /// <summary>Variantes del pool. Solo para el volcado de arranque: que se vea si alguna está repetida.</summary>
        public int Count => _all.Count;

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
