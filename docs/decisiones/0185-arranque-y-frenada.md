# 0185 — Arranque y frenada en `/Sim` (BV-A H4)

Fecha: 3 oct 2026 · Estado: **aceptada y ENCENDIDA** (`tuning.movement.accelTicks` = **2**, elegido con un criterio escrito
antes de medir), **acotada al juego abierto** (enmienda del mismo día, decisión del coordinador) ·
Requisitos: RT-020, RT-023, RT-089, RF-050 · Ficha: [BV-A](../pendientes/BV-A.md) · Va después de la [ADR 0184](0184-una-colocacion-se-sostiene.md)

## Problema

BV-A H4 (CONFIRMED en `/Sim`): `MatchEngine.Move` daba a cada jugador el paso **constante** de `SpeedPerTick` desde el
primer tick y lo cortaba en seco al llegar. La velocidad por tick de la traza es bimodal (casi todo a 0 o a 2,0-2,5 c/s)
y hay cientos de *paso-0-paso* por partido (carrera, un tick quieto, carrera).

## Decisión (implementada)

Un dato, `tuning.movement.accelTicks` (0 = sin rampa: el motor de antes más el arreglo sin dato de `BallDead` de abajo;
lo fija `MobNarrowingTests.WithTheNewRulesOffEveryTraceIsTheOneBeforeAdr0184PlusBoA`). Con él, `Move` (`RampedStep`) calcula el
paso en milésimas de casilla por tick, enteras (RT-023; los únicos `float` son las posiciones de las que se leen
distancias):

1. **De dónde parte:** de lo que el jugador corrió el tick anterior, `MatchPlayer.Velocity` (su desplazamiento propio,
   sin la separación de cuerpos; cero si estaba parado, derribado, sacando o sosteniendo). No hace falta un campo nuevo.
2. **Giro:** conserva `(100 + cos θ·100) / 2` por ciento: recto, toda; a 90°, la mitad; media vuelta, nada.
   **Desvío de la propuesta:** la ADR proponía `max(0, cos θ)`, que deja a cero cualquier giro de 90°; un futbolista que
   abre a un lado no se para.
3. **Arranque:** `v = min(techo, v + techo / accelTicks)`. El techo sigue siendo `SpeedPerTick` (atributo `Speed`
   cansado, bono de turba): el rápido llega en los mismos ticks, a más punta.
4. **Frenada:** `v ≤ isqrt(2·a·d + a²/4) − a/2`, la versión discreta de v² = 2·a·d (la continua sólo recortaba un paso
   al llegar), con un mínimo de un tick de aceleración para no quedarse a centésimas.
5. **El pase y las estimaciones de llegada siguen usando la velocidad punta** (`SpeedPerTickMilli`: `Utility.PassTarget`,
   `TicksToReach`): predicen dónde estará un jugador lanzado. Con el arranque, el receptor que sale de parado llega algo
   más tarde de lo que el pasador estima; no se ha medido aparte.
6. `WalkTo` y el andar del sacador en las reanudaciones no cambian (paso constante, ADR 0147).

**Hermano encontrado midiendo** (sólo se activa con el arranque encendido): en la turba de la semilla 3 el sacador del
saque de centro empezaba en una esquina, a 8,6 casillas, y al resolverse el saque —en cuanto los demás estaban colocados—
se teletransportaba 3,2 casillas (`MobNarrowingTests`). La espera del saque de centro exige ahora también que el sacador
esté a menos de `TakerInPlaceCells` (0,25) de su punto, con el mismo tope de espera (RF-053: nadie se teletransporta).

Valores conocidos (`AccelerationTests`, con el dato a 3 y control a 0): desde parado 48 → 96 → 144 hasta el techo de
146 en **3 ticks**; lanzado hacia un destino a 3 casillas, frena en **3 pasos** (125, 77, 24) sin rebasar; media
vuelta, se queda en la aceleración de un tick; a 90°, la mitad.

7. **Sólo en juego abierto** (enmienda, `MatchEngine.InOpenPlay`): la rampa no actúa con una reanudación pendiente
   (cuenta atrás de saque, falta, penalti, saque de centro), durante la celebración de un gol ni con un área cerrada
   (saque de puerta, portero con el balón en su área, ADR 0152). Ahí el movimiento es coreografía y va a paso constante,
   como antes. **La turba es juego abierto**: allí la rampa sigue.

## Enmienda: acotado al juego abierto (3 oct 2026)

Con la rampa en todo el movimiento, el bucle daba 10 rojas. El coordinador decidió acotarla a la carrera, con la condición
de que el acotado **no tapara reglas rotas**: cada roja que parecía una regla se investigó con `gameplay-debug` en un
árbol aparte con la rampa en todo (commit `02a9f15`), para saber si la causa era «llega tarde a la reanudación».

| roja | causa | estado | qué se hizo |
|---|---|---|---|
| (a) `EnLaTurbaNoSePita`, semilla 17 | la falta se pitó en el **último tick reglamentario**, antes de que se emitiera MOB_START en el mismo tick (índices 227-228 frente a 230). El árbitro aún estaba: no es regla rota, es el instrumento comparando ticks. Hermano (revisión independiente): esa falta deja un saque de falta pendiente al empezar la turba | **CONFIRMED** (traza) | el test cuenta desde el suceso MOB_START (Regla J) y comprueba en todas las turbas de 300 partidos que no queda reanudación de falta ni penalti (dos empiezan en el tick de una falta pitada); la turba descarta el saque de falta pendiente (`_freeKickFor`) |
| (b1) `FoulByFoul…`: sacador de falta derribado, semilla 81 | **no es «llegar tarde»**: una reanudación pedida a mitad del bucle de jugadores dejaba `BallDead` falso el resto del tick (se leía al principio), y quien decidía después empezaba una carga; se resolvió en la cuenta atrás (bloqueo decidido en el tick 1284 de la falta, resuelto en el 1287 sobre el sacador). Pasa **sin arranque**: 9 contactos en la cuenta atrás en 300 partidos con 0 y con 3 | **CONFIRMED** (traza, y 9 → 0 con el arreglo) | `BeginRestart` marca `BallDead` (RF-057, ADR 0132). Test `RestartContactTests` (valor conocido: semillas 18, 31, 68 antes). Es la única diferencia de motor con `accelTicks` 0 (sin él vuelve la huella vieja `2380212350706324353`) |
| (b2) control de `TheVictimOfAWhistledFoulGoesDown` | con la regla apagada caían víctimas por otro mecanismo: un **bloqueo** que gana y es falta derriba por sí mismo (29 de 34). Más bloqueos con el arranque (+7 %) | **CONFIRMED** (desglose) | el control cuenta sólo faltas de entrada |
| (c) `LethalRiskTests`, valor nulo | la run de la semilla llegaba al partido letal **sin suplentes**; `Assert.NotNull(bench)` | **CONFIRMED** | la búsqueda exige banquillo, como ya exigía la palanca de colocación |
| (d) `RepeatTackleReachTests.TheRealCaseNoLongerFreezes` | Arrollador no se activa con arranque en ninguna de 400 semillas de run (0 frente a 3 sin él). **No es una regresión del perk** (la primera versión de esta tabla lo llamaba «ruido de plantilla» sin medirlo): con un portador en cada partido (los seis de campo del local, 300 partidos de referencia) dispara en 184 partidos sin rampa y 215 con ella, y la repetición se resuelve 45 y 75 veces. Es que en las plantillas de run casi nadie lo lleva | **CONFIRMED** (medido por etapas) | el caso fijado se juega con `accelTicks` 0; el censo `WithRealSteamrollerRepeatsNoOwnerIsLeftOutOfACarrierState` cubre repeticiones reales con los datos vigentes |
| (e) baile de cobertura de 27 fotogramas en el borde del área cerrada (penalti) | la rampa dentro del área cerrada: dos cuerpos frenando sobre el mismo punto del borde | **CONFIRMED** con el acotado: racha más larga 6 (tope 15) **sin** el parche de cobertura del commit `02a9f15`, que se retira (era un arreglo condicionado al arranque). La regla del área cerrada sigue siendo la de la ADR 0152 (`Move` saca andando) | — |
| (f) controles de `PositioningHoldTests`, `DancingTeammatesTests` y el de alcance de `TackleReachTests` | el arranque es **otro remedio del mismo síntoma**: con él encendido, el control (sin el arreglo) dejaba de ver el síntoma (27,6 % de deshechas; 274 episodios; 14,3 % de entradas lejanas) | instrumento | los controles corren con `accelTicks` 0, el motor en que se calibraron; las aserciones sobre los datos siguen con 3 |
| (g) huellas de `MobNarrowingTests` | trayectorias nuevas | — | renovadas, con los valores anteriores en el comentario |
| (h) duelos aéreos (`EmergentChainTests`), `ShotHeightTests`, `ForwardOffBallTests`, `KeeperAreaTests`, `PenaltyAreaSymmetryTests` | — | verdes con el acotado | — |

| hermano: teletransporte del sacador del saque de centro | el sacador anda a paso constante (`WalkRestartTaker`), así que no depende de la rampa | — | `TakerInPlaceCells` vale con cualquier `accelTicks` (con 0 sólo cambia la huella de los partidos con turba; condicionado al arranque, vuelve la vieja) |
| hermano del barrido con 2: congelación de 411 ticks en `run:385@915` | Caño (`nutmeg`) derriba al defensor al intentar el regate; si el regate salía perdido, el balón pasaba a un derribado que se levantaba en `Positioning` con él. No depende del arranque (la semilla lo destapó) | **CONFIRMED** (traza; 0 congelaciones tras el arreglo) | regla de BM-B: el balón queda suelto a sus pies. Test `TheNutmegOnALostDribbleNoLongerFreezes` |
| hermano: `BallDead` a mitad de tick también cambia `ChaseBall` y `HasLooseBallDuty` para quien decide después | consecuencia buscada (balón muerto para todo lo que lo lee) | lote 5.000 × 2 con 0 antes/después: goles, tiros, entradas, faltas, lesiones iguales dentro de 1 e.t. | comentario AW-R al día |

Tests nuevos de valor conocido (`OpenPlayRampTests`): la rampa actúa en juego abierto, se apaga con una reanudación
pendiente y sigue en la turba; la barrera sólo quita la velocidad hacia el balón (con 0, toda); el saque de centro espera
al sacador a ≤ 0,25 con y sin rampa; y la rampa deja las inversiones por debajo de un tercio (aserción, 12 partidos).

El arreglo de la barrera (sólo quita la velocidad hacia el balón) y la espera del sacador del saque de centro siguen, con
la rampa encendida: actúan también en la ventana de barrera posterior al saque, que ya es juego abierto.

### `game-design-review` del acotado (corta)

1 el jugador ve arrancar y frenar en la jugada; en las colocaciones, la gente anda a paso constante como antes; 2-3 no
hay decisión nueva; 4 RT-020 (física de movimiento), RF-053 (nadie se teletransporta, sin cambios); 5 `/Sim`
(`Move`, `InOpenPlay`), un dato; 6 alternativas: rampa en todo arreglando cada choque con la barrera y el área (lo que
se intentó: frágil, cada reanudación es un caso) o una aceleración mayor en las colocaciones (otra cifra sin
procedencia); 7 trade-off: queda *paso-0-paso* en las colocaciones (con 2: 4.958 de 7.595 en 40 partidos) y el paso de
colocación a jugada arranca desde la velocidad de andar; 8 no cambia estrategias; 9 degeneración medida abajo: más
contacto (con 2: entradas +5 %, lesiones −1 % / +5 %); 10 tests de arriba, sonda, lote, barrido y puertas.

## Procedencia del valor (Regla H): criterio escrito ANTES del barrido 2/3/4

Escrito y commiteado antes de medir 2 y 4 (revisión independiente: el 3 no tenía procedencia). Se elige **el menor
`accelTicks` cuya sonda limpia (40 partidos, `ReversalsWithAndWithoutTheRamp`) deje las inversiones a velocidad de
carrera en ≤ 0,02 por jugador y segundo**; a igualdad, el de menor subida de lesiones por partido (lote 10.000, s1). El
0,02 es **provisional, sin medir** su percepción: es un orden de magnitud por debajo del motor sin rampa (0,092), que es
lo que pedía BV-A («que no vayan y vengan»), no una cifra observada en pantalla.

**Resultado** (sonda de 40 partidos, `ReversalsWithAndWithoutTheRamp`; se añadió el 1 para que «el menor» tuviera
suelo): 0 → 0,0934 · 1 → 0,0954 · **2 → 0,0198** · 3 → 0,0101 · 4 → 0,0086. El criterio elige **2**, y se cambió el
dato de 3 a 2. **El 2 pasa por un 1 %**, dentro de su propio error (~1.000 inversiones, ±3 %): la elección es frágil y
el 3 queda como alternativa medida (abajo, en la historia). Con el 2 la subida de lesiones es menor (s1 −1 %, s2 +5 %
frente a +9 % y +13 % con 3), que era el desempate del criterio.

## Medición final (`accelTicks` 2 frente a 0, mismo árbol)

**Sonda limpia de la ADR 0184** (40 partidos): inversiones a velocidad de carrera **0,0934 → 0,0198 por jugador y
segundo**; *paso-0-paso* 19.722 → 7.595 (en reanudación 5.346 → 4.958).

Lote 10.000 × 2 semillas (media ± error típico por partido):

| | s1 con 0 | s1 con 2 | s2 con 0 | s2 con 2 |
|---|---|---|---|---|
| goles | 2,475 ± 0,013 | 2,452 ± 0,013 | 2,088 ± 0,013 | 2,162 ± 0,013 |
| tiros | 9,27 ± 0,04 | 9,19 ± 0,03 | 8,50 ± 0,03 | 8,60 ± 0,03 |
| entradas | 9,25 ± 0,04 | 9,69 ± 0,04 | 11,25 ± 0,05 | 11,82 ± 0,05 |
| faltas | 6,91 ± 0,05 | 7,00 ± 0,05 | 4,59 ± 0,02 | 4,84 ± 0,02 |
| bloqueos | 6,19 ± 0,07 | 6,44 ± 0,07 | 3,47 ± 0,02 | 3,65 ± 0,03 |
| lesiones | 0,739 ± 0,009 | **0,734 ± 0,009** | 0,408 ± 0,006 | **0,430 ± 0,007** |

Todo dentro de banda salvo `betterTeamWinRate` 60-40, que ya lo estaba con 0.

Runs completas (`--full-runs 240`, semillas 1-4, contextual, 960 runs por brazo; condición 5 de la ADR 0048): muertes
por partido **0,144 ± 0,007 → 0,157 ± 0,007** (diferencia +0,014 ± 0,010, dentro del ruido; banda 0,11-0,22); lesiones
propias por run 3,32 → 3,43 ± 0,16; `runWinRate` 13,5 → 15,1 %.

**`elf_none`** (`RaceBalanceTests`, semillas 1-3): **40,85 · 42,55 · 40,80**, dentro de la banda D-29 (con 0 y las ADR
0184/0186: 39,08 · 39,90 · 39,90, [BV-C](../pendientes/BV-C.md); con 3: 41,27 · 44,10 · 42,30). El margen es estrecho. El
enano, 57,33 · 55,88 · 57,95 (techo 60).

**Puertas completas** (una pasada, con todos los arreglos): 7 rojas de 48 (contando los agregados), **ninguna nueva**: curva
de jefes, `orc_violence` 52,16, `TheThreeDoctrinesBuyDifferently` (−0,64) y `betterTeamWinRate` 60-40 90,36 (≈ 91 en
`main`, BV-C) —las de `main`— y `elf_brawler` 45,26, que ya estaba roja con 0 en este árbol (45,65). Pasan a verde
`elf_none` y `elf_out_of_zone`.

**Barrido de detectores** (`tools/barrido-detectores.sh 1000`, ref / run, frente a `main`): baile BB-K **0,838 → 0,435** /
0,803 → 0,404; amontonamiento sobre el portero 0,780 → 0,676 / 0,646 → 0,618; sin repliegue 1,84 → 1,68 / igual. Sube
BF-C «delantero elige pegar sin portador» en run 1,96 → 2,17 (±0,08; ref igual): **es contacto real** —con 3 se desglosó:
fuera del suelo +11 %, el mismo aumento de entradas y bloqueos del equipo—, y no hay más delanteros que peguen sin balón
(sigue en 0, ADR 0133). Balón suelto quieto (run) 0,057 → 0,073 (±0,012): dentro del ruido, a vigilar (con la rampa se
llega más tarde a un balón suelto). La congelación que destapó el primer barrido con 2 (`run:385`, Caño) está arreglada
y vuelve a 0.

## `game-design-review` de las lesiones (revisión independiente: un modificador invisible que toca el recurso central)

1. **Qué experimenta el jugador:** ve correr como personas; no ve que la carrera haga que una entrada alcance más. Lo
   que nota es el número: con 2, lesiones por partido s1 −1 %, s2 +5 %; propias por run +3 % (3,32 → 3,43 ± 0,16);
   muertes por partido +0,014 ± 0,010. Con 3 era +9-13 % por partido.
2. **Qué decide:** nada nuevo. Es física de movimiento, no un modificador elegible, y la previsibilidad de la ADR 0048
   (se sabe antes, se evita, se reduce con la alineación) no cambia: no hay daño nuevo, hay algo más de contacto.
3. **Qué debería decidir:** lo mismo que ya decide (alineación, a quién exponer); la carrera premia anticiparse y eso
   pertenece al motor, no a una elección del jugador.
4. **Regla:** RT-020 (física), RF-057 (contacto sólo en jugada), RF-093 y ADR 0048 (muerte rara), ADR 0188 (cuánto
   lesiona cada contacto).
5. **Sistemas:** `/Sim` (`Move`, `RampedStep`, `InOpenPlay`), `/data` (`tuning.movement.accelTicks`). `/Game` sólo lee
   posiciones.
6. **Alternativas:** (a) 3 con `onTackleBase` reajustado a la baja; (b) 2 sin tocar las lesiones; (c) apagado. Elegida
   (b) por el criterio escrito.
7. **Trade-off:** más contacto (entradas +5 %) a cambio de que no vayan y vengan; con 2 queda más *paso-0-paso* que con
   3 (7.595 frente a 5.178 en 40 partidos).
8. **Estrategias:** premia la anticipación (quien ya corre llega antes); no crea combinaciones nuevas con perks
   (Arrollador dispara algo más porque hay más entradas ganadas: 184 → 215 en 300 partidos con portador).
9. **Degeneración:** se mira contra el objetivo de la ADR 0188 (las cifras de antes de la 0184: 0,692 / 0,395 por
   partido y 3,70 propias por run), con su mismo criterio (la peor desviación relativa de las tres): **con 0, +6,8 % /
   +3,3 % / −10,3 %, peor 10,3 %; con 2, +6,1 % / +8,9 % / −7,3 %, peor 8,9 %**; con 3 era peor de 16 %. **La ADR 0188
   lo compensa con el 2 y no hace falta tocar `onTackleBase`**; con 3 sí habría hecho falta. LIKELY: la run tiene ±4 %
   de error típico, del tamaño de las diferencias.
10. **Cómo se demuestra:** `OpenPlayRampTests` (valores conocidos), la sonda, el lote, las runs completas, las puertas y
    el barrido de abajo.

## Siguiente paso

Que el revisor lo juegue. Abierto: el 2 se eligió por un margen del 1 % (el 3 está medido); el *paso-0-paso* que queda
en las colocaciones y en la carrera, si se nota; el detector BF-C cuenta también fotogramas de derribados.

## Historia: el acotado con `accelTicks` 3 (antes del barrido)

Inversiones 0,092 → 0,010; entradas +12 % / +5 %, lesiones 0,738 → 0,805 / 0,407 → 0,458 (+9 % / +13 %); muertes por
partido 0,132 → 0,138 ± 0,010 (480 runs); `elf_none` 41,27 · 44,10 · 42,30; puertas: 5 rojas, todas ya rojas con 0 o en
`main`. Barrido: BF-C «delantero elige pegar sin portador» subía 2,33 → 2,77; desglosado, el grueso eran fotogramas de un
delantero en el suelo con la última decisión aún anotada, y fuera del suelo +11 % (1.396 → 1.551 en 300 partidos), que
**es contacto real**, el mismo aumento de entradas y bloqueos del equipo, no un artefacto.

## Historia: la primera medición, con la rampa en todo el movimiento

Inversiones 0,091 → 0,004, *paso-0-paso* ~19.900 → 269; entradas +10 %, lesiones +2-6 %, muertes por partido 0,151 →
0,142 ± 0,008 (`--full-runs 240`, semillas 1-3); 9-10 rojas en el bucle, resueltas o acotadas arriba.

## Nota de `game-design-review` (diez preguntas, corta)

1 el jugador ve muñecos que arrancan y frenan en seco y van y vienen; 2 que aceleren y frenen como personas; 3 no es
regla nueva: es física de movimiento (RT-020); 4 `/Sim` (`Move`) y un dato; `/Game` sólo lee posiciones (ya suaviza con
Hermite, RT-014); 5 alternativa: sólo suavizar en `/Game` (hecho en BV-A; no cambia quién llega antes); 6 el mismo
paso constante está en `WalkTo` (no se toca); 7 trade-off: quien sale de parado pierde un paso, quien gira pierde
rapidez; 8 premia anticiparse y castiga el ir y venir; 9 degeneración medida: más entradas (+10 %) y las rojas de arriba;
10 valores conocidos con control, sonda, lote, runs completas; faltan barrido y puertas.
