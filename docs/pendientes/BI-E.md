# BI-E — El balón aéreo no baja: cadenas de hasta 80 cabezazos en el centro del campo

Estado: **CONFIRMED y con causa aislada** (24 sep 2026). Reportada por el revisor jugando la build.

## De dónde sale

El revisor, jugando: *«el mayor problema que veo es el ping-pong cuando disputan un balón elevado. El balón
no cae fácilmente y esto no debería ser así. Una cosa es rematar un balón a puerta y otra que estén medio
partido disputando el balón en el medio campo entre los dos equipos»*.

Es un defecto de la **ADR 0139**, escrita ese mismo día. Aquella ADR ya cazó un ping-pong —1.753 duelos por
veinte partidos— y lo cortó con un enfriamiento por jugador. **El enfriamiento no era la causa completa.**

## Medido (30 partidos, traza completa del balón)

| | |
|---|---|
| fotogramas con el balón **en la banda aérea** (0,5-1,7) | **18,9 %** |
| raso (≤ 0,5) | 77,7 % · por encima del salto (> 1,7) | 3,4 % |
| duelos aéreos | **11,7 por partido** |
| cadenas de 2+ duelos seguidos | 16 · **la más larga de 80** · media 21,0 |
| hueco entre duelos encadenados | **7,5 ticks** |
| **recorrido del balón entre duelo y duelo** | **0,72 casillas** |
| altura en banda | mediana **1,04** · p90 1,47 |
| despejes | 10,2 por partido · centros 2,5 |

## Las hipótesis, con su estado

- **H1 · El cabezazo conserva la altura y encima empuja hacia arriba.** `Ball.Head` preserva `Z` a
  propósito (para no teletransportar el balón al césped) y `headerLiftCellsPerTickMilli` es **positivo**,
  así que un balón cabeceado a una casilla de altura sigue a una casilla de altura. → **CONFIRMED**: la
  mediana de altura en banda es 1,04 y el balón pasa el 18,9 % del partido ahí.
- **H2 · El cabezazo es lento en horizontal.** 180 milésimas por tick, **más lento que un pase** (250). →
  **CONFIRMED**: entre dos duelos encadenados el balón recorre **0,72 casillas**. Con 7,5 ticks de hueco
  debería recorrer 1,35: recorre la mitad porque cada equipo lo cabecea hacia **su** lado y los dos
  impulsos se cancelan.
- **H3 · El enfriamiento de 12 ticks no basta porque los que repiten son OTROS.** → **CONFIRMED**: el hueco
  medio entre duelos encadenados es **7,5 ticks**, menor que el enfriamiento, así que por definición no
  son los mismos dos jugadores. El enfriamiento hace su trabajo; el problema es que hay más gente.
- **H4 · El rebote contra el césped devuelve el balón a la banda.** → **sin evidencia de activación**: con
  restitución del 45 % y las alturas medidas, el bote no llega a 0,5. No se descarta como mecanismo, pero
  no es lo que produce esto.
- **H5 · La banda aérea es demasiado ancha.** → **REJECTED como causa principal**: la mediana en banda es
  1,04, en mitad del rango, no pegada al borde inferior. Estrechar la banda no quitaría el ping-pong.
- **H6 · Hay demasiados balones aéreos de entrada** (10,2 despejes por partido). → **LIKELY, y es
  amplificador, no causa**: sin H1 y H2 cada despeje se resolvería en unos ticks. Es cuestión de balance y
  le toca a la fase siguiente.

## La causa, en una frase

**Dos equipos se cabecean el balón en direcciones opuestas, despacio y sin que pierda altura, así que el
balón ni avanza ni baja: se queda flotando en el sitio hasta que alguien falla.**

Los tres términos son necesarios. Quitando cualquiera de los dos primeros, la cadena se rompe sola.

## Lo que NO es

- **No es el enfriamiento** (H3): funciona, y subirlo sólo haría que la cadena la siguieran jugadores cada
  vez más lejanos.
- **No es que el duelo aéreo exista**: el revisor lo dice explícitamente — *«una cosa es rematar un balón a
  puerta»*. Lo que sobra es el ping-pong en el medio campo, no el juego aéreo.

## Arreglo y remedición (24 sep 2026)

**Un cabezazo empuja el balón hacia ABAJO**, y sale más rápido en horizontal (180 → 320 milésimas, era más
lento que un pase). No es un ajuste de intensidad: es que la mecánica haga lo que su nombre dice. El dato
deja de llamarse `headerLift` y pasa a ser `headerDrop`, porque un término llamado «elevación» que hace
caer el balón es una mentira en `/data`.

**El duelo aéreo sigue existiendo y sigue decidiéndose con el cuerpo.** Lo que cambia es que se resuelve
**una vez** y el balón vuelve al suelo, que es donde se juega.

| | antes | después |
|---|---|---|
| cadena más larga de duelos | **80** | **2** |
| cadenas de 2+ | 16 (media 21,0) | 1 (media 2,0) |
| duelos por partido | 11,7 | **0,4** |
| fotogramas en banda aérea | 18,9 % | 13,5 % |
| recorrido entre duelos | 0,72 casillas | 1,41 |
| fotogramas por encima del salto | 3,4 % | **0,0 %** |

**Los 11,7 duelos por partido eran ping-pong, no juego aéreo.** Descontando las cadenas, las disputas
«primeras» eran ya ~0,5 por partido antes del arreglo: la tasa real no ha bajado, lo que ha desaparecido es
la repetición.

## Lo que esta medición destapa, y queda abierto

**La tercera altura está muerta.** El 3,4 % de fotogramas por encima del alcance del salto era **el propio
ping-pong bombeando el balón hacia arriba**; ahora es 0,0 %. Con `aerialReachHeightCells` en 1,7 y el pico
del centro en 1,4, **ningún balón legítimo pasa por encima de todo el mundo**, así que el caso «no lo toca
nadie» de la ADR 0139 no se da nunca en un partido real. O sobra la altura, o el centro tiene que volar más
alto. Es decisión de balance y va a la fase siguiente.
