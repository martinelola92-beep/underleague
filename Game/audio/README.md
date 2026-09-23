# Audio — cómo se añaden sonidos

**La estructura de carpetas es la API.** El código de juego pide un *pool* por su nombre y el
`AudioManager` (autoload, `Game/Autoload/AudioManager.cs`) elige la variante:

```csharp
AudioManager.Instance?.PlayRandomSfx("combat/heavy_hit");   // un golpe
AudioManager.Instance?.PlayMusic("map");                    // la música de gestión, con fundido
AudioManager.Instance?.PlayAmbience("stadium");             // el lecho del estadio, en bucle
```

Nadie en el juego sabe qué ficheros existen. Para añadir una variante nueva **se copia el fichero en su
carpeta y se importa** (abajo): ni gameplay, ni `PresentationDirector`, ni esta documentación cambian.

## Dónde va cada cosa

```
Game/audio/
├── sfx/         → bus SFX        cortos, se solapan, salen por el pool de reproductores
│   ├── combat/      light_hit · medium_hit · heavy_hit · crush · tackle · fall
│   ├── football/    kick · ball_hit · goal · net
│   ├── players/     grunt · shout · pain
│   ├── crowd/       cheer · boo · gasp · goal · woah
│   ├── referee/     whistle · horn
│   └── death/       el cuerno de la muerte
├── music/       → bus Music      una pista a la vez, en bucle, con fundido cruzado
│   ├── menu/        antes de empezar y después de morir
│   └── map/         todo el rato entre dos partidos (mapa, ojeo, equipo, mercado, clínica, informe)
└── ambience/    → bus Ambience   un lecho continuo
    └── stadium/     el estadio durante el partido
```

Cada **carpeta hoja es un pool** y su ruta relativa es su nombre (`sfx/combat/heavy_hit`, que el código
pide como `combat/heavy_hit`; `music/map` como `map`). Una carpeta vacía es un pool que no existe todavía:
las llamadas se ignoran en silencio y se anotan una vez en consola, así que el juego funciona igual
mientras faltan sonidos.

**Un fichero suelto que no esté en una carpeta hoja no es un pool** y nadie lo encontrará. Si dejas algo en
`ambience/` a pelo, hazle su carpeta.

## Nombre de los ficheros

```
heavy_hit_01.wav
heavy_hit_02.wav
...
```

El nombre no lo lee nadie —el gestor coge todo lo que haya en la carpeta—, pero numerar ordena y evita
duplicados. Formatos reconocidos: `.wav`, `.ogg`, `.mp3`.

**WAV 16 bits a 44,1 kHz para efectos** (Godot los guarda sin comprimir y suenan sin latencia de
descodificación) y **OGG para música o ambientes largos**. El MP3 mete silencio al principio: no usarlo en
efectos salvo que el arranque no importe (el cuerno de `sfx/death/` es MP3 y se le tolera porque no tiene
que caer en un fotograma exacto). Un fundido de 2-5 ms al principio y al final evita los chasquidos.

## Un paso que se olvida: importar

Godot no ve un `.wav` hasta que lo **importa** (te crea un `.wav.import` al lado). Si copias ficheros con
el editor abierto lo hace solo; si los copias desde fuera —o trabajas en WSL sin editor, que es el caso—
hay que pedírselo una vez:

```bash
timeout 420 godot --path Game --headless --import
```

Sin eso el pool sigue apareciendo vacío y el gestor dice `sin pools en res://audio`, que es exactamente lo
que parece un fallo del código y no lo es. **Comprobado el 23 sep 2026**: con tres WAV sueltos el gestor no
los veía; tras el `--import`, `[audio] 1 pools cargados`.

**Música y ambiente necesitan además el bucle**, que es un ajuste de importación y no algo que el código
pueda poner: en el `.import` del fichero, `loop=false` → `loop=true`, y volver a importar. Sin eso la
pista suena una vez y el mapa se queda en silencio a los dos minutos.

## Lo que el gestor hace por ti

1. **Bolsa barajada, no azar puro.** Las N variantes se barajan, suenan todas una vez y entonces se
   rebaraja. Nunca oirás `heavy_hit_02 · heavy_hit_02 · heavy_hit_02`. Además, al rebarajar no deja que la
   primera de la bolsa nueva sea la última que sonó: el corte entre bolsas tampoco repite.
2. **Variación en cada golpe**: ±4 % de tono y ±1,5 dB de volumen. Con cinco ficheros suenan muchos más.
3. **Pool de ocho reproductores** reutilizados, en vez de crear un nodo por sonido.
4. **La música no se reinicia** si ya está sonando ese pool: pasar del mapa al mercado y al equipo es la
   misma música, continua, porque para el jugador todo eso es el mismo sitio.

Con eso, cinco variantes de `heavy_hit` dan 5 × (tono × volumen) combinaciones perceptibles y sin patrón
audible.

## Qué suena hoy

**Dos capas, y la distinción importa** porque decide dónde se añade un sonido nuevo:

| capa | de qué cuelga | dónde está la tabla | qué pone |
|---|---|---|---|
| **retransmisión** | `MomentKind` (el director, ADR 0119) | `Game/Match/MomentSounds.cs` | lo que el pregón subraya: silbato, gol, grada, dolor, muerte, final |
| **campo** | evento crudo del partido | `Game/Match/MatchEventSounds.cs` | el cuerpo del césped: balón, golpeo, entrada, caída, hueso |

Un momento llega **cuando se presenta**, no cuando ocurre, y solo hay ~8,6 por partido. Un pase, una
entrada o un tiro no son momentos —nadie los cuenta— y son casi todo lo que pasa. Por eso hacen falta las
dos: con una sola, el campo sonaría ocho veces y estaría mudo el resto.

Donde las dos tienen algo que decir, **cada una dice lo suyo**: el hueso cruje en la casilla (campo) y el
grito llega con el sello (retransmisión). Así la lesión grave suena a crujido-y-luego-grito sin que nadie
sincronice nada.

**La capa de campo solo suena a ×1** (`docs/ui/README` §4: «la velocidad degrada la presentación, nunca la
información»): a ×16 serían dieciséis golpes por segundo. Los momentos siguen sonando a cualquier
velocidad, porque son lo que se está contando.

**Pools todavía vacíos**, que suenan a nada y no rompen nada: `combat/tackle`, `combat/heavy_hit`,
`combat/medium_hit`, `combat/light_hit`, `football/net` y `referee/horn`. Los dos primeros son los que más
se notarían: hoy la entrada se apoya en `players/grunt` y el cuerno del árbitro lo sostiene la bronca.
Cuando haya ficheros, es **una línea de la tabla**, no un cambio de diseño.

## Buses

Tres buses bajo Master, definidos en `Game/default_bus_layout.tres`: **`SFX`** (efectos), **`Music`**
(−6 dB, para que el partido se oiga por encima de su propia música) y **`Ambience`** (−12 dB, es un lecho,
no un protagonista). Si un bus no existe en el layout, Godot manda ese sonido al `Master` y todo suena
igual, solo que sin control de volumen separado. El volumen se ajusta con:

```csharp
AudioManager.SetBusVolumeLinear("Music", 0.7f);   // 0..1 desde un deslizador; en 0 silencia de verdad
```

## Cómo se demuestra que suena, sin altavoz

En WSL no hay tarjeta de sonido y las capturas corren con `--audio-driver Dummy`, así que «lo he oído» no
es una opción. El indicador **`--audio-trace`** escribe una línea por reproducción:

```bash
timeout 700 xvfb-run -a --server-args="-screen 0 1280x800x24" godot --path Game \
  --scene res://Scenes/CapturasRetransmision.tscn --rendering-driver opengl3 --audio-driver Dummy \
  -- --audio-trace
# [audio] 17 pools cargados desde res://audio
# [audio] music music/menu
# [audio] play sfx/football/kick
# [audio] drop sfx/crowd/boo      ← los ocho reproductores ocupados: si abundan, ocho van cortos
```

El driver es mudo pero la decisión de sonar se toma igual, así que la traza dice qué se habría oído, en qué
orden y con qué frecuencia. Con `grep '\[audio\] play' … | sort | uniq -c` sale el reparto por pool.

## Aleatoriedad y determinismo

La elección de variante, el tono y el volumen usan un generador **propio del gestor**, sembrado con el
reloj. No sale de ningún flujo de `RngStreams` ni vuelve a la simulación: dos reproducciones del mismo
partido suenan distinto y el resultado es el mismo. RT-021 (determinismo) no alcanza a esto porque no
decide nada del partido — y por la misma razón, **nada de audio puede influir en `/Sim`** (RT-011/RT-014).
