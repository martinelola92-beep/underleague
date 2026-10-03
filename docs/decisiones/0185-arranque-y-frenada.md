# 0185 — Arranque y frenada en `/Sim` (BV-A H4)

Fecha: 3 oct 2026 · Estado: **aceptada y ENCENDIDA** (`tuning.movement.accelTicks` = 3), **acotada al juego abierto**
(enmienda del mismo día, decisión del coordinador) ·
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
| (a) `EnLaTurbaNoSePita`, semilla 17 | la falta se pitó en el **último tick reglamentario**, antes de que se emitiera MOB_START en el mismo tick (índices 227-228 frente a 230). El árbitro aún estaba: no es regla rota, es el instrumento comparando ticks | **CONFIRMED** (traza) | el test cuenta desde el suceso MOB_START (Regla J) |
| (b1) `FoulByFoul…`: sacador de falta derribado, semilla 81 | **no es «llegar tarde»**: una reanudación pedida a mitad del bucle de jugadores dejaba `BallDead` falso el resto del tick (se leía al principio), y quien decidía después empezaba una carga; se resolvió en la cuenta atrás (bloqueo decidido en el tick 1284 de la falta, resuelto en el 1287 sobre el sacador). Pasa **sin arranque**: 9 contactos en la cuenta atrás en 300 partidos con 0 y con 3 | **CONFIRMED** (traza, y 9 → 0 con el arreglo) | `BeginRestart` marca `BallDead` (RF-057, ADR 0132). Test `RestartContactTests` (valor conocido: semillas 18, 31, 68 antes). Es la única diferencia de motor con `accelTicks` 0 (sin él vuelve la huella vieja `2380212350706324353`) |
| (b2) control de `TheVictimOfAWhistledFoulGoesDown` | con la regla apagada caían víctimas por otro mecanismo: un **bloqueo** que gana y es falta derriba por sí mismo (29 de 34). Más bloqueos con el arranque (+7 %) | **CONFIRMED** (desglose) | el control cuenta sólo faltas de entrada |
| (c) `LethalRiskTests`, valor nulo | la run de la semilla llegaba al partido letal **sin suplentes**; `Assert.NotNull(bench)` | **CONFIRMED** | la búsqueda exige banquillo, como ya exigía la palanca de colocación |
| (d) `RepeatTackleReachTests.TheRealCaseNoLongerFreezes` | Arrollador (rara) no se activa con arranque en ninguna de 400 semillas de run (0 frente a 3 sin él). No hay caso real sustituto | LIKELY ruido de plantilla (3 frente a 0) | el caso fijado se juega con `accelTicks` 0; la regla la fijan los tests de valor conocido |
| (e) baile de cobertura de 27 fotogramas en el borde del área cerrada (penalti) | la rampa dentro del área cerrada: dos cuerpos frenando sobre el mismo punto del borde | **CONFIRMED** con el acotado: racha más larga 6 (tope 15) **sin** el parche de cobertura del commit `02a9f15`, que se retira (era un arreglo condicionado al arranque). La regla del área cerrada sigue siendo la de la ADR 0152 (`Move` saca andando) | — |
| (f) controles de `PositioningHoldTests`, `DancingTeammatesTests` y el de alcance de `TackleReachTests` | el arranque es **otro remedio del mismo síntoma**: con él encendido, el control (sin el arreglo) dejaba de ver el síntoma (27,6 % de deshechas; 274 episodios; 14,3 % de entradas lejanas) | instrumento | los controles corren con `accelTicks` 0, el motor en que se calibraron; las aserciones sobre los datos siguen con 3 |
| (g) huellas de `MobNarrowingTests` | trayectorias nuevas | — | renovadas, con los valores anteriores en el comentario |
| (h) duelos aéreos (`EmergentChainTests`), `ShotHeightTests`, `ForwardOffBallTests`, `KeeperAreaTests`, `PenaltyAreaSymmetryTests` | — | verdes con el acotado | — |

El arreglo de la barrera (sólo quita la velocidad hacia el balón) y la espera del sacador del saque de centro siguen, con
la rampa encendida: actúan también en la ventana de barrera posterior al saque, que ya es juego abierto.

### `game-design-review` del acotado (corta)

1 el jugador ve arrancar y frenar en la jugada; en las colocaciones, la gente anda a paso constante como antes; 2-3 no
hay decisión nueva; 4 RT-020 (física de movimiento), RF-053 (nadie se teletransporta, sin cambios); 5 `/Sim`
(`Move`, `InOpenPlay`), un dato; 6 alternativas: rampa en todo arreglando cada choque con la barrera y el área (lo que
se intentó: frágil, cada reanudación es un caso) o una aceleración mayor en las colocaciones (otra cifra sin
procedencia); 7 trade-off: queda *paso-0-paso* en las colocaciones (3.909 de 5.178 en 40 partidos) y el paso de
colocación a jugada arranca desde la velocidad de andar; 8 no cambia estrategias; 9 degeneración medida abajo: más
contacto (entradas +5-12 %, lesiones +9-13 %); 10 tests de arriba, sonda, lote, barrido y puertas.

## Medición final con el acotado (`accelTicks` 3 frente a 0, mismo árbol)

**Sonda limpia de la ADR 0184** (40 partidos, `AccelerationTests.ReversalsWithAndWithoutTheRamp`): inversiones a
velocidad de carrera **0,092 → 0,010 por jugador y segundo** (con la rampa en todo eran 0,004); *paso-0-paso*
**19.536 → 5.178**, de ellos en juego **14.244 → 1.269** y en reanudación 5.292 → 3.909.

Lote 10.000 × 2 semillas (media ± error típico por partido):

| | s1 con 0 | s1 con 3 | s2 con 0 | s2 con 3 |
|---|---|---|---|---|
| goles | 2,476 ± 0,013 | 2,452 ± 0,013 | 2,090 ± 0,013 | 2,103 ± 0,012 |
| tiros | 9,28 ± 0,04 | 9,02 ± 0,03 | 8,50 ± 0,03 | 8,38 ± 0,03 |
| entradas | 9,26 ± 0,04 | **10,37 ± 0,04** | 11,25 ± 0,05 | **11,86 ± 0,05** |
| faltas | 6,91 ± 0,05 | 7,42 ± 0,05 | 4,59 ± 0,02 | 4,99 ± 0,03 |
| bloqueos | 6,19 ± 0,07 | 6,64 ± 0,07 | 3,47 ± 0,02 | 3,90 ± 0,03 |
| lesiones | 0,738 ± 0,009 | **0,805 ± 0,009** | 0,407 ± 0,006 | **0,458 ± 0,007** |

Todo dentro de banda; la única fuera (`betterTeamWinRate` 60-40) ya lo estaba con 0 en las dos semillas.

Runs completas (`--full-runs 240`, semillas 1-2, contextual, 480 runs por brazo; condición 5 de la ADR 0048): muertes
por partido **0,132 ± 0,009 → 0,138 ± 0,010** (dentro del ruido); lesiones propias por run 3,06 → 3,31 ± 0,16;
`runWinRate` 11,7 / 17,5 → 15,8 / 15,4.

**`elf_none`** (`RaceBalanceTests`, semillas 1-3): **41,27 · 44,10 · 42,30** — vuelve a la banda D-29 (con 0 y las ADR
0184/0186: 39,08 · 39,90 · 39,90, [BV-C](../pendientes/BV-C.md)). El enano queda alto: 58,65 · 57,15 · 58,58 (techo 60).

**Puertas completas** (una pasada): 5 rojas de 48. Siguen las de `main` —curva de jefes, `orc_violence` 54,53,
`TheThreeDoctrinesBuyDifferently` (−0,90)— y `elf_brawler` 45,81, que **ya estaba roja con 0** en este árbol (45,65); pasan
a verde `elf_none`, `betterTeamWinRate` de la puerta y `elf_out_of_zone` (45,68 con 0).

**Barrido de detectores** (`tools/barrido-detectores.sh 1000`, ref / run, frente a `main`): baile BB-K **0,838 → 0,244** /
0,803 → 0,259; amontonamiento sobre el portero 0,780 → 0,700 / 0,646 → 0,643; sin repliegue 1,84 → 1,77 / 1,69 → 1,62;
igual el resto salvo **BF-C «delantero elige pegar sin portador»** 2,33 → 2,77 / 1,96 → 2,58 (con 0 en este árbol: 2,36 /
1,97, así que es de la rampa). Desglosado (300 partidos): el grueso son fotogramas de un delantero **en el suelo** con la
última decisión, una entrada al portador, aún anotada (2.721 → 3.267 con el balón en vuelo); fuera del suelo, 1.396 →
1.551 (+11 %), el mismo +10 % de contacto de todo el equipo. «Delantero pega sin balón» sigue en 0 (ADR 0133).
**LIKELY**: no es un delantero que pegue más sin balón, es más contacto en general y un detector que cuenta la acción
vieja mientras está derribado (anotado para BF-C, no tocado aquí). Gol sin ángulo 0,178 → 0,205 (±0,019) y balón suelto
quieto (run) 0,057 → 0,070 (±0,011): dentro del ruido.

## Procedencia del 3 (Regla H): criterio escrito ANTES del barrido 2/3/4

Escrito y commiteado antes de medir 2 y 4 (revisión independiente: el 3 no tenía procedencia). Se elige **el menor
`accelTicks` cuya sonda limpia (40 partidos, `ReversalsWithAndWithoutTheRamp`) deje las inversiones a velocidad de
carrera en ≤ 0,02 por jugador y segundo**; a igualdad, el de menor subida de lesiones por partido (lote 10.000, s1). El
0,02 es **provisional, sin medir** su percepción: es un orden de magnitud por debajo del motor sin rampa (0,092), que es
lo que pedía BV-A («que no vayan y vengan»), no una cifra observada en pantalla.

## Siguiente paso

Que el revisor lo juegue. Abierto: el aumento de contacto y lesiones (+9-13 %) es el coste de la rampa en la carrera
(quien sale de parado llega tarde y la entrada le alcanza); el *paso-0-paso* que queda en las colocaciones, si se nota;
el detector BF-C cuenta derribados.

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
