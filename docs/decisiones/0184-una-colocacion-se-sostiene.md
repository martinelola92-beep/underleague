# 0184 — Una colocación se sostiene (BV-A H8)

Fecha: 3 oct 2026 · Estado: **aceptada** · Requisitos: RT-089, RT-093, RT-096, RT-097, RT-098 · Ficha: [BV-A](../pendientes/BV-A.md) ·
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
por qué cambió la acción. Validado con respuesta conocida (`PositioningHoldTests.TheReversalInstrumentAnswersTheKnownCases`:
carrera recta 0, media vuelta sí, giro de 90° no, paso de andar no) y contra la cifra de BV-A: en 40 partidos de
referencia ve **0,301 inversiones por jugador y segundo, 46,5 % deshechas** (BV-A, otro partido y otro equipo: 0,33/s
y 52 %).

## Causas (CONFIRMED, 40 partidos, semillas 1-40)

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

Un dato, `ai.context.positioningHoldBonus` = **40** (0 = el motor de antes, bit a bit: comprobado con las huellas de
`MobNarrowingTests` antes de cambiarlas), y una regla con tres caras, siempre **entre acciones de colocación**
(`CoverSpace`, `Retreat`, `MarkOpponent`, `FindSpace`, `OfferSupport`: el conjunto que ya cede ante el deber de la ADR 0177):

1. si otra colocación gana a la que el jugador ejecuta por menos de 40 puntos, sigue con la suya. Va **después** del
   bucle de `Utility.Choose`, no como bono dentro: perseguir, entrar, presionar, bloquear o pasar ganan y pierden
   exactamente como antes (tests `TheHoldOnlyEverSwapsOnePositioningForAnother`, `TheHoldNeverStopsAPlayerFromGoingForTheBall`);
2. una colocación en curso no se descarta por el límite exterior al llegar al borde: se queda en él;
3. `FindSpace` vuelve a puntuar el hueco que ya buscaba, con las mismas reglas y 40 de ventaja.

Precedentes del propio motor: el compromiso de la conducción (`dribble.driveTicks`) y de la protección (ADR 0137),
y la histéresis que AW-S dejó anotada como deuda (gfootball exige al aspirante un 20 % de ventaja).

**Procedencia del 40 (Regla H).** Barrido en la sonda (40 partidos; deshechas / inversiones por jugador y segundo):
0 → 46,5 % / 0,301; 30 → 15,3 % / 0,181; **40 → 13,7 % / 0,164**; 50 → 12,8 % / 0,162; 60 → 11,5 % / 0,149; 100 →
11,5 % / 0,135. El objetivo del encargo era < 15 %: 40 es la menor cifra que lo cumple. **No se toma la que satura
(60) porque el lote mostró una respuesta en dosis empinada** en el resto del juego (semilla 1, 10.000 partidos):

| `positioningHoldBonus` | goles | tiros | entradas | lesiones |
|---|---|---|---|---|
| 0 | 2,479 ± 0,012 | 9,30 | 7,63 ± 0,04 | 0,692 ± 0,009 |
| **40** | **2,579 ± 0,013** | 9,59 | **8,43 ± 0,04** | 0,728 ± 0,009 |
| 60 | 2,393 ± 0,013 | 9,09 | 9,05 ± 0,04 | 0,771 ± 0,009 |
| 100 | 2,181 ± 0,012 | 8,47 | 11,29 ± 0,05 | 0,884 ± 0,010 |

Con 100 se van los goles un 12 % y las lesiones rozan el techo de RT-056 (0,90): es el «jugador clavado» de la
pregunta 9. 40 equivale a una casilla de `retreatDistanceBonusPerCell` (40 por casilla) y a dos tercios de casilla de
avance de `FindSpace`.

**Descartado, medido:** sostener sólo mientras el estado táctico del equipo no cambie (la lectura literal de «salvo
cambio de situación», con un campo nuevo en `MatchPlayer`), probado con 60: casi no cambia la oscilación (12,6 % frente
a 11,5 %) y en la semilla 2 quitaba más goles (2,04 frente a 2,13; error típico 0,013). Las puntuaciones ya cambian
cientos de puntos al cambiar la posesión, así que la situación ya rompe la sostenida por sí sola.

## Efecto medido

Sonda (40 partidos, `PositioningHoldTests` con control): deshechas **46,5 % → 13,7 %**, inversiones **0,301 → 0,164**
por jugador y segundo, *paso-0-paso* 612 → 478 por partido (lo que queda es H4, ADR 0185).

Lote de `/Balance` (`--runs 10000 --teams data/balance/reference.json`, base del mismo árbol con el dato a 0; media ±
error típico por partido, de `matches.csv`):

| | s1 base | s1 = 40 | s2 base | s2 = 40 |
|---|---|---|---|---|
| goles | 2,479 ± 0,012 | 2,579 ± 0,013 | 2,189 ± 0,013 | 2,194 ± 0,013 |
| tiros | 9,30 ± 0,03 | 9,59 ± 0,04 | 8,53 ± 0,03 | 8,76 ± 0,03 |
| cambios de posesión | 25,49 ± 0,06 | 24,71 ± 0,06 | 25,14 ± 0,06 | 25,04 ± 0,06 |
| cadena de pases | 1,915 | 1,978 | 1,989 | 2,040 |
| entradas | 7,63 ± 0,04 | 8,43 ± 0,04 | 9,04 ± 0,04 | 9,74 ± 0,04 |
| entradas sin balón | 3,39 | 3,46 | 2,01 | 2,34 |
| lesiones | 0,692 ± 0,009 | 0,728 ± 0,009 | 0,395 ± 0,006 | 0,421 ± 0,007 |
| faltas | 6,26 | 6,52 | 3,91 | 4,29 |
| intercepción de pase (%) | 5,96 | 6,02 | 6,82 | 6,92 |
| centros | 2,46 | 2,11 | 2,48 | 2,12 |
| `betterTeamWinRate` 60-40 | 86,31 | **94,30 OUT** | 98,50 OUT | 97,90 OUT |
| `betterTeamWinRate` 60-50 | 80,07 | 82,41 | 80,31 | 85,05 |

El equipo **no reacciona peor**: goles y tiros iguales o algo más, intercepciones iguales, más presión (entradas +8-10 %).
Las métricas obligatorias de RT-056 quedan en banda en las dos semillas salvo `betterTeamWinRate` 60-40, que mide
**una sola pareja de plantillas por semilla** (`docs/balance.md`): en la semilla 2 ya estaba fuera en la base. Ver
«Puertas».

**Atribución (semilla 1, con 60, cada cara sola y las otras apagadas):** la sostenida entre colocaciones sola: entradas
7,63 → 8,20, goles 2,48 → 2,33; el borde exterior solo: nada medible (goles 2,475, entradas 7,62); el hueco de
`FindSpace` solo: entradas 8,53, goles 2,57. Lectura (LIKELY): el defensa que sostiene su línea defiende mejor y está
más cerca para entrar; el atacante que sostiene su hueco recibe más balones y le entran más.

**Sube algo la sangre**: lesiones +0,03 por partido, en banda; lo vigila la puerta de la ADR 0168.

## Nota de `game-design-review` (diez preguntas)

1 el jugador ve futbolistas que dan media vuelta y la deshacen dos décimas después; 2 que cada uno se comprometa con
su sitio y lo deje sólo si otro es claramente mejor o el juego cambia; 3 colocación de zona (`simulacion.md`, RF-042) —
no es regla nueva, es quitar un artefacto de la utilidad—; 4 `/Sim/Engine/Utility.cs` y un dato en
`data/ai/weights.json`; nada en `/Game`; 5 alternativas: bono a la acción en curso **dentro** del bucle (rechazada: la
auditoría 3 midió que sostener `CoverSpace` así hunde la tasa de entrada, 19,5 → 11,4 %), compromiso de N ticks
(rechazado: ciego a la situación), cadencia de decisión 3 (auditoría 3: menos churn pero entradas fuera de banda, y
cambia todo el motor); 6 el mismo defecto puede estar en cualquier acción que fije un punto desde la posición propia
(`OfferSupport`: punto a 1,6 del portador por mi lado; sin evidencia de que oscile, 193 inversiones en 40 partidos);
7 trade-off: reacciona algo más tarde a cambios pequeños; 8 el defensa se queda en su línea y el atacante en su hueco:
más contacto; 9 degeneración: un jugador clavado mientras el juego pasa — medida, aparece con 100 y por eso la dosis
es 40; la regla nunca pisa perseguir ni entrar; 10 sonda con control, escenarios montados, lote de dos semillas y puertas.

## Puertas

`Category=Gate`, una invocación (48 tests, 11 m 44 s): **7 rojas**. Contra las rojas conocidas de `main` (BossGate curva,
BuildGate ×3, MatchOrder `DefensiveConcedes`):

- **Iguales:** `BossGateTests.TheGateCurveMatchesTheAdr0033Table` y las tres de `BuildGateTests`.
- **Sale de rojo:** `MatchOrder` `DefensiveConcedes` (pasa).
- **Nuevas (dos causas):**
  1. `StatisticalTests.BetterTeamWinRateIsInRange` y `NoMandatoryMetricIsOutOfRange`: `betterTeamWinRate` 60-40 =
     92,17 en 1.000 partidos de la semilla 1 (banda 70-90). **Es la plantilla de la semilla 1, no la regla**
     (`docs/balance.md`: una pareja de plantillas por semilla). Ocho semillas pareadas (1 y 2 con 10.000 partidos,
     3-8 con 2.000), 60-40 base → 40: 86,3→94,3 · 98,5→97,9 · 90,1→86,5 · 84,4→90,4 · 77,5→76,6 · 84,4→86,8 ·
     86,5→87,7 · 100→99,7; media **88,5 → 90,0**, diferencia pareada **+1,5 ± 1,4** (error típico entre semillas):
     no distinguible de cero. La fila 60-50 en las mismas ocho: **75,15 → 75,14**. Es el mismo caso que la enmienda
     de la ADR 0176 (BB-P).
  2. `FullRunGateTests.TheThreeDoctrinesBuyDifferently`: la ahorradora acaba con 13,84 % de oro sin gastar y la
     contextual con 16,06 % (la puerta exige ahorradora > contextual). Con `--full-runs 240`, tres semillas pareadas,
     ahorradora − contextual: base +1,28 · +0,97 · +0,76; con 40 −2,22 · −0,22 · +1,83 (media +1,0 → −0,2,
     dispersión ~2 entre semillas). La señal no se separa del ruido con tres semillas, pero la puerta es roja con la
     semilla que fija. `runWinRate` 16,8 → 16,4 % y `deathsPerRun` 1,40 → 1,31 de media en las mismas tres.

**Ninguna banda se relaja.** Las dos rojas nuevas quedan **abiertas para el revisor**: la primera es una puerta que
depende de una plantilla (BB-P); la segunda, una comparación de la economía de la run que esta regla de motor no toca
de forma directa. No se persigue un valor del dato que las ponga en verde: con las diferencias dentro del ruido entre semillas,
sería elegir la cifra por la suerte de una plantilla (lo que la ADR 0176 tuvo que deshacer). H4 (ADR 0185) **no se
implementa en esta tanda** por lo mismo: con dos puertas abiertas, otra primitiva de movimiento encima no se podría
atribuir.
