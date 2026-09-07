# Plan de implementación: intercepción durante el vuelo del disparo (AW-A) y derivados

Plan derivado de `docs/referencia-motores-futbol.md` (7 sep 2026). Cubre el §1 del informe (AW-A) como
paquete principal y deja el §2 (pasillo de pase dependiente del tiempo) y el §3 (término triangular) como
pasos posteriores e independientes. **No se ha implementado nada.** Cada paso es una palanca, con sus tests,
su lote de balance y su criterio de parada, en la misma cadencia que AW-D y AW-E.

## 0. Diagnóstico sobre el código actual

Lo que hay hoy (números del código y de `data/sim/tuning.json`, no del informe):

| Hecho | Dónde | Valor |
|---|---|---|
| Velocidad del disparo | `ball.shotSpeedCellsPerTickMilli` | 0,70 casillas/tick |
| Velocidad de un jugador | `movement.base + per99 × Speed/99` | 0,131 a 0,159 casillas/tick (Speed 0-99) |
| Radio de intercepción de pase | `pass.interceptRadiusCells` | 0,9 casillas |
| Portería | `Pitch.GoalCenter` | un **punto** en la fila 2,5, sin anchura |
| Punto de mira del tiro a puerta | `LaunchShot` | siempre el centro exacto de la portería |
| Punto de mira del tiro fuera | `OffTargetShotTarget` (AW-C) | fila 2,5 ± 1,25 |
| Objetivo del portero (`CoverSpace`) | `Utility.EvaluateCover` | 0,7 casillas de la línea, **sobre la recta portería→balón** |
| Vuelo del disparo | `UpdateFlight` | `TryIntercept` se salta si `IsShot`; `ResolveShotArrival` solo al agotar `FlightTicksLeft` |
| Fórmula de parada | `ResolveShotArrival` | velocidad/fuerza del portero, fatiga, técnica del tirador, calidad, distancia; **no lee `goalkeeper.Position`** |

Consecuencias que el plan tiene que respetar:

- Un tiro desde 8 casillas dura 12 ticks; en ese tiempo el portero recorre 1,6-1,9 casillas. Con el radio de
  0,9 casillas, un portero a **más de ~2,7 casillas** de la trayectoria no llega nunca; a menos de 0,9 llega
  desde el primer tick. Es el rango en el que la nueva regla decide algo.
- Como el punto de mira es el centro de la portería y el portero bien colocado está sobre la recta
  portería→balón, **un portero asentado siempre está a menos de 0,9 casillas de la trayectoria**: para él la
  regla nueva es neutra y sigue aplicando la fórmula actual. La regla solo muerde al portero **desplazado**
  (volviendo de un balón suelto, tras una parada previa, arrastrado por `ChaseBall`), que es exactamente el
  caso que motivó AW-A. Es la razón para **no** tocar la fórmula de parada en el paso 1.
- El render (`MatchTrace.Capture`, `BallAt(frame)`) lee la posición del balón tick a tick, y los eventos solo
  alimentan el ticker de texto. Una parada a mitad de vuelo se dibuja sin cambios en `/Game`.
- No hay métrica de goles por partido ni de paradas en `summary.csv`: `MatchReport` cuenta `Shots`,
  `ShotsOnTarget` y `Goals` pero no `Saves`. Sin eso no se puede medir AW-A, de ahí el paso 0.

## 1. Cadencia común a todos los pasos

1. Planificación cerrada aquí; el subagente (`fast-worker`) recibe ficheros permitidos, interfaz y tests.
2. Tests filtrados del área tocada (`--filter FullyQualifiedName~X`), con al menos un test de aritmética exacta.
3. Lote de balance: `--runs 500` con semilla 1 y semilla 2 sobre `data/balance/reference.json`; se compara
   con la referencia del paso anterior, no con la de hace tres pasos.
4. Un único ajuste de magnitud si algo sale de banda; si con ese ajuste no entra, se para y se informa.
5. Informe al revisor, commit (`sim` y `data` en commits separados de `docs`), push. La suite completa una vez
   por paquete cerrado, en `main`, con `nohup` (52 min).

Referencia de partida (tras AW-D y AW-E, sin más cambios en `/Sim`):

| Métrica | s1 | s2 | Banda |
|---|---|---|---|
| possessionChanges | 24,87 | 25,42 | 12-25 (25,4 aceptado por el revisor) |
| passChainAvgLength | 2,02 | 2,01 | 2-4 |
| shotsPerMatch | 13,07 | 12,69 | 8-16 |
| tacklesPerMatch | 7,80 | 8,52 | 6-14 |
| ballThirdMaxShare | 42,43 | 41,54 | 0-50 |
| injuriesPerMatch | 0,61 | 0,34 | 0,3-0,8 |

El paso 0 añade a esta tabla goles por partido y tasa de paradas antes de tocar el motor.

## 2. Paso 0: instrumentación (sin cambio de comportamiento)

**Objetivo.** Poder medir lo que AW-A cambia. Hoy se mediría a ciegas.

**Cambios.**
- `MatchReport`: contador `Saves` (por equipo, incrementado en `ResolveShotArrival` junto al evento `Save`).
- `Sim/Analysis/MatchMetrics.cs`: tres filas **INFO** (no gating, no exigen ADR):
  `goalsPerMatch`, `shotsOnTargetShare` (= tiros a puerta / tiros) y `saveRate` (= paradas / tiros a puerta).
- `Sim.Tests/Engine/MatchRulesTests.ReportCountersAgreeWithTheEventStream`: extender al contador de paradas.
- `docs/balance.md`: las tres filas en la tabla de métricas informativas.

**Tests.** `MatchRulesTests` y `StatisticalTests` filtrados. Lote 500 × 2 semillas: fija la referencia de
`goalsPerMatch` y `saveRate` que usarán los pasos 1 y 2. Ninguna métrica puede moverse (el cambio no toca el
motor); si se mueve, el cambio no es lo que dice ser.

**Coste.** Un `fast-worker`, una tarde. **Criterio de salida.** Tres filas nuevas en `summary.csv`, resto
idéntico a la referencia.

## 3. Paso 1: el portero tiene que llegar al balón (núcleo de AW-A)

**Regla.** Durante el vuelo de un tiro a puerta, cada tick, si el portero rival está a menos de
`save.reachCells` de la posición actual del balón, se resuelve **en ese tick** el duelo de parada con la
fórmula actual, sin cambiarla. Si el duelo falla, el balón sigue su vuelo y entra sin segundo duelo. Si el
portero no ha estado nunca dentro del radio cuando el balón llega a la línea, es gol sin duelo. Un solo
intento por disparo, como `InterceptAttempted` en los pases.

Es la traducción literal del bucle de intercepción por ciclos de librcsc/HELIOS-base
(`control_area + max_speed × steps` frente a la distancia) al motor actual: no hace falta precalcular nada
porque el propio tick es el paso del bucle y el portero se mueve con su IA de siempre entre un tick y otro.

**Cambios en `/Sim`.**
- `Ball`: campo `SaveAttempted` (bool), reseteado en `LaunchShot` y `SetLoose`/`SetOwner`.
- `MatchEngine.UpdateFlight`: cuando `IsShot && ShotOnTarget && !SaveAttempted`, llamar a un nuevo
  `TryGoalkeeperReach()` **antes** de comprobar `FlightTicksLeft <= 0`, de modo que el tick de llegada también
  cuenta (el balón está entonces en la línea, donde el portero asentado siempre está a menos de 0,7 casillas).
- `TryGoalkeeperReach()`: `Vec2.Distance(gk.Position, _ball.Position) < save.ReachCells`, `CanTouchBall(gk)`,
  marca `SaveAttempted` y ejecuta el duelo. La fórmula del duelo se extrae de `ResolveShotArrival` a un
  método `ResolveSaveDuel(goalkeeper, shooter)` que devuelve bool; **no cambia ni un término**.
- `ResolveShotArrival`: si `ShotOnTarget`, ya no hace el duelo: es gol (el duelo, si tocaba, ocurrió en
  `TryGoalkeeperReach`). La rama de tiro fuera queda igual.
- `SaveTuning`: nuevo `ReachCells` (float, casillas). Valor de partida **0,9**, el mismo que
  `pass.interceptRadiusCells`, para no introducir un número nuevo sin motivo. `Catalog.cs`, `DataLoader.cs`,
  `data/sim/tuning.json`, esquema de tuning.
- Determinismo: la comparación es de `float` contra constante, como en `TryIntercept` (RT-023 permite `float`
  en posiciones). El RNG solo se consume cuando hay duelo: un partido con portero fuera de alcance consume
  menos tiradas que hoy, lo que cambia todas las semillas. Es esperable y no es un fallo, pero el test de
  determinismo RT-024 sigue teniendo que pasar (dos ejecuciones iguales).

**Lo que NO se toca en este paso.** La fórmula de parada, `basePercent`, la IA del portero, el punto de
mira del tiro, los defensas. Todo eso tiene su paso propio o queda descartado con motivo.

**Riesgo conocido y cómo se comprueba antes del lote.** Si la IA del portero no vuelve a la línea mientras
el balón vuela (por ejemplo si `ChaseBall` le gana a `CoverSpace` con el balón en vuelo), la regla nueva
convertiría muchos tiros en goles sin duelo y el lote lo achacaría al radio. Antes del lote: volcado de
utilidad del portero (RT-098, skill `sim-debug`) en 3-4 disparos de un partido de referencia, y un recuento
en 20 partidos con traza de "gol con el portero a más de `ReachCells`" frente a "gol tras duelo". Si el
portero no acude, el primer arreglo es de IA (peso o contexto de `CoverSpace` con balón en vuelo), no del radio.

**Tests (nuevo `Sim.Tests/Engine/ShotInterceptionTests.cs`).**
1. Portero en la línea, fila 2,5, tiro desde el centro: en 50 semillas hay eventos `Save` y `Goal`; ninguna
   termina en gol sin que el portero haya estado a menos de `ReachCells` (se comprueba con la traza).
2. Portero inmovilizado a 3 casillas de la trayectoria (estado `KnockedDown` con duración mayor que el vuelo):
   en 50 semillas **nunca** hay `Save`; todos los tiros a puerta son gol.
3. Aritmética exacta del borde: portero inmóvil exactamente a `ReachCells − 0,01` → hay duelo;
   a `ReachCells + 0,01` → no lo hay. Usa `FlightTicks` con la distancia real para fijar el tick.
4. `SaveAttempted` solo permite un duelo: con `save.basePercent` forzado a 5, un tiro no genera dos tiradas
   (se verifica comparando el estado del RNG o el número de eventos).
5. `MatchRulesTests.GoalkeeperNeverLeavesTheArea` y `ReportCountersAgreeWithTheEventStream` sin cambios y
   en verde: una parada a mitad de vuelo pone el balón en la posición del portero, siempre dentro del área.

**Lote de balance.** 500 × 2 semillas. Métricas que vigilar, con la referencia del paso 0:

| Métrica | Qué se espera | Parada |
|---|---|---|
| goalsPerMatch (INFO) | sube: el portero desplazado ya no para | > +40 % sobre la referencia |
| saveRate (INFO) | baja | < 25 % absoluto |
| scorelineShare_1-0_to_3-2 (gating) | debe seguir IN | OUT |
| share_over5goals (INFO) | puede subir | > 20 % |
| shotsPerMatch, possessionChanges | no deberían moverse; si lo hacen, la causa es otra | fuera de la tolerancia habitual (±1) |

**Ajuste permitido (uno solo).** Si `goalsPerMatch` dispara, subir `save.reachCells` de 0,9 a 1,2 (un portero
cubre más de una casilla de anchura al estirarse). Si aun así se sale, parar e informar: el siguiente lever
sería el paso 2 (estirada), no `basePercent`. Cambiar `basePercent` compensaría el síntoma escondiendo el
diagnóstico y además reequilibra al portero asentado, que no es el problema.

**Criterio de salida.** Tests en verde, `scorelineShare` IN en las dos semillas, `goalsPerMatch` dentro
de la tolerancia, y el recuento en traza confirma que los goles nuevos son de portero desplazado. Commit
`feat(sim): la parada exige llegar al balón — RF-057c, AW-A`, ADR corto (decisión: la parada se resuelve
tick a tick durante el vuelo; motivo y fuentes del informe).

## 4. Paso 2: la estirada (alcance desesperado)

**Regla.** Solo en el tick de llegada, si no hubo duelo y el portero está a menos de `save.diveReachCells`
(> `reachCells`), se resuelve el duelo con una penalización fija `save.divePenaltyPercent` sobre
`savePercent`. Traduce el par "usual / optimista" de gfootball (`AI_GetTimeNeededForDistance_ms`, 0,28 m
frente a 0,9 m). Valores de partida: `diveReachCells` 1,5, `divePenaltyPercent` 20.

**Por qué es un paso aparte.** Añade dos números y un segundo camino al duelo; si se mete junto al paso 1 no
se sabe cuál de los dos mueve `goalsPerMatch`. Además puede que el paso 1 ya deje `goalsPerMatch` en un sitio
aceptable y este paso no haga falta.

**Tests.** Extender `ShotInterceptionTests`: portero inmóvil entre `reachCells` y `diveReachCells` → hay
duelo con la penalización (aritmética exacta sobre `savePercent`); más allá → gol sin duelo.

**Lote y parada.** Iguales al paso 1. Se espera que `goalsPerMatch` baje algo respecto del paso 1 y
`saveRate` suba. Ajuste único: `divePenaltyPercent` 20 → 30.

## 5. Paso 3: los defensas también pueden bloquear un tiro

**Regla.** El mismo `TryGoalkeeperReach` extendido a los jugadores de campo del equipo defensor: dentro de
`pass.interceptRadiusCells` del balón en vuelo, un intento por jugador y disparo, probabilidad
`InterceptChance(defender, shooter)` reducida por un factor `shot.blockChancePercent` (partida: 50). Si
bloquea, el balón queda **suelto** en la posición del defensor (no en posesión: un tiro bloqueado rebota),
con `LastTouchTeam` del defensor.

**Alcance mayor que los anteriores.** Necesita un evento nuevo (`SHOT_BLOCKED` en `EventType`, texto en
`data/l10n` es/en, fila en `UiText`), contador `ShotsBlocked` en `MatchReport`, y decidir si cuenta como tiro
a puerta en `ShotsOnTarget`. Toca `/Sim`, `/data` y `/Game` (ticker), así que son tres commits.

**Lote y parada.** Además de las del paso 1: `shotsPerMatch` puede bajar si el bloqueo corta jugadas;
`possessionChanges` sube (balón suelto). La tolerancia de `possessionChanges` ya está agotada (25,4 sobre
25), así que este paso es el más probable de quedarse fuera de banda; si ocurre, se propone al revisor la ADR
de banda (RT-057) con los datos, en lugar de bajar el bloqueo hasta hacerlo irrelevante.

## 6. Paso 4 (§2 del informe): pasillo de pase dependiente del tiempo

> **Intentado y descartado (7 sep 2026), sin commitear.** Ver `docs/pendientes.md` fila AW-A para el
> detalle completo de las tres pruebas (radio 0,5 sobre los dos consumidores, 0,5 solo sobre `EvaluatePass`,
> 0,3 solo sobre `EvaluatePass`). Ninguna cumplió el criterio de parada: `passChainAvgLength` se queda bajo
> el suelo de 2 en al menos una semilla y `possessionChanges` empeora en vez de mejorar. Conclusión: la
> premisa de SimpleSoccer (espacio continuo) no traduce bien a una rejilla de 5×16 sin un tope absoluto al
> radio efectivo, que el diseño de abajo no incluía. No reintentar con solo un ajuste de magnitud; una
> vuelta futura necesitaría rediseñar el crecimiento del radio (por ejemplo con un máximo absoluto en
> casillas, no solo el factor lineal).

**Regla.** `Utility.PassLaneRadius` (0,6 fijo) pasa a ser `radio + velocidad_rival × ticks` donde `ticks` es
lo que tarda el balón en llegar al punto del segmento más cercano al rival (`distancia_desde_origen /
passSpeed`). Un rival lejos del pasador pero cerca del receptor tiene tiempo de cerrar el pasillo; hoy no
cuenta. Es `isPassSafeFromOpponent` de SimpleSoccer con las unidades del motor.

**Dónde muerde.** `passOpenReceiverBonus` en `EvaluatePass` y `findSpaceOpenLaneBonus` en
`EvaluateFindSpace`, es decir, qué pases se eligen y adónde se desmarca la gente. Se espera que baje el
número de pases interceptados (`PassFailed intercepted`) y suba `passChainAvgLength`.

**Tests.** `UtilityTests`: rival a 0,8 casillas del segmento junto al receptor cierra el pasillo con la regla
nueva y no con la vieja; el mismo rival junto al pasador no lo cierra (aritmética exacta con
`passSpeedCellsPerTickMilli`).

**Lote y parada.** `passChainAvgLength` (2,0, justo sobre el suelo de 2) es la métrica sensible: si sube,
bien; si baja de 2, se para. `possessionChanges` debería bajar (menos intercepciones), lo que además daría
margen al paso 3. Por eso, si el paso 3 se sale de banda, **conviene hacer el paso 4 antes** y repetir.

## 7. Paso 5 (§3 del informe): término triangular en `FindSpace` y `OfferSupport`

Opcional. Sumar a los candidatos un bonus por "distancia al balón entre 2 y 4 casillas" (ni encima ni
lejos), de SimpleSoccer. Solo si tras los pasos 3 y 4 el revisor sigue viendo apelotonamiento con AW-E
cerrado. No se planifica en detalle hasta entonces.

## 8. Decisiones que se dejan al revisor (no bloquean los pasos 0-2)

1. **Anchura de la portería.** Hoy todo tiro a puerta apunta al centro exacto, así que la fila del portero
   solo importa cuando está desplazado. Si se quiere que colocarse "en el palo corto" cuente, el punto de
   mira tendría que dispersarse dentro de una boca de ±0,5 casillas (paso adicional entre el 2 y el 3). Cambia
   la lectura de `shootAnglePenaltyPerRow` y de "fuera", así que es regla de juego y no se hace sin decirlo.
2. **Banda de `possessionChanges`.** Ya está en el límite aceptado (25,4). El paso 3 la empujará; la
   alternativa a una ADR de banda es hacer el paso 4 antes.

## 9. Orden propuesto y esfuerzo

| Paso | Palanca | Ficheros principales | Esfuerzo | Lote |
|---|---|---|---|---|
| 0 | contadores y filas INFO | MatchReport, MatchMetrics, balance.md | pequeño | sí (referencia) |
| 1 | portero debe llegar (`reachCells`) | MatchEngine, Ball, Catalog, DataLoader, tuning.json, tests nuevos | medio | sí |
| 2 | estirada (`diveReachCells`, `divePenaltyPercent`) | los mismos | pequeño | sí |
| 4 | pasillo de pase por tiempo | Utility, UtilityTests | pequeño | sí |
| 3 | bloqueo de tiro por defensas | + EventType, l10n, UiText, MatchReport | medio-grande | sí |
| 5 | término triangular | Utility | pequeño | solo si hace falta |

Se propone hacer 0 → 1 → 2 y **parar para revisión jugando la build** (el revisor pidió ver al portero), y
solo después 4 → 3.
