# Barrido de detectores de síntomas — 3 oct 2026

**Encargo del revisor:** «usa el mismo sistema de fotogramas para detectar otros errores que he ido notificando».
**Qué es:** una batería de 15 detectores automáticos, uno por síntoma notificado, pasada sobre la build actual (`d50df7b` +
instrumentos). **Sólo diagnóstico:** no se ha tocado `/Sim` ni `/Game` (salvo el modo `ventana` del instrumento
`-- movimiento`, que sólo graba).

## Método

- **Traza de `/Sim` (RT-098) siempre que basta**: barata, determinista, sin Godot. Los 15 detectores
  (`Sim.Tests/Analysis/Detectors/SymptomDetectors.cs`) leen la traza y los eventos; ninguno necesitó el registro por
  fotograma de Godot para *detectar*. Godot sólo se usa para **fotografiar** los peores casos (hojas y MP4).
- **Tres trazas por detector** (1.000 partidos cada una, semillas 1..1000): `ref` = `TestMatches.Reference` (la semilla
  es el emparejamiento); `run` = primer partido del acto 1 de una run de `human_abattoir` con esa semilla, que es
  exactamente lo que reproduce Godot con `-- movimiento <carpeta> <semilla>`; `armado` = `ref` con `box_predator` en
  todos los jugadores de campo (sólo BB-I; la referencia no lleva perks). 3.000 partidos en ~50 s.
- **Comando único:** `tools/barrido-detectores.sh [partidos=100] [semilla0=1]` → valida los detectores (Regla J) y barre.
  Salida en `Game/screenshots/detectores/` (`resumen.md`, `casos.txt` con todos los casos, `peores.tsv` con semilla y tick
  de los tres peores). Hojas y MP4: `tools/detectores-hoja.py` sobre lo que graba
  `godot ... -- movimiento <dir> <semilla> ventana <etiqueta> <tickIni> <tickFin> <ids|->`.
- **Umbrales (Regla H):** cada uno lleva su procedencia en el comentario del código. Los que vienen de la ficha del
  síntoma están **medidos** (BB-K, BC-G, BO-A, BA-E, BN-A); el resto son **provisionales, sin medir** (BH-A 150 ticks,
  BA-J «más de 1 no delantero sin replegar», BB-C 10 ticks de sonda, BC-G «quieto» < 0,02 casillas/tick).

## Validación (Regla J) — antes de creerse ninguna cifra

1. **22 pruebas sintéticas** (`SymptomDetectorsValidationTests`, 4 s): para cada detector un caso positivo construido con
   respuesta conocida y uno negativo (p. ej. dos compañeros oscilando sobre la misma casilla = baile; los mismos sin
   compañero encima o corriendo recto = nada; entrada que *pita* la falta ≠ robo del saque).
2. **Contraste con el contador del propio motor** (BA-E): los tiros sin ángulo del detector coinciden con
   `MatchReport.LowApertureShots` dentro del ±10 % en 150 partidos.
3. **Partidos históricos con el síntoma conocido** (la build *anterior* a cada arreglo, 400 partidos `ref`,
   `git archive` de `e727ab4^` y `6bba253^` + los mismos detectores). El detector tiene que ver lo que la ficha vio:

| Síntoma | Ficha (build vieja) | Detector en la build vieja | Detector en la build actual |
|---|---|---|---|
| BB-K baile | 17,3 episodios/partido | **18,6 ± 0,6** (99,5 % de partidos) | 0,94 ± 0,05 |
| BO-A atascados | semilla 40, tick 614, 632 ticks | **semilla 40, tick 614, 632 ticks** (exacto) | 0,024; peor 53 (`ref`), 173 (`run`) |
| BN-A amontonamiento | 89 % con ≥ 2 a < 2 casillas | **3,02/partido, 89,8 %** | 0,60/partido, 46 % |
| BC-G balón suelto ≥ 60 ticks | 3 bloqueos de 310-597 en 1.000 | **12/400 (3,0 %)**, hasta 656 ticks | **0/2.000** |
| BB-G2 portero perseguidor | semilla 141, 285 ticks | **6/400 (1,5 %)**, semilla 389 tick 877, 483 ticks | **0/2.000** |
| BH-A congelación | (BB-O: 740 de 1.200 fotogramas) | sale en las mismas semillas 40@618 y 224@623, 389@882 | **0/2.000** |

   Las cifras viejas de la ficha y las del detector no son del mismo conjunto de semillas, pero el orden de magnitud y,
   en BO-A, la semilla y el tick exactos coinciden: el instrumento mide lo que dice medir.
4. **Control sobre traza real**: una reposición de equipos aparece como salto *explicado* (20 por partido); si diera 0 el
   detector estaría ciego.

**Sin validación histórica** (sólo sintética): BB-B, BB-C, BB-A/BB-L, BB-I, BA-J, BF-C. BB-B y BB-C se arreglaron el
16 sep, antes de las dos builds viejas que se pudieron reconstruir. **BF-C no pasa la prueba histórica** (ver su fila).

## Tabla de resultados (1.000 partidos por traza; «casos/partido ± error típico de la media»)

| Síntoma | Detector (qué cuenta como caso) | Validación | Tasa `ref` | Tasa `run` | Peores casos (traza:semilla@tick) | Etiqueta |
|---|---|---|---|---|---|---|
| **BB-K** baile | racha ≥ 4 inversiones de rumbo con compañero a < 1 casilla | sintética + histórica (18,6 → 0,94) | 0,938 ± 0,045 (46 % de partidos) | 0,789 ± 0,043 (40 %) | run:775@1821 (54 inversiones, FindSpace), ref:487@1181 (52) | **CONFIRMED** (residual; de 1.700 episodios 838 `CoverSpace`, 747 `FindSpace`) |
| **BC-G** balón suelto, bloqueo largo | balón libre y quieto ≥ 60 ticks en juego abierto | sintética + histórica (3,0 % → 0) | 0/1.000 | 0/1.000 | — | **REJECTED** bajo la ADR 0177 (cota 95 %: < 0,15 %/partido con 2.000) |
| BC-G balón suelto, episodio corto | ídem ≥ 15 ticks | idem | 0,093 ± 0,010 (8,6 %) | 0,130 ± 0,012 (11,4 %) | ref:232@875 (26), ref:335@1375 (25), run:640@1177 (25) | **CONFIRMED** corto (el máximo es 26 ticks ≈ 1,7 s; ya era «casi todos cortos» en la ficha) |
| **BA-J** sin repliegue | tras `SAVE held`, al soltar el portero quedan > 1 jugadores de campo no delanteros del que tiró en campo contrario | sintética | 1,69 ± 0,04 (84 % de partidos); rezagados por parada 2,17 | 1,73 ± 0,04; 2,10 | ref:22@769 (5 sin replegar), ref:77@1555, ref:84@1670; run:7@1835 | **LIKELY** (baja de 3,5-3,6 a 2,1-2,2 rezagados con la ADR 0178, pero el umbral «> 1» es provisional y no hay un valor correcto de referencia) |
| **BF-C** delantero pega sin balón | (a) evento `Tackle offBall*` de un delantero; (b) acción `Block`/`Tackle` de delantero sin rival con balón | sintética; **la histórica no discrimina** | (a) 0/1.000; (b) 1,92 ± 0,06 (74 %), 11,9 ticks/partido | (a) 0; (b) 1,59 ± 0,06; 10,5 ticks | (b) run:794@405 (31 ticks), run:283@406 (25), ref:8@100 (22) | (a) **REJECTED** (el puesto está cerrado, ADR 0133); (b) **LIKELY**: la *elección* persiste (pre-0179: 14,5 ticks/partido, ahora 11,9), pero el instrumento no separa antes/después con claridad |
| **BB-G2** portero perseguidor | balón libre y quieto fuera del área, el portero es el compañero más cercano y nadie de campo lo persigue ≥ 15 ticks | sintética + histórica (1,5 % → 0) | 0/1.000 | 0/1.000 | — | **REJECTED** bajo la ADR 0177 (< 0,15 %/partido) |
| **BN-A** amontonamiento | portero con balón y ≥ 2 compañeros a < 2 casillas | sintética + histórica (3,02 → 0,60) | 0,596 ± 0,024 (46 %) | 0,517 ± 0,022 (42 %) | ref:205@1669, ref:411@405, ref:466@1163 (5 compañeros a < 2, 20 ticks); run:61@1509 | **CONFIRMED** (residual): todos en juego abierto (ningún saque de puerta), episodios de 20 ticks |
| **BO-A** atascados | mismo portador y mismo rival a < 1 casilla > 45 ticks | sintética + histórica exacta (semilla 40@614, 632) | 0,024 ± 0,005 (2,4 %) | 0,036 ± 0,006 (3,4 %) | **run:743@1127 (173 ticks = 11,5 s)**, ref:59@252, ref:97@368 (53) | **CONFIRMED** (residual raro; antes 9 %) |
| **BB-A / BB-L** salto | salto > 0,6 casillas en un tick sin reposición ni saque que lo explique; y último paso antes de salir del campo | sintética + control real | 0,003 (3/1.000) | 0,002 (2/1.000) | run:57@1377 (1,11), ref:74@955, ref:343@394 — **todos el portero** | BB-A: **CONFIRMED** raro y sólo del portero (0,25 %; causa no aislada, **LIKELY** su salida/estirada). BB-L en la traza: **REJECTED** (0/2.000). El deslizamiento *dibujado* (BA-K) no se midió en Godot: **LIKELY** arreglado |
| **BB-B** robo antes del saque | dueño del balón rival o entrada contra el sacador *con la ventana ya abierta* (centro, y cualquier saque) | sintética | 0/1.000 | 0/1.000 | — | **REJECTED** (cota < 0,15 %/partido); sólo validado en sintético |
| **BB-C** celebración | el goleador salta > 0,6 casillas celebrando, o celebra en su campo a mitad de sonda | sintética | 0/1.000 | 0/1.000 | — | **REJECTED** (< 0,15 %/partido); sólo validado en sintético |
| **BH-A** congelación | ≥ 150 ticks de juego abierto sin evento ni balón fuera de 1 casilla, o dueño del balón fuera del campo | sintética + histórica (3 semillas) | 0/1.000 | 0/1.000 | — | **REJECTED** (< 0,15 %/partido) |
| **BA-E** goles sin ángulo | gol cuyo último tiro salió con apertura < 0,5 | sintética + contador del motor ±10 % | 0,234 ± 0,015 (21 % de partidos; ≈ 12 % de los goles) | 0,178 ± 0,013 (17 %) | ref:32@165 y ref:35@126 (**gol desde (16,0; 1,5), apertura 0,00: sobre la propia línea de gol**), ref:39@130 (desde (0,0; 5,5)); run:35@1231 | **CONFIRMED** (reaparece; 9,4 % de los tiros son < 0,5 frente al 25-29 % de la ficha) |
| **BB-I** «Depredador de área» sin tiro | `PERK_TRIGGERED box_predator` sin `SHOT` del mismo actor en el mismo tick, o con `SHOT_BLOCKED` en ese tick o el siguiente | sintética | armado: 0 de 2.648 activaciones por 1.000 partidos | — | — | **REJECTED** en la traza (por datos ya era imposible); queda **LIKELY** una causa de presentación (el pergamino dura 1 s, `MatchFlashView.DurationFrames`) que la traza no ve |

**Potencia de los REJECTED.** Con 0 casos en 2.000 partidos (`ref`+`run`) la cota superior al 95 % (regla de tres) es
0,15 casos/partido; en BB-I, 0 de ~2.650 activaciones por cada 1.000 partidos armados. Es potencia suficiente para el
efecto que sí se vio en las builds viejas (BC-G 3,0 %, BB-G2 1,5 %, BH-A 0,3-1,5 % por partido). **No lo es** para
algo de frecuencia de 1 por cada 10.000 partidos.

## Qué reaparece

Cinco de las trece fichas siguen produciendo el síntoma en la build actual, **todas en versión reducida** (la medición
histórica da 5-20 veces más antes de los arreglos): BB-K (`FindSpace` ya pesa tanto como `CoverSpace`), BN-A (en juego
abierto, mientras el portero retiene 20 ticks), BO-A (un caso de 11,5 s en `run`), BA-E (goles desde la propia línea de
gol) y BC-G (sólo episodios cortos). BA-J y BF-C son **LIKELY**: el detector ve el comportamiento pero no hay un umbral
de «bien» medido. Hojas de contacto (16 fotogramas recortados alrededor de los jugadores del caso) y MP4 de los peores casos `run`, en
`Game/screenshots/detectores/hojas/`: `BO-A` (semilla 743, tick 1105-1190: el portador azul con el balón a los pies, rodeado
de un grupo rojo durante todo el tramo), `BB-K` (775, 1800-1860: los jugadores 2 y 5 de la plantilla azul apilados sobre la
misma casilla de la banda, el rótulo se lee «52»), `BN-A` (61, 1490-1530: cuatro compañeros pegados al portero en el instante
de la atrapada y dispersándose a los ~10 ticks), `BA-E` (35, 1205-1245: el tiro desde la línea de fondo junto al palo que acaba
en gol), `BB-AL` (57, 1360-1395: el salto del portero), `BC-G` (640, 1160-1205), `BF-C` (794, 385-440) y `BA-J` (7, 1815-1860).

## Lo que no se hizo / límites

- **No se arregló nada** (diagnóstico). Las fichas de lo que reaparece llevan una sección «Barrido de detectores (3 oct)»
  con su semilla y su tick.
- Hojas de contacto sólo para los casos de la traza `run` (únicos reproducibles en Godot); los de `ref` se reproducen
  con `TestMatches.Reference(catalog, semilla)` en `Sim.Tests`.
- Los detectores de BB-A/BB-L y BB-C miran la **traza de `/Sim`**, no el render: el deslizamiento dibujado de BA-K sólo
  se vería con el registro por fotograma de `-- movimiento` (no se barrió).
- La máquina estaba compartida: los barridos se lanzaron con 1 hilo y los Godot por `tools/pesado.sh`.

## Tarde: tras las ADR 0184/0186/0188 y los arreglos de BH-A y BO-A

Barrido de 1.000 partidos por traza (`tools/barrido-detectores.sh 1000`), casos por partido `ref` / `run`:

| Síntoma | Mañana (antes de 0184/0186) | `main` con 0184/0186/0188 | Con los arreglos | Estado |
|---|---|---|---|---|
| BH-A congelación | 0 / 0 | 0 / 0,001 (`run:130@1025`, 280 ticks) | **0 / 0** | arreglado en la causa ([BH-A](../pendientes/BH-A.md), enmienda de la ADR 0186) |
| BO-A atascados > 3 s | 0,024 / 0,036 | 0,047 / 0,048 | **0,005 / 0,002** | arreglado: tope de protección exacto ([BO-A](../pendientes/BO-A.md), enmienda de la ADR 0153) |
| BN-A amontonamiento | 0,596 / 0,517 | 0,765 / 0,645 | 0,780 / 0,646 | **sin arreglar**: CONFIRMED de la ADR 0184; es el grupo que defendía en el área en la parada ([BN-A](../pendientes/BN-A.md)) |
| BN-A que sigue a los 10 ticks (fila nueva) | 0,042 / 0,035 (sonda, sostenida 0 y BV-B apagada) | sin medir | 0,084 / 0,068 | idem |

Los demás detectores, sin cambio distinguible del ruido frente a `main` (BB-K 0,838 / 0,803, BC-G ≥ 15 ticks 0,044 /
0,057, BA-J 1,84 / 1,69, BF-C 2,33 / 1,96, BA-E 0,178 / 0,141; BB-A 2 / 3 casos en 1.000). Salida en el temporal del
agente (no se tocó `Game/screenshots/detectores/`, que sigue siendo el barrido de `main`).

**Partido de referencia** (`/Balance`, 10.000 × 2, media ± e.t.): s1 goles 2,480 ± 0,013, entradas 9,27, lesiones 0,744 ±
0,009; s2 2,090 ± 0,013, 11,20, 0,415 ± 0,007. ADR 0188 (`main`): 2,479 / 9,29 / 0,742 y 2,088 / 11,24 / 0,412. Fuera de
banda sólo `betterTeamWinRate` 60-40, la conocida.

**`Category=Gate` una vez (48 tests, 12 m 12 s): 9 rojas.** Las 7 de `main` (ADR 0188): BossGate curva, BuildGate ×3
(`orc_violence` 53,62; `elf_brawler` 45,52), `elf_none` 39,62, `StatisticalTests` ×2 (90,96, igual que `main`). Además:
- `EveryCatalogPerkIsAssignedInSomeBuild` (`immovable` sin build): comprueba **sólo datos** (`/data/builds`), no juega
  partidos; `immovable` entró en `610c567`, después de la pasada de puertas de la ADR 0188. Roja también en `main`.
- `TheThreeDoctrinesBuyDifferently` (ahorradora 15,20 frente a contextual 15,67): la puerta de una semilla que ya saltó
  con las ADR 0184/0186 y volvió a verde con la 0188; el signo cambia por semilla (ver ADR 0184, «Puertas»). Sin medir
  aparte con más semillas.
- Dentro de `BadBuildsLoseToTheirBaseline`, ya roja, aparece `elf_out_of_zone` 45,18 (techo 45; `main` < 45).
