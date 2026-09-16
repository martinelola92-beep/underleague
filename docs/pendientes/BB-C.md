# BB-C — «Celebra» se activa cuando ya han vuelto a su campo

**Estado:** Resuelta (16 sep 2026, `Sim/Engine/MatchEngine.cs`, `ResetPositions`), corregida una vez tras
`independent-reviewer`. Confirmado en código y reproducido con semilla (contra la hipótesis original: la
celebración no se dispara tarde, es `ResetPositions` la que ignoraba el estado al mover la posición).

## Observación

**«Celebra» se activa cuando ya han vuelto a su campo** (después del salto de BB-A), y además los deja parados unos ticks al reanudar

## Causa real (corrige la hipótesis original)

La celebración se dispara **en el momento correcto** (`EnterState(Celebrating, ...)`, justo al marcar,
antes de `ScheduleKickoff`). El bug estaba en `ResetPositions()`, llamada por el saque de centro
siguiente **en el mismo tick del gol**: movía `Position`/`Velocity`/`EffectiveHome`/`TargetPoint` de
**todo** jugador en el campo sin mirar su estado, y solo protegía el `PlayerState` de
`Celebrating`/`KnockedDown` frente al `EnterState(Positioning, 0)`. El goleador quedaba con el estado
`Celebrating` correcto pero la posición ya en su casilla-hogar — la celebración se veía en el sitio
equivocado porque la posición había saltado, no porque el estado llegara tarde. Reproducido: 60 semillas
de referencia, el goleador saltaba hasta 9,37 casillas en el mismo tick del gol antes de la corrección
(`Sim.Tests/Engine/GoalCelebrationPositionTests.cs`).

**"Los deja parados unos ticks al reanudar"**: con `CelebratingTicks=30` > `kickoffTicks=15`, el goleador
sigue celebrando (sin poder decidir) 15 ticks después de que el saque ya se ha ejecutado. `CanTouchBall`
ya excluía a un jugador `Celebrating` de ser designado sacador, así que dejarlo donde está no afecta a
quién saca ni a la reforma del resto del equipo. **Que sea "el comportamiento esperado" es una afirmación
de diseño, no un hecho que el test demuestre** (independent-reviewer): no ha pasado por
`game-design-review`. Queda anotada como decisión de facto, no como conclusión cerrada — ver "Efecto
medido en balance" abajo para lo que sí se midió.

## Arreglo (corregido tras `independent-reviewer`: alcance reducido a solo `Celebrating`)

`ResetPositions()` excluye del movimiento de posición (no solo del cambio de estado) a los jugadores en
`Celebrating`. **`KnockedDown` se sacó del arreglo** (la primera versión lo incluía, sin que el ticket lo
pidiera ni nadie lo midiera): un jugador derribado es objetivo preferente de "olfato de sangre"
(`Utility.CanReceiveOffBallTackle`), y dejarlo donde cayó en vez de en su casilla-hogar cambiaría a quién
apunta ese perk durante el saque siguiente — un efecto de segundo orden real, sin medir. `KnockedDown`
sigue exactamente como antes de este ciclo (posición sí se resetea, estado no). Es el mismo bug en teoría
para ese caso, y queda anotado en "Hermanos" para su propia ficha, no mezclado con esta.

`Injured`/`SentOff` no entran en el guard: `CanTouchBall` los excluye de ser sacador igual que a
`Celebrating`/`KnockedDown`, pero ninguno de los dos puede seguir en el campo activo con el balón en
juego más allá del tick en que ocurren (`Injured` sale del campo; `SentOff` también), así que no aplica
el mismo escenario "sigue en el campo, en una animación, durante el saque siguiente".

## Efecto medido en balance (Regla D, `balance-measure`)

Un cambio de posición en `/Sim` altera comportamiento cuantificable; medido, no asumido:

| métrica (60 semillas de referencia) | sin el arreglo | con el arreglo |
|---|---|---|
| `goalsPerMatch` (2.000 partidos, `/Balance`, semilla 1) | 2,81 | 2,88 |
| `shotsPerMatch` | 8,84 | 9,09 |

Ambas dentro de sus bandas obligatorias de RT-056 (`shotsPerMatch` 7-15). El movimiento es del orden de
la variación normal entre árboles ya documentada en esta sesión (ADR 0117), no una tendencia nueva.

## Verificación

- Test nuevo `GoalCelebrationPositionTests.TheScorerDoesNotJumpToItsHomePositionOnTheSameTickAsTheGoal`
  (60 semillas de referencia, solo goles reales — excluye gol de oro y gol anulado por perk, que no pasan
  por `ResetPositions`): falla sin el arreglo (salto de hasta 9,37 casillas), pasa con él. Comprueba
  **tanto** la distancia (cota 0,6 casillas, la misma convención que
  `MatchRulesTests.TheRestartTakerStandsStillDuringTheDeadBall`) **como** que el goleador siga en
  `PlayerState.Celebrating` en el tick del gol — no solo la distancia, que por sí sola no distinguiría
  este bug de que la celebración dejara de dispararse.
- RT-024 (determinismo): verde, 4/4.
- 43 puertas (semilla 1, canónica): **3 rojas** (`BuildsWinDifferently`, `BadBuildsLoseToTheirBaseline`,
  `NoGateMetricIsOutOfRange`) — remedido **emparejado** (mismo árbol, única variable el arreglo) en dos
  semillas:

  | | semilla 1: sin arreglo | semilla 1: con arreglo | semilla 2: sin arreglo | semilla 2: con arreglo |
  |---|---|---|---|---|
  | `coherentBuildsBeatNone_orc_violence` (≥58) | 55,62 (rojo) | verde | verde | 57,92 (rojo) |
  | `buildsWinDifferently_passChain` (≥1,11) | 1,11 (rojo) | 1,10 (rojo) | 1,07 (rojo) | 1,08 (rojo) |
  | `badBuildsLoseToNone` (techo 45) | verde | `elf_brawler`=48,12 (rojo) | `elf_brawler`=46,04, `elf_out_of_zone`=49,79 (rojo) | `orc_misplaced`=45,83, `elf_out_of_zone`=50,00 (rojo) |
  | **puertas rojas** | **3** | **3** | **3** | **4** |

  **Etiqueta correcta (Regla F): LIKELY ruido de puertas que ya operan en su margen, no CONFIRMED "sin
  relación causal".** El propio `docs/pendientes/BB-P.md` que una versión anterior de esta sección citaba
  como respaldo dice, de estas mismas puertas, que "no se puede afirmar sin medirlo que sean el mismo
  patrón — es la hipótesis a comprobar primero, no una conclusión": se estaba citando como confirmación un
  documento que marca la afirmación como sin confirmar. Lo que sí se sostiene: cada movimiento individual
  (1-2 puntos) cabe en el error típico de 2,3 puntos que `BuildGateTests.cs` documenta para estas celdas,
  y el efecto no es sistemáticamente en una sola dirección (una puerta mejora en una semilla y empeora en
  otra). El cambio puede costar una puerta roja adicional en algunas semillas (semilla 2: 3→4); no hay
  evidencia de que sea sistemático en las dos semillas medidas.
- `/Balance` (2.000 partidos, semilla 1): todas las bandas obligatorias de RT-056 dentro de rango, con y
  sin el arreglo.
- 760 tests no-puerta en verde — dos de ellos necesitaron ajuste, no por relación de código con este
  cambio sino por el mismo desplazamiento de RNG que las puertas de arriba:
  - `MatchRulesTests.AWhistledFoulRestartsWithAFreeKickForTheFouledTeamAndAnUnseenOneDoesNot`: con 50
    semillas fijas, `sameTeam` pasó de dentro de tolerancia a 9/167 (5,4 %, por encima del tope del 5 %).
    Remedido con 250 semillas: la tasa real es 2,6 % sin el arreglo y 3,7 % con él, las dos bajo el tope.
    50 partidos era muestra insuficiente para esta cola rara; subido a 150 (ver comentario en el test).
  - `RunEngineTests.ARunCanBePlayedFromStartToFinish`: la semilla fija (2) dejó de dar Victoria con el
    arreglo puesto (mismo mecanismo: una run entera es la medición más sensible a desplazamiento de RNG
    que existe en el proyecto, porque encadena 17-22 partidos). Cambiada a la semilla 4, que sí completa
    la run con el mismo rango de nodos y partidos — es la segunda vez que esta prueba necesita una semilla
    nueva por una razón de código ajena a lo que prueba (la primera fue un cambio de tamaño de plantilla,
    ver el comentario del test), y no será la última: cualquier cambio real de `/Sim` puede volver a
    tumbarla. Candidato, no resuelto aquí, a `docs/pendientes/BB-P.md`.

## Hermanos

Mismo hilo de causa (el reposicionamiento se resuelve por teletransporte en vez de por transición): [BB-A](./BB-A.md), [BB-D](./BB-D.md), [BB-L](./BB-L.md).

**Nuevos, encontrados por `independent-reviewer` al revisar este arreglo, ninguno corregido aquí:**

- **`EnforceRestartClearance` tiene el mismo bug, sin arreglar.** Su bucle (`Sim/Engine/MatchEngine.cs`,
  ~línea 2767) solo filtra `player.Team == _restartTeam || !player.OnPitch`: teletransporta a un jugador
  `Celebrating`, `KnockedDown` o `Injured` igual que a cualquier otro rival, en las cuatro reanudaciones
  con barrera que no son el saque de centro (banda, córner, puerta, falta). Un jugador derribado a menos
  de 2 casillas del punto de saque **sale despedido desde el suelo**. Es la misma causa que este ticket
  acaba de corregir en `ResetPositions`, en un sitio distinto del mismo fichero.
- **`KnockedDown` en `ResetPositions`** (sacado de este arreglo, ver arriba): el mismo salto de posición
  que tenía el goleador celebrando, pero para un jugador derribado en la jugada del gol, con interacción
  sin medir con "olfato de sangre".
