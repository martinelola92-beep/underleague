# 0185 — Arranque y frenada en `/Sim` (BV-A H4)

Fecha: 3 oct 2026 · Estado: **implementada y APAGADA en los datos** (`tuning.movement.accelTicks` = 0): la medición
saca nueve tests de comportamiento de verde con 3, que hay que resolver antes de encenderla ·
Requisitos: RT-020, RT-023, RT-089, RF-050 · Ficha: [BV-A](../pendientes/BV-A.md) · Va después de la [ADR 0184](0184-una-colocacion-se-sostiene.md)

## Problema

BV-A H4 (CONFIRMED en `/Sim`): `MatchEngine.Move` daba a cada jugador el paso **constante** de `SpeedPerTick` desde el
primer tick y lo cortaba en seco al llegar. La velocidad por tick de la traza es bimodal (casi todo a 0 o a 2,0-2,5 c/s)
y hay cientos de *paso-0-paso* por partido (carrera, un tick quieto, carrera).

## Decisión (implementada)

Un dato, `tuning.movement.accelTicks` (0 = el motor de antes, bit a bit; lo fija
`MobNarrowingTests.WithTheNewRulesOffEveryTraceIsTheOneBeforeAdr0184PlusBoA`). Con él, `Move` (`RampedStep`) calcula el
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

## Medición con `accelTicks` = 3, contra el mismo árbol con 0

**Lo que buscaba (la sonda limpia de la ADR 0184, 40 partidos):** inversiones a velocidad de carrera **0,091 → 0,004 por
jugador y segundo**, *paso-0-paso* **~19.900 → 269** en 40 partidos. La velocidad deja de ser bimodal.

Lote 10.000 × 2 semillas (media ± error típico por partido):

| | s1 con 0 | s1 con 3 | s2 con 0 | s2 con 3 |
|---|---|---|---|---|
| goles | 2,480 ± 0,013 | 2,494 ± 0,013 | 2,090 ± 0,013 | 2,150 ± 0,013 |
| tiros | 9,32 | 9,14 | 8,50 | 8,42 |
| entradas | 9,27 ± 0,04 | **10,36 ± 0,04** | 11,20 ± 0,04 | **12,11 ± 0,05** |
| faltas | 6,97 | 7,45 | 4,60 | 5,02 |
| lesiones | 0,744 ± 0,009 | 0,790 ± 0,009 | 0,415 ± 0,007 | 0,429 ± 0,007 |

Runs completas (`--full-runs 240`, semillas 1-3, contextual, 720 runs por brazo): muertes por partido **0,151 ± 0,008 →
0,142 ± 0,007**; lesiones propias por run 3,47 → 3,52 ± 0,14; `runWinRate` 14,6 / 17,5 / 16,7 → 14,6 / 15,8 / 16,7.

**Por qué no se enciende todavía:** con 3, el bucle de tests da **9 rojas**, y varias son de comportamiento:

- `KeeperAreaTests.TheAreaIsEmptyWhenTheGoalKickIsTaken`: 7 jugadores dentro del área al sacar de puerta (salen más
  despacio arrancando desde parado);
- `PenaltyAreaSymmetryTests`: faltas de penalti muy asimétricas entre las filas 1 y 5 (53 frente a 180 en 9.000 partidos);
- `DancingTeammatesTests` (×2): vuelve una racha de baile de 33 fotogramas en cobertura;
- `EmergentChainTests`: duelos aéreos demasiado raros (18 en 200 partidos);
- `ForwardOffBallTests`, `RepeatTackleReachTests.TheRealCaseNoLongerFreezes`, `ShotHeightTests` (un balón muerto en el
  marco): casos con semilla que cambian;
- `PositioningHoldTests` (control): sin sostenida la sonda ve sólo un 25 % de deshechas, porque el arranque ya quita la
  oscilación (no es una regresión: el control da por supuesto el motor sin arranque).

Ninguna se ha investigado aún; la de la simetría de filas pide `gameplay-debug` antes que nada (una asimetría así no
la explica la física simétrica del arranque). Con el dato a 0, el bucle está verde y las huellas son las de `main`.

## Siguiente paso

Investigar las rojas con el dato encendido (empezando por la asimetría de filas y el área del saque de puerta), y medir
`elf_none` (BV-C) y `tools/barrido-detectores.sh 1000` con él. Las puertas no se han pasado con 3.

## Nota de `game-design-review` (diez preguntas, corta)

1 el jugador ve muñecos que arrancan y frenan en seco y van y vienen; 2 que aceleren y frenen como personas; 3 no es
regla nueva: es física de movimiento (RT-020); 4 `/Sim` (`Move`) y un dato; `/Game` sólo lee posiciones (ya suaviza con
Hermite, RT-014); 5 alternativa: sólo suavizar en `/Game` (hecho en BV-A; no cambia quién llega antes); 6 el mismo
paso constante está en `WalkTo` (no se toca); 7 trade-off: quien sale de parado pierde un paso, quien gira pierde
rapidez; 8 premia anticiparse y castiga el ir y venir; 9 degeneración medida: más entradas (+10 %) y las rojas de arriba;
10 valores conocidos con control, sonda, lote, runs completas; faltan barrido y puertas.
