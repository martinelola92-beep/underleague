# Audio — cómo se añaden sonidos

**La estructura de carpetas es la API.** El código de juego pide un *pool* por su nombre y el
`AudioManager` (autoload, `Game/Autoload/AudioManager.cs`) elige la variante:

```csharp
AudioManager.Instance?.PlayRandomSfx("combat/heavy_hit");
```

Nadie en el juego sabe qué ficheros existen. Para añadir una variante nueva **se copia el fichero en su
carpeta y ya está**: ni gameplay, ni `PresentationDirector`, ni esta documentación cambian.

## Dónde va cada cosa

```
Game/audio/sfx/
├── combat/      light_hit · medium_hit · heavy_hit · crush · tackle · fall
├── football/    kick · ball_hit · goal · net
├── players/     grunt · shout · pain
├── crowd/       cheer · boo · gasp
└── referee/     whistle · horn
```

Cada **carpeta hoja es un pool** y su ruta relativa es su nombre (`combat/heavy_hit`). Una carpeta vacía es
un pool que no existe todavía: las llamadas se ignoran en silencio y se anotan una vez en consola, así que
el juego funciona igual mientras faltan sonidos.

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
efectos. Un fundido de 2-5 ms al principio y al final evita los chasquidos.

## Un paso que se olvida: importar

Godot no ve un `.wav` hasta que lo **importa** (te crea un `.wav.import` al lado). Si copias ficheros con
el editor abierto lo hace solo; si los copias desde fuera —o trabajas en WSL sin editor, que es el caso—
hay que pedírselo una vez:

```bash
timeout 420 godot --path Game --headless --import
```

Sin eso el pool sigue apareciendo vacío y el gestor dice `sin pools en res://audio/sfx`, que es
exactamente lo que parece un fallo del código y no lo es. **Comprobado el 23 sep 2026**: con tres WAV
sueltos el gestor no los veía; tras el `--import`, `[audio] 1 pools cargados desde res://audio/sfx`.

## Lo que el gestor hace por ti

1. **Bolsa barajada, no azar puro.** Las N variantes se barajan, suenan todas una vez y entonces se
   rebaraja. Nunca oirás `heavy_hit_02 · heavy_hit_02 · heavy_hit_02`. Además, al rebarajar no deja que la
   primera de la bolsa nueva sea la última que sonó: el corte entre bolsas tampoco repite.
2. **Variación en cada golpe**: ±4 % de tono y ±1,5 dB de volumen. Con cinco ficheros suenan muchos más.
3. **Pool de ocho reproductores** reutilizados, en vez de crear un nodo por sonido.

Con eso, cinco variantes de `heavy_hit` dan 5 × (tono × volumen) combinaciones perceptibles y sin patrón
audible.

## Qué suena hoy, y qué no

El enganche vivo es la **capa de momentos**: `Game/Match/MomentSounds.cs` traduce cada `MomentKind` de la
simulación a un pool, y `BroadcastScreen` lo dispara cuando el momento empieza a presentarse. Gol, falta,
tarjetas, lesiones, muerte, turba, final.

**Los pools de `combat/` todavía no los dispara nadie.** Cuelgan de un *evento* suelto del partido —la
entrada, el derribo, la lesión— y no de un momento, y para eso la pantalla tiene que recorrer los eventos
de cada fotograma, cosa que hoy no hace. Las carpetas están puestas para cuando se haga; el gesto será el
mismo `PlayRandomSfx`.

## Buses

Los reproductores de efectos salen por el bus **`SFX`**. Si ese bus no existe en el layout, Godot los manda
al `Master` y todo suena igual, solo que sin control de volumen separado. El volumen se ajusta con:

```csharp
AudioManager.SetBusVolumeLinear("SFX", 0.7f);   // 0..1 desde un deslizador; en 0 silencia de verdad
```

## Aleatoriedad y determinismo

La elección de variante, el tono y el volumen usan un generador **propio del gestor**, sembrado con el
reloj. No sale de ningún flujo de `RngStreams` ni vuelve a la simulación: dos reproducciones del mismo
partido suenan distinto y el resultado es el mismo. RT-021 (determinismo) no alcanza a esto porque no
decide nada del partido — y por la misma razón, **nada de audio puede influir en `/Sim`** (RT-011/RT-014).
