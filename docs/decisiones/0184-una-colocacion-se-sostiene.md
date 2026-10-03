# 0184 — Una colocación se sostiene (BV-A H8)

Fecha: 3 oct 2026 · Estado: **aceptada, con dos puertas en rojo para decisión del revisor** (ver «Puertas») · Requisitos: RT-089, RT-093, RT-096, RT-097, RT-098 · Ficha: [BV-A](../pendientes/BV-A.md) ·
Hermanas: [ADR 0176](0176-dos-companeros-no-cubren-el-mismo-punto.md) (BB-K), [ADR 0177](0177-el-designado-de-un-balon-quieto-va-a-por-el.md) (BC-G) ·
Siguiente paso: [ADR 0185](0185-arranque-y-frenada.md) (H4)

## Problema

El revisor ve a los jugadores «ir y venir como indecisos». BV-A H8 lo midió en la traza: el 52 % de las inversiones de
rumbo a velocidad de carrera se deshacen en ≤ 4 ticks. La causa en `/Sim` quedó **LIKELY**.

## Instrumento (Regla J)

`Sim.Tests/Engine/OscillationProbeTests.cs` (`Category=Diagnostic`) sobre la traza (`MatchTrace`, RT-098): una
**inversión** son dos pasos consecutivos de ≥ 0,09 casillas con más de 135° entre ellos; se **deshace** si en los 4
pasos siguientes hay uno de > 0,02 que vuelve a formar más de 135° con el de salida. Cada inversión se clasifica
por lo que la traza sabe del tick (acción vigente, destino, balón en vuelo), y el censo de utilidad
(`UtilityCensus.Switches`, `SwitchMargin`, `SwitchesFromOuterLimit`, `SwitchesFromDiscard`, contabilidad pura) dice
por qué cambió la acción.

**Sonda limpia** (revisión independiente): quedan fuera los **porteros**, los **cortes** (un paso de más de 0,3
casillas —por encima de la carrera más rápida con turba y empuje— o cualquier fotograma con reanudación en la
ventana) y las **inversiones legítimas** (cambia el dueño del balón o su vuelo en los tres fotogramas del giro:
posesión, pase, despeje, desvío). Sin sostenida, en 40 partidos: 8.285 inversiones contadas, 2.838 cortes y 4.082
legítimas apartadas; **42,4 % deshechas, 0,164 por jugador y segundo**. (La primera versión, sin limpiar, daba
46,5 % y 0,301/s; BV-A medía 52 % y 0,33/s en otro partido.)

Validación con respuesta conocida: geometría (`PositioningHoldTests.TheReversalInstrumentAnswersTheKnownCases`:
carrera recta 0, media vuelta sí, giro de 90° no, paso de andar no) y, **sobre una traza real**, contra un instrumento
independiente (`TheProbeAgreesWithTheUtilityDumpOnARealTrace`): las inversiones que la sonda atribuye a un cambio de
acción en la semilla 1 las confirma el volcado RT-098 en el mismo tick, con la misma acción elegida y distinta de la
anterior.

## Causas (CONFIRMED, 40 partidos, semillas 1-40, con la primera versión de la sonda)

De 17.766 inversiones, 11.149 (63 %) ocurren en un **cambio de acción**, y el par dominante es
`CoverSpace ⇄ Retreat` (5.473, el 86 % deshechas). Tres mecanismos, cada uno aislado:

1. **Empate que el propio movimiento invierte.** El contexto de `Retreat` crece con la distancia a casa
   (`retreatDistanceBonusPerCell`), así que al ir a cubrir gana el repliegue y al replegarse vuelve a ganar cubrir:
   un ciclo límite. El 22 % de los cambios de acción se deciden por menos de 10 puntos.
2. **Descarte al llegar al borde.** `CoverSpace` se descarta si su punto (fuera del límite exterior) queda a menos de
   `OuterLimitMinAdvance` (0,25) del jugador: al llegar lo manda a casa, y a 0,25 de distancia vuelve a ser viable.
   Volcado RT-098 (semilla 1, portero, ticks 316-330): `CoverSpace` 864 y `Retreat` 80 en ticks alternos, con
   `CoverSpace` ausente (descartada) en los otros. El censo: 1.470 de los 5.191 `CoverSpace → Retreat` vienen de ahí.
3. **El hueco de `FindSpace` no está entre sus candidatos.** Los dieciséis candidatos se miden desde la posición de
   cada tick; el hueco elegido en la decisión anterior no se vuelve a puntuar y un empate manda al jugador al lado
   contrario. Quitados 1 y 2, era el 42 % de las inversiones deshechas que quedaban.

**REJECTED:** la separación de cuerpos que empuja y luego atrae (3 inversiones de 17.766 tienen el paso contra el
destino; el empuje, ≤ 0,06/tick, no invierte un paso de carrera), y la llegada con rebase (120). **Sin aislar:** el
destino que se mueve con el balón en vuelo (34 % de las inversiones, pero repartido entre las tres causas de arriba).

## Decisión

Un dato, `ai.context.positioningHoldBonus` = **50** (0 = el motor de antes, bit a bit: lo fija el test
`MobNarrowingTests.WithTheHoldOffEveryTraceIsTheOneBeforeAdr0184` con las tres huellas de antes de esta ADR), y una
regla que actúa **sólo en el paso posterior al bucle de `Utility.Choose`**, sólo **entre acciones de colocación**
(`CoverSpace`, `Retreat`, `MarkOpponent`, `FindSpace`, `OfferSupport`: el conjunto que ya cede ante el deber de la
ADR 0177) y sólo **mientras la posesión no haya cambiado** desde que se eligió (`MatchPlayer.ChoseWithBall`):

1. si la mejor acción es otra colocación y gana a la que el jugador ejecuta por menos de 50 puntos, sigue con la suya;
2. la colocación en curso entra en esa comparación aunque el límite exterior la descarte **por haber llegado al
   borde** (y sólo por eso); contra perseguir, entrar, presionar, bloquear o pasar sigue descartada;
3. `FindSpace` vuelve a puntuar el hueco que ya buscaba y sólo lo cambia si el mejor nuevo le gana por más de 50.
   **La sostenida elige el sitio, no puntúa**: lo que `FindSpace` vale frente a las demás acciones sigue siendo el
   mejor hueco disponible, como sin sostenida.

Al recuperar o perder el balón se decide desde cero.

**Corregido tras la revisión independiente.** La primera versión (commit `fbe7918`) tenía dos fugas: el hueco de
`FindSpace` sumaba la sostenida a su puntuación (competía mejor contra perseguir o bloquear, y frente a otra colocación
contaba dos veces), y el descarte del borde se levantaba dentro de `Evaluate` (así `CoverSpace` se sostenía también
frente a acciones que no son de colocación). Las dos quedan en el paso posterior al bucle, y tests que fallaban con la
versión de antes: `TheHoldOnlyEverSwapsOnePositioningForAnother` (rejilla de escenarios con el balón suelto, en un
compañero y en un rival, viniendo de cada una de las cinco colocaciones), `ARunningFindSpaceNeverResistsChasingOrBlocking`
y `WinningTheBallBreaksTheHold`. **Medido, las fugas no explicaban los goles**: con 40 en la semilla 1 los goles eran
2,579 ± 0,013 con fugas y 2,583 ± 0,013 sin ellas.

Precedentes del propio motor: el compromiso de la conducción (`dribble.driveTicks`) y de la protección (ADR 0137),
y la histéresis que AW-S dejó anotada como deuda (gfootball exige al aspirante un 20 % de ventaja).

**Procedencia del 50 (Regla H).** Barrido en la sonda limpia (40 partidos; deshechas / inversiones por jugador y
segundo): 0 → 42,4 % / 0,164; 40 → 15,4 % / 0,096; **50 → 14,6 % / 0,095**; 60 → 13,6 % / 0,088; 80 → 12,3 % / 0,077;
100 → 11,3 % / 0,076. El objetivo del encargo era < 15 %, y 50 es la menor dosis que lo cumple. No se sube más porque
la respuesta en el resto del juego es empinada (semilla 1, 10.000 partidos, versión con fugas): con 100 los goles caían
un 12 % y las lesiones llegaban a 0,884, el «jugador clavado» de la pregunta 9.

**Descartado, medido:** la sostenida sin excepción de posesión (la revisión pidió que al recuperar el balón se pase a
buscar hueco u ofrecerse; ahora es un test) y la excepción por cualquier cambio de estado táctico (con la versión con
fugas casi no cambiaba la oscilación y en la semilla 2 quitaba goles: 2,04 frente a 2,13).

## Efecto medido

Sonda limpia (40 partidos, `PositioningHoldTests` con control): deshechas **42,4 % → 14,6 %**, inversiones **0,164 →
0,095** por jugador y segundo, *paso-0-paso* 591 → 486 por partido (lo que queda es H4, ADR 0185).

Lote de `/Balance` con el código final (`--runs 10000 --teams data/balance/reference.json`; la base es el mismo código
con el dato a 0, que es bit a bit el de antes; media ± error típico por partido, de `matches.csv`):

| | s1 base | s1 = 50 | s2 base | s2 = 50 |
|---|---|---|---|---|
| goles | 2,479 ± 0,012 | 2,539 ± 0,013 | 2,189 ± 0,013 | 2,151 ± 0,013 |
| tiros | 9,30 ± 0,03 | 9,46 ± 0,04 | 8,53 ± 0,03 | 8,61 ± 0,03 |
| cambios de posesión | 25,49 ± 0,06 | 24,59 ± 0,06 | 25,14 ± 0,06 | 24,85 ± 0,07 |
| cadena de pases | 1,915 | 2,025 | 1,989 | 2,111 |
| entradas | 7,63 ± 0,04 | 9,00 ± 0,04 | 9,04 ± 0,04 | 11,01 ± 0,05 |
| entradas sin balón | 3,39 | 3,63 | 2,01 | 2,45 |
| lesiones | 0,692 ± 0,009 | 0,759 ± 0,009 | 0,395 ± 0,006 | 0,464 ± 0,007 |
| faltas | 6,26 | 6,72 | 3,91 | 4,53 |
| intercepción de pase (%) | 5,96 | 6,11 | 6,82 | 6,65 |
| centros | 2,46 | 1,93 | 2,48 | 1,99 |
| `betterTeamWinRate` 60-40 | 86,31 | **91,66 OUT** | 98,50 OUT | 99,04 OUT |
| `betterTeamWinRate` 60-50 | 80,07 | 80,25 | 80,31 | 85,29 |

Lo que se mueve de verdad es **más contacto**: entradas +18-22 %, faltas +7-16 %, lesiones +10-17 %, todas en banda y
repartidas por puesto (semilla 1 por partido-jugador: defensa 0,56 → 0,66, medio 0,79 → 0,92, delantero 0,32 → 0,43).
Goles +2 % / −2 % según semilla e intercepciones iguales: el equipo no reacciona peor con el balón. Los centros bajan
un 20 % (sin aislar).

**Muertes (ADR 0048: la muerte sigue siendo rara).** En los partidos de referencia no hay muertes en ninguno de los dos
brazos (plantillas sanas). En runs completas (`--full-runs 240`, semillas 1-3, doctrina contextual, 720 runs por
brazo): muertes por run **1,401 ± 0,074 → 1,308 ± 0,070**, por partido **0,152 ± 0,008 → 0,149 ± 0,008** (banda
0,11-0,22); lesiones propias por run 3,70 ± 0,14 → 3,81 ± 0,15. `runWinRate` 18,3 / 16,7 / 15,4 → 12,5 / 13,8 / 14,2
(media 16,8 → 13,5, unos 2 errores típicos): **la run se pone algo más difícil**; la cota de la puerta (5-40) se cumple.

**Hermano encontrado midiendo (CONFIRMED, latente).** Con la sostenida a 50, `StalledDuelTests` cazó a un dueño del
balón en `Positioning` (estado sin pases ni regate) que eligió perseguir su propio balón y se quedó 414 ticks en
`Chasing` con un rival encima (semilla 70). Con la sostenida a 0, cero fotogramas así en 100 partidos: el mecanismo
existía sin activarse. Guarda en `MatchEngine.UpdatePlayer` —el dueño del balón decide como portador—, del tipo de la
de BB-O; no cambia ningún partido con el dato a 0 (lo fijan las huellas). **Sin aislar** el camino por el que el dueño
acaba en `Positioning` (no es el fin del derribo: probado y descartado).

**Atribución por cara** (semilla 1, versión con fugas a 60, cada cara sola y las otras apagadas): la sostenida entre
colocaciones sola: entradas 7,63 → 8,20, goles 2,48 → 2,33; el borde exterior solo: nada medible; el hueco de
`FindSpace` solo: entradas 8,53, goles 2,57. Lectura (LIKELY): el defensa que sostiene su línea está más cerca para
entrar; el atacante que sostiene su hueco recibe más balones y le entran más.

## Nota de `game-design-review` (diez preguntas)

1 el jugador ve futbolistas que dan media vuelta y la deshacen dos décimas después; 2 que cada uno se comprometa con
su sitio y lo deje sólo si otro es claramente mejor o el juego cambia; 3 colocación de zona (`simulacion.md`, RF-042) —
no es regla nueva, es quitar un artefacto de la utilidad—; 4 `/Sim/Engine/Utility.cs`, un campo en `MatchPlayer` y un
dato en `data/ai/weights.json`; nada en `/Game`; 5 alternativas: bono a la acción en curso **dentro** del bucle
(rechazada: la auditoría 3 midió que sostener `CoverSpace` así hunde la tasa de entrada, 19,5 → 11,4 %), compromiso de
N ticks (rechazado: ciego a la situación), cadencia de decisión 3 (auditoría 3: entradas fuera de banda y cambia todo
el motor); 6 el mismo defecto puede estar en cualquier acción que fije un punto desde la posición propia
(`OfferSupport`; sin evidencia de que oscile); 7 trade-off: reacciona algo más tarde a cambios pequeños sin cambio de
posesión; 8 el defensa se queda en su línea y el atacante en su hueco: más contacto, medido; 9 degeneración: un
jugador clavado mientras el juego pasa — aparece con 100 y por eso la dosis es la mínima que cumple; la regla nunca
pisa perseguir, entrar, presionar, bloquear ni pasar (test de rejilla); 10 sonda limpia con control y contraste RT-098,
escenarios montados, lote de dos semillas, runs completas y puertas.

## Puertas

Con el código final (`positioningHoldBonus` = 50), las puertas afectadas (`StatisticalTests`, `FullRunGateTests`,
`MatchOrder`): **3 rojas**, todas de las dos causas que la revisión pidió volver a medir. `MatchOrder` `DefensiveConcedes`
pasa (en `main` estaba roja). El bucle de tests (`Category!=Gate&Category!=Diagnostic`) está verde, con el determinismo
RT-024 dentro. El conjunto completo de puertas no se ha vuelto a pasar tras el arreglo; la pasada de la primera versión
(40, con fugas) daba BossGate curva y BuildGate ×3 iguales a `main`.

**Quedan como decisión del revisor, sin cerrar como ruido:**

1. `StatisticalTests.BetterTeamWinRateIsInRange` y `NoMandatoryMetricIsOutOfRange`: `betterTeamWinRate` 60-40 =
   **93,98** en 1.000 partidos de la semilla 1 (banda 70-90). Ocho semillas pareadas, base → 50 (1 y 2 con 10.000
   partidos, 3-8 con 2.000): 86,31→91,66 · 98,50→99,04 · 90,09→87,09 · 84,38→88,29 · 77,48→78,38 · 84,38→90,09 ·
   86,49→86,19 · 100,00→99,70. Media **88,5 → 90,1**; diferencia pareada **+1,6 ± 1,1** (error típico entre
   semillas). La fila 60-50 en las mismas ocho: 75,2 → 76,2. Tres de ocho semillas ya estaban por encima de 90 en la
   base (BB-P).
2. `FullRunGateTests.TheThreeDoctrinesBuyDifferently`: con la semilla 1, la contextual compra 1,27 por mercado y la
   ahorradora 1,28 (la puerta exige contextual > ahorradora). Pareado, `--full-runs 240`, semillas 1-3, contextual −
   ahorradora en compras por mercado: base +0,05 · +0,06 · +0,08; con 50 −0,01 · +0,08 · +0,07. Y ahorradora −
   contextual en oro sin gastar: base +1,28 · +0,97 · +0,76; con 50 −0,14 · +1,38 · +0,19. La semilla 1 cambia de
   signo; las otras dos no.

**Ninguna banda se relaja.** No se elige un valor del dato para ponerlas en verde: con 40, 50 y 60 caían en verde o en
rojo puertas distintas (40: oscilación 15,4 %, por encima del objetivo; 60: `RefereeSaturationTests` 24 de 60 frente a
33 en la base), lo que señala filas sensibles a la plantilla y no una dosis buena. H4 (ADR 0185) no se implementa en
esta tanda: con dos puertas abiertas, otra primitiva de movimiento encima no se podría atribuir.

**Tests que cambian (y por qué):** `MobNarrowingTests` renueva sus tres huellas (cambian todas las trayectorias) y
gana `WithTheHoldOffEveryTraceIsTheOneBeforeAdr0184`, que fija las de antes con el dato a 0.
`RefereeTraitsEngineTests.ANeutralRefereeDoesSendOffOnASecondYellow_Precondition` pasa de 250 a 500 semillas: la roja
por doble amarilla sale en 6 de 500 partidos con el dato a 0, 8 con 40 y 3 con 50 (primera en la semilla 311), un
suceso de Poisson de ~1 por cada 100 semillas con el que una ventana de 250 fallaba por mala suerte; la afirmación no
cambia.
