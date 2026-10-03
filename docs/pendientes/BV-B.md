# BV-B — Entradas, faltas y caídas: lo que se ve y lo que viene de `/Sim`

**Estado:** **Parte `/Game` implementada (3 oct 2026, tercera pasada de BV-A)**, medida antes/después con 3 semillas (tabla
abajo). **Parte `/Sim` arreglada (3 oct 2026, [ADR 0186](../decisiones/0186-la-entrada-llega-y-la-falta-tira-a-la-victima.md))**:
quien entra sigue a quien la recibe durante `Tackling` y en la falta pitada cae la víctima (el infractor sólo si fue
dura); sección «`/Sim`: arreglo» al final. Quedan anotados: BN-A sube con la ADR 0184 (separado por dato, LIKELY) y `elf_none` sale de la banda D-29 por seguir a la víctima; escapar del alcance de la decisión no lo arregla (ADR 0186, enmienda). Hermanas: [BV-A](./BV-A.md) (movimiento de los modelos), BI-H
(contacto con el balón), ADR 0173 (pausa breve en lo que para el juego).

## Observación (revisor, vía coordinador)

> «otro punto flaco son las entradas, faltas y caídas».

## Cómo es hoy en `/Sim` (leído en `MatchEngine.ResolveTackle`, `WhistleOrLetPlay`; no supuesto)

- La entrada se **decide** a ≤ `tackleDistanceMaxCells` = 1,0 casillas (`data/ai/weights.json`), dura `TacklingTicks` = 3
  y se **resuelve** si el rival sigue a ≤ 1,0 + `TackleReachMargin` 0,3 = **1,3 casillas**. El suceso `TACKLE` cae en el tick
  de la resolución.
- Quién cae, en el mismo tick del suceso (desfase medido 0 en las 3 semillas): **ganada** → cae quien la recibe
  (`KnockedDownTicks` 18 = 1,2 s); **fallada** → cae quien entra, la mitad (9 ticks = 0,6 s); **falta pitada** → cae **quien la
  comete** (18 ticks) y la víctima **sigue de pie**; falta no vista y bloqueos, igual que la falta.

## Medición (instrumento de BV-A: `-- movimiento`, tramos `caidaNN` alrededor de cada entrada o falta con jugadores con
modelo; `tools/entradas-analisis.py`)

Validación (Regla J): la cadera dibujada de pie corriendo da 0,49 casillas en las 3 semillas; tumbada, 0,08–0,15. El
instrumento leía al principio el **reloj del muñeco** como «segundo del clip» y daba la plancha perfectamente alineada
mientras la cadera dibujada bajaba 0,8 s tarde: la búsqueda del gesto se perdía tras la transición (arreglado en
`8d35b19`). Desde entonces la medida que manda es la cadera.

### Lo que viene de `/Sim` (anotado, no tocado)

- **Entradas que golpean al aire**: en el tick de la resolución, la distancia entre los dos es p50 0,81–0,84 casillas y
  p90 1,0–1,15; **11 de 28** entradas se resuelven a más de 0,9 casillas (dos radios de humano y una pierna). El patrón
  es siempre el mismo: se decide a ~0,7 y durante los 3 ticks de `Tackling` el rival se aleja (la distancia crece). Casos:
  - semilla 20260905: ticks 150 (1,01), 1398 (falta, 0,91), 1428 (ganada, 0,97), 1439 (falta sin balón, 1,19);
  - semilla 20260906: ticks 453 (1,15), 616 (0,96), 664 (1,02), 916 (falta sin balón, **1,34**);
  - semilla 20260907: ticks 116 (1,16), 691 (0,91), 799 (ganada, 1,08).
  Lo que lo arreglaría es de `/Sim`: que quien entra cierre la distancia durante `Tackling`, o que el alcance de la
  resolución no supere al de la decisión. Cambia quién gana entradas: ADR y `balance-measure`.
- **Falta pitada: cae quien la comete y la víctima sigue de pie.** Es una regla del motor (`WhistleOrLetPlay`), no un
  error de la vista; visualmente se lee como «el que entra se tira». La vista añade un trastabilleo a quien la recibe
  (abajo), pero si la víctima debe caer es una decisión de diseño (`game-design-review`).

### Antes / después (`/Game`), semillas 20260905 · 20260906 · 20260907 (tramos `caidaNN`, jugadores con modelo)

| Medida (cadera dibujada) | Antes (`b47af2d`) | Después (`82fbd58`) | Etiqueta |
|---|---|---|---|
| Quien entra lleva el clip de entrada en el tick del contacto | 0 de 3 · 0 de 4 · 0 de 1 | **2 de 3 · 4 de 4 · 1 de 1**, en el segundo 1,00 del clip (el contacto medido) | Arreglada; la que falta es una entrada sin aviso a tiempo en la traza |
| Del golpe a tocar el suelo, p50 | 0,57 s ×3 | **0,03 · 0,03 · 0,30 s** (la plancha ya va al suelo en el contacto; el derribado, 0,3 s) | Arreglada |
| Tiempo en el suelo en pantalla, p50 | 0,67 · 0,07 · 0,60 s | 0,83 · 0,60 · 0,40 s | Mejorada: deja de haber caídas de 1-2 fotogramas (p10 0,03 → 0,27-0,40 s) |
| Del suelo a de pie (cadera 0,22 → 0,45), por caída | 0,10-0,17 s todas (un fundido: **reaparece de pie**) | levantada **0,53-0,57 s**; plancha 0,17-0,30 s (su propia levantada, acelerada al tiempo de `/Sim`); queda un caso de 0,10 s (`idle`) en dos de las semillas | Arreglada salvo un caso por semilla |
| Caídas que se ven | 4/4 · 9/9 · 3/3 | 3/4 · 7/9 · 3/3 | Las que no bajan de 0,22 son planchas cortas (9 ticks) aceleradas ×2; LIKELY, no aislado |

**Qué se hizo** (`PlayerModel.ChooseFallGesture`, `MatchPitchView3D.FallFor`, commits `1c6603c`, `8d35b19`, `db16253`, `82fbd58`):
la vista lee de la traza y sus eventos el `TACKLE` propio que viene y si quien entra acaba en el suelo; si cae, **plancha**
(`tackle`) colocada para que su segundo 1,0 (medido: pie bajo y por delante, cadera lanzándose) caiga en el tick del contacto,
que ya trae su deslizamiento y su levantada, acelerada para acabar cuando `/Sim` lo levanta; si sigue de pie (ganada o perdida
sin caer), **toque de pie** con el golpeo alineado. Quien recibe y cae: **gira hacia la dirección del golpe** (×4 el giro
durante 0,15 s), `trip` desde su 0,25 s (toca el suelo 0,4 s después), el dibujo del cuerpo se desplaza 0,3 casillas en la
dirección del golpe (peso, provisional) y **se levanta** con `standup` (0,35-1,6 s medidos) a tiempo de estar de pie cuando
`/Sim` lo da por levantado; sin aviso a tiempo, se levanta deprisa en vez de reaparecer. Quien recibe una falta y no cae
**trastabilla** (`trip` 0,1-0,4 s). Las cifras de clip salen del perfil medido (`pies.csv`: punteras, cadera y cabeza por clip);
el resto está marcado provisional en el código.

**Defecto encontrado por el camino (Regla J)**: los gestos no se veían donde el reloj del muñeco decía. La búsqueda iba por
encima de la transición y no llegaba al clip, y con el ritmo de los gestos a 0 el fundido de la transición no avanzaba. El
instrumento de la segunda pasada leía ese reloj, así que la alineación del golpeo de la segunda pasada estaba medida sobre
el reloj, no sobre el clip (desfase pequeño allí, 0,07 s, porque el golpeo se lanzaba casi desde su principio). Ahora cada
gesto lleva su búsqueda junto al clip y se coloca cada fotograma; la medida que manda es la cadera dibujada.

**Acento en faltas fuertes y lesiones: no se añade (Regla G).** Ya existen: la falta pitada, la tarjeta y la lesión que para
el juego congelan 0,6 s con su sello (ADR 0173), y la lesión y la roja sacuden la cámara a ×1 (`BroadcastScreen.ShowInjuryBanner`,
`ShowRedBanner`). Uno más los duplicaría.

**Clips (punto 3)**: la UAL2 de Quaternius del repo trae `Hit_Knockback`, `LayToIdle` y `Slide_*`, pero con otro esqueleto
(`pelvis`, `thigh_l`…); usarlos exige retargetear o renombrar huesos de los que dependen el balón y el contacto. **No usados**.
El pack de Mixamo ya tenía `trip`, `fallen`, `standing up` y la plancha: el defecto era cómo se usaban, no qué clips había.

Hojas: `Game/screenshots/movimiento/hoja-caida-v3-antes.png` / `-despues.png` (el mismo bloqueo con falta, semilla 20260905,
fotogramas 36-81 cada 3: antes `trip` y de pie de golpe; después plancha, deslizamiento y levantada).


## `/Sim`: arreglo (3 oct 2026, ADR 0186)

| | Hipótesis | Etiqueta | Medida |
|---|---|---|---|
| (i)a | Quien entra se queda quieto los `TacklingTicks` (rama por defecto de `ExecuteAction`) y la víctima se aleja | **CONFIRMED** | `TackleReachTests` (100 partidos de referencia): con la regla apagada, 324 de 1.280 entradas a más de 0,9 casillas al empezar el tick de la resolución (p50 0,74); siguiéndole hasta 0,6, 14 de 1.269 (p50 0,64) |
| (i)b | El margen de 0,3 sobre el alcance de la decisión permite resolver hasta 1,3 | Contribuye, **no se toca** | Con la víctima seguida casi ninguna entrada lo usa: 14 de 1.269 por encima de 0,9 al empezar el tick |
| (i)c | Seguirle hasta su posición | **REJECTED** (medido) | Lo metía en su cuerpo: la separación empujaba al tirador en el mismo tick y `SymptomDetectorsValidationTests` (tiros sin ángulo, traza contra motor) dejó de cuadrar (108 frente a 81-104). Con 0,6 cuadra |
| (ii) | La regla de `ResolveFoul` derriba siempre al infractor y nunca a la víctima | **CONFIRMED** (leído y medido) | Regla apagada: víctima en el suelo en 26 de 317 faltas pitadas (otras causas), infractor en las 317. Con la regla: víctima en las 315 de 315 e infractor en 294 (las faltas las hacen casi siempre jugadores con rasgo de entrada dura) |

`/Game` no se toca: ya presenta la caída por estado (`FallFor`). Efecto en el partido, en la ADR 0186: entradas +3 %,
faltas +4-5 %, lesiones hasta +5 %, goles −3 %; muertes por partido 0,149 → 0,159 ± 0,008 (en banda).

**Abierto:** BN-A (amontonamiento sobre el portero) 0,60 → 0,72 por partido en `ref` y 0,42 → 0,56 en `run` de `main` al
final (≈ 1-1,4 errores típicos en cada traza), sin aislar entre 0184 y 0186.

### `visual-review` de la falta pitada (3 oct 2026, código final de la ADR 0186)

Instrumento de BV-A (`-- movimiento`, semilla 20260905, ventanas `ventana`), cadera dibujada (`hipsY`; de pie ~0,48,
tumbado 0,08-0,15) en `fotogramas.csv`. **Falta del tick 54** (infractor rival 2000001, víctima #6 con modelo): #6 pasa
a `KnockedDown` con `trip` y la cadera baja 0,48 → 0,29 en 0,2 s (la víctima cae). **Falta dura del tick 1328**
(infractor #5 con modelo, víctima rival): #5 entra con la plancha (`tackle`) y queda en el suelo (cadera 0,09-0,16) los
18 ticks del derribo. Imágenes `faltablanda_0037.jpg` y `faltadura_0036.jpg` del instrumento (no se suben: a pantalla
completa los muñecos son pequeños y la cifra que manda es la cadera). **Límite:** en esta semilla la maqueta sólo pone
modelo al equipo humano y las cuatro faltas pitadas son duras, así que no hay captura de un infractor con modelo que se
quede de pie; eso lo cubre el test `FoulByFoulTheHardOnesTopplesTheOffenderAndTheBallIsDropped` (61 faltas no duras,
ningún infractor en el suelo).
